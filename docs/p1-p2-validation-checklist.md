# P1+P2 Security Validation Checklist

This checklist defines the validation gate for:

- P1: signed update-feed pinning + sensitive buffer zeroization hardening.
- P2: strict secret input validation + sanitized user-facing error handling.

## Restoration Status (2026-11-04)

- The 2.0.6/2.0.7 regression was the changed assembly product, not the plugin's display name. Preserve `AssemblyProduct("KeePass Plugin")`, assembly name/title `KeeChallenge`, and the `KeeChallengeExt` entry point.
- P1/P2 source changes have been restored locally from 2.0.7 without its assembly metadata or release-version changes.
- Release/Debug builds and the Windows PowerShell reflective load probe passed against the bundled KeePass 2.61 reference.
- `VERSION` was re-signed using the existing external signing key; its reported public-key fingerprint matches the pinned key.
- Final versioned 2.0.9 regression runner passes with explicit `RESULT: PASS (0 failed)` in Debug/Release and actual x86/x64 processes, covering loader acceptance/rejection, signature positive/negative checks, parser hardening, and sensitive-data handling.
- Dependabot workflow updates were applied and combined validation rerun.
- Earlier architecture-labeled runs reused compiler intermediates and both executed as 32-bit. Rebuilding each target fixes this; the script now requires the expected process-bitness output and keeps the plugin architecture-neutral.
- On 2026-11-04 the maintainer reported passing the complete KeePass 2.62 manual matrix in separate x86/x64 host runs, including mode-mismatch correction, missing-key cancellation, and all dialog cancellation paths. See [the manual release test](manual-release-test.md).
- Assembly and signed feed metadata are aligned at 2.0.9. The maintainer reported the final candidate working in a fresh copy and with an existing database. Remote CI remains before publication.
- The historical May results below and in the linked results document do not validate this restored worktree.

For the step-by-step physical smoke test and hardware regression matrix, use [the manual release test](manual-release-test.md).

This dependency-free .NET Framework runner rebuilds and executes both configurations and both process architectures. It checks the actual KeePass loader, including a deliberately renamed-product fixture, in addition to the pure-logic security regressions. It does not replace the visible Plugins-panel or physical-YubiKey tests.

The existing reflection probe must run under Windows PowerShell, not PowerShell Core:

```powershell
powershell -NoProfile -File .\scripts\load-test.ps1 -PluginPath "$PWD\KeeChallenge\src\bin\Release\net48\KeeChallenge.dll" -KeePassPath "$PWD\KeeChallenge\lib\KeePass.exe"
```

Current automated run command:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

## Timing

1. Pre-integration: run against the combined candidate branch.
2. Before tagging: confirm remote CI and final artifact smoke results.
3. Before publication: inspect draft release assets, checksum, and notes.

## 1) Automated Gate (Pre-Merge)

- [ ] Combined candidate GitHub checks are green (pending push).
- [x] Local build passes (Release and Debug):

```powershell
dotnet build KeeChallenge/src/KeeChallenge.csproj -c Release /nologo
dotnet build KeeChallenge/src/KeeChallenge.csproj -c Debug /nologo
```

- [x] VERSION feed signing and verification passes:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\sign-version-feed.ps1 -PrivateKeyPath <path-to-private-key.xml>
```

- [ ] Workspace scan shows no private key leakage patterns:
  - `BEGIN RSA PRIVATE KEY`
  - `BEGIN PRIVATE KEY`
  - `update-feed-private-key`
  - `KeeChallengeSecrets`

## 2) Manual Hardware Matrix (P1+P2)

Run on both architectures:

- [ ] Windows + KeePass x64 + `KeeChallenge/lib/64bit`.
- [ ] Windows + KeePass x86 + `KeeChallenge/lib/32bit`.

Functional flows:

- [ ] Existing database unlock succeeds with expected YubiKey touch flow.
- [ ] Wrong/failed challenge-response fails closed (no unlock).
- [ ] Recovery mode flow succeeds and re-encrypts metadata for subsequent unlocks.
- [ ] Cancel from prompt/dialog paths returns cleanly (no crash, no stuck state).

## 3) P2 Input Validation Matrix (Manual)

In both `KeyCreation` and `RecoveryMode` dialogs:

- [ ] Empty input rejected with clear validation message.
- [ ] Short input (for example `1234`) rejected gracefully.
- [ ] Non-hex input (for example `GG...`) rejected gracefully.
- [ ] Valid 40-hex-character input accepted.
- [ ] Whitespace around valid hex is handled correctly.
- [ ] No unhandled exceptions or abrupt dialog termination in any case.

## 4) Error-Surface and Diagnostics Validation

- [ ] User-facing error text is sanitized and consistent (no raw exception text shown).
- [ ] Detailed exception context still appears in diagnostics output (Debug/Trace) for maintainers.
- [ ] Native DLL failure scenarios show actionable but non-sensitive user messages.

## 5) Update Authenticity Checks (P1)

- [ ] Positive case: signed `VERSION` feed is accepted by update-check path.
- [ ] Negative case: tampered signature/header is rejected (fail-closed behavior).
- [ ] Update-check is disabled if signing configuration cannot be initialized.

## Pass Criteria

P1+P2 are considered ready only when:

- the combined candidate CI checks are green,
- Release and Debug builds pass,
- manual architecture matrix passes,
- malformed input cases are handled gracefully,
- update signature positive/negative checks pass,
- no private-key leakage is found.