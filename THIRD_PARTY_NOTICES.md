# Third-Party Notices

This repository and its release artifacts include third-party software components.
See `THIRD_PARTY_LICENSES.md` for explicit attribution and licensing references tied to bundled components.

## Included Third-Party Components

### 1) KeePass Reference Assembly

- Component: KeePass
- Upstream project: https://keepass.info/
- Upstream source: https://sourceforge.net/p/keepass/code/HEAD/tree/
- Bundled path in this repository: KeeChallenge/lib/KeePass.exe
- Observed file version in this repository: 2.61.0.0
- Purpose in this repository: compile-time reference for the plugin project and compatibility with KeePass 2.x

Licensing and notices:
- KeePass is distributed by its upstream author under its own license terms.
- Attribution details and license references for the bundled copy are recorded in `THIRD_PARTY_LICENSES.md`.

### 2) Yubico Native Challenge-Response Libraries

- Component family: yubikey-personalization native libraries and dependencies
- Upstream project: https://github.com/Yubico/yubikey-personalization
- Upstream release page: https://opensource.yubico.com/yubikey-personalization/releases.html
- Bundled paths in this repository:
  - KeeChallenge/lib/32bit/libykpers-1-1.dll
  - KeeChallenge/lib/32bit/libyubikey-0.dll
  - KeeChallenge/lib/32bit/libjson-0.dll
  - KeeChallenge/lib/32bit/libjson-c-2.dll
  - KeeChallenge/lib/64bit/libykpers-1-1.dll
  - KeeChallenge/lib/64bit/libyubikey-0.dll
  - KeeChallenge/lib/64bit/libjson-0.dll
  - KeeChallenge/lib/64bit/libjson-c-2.dll
- Purpose in this repository: runtime native dependencies required for YubiKey HMAC-SHA1 challenge-response communication

Licensing and notices:
- These binaries are distributed under their respective upstream licenses.
- License obligations and copyright attributions are governed by the upstream release package and source repositories.
- Attribution details and license references for bundled Yubico binaries are recorded in `THIRD_PARTY_LICENSES.md`.
- When updating these binaries, maintainers must review and carry forward all required third-party notices.

## Maintainer Update Checklist

When updating any bundled third-party binary:

1. Record upstream source URL and release identifier.
2. Verify applicable license terms for each binary.
3. Update this file with new version/provenance details.
4. Ensure required license/notice texts are included in releases if required by upstream terms.
5. Confirm release notes mention the dependency update.

## Attribution Scope

This file documents third-party components bundled directly in this repository.
It does not replace or override any upstream license terms.
