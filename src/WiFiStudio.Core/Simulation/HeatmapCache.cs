using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;

namespace WiFiStudio.Core.Simulation;

public sealed class HeatmapCache
{
    private const double TileSizeCm = 500;
    private readonly ConcurrentDictionary<string, HeatmapResult> _results = new();
    private readonly ConcurrentDictionary<string, HashSet<string>> _tileIndex = new();

    public bool TryGet(ProjectModel project, out HeatmapResult? result)
    {
        return _results.TryGetValue(CreateKey(project), out result);
    }

    public void Store(ProjectModel project, HeatmapResult result)
    {
        var key = CreateKey(project);
        _results[key] = result;
        _tileIndex[key] = result.Samples.Select(sample => TileKey(sample.X, sample.Y)).ToHashSet();
    }

    public void Clear() => _results.Clear();

    public void InvalidateRegion(PlanRect dirtyRegion)
    {
        var dirtyTiles = TilesForRegion(dirtyRegion).ToHashSet();
        foreach (var pair in _tileIndex)
        {
            if (!pair.Value.Overlaps(dirtyTiles))
            {
                continue;
            }

            _results.TryRemove(pair.Key, out _);
            _tileIndex.TryRemove(pair.Key, out _);
        }
    }

    private static string CreateKey(ProjectModel project)
    {
        var json = ProjectJsonSerializer.Serialize(project);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static IEnumerable<string> TilesForRegion(PlanRect region)
    {
        var left = (int)Math.Floor(region.X / TileSizeCm);
        var top = (int)Math.Floor(region.Y / TileSizeCm);
        var right = (int)Math.Floor((region.X + Math.Max(1, region.Width)) / TileSizeCm);
        var bottom = (int)Math.Floor((region.Y + Math.Max(1, region.Height)) / TileSizeCm);
        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                yield return $"{x}:{y}";
            }
        }
    }

    private static string TileKey(double x, double y)
    {
        return $"{(int)Math.Floor(x / TileSizeCm)}:{(int)Math.Floor(y / TileSizeCm)}";
    }
}
