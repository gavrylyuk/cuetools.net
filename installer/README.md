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

## 2.2.6.2 Network browser
Added Desktop > Network with Windows Shell computer/share discovery, saved UNC server/share/folder locations, refresh and asynchronous directory loading. Existing Windows credentials are used; no credentials are stored by this feature. Network requests that finish after a node is refreshed or collapsed are ignored. Newly loaded children inherit checkbox selections without changing existing selections.

Validation: Release application and self-contained installer builds passed. All 24 migration/native tests passed, including 16 new network tests for UNC validation, persistence, background completion, checkbox inheritance, stale results and offline errors. Computer-use verification in an isolated test copy confirmed discovery of a network computer, adding and browsing a local UNC folder, restoring that saved location after restart, and retaining the selected location when the Add dialog closes. The user's installed application was left running. Remote NAS authentication and Wine were not separately tested.

## 2.2.6.3 CTDB browser destination
The match dialog's CTDB button now maps the public API host (`db.cuetools.net`) to `https://db.cue.tools/ui/?tocid=...`. Custom server schemes, ports and base paths are preserved, and TOC IDs are URL-escaped. The old public web address returned HTTP 403 during verification; the current HTTPS web UI returned HTTP 200 and its API returned disc data for a known TOCID. All 12 URL-handling regression tests passed. The Release self-contained application and Setup executable were rebuilt.
