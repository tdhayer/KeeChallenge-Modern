[KeeChallenge-Modern v2.0.5](https://github.com/tdhayer/KeeChallenge-Modern/ "KeeChallenge-Modern Documentation")
=================

## Fork Lineage
- This repository is an independently maintained fork of the original KeeChallenge project.
- Upstream/original repository: https://github.com/brush701/keechallenge
- Full credit to the original authors and contributors is preserved in source headers, license, and project history.
- This fork focuses on continuing maintenance, modernization, and release publishing when upstream releases are unavailable.

## Changes
v2.0.5
* P0 hardening completed: native DLL loading trust boundary strengthened with bundled-library integrity validation and safer directory loading APIs.
* Metadata parser hardening: explicit DTD prohibition, resolver disabled, document-size limits, and stricter encrypted payload validation.
* Validation and release hygiene: full post-P0 integration/recovery regression matrix completed and local test sandbox artifacts excluded from source control.

v2.0.4
* Plugin update-check fix: corrected `UpdateUrl` endpoint to a valid raw GitHub URL.
* Plugin version feed fix: aligned `VERSION` component name with plugin `AssemblyTitle` (`KeeChallenge`) for proper matching.
* Release metadata sync: bumped `VERSION` and assembly version fields to 2.0.4.

v2.0.3
* Release readiness pass: standardized source license headers to GPLv3 wording to match repository licensing.
* Platform support clarity: `OSXGuide.md` marked legacy/unsupported and linked as historical-only guidance.
* Compliance hardening: added `THIRD_PARTY_LICENSES.md` and ensured release ZIP includes license/notice documents.
* Security/docs correction: replaced inaccurate "TOTP secret" wording with challenge-response secret terminology.
* Build docs sync: aligned .NET SDK prerequisite guidance to 8.0+.

v2.0.2
* Privacy hardening for public release: rewritten commit identity to GitHub noreply address.
* Compliance/docs update: added `THIRD_PARTY_NOTICES.md` and linked it from dependencies.
* Release metadata sync: project `VERSION` and assembly version fields aligned to 2.0.2.

v2.0
* Security hardening: constant-time secret verification, AES/SHA factory API modernization, metadata validation gates, masked secret input in all dialogs, native interop buffer zeroing and handle safety.
* Architecture: `IChallengeResponseProvider` abstraction introduced; `YubiWrapper` implements it + `IDisposable`.
* Metadata format v2: new databases write a `<version>` element; legacy v1 files are read without modification.
* Build modernized: SDK-style project targeting .NET 4.8, builds with `dotnet build`. KeePass reference is repo-relative (no machine path required).
* Improved corrupt metadata handling: corrupt encrypted payloads now fail gracefully with "Incorrect response from YubiKey" instead of surfacing padding exceptions.
* Improved onboarding UX: LT64 mismatch is auto-detected and the user is prompted to switch challenge mode.

v1.5 (legacy)
* Thanks to Robert Claypool for his numerous contributions to clean up and improve KeeChallenge.
* Migrated to Github from Sourceforge.
* Changed recovery mode to better support variable length challenges.
* MD5 Checksum: `80A7EADA1C86332B3F91B75E4E8317F0`
* SHA1 Checksum: `06C3B96ED674E5617F0DAFF5101E23EF95AFF71C`

v1.4
* Added support for variable length challenges. To use it, a new composite master key must be created.

v1.3
* Added OSX support. Thanks to Markku Roponen for figuring this out!
* Updated Yubico libraries to v1.16.2 to support Yubikey Neo firmware 3.3.0

v1.2
* Bug fixes for dynamic 32/64 bit support
* Added button for recovery mode and fixed a bug

v1.1
* Added support for OpenURL function
* Persisted slot choice
* Provide support for 32 bit systems
* Fixed null reference error on cancellation

v1.0.2
* Added support for choosing Yubikey slot via Tools->KeeChallenge Settings. Default is slot 2
* Added plugin update checking
* Don't start the 15 second countdown until the Yubikey is inserted

v1.0.1
* Updated KeeEntry.cs and YubiWrapper.cs to properly initialize and clean up the native Yubico libraries

---

## Supported Platforms
Windows only (this pass). The native Yubico libraries (`libykpers-1-1.dll` and friends) are Windows DLLs; the plugin selects the correct 32-bit or 64-bit version automatically at runtime.

Linux/macOS support existed in earlier releases via Mono and a `KeeChallenge.dll.config` dllmap. That path has not been maintained in v2.0 and is not tested.
`OSXGuide.md` is retained only as historical reference and is legacy/unsupported for current releases.

---

## Dependencies

- **KeePass 2.x** - available as an installer or portable ZIP from https://keepass.info/download.html. Version 2.55 or later recommended; v2.0 was tested against KeePass 2.61.
- **Yubico native libraries** - the prebuilt `32bit/` and `64bit/` DLL bundles from https://opensource.yubico.com/yubikey-personalization/releases.html. The plugin ships with or alongside these folders.
- **.NET Framework 4.8** - included in Windows 10 1903+ and Windows 11. Available from https://dotnet.microsoft.com/download/dotnet-framework/net48 for older systems.

For bundled third-party component provenance and notice guidance, see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) and [THIRD_PARTY_LICENSES.md](THIRD_PARTY_LICENSES.md).

