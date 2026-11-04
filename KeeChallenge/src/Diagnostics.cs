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
using System.Diagnostics;
using System.Threading;

namespace KeeChallenge
{
    internal static class Diagnostics
    {
        private static int failureCount;

        internal static int FailureCount
        {
            get { return Volatile.Read(ref failureCount); }
        }

        internal static void TraceException(string context, Exception ex)
        {
            if (string.IsNullOrEmpty(context)) context = "Unhandled plugin exception";
            if (ex == null) return;

            try
            {
                Trace.WriteLine(string.Format("[KeeChallenge] {0}: {1}", context, ex));
            }
            catch (Exception traceError)
            {
                Interlocked.Increment(ref failureCount);
                try
                {
                    Console.Error.WriteLine("[KeeChallenge] Diagnostic output failed ({0}).",
                        traceError.GetType().Name);
                }
                catch (Exception)
                {
                    // Diagnostics must not interrupt key handling, even when both sinks fail.
                    Interlocked.Increment(ref failureCount);
                }
            }
        }
    }
}