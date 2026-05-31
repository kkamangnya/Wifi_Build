namespace WiFiStudio.Rendering.Canvas;

public sealed class PlanViewport
{
    public double PixelsPerCentimeter { get; set; } = 0.25;
    public double Zoom { get; set; } = 1.0;

    public double Scale => PixelsPerCentimeter * Zoom;
}
