using System;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace OpenSstpWin
{
    internal sealed class RunResult
    {
        internal int Code;
        internal string Output = "";
    }

    /// <summary>
    /// Uses the SSTP client built into Windows (Add-VpnConnection + rasdial).
    /// No VPN protocol code lives in this program.
    /// </summary>
    internal static class VpnController
    {
        internal const string ConnectionName = "OpenSSTP-Win";

        static VpnController()
        {
            try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); } catch { }
        }

        private static Encoding OutputEncoding()
        {
            try { return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage); }
            catch { return Encoding.UTF8; }
        }

        private static async Task<RunResult> Run(string file, params string[] args)
        {
            var psi = new ProcessStartInfo(file)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            Encoding encoding = OutputEncoding();
            psi.StandardOutputEncoding = encoding;
            psi.StandardErrorEncoding = encoding;
            foreach (string arg in args) psi.ArgumentList.Add(arg);

            try
            {
                using Process process = Process.Start(psi)!;
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                return new RunResult { Code = process.ExitCode, Output = (await stdout + await stderr).Trim() };
            }
            catch (Exception ex)
            {
                return new RunResult { Code = -1, Output = ex.Message };
            }
        }

        private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";

        /// <summary>Creates or updates the Windows VPN entry.</summary>
        internal static Task<RunResult> PrepareConnection(string server, bool useDefaultGateway)
        {
            string split = useDefaultGateway ? "$false" : "$true";
            string name = Quote(ConnectionName);
            string address = Quote(server);

            string script =
                "$ErrorActionPreference='Stop'; " +
                "if (Get-VpnConnection -Name " + name + " -ErrorAction SilentlyContinue) { " +
                "Set-VpnConnection -Name " + name + " -ServerAddress " + address +
                " -TunnelType Sstp -SplitTunneling " + split + " -Force " +
                "} else { " +
                "Add-VpnConnection -Name " + name + " -ServerAddress " + address +
                " -TunnelType Sstp -EncryptionLevel Required -AuthenticationMethod MSChapv2" +
                " -SplitTunneling " + split + " -Force }";

            return Run("powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script);
        }

        internal static Task<RunResult> Dial(string user, string password)
        {
            return Run("rasdial.exe", ConnectionName, user, password);
        }

        internal static Task<RunResult> HangUp()
        {
            return Run("rasdial.exe", ConnectionName, "/disconnect");
        }

        /// <summary>The adapter Windows creates for the active VPN connection, or null.</summary>
        internal static NetworkInterface? FindActiveInterface()
        {
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus == OperationalStatus.Up &&
                        string.Equals(ni.Name, ConnectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        return ni;
                    }
                }
            }
            catch { }

            return null;
        }
    }
}
