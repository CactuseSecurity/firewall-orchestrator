
using System;
using System.Net;
using System.Numerics;
using System.Linq;
using System.Net.Sockets;
using System.Collections;
using System.Text.RegularExpressions;

namespace FWO.Basics
{
    public static class Extensions
    {
        public static bool TrySplit(this string text, char separator, int index, out string output)
        {
            string[] splits = text.Split(separator);

            output = "";

            if (splits.Length < 2 || splits.Length < index + 1)
                return false;

            output = splits[index];

            return true;
        }

        public static bool TrySplit(this string text, char separator, out int length)
        {
            string[] splits = text.Split(separator);

            length = 0;

            if (splits.Length < 2)
                return false;

            length = splits.Length;

            return true;
        }

        // private static string StripOffNetmask(this string ip)
        // {
        //     if (TryGetNetmask(ip, out string netmask))
        //         return ip.Replace(netmask, "");

        //     return ip;
        // }

        public static bool TryGetNetmask(this string ip, out string netmask)
        {
            netmask = "";

            Match match = Regex.Match(ip, @"(\/[\d\.\:]+)\D?");

            if (match.Success)
                netmask = match.Groups[1].Value;

            return match.Success;
        }

        public static bool IsIPv4(this string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out IPAddress? addr))
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                    return true;

                return false;
            }

            return false;
        }

        public static bool IsIPv6(this string ipAddress)
        {
            if (IPAddress.TryParse(ipAddress, out IPAddress? addr))
            {
                if (addr.AddressFamily == AddressFamily.InterNetworkV6)
                    return true;

                return false;
            }

            return false;
        }
    }
}

