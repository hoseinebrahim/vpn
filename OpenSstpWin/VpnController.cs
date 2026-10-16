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

        /// <summary>
        /// Always recreates the Windows VPN entry from scratch so that stale or half-configured
        /// entries (and stale sessions) can never cause PPP/IPCP failures such as error 720.
        /// </summary>
        internal static Task<RunResult> PrepareConnection(string server, bool useDefaultGateway)
        {
            string split = useDefaultGateway ? "$false" : "$true";
            string name = Quote(ConnectionName);
            string address = Quote(server);

            string script =
                "$ErrorActionPreference='Stop'; " +
                "$n=" + name + "; " +
                "& rasdial.exe $n /disconnect | Out-Null; " +
                "if (Get-VpnConnection -Name $n -ErrorAction SilentlyContinue) { Remove-VpnConnection -Name $n -Force }; " +
                "Add-VpnConnection -Name $n -ServerAddress " + address +
                " -TunnelType Sstp -EncryptionLevel Required -AuthenticationMethod MSChapv2" +
                " -SplitTunneling " + split + " -Force";

            return Run("powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", script);
        }

        /// <summary>Returns the settings Windows actually holds for the entry (for troubleshooting).</summary>
        internal static Task<RunResult> Describe()
        {
            string script =
                "Get-VpnConnection -Name " + Quote(ConnectionName) + " | " +
                "Format-List ServerAddress,TunnelType,AuthenticationMethod,EncryptionLevel,SplitTunneling";

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
