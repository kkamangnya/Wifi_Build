namespace WiFiStudio.Core.Models;

public enum FrequencyBand
{
    Ghz24,
    Ghz5,
    Ghz6
}

public enum HeatmapType
{
    Rssi,
    Snr,
    Interference,
    Capacity,
    BestAp,
    DeadZone,
    UserQuality
}

public enum PlanElementKind
{
    Wall,
    Door,
    Window,
    Furniture,
    AccessPoint,
    VirtualUser
}

public enum PlanObjectType
{
    Desk,
    Chair,
    Sofa,
    Bed,
    Bookshelf,
    Cabinet,
    Partition,
    Tv,
    Refrigerator,
    WashingMachine,
    Microwave,
    ServerRack,
    MetalShelf,
    Plant,
    GlassDoor,
    WoodDoor,
    ConcreteColumn,
    ElevatorShaft,
    Stairs,
    ConferenceTable,
    Person,
    FixedUser,
    MobileUser,
    UserGroup,
    MeshNode,
    Router
}

public enum UserMobilityMode
{
    Fixed,
    Mobile
}

public enum LinkQuality
{
    Excellent,
    Good,
    Fair,
    Poor,
    DeadZone
}

public enum OptimizationMode
{
    Balanced,
    AreaCoverage,
    UserQuality,
    UserRouteQuality,
    MinimizeApCount,
    MinimizeDeadZones
}

public enum SelectedElementKind
{
    None,
    Wall,
    AccessPoint,
    Object,
    User
}
