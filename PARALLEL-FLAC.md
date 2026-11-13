# Parallel FLAC conversion

The Windows release contains both original and parallel conversion modes. It uses the standard CUETools settings and local database. The .NET runtime is included in the installer. Windows x64 is the tested platform; Wine has not been tested.

## Use parallel conversion

Select **Encode > Tracks > Lossless > flac > flac.exe**, with the default script. Select a CUE, a folder containing CUEs, or multiple CUEs. Click **Go**, choose a local temporary folder and start with **4 workers**. Original mode remains available in the options dialog. The selected external FLAC executable must be installed/configured as usual.

The decoder and disc verification remain sequential to preserve disc offsets, gaps and AccurateRip/CTDB ordering. Tracks are temporarily spooled to WAV, then encoded concurrently. Each output is decoded internally and its PCM is checked before album tagging and publication. Temporary disk capacity must accommodate several uncompressed tracks; increasing workers also increases disk and CPU load. Albums run in order using the same worker budget.

The queue shows album and track stages, decode/disc verification, encoding/checking, elapsed time and weighted overall progress. Hide the queue and click the status text to reopen it. Pause affects decoding checkpoints and owned encoder processes. Stop current album skips it; Stop all keeps completed albums and discards the incomplete album. Stop + delete rolls back this batch's published files and restores approved overwritten files. Outputs changed by another program are preserved and reported. Backup files are retained until the batch is closed.

## Manual acceptance checklist

Use copies of real albums and a separate output folder for initial testing.

- Split FLAC, APE, WAV and WV images with CUE; compare track boundaries, tags, artwork and filenames with Original mode.
- Reencode separate tracks with a CUE using the configured recent flac.exe; check tags and resulting audio.
- Select several CUEs, including different source formats. Confirm ordered album results, per-track progress and collision errors for duplicate output names.
- Test a local SSD and NAS/network input. Compare elapsed times with Original mode and worker counts 1, 2 and 4. Decoding or network IO may limit speedups.
- Check real AccurateRip/CTDB verification and metadata dialogs, including unknown discs and cancelled dialogs. Automated tests do not contact those services.
- Pause/resume while decoding and encoding. Stop a current album while paused. Stop the whole batch while paused. Confirm no encoder processes remain.
- Test Stop + delete on disposable outputs, including approved overwrites; confirm originals are restored and source audio remains intact.
- Test invalid encoder options, insufficient temporary space, an unavailable network source and closing the application during conversion. Confirm errors are visible and incomplete output is not published.

## Automated validation

Audio comparisons cover WAV/FLAC/APE/WV image and separate-track sources, gap handling and positive/negative offsets. Serial and parallel PCM hashes, tags and generated CUE text agree. Process tests cover bounded concurrency, pause/cancellation, 16/24-bit samples, stdin/file input and stdout output templates. Output transaction tests cover collisions, rollback, source protection and externally changed files. Installer installation/uninstallation is also smoke tested.

Filename reservations are validated as each album starts, rather than resolving metadata and output names for the entire batch up front. Windows process suspension is used for pause; compatibility under Wine requires manual testing.
