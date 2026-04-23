/* KeeChallenge--Provides Yubikey challenge-response capability to Keepass
*  Copyright (C) 2014  Ben Rush
*  
*  This program is free software; you can redistribute it and/or
*  modify it under the terms of the GNU General Public License
*  as published by the Free Software Foundation; either version 2
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
using System.Drawing;
using System.Windows.Forms;

using KeePass.UI;

namespace KeeChallenge
{
    public partial class KeyCreation : Form
    {
        private static byte[] GenerateResponseForMode(byte[] challenge, byte[] key, bool lt64)
        {
            byte[] workingChallenge = challenge;
            if (lt64)
            {
                workingChallenge = new byte[challenge.Length - 1];
                Array.Copy(challenge, workingChallenge, workingChallenge.Length);
            }

            using (System.Security.Cryptography.HMACSHA1 hmac = new System.Security.Cryptography.HMACSHA1(key))
            {
                return hmac.ComputeHash(workingChallenge);
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

        public byte[] Secret
        {
            get;
            private set;
        }

        private KeeChallengeProv m_parent;

        public KeyCreation(KeeChallengeProv parent)
        {
            InitializeComponent();
            Secret = null;
            Icon = Icon.FromHandle(Properties.Resources.yubikey.GetHicon());
            m_parent = parent;
        }
  
        public void OnClosing(object o, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                m_parent.LT64 = LT64_cb.Checked;

                Secret = new byte[KeeChallengeProv.secretLenBytes];
                string normalizedSecret = secretTextBox.Text.Replace(" ", string.Empty); //remove spaces
                
                if (normalizedSecret.Length == KeeChallengeProv.secretLenBytes * 2)
                {
                    for (int i = 0; i < normalizedSecret.Length; i += 2)
                    {
                        string b = normalizedSecret.Substring(i, 2);
                        Secret[i / 2] = Convert.ToByte(b,16);
                    }
                    secretTextBox.Text = string.Empty;
                }
                else
                {
                    //invalid key
                    MessageBox.Show("Error: secret must be 20 bytes long");
                    e.Cancel = true;
                    return;
                }
                
                //Confirm they have a key whose secret matches this
                byte[] challenge = m_parent.GenerateChallenge();                
                KeyEntry validate = new KeyEntry(m_parent, challenge);               
                
                if ( validate.ShowDialog(this) != DialogResult.OK)
                {
                    MessageBox.Show("Unable to get response from yubikey");
                    e.Cancel = true;
                    Array.Clear(Secret, 0, Secret.Length);
                    return;
                }

                byte[] validResp = m_parent.GenerateResponse(challenge, Secret);

                if (!FixedTimeEquals(validate.Response, validResp))
                {
                    bool oppositeLt64 = !m_parent.LT64;
                    byte[] oppositeResp = GenerateResponseForMode(challenge, Secret, oppositeLt64);
                    bool oppositeMatches = FixedTimeEquals(validate.Response, oppositeResp);
                    Array.Clear(oppositeResp, 0, oppositeResp.Length);

                    if (oppositeMatches)
                    {
                        string modeText = oppositeLt64 ? "enabled" : "disabled";
                        DialogResult switchMode = MessageBox.Show(
                            "The entered secret matches your YubiKey when Variable Length Challenge is " + modeText + ".\n\n" +
                            "Would you like to switch this setting now?",
                            "KeeChallenge",
                            MessageBoxButtons.YesNo,
                            MessageBoxIcon.Question);

                        if (switchMode == DialogResult.Yes)
                        {
                            LT64_cb.Checked = oppositeLt64;
                            m_parent.LT64 = oppositeLt64;
                            Array.Clear(validResp, 0, validResp.Length);
                            return;
                        }

                        MessageBox.Show("Error: secret does not match yubikey with the current challenge mode.");
                    }
                    else
                    {
                        MessageBox.Show("Error: secret does not match yubikey");
                    }

                    e.Cancel = true;
                    Array.Clear(Secret, 0, Secret.Length);
                    Array.Clear(validResp, 0, validResp.Length);
                    return; //Error: wrong secret
                }
                
                Array.Clear(validate.Response, 0, validate.Response.Length);
                Array.Clear(validResp, 0, validResp.Length);
            }
            GlobalWindowManager.RemoveWindow(this);
        }    
    }
}