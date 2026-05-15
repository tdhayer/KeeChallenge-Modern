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
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Security;
using System.Runtime.ConstrainedExecution;
using System.IO;
using System.ComponentModel;
using System.Security.Cryptography;
using KeePassLib.Utility;

namespace KeeChallenge
{
    public enum YubiSlot
    {
        SLOT1 = 0,
        SLOT2 = 1
    };

    public class YubiWrapper : IChallengeResponseProvider
    {
        public const uint yubiRespLen = 20;
        private const uint yubiBuffLen = 64;
        private const uint LOAD_LIBRARY_SEARCH_DEFAULT_DIRS = 0x00001000;
        private const uint LOAD_LIBRARY_SEARCH_USER_DIRS = 0x00000400;

        public uint ResponseLength { get { return yubiRespLen; } }

        private IReadOnlyList<string> nativeDLLs = new List<string>() { "libykpers-1-1.dll", "libyubikey-0.dll", "libjson-0.dll", "libjson-c-2.dll" };

        private static readonly IReadOnlyDictionary<string, string> nativeDllHashes32 =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "libjson-0.dll", "74B9AE9167321E7F1A73419A356148F0FA3BCC06B0CC23F21DE9AB0D059AEA2D" },
                { "libjson-c-2.dll", "D346EA2FD1C12F33BC366AA7ABDED0439047471F37D34BE4C551C28A0CEFEE5B" },
                { "libykpers-1-1.dll", "97B347F7AC217F8E33A94FF10AE6E26952C97F89963A5FCB47EDC2AAC800DCC6" },
                { "libyubikey-0.dll", "D39849F504460F8AF671C0056B17291B66C5A5D6B41FE6B7340EB071CDF85E63" }
            };

        private static readonly IReadOnlyDictionary<string, string> nativeDllHashes64 =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "libjson-0.dll", "142CCC9416BCD1E811FA544DCD605CD4AFAB99053061E5F14C102AC5BE5A4E7A" },
                { "libjson-c-2.dll", "8CE09E34E741E3F07B8EB3DA44180C6B3EB7CB88604843B5F8D39386EEC2A287" },
                { "libykpers-1-1.dll", "C93C2CC2BB72965591BC4F6E274A187694D74C16966B9E7E99B6A5AFB4513E06" },
                { "libyubikey-0.dll", "A5E6949A09A2E145A1A5B7CBC883C952791A42B9D97EC070FE29294D3E12F5E9" }
            };

        private static bool is64BitProcess = (IntPtr.Size == 8);

        private static bool IsLinux
        {
            get
            {
                int p = (int)Environment.OSVersion.Platform;
                return (p == 4) || (p == 6) || (p == 128);
            }
        }

        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                string dir = Path.GetDirectoryName(path);
                if (dir == null) throw new InvalidOperationException("Unable to determine assembly directory.");
                return dir;
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetDefaultDllDirectories(uint DirectoryFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AddDllDirectory(string NewDirectory);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool RemoveDllDirectory(IntPtr Cookie);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true, ExactSpelling = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string methodName);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail), DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string moduleName);

        [SecurityCritical]
        internal static bool DoesWin32MethodExist(string moduleName, string methodName)
        {
            IntPtr moduleHandle = GetModuleHandle(moduleName);
            if (moduleHandle == IntPtr.Zero)
            {
                return false;
            }
            return (GetProcAddress(moduleHandle, methodName) != IntPtr.Zero);
        }
        
        private static ReadOnlyCollection<byte> slots = new ReadOnlyCollection<byte>(new List<byte>()
        {
            0x30, //SLOT_CHAL_HMAC1
            0x38  //SLOT_CHAL_HMAC2
        });

        private IntPtr yk = IntPtr.Zero;
        private IntPtr dllDirectoryCookie = IntPtr.Zero;
        private bool usingLegacySetDllDirectory = false;

        private static string ComputeSha256(string filePath)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream fs = File.OpenRead(filePath))
            {
                byte[] hash = sha.ComputeHash(fs);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        private static bool ValidateNativeDllSet(string directory, bool use64BitHashes, out string error)
        {
            error = string.Empty;
            IReadOnlyDictionary<string, string> expectedHashes = use64BitHashes ? nativeDllHashes64 : nativeDllHashes32;

            foreach (KeyValuePair<string, string> expected in expectedHashes)
            {
                string dllPath = Path.Combine(directory, expected.Key);
                if (!File.Exists(dllPath))
                {
                    error = string.Format("Missing required native library: {0}", dllPath);
                    return false;
                }

                string actualHash = ComputeSha256(dllPath);
                if (!string.Equals(actualHash, expected.Value, StringComparison.OrdinalIgnoreCase))
                {
                    error = string.Format("Integrity check failed for native library: {0}", dllPath);
                    return false;
                }
            }

            return true;
        }

        private void ConfigureNativeDllSearchPath(string nativeDir)
        {
            // Prefer per-process user DLL directories over process-wide SetDllDirectory.
            if (DoesWin32MethodExist("kernel32.dll", "SetDefaultDllDirectories") &&
                DoesWin32MethodExist("kernel32.dll", "AddDllDirectory"))
            {
                uint flags = LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS;
                if (!SetDefaultDllDirectories(flags))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set secure DLL search directories.");
                }

                dllDirectoryCookie = AddDllDirectory(nativeDir);
                if (dllDirectoryCookie == IntPtr.Zero)
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to add native DLL directory.");
                }

                usingLegacySetDllDirectory = false;
                return;
            }

            if (!DoesWin32MethodExist("kernel32.dll", "SetDllDirectoryW"))
            {
                throw new PlatformNotSupportedException("KeeChallenge requires Windows XP Service Pack 1 or greater");
            }

            if (!SetDllDirectory(nativeDir))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to set native DLL directory.");
            }

            usingLegacySetDllDirectory = true;
        }

        public bool Init()
        {
            try
            { 
                if (!IsLinux) //no DLL Hell on Linux!
                {     
                    // Legacy cleanup (v1.0.2 and prior placed DLLs directly in cwd).
                    // We no longer delete automatically; warn the user instead to avoid
                    // destructive side-effects in locked-down or shared KeePass installs.
                    {
                        var legacyFiles = nativeDLLs
                            .Select(s => Path.Combine(Environment.CurrentDirectory, s))
                            .Where(File.Exists)
                            .ToList();
                        if (legacyFiles.Count > 0)
                        {
                            string warn = "KeeChallenge-Modern: legacy DLL files were found in the KeePass directory.\n" +
                                "Please delete them manually to avoid conflicts:\n" +
                                string.Join("\n", legacyFiles);
                            MessageService.ShowWarning(warn);
                            return false;
                        }
                    }


                    string _32BitDir = Path.Combine(AssemblyDirectory, "32bit");
                    string _64BitDir = Path.Combine(AssemblyDirectory, "64bit");
                    if (!Directory.Exists(_32BitDir) || !Directory.Exists(_64BitDir))
                    {
                        string err = String.Format("Error: one of the following directories is missing:\n{0}\n{1}\nPlease reinstall KeeChallenge and ensure that these directories are present", _32BitDir, _64BitDir);
                        MessageService.ShowWarning(err);
                        return false;
                    }

                    string nativeDir = is64BitProcess ? _64BitDir : _32BitDir;
                    string integrityError;
                    if (!ValidateNativeDllSet(nativeDir, is64BitProcess, out integrityError))
                    {
                        MessageService.ShowWarning("Native DLL integrity check failed.", integrityError);
                        return false;
                    }

                    ConfigureNativeDllSearchPath(nativeDir);
                }
                if (yk_init() != 1) return false;
                yk = yk_open_first_key();
                if (yk == IntPtr.Zero) return false;
            }
            catch (Exception e)
            {
                Debug.Assert(false, e.Message);
                MessageService.ShowWarning("Error connecting to yubikey!", e.Message);
                return false;
            }
           return true;
        }

        [DllImport("libykpers-1-1.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int yk_init();

        [DllImport("libykpers-1-1.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int yk_release();

        [DllImport("libykpers-1-1.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int yk_close_key(IntPtr yk);

        [DllImport("libykpers-1-1.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern IntPtr yk_open_first_key();

        [DllImport("libykpers-1-1.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        private static extern int yk_challenge_response(IntPtr yk, byte yk_cmd, int may_block, uint challenge_len, byte[] challenge, uint response_len, byte[] response);
               
        public bool ChallengeResponse(YubiSlot slot, byte[] challenge, out byte[] response)
        {
            response = new byte[yubiRespLen];
            if (yk == IntPtr.Zero) return false;

            byte[] temp = new byte[yubiBuffLen];
            try
            {
                int ret = yk_challenge_response(yk, slots[(int)slot], 1, (uint)challenge.Length, challenge, yubiBuffLen, temp);
                if (ret == 1)
                {
                    Array.Copy(temp, response, response.Length);
                    return true;
                }
                return false;
            }
            finally
            {
                Array.Clear(temp, 0, temp.Length);
            }
        }

        public void Close()
        {
            if (yk != IntPtr.Zero)
            {
                bool closedOk = yk_close_key(yk) == 1;
                yk = IntPtr.Zero;
                bool releasedOk = yk_release() == 1;
                Debug.Assert(closedOk && releasedOk, "Error closing YubiKey");
            }

            if (dllDirectoryCookie != IntPtr.Zero)
            {
                bool removed = RemoveDllDirectory(dllDirectoryCookie);
                Debug.Assert(removed, "Error removing native DLL directory cookie");
                dllDirectoryCookie = IntPtr.Zero;
            }

            if (usingLegacySetDllDirectory)
            {
                // Best-effort reset for legacy fallback mode.
                SetDllDirectory(null);
                usingLegacySetDllDirectory = false;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
