using System.Text.Json;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Serialization;

public static class MaterialLibrarySerializer
{
    public static async Task<IReadOnlyList<MaterialProfile>> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<MaterialProfile>>(json, ProjectJsonSerializer.Options)
            ?? [];
    }

    public static async Task SaveAsync(IEnumerable<MaterialProfile> materials, string path, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var json = JsonSerializer.Serialize(materials, ProjectJsonSerializer.Options);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }
}
