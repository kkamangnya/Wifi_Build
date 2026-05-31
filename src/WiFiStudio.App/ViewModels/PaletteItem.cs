using WiFiStudio.Core.Models;

namespace WiFiStudio.App.ViewModels;

public sealed class PaletteItem
{
    public string Category { get; init; } = "";
    public string Label { get; init; } = "";
    public string ToolParameter { get; init; } = "";
    public PlanObjectType? ObjectType { get; init; }
}
