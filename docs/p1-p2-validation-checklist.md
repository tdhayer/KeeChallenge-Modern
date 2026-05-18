# P1+P2 Security Validation Checklist

This checklist defines the validation gate for:

- P1: signed update-feed pinning + sensitive buffer zeroization hardening.
- P2: strict secret input validation + sanitized user-facing error handling.

## Timing

1. Pre-merge (stacked PR state): run while PR #10 and PR #11 are open.
2. Pre-integration: run once again immediately before any merge action.
3. Pre-release tag: run one final pass before tagging next release.

## 1) Automated Gate (Pre-Merge)

- [ ] PR checks are green:
  - PR #10 (`feat/p1-signed-feed-pin-zeroize`)
  - PR #11 (`feat/p2-input-validation-sanitized-errors`)
- [ ] Local build passes (Release and Debug):

```powershell
dotnet build KeeChallenge/src/KeeChallenge.csproj -c Release /nologo
dotnet build KeeChallenge/src/KeeChallenge.csproj -c Debug /nologo
```

- [ ] VERSION feed signing and verification passes:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\sign-version-feed.ps1 -PrivateKeyPath C:\Users\tdhay\KeeChallengeSecrets\update-feed-private-key.xml
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

- both PR CI suites are green,
- Release and Debug builds pass,
- manual architecture matrix passes,
- malformed input cases are handled gracefully,
- update signature positive/negative checks pass,
- no private-key leakage is found.