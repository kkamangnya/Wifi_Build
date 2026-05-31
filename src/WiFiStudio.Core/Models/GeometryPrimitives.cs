namespace WiFiStudio.Core.Models;

public sealed record PlanPoint(double X, double Y)
{
    public static PlanPoint Zero { get; } = new(0, 0);
}

public sealed record PlanSize(double Width, double Height);

public sealed record PlanRect(double X, double Y, double Width, double Height)
{
    public PlanPoint Center => new(X + Width / 2.0, Y + Height / 2.0);
}
