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
using System.Drawing;
using System.Windows.Forms;

using KeePass.UI;
using KeePassLib.Utility;

namespace KeeChallenge
{
    public partial class RecoveryMode : Form
    {

        public byte[] Secret
        {
            get;
            private set;
        }

        private KeeChallengeProv m_parent;

        public RecoveryMode(KeeChallengeProv parent)
        {
            InitializeComponent();

            Icon = Icon.FromHandle(Properties.Resources.yubikey.GetHicon());
            m_parent = parent;
        }

        public void OnClosing(object o, FormClosingEventArgs e)
        {
            if (DialogResult == DialogResult.OK)
            {
                m_parent.LT64 = LT64_cb.Checked;

                string parseError;
                byte[] parsedSecret = null;
                if (!SecretInputParser.TryParseSecret(secretTextBox.Text, out parsedSecret, out parseError))
                {
                    MessageService.ShowWarning(parseError);
                    e.Cancel = true;
                    return;
                }

                Secret = parsedSecret;

                secretTextBox.Text = string.Empty;
            }
            else
            {
                SensitiveData.Clear(Secret);
            }
            GlobalWindowManager.RemoveWindow(this);
        }
    }
}
