# Manual Release Test - Restored P1/P2 Build

Status: manual matrix and final 2.0.9 candidate compatibility check passed; remote CI pending.

## Maintainer-reported run - 2026-11-04

Portable installation: isolated KeePass 2.62 copy (workstation path omitted).

The maintainer confirmed separate 32-bit and 64-bit host runs with the new candidate. Reported passing checks:

- Plugin listed, settings menu and key provider present.
- Existing database unlock with YubiKey touch; lock/unlock and restart/unlock.
- New database creation in both LT64 modes.
- Wrong key or response rejected.
- Abort followed by immediate retry; timeout followed by immediate retry.
- Recovery followed by normal YubiKey unlock.
- Input-validation matrix in both dialogs.
- Native-library and metadata negative tests.

No unexpected behavior was reported. These are maintainer-reported results, not assistant-observed GUI tests. The maintainer subsequently confirmed mode-mismatch correction (H3), missing-key prompt cancellation (H5), and cancellation of all dialog types (H9) passed on both x86 and x64.

All cases S1-S4, H1-H9, V1-V6, and N1-N3 are therefore recorded as passing on both architectures for the supplied pre-version-bump candidate. The pending cells below are a reusable blank worksheet for the final candidate, not a contradiction of that completed run.

The supplied candidate ZIP has SHA-256 `9192f453dab32d48bc3aefbf6e9b54cd11101705c95b4e8f4d48ed8d9ccdc61e`. The installed DLL hash, Windows version, YubiKey firmware, and tested slot were not separately recorded.

The versioned 2.0.9 candidate has passed all automated checks in actual x86/x64 Debug/Release processes. At 13:19 on 2026-11-04, the maintainer reported testing the final candidate on a fresh copy and with an existing database; both worked. That final confirmation did not separately enumerate process bitness or individual smoke-test steps; the earlier full x86/x64 hardware matrix remains recorded above. Release remains pending remote CI.

Final versioned candidate (supersedes earlier local ZIPs): `test-sandbox\KeeChallenge-Modern-v2.0.9-rc-clean.zip`.

SHA-256: `8b3392c86b973cf95f072e802e0b2975365eddee48095aa0339894ea4e0e82e6`.

The package manifest, embedded DLL hash, and architecture-neutral plugin PE flags were verified.

Publication privacy cleanup replaced fork-maintainer full-name metadata with the collective attribution and mapped build paths to a generic source root. The full automated x86/x64 Debug/Release matrix passed again. Every clean ZIP entry was scanned for the identified personal/workstation details, with no matches. Earlier ZIPs are local-only and must not be published. Physical testing above predates this metadata/build-path-only rebuild.

## What this test covers

A smoke test is the short first check: KeePass starts, lists the plugin, and unlocks a disposable database. The remaining cases below are the full hardware regression test for the restored changes.

The automated tests cover strict parsing, buffer clearing, HMAC compatibility, decrypt failures, signed/tampered/unsigned feeds, and the KeePass loader's product-name filter. They do not prove physical device behavior or a 64-bit host run. Both architecture-labeled automated outputs previously ran as 32-bit.

## Safe setup

1. Use a disposable KeePass installation and test database, not your production vault.
2. Keep the candidate DLL with its matching `32bit` and `64bit` folders under the installation's `Plugins` folder. Do not mix old and new plugin copies.
3. If testing an existing database, copy BOTH its `.kdbx` and corresponding `.xml` sidecar into an isolated folder. Back up the pair before each recovery or corruption test. Unlocking can rotate the sidecar challenge.
4. Use a dedicated test YubiKey/slot with a known challenge-response secret. Do not reprogram your daily-use key or overwrite a production slot.
5. Keep the secret and database passwords out of logs, screenshots, and committed results.
6. Record candidate ZIP SHA-256, KeePass version, Windows version, YubiKey model/firmware, slot, and challenge mode. Confirm actual KeePass process bitness in Task Manager; folder names alone are not proof.
7. Test only one isolated KeePass instance at a time. Close it before replacing DLLs.

Candidate ZIP SHA-256: ____________________

Environment / KeePass version / confirmed process bitness: ____________________

YubiKey model / firmware / test slot / LT64 mode (no secrets): ____________________

## A. Smoke test - do this first on each host architecture

| ID | Action | Expected result | x86 | x64 |
|---|---|---|---|---|
| S1 | Start isolated KeePass and open Tools > Plugins. | KeeChallenge is listed, with no load warning. | Pending | Pending |
| S2 | Inspect Tools menu and master-key provider choices. | KeeChallenge Settings and Yubikey challenge-response are present. | Pending | Pending |
| S3 | Unlock a disposable existing database with the correct key and touch. | Unlock succeeds; the provider is not interpreted as a key-file path. | Pending | Pending |
| S4 | Lock and unlock again, then restart KeePass and unlock. | Both unlocks succeed; updated sidecar remains usable. | Pending | Pending |

