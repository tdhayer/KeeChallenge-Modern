# Contributing to KeeChallenge-Modern

Thanks for your interest in contributing! This guide covers everything you need to get started.

## Table of Contents

- [Getting Started](#getting-started)
- [Building](#building)
- [Running Locally](#running-locally)
- [Code Style](#code-style)
- [Submitting Changes](#submitting-changes)
- [Reporting Bugs](#reporting-bugs)
- [Security Issues](#security-issues)

---

## Getting Started

**Prerequisites:**
- Windows (KeePass is Windows-only; the plugin targets `net48`)
- [.NET SDK 8+](https://dotnet.microsoft.com/en-us/download) (used to drive the `net48` build)
- [KeePass 2.x](https://keepass.info/download.html) installed
- A YubiKey (series 4 or 5) for functional testing — optional for pure-logic work

**Fork and clone:**
```
git clone https://github.com/<you>/KeeChallenge-Modern.git
cd KeeChallenge-Modern
```

---

## Building

```powershell
dotnet build KeeChallenge/src/KeeChallenge.csproj -c Release
```

The output DLL lands at `KeeChallenge/src/bin/Release/net48/KeeChallenge.dll`.

The `KeePass.exe` reference is committed at `KeeChallenge/lib/KeePass.exe`. If your copy of KeePass differs in version, replace it and commit with `git add -f`.
If you update any bundled third-party binaries, also update `THIRD_PARTY_NOTICES.md` and `THIRD_PARTY_LICENSES.md` in the same change.

---

## Running Locally

1. Build the plugin (see above).
2. Copy `KeeChallenge.dll` and the appropriate native DLLs (`KeeChallenge/lib/32bit/` or `64bit/`) to your KeePass `Plugins` folder.
3. Restart KeePass — the plugin loads automatically.

---

## Code Style

- C# 7.3 feature set (target is `net48`; do not use C# 8+ nullable reference types or `default interface members`)
- Use `MessageService.ShowWarning` / `MessageService.ShowInfo` (KeePass-native) — not `MessageBox.Show` — for user-facing error messages
- Security-sensitive comparisons: use `FixedTimeEquals` (already present in `KeeChallenge.cs`), never `==` or `string.Compare`
- No external NuGet packages without prior discussion; the plugin must remain a single self-contained DLL

---

## Submitting Changes

1. Create a feature branch: `git checkout -b fix/my-issue`
2. Make your changes and confirm the build is clean: `dotnet build ... -c Release /nologo /clp:ErrorsOnly`
3. Push and open a Pull Request against `main`
4. CI will run automatically and must pass before merge

**Commit message format:** `type(scope): short description`  
Examples: `fix(yubikey): handle null response gracefully`, `feat(ci): add test project`

---

## Reporting Bugs

Open a [GitHub Issue](https://github.com/tdhayer/KeeChallenge-Modern/issues) and include:
- KeePass version
- Windows version
- YubiKey model and firmware version (if relevant)
- Steps to reproduce
- Expected vs. actual behaviour

---

## Security Issues

**Do not open a public issue for security vulnerabilities.**  
See [SECURITY.md](SECURITY.md) for the private reporting process.
