#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using CUETools.Codecs;
using ExternalSettings = CUETools.Codecs.CommandLine.EncoderSettings;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CUETools.Net10.Tests")]

namespace CUETools.Processor
{
    public sealed class ParallelTrackProgress
    {
        public string Album, Output, DisplayOutput, Stage, Error;
        public long Samples, Written;
        public double Percent;
        public bool Complete;
        internal ParallelTrackProgress Copy() => (ParallelTrackProgress)MemberwiseClone();
    }
    public sealed class ParallelAlbumProgress
    {
        public string Input, Stage = "Waiting", Error;
        public long Samples;
        public double DecodePercent;
        public bool Complete;
        public List<ParallelTrackProgress> Tracks = new List<ParallelTrackProgress>();
        public double Fraction => Complete ? 1 : Math.Min(.95, .4 * DecodePercent + .5 * Tracks.Sum(t => t.Samples * (t.Complete ? 1 : t.Stage == "Checking PCM" ? .8 + .2 * t.Percent : t.Stage == "Encoding" ? .8 * t.Percent : 0)) / Math.Max(1, Math.Max(Samples, Tracks.Sum(t => t.Samples))));
    }
    public sealed class ParallelConversionBatch : IDisposable
    {
        internal readonly object Sync = new object();
        internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
        private readonly ManualResetEventSlim running = new ManualResetEventSlim(true);
        private readonly List<ParallelAlbumProgress> albums = new List<ParallelAlbumProgress>();
        private readonly List<OutputReceipt> receipts = new List<OutputReceipt>();
        private readonly Dictionary<string, string> reserved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Process> processes = new List<Process>();
        public int MaxActiveEncoders { get; private set; }
        internal Process[] EncoderProcesses { get { lock (Sync) return processes.ToArray(); } }
        public readonly int Workers;
        public readonly string TempDirectory;
        public readonly Stopwatch Elapsed = Stopwatch.StartNew();
        public bool Paused { get; private set; }
        public bool Finished { get; private set; }
        public bool DeleteRequested { get; private set; }
        public string CleanupError { get; private set; }
        public ParallelFlacSession ActiveSession { get; internal set; }
        public ParallelConversionBatch(int workers, string tempDirectory)
        {
            if (workers < 1 || workers > 16) throw new ArgumentOutOfRangeException(nameof(workers));
            Workers = workers; TempDirectory = Path.GetFullPath(tempDirectory); Directory.CreateDirectory(TempDirectory);
        }
        public void SetPending(IEnumerable<string> inputs)
        {
            lock (Sync) foreach (string input in inputs.Where(File.Exists)) Album(input);
        }
        internal ParallelAlbumProgress Album(string input)
        {
            string full = Path.GetFullPath(input);
            var album = albums.Find(a => a.Input.Equals(full, StringComparison.OrdinalIgnoreCase));
            if (album == null) { album = new ParallelAlbumProgress { Input = full }; albums.Add(album); }
            return album;
        }
        public void Preflight(CUEConfig config)
        {
            foreach (var album in Snapshot().Where(a => a.Samples == 0 && a.Stage == "Waiting"))
            {
                Checkpoint();
                try
                {
                    var cue = new CUESheet(config);
                    try { cue.Open(album.Input); lock (Sync) Album(album.Input).Samples = (long)cue.TOC.AudioLength * 588; }
                    finally { cue.Close(); }
                }
                catch (Exception error) { lock (Sync) Album(album.Input).Error = "Preflight: " + error.Message; }
            }
        }
        public List<ParallelAlbumProgress> Snapshot()
        {
            lock (Sync) return albums.Select(a => new ParallelAlbumProgress { Input = a.Input, Samples = a.Samples, Stage = a.Stage, Error = a.Error, Complete = a.Complete, DecodePercent = a.DecodePercent, Tracks = a.Tracks.Select(t => t.Copy()).ToList() }).ToList();
        }
        public void FailAlbum(string input, Exception error)
        { lock (Sync) { var album = Album(input); album.Error = error.Message; album.Stage = Cancellation.IsCancellationRequested || error is OperationCanceledException ? "Stopped" : "Failed"; } }
        public void ReportSource(string input, CUEToolsProgressEventArgs progress)
        {
            lock (Sync)
            {
                var album = Album(input);
                if (progress.status.StartsWith("Writing track", StringComparison.Ordinal))
                { album.DecodePercent = Math.Max(album.DecodePercent, Math.Min(1, progress.percent)); album.Stage = "Decoding / disc verification"; }
            }
        }
        internal void Phase(ParallelAlbumProgress album, string stage) { lock (Sync) album.Stage = stage; }
        internal void Track(ParallelTrackProgress track, string stage, double percent = 0, string error = null)
        { lock (Sync) { track.Stage = stage; track.Percent = Math.Max(0, Math.Min(1, percent)); track.Error = error; } }
        internal void Reserve(string path, string input)
        {
            path = Path.GetFullPath(path);
            lock (Sync)
            {
                if (reserved.TryGetValue(path, out string previous) && !previous.Equals(input, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Two albums target the same output: " + path + ". First album: " + previous);
                reserved[path] = input;
            }
        }
        public void Checkpoint()
        {
            while (!running.Wait(100)) if (Cancellation.IsCancellationRequested) throw new StopException();
            if (Cancellation.IsCancellationRequested) throw new StopException();
        }
        internal void Register(Process process)
        {
            lock (Sync)
            {
                if (Cancellation.IsCancellationRequested) { TryKill(process); throw new OperationCanceledException(); }
                processes.Add(process);
                MaxActiveEncoders = Math.Max(MaxActiveEncoders, processes.Count);
                if (Paused) Suspend(process, true);
            }
        }
        internal void Unregister(Process process) { lock (Sync) processes.Remove(process); }
        public void TogglePause()
        {
            lock (Sync)
            {
                if (Finished || Cancellation.IsCancellationRequested) return;
                bool pause = !Paused;
                if (pause) running.Reset();
                try { foreach (var process in processes.ToArray()) Suspend(process, pause); Paused = pause; }
                catch { foreach (var process in processes.ToArray()) { try { Suspend(process, false); } catch { } } Paused = false; running.Set(); throw; }
                if (!pause) running.Set();
            }
        }
        public void Cancel(bool deleteGenerated)
        {
            lock (Sync)
            {
                DeleteRequested |= deleteGenerated; Cancellation.Cancel(); running.Set();
                foreach (var process in processes.ToArray()) TryKill(process);
                Paused = false;
            }
        }
        internal static void TryKill(Process process)
        {
            try { if (!process.HasExited) { try { NtResumeProcess(process.Handle); } catch { } process.Kill(true); } } catch (InvalidOperationException) { }
        }
        private static void Suspend(Process process, bool pause)
        {
            if (process.HasExited) return;
            int status = pause ? NtSuspendProcess(process.Handle) : NtResumeProcess(process.Handle);
            if (status < 0 && !process.HasExited) throw new IOException("Windows could not " + (pause ? "pause" : "resume") + " an encoder (NTSTATUS " + status.ToString("X8") + ").");
        }
        [DllImport("ntdll.dll")] private static extern int NtSuspendProcess(IntPtr process);
        [DllImport("ntdll.dll")] private static extern int NtResumeProcess(IntPtr process);
        internal void Keep(OutputReceipt receipt) { lock (Sync) receipts.Add(receipt); }
        public void Finish()
        {
            if (DeleteRequested)
            {
                var errors = new List<string>();
                foreach (var receipt in receipts.AsEnumerable().Reverse()) try { receipt.Rollback(); } catch (Exception e) { errors.Add(e.Message); }
                CleanupError = string.Join(Environment.NewLine, errors);
            }
            lock (Sync)
            {
                foreach (var album in albums.Where(a => !a.Complete && (a.Stage == "Waiting" || Cancellation.IsCancellationRequested))) album.Stage = "Stopped";
                if (DeleteRequested && string.IsNullOrEmpty(CleanupError)) foreach (var album in albums.Where(a => a.Complete)) album.Stage = "Generated files deleted";
                Finished = true; Elapsed.Stop();
            }
        }
        public void Dispose()
        {
            if (!Finished) { Cancel(false); Finish(); }
            foreach (var receipt in receipts) receipt.ReleaseBackups();
            running.Dispose(); Cancellation.Dispose();
        }
    }

    public sealed class ParallelAlbumOutput : IDisposable
    {
        private readonly ParallelConversionBatch batch;
        private readonly string input, destination;
        private readonly Dictionary<string, string> approved;

        public string StageDirectory { get; }
        public ParallelAlbumOutput(ParallelConversionBatch batch, string input, string outputDirectory, IEnumerable<string> planned, IEnumerable<string> sources, IEnumerable<string> overwriteApproved)
        {
            this.batch = batch; this.input = Path.GetFullPath(input); destination = Path.GetFullPath(outputDirectory);
            var sourcePaths = new HashSet<string>(sources.Append(input).Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
            foreach (string path in planned)
            {
                string full = Path.GetFullPath(path);
                if (sourcePaths.Contains(full)) throw new IOException("An output would replace an input file: " + full);
                EnsureInside(destination, full); batch.Reserve(full, this.input);
            }
            approved = overwriteApproved.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(p => p, p => File.Exists(p) ? OutputReceipt.Hash(p) : null, StringComparer.OrdinalIgnoreCase);
            StageDirectory = Path.Combine(destination, ".cuetools-stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(StageDirectory);
        }
        internal static void EnsureInside(string parent, string path)
        {
            string prefix = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new IOException("An output path is outside the album directory: " + path);
        }
        public void ValidatePaths(IEnumerable<string> paths) { foreach (string path in paths) EnsureInside(StageDirectory, path); }
        public void Commit()
        {
            var receipt = new OutputReceipt();
            try
            {
                foreach (string source in Directory.GetFiles(StageDirectory, "*", SearchOption.AllDirectories))
                {
                    batch.Checkpoint();
                    string target = Path.GetFullPath(Path.Combine(destination, Path.GetRelativePath(StageDirectory, source)));
                    EnsureInside(destination, target); batch.Reserve(target, input);
                    if (File.Exists(target) && (!approved.TryGetValue(target, out string hash) || hash == null || OutputReceipt.Hash(target) != hash))
                        throw new IOException("An existing or changed file was not approved for overwrite: " + target);
                }
                foreach (string source in Directory.GetFiles(StageDirectory, "*", SearchOption.AllDirectories))
                {
                    batch.Checkpoint();
                    string target = Path.Combine(destination, Path.GetRelativePath(StageDirectory, source));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    receipt.Promote(source, target);
                }
                batch.Checkpoint(); batch.Keep(receipt);
            }
            catch { receipt.Rollback(); throw; }
        }
        public void Dispose()
        {
            EnsureInside(destination, StageDirectory);
            if (Directory.Exists(StageDirectory)) Directory.Delete(StageDirectory, true);
        }
    }
    internal sealed class OutputReceipt
    {
        private sealed class Entry { internal string Target, Backup, Hash; }
        private readonly List<Entry> entries = new List<Entry>();
        private bool preserveBackups;
        internal static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
        internal void Promote(string source, string target)
        {
            var entry = new Entry { Target = target, Hash = Hash(source) };
            if (File.Exists(target)) { entry.Backup = target + ".cuetools-backup-" + Guid.NewGuid().ToString("N"); File.Move(target, entry.Backup); }
            try { File.Move(source, target); entries.Add(entry); }
            catch { if (entry.Backup != null) File.Move(entry.Backup, target); throw; }
        }
        internal void Rollback()
        {
            var errors = new List<string>();
            foreach (var entry in entries.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(entry.Target))
                    {
                        if (Hash(entry.Target) != entry.Hash) throw new IOException("Output changed after conversion; preserved it and its backup: " + entry.Target);
                        File.Delete(entry.Target);
                    }
                    if (entry.Backup != null && File.Exists(entry.Backup)) File.Move(entry.Backup, entry.Target);
                }
                catch (Exception e) { errors.Add(e.Message); }
            }
            if (errors.Count != 0) { preserveBackups = true; throw new IOException(string.Join(Environment.NewLine, errors)); }
            entries.Clear();
        }
        internal void ReleaseBackups()
        {
            // If rollback failed, preserve recovery copies for the user.
            if (preserveBackups) return;
            foreach (var entry in entries) if (entry.Backup != null && File.Exists(entry.Backup) && File.Exists(entry.Target) && Hash(entry.Target) == entry.Hash) File.Delete(entry.Backup);
        }
    }

    public sealed class ParallelFlacSession : IDisposable
    {
        private readonly ParallelConversionBatch batch;
        private readonly ParallelAlbumProgress album;
        private readonly CUEConfig config;
        private readonly SemaphoreSlim workers, spoolSlots;
        private readonly CancellationTokenSource cancellation;
        private readonly List<Task> tasks = new List<Task>();
        private readonly List<Process> processes = new List<Process>();
        private readonly string spoolDirectory;
        private Exception failure;
        private readonly string stageRoot, finalRoot;
        public ParallelFlacSession(ParallelConversionBatch batch, CUEConfig config, string input, long samples, string stageRoot = null, string finalRoot = null)
        {
            this.batch = batch; this.config = config; this.stageRoot = stageRoot; this.finalRoot = finalRoot;
            lock (batch.Sync) { album = batch.Album(input); album.Samples = samples; album.Stage = "Decoding / disc verification"; }
            workers = new SemaphoreSlim(batch.Workers); spoolSlots = new SemaphoreSlim(batch.Workers + 1);
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(batch.Cancellation.Token);
            spoolDirectory = Path.Combine(batch.TempDirectory, "cuetools-pcm-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(spoolDirectory);
            batch.ActiveSession = this;
        }
        public void Checkpoint()
        {
            batch.Checkpoint();
            if (failure != null) throw new IOException("Parallel FLAC encoding failed: " + failure.Message, failure);
            cancellation.Token.ThrowIfCancellationRequested();
        }
        public void CancelCurrent()
        {
            try { cancellation.Cancel(); } catch (ObjectDisposedException) { return; }
            lock (processes) foreach (var process in processes.ToArray()) ParallelConversionBatch.TryKill(process);
            if (batch.Paused) batch.TogglePause();
        }
        public IAudioDest CreateDest(string path, AudioPCMConfig pcm, long count, int padding)
        {
            Checkpoint();
            while (!spoolSlots.Wait(100)) Checkpoint();
            try
            {
                var settings = (ExternalSettings)config.formats["flac"].encoderLossless.Settings.Clone();
                settings.PCM = pcm; settings.Padding = padding;
                var track = new ParallelTrackProgress { Album = album.Input, Output = path, Samples = count, Stage = "Decoding", Percent = 0 };
                track.DisplayOutput = stageRoot == null ? path : Path.Combine(finalRoot, Path.GetRelativePath(stageRoot, path));
                lock (batch.Sync) album.Tracks.Add(track);
                return new SpoolWriter(this, settings, track, Path.Combine(spoolDirectory, Guid.NewGuid().ToString("N") + ".wav"));
            }
            catch { spoolSlots.Release(); throw; }
        }
        private void Queue(ExternalSettings settings, ParallelTrackProgress track, string wav, byte[] expectedHash)
        {
            batch.Track(track, "Queued");
            tasks.Add(Task.Run(async () =>
            {
                bool acquired = false;
                try
                {
                    await workers.WaitAsync(cancellation.Token).ConfigureAwait(false); acquired = true; Checkpoint();
                    await Encode(settings, track, wav).ConfigureAwait(false);
                    Checkpoint(); batch.Track(track, "Checking PCM");
                    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var decoderSettings = config.formats["flac"].decoder?.Settings;
                    if (decoderSettings is Codecs.CommandLine.DecoderSettings)
                        decoderSettings = config.advanced.decoders.FirstOrDefault(d => d.Extension == "flac" && !(d is Codecs.CommandLine.DecoderSettings));
                    if (decoderSettings == null) throw new IOException("An internal FLAC decoder is required for output PCM verification.");
                    var decoder = (IAudioSource)Activator.CreateInstance(decoderSettings.DecoderType, decoderSettings.Clone(), track.Output, null);
                    long count = 0;
                    try
                    {
                        if (decoder.PCM.BitsPerSample != settings.PCM.BitsPerSample || decoder.PCM.ChannelCount != settings.PCM.ChannelCount || decoder.PCM.SampleRate != settings.PCM.SampleRate)
                            throw new IOException("Encoded FLAC PCM format differs from its input.");
                        var buffer = new AudioBuffer(decoder, 65536);
                        int read;
                        while ((read = decoder.Read(buffer, -1)) != 0)
                        {
                            Checkpoint(); hash.AppendData(buffer.Bytes, 0, read * decoder.PCM.BlockAlign); count += read;
                            batch.Track(track, "Checking PCM", (double)count / Math.Max(1, track.Samples));
                        }
                    }
                    finally { decoder.Close(); }
                    if (count != track.Samples || !expectedHash.SequenceEqual(hash.GetHashAndReset())) throw new IOException("Decoded output PCM does not match the intended track.");
                    lock (batch.Sync) { track.Complete = true; track.Stage = "Checked; waiting for tags"; track.Percent = 1; }
                }
                catch (Exception error)
                {
                    if (!(error is OperationCanceledException) && !(error is StopException)) Interlocked.CompareExchange(ref failure, error, null);
                    batch.Track(track, cancellation.IsCancellationRequested ? "Stopped" : "Failed", 0, error.Message);
                    CancelCurrent();
                }
                finally
                {
                    try { File.Delete(wav); } catch { }
                    if (acquired) workers.Release(); spoolSlots.Release();
                }
            }));
        }
        private async Task Encode(ExternalSettings settings, ParallelTrackProgress track, string wav)
        {
            bool fromFile = settings.Parameters.Contains("%I"), toFile = settings.Parameters.Contains("%O");
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo(settings.Path, settings.Parameters.Replace("%O", Quote(track.Output)).Replace("%I", Quote(wav)).Replace("%M", settings.EncoderMode).Replace("%P", settings.Padding.ToString()))
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = !fromFile, RedirectStandardOutput = !toFile, RedirectStandardError = true };
            var errors = new StringBuilder();
            batch.Track(track, "Encoding");
            Task input = Task.CompletedTask, output = Task.CompletedTask, stderr = Task.CompletedTask;
            process.Start();
            lock (processes) processes.Add(process);
            try
            {
                batch.Register(process);
                using var registration = cancellation.Token.Register(() => ParallelConversionBatch.TryKill(process));
                stderr = Task.Run(async () =>
                {
                    char[] buffer = new char[2048]; int read;
                    while ((read = await process.StandardError.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false)) != 0)
                    {
                        string text = new string(buffer, 0, read);
                        lock (errors) { errors.Append(text); if (errors.Length > 8192) errors.Remove(0, errors.Length - 8192); }
                        foreach (Match match in Regex.Matches(text, @"(\d{1,3})(?:\.\d+)?%"))
                            if (int.TryParse(match.Groups[1].Value, out int percent) && percent <= 100) batch.Track(track, "Encoding", percent / 100.0);
                    }
                });

                if (!fromFile) input = Task.Run(async () =>
                {
                    try { using var file = File.OpenRead(wav); await file.CopyToAsync(process.StandardInput.BaseStream, cancellation.Token).ConfigureAwait(false); }
                    finally { process.StandardInput.Close(); }
                });
                if (!toFile) output = Task.Run(async () =>
                {
                    using var file = new FileStream(track.Output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    await process.StandardOutput.BaseStream.CopyToAsync(file, cancellation.Token).ConfigureAwait(false);
                });
                await process.WaitForExitAsync(cancellation.Token).ConfigureAwait(false);
                await Task.WhenAll(input, output, stderr).ConfigureAwait(false);
                if (process.ExitCode != 0) throw new IOException("flac.exe exit code " + process.ExitCode + ": " + errors.ToString());
            }
            finally
            {
                ParallelConversionBatch.TryKill(process);
                try { if (!process.HasExited) process.WaitForExit(5000); } catch { }
                try { await Task.WhenAll(input, output, stderr).ConfigureAwait(false); } catch { }
                batch.Unregister(process); lock (processes) processes.Remove(process);
            }
        }
        private static string Quote(string path) => "\"" + path + "\"";
        public void Complete()
        {
            lock (batch.Sync) album.DecodePercent = 1;
            batch.Phase(album, "Waiting for encoders / PCM checks");
            while (!Task.WaitAll(tasks.ToArray(), 100)) Checkpoint();
            Checkpoint(); batch.Phase(album, "Writing tags and verification report");
        }
        public void MarkCommitted()
        { lock (batch.Sync) { album.Complete = true; album.Stage = "Complete"; foreach (var track in album.Tracks) track.Stage = "Complete / tagged"; } }
        public void MarkFailed(Exception error)
        { lock (batch.Sync) { album.Error = error.Message; album.Stage = cancellation.IsCancellationRequested || error is StopException ? "Stopped" : "Failed"; } }
        public void Dispose()
        {
            CancelCurrent(); try { Task.WaitAll(tasks.ToArray()); } catch { }
            if (Directory.Exists(spoolDirectory)) Directory.Delete(spoolDirectory, true);
            lock (batch.Sync)
                if (!album.Complete)
                    foreach (var track in album.Tracks)
                        track.Stage = track.Complete ? "Checked; album not published" : "Stopped / not published";
            batch.ActiveSession = null; workers.Dispose(); spoolSlots.Dispose(); cancellation.Dispose();
        }
        private sealed class SpoolWriter : IAudioDest
        {
            private readonly ParallelFlacSession session;
            private readonly ExternalSettings settings;
            private readonly ParallelTrackProgress track;
            private readonly string wav;
            private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            private readonly Codecs.WAV.AudioEncoder writer;
            private bool closed;
            public IAudioEncoderSettings Settings => settings;
            public string Path => track.Output;
            public long FinalSampleCount { set { track.Samples = value; writer.FinalSampleCount = value; } }
            internal SpoolWriter(ParallelFlacSession session, ExternalSettings settings, ParallelTrackProgress track, string wav)
            {
                this.session = session; this.settings = settings; this.track = track; this.wav = wav;
                writer = new Codecs.WAV.AudioEncoder(new Codecs.WAV.EncoderSettings(settings.PCM), wav); writer.FinalSampleCount = track.Samples;
            }
            public void Write(AudioBuffer buffer)
            {
                session.Checkpoint(); writer.Write(buffer); hash.AppendData(buffer.Bytes, 0, buffer.Length * settings.PCM.BlockAlign);
                lock (session.batch.Sync) { track.Written += buffer.Length; track.Percent = (double)track.Written / Math.Max(1, track.Samples); }
            }
            public void Close()
            {
                if (closed) return; closed = true;
                try
                {
                    writer.Close();
                    if (track.Written != track.Samples) throw new IOException("Incomplete temporary PCM track.");
                    session.Queue(settings, track, wav, hash.GetHashAndReset());
                }
                finally { hash.Dispose(); }
            }
            public void Delete()
            {
                if (!closed) { closed = true; try { writer.Close(); } finally { hash.Dispose(); session.spoolSlots.Release(); } }
                if (File.Exists(wav)) File.Delete(wav);
            }
        }
    }
}
#endif
