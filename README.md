# CUETools
CUETools is a tool for lossless audio/CUE sheet format conversion. The goal is to make sure the album image is preserved accurately. A lossless disc image must be lossless not only in preserving contents of the audio tracks, but also in preserving gaps and CUE sheet contents. Many applications lose vital information upon conversion, and don't support all possible CUE sheet styles. For example, foobar2000 loses disc pre-gap information when converting an album image, and doesn't support gaps appended (noncompliant) CUE sheets.
# Supported formats
Supports WAV, FLAC, APE, LossyWAV, ALAC, TTA, and WavPack audio input/output. Audio must be 16-bit, 44.1kHz samples stereo (i.e. CD PCM). Supports every CUE sheet style (embedded, single file, gaps appended/prepended/left out). It is also possible to process a set of audio files in a directory without a CUE sheet, or use a RAR archive as an input without unpacking it.
# Installing
Prebuilt binaries can be downloaded from [CUETools Download](http://cue.tools/wiki/CUETools_Download).
## Installing from sources
* Get the CUETools sources from GitHub ([https://github.com/gavrylyuk/cuetools.net](https://github.com/gavrylyuk/cuetools.net)):  
`git clone --branch cuetools-only https://github.com/gavrylyuk/cuetools.net.git`
* Get the required submodules using:  
`git submodule update --init --recursive`
* Apply patches to ThirdParty modules:  
`git apply --directory=ThirdParty/flac ThirdParty/submodule_flac_CUETools.patch --whitespace=nowarn`  
`powershell -c "Expand-Archive ThirdParty/MAC_SDK/MAC_1086_SDK.zip -DestinationPath ThirdParty/MAC_SDK/"`  
`git apply --directory=ThirdParty/MAC_SDK ThirdParty/ThirdParty_MAC_SDK_CUETools.patch`  
`git apply --directory=ThirdParty/taglib-sharp ThirdParty/submodule_taglib-sharp_CUETools.patch`  
`git apply --directory=ThirdParty/WavPack ThirdParty/submodule_WavPack_CUETools.patch`  
`git apply --directory=ThirdParty/WindowsMediaLib ThirdParty/submodule_WindowsMediaLib_CUETools.patch`\
`git apply --directory=ThirdParty/openclnet ThirdParty/submodule_openclnet_net10.patch`
* Install Visual Studio 2026 with C++/CLI support, the .NET 10 SDK and a Windows SDK.
* Open `CUETools.Net10.slnx` for CUETools development.

## CUETools-only branch
This branch contains the CUETools desktop utility, its shared libraries, codec/compression plugins, native codecs and regression tests. Other applications are retained on the `net10-migration` branch. `CUETools.Ripper` supplies interfaces used by Processor; the separate ripping applications and hardware backend are excluded.

After preparing third-party sources above:

```powershell
./build-net10.ps1 -Configuration Release
./publish-net10.ps1 -Configuration Release -NoBuild
```

The build compiles CUETools, its plugins and native x64 codecs, then runs parity, codec, processor and CUETools migration tests. The publish script produces only CUETools under `bin/Release/cuetools-only/win-x64/CUETools/`. Start `CUETools.exe` there with the .NET 10 Desktop Runtime and Visual C++ x64 runtime installed.

Use `CUETools.Net10.slnx`; the old multi-application solution, collection scripts, release workflow and unrelated applications have been removed. Shared libraries retain their existing framework targets for now. [NET10-MIGRATION.md](NET10-MIGRATION.md) records historical migration checks for the full suite; sections about removed applications describe the parent branch.

