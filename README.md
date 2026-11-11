# CUETools
CUETools is a tool for lossless audio/CUE sheet format conversion. The goal is to make sure the album image is preserved accurately. A lossless disc image must be lossless not only in preserving contents of the audio tracks, but also in preserving gaps and CUE sheet contents. Many applications lose vital information upon conversion, and don't support all possible CUE sheet styles. For example, foobar2000 loses disc pre-gap information when converting an album image, and doesn't support gaps appended (noncompliant) CUE sheets.
# Supported formats
Supports WAV, FLAC, APE, LossyWAV, ALAC, TTA, and WavPack audio input/output. Audio must be 16-bit, 44.1kHz samples stereo (i.e. CD PCM). Supports every CUE sheet style (embedded, single file, gaps appended/prepended/left out). It is also possible to process a set of audio files in a directory without a CUE sheet, or use a RAR archive as an input without unpacking it.
# CUERipper
CUERipper is a utility for extracting digital audio from CDs, an open-source alternative to EAC. It has a lot fewer configuration options, so is somewhat easier to use, and is included in CUETools package. It supports MusicBrainz and freeDB metadata databases, AccurateRip and CTDB.
# Installing
Prebuilt binaries can be downloaded from [CUETools Download](http://cue.tools/wiki/CUETools_Download).
## Installing from sources
* Get the CUETools sources from GitHub ([https://github.com/gchudov/cuetools.net](https://github.com/gchudov/cuetools.net)):  
`git clone https://github.com/gchudov/cuetools.net.git`
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
* The updated solution requires Visual Studio 2026 (Community Edition will work), the .NET 10 SDK, and C++/CLI support for native wrappers.
  * Install the .NET 10 SDK and the .NET Framework developer tools needed by the retained legacy projects
  * Install an appropriate Windows SDK version (e.g. 10.0.16299.0 or newer)
  * Install the Microsoft Visual Studio Installer Projects
* Open cuetools.net\CUETools.sln
* Select 'Any CPU' under 'Solution Platforms'
* Build solution

### Command-line managed build
Run from a Visual Studio Developer PowerShell using Visual Studio's .NET Framework MSBuild:

```powershell
msbuild CUETools.sln /restore /m /p:Configuration=Debug '/p:Platform=Any CPU'
```

The `Any CPU` configuration builds the managed projects. Native codecs use the separate `Win32` and `x64` configurations. Use Visual Studio MSBuild rather than `dotnet build` for the legacy projects in this solution.

The `net20` targets use NuGet reference assemblies. When these are present, `Directory.Build.targets` bypasses the legacy runtime installation check and generates resources with the current MSBuild task, so compilation does not require launching CLR 2.0 tools. Installing .NET 3.5 development tools alone does not ensure those runtime tools can launch. Keep framework targets compatible across project references; in particular, `Freedb` must remain compatible with its `net47` consumers.

## .NET 10 build and distribution
The staged migration uses `CUETools.Net10.slnx` and `.Net10.csproj` companion projects for legacy desktop code. The original solution and desktop project files remain available during integration testing. See [NET10-MIGRATION.md](NET10-MIGRATION.md) for status and remaining checks.

From PowerShell in the repository root, after preparing third-party sources as above:

```powershell
./build-net10.ps1 -Configuration Release
./publish-net10.ps1 -Configuration Release -NoBuild
```

The build script compiles the managed applications and x64 native codecs and runs the regression tests. The publish script assembles separate application folders under `bin/Release/net10-dist/win-x64/`, including plugins and native DLLs. Start `CUETools/CUETools.exe` or `CUERipper/CUERipper.exe` inside that directory. These framework-dependent builds require the .NET 10 Desktop Runtime for desktop apps and the Visual C++ runtime for native codecs.

CUEPlayer now stores its playlist in `%LOCALAPPDATA%\CUETools\CUEPlayer\playlist.sqlite`. Existing SQL Server Compact `.sdf` files are preserved; automatic import is not implemented. The DirectSound compatibility entry point uses WASAPI on .NET 10.

The modern EAC plugin builds separately for evaluation. It is not installed by these scripts: EAC host compatibility must be verified before replacing the existing .NET Framework plugin or its installer. Hardware ripping, GPU/OpenCL behavior and x86 distribution validation remain separate integration checks.
