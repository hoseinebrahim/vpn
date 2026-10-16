using System;
using System.Drawing;
using System.Net.NetworkInformation;
using System.Windows.Forms;

namespace OpenSstpWin
{
    internal sealed class MainForm : Form
    {
        private readonly TextBox txtServer = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox txtUser = new TextBox { Dock = DockStyle.Fill };
        private readonly TextBox txtPass = new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
        private readonly CheckBox chkGateway = new CheckBox { Text = "Route all traffic through VPN", AutoSize = true, Checked = true };
        private readonly Button btnConnect = new Button { Text = "Connect", Dock = DockStyle.Fill, Height = 34 };
        private readonly Label lblStatus = new Label { AutoSize = true, Font = new Font("Segoe UI", 10f, FontStyle.Bold) };

        private readonly Label lblDownSpeed = new Label { AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold) };
        private readonly Label lblDownTotal = new Label { AutoSize = true };
        private readonly Label lblUpSpeed = new Label { AutoSize = true, Font = new Font("Segoe UI", 16f, FontStyle.Bold) };
        private readonly Label lblUpTotal = new Label { AutoSize = true };

        private readonly GraphControl graph = new GraphControl { Dock = DockStyle.Fill, MinimumSize = new Size(300, 180) };
        private readonly RadioButton rb1 = new RadioButton { Text = "1 min", AutoSize = true, Checked = true };
        private readonly RadioButton rb5 = new RadioButton { Text = "5 min", AutoSize = true };
        private readonly RadioButton rb30 = new RadioButton { Text = "30 min", AutoSize = true };

