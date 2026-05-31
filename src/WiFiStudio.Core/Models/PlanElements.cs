namespace WiFiStudio.Core.Models;

public abstract class PlanElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Element";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; }
    public bool Movable { get; set; } = true;
    public int ZIndex { get; set; }
}

public sealed class WallElement : PlanElement
{
    public WallElement()
    {
        Name = "Wall";
    }

    public PlanPoint Center { get; set; } = PlanPoint.Zero;
    public double LengthCm { get; set; } = 300;
    public double ThicknessCm { get; set; } = 12;
    public double RotationDegrees { get; set; }
    public string MaterialId { get; set; } = "drywall";
    public double? OverrideAttenuationDb { get; set; }
    public bool BlocksSignal { get; set; } = true;
}

public sealed class DoorElement : PlanElement
{
    public DoorElement()
    {
        Name = "Door";
    }

    public PlanPoint Center { get; set; } = PlanPoint.Zero;
    public double WidthCm { get; set; } = 90;
    public double RotationDegrees { get; set; }
    public string MaterialId { get; set; } = "wood";
}

public sealed class WindowElement : PlanElement
{
    public WindowElement()
    {
        Name = "Window";
    }

    public PlanPoint Center { get; set; } = PlanPoint.Zero;
    public double WidthCm { get; set; } = 140;
    public double RotationDegrees { get; set; }
    public string MaterialId { get; set; } = "glass";
}

public sealed class FurnitureElement : PlanElement
{
    public FurnitureElement()
    {
        Name = "Furniture";
    }

    public PlanPoint Center { get; set; } = PlanPoint.Zero;
    public PlanSize SizeCm { get; set; } = new(160, 80);
    public double RotationDegrees { get; set; }
    public string MaterialId { get; set; } = "wood";
    public double AttenuationDb { get; set; } = 1.5;
    public bool BlocksSignal { get; set; } = true;
}

public sealed class PlanObject : PlanElement
{
    public PlanObject()
    {
        Name = "Object";
    }

    public PlanObjectType Type { get; set; } = PlanObjectType.Desk;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 160;
    public double Height { get; set; } = 80;
    public double Rotation { get; set; }
    public string Material { get; set; } = "wood";
    public double AttenuationDb { get; set; } = 2.0;
    public bool BlocksSignal { get; set; } = true;

    public PlanPoint Center
    {
        get => new(X, Y);
        set
        {
            X = value.X;
            Y = value.Y;
        }
    }

    public PlanSize Size
    {
        get => new(Width, Height);
        set
        {
            Width = value.Width;
            Height = value.Height;
        }
    }

    public static PlanObject CreateDefault(PlanObjectType type, PlanPoint center)
    {
        var preset = PlanObjectPreset.For(type);
        return new PlanObject
        {
            Name = preset.Name,
            Type = type,
            X = center.X,
            Y = center.Y,
            Width = preset.WidthCm,
            Height = preset.HeightCm,
            Material = preset.MaterialId,
            AttenuationDb = preset.AttenuationDb,
            BlocksSignal = preset.BlocksSignal,
            Movable = true,
            ZIndex = preset.ZIndex
        };
    }
}

public sealed class AccessPoint : PlanElement
{
    public AccessPoint()
    {
        Name = "AP";
    }

    public PlanPoint Position { get; set; } = PlanPoint.Zero;
    public FrequencyBand Band { get; set; } = FrequencyBand.Ghz5;
    public double TxPowerDbm { get; set; } = 18;
    public int Channel { get; set; } = 36;
    public int BandwidthMhz { get; set; } = 40;
    public double AntennaGainDbi { get; set; } = 3;
    public double CoverageTargetDbm { get; set; } = -67;
    public bool IsEnabled { get; set; } = true;
}

public class UserLocation : PlanElement
{
    public UserLocation()
    {
        Name = "Person";
    }