---

## Building from Source

### Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download) (8.0 or later; tested with 10.0)
- A copy of `KeePass.exe` placed at `KeeChallenge/lib/KeePass.exe` (copy from your KeePass install or portable ZIP)

### Build

```
dotnet build KeeChallenge/src/KeeChallenge.csproj -c Release
```

Output: `KeeChallenge/src/bin/Release/net48/KeeChallenge.dll`

No manual reference editing required - the project resolves `KeePass.exe` via the repo-relative path above.

---

## Installation

KeeChallenge-Modern works with both the **installed** and **portable** versions of KeePass.

### Option A - Installed KeePass (system-wide)

1. Locate your KeePass install directory (typically `C:\Program Files\KeePass Password Safe 2\`).
2. Copy `KeeChallenge.dll` into the `Plugins\` subfolder inside that directory. Create `Plugins\` if it doesn't exist.
3. Copy the `32bit\` and `64bit\` Yubico library folders into the same `Plugins\` subfolder.

```
KeePass Password Safe 2\
  KeePass.exe
  Plugins\
    KeeChallenge.dll
    32bit\
      libykpers-1-1.dll
      libyubikey-0.dll
      ...
    64bit\
      libykpers-1-1.dll
      libyubikey-0.dll
      ...
```

### Option B - Portable KeePass (recommended for testing or no admin rights)

1. Download the **KeePass Portable ZIP** from https://keepass.info/download.html and extract it to any folder (e.g. `C:\Tools\KeePass\`).
2. Copy `KeeChallenge.dll` into the `Plugins\` subfolder of that extracted folder. Create `Plugins\` if it doesn't exist.
3. Copy the `32bit\` and `64bit\` Yubico library folders into the same `Plugins\` subfolder.
4. Launch `KeePass.exe` directly from the extracted folder. No installation or admin rights required.

```
KeePass\           <- extracted portable ZIP, run from here
  KeePass.exe
  Plugins\
    KeeChallenge.dll
    32bit\
      libykpers-1-1.dll
      ...
    64bit\
      libykpers-1-1.dll
      ...
```

> **Note:** KeePass stores your database's `.xml` metadata file (challenge, IV, encrypted secret) in the same directory as your `.kdbx` file. Make sure that location is writable.

---

## Using

KeeChallenge-Modern uses the **HMAC-SHA1 challenge-response** functionality built into the YubiKey.

### First-time setup

1. Configure your YubiKey slot 2 for HMAC-SHA1 challenge-response using the [YubiKey Personalization Tool](https://www.yubico.com/support/download/yubikey-personalization-tools/).
   - Fixed 64-byte challenge is the default and recommended setting.
  - Variable-length (LT64) is supported - enable it in KeeChallenge Settings if you configure it on the key.
   - Requiring a button press is strongly recommended.
2. **Copy and securely store your secret.** You will need it to recover access if you lose your YubiKey. Store it in a second safe location (printed, separate password manager, etc.).
3. When setting your KeePass master key, select **"Yubikey challenge-response"** under Key Providers.
4. Enter the secret from your YubiKey when prompted, then touch your YubiKey to verify.

### How it works

Your YubiKey secret is never stored in plain text. On each database open:
1. A stored challenge is sent to the YubiKey.
2. The HMAC-SHA1 response is used as an AES-256 key to decrypt the stored secret.
3. The decrypted secret is verified against a SHA-256 hash before use.
4. A new random challenge is generated and saved for the next open.

All state is stored in an `.xml` file alongside your `.kdbx` database file.

### Recovery mode

If the `.xml` file is lost or corrupted, or if you lose your YubiKey, use **Recovery Mode**: enter your stored secret directly to regain access. You will then be prompted to reconfigure with a YubiKey.

> **Security note:** KeeChallenge-Modern is not intended as the sole authentication factor. Physical possession of your YubiKey is sufficient to open the database if no master password is set. Always use KeeChallenge-Modern **together with a strong master password**.

---

## Common Errors

**"The following plugin is incompatible with the current KeePass version..."**
This occurs after a KeePass update when `KeePass.exe.config` is stale. The most reliable fix is a full KeePass uninstall/reinstall. Alternatively, download the KeePass portable ZIP and copy `KeePass.exe.config` from it into your install directory.

**YubiKey not detected / "Error connecting to yubikey"**
- Ensure the `32bit\` and `64bit\` Yubico library folders are present in the `Plugins\` directory alongside `KeeChallenge.dll`.
- Try removing and reinserting the YubiKey.
- The 15-second countdown starts once the YubiKey is detected; if no key is found within that window, use Recovery Mode or retry.

**Build error: "Could not locate the assembly KeePass"**
Place a copy of `KeePass.exe` at `KeeChallenge/lib/KeePass.exe` relative to the repository root. The project uses this repo-relative path - no system install path is hardcoded.
