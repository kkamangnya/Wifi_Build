namespace WiFiStudio.Rendering.Layers;

public sealed class LayerVisibility
{
    public bool Grid { get; set; } = true;
    public bool Heatmap { get; set; } = true;
    public bool Walls { get; set; } = true;
    public bool AccessPoints { get; set; } = true;
}