Stop and investigate any smoke-test failure before proceeding.

## B. Physical device and cancellation regression

Run each case on both confirmed host architectures. Use only a test slot that you intentionally configure; repeat for Slot 1/Slot 2 if claiming support validation for both.

| ID | Action | Expected result | x86 | x64 |
|---|---|---|---|---|
| H1 | Create a new disposable database with LT64 enabled and matching test-key mode. | Creation, save, close, and reopen succeed. | Pending | Pending |
| H2 | Repeat with LT64 disabled and matching test-key mode. | Creation, save, close, and reopen succeed. | Pending | Pending |
| H3 | Enter the known correct secret but select the opposite challenge mode during creation. | Mode-mismatch prompt appears; accepting the correction permits creation and reopening. | Pending | Pending |
| H4 | Try a different test key or a nonmatching secret. | No unlock; clear error; KeePass remains usable. | Pending | Pending |
| H5 | Remove the key and attempt unlock; cancel the insertion prompt. | No unlock, no crash, clean return. | Pending | Pending |
| H6 | Start unlock and press Abort before touching; immediately retry with touch. | Abort returns cleanly; next attempt succeeds without frozen timer or extra retry. | Pending | Pending |
| H7 | Start unlock, do not touch, and let the countdown expire; immediately retry with touch. | Timeout returns cleanly; next attempt succeeds. | Pending | Pending |
| H8 | Use Recovery Mode with the correct known test secret and challenge mode; close and unlock normally afterward. | Recovery succeeds and rewrites metadata; normal YubiKey unlock still succeeds. | Pending | Pending |
| H9 | Cancel creation, recovery, and challenge dialogs. | No accidental successful unlock; no crash or stuck worker. | Pending | Pending |

## C. Input validation and error messages

Run these in BOTH Key Creation and Recovery Mode on each host. Do not put example secrets into a production key.

| ID | Input / action | Expected result | x86 | x64 |
|---|---|---|---|---|
| V1 | Empty input. | Length warning; dialog remains open. | Pending | Pending |
| V2 | Short input (`1234`). | Length warning; no exception. | Pending | Pending |
| V3 | Exactly 40 characters containing a non-hex character. | Hex-only warning; no exception. | Pending | Pending |
| V4 | Correct 40-hex-character test secret. | Proceeds to the expected creation/recovery flow. | Pending | Pending |
| V5 | Correct test secret with surrounding/interspersed whitespace. | Accepted consistently. | Pending | Pending |
| V6 | Trigger a failed response. | No raw exception text or stack trace in the warning. | Pending | Pending |

## D. Negative integrity and metadata checks

Use fresh copies of the disposable database/sidecar for each case. Keep KeePass closed while mutating files. Never perform these tests on a live vault.

| ID | Action | Expected result | x86 | x64 |
|---|---|---|---|---|
| N1 | Temporarily rename one native DLL in the architecture folder used by the host; attempt key use, then close KeePass and restore it. | Actionable non-sensitive native-library error; no unlock/crash. A fresh run works after restore. | Pending | Pending |
| N2 | Modify one byte of a copied native DLL; attempt key use, then restore the original. | Integrity check rejects it before device use; fresh run works after restore. | Pending | Pending |
| N3 | On separate sidecar copies, test a DTD, invalid Base64, and size above 64 KiB. | Graceful metadata error/recovery path, no crash or unlock bypass. Restore the paired backup after each case. | Pending | Pending |

## E. Automated-only evidence and release gates

- [ ] Run `powershell -NoProfile -File .\scripts\test.ps1` from the source checkout; retain its explicit pass summaries.
- [ ] Signed feed is accepted and unsigned/tampered feeds rejected by the automated KeePass parser test. The public feed is not updated merely by testing a local build.
- [ ] Buffer-clearing assertions pass. This is not a claim that every managed-memory copy has been erased.
- [ ] Run the updated workflows on GitHub after pushing the candidate branch. Local builds do not execute or validate the changed GitHub Actions themselves.
- [ ] Record all failures and retest after fixes; do not carry over passes from a different candidate.
- [ ] Before release, choose a new version, update assembly/feed metadata together, re-sign the feed, rebuild, and rerun required checks. This candidate still reports 2.0.8; do not distribute it as the existing 2.0.8 release.
- [ ] No release until hardware/host tests and remote CI are confirmed.

## Results

Tester / date: repository maintainer / 2026-11-04 (reported in chat).

Failed case IDs / symptoms / retest results: none reported for the confirmed checks above.

Final decision: manual matrix and final candidate compatibility check PASS; publication pending remote CI.
