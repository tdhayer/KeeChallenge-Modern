## KeeChallenge Security Modernization Plan

### Scope and Decisions
- Target runtime for this pass: Windows only.
- Target compatibility: current KeePass 2.x stable.
- Strategy: security hardening first (P0), then refactor (P1), then build/release modernization (P2).
- Metadata evolution: allow new format with backward read compatibility.

### Phases

#### P0: Security Hardening (in progress)
1. Baseline behavior capture and test checklist.
2. Constant-time verification and crypto API cleanup.
3. Secret handling hardening in UI and memory paths.
4. Metadata validation gates before decrypt.
5. Native interop load and cleanup hardening.
6. Verification matrix (functional + negative cases).

#### P1: Architecture Refactor — COMPLETE
1. ~~Introduce challenge-response provider abstraction.~~ DONE — `IChallengeResponseProvider` interface introduced; `YubiWrapper` implements it + `IDisposable`; `KeyEntry.yubi` field typed to interface.
2. ~~Add metadata versioning with backward-compatible reader.~~ DONE — `<version>2</version>` written to new files; reader silently ignores missing version (v1 legacy compat).
3. ~~Refactor workflow/error semantics (cancel/device/parse/crypto paths).~~ DONE — `DecryptSecret` no longer calls `MessageService.ShowWarning`; `Get()` owns UX for incorrect-response path.

#### P2: Build and Release Modernization — COMPLETE
1. ~~Modernize project/build references and remove machine-specific assumptions.~~ DONE — SDK-style `.csproj` targeting `net48`; KeePass reference is repo-relative (`lib/KeePass.exe`); explicit `<Compile>` list removed (glob); `System.Resources.Extensions` NuGet added for non-string resx compat; build now uses `dotnet build` (dotnet 10 SDK).
2. ~~Update release hygiene: metadata, checksums/signing, and docs.~~ DONE — `AssemblyVersion`, `AssemblyFileVersion`, `AssemblyInformationalVersion` all set to `1.5.0.0`; copyright updated to 2014-2026; description updated; `.sln` header bumped to VS2022 format.

### Relevant Files
- KeeChallenge/src/KeeChallenge.cs
- KeeChallenge/src/YubiWrapper.cs
- KeeChallenge/src/KeyCreation.cs
- KeeChallenge/src/RecoveryMode.cs
- KeeChallenge/src/KeyEntry.cs
- KeeChallenge/src/KeyCreation.Designer.cs
- KeeChallenge/src/RecoveryMode.Designer.cs
- KeeChallenge/src/KeeChallenge.csproj
- KeeChallenge/src/Properties/AssemblyInfo.cs
- README.md

### Verification Matrix
1. Build Release and validate plugin load in current KeePass 2.x.
2. Functional checks: create/open/recovery with slot 2, LT64 on/off.
3. Negative checks: corrupt XML, malformed base64, bad lengths, missing native DLLs, no YubiKey, cancel paths.
4. Security checks: constant-time compare, secret masking, buffer clearing paths.
5. Backward compatibility: open legacy metadata and verify behavior.

### Current Implementation Status
Completed in code:
- Constant-time compare added in core decrypt verification and key creation response verification.
- Legacy crypto object construction updated to factory APIs with safer disposal patterns.
- Metadata length/presence validation added before decrypt.
- Secret entry masking enabled in key creation and recovery dialogs.
- Secret parsing updated to avoid in-place textbox mutation.

Edited files:
- KeeChallenge/src/KeeChallenge.cs
- KeeChallenge/src/KeyCreation.cs
- KeeChallenge/src/RecoveryMode.cs
- KeeChallenge/src/KeyCreation.Designer.cs
- KeeChallenge/src/RecoveryMode.Designer.cs

Pending next:
- P0 native interop hardening in KeeChallenge/src/YubiWrapper.cs.
- Full build and validation matrix after .NET tooling is installed.

### Resume Checklist After Reboot
1. ~~Verify tooling: msbuild or dotnet available in PATH.~~ DONE — VS2019 BuildTools MSBuild found at C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe
2. ~~Build solution in Release.~~ DONE — clean build, both Debug and Release.
3. ~~Implement YubiWrapper hardening task.~~ DONE — see below.
4. ~~Re-run build and functional/negative matrix.~~ BUILD PASSES. Functional/negative tests require physical YubiKey — manual step.
5. Capture residual risks for P1 handoff. — NEXT

### P0 Complete — YubiWrapper Hardening (Done)
- `ChallengeResponse`: `temp` buffer (64-byte raw HMAC response) zeroed in `finally` block after copy.
- `Close()`: `yk` handle set to `IntPtr.Zero` immediately after `yk_close_key`, preventing double-close; no longer throws (uses `Debug.Assert`), safe in `finally` paths.
- `AssemblyDirectory`: null-guard added; throws `InvalidOperationException` with a clear message if `Path.GetDirectoryName` returns null.
- KeePass reference: moved from machine-specific absolute path to repo-relative `lib/KeePass.exe`. Copy of `KeePass.exe` from installed instance placed in `KeeChallenge/lib/`.

### P0 Residual Risks (for P1 handoff)
- Functional/negative matrix requires physical YubiKey — not yet executed.
- `yk_challenge_response` P/Invoke: no `CharSet` or `ExactSpelling` on native DLL imports — lower risk (no string params), but worth cleaning in P1.
- `Init()` still uses `MessageBox.Show` for error reporting — should be replaced with proper plugin error surfacing in P1.
- `nativeDLLs` field is a mutable `List<string>` — minor; could be `IReadOnlyList` in P1.
