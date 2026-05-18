/* KeeChallenge--Provides Yubikey challenge-response capability to Keepass
*  Copyright (C) 2014  Ben Rush
*  
*  This program is free software; you can redistribute it and/or
*  modify it under the terms of the GNU General Public License
*  as published by the Free Software Foundation; either version 3
*  of the License, or (at your option) any later version.
*  
*  This program is distributed in the hope that it will be useful,
*  but WITHOUT ANY WARRANTY; without even the implied warranty of
*  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
*  GNU General Public License for more details.
*  
*  You should have received a copy of the GNU General Public License
*  along with this program; if not, write to the Free Software
*  Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

using System;
using System.Text;

namespace KeeChallenge
{
    internal static class SecretInputParser
    {
        private const int ExpectedSecretBytes = KeeChallengeProv.secretLenBytes;
        private const int ExpectedSecretHexLength = ExpectedSecretBytes * 2;

        internal static bool TryParseSecret(string rawInput, out byte[] secret, out string userError)
        {
            secret = null;
            userError = string.Empty;

            string normalized = RemoveWhitespace(rawInput);
            if (normalized.Length != ExpectedSecretHexLength)
            {
                userError = "Error: secret must be exactly 20 bytes (40 hex characters).";
                return false;
            }

            for (int i = 0; i < normalized.Length; ++i)
            {
                if (!Uri.IsHexDigit(normalized[i]))
                {
                    userError = "Error: secret must contain only hexadecimal characters (0-9, A-F).";
                    return false;
                }
            }

            secret = new byte[ExpectedSecretBytes];
            for (int i = 0; i < ExpectedSecretBytes; ++i)
            {
                int high = HexNibble(normalized[i * 2]);
                int low = HexNibble(normalized[(i * 2) + 1]);
                secret[i] = (byte)((high << 4) | low);
            }

            return true;
        }

        private static string RemoveWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; ++i)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    sb.Append(value[i]);
                }
            }

            return sb.ToString();
        }

        private static int HexNibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return (c - 'a') + 10;
            return (c - 'A') + 10;
        }
    }
}