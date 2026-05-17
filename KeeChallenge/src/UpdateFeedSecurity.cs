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
using System.Security.Cryptography;
using System.Text;

using KeePass.Util;

namespace KeeChallenge
{
    internal static class UpdateFeedSecurity
    {
        internal const string UpdateFeedUrl = "https://raw.githubusercontent.com/tdhayer/KeeChallenge-Modern/master/VERSION";

        private const string PinnedPublicKeyXml = "<RSAKeyValue><Modulus>rzEcRac2BKWp6XQeZe/+J2QCumnaDRx/NWpfIpsTf11oE5a+NF91CJNld7jQ/DEQzPRZrSKWqWt8R44orxk5NAFARHWVhMY+gISFJKXwVzp4sOAPNezZ0iSPg90uyeI8iT7Yn98ygZ8c5gckc/vSPYIcVdJhbFHjHpAxjwOxckZiOrGuE24mMTWSqJngAwLvSbLzpSKnszfzZd7aZY0sFjtfsnpakbNbgpkq3i3GDYDCxBBQ7t8NGf/mAQMg2qFI69p09eq87hcH/mGuUdtGcyzeFMb9CUKM0kGatA9rQqqAllQoXigM1OUxEnbav6LDGXcVFcAW3ol7Sdg04k9Ja/1wDhfjY3CFEVIbH1R+XOuMoykBlzGnnDBls0OYBkNhvy2tmbFmhOFKIljLk4cwLvvQ++D1mm439fcp1eTr2Hs/YHrzHKt+Vkr104pTF/+EFC94vi4NOBRmIlSyWLT76js/v/eGn3VmClJRHJqAXaO1wrYkToGobWozOYZ+v10FouEnKlZ3x11fWaxWL0/O0jIwhwnXIhPq7wYbuVTNTQKeC6cHAm7K9NcAku/pdz4z/ZeQcJUIwGHdD2FFXs1VE6i1ifGQyiVXy2ZUbzykqZhQ4GI6w2bhEq5gDDLBBKo473zSceUKOSV4JhA/pgVliflM5AshFvmDeuTVqJNmaE0=</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        private const string PinnedPublicKeySha256 = "074AD9A5CAD14462CEB2E67740B9CF7CE0B854AE49989842E64C3886EA2F999F";

        internal static bool TryConfigure(out string error)
        {
            error = string.Empty;

            try
            {
                string fingerprint = ComputeSha256Hex(PinnedPublicKeyXml);
                if (!string.Equals(fingerprint, PinnedPublicKeySha256, StringComparison.OrdinalIgnoreCase))
                {
                    error = "Pinned update signing key fingerprint mismatch.";
                    return false;
                }

                UpdateCheckEx.SetFileSigKey(UpdateFeedUrl, PinnedPublicKeyXml);
                return true;
            }
            catch (Exception ex)
            {
                Diagnostics.TraceException("Signed update feed configuration failed.", ex);
                error = "Unable to configure signed update feed verification.";
                return false;
            }
        }

        private static string ComputeSha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
                StringBuilder sb = new StringBuilder(hash.Length * 2);

                for (int i = 0; i < hash.Length; ++i)
                {
                    sb.Append(hash[i].ToString("X2"));
                }

                return sb.ToString();
            }
        }
    }
}