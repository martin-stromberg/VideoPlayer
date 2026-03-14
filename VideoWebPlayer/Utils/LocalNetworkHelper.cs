using System.Net;
using System.Net.Sockets;

namespace VideoWebPlayer.Utils
{
    /// <summary>
    /// Helper utilities for detecting local network addresses.
    /// </summary>
    public static class LocalNetworkHelper
    {
        /// <summary>
        /// Determines whether the provided IP address is within local network ranges.
        /// </summary>
        /// <param name="ip">The IP address to evaluate.</param>
        /// <returns><c>true</c> when the address is local; otherwise <c>false</c>.</returns>
        public static bool IsLocalIpAddress(IPAddress ip)
        {
            if (IPAddress.IsLoopback(ip))
                return true;

            // IPv4
            if (ip.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] bytes = ip.GetAddressBytes();
                // 10.0.0.0/8
                if (bytes[0] == 10)
                    return true;
                // 192.168.0.0/16
                if (bytes[0] == 192 && bytes[1] == 168)
                    return true;
                // 172.16.0.0/12
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                    return true;
            }
            // IPv6 local
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal)
                return true;

            return false;
        }
    }
}