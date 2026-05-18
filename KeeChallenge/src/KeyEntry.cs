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
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;

using KeePass.UI;
using KeePassLib.Utility;

namespace KeeChallenge
{
    public partial class KeyEntry : Form
    {
        private Timer countdown;
        private byte[] m_challenge;
        private byte[] m_response;
        private IChallengeResponseProvider yubi;
        private YubiSlot yubiSlot;
        private KeeChallengeProv m_parent;
        
        private bool success;
        private bool cancelRequested;
        private bool formClosed;
        private bool deferProviderDispose;

        private BackgroundWorker keyWorker;

        public byte[] Response
        {
            get { return m_response; }
            private set { m_response = value; }
        }

        public byte[] Challenge
        {
            get { return m_challenge; }
            set { m_challenge = value; }
        }

        public bool RecoveryMode
        {
            get;
            private set;
        }

        public KeyEntry(KeeChallengeProv parent)
        {
            InitializeComponent();
            m_parent = parent;
            success = false;
            Response = new byte[YubiWrapper.yubiRespLen];
            Challenge = null;
            yubiSlot = parent.YubikeySlot;
            RecoveryMode = false;
            Icon = Icon.FromHandle(Properties.Resources.yubikey.GetHicon());
        }

        public KeyEntry(KeeChallengeProv parent, byte[] challenge)
        {
            InitializeComponent();
            m_parent = parent;
            success = false;
            Response = new byte[YubiWrapper.yubiRespLen];
            Challenge = challenge;
            yubiSlot = parent.YubikeySlot;

            Icon = Icon.FromHandle(Properties.Resources.yubikey.GetHicon());
        }
               
        private void YubiChallengeResponse(object sender, DoWorkEventArgs e) //Should terminate in 15seconds worst case
        {
            //Send the challenge to yubikey and get response
            if (Challenge == null || cancelRequested || yubi == null) return;

            try
            {
                success = yubi.ChallengeResponse(yubiSlot, Challenge, out m_response,
                    () => cancelRequested || formClosed || IsDisposed);
            }
            catch (Exception ex)
            {
                success = false;
                Diagnostics.TraceException("Yubi challenge-response worker failed.", ex);
            }
        }

        private void KeyWorkerDone(object sender, RunWorkerCompletedEventArgs e) //guaranteed to run after YubiChallengeResponse
        {
            if (deferProviderDispose && yubi != null)
            {
                yubi.Dispose();
                yubi = null;
                deferProviderDispose = false;
            }

            if (formClosed || cancelRequested || IsDisposed) return;

            if (e.Error != null)
            {
                Diagnostics.TraceException("Yubi challenge-response completion error.", e.Error);
                MessageService.ShowWarning("Error getting response from YubiKey.");
                DialogResult = DialogResult.No;
                return;
            }

            if (success)
                DialogResult = DialogResult.OK;  //setting this calls Close() IF the form is shown using ShowDialog()
            else
            {
                MessageService.ShowWarning("Error getting response from YubiKey.");
                DialogResult = DialogResult.No;
            }
        }

        private void Countdown(object sender, EventArgs eventArgs)
        {
            if (countdown == null) return;
            if (progressBar.Value > 0)
                progressBar.Value--;
            else
            {
                CountdownCompleted();
            }
        }

        private void CountdownCompleted()
        {
            if (countdown != null)
            {
                countdown.Stop();
            }

            Close();
        }
        
        private void OnFormLoad(object sender, EventArgs e)
        {
            ControlBox = false;

            progressBar.Maximum = 15;
            progressBar.Minimum = 0;
            progressBar.Value = 15;

            yubi = new YubiWrapper();
            try
            {
                while (!yubi.Init())
                {
                    YubiPrompt prompt = new YubiPrompt();
                    DialogResult res =  prompt.ShowDialog();
                    if (res != DialogResult.Retry)
                    {
                        RecoveryMode = prompt.RecoveryMode;
                        DialogResult = DialogResult.Abort;
                        return;
                    }
                }
            }
            catch (PlatformNotSupportedException err)
            {
                Debug.Assert(false);
                Diagnostics.TraceException("YubiKey platform initialization failed.", err);
                MessageService.ShowWarning("KeeChallenge-Modern currently supports Windows hosts only.");
                return;
            }
            //spawn background countdown timer
            countdown = new Timer();
            countdown.Tick += Countdown;
            countdown.Interval = 1000;
            countdown.Enabled = true;

            keyWorker = new BackgroundWorker();            
            keyWorker.DoWork += YubiChallengeResponse;
            keyWorker.RunWorkerCompleted += KeyWorkerDone;
            keyWorker.RunWorkerAsync();     
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            formClosed = true;

            if (countdown != null)
            {
                countdown.Enabled = false;
                countdown.Dispose();
                countdown = null;
            }

            if (yubi != null)
            {
                if (keyWorker != null && keyWorker.IsBusy)
                {
                    // Force any in-flight blocking native call to return so a
                    // follow-up dialog's Init/timer start isn't stalled.
                    try { yubi.RequestCancel(); }
                    catch (Exception ex) { Diagnostics.TraceException("RequestCancel from OnFormClosed failed.", ex); }

                    // Drain the worker so the YubiKey handle + library are fully
                    // released before control returns to the caller. Without this,
                    // a fast re-open of the password dialog can hit a half-released
                    // device state and the next challenge fails immediately.
                    var sw = Stopwatch.StartNew();
                    while (keyWorker.IsBusy && sw.ElapsedMilliseconds < 1500)
                    {
                        Application.DoEvents();
                        System.Threading.Thread.Sleep(10);
                    }
                }

                if (keyWorker != null && keyWorker.IsBusy)
                {
                    // Worker still hasn't unwound; fall back to deferred disposal.
                    deferProviderDispose = true;
                }
                else
                {
                    yubi.Dispose();
                    yubi = null;
                }
            }

            GlobalWindowManager.RemoveWindow(this);
        }

        private void AbortButton_Click(object sender, EventArgs e)
        {
            cancelRequested = true;
            // OnFormClosed handles RequestCancel + worker drain.
            CountdownCompleted();
        }
    }
}
