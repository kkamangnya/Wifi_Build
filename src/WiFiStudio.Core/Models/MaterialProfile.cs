namespace WiFiStudio.Core.Models;

public sealed class MaterialProfile
{
    public string Id { get; set; } = "drywall";
    public string Name { get; set; } = "Gypsum Board";
    public string Category { get; set; } = "Wall";
    public double BaseAttenuationDb { get; set; } = 3.0;
    public double AttenuationMultiplier2_4Ghz { get; set; } = 0.86;
    public double AttenuationMultiplier5Ghz { get; set; } = 1.0;
    public double AttenuationMultiplier6Ghz { get; set; } = 1.14;
    public string Color { get; set; } = "#D3C8B8";
    public string Description { get; set; } = "Standard interior wall material.";
    public bool IsConductive { get; set; }

    public double AttenuationDb
    {
        get => BaseAttenuationDb;
        set => BaseAttenuationDb = value;
    }

    public string ColorHex
    {
        get => Color;
        set => Color = value;
    }

    public double MultiplierFor(FrequencyBand band) => band switch
    {
        FrequencyBand.Ghz24 => AttenuationMultiplier2_4Ghz,
        FrequencyBand.Ghz6 => AttenuationMultiplier6Ghz,
        _ => AttenuationMultiplier5Ghz
    };

    public static IReadOnlyList<MaterialProfile> CreateDefaultLibrary() =>
    [
        new() { Id = "drywall", Name = "Gypsum Board", Category = "Wall", BaseAttenuationDb = 3.0, Color = "#D3C8B8", Description = "Lightweight interior gypsum wall." },
        new() { Id = "wood", Name = "Wood", Category = "Wall", BaseAttenuationDb = 4.0, Color = "#B8834B", Description = "Wood wall, door, and furniture material." },
        new() { Id = "glass", Name = "Glass", Category = "Wall", BaseAttenuationDb = 3.0, Color = "#8FD3FF", Description = "Glass wall or door." },
        new() { Id = "brick", Name = "Brick", Category = "Wall", BaseAttenuationDb = 8.0, Color = "#A54E3E", Description = "Brick wall with moderate RF loss." },
        new() { Id = "concrete", Name = "Concrete", Category = "Wall", BaseAttenuationDb = 12.0, Color = "#9EA3A8", Description = "Dense structural concrete.", AttenuationMultiplier2_4Ghz = 0.9, AttenuationMultiplier5Ghz = 1.08, AttenuationMultiplier6Ghz = 1.22 },
        new() { Id = "metal", Name = "Metal", Category = "Wall", BaseAttenuationDb = 20.0, Color = "#66717E", Description = "Conductive RF blocking material.", IsConductive = true, AttenuationMultiplier2_4Ghz = 0.95, AttenuationMultiplier5Ghz = 1.12, AttenuationMultiplier6Ghz = 1.28 },
        new() { Id = "acoustic-wall", Name = "Acoustic Wall", Category = "Wall", BaseAttenuationDb = 18.0, Color = "#7E6A8A", Description = "Dense acoustic wall assembly.", AttenuationMultiplier2_4Ghz = 0.92, AttenuationMultiplier5Ghz = 1.1, AttenuationMultiplier6Ghz = 1.25 },
        new() { Id = "insulated-wall", Name = "Insulated Wall", Category = "Wall", BaseAttenuationDb = 10.0, Color = "#C6D7B9", Description = "Insulated wall assembly.", AttenuationMultiplier2_4Ghz = 0.88, AttenuationMultiplier5Ghz = 1.05, AttenuationMultiplier6Ghz = 1.18 },
        new() { Id = "composite-wall", Name = "Composite Wall", Category = "Wall", BaseAttenuationDb = 14.0, Color = "#A2A08F", Description = "Mixed material wall assembly.", AttenuationMultiplier2_4Ghz = 0.9, AttenuationMultiplier5Ghz = 1.08, AttenuationMultiplier6Ghz = 1.22 },
        new() { Id = "fabric", Name = "Fabric", Category = "Furniture", BaseAttenuationDb = 3.0, Color = "#C48A7A", Description = "Fabric and soft seating." },
        new() { Id = "appliance", Name = "Appliance", Category = "Object", BaseAttenuationDb = 10.0, Color = "#D8DEE9", Description = "Large appliance with metal body.", IsConductive = true },
        new() { Id = "human-body", Name = "Human Body", Category = "User", BaseAttenuationDb = 2.0, Color = "#4CAF50", Description = "Human body attenuation." }
    ];
}
