using System.Windows.Forms;
using CUETools.Codecs.CoreAudio;
using NAudio.CoreAudioApi;

namespace CUETools.Codecs.DirectSound
{
    // Compatibility entry point: modern playback uses the existing WASAPI implementation.
    public sealed class DirectSoundOut : WasapiOut
    {
        public DirectSoundOut(Control owner, AudioPCMConfig pcm, int delay)
            : base(GetDefaultAudioEndpoint(), AudioClientShareMode.Shared, true, delay, pcm) { }
    }
}
