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

namespace KeeChallenge
{
    /// <summary>
    /// Abstracts a hardware or software challenge-response device.
    /// Implementations must be safe to call Close/Dispose multiple times.
    /// </summary>
    public interface IChallengeResponseProvider : IDisposable
    {
        /// <summary>Length of the byte[] returned by ChallengeResponse.</summary>
        uint ResponseLength { get; }

        /// <summary>
        /// Open and initialise the device. Returns false if the device is
        /// unavailable; the caller should prompt the user and retry or abort.
        /// </summary>
        bool Init();

        /// <summary>
        /// Perform a challenge-response operation.
        /// Returns true and populates <paramref name="response"/> on success.
        /// </summary>
        bool ChallengeResponse(YubiSlot slot, byte[] challenge, out byte[] response);

        /// <summary>Release the device handle.</summary>
        void Close();
    }
}
