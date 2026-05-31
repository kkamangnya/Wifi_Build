using System.Text.Json;
using System.Text.Json.Serialization;
using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Serialization;

public static class ProjectJsonSerializer
{
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    public static string Serialize(ProjectModel project) => JsonSerializer.Serialize(project, Options);

    public static ProjectModel Deserialize(string json)
    {
        var project = JsonSerializer.Deserialize<ProjectModel>(json, Options)
            ?? throw new InvalidOperationException("Project JSON could not be deserialized.");

        Normalize(project);
        return project;
    }

    public static async Task SaveAsync(ProjectModel project, string path, CancellationToken cancellationToken = default)
    {
        project.ModifiedAtUtc = DateTimeOffset.UtcNow;
        var json = Serialize(project);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    public static async Task<ProjectModel> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    public static void Normalize(ProjectModel project)
    {
        project.SchemaVersion = string.IsNullOrWhiteSpace(project.SchemaVersion) ? "2.0" : project.SchemaVersion;
        project.FloorPlan ??= new FloorPlan();
        project.HeatmapDisplay ??= new HeatmapDisplaySettings();
        project.LayerState ??= new LayerState();
        project.Floors ??= [];
        if (project.Floors.Count == 0)
        {
            project.Floors.Add(project.FloorPlan);
            project.ActiveFloorIndex = 0;
        }
        else
        {
            project.ActiveFloorIndex = Math.Clamp(project.ActiveFloorIndex, 0, project.Floors.Count - 1);
            project.FloorPlan = project.Floors[project.ActiveFloorIndex];
        }

        project.Materials ??= [];
        if (project.Materials.Count == 0)
        {
            project.Materials = [.. MaterialProfile.CreateDefaultLibrary()];
        }

        foreach (var material in project.Materials)
        {
            if (material.BaseAttenuationDb <= 0 && material.AttenuationDb > 0)
            {
                material.BaseAttenuationDb = material.AttenuationDb;
            }
        }

        if (project.FloorPlan.Users.Count == 0 && project.FloorPlan.VirtualUsers.Count > 0)
        {
            project.FloorPlan.Users.AddRange(project.FloorPlan.VirtualUsers.Select(user => new UserLocation
            {
                Id = user.Id,
                Name = user.Name,
                IsVisible = user.IsVisible,
                IsLocked = user.IsLocked,
                Movable = user.Movable,
                ZIndex = user.ZIndex,
                Type = user.Type,
                Position = user.Position,
                Weight = user.Weight,
                MobilityMode = user.MobilityMode,
                Route = user.Route,
                AttenuationDb = user.AttenuationDb,
                BlocksSignal = user.BlocksSignal
            }));
        }
    }
}