    public PlanObjectType Type { get; set; } = PlanObjectType.Person;
    public PlanPoint Position { get; set; } = PlanPoint.Zero;
    public double Weight { get; set; } = 2.0;
    public UserMobilityMode MobilityMode { get; set; } = UserMobilityMode.Fixed;
    public List<PlanPoint> Route { get; set; } = [];
    public double AttenuationDb { get; set; } = 2.0;
    public bool BlocksSignal { get; set; }
}

public sealed class VirtualUser : UserLocation
{
}

public sealed record PlanObjectPreset(
    PlanObjectType Type,
    string Name,
    string Category,
    string MaterialId,
    double WidthCm,
    double HeightCm,
    double AttenuationDb,
    bool BlocksSignal,
    int ZIndex)
{
    public static IReadOnlyList<PlanObjectPreset> All { get; } =
    [
        new(PlanObjectType.Desk, "Desk", "Furniture", "wood", 140, 70, 2, true, 20),
        new(PlanObjectType.Chair, "Chair", "Furniture", "wood", 55, 55, 1, true, 20),
        new(PlanObjectType.Sofa, "Sofa", "Furniture", "fabric", 210, 90, 3, true, 20),
        new(PlanObjectType.Bed, "Bed", "Furniture", "fabric", 210, 160, 3, true, 20),
        new(PlanObjectType.Bookshelf, "Bookshelf", "Furniture", "wood", 120, 35, 4, true, 20),
        new(PlanObjectType.Cabinet, "Cabinet", "Furniture", "wood", 100, 45, 4, true, 20),
        new(PlanObjectType.Partition, "Partition", "Structure", "composite-wall", 180, 15, 5, true, 15),
        new(PlanObjectType.Tv, "TV", "Electronics", "metal", 120, 12, 4, true, 25),
        new(PlanObjectType.Refrigerator, "Refrigerator", "Electronics", "appliance", 85, 80, 10, true, 25),
        new(PlanObjectType.WashingMachine, "Washing Machine", "Electronics", "appliance", 70, 70, 9, true, 25),
        new(PlanObjectType.Microwave, "Microwave", "Electronics", "appliance", 55, 45, 6, true, 25),
        new(PlanObjectType.ServerRack, "Server Rack", "Electronics", "metal", 80, 110, 12, true, 25),
        new(PlanObjectType.MetalShelf, "Metal Shelf", "Electronics", "metal", 130, 45, 8, true, 25),
        new(PlanObjectType.Plant, "Plant", "Furniture", "wood", 60, 60, 1, false, 20),
        new(PlanObjectType.GlassDoor, "Glass Door", "Structure", "glass", 100, 12, 3, true, 15),
        new(PlanObjectType.WoodDoor, "Wood Door", "Structure", "wood", 90, 12, 4, true, 15),
        new(PlanObjectType.ConcreteColumn, "Concrete Column", "Structure", "concrete", 55, 55, 15, true, 15),
        new(PlanObjectType.ElevatorShaft, "Elevator Shaft", "Structure", "metal", 210, 210, 20, true, 15),
        new(PlanObjectType.Stairs, "Stairs", "Structure", "concrete", 260, 180, 8, true, 15),
        new(PlanObjectType.ConferenceTable, "Conference Table", "Furniture", "wood", 300, 120, 3, true, 20),
        new(PlanObjectType.Person, "Person", "User", "human-body", 45, 45, 2, false, 40),
        new(PlanObjectType.FixedUser, "Fixed User", "User", "human-body", 45, 45, 2, false, 40),
        new(PlanObjectType.MobileUser, "Mobile User", "User", "human-body", 45, 45, 2, false, 40),
        new(PlanObjectType.UserGroup, "User Group", "User", "human-body", 120, 90, 2, false, 40),
        new(PlanObjectType.MeshNode, "Mesh Node", "Network", "appliance", 40, 40, 1, false, 35),
        new(PlanObjectType.Router, "Router", "Network", "appliance", 45, 35, 1, false, 35)
    ];

    public static PlanObjectPreset For(PlanObjectType type)
    {
        return All.FirstOrDefault(p => p.Type == type) ?? All[0];
    }
}
