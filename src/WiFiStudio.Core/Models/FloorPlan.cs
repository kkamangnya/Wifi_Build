namespace WiFiStudio.Core.Models;

public sealed class FloorPlan
{
    public string Name { get; set; } = "Main Floor";
    public double WidthCm { get; set; } = 2500;
    public double HeightCm { get; set; } = 1600;
    public double GridSizeCm { get; set; } = 25;

    public List<WallElement> Walls { get; set; } = [];
    public List<DoorElement> Doors { get; set; } = [];
    public List<WindowElement> Windows { get; set; } = [];
    public List<FurnitureElement> Furniture { get; set; } = [];
    public List<PlanObject> Objects { get; set; } = [];
    public List<AccessPoint> AccessPoints { get; set; } = [];
    public List<UserLocation> Users { get; set; } = [];
    public List<VirtualUser> VirtualUsers { get; set; } = [];
}
