# Security Policy

## Supported Versions

Only the latest release of KeeChallenge-Modern is actively maintained. Older versions do not receive security fixes.

| Version | Supported |
|---------|-----------|
| 2.x     | ✅ Yes    |
| 1.x     | ❌ No     |

## Reporting a Vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

Report security issues privately via GitHub's built-in [private vulnerability reporting](https://github.com/tdhayer/KeeChallenge-Modern/security/advisories/new).

Include as much detail as possible:
- A description of the vulnerability and its potential impact
- Steps to reproduce or a proof-of-concept (no weaponized exploits, please)
- Affected version(s)
- Any suggested mitigations

You should receive an acknowledgement within **7 days**. If you do not, feel free to follow up.

## Disclosure Policy

- Vulnerabilities will be fixed in a private branch and released as a patch.
- A CVE will be requested for qualifying issues.
- Credit will be given to reporters in the release notes unless anonymity is requested.
- Coordinated disclosure: please allow at least **90 days** before public disclosure.

## Security Considerations

KeeChallenge-Modern is a KeePass plugin that bridges KeePass to HMAC-SHA1 YubiKey challenge-response.

- The plugin stores an **encrypted** copy of the challenge-response secret inside the KeePass database (AES-256-CBC, key derived from the YubiKey response). The secret is never written to disk in plaintext.
- Responses from the YubiKey are compared using a **constant-time** equality check (`FixedTimeEquals`) to resist timing attacks.
- The plugin does not phone home except for version-update checks against `https://raw.githubusercontent.com/tdhayer/KeeChallenge-Modern/master/VERSION`. The feed is signature-validated by KeePass using a pinned RSA public key configured by the plugin; tampered/unsigned metadata is rejected.

## Automated Security Checks

This repository uses GitHub-native security automation in CI:

- `CodeQL` code scanning via GitHub Code Scanning Default Setup.
- `Dependency Review` on pull requests to catch vulnerable package deltas.
- `Dependabot` updates for NuGet dependencies and GitHub Actions.

Note: Keep CodeQL on Default Setup unless intentionally migrating to Advanced Setup. Running both at once causes SARIF upload conflicts.

Repository settings status (manual, in GitHub UI):

- **Secret scanning** and **push protection** are enabled.
- Branch protection requires `CodeQL` and `Dependency Review` checks.
