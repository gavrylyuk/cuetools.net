using System;
using System.IO;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace JDP
{
    // The modern application uses per-user pipes instead of .NET Remoting.
    internal static class SingletonController
    {
        public delegate bool ReceiveDelegate(string[] args);
        private static Mutex mutex;
        private static bool ownsMutex;
        private static CancellationTokenSource shutdown;
        private static Task listener;

        private static string PipeName(string id)
        {
            return id + "-" + WindowsIdentity.GetCurrent().User.Value;
        }

        public static bool IamFirst(string id, ReceiveDelegate receiver)
        {
            mutex = new Mutex(false, @"Local\" + PipeName(id));
            try { ownsMutex = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            if (!ownsMutex)
                return false;

            shutdown = new CancellationTokenSource();
            var cancellation = shutdown.Token;
            listener = Task.Run(async () =>
            {
                while (!cancellation.IsCancellationRequested)
                {
                    try
                    {
                        using (var pipe = new NamedPipeServerStream(PipeName(id), PipeDirection.InOut, 1,
                            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
                        {
                            await pipe.WaitForConnectionAsync(cancellation).ConfigureAwait(false);
                            using (var reader = new BinaryReader(pipe, Encoding.UTF8, true))
                            using (var writer = new BinaryWriter(pipe, Encoding.UTF8, true))
                            {
                                int count = reader.ReadInt32();
                                if (count < 0 || count > 1024)
                                    throw new InvalidDataException("Invalid argument count.");
                                var arguments = new string[count];
                                for (int i = 0; i < count; i++)
                                    arguments[i] = reader.ReadString();
                                writer.Write(receiver(arguments));
                                writer.Flush();
                            }
                        }
                    }
                    catch (OperationCanceledException) { break; }
                    catch (IOException) { }
                    catch (ObjectDisposedException) { break; }
                    catch (InvalidOperationException) when (cancellation.IsCancellationRequested) { break; }
                }
            }, cancellation);
        
            return true;
        }

        public static bool TrySend(string id, string[] arguments)
        {
            bool accepted = false;
            try
            {
                using (var pipe = new NamedPipeClientStream(".", PipeName(id), PipeDirection.InOut,
                    PipeOptions.CurrentUserOnly))
                {
                    pipe.Connect(3000);
                    // Bound both connect and acknowledgement waits if the first instance is unresponsive.
                    pipe.ReadMode = PipeTransmissionMode.Byte;
                    using (var writer = new BinaryWriter(pipe, Encoding.UTF8, true))
                    using (var reader = new BinaryReader(pipe, Encoding.UTF8, true))
                    {
                        writer.Write(arguments.Length);
                        foreach (string argument in arguments)
                            writer.Write(argument);
                        writer.Flush();
                        var reply = Task.Run(() => reader.ReadBoolean());
                        accepted = reply.Wait(3000) && reply.Result;
                    }
                }
            }
            catch (IOException) { }
            catch (TimeoutException) { }
            catch (AggregateException) { }

            return accepted;
        }

        public static void Send(string id, string[] arguments)
        {
            if (!TrySend(id, arguments))
                MessageBox.Show("Another instance of the application seems to be running, but not responding.",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public static void Cleanup()
        {
            shutdown?.Cancel();
            // Do not wait here: the receiver may be marshaling to the closing UI thread.
            if (ownsMutex)
                mutex.ReleaseMutex();
            mutex?.Dispose();
            mutex = null;
            ownsMutex = false;
        }
    }
}
