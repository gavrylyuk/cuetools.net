# .NET 10 migration

## Objective and approach
Migrate the managed solution to .NET 10, using net10.0-windows for Windows desktop code. Retain existing targets during the transition so each stage has a working baseline. Native codec libraries remain native projects; managed wrappers and packaging must match process architecture. Do not remove old targets until application and integration checks pass.

The checkout already contains local framework upgrades and third-party changes. Preserve unrelated changes. SDK available at migration start: 10.0.403; Visual Studio MSBuild: 18.11.1.

## Checkpoints
1. **Implemented and validated: core and test infrastructure.** Add net10.0 to Codecs, CDImage, Parity, AccurateRip, Compression. Modernize TestHelpers and TestParity project files. Run existing checksum/parity tests on .NET 10 and verify the legacy solution build.
2. **Implemented; automated regressions pass: managed codecs and processing.** Migrate managed codec/DSP/compression libraries, CTDB, Freedb, and Processor in dependency order. Review conditional compilation and third-party assemblies. Validate PCM/FLAC round trips and checksums.
3. **Implemented and built: console applications.** Move the netcoreapp2.0 applications to net10.0; update dependency copying and output layout. Verify command-line help and fixture conversions.
4. **Implemented and built; integration checks remain: desktop applications.** Convert WinForms/WPF projects to SDK style and net10.0-windows. Replace Remoting-based single-instance IPC with named pipes, migrate configuration/settings, and replace unsupported DirectSound dependencies. Verify startup, forms, settings, plugin loading and second-instance forwarding.
5. **x64 native build implemented; external-host/hardware checks pending: native and external integration.** Port the TTA C++/CLI wrapper; build native codecs for supported architectures. Verify WMA/OpenCL/DirectSound behavior and EAC hosting compatibility. Keep a legacy EAC bridge if required by the host until a replacement is verified.
6. **Local publishing and approved CI changes implemented; legacy retirement pending: release and retirement.** Update CI, publish/copy scripts and installer. Produce a usable application distribution containing native libraries and plugins. Validate encoding/decoding, audio checksums, disc verification and actual CD ripping. Remove obsolete targets and temporary legacy build workarounds only after replacement coverage passes.

## Validation rules
- A successful build is necessary but does not prove audio or device behavior.
- Reuse existing regression tests and record exact commands and results below.
- Clearly distinguish automated tests, manual checks and hardware checks that remain unperformed.
- Keep generated logs/results under bin or other ignored build directories.

## Execution record
- Baseline before migration: Debug / Any CPU solution build succeeded using Visual Studio MSBuild, including legacy runtime-tool workarounds in Directory.Build.targets.

## Implemented checkpoints
- Added net10.0 targets to the SDK libraries and replaced netcoreapp2.0 console targets with net10.0. Retained legacy library targets during validation.
- Added CUETools.Net10.slnx and additive .Net10.csproj desktop/plugin/test projects. Original desktop project files remain intact. Companion projects isolate obj/Net10 intermediates to avoid collisions.
- Added the Windows-specific Processor target to retain artwork processing. Modern plugin loading resolves plugin DLLs by path, and modern project references include transitive runtime dependencies in application manifests.
- Replaced Remoting in the modern CUETools entry point with per-user named pipes. The original entry point remains available to the legacy build.
- Replaced CUEPlayer's SQL CE playlist adapter with transactional SQLite saves, database-assigned IDs and tests for concurrent additions, edits, deletions and rollback. Regenerated the typed playlist model from the existing schema after removing obsolete provider annotations. Existing .sdf files are not modified or automatically imported.
- Preserved Windows ANSI/code-page behavior for CUE sheets and metadata on modern .NET. Corrected a file-handle leak in UTF-8-to-ANSI fallback. Added a regression test.
- Preserved the legacy WinForms default font; visual inspection confirmed that this corrects clipped CUEPlayer deck controls.
- Added a WASAPI-backed DirectSound compatibility entry point. The EAC .NET 10 companion builds for evaluation; host compatibility is not yet proven and the legacy installer remains in place.
- Ported TTA to a separate .NET 10 C++/CLI project. Built the x64 native FLAC, Monkey's Audio, WavPack, TTA and AVX components. Corrected the AVX Debug CRT/instrumentation mismatch.
- Captured third-party modifications in reproducible patches, including the new OpenCL project patch. Reverse apply-checks pass for the updated TagLib and WindowsMedia patches.
- Added build-net10.ps1 and publish-net10.ps1. The distribution uses separate per-app folders to keep portable and Windows-specific dependency manifests consistent.
- User approved the prepared CI patch; it is applied. Existing CI/release jobs use the VS 2026 runner/toolchain, and CI-net10-windows.yml builds/tests and uploads the modern x64 applications. No CI run, commit, push or release publication was performed here.

