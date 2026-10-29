using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Harbor;

internal static class AtomicFile
{
    internal static string ReadText(string path)
    {
        // Readers must allow atomic replacement while the watchdog samples state.
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file, Encoding.UTF8); return reader.ReadToEnd();
    }
    internal static void WriteText(string path, string text)
    {
        string temp = path + "." + Environment.ProcessId + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, text);
            for (int attempt = 0; ; attempt++)
            {
                try { if (File.Exists(path)) File.Replace(temp, path, null, true); else File.Move(temp, path); return; }
                catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < 3) { Thread.Sleep(10 * (attempt + 1)); }
            }
        }
        finally { try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
    }
    internal static bool TryWriteText(string path, string text)
    {
        try { WriteText(path, text); return true; } catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
    }
}
