using System.Media;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PitWall.Host;

internal static class HostCallAudio
{
    public static void PlayAlert()
    {
        try { SystemSounds.Asterisk.Play(); }
        catch { /* Audio output may be unavailable. */ }
    }

    public static void Speak(string text)
    {
        object? voice = null;
        try
        {
            var speechType = Type.GetTypeFromProgID("SAPI.SpVoice", throwOnError: false);
            if (speechType is null)
                return;
            voice = Activator.CreateInstance(speechType);
            if (voice is null)
                return;
            speechType.InvokeMember("Speak", BindingFlags.InvokeMethod, null, voice, [text, 0]);
        }
        catch { /* SAPI may be disabled or have no voice installed. */ }
        finally
        {
            if (voice is not null && Marshal.IsComObject(voice))
                Marshal.ReleaseComObject(voice);
        }
    }
}