## Validation record
- Full x64 Release build through build-net10.ps1 completed successfully.
- Existing parity suite: 18 passed, 4 pre-existing skips.
- Existing codec suite: 34 passed, 1 pre-existing skip; includes native FLAC and the existing ALAC/FLAC fixtures.
- Existing processor suite: 4 passed, 1 pre-existing skip; includes CD-Extra, enhanced-CD and single-track CUE fixtures.
- Migration suite: 5 managed cases plus 6 native TTA cases covering partial/exact/multiple frames at 16 and 24 bits. Final end-to-end results are recorded below.
- Managed FLAC CLI encode/verify/decode round trip: original and decoded PCM bytes match exactly.
- Raw CUETools, CUERipper and CUEPlayer main windows were inspected. Raw CUERipper correctly exposed missing plugin deployment; the publishing step supplies those plugins. Packaged startup verification is recorded below when complete.
- NuGet audit exposed a vulnerable native SQLite minimum. The modern player now pins Microsoft.Data.Sqlite 10.0.13 and SQLitePCLRaw.bundle_e_sqlite3 2.1.14; the persistence suite passes with the patched bundle.

## Remaining integration work before retiring the legacy build
- Complete ripping with a supported drive and inserted disc. Packaged CUERipper successfully enumerated the connected HL-DT-ST GP60NB60 drive; no disc was present, so ripping itself remains untested.
- Exercise GPU/OpenCL encoding/parity, WMA, external encoders, streaming and live metadata service behavior. These were compiled but not all exercised.
- Verify EAC's hosting/activation behavior before replacing its installed plugin or installer. A successful .NET 10 plugin compilation does not prove EAC compatibility.
- Validate an x86 build/distribution if continued x86 support is required; the new build/publish scripts currently produce x64 applications.
- Audit existing custom WinForms designer-serialization annotations. WFO1000 remains visible as a warning in the companion projects; this is not a claim of complete designer compatibility.
- Import old SQL CE playlists only when an actual .sdf source and a working export path are available.
- Run the approved workflows on GitHub, then remove old targets, companion duplication and legacy-only workarounds after integration validation.

## Final package integration checks
- Original CUETools.sln Release / Any CPU build succeeded after the managed migration.
- Local per-app x64 distributions were published under bin/Release/net10-dist/win-x64.
- Packaged CUERipper loaded its managed/native plugin layout and enumerated the connected CD drive. It reported no medium present rather than a missing ripper module.
- Published native FLAC, WavPack and Monkey's Audio encode/decode round trips matched the source PCM exactly.
- A published TTA short-file round trip exposed a pre-existing native encoder bug: the first frame was not shortened when it was also the final partial frame. Corrected TTAWriter frame initialization, added native regression cases and refreshed the TTA plugin in every published app. The fixed published TTA round trip matches source PCM exactly.
- The hidden startup-diagnostic process was closed so it does not keep the CD drive open. User-controlled windows were left alone.

Final Release verification: 67 tests passed, 6 pre-existing skips, zero failures. The final managed build/test script completed successfully after all code fixes. Native TTA cases cover 16-bit and 24-bit short, exact-frame and multi-frame inputs. The original solution's Release / Any CPU build also passed. No remote CI execution or legacy-target retirement is claimed.