        private readonly TextBox txtLog = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Height = 70,
        };

        private readonly Timer timer = new Timer { Interval = 1000 };
        private readonly TrafficMonitor monitor = new TrafficMonitor();
        private bool busy;
        private int windowSeconds = 60;

        internal MainForm()
        {
            Text = "Open SSTP Client for Windows";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(480, 640);
            MinimumSize = new Size(440, 600);
            Font = new Font("Segoe UI", 9f);

            BuildLayout();
            LoadSettings();

            btnConnect.Click += OnConnectClicked;
            rb1.CheckedChanged += (_, _) => OnRangeChanged(rb1, 60);
            rb5.CheckedChanged += (_, _) => OnRangeChanged(rb5, 300);
            rb30.CheckedChanged += (_, _) => OnRangeChanged(rb30, 1800);

            timer.Tick += (_, _) => Poll();
            timer.Start();
            Poll();
        }

        private void BuildLayout()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, Padding = new Padding(12),
            };
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 80f));

            var form = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 3 };
            form.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            form.Controls.Add(new Label { Text = "Server", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
            form.Controls.Add(txtServer, 1, 0);
            form.Controls.Add(new Label { Text = "Username", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
            form.Controls.Add(txtUser, 1, 1);
            form.Controls.Add(new Label { Text = "Password", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 2);
            form.Controls.Add(txtPass, 1, 2);

            var actions = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 1, RowCount = 3 };
            actions.Controls.Add(chkGateway, 0, 0);
            actions.Controls.Add(btnConnect, 0, 1);
            actions.Controls.Add(lblStatus, 0, 2);

            var stats = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 4 };
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            var downTitle = new Label { Text = "\u2193 DOWNLOAD", AutoSize = true, ForeColor = GraphControl.DownColor, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            var upTitle = new Label { Text = "\u2191 UPLOAD", AutoSize = true, ForeColor = GraphControl.UpColor, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
            stats.Controls.Add(downTitle, 0, 0);
            stats.Controls.Add(upTitle, 1, 0);
            stats.Controls.Add(lblDownSpeed, 0, 1);
            stats.Controls.Add(lblUpSpeed, 1, 1);
            stats.Controls.Add(lblDownTotal, 0, 2);
            stats.Controls.Add(lblUpTotal, 1, 2);

            var ranges = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.LeftToRight };
            ranges.Controls.Add(rb1);
            ranges.Controls.Add(rb5);
            ranges.Controls.Add(rb30);

            root.Controls.Add(form, 0, 0);
            root.Controls.Add(actions, 0, 1);
            root.Controls.Add(stats, 0, 2);
            root.Controls.Add(graph, 0, 3);
            root.Controls.Add(ranges, 0, 4);
            root.Controls.Add(txtLog, 0, 5);

            Controls.Add(root);
        }

        private void LoadSettings()
        {
            Settings s = Settings.Load();
            txtServer.Text = s.Server;
            txtUser.Text = s.Username;
            chkGateway.Checked = s.UseDefaultGateway;
        }

        private void SaveSettings()
        {
            new Settings
            {
                Server = txtServer.Text.Trim(),
                Username = txtUser.Text.Trim(),
                UseDefaultGateway = chkGateway.Checked,
            }.Save();
        }

        private void Log(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;

            txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + message.Trim() + Environment.NewLine);
        }

        private void OnRangeChanged(RadioButton button, int seconds)
        {
            if (!button.Checked) return;

            windowSeconds = seconds;
            RefreshView();
        }

        private async void OnConnectClicked(object? sender, EventArgs e)
        {
            if (busy) return;

            if (VpnController.FindActiveInterface() != null)
            {
                SetBusy(true, "Disconnecting...");
                RunResult result = await VpnController.HangUp();
                Log(result.Output);
                SetBusy(false, null);
                Poll();
                return;
            }

            string server = txtServer.Text.Trim();
            string user = txtUser.Text.Trim();
            if (server.Length == 0 || user.Length == 0 || txtPass.Text.Length == 0)
            {
                MessageBox.Show(this, "Enter server, username and password.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SaveSettings();
            SetBusy(true, "Connecting...");

            RunResult prepared = await VpnController.PrepareConnection(server, chkGateway.Checked);
            if (prepared.Code != 0)
            {
                Log("Could not create the VPN entry: " + prepared.Output);
                SetBusy(false, null);
                return;
            }

            Log("Dialing " + VpnController.ConnectionName + " as " + user + " (password hidden)");
            RunResult dial = await VpnController.Dial(user, txtPass.Text);
            Log(dial.Output);

            SetBusy(false, null);
            Poll();
        }

        private void SetBusy(bool value, string? status)
        {
            busy = value;
            btnConnect.Enabled = !value;
            if (status != null) lblStatus.Text = status;
        }

        private void Poll()
        {
            NetworkInterface? ni = VpnController.FindActiveInterface();

            if (ni != null)
            {
                try
                {
                    IPInterfaceStatistics stats = ni.GetIPStatistics();
                    if (!monitor.Active) monitor.Start(stats.BytesReceived, stats.BytesSent);
                    monitor.Sample(stats.BytesReceived, stats.BytesSent);
                }
                catch (Exception ex)
                {
                    Log("Cannot read adapter statistics: " + ex.Message);
                    monitor.Stop();
                }
            }
            else if (monitor.Active)
            {
                monitor.Stop();
            }

            if (!busy)
            {
                lblStatus.Text = ni != null ? "Connected" : "Not connected";
                lblStatus.ForeColor = ni != null ? Color.SeaGreen : SystemColors.GrayText;
                btnConnect.Text = ni != null ? "Disconnect" : "Connect";
            }

            RefreshView();
        }

        private void RefreshView()
        {
            lblDownSpeed.Text = Format.Speed(monitor.RxSpeed);
            lblUpSpeed.Text = Format.Speed(monitor.TxSpeed);
            lblDownTotal.Text = "Total: " + Format.Bytes(monitor.TotalRx);
            lblUpTotal.Text = "Total: " + Format.Bytes(monitor.TotalTx);
            graph.SetData(monitor.Samples, windowSeconds);
        }
    }
}
