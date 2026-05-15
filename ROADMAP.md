# KeeChallenge-Modern Roadmap (Post-v2.0.5)

This file is the forward-looking plan after the v2.0.5 patch release.
Use it as the single source of truth for what to do next.

## Current Status

- P0/P1/P2 modernization work is complete.
- v2.0.5 has been tagged and pushed.
- CI and release workflows are active.
- Release assets now include SHA-256 checksums.
- Phase 1 pipeline security baseline is active:
   - CodeQL workflow
   - Dependency Review workflow
   - Dependabot configuration (NuGet + GitHub Actions)

## Security Pipeline Follow-up (Post-Phase 1)

Manual GitHub settings status in repository settings:

- Secret scanning and push protection: enabled
- Branch protection requiring `CodeQL` and `Dependency Review`: enabled

Next engineering phase:

- P1 security hardening issue: https://github.com/tdhayer/KeeChallenge-Modern/issues/8
- P2 security hardening issue: https://github.com/tdhayer/KeeChallenge-Modern/issues/9

## Next Milestone: Stabilization and Security Hardening

### Priority 0: Hardware Validation (Real YubiKey)

Run and record the functional and negative matrix on a physical YubiKey 5.

Checklist:
- Verify create flow with Slot 2, LT64 on.
- Verify create flow with Slot 2, LT64 off.
- Verify open/unlock flow with correct key.
- Verify wrong key/response behavior and warning paths.
- Verify cancel behavior in all dialogs.
- Verify no-key-present behavior and user messaging.
- Verify recovery flow (valid and invalid secret lengths).

Exit criteria:
- No crashes.
- No silent data loss.
- No misleading error states.

### Priority 1: Release and Maintenance Hygiene

- Keep action SHAs fresh quarterly (checkout/setup-dotnet/gh-release).
- Validate release artifact contents each release:
  - KeeChallenge.dll
  - lib/32bit/*
  - lib/64bit/*
  - .sha256 sidecar
- Confirm VERSION and release notes stay aligned.

### Priority 2: Compatibility and UX Follow-ups (Optional)

- Evaluate whether USB-only support should be explicitly stated in README for all challenge-response paths.
- Decide whether to keep legacy wording "v2.0" in README or switch to "v2.x" language.
- Consider a small diagnostics section in README for common startup failures (missing native DLLs, no key detected).

## Stretch Goals (Only If Needed)

- Add a small pure-logic test project for parser/metadata validation only.
- Explore modern Yubico APIs if NFC/CCID support becomes a product requirement.

## Resume In 5 Minutes

1. Pull latest master:
   `git checkout master ; git pull --ff-only`
2. Build release:
   `dotnet build KeeChallenge/src/KeeChallenge.csproj -c Release /nologo /clp:ErrorsOnly`
3. Run the hardware validation checklist above and capture results in an issue.
4. If all checks pass, prepare next patch tag (increment from current release only if changes were made).

## Decision Log

- Do not retag existing releases.
- Use patch tags for post-release fixes (v2.0.1, v2.0.2, ...).
- Prefer KeePass MessageService for user warnings/info.
- Treat current implementation as USB challenge-response path.
