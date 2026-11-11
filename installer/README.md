# Installer validation

The Windows x64 Setup.exe uses Inno Setup and a self-contained .NET 10 payload. Build with `./build-installer.ps1 -Configuration Release` or add `-NoBuild` after a successful Release build.

Local validation completed:

- Compiled the self-contained Windows desktop payload and installer.
- Installed silently into a temporary directory using `/CURRENTUSER` with a separate Start menu group.
- Compared all 353 installed payload files byte-for-byte with the published files (PDBs are excluded).
- Launched the installed executable and confirmed `coreclr.dll` loaded from its installation directory.
- Reinstalled the same version successfully, then uninstalled and checked executable/uninstall-registration removal.
- Inspected native codec imports; packaged codecs use Windows system DLLs and the bundled prerequisite's VC v14 runtime family.

The test PC already had a current Visual C++ runtime, so the missing-prerequisite installation path has not been exercised on a clean Windows VM. Wine, ARM64 emulation and upgrades from a different release version have not been tested. The Setup executable is unsigned. The prerequisite and compiler downloads are checked for valid publisher signatures by the build script.

The existing branch's Release regression suite passed before installer work: 64 passed, 6 existing skips. Installer changes do not modify audio-processing code.

## 2.2.6.1 web-link fix
All five CUETools web-link launch sites explicitly request Windows shell execution, which uses the default browser under modern .NET. The rebuilt website-button handler was invoked in a temporary .NET 10 harness and completed without exception. The self-contained installer was rebuilt with the four-part file version to distinguish this fix from the initial package.
