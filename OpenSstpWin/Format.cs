using System;
using System.Globalization;

namespace OpenSstpWin
{
    internal static class Format
    {
        private static readonly string[] ByteUnits = { "B", "KB", "MB", "GB", "TB" };
        private static readonly string[] SpeedUnits = { "B/s", "KB/s", "MB/s", "GB/s", "TB/s" };

        private static string Scale(double raw, string[] units)
        {
            double value = Math.Max(0.0, raw);
            int index = 0;

            while (value >= 1024.0 && index < units.Length - 1)
            {
                value /= 1024.0;
                index++;
            }

            string pattern = index == 0 ? "0" : value < 10.0 ? "0.00" : value < 100.0 ? "0.0" : "0";

            return value.ToString(pattern, CultureInfo.InvariantCulture) + " " + units[index];
        }

        internal static string Bytes(double bytes) => Scale(bytes, ByteUnits);

        internal static string Speed(double bytesPerSecond) => Scale(bytesPerSecond, SpeedUnits);
    }
}
