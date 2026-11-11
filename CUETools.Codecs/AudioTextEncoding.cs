using System.Text;

namespace CUETools.Codecs
{
    public static class AudioTextEncoding
    {
#if NET10_0_OR_GREATER
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void RegisterCodePages()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }
#endif
        // Encoding.Default changed to UTF-8 on modern .NET. Code page 0 retains
        // the Windows system ANSI code page for legacy CUE sheets and metadata.
        public static Encoding Ansi
        {
            get
            {
#if NET10_0_OR_GREATER
                return Encoding.GetEncoding(0);
#else
                return Encoding.Default;
#endif
            }
        }
    }
}
