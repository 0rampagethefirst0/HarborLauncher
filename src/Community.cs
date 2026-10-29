using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Harbor;

internal static class Community
{
    internal const string Origin = "https://harbor-mods.orangemole11.chatgpt.site";
    static readonly HttpClient client = new() { Timeout = TimeSpan.FromSeconds(20), MaxResponseContentBufferSize = 16_000_000 };
    internal static void Open(string path) => Process.Start(new ProcessStartInfo(Origin + path) { UseShellExecute = true });
    internal static async Task<int> Refresh(Mods mods)
    {
        var incoming = new List<ModDefinition>(); long bytes = 0;
        for (int page = 0; page < 1000; page++)
        {
            string json = await client.GetStringAsync(Origin + "/api/catalog?page=" + page);
            bytes += json.Length; if (bytes > 64_000_000) throw new InvalidDataException("Community catalog is too large.");
            var batch = JsonSerializer.Deserialize<List<ModDefinition>>(json, Mods.Json) ?? throw new InvalidDataException("Invalid community catalog.");
            incoming.AddRange(batch); if (incoming.Count > 10000) throw new InvalidDataException("The catalog exceeds 10,000 packages.");
            if (batch.Count < 12) break;
        }
        mods.ReplaceCommunity(incoming); return incoming.Count;
    }
}
