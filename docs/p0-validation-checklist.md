# P0 Security Hardening Validation Checklist

This checklist defines what "fully tested" means for the P0 hardening work and when each test phase runs.

## Timing

1. Per-PR validation: run on each P0 PR before merge.
2. Post-P0 integration validation: run after both P0 PRs are merged to `master`.
3. Release-gate validation: run immediately before creating the next release tag.

## 1) Per-PR Validation (Before Merge)

Run for each P0 PR independently.

### Automated

- [ ] GitHub PR checks are green (`CI/build`, `CodeQL`, `Dependency Review`).
- [ ] Local build passes:

```powershell
dotnet build KeeChallenge/src/KeeChallenge.csproj -c Release /nologo
```

### Focused Manual Smoke

- [ ] KeePass loads plugin successfully with expected native library folder layout.
- [ ] Existing database unlock path still works with YubiKey challenge-response.
- [ ] Wrong/failed response path still fails closed (no unlock).

## 2) Post-P0 Integration Validation (After Both P0 PRs Merge)

Run this once after P0-1 and P0-2 are both merged.

### Environment Matrix

- [ ] Windows + KeePass x64 + `KeeChallenge/lib/64bit` native DLL set.
- [ ] Windows + KeePass x86 + `KeeChallenge/lib/32bit` native DLL set.

### Functional Matrix

- [ ] New DB setup flow with KeeChallenge provider.
- [ ] Existing DB unlock flow.
- [ ] Recovery mode flow (including re-encryption path).
- [ ] Plugin restart/load/unload stability.

### Security Regression Matrix

- [ ] Native DLL tamper test:
  - Modify one native DLL byte in a test copy.
  - Verify plugin blocks initialization with integrity warning.
- [ ] XML metadata oversized test:
  - Replace sidecar `.xml` with a file larger than 64 KB.
  - Verify plugin rejects metadata and falls back to recovery mode warning.
- [ ] XML DTD test:
  - Add a `<!DOCTYPE ...>` declaration to sidecar XML.
  - Verify parser rejects file and falls back to recovery mode warning.
- [ ] XML malformed base64 test:
  - Corrupt `encrypted`/`iv` value.
  - Verify plugin rejects metadata and falls back safely.

## 3) Release-Gate Validation (Immediately Before Tag)

- [ ] Repeat key path tests from integration matrix (both architectures).
- [ ] Verify update-check behavior is still healthy.
- [ ] Build release artifact and verify expected contents.
- [ ] Confirm docs/version/changelog are in sync.

## Pass Criteria

P0 is considered complete only when:

- all P0 PR checks pass,
- integration matrix passes on x86 and x64 hosts,
- tamper/oversized/malformed metadata tests pass,
- release-gate checks pass.
