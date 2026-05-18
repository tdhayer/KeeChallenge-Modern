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
using System.IO;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Xml;
using System.Text.RegularExpressions;
using System.Linq;

using KeePassLib.Keys;
using KeePassLib.Utility;
using KeePassLib.Cryptography;
using KeePassLib.Serialization;

namespace KeeChallenge
{
    public sealed class KeeChallengeProv : KeyProvider
    {
        public const string m_name = "Yubikey challenge-response";
        public const int keyLenBytes = 20;
        public const int challengeLenBytes = 64;
        public const int secretLenBytes = 20;
        public const int MetadataVersion = 2;
        private const long MaxMetadataFileBytes = 64 * 1024;
        private const long MaxMetadataXmlChars = 64 * 1024;
        private const int MaxEncryptedSecretBytes = 256;
        private const string MetadataReadErrorMessage = "Error: metadata could not be read correctly. Reverting to Recovery Mode.";
        private bool m_LT64 = false;

        //If variable length challenges are enabled, a 63 byte challenge is sent instead.
        //See GenerateChallenge() and http://forum.yubico.com/viewtopic.php?f=16&t=1078
        public bool LT64
        {
            get { return m_LT64; }
            set { m_LT64 = value; }
        }

        public YubiSlot YubikeySlot
        {
            get;
            set;
        }

        public KeeChallengeProv()
        {
            YubikeySlot = YubiSlot.SLOT2;
        }

        private IOConnectionInfo mInfo;

        public override string Name
        {
            get { return m_name; }
        }

        public override bool SecureDesktopCompatible
        {
            get
            {
                return true;
            }
        }

        public override byte[] GetKey(KeyProviderQueryContext ctx)
        {
            if (ctx == null)
            {
                Debug.Assert(false);
                return null;
            }

            mInfo = ctx.DatabaseIOInfo.CloneDeep();
            string db = mInfo.Path;
            Regex rgx = new Regex(@"\.kdbx$");
            mInfo.Path = rgx.Replace(db, ".xml");

            if (Object.ReferenceEquals(db,mInfo.Path)) //no terminating .kdbx found-> maybe using keepass 1? should never happen...
            {
                MessageService.ShowWarning("Invalid database. KeeChallenge only works with .kdbx files.");
                return null;
            }


            try
            {
                if (ctx.CreatingNewKey) return Create(ctx);
                return Get(ctx);
            }
            catch (Exception ex)
            {
                Diagnostics.TraceException("GetKey failed.", ex);
                MessageService.ShowWarning("KeeChallenge could not complete key processing. Retry or use Recovery Mode if needed.");
            }

            return null;
        }

        public byte[] GenerateChallenge()
        {
            byte[] chal =  CryptoRandom.Instance.GetRandomBytes(challengeLenBytes);  
            if (LT64)
            {
                chal[challengeLenBytes - 2] = (byte)~chal[challengeLenBytes - 1];
            }

            return chal;
        }

        public byte[] GenerateResponse(byte[] challenge, byte[] key)
        {
            byte[] challengeForHmac = challenge;
            try
            {
                if (LT64)
                    challengeForHmac = challenge.Take(challengeLenBytes - 1).ToArray();

                using (HMACSHA1 hmac = new HMACSHA1(key))
                {
                    return hmac.ComputeHash(challengeForHmac);
                }
            }
            finally
            {
                if (!Object.ReferenceEquals(challengeForHmac, challenge))
                {
                    SensitiveData.Clear(challengeForHmac);
                }
            }
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null) return false;
            if (left.Length != right.Length) return false;

            int diff = 0;
            for (int i = 0; i < left.Length; ++i)
            {
                diff |= left[i] ^ right[i];
            }

            return diff == 0;
        }

        private bool EncryptAndSave(byte[] secret)
        {
            byte[] challenge = null;
            byte[] resp = null;
            byte[] key = null;
            byte[] secretHash = null;
            byte[] iv = null;
            byte[] encrypted = null;
            Stream s = null;
            try
            {
                //generate a random challenge for use next time
                challenge = GenerateChallenge();

                //generate the expected HMAC-SHA1 response for the challenge based on the secret
                resp = GenerateResponse(challenge, secret);

                //use the response to encrypt the secret
                using (SHA256 sha = SHA256.Create())
                {
                    key = sha.ComputeHash(resp); // get a 256 bit key from the 160 bit hmac response
                    secretHash = sha.ComputeHash(secret);
                }

                using (Aes aes = Aes.Create())
                {
                    aes.KeySize = key.Length * sizeof(byte) * 8; //pedantic, but foolproof
                    aes.Key = key;
                    aes.GenerateIV();
                    aes.Padding = PaddingMode.PKCS7;
                    iv = (byte[])aes.IV.Clone();

                    using (ICryptoTransform enc = aes.CreateEncryptor())
                    {
                        using (MemoryStream msEncrypt = new MemoryStream())
                        {
                            using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, enc, CryptoStreamMode.Write))
                            {
                                csEncrypt.Write(secret, 0, secret.Length);
                                csEncrypt.FlushFinalBlock();

                                encrypted = msEncrypt.ToArray();
                            }
                        }
                    }
                }

                FileTransactionEx ft = new FileTransactionEx(mInfo,
                    false);
                s = ft.OpenWrite();
               
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.CloseOutput = true;
                settings.Indent = true;
                settings.IndentChars = "\t";
                settings.NewLineOnAttributes = true;

                using (XmlWriter xml = XmlWriter.Create(s, settings))
                {
                    xml.WriteStartDocument();
                    xml.WriteStartElement("data");

                    xml.WriteElementString("version", MetadataVersion.ToString());

                    xml.WriteStartElement("aes");
                    xml.WriteElementString("encrypted", Convert.ToBase64String(encrypted));
                    xml.WriteElementString("iv", Convert.ToBase64String(iv));
                    xml.WriteEndElement();

                    xml.WriteElementString("challenge", Convert.ToBase64String(challenge));
                    xml.WriteElementString("verification", Convert.ToBase64String(secretHash));
                    xml.WriteElementString("lt64", LT64.ToString());

                    xml.WriteEndElement();
                    xml.WriteEndDocument();
                }
  
                ft.CommitWrite();  
            }
            catch (Exception ex)
            {
                Diagnostics.TraceException("EncryptAndSave failed for metadata path: " + mInfo.Path, ex);
                MessageService.ShowWarning("Error: unable to write KeeChallenge metadata.");
                return false;
            }    
            finally
            {
                if (s != null)
                    s.Close();

                SensitiveData.Clear(challenge, resp, key, secretHash, iv, encrypted);
            }

            return true;
        }

        private static bool DecryptSecret(byte[] encryptedSecret, byte[] yubiResp, byte[] iv, byte[] verification, out byte[] secret)
        {
            //use the response to decrypt the secret
            byte[] key = null;
            byte[] secretHash = null;
            secret = new byte[keyLenBytes];
            try
            {
                using (SHA256 sha = SHA256.Create())
                {
                    key = sha.ComputeHash(yubiResp); // get a 256 bit key from the 160 bit hmac response
                }

                try
                {
                    using (Aes aes = Aes.Create())
                    {
                        aes.KeySize = key.Length * sizeof(byte) * 8; //pedantic, but foolproof
                        aes.Key = key;
                        aes.IV = iv;
                        aes.Padding = PaddingMode.PKCS7;

                        using (ICryptoTransform dec = aes.CreateDecryptor())
                        {
                            using (MemoryStream msDecrypt = new MemoryStream(encryptedSecret))
                            {
                                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, dec, CryptoStreamMode.Read))
                                {
                                    csDecrypt.Read(secret, 0, secret.Length);
                                }
                            }
                        }
                    }
                }
                catch (System.Security.Cryptography.CryptographicException)
                {
                    // Corrupt ciphertext (e.g. bad padding) — treat as wrong key / corrupt data
                    SensitiveData.Clear(secret);
                    return false;
                }

                using (SHA256 sha = SHA256.Create())
                {
                    secretHash = sha.ComputeHash(secret);
                }

                if (!FixedTimeEquals(secretHash, verification))
                {
                    SensitiveData.Clear(secret);
                    return false;
                }

                return true;
            }
            finally
            {
                SensitiveData.Clear(key, secretHash);
            }
        }
        
        private static void ClearTemporarySecretState(params byte[][] buffers)
        {
            SensitiveData.Clear(buffers);
        }

        private bool ReadEncryptedSecret(out byte[] encryptedSecret, out byte[] challenge, out byte[] iv, out byte[] verification)
        {
            encryptedSecret = null;
            iv = null;
            challenge = null;
            verification = null;
            
            LT64 = false; //default to false if not found

            XmlReader xml = null;
            Stream s = null;
            try
            {
                s = IOConnection.OpenRead(mInfo);

                if (s.CanSeek && s.Length > MaxMetadataFileBytes)
                {
                    throw new InvalidDataException("Metadata file exceeds maximum allowed size.");
                }

                //read file

                XmlReaderSettings settings = new XmlReaderSettings();
                settings.CloseInput = true;
                settings.DtdProcessing = DtdProcessing.Prohibit;
                settings.XmlResolver = null;
                settings.MaxCharactersInDocument = MaxMetadataXmlChars;
                settings.MaxCharactersFromEntities = 0;
                xml = XmlReader.Create(s, settings);
                
                while (xml.Read())
                {
                    if (xml.IsStartElement())
                    {
                        switch (xml.Name)
                        {
                            case "version":
                                xml.Read(); // consume value; reserved for future behavioral changes
                                break;
                            case "encrypted":
                                xml.Read();
                                encryptedSecret = Convert.FromBase64String(xml.Value.Trim());
                                break;
                            case "iv":
                                xml.Read();
                                iv = Convert.FromBase64String(xml.Value.Trim());
                                break;
                            case "challenge":
                                xml.Read();
                                challenge = Convert.FromBase64String(xml.Value.Trim());
                                break;
                            case "verification":
                                xml.Read();
                                verification = Convert.FromBase64String(xml.Value.Trim());
                                break;
                            case "lt64":
                                xml.Read();
                                if (!bool.TryParse(xml.Value.Trim(), out m_LT64)) throw new Exception("Unable to parse LT64 flag");
                                break;

                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Diagnostics.TraceException("ReadEncryptedSecret failed for metadata path: " + mInfo.Path, ex);
                MessageService.ShowWarning(MetadataReadErrorMessage);
                return false;
            }
            finally
            {
                if (xml != null)
                    xml.Close();
                if (s != null)
                    s.Close();
            }

            int expectedChallengeLength = LT64 ? challengeLenBytes - 1 : challengeLenBytes;
            bool challengeLengthValid = (challenge != null) &&
                (challenge.Length == expectedChallengeLength || challenge.Length == challengeLenBytes);
            bool metadataValid =
                (encryptedSecret != null && encryptedSecret.Length > 0 && encryptedSecret.Length <= MaxEncryptedSecretBytes) &&
                (iv != null && iv.Length == 16) &&
                challengeLengthValid &&
                (verification != null && verification.Length == 32);

            if (!metadataValid)
            {
                MessageService.ShowWarning(MetadataReadErrorMessage);
                return false;
            }

            //if failed, return false
            return true;
        }

        private byte[] Create(KeyProviderQueryContext ctx)
        {
            //show the entry dialog for the secret
            //get the secret
            KeyCreation creator = new KeyCreation(this);

            if (creator.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                SensitiveData.Clear(creator.Secret);
                return null;
            }

            if (creator.Secret == null || creator.Secret.Length == 0)
            {
                return null;
            }

            byte[] secret = new byte[creator.Secret.Length];
            
            Array.Copy(creator.Secret, secret, creator.Secret.Length); //probably paranoid here, but not a big performance hit
            SensitiveData.Clear(creator.Secret);

            if (!EncryptAndSave(secret))
            {
                SensitiveData.Clear(secret);
                return null;
            }

            //store the encrypted secret, the iv, and the challenge to disk           
           
            return secret;
        }

        private byte[] Get(KeyProviderQueryContext ctx)
        {
            //read the challenge, iv, and encrypted secret from disk -- if missing, you must use recovery mode
            byte[] encryptedSecret = null;
            byte[] iv = null;
            byte[] challenge = null;
            byte[] verification = null;
            byte[] secret = null;
            byte[] resp = null;
            byte[] result = null;

            try
            {
                if (!ReadEncryptedSecret(out encryptedSecret, out challenge, out iv, out verification))
                {
                    secret = RecoveryMode();
                    if (secret == null) return null;
                    if (!EncryptAndSave(secret)) return null;

                    result = secret;
                    secret = null;
                    return result;
                }
                    //show the dialog box prompting user to press yubikey button
                resp = new byte[YubiWrapper.yubiRespLen];
                KeyEntry entryForm = new KeyEntry(this, challenge);
                
                if (entryForm.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    if (entryForm.RecoveryMode)
                    {
                        secret = RecoveryMode();
                        if (secret == null) return null;
                        if (!EncryptAndSave(secret)) return null;

                        result = secret;
                        secret = null;
                        return result;
                    }

                    else return null;                
                }

                entryForm.Response.CopyTo(resp,0);
                SensitiveData.Clear(entryForm.Response);

                if (DecryptSecret(encryptedSecret, resp, iv, verification, out secret))
                {
                    if (EncryptAndSave(secret))
                    {
                        result = secret;
                        secret = null;
                        return result;
                    }

                    return null;
                }

                MessageService.ShowWarning("Incorrect response from YubiKey.");
                return null;
            }
            finally
            {
                ClearTemporarySecretState(encryptedSecret, iv, challenge, verification, secret, resp);
            }
        }

        private byte[] RecoveryMode()
        {
            //prompt user to enter secret
            RecoveryMode recovery = new RecoveryMode(this);
            if (recovery.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                SensitiveData.Clear(recovery.Secret);
                return null;
            }

            if (recovery.Secret == null || recovery.Secret.Length == 0)
            {
                return null;
            }

            byte[] secret = new byte[recovery.Secret.Length];

            recovery.Secret.CopyTo(secret, 0);
            SensitiveData.Clear(recovery.Secret);            
             
            return secret;
       }

    }
}
