# Third-Party Attribution and Licensing References

This file records attribution details and license references for third-party components bundled in this repository.

## 1) KeePass Reference Assembly

- Component: KeePass
- Bundled file: `KeeChallenge/lib/KeePass.exe`
- Observed version: `2.61.0.0`
- Observed company metadata: `Dominik Reichl`
- Observed copyright metadata: `Copyright © 2003-2026 Dominik Reichl`
- Upstream project: https://keepass.info/
- Upstream source: https://sourceforge.net/p/keepass/code/HEAD/tree/
- License reference (authoritative): https://keepass.info/help/base/license.html

Notes:
- The bundled `KeePass.exe` is used as a compile-time reference for the plugin project.
- Upstream license terms govern this component.

## 2) Yubico Native Challenge-Response Libraries

- Component family: yubikey-personalization native libraries and dependencies
- Bundled files:
  - `KeeChallenge/lib/32bit/libykpers-1-1.dll`
  - `KeeChallenge/lib/32bit/libyubikey-0.dll`
  - `KeeChallenge/lib/32bit/libjson-0.dll`
  - `KeeChallenge/lib/32bit/libjson-c-2.dll`
  - `KeeChallenge/lib/64bit/libykpers-1-1.dll`
  - `KeeChallenge/lib/64bit/libyubikey-0.dll`
  - `KeeChallenge/lib/64bit/libjson-0.dll`
  - `KeeChallenge/lib/64bit/libjson-c-2.dll`
- Upstream project: https://github.com/Yubico/yubikey-personalization
- Upstream releases: https://opensource.yubico.com/yubikey-personalization/releases.html
- License reference set (authoritative):
  - https://github.com/Yubico/yubikey-personalization/blob/master/COPYING
  - https://github.com/Yubico/yubico-c/blob/master/COPYING
  - https://github.com/json-c/json-c/blob/master/COPYING

Notes:
- These native binaries are redistributed from upstream release bundles.
- Upstream license terms govern each bundled binary.

## Distribution Policy

For every public release artifact, include at minimum:

1. `LICENSE`
2. `THIRD_PARTY_NOTICES.md`
3. `THIRD_PARTY_LICENSES.md`

When updating bundled third-party binaries, verify upstream license requirements and update this file together with `THIRD_PARTY_NOTICES.md`.
