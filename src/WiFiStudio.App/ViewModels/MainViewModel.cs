using System.Collections.ObjectModel;
using WiFiStudio.App.Services;
using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;
using WiFiStudio.Core.Optimization;
using WiFiStudio.Core.Serialization;
using WiFiStudio.Core.Simulation;
using WiFiStudio.Rendering.Heatmaps;

namespace WiFiStudio.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IProjectFileService _fileService;
    private readonly RfSimulationEngine _simulationEngine = new();
    private readonly HeatmapCache _heatmapCache = new();
    private readonly ApPlacementOptimizer _optimizer = new();
    private readonly UserSignalAnalyzer _userAnalyzer = new();
    private readonly UserRouteSimulationEngine _routeAnalyzer = new();
    private readonly ExperimentRunner _experimentRunner = new();
    private readonly ProjectHistory _history = new();
    private CancellationTokenSource? _operationCts;
    private ProjectModel _project;
    private HeatmapResult? _heatmapResult;
    private OptimizationResult? _optimizationResult;
    private ExperimentRunResult? _lastExperiment;
    private AccessPointRecommendation? _pendingRecommendation;
    private CanvasTool _activeTool = CanvasTool.Select;
    private PlanObjectType _activeObjectType = PlanObjectType.Desk;
    private MaterialProfile? _selectedMaterial;
    private string? _currentProjectPath;
    private string _selectedFrequency = "5 GHz";
    private string _selectedHeatmapMode = "RSSI Heatmap";
    private string _simulationState = "Idle";
    private string _statusText = "Ready";
    private string _pointerText = "X 0 cm, Y 0 cm";
    private double _zoom = 1.0;
    private int _selectedCount;
    private SelectedElementKind _selectedKind;
    private string? _selectedId;
    private string _selectedElementName = "None";
    private string _selectedElementDetails = "Select an object";
    private string _userSignalText = "No user selected";
    private string _optimizationSummaryText = "No optimization result";
    private string _experimentSummaryText = "Run Experiment to compare the five structure and material conditions.";
    private string _experimentComparisonText = "Condition 5 Before/After comparison will appear after Run Experiment.";
    private string _experimentExportDirectoryText = "No experiment export yet.";
    private bool _snapEnabled = true;
    private bool _hadHeatmapBeforeManipulation;
    private string? _selectedRecentProject;
    private DateTimeOffset _lastAutosaveUtc = DateTimeOffset.MinValue;

    public MainViewModel(IProjectFileService fileService)
    {
        _fileService = fileService;
        _project = ProjectFactory.CreateSampleOffice();
        ProjectJsonSerializer.Normalize(_project);
        MaterialOptions = new ObservableCollection<MaterialProfile>(_project.Materials);
        PaletteItems = new ObservableCollection<PaletteItem>(CreatePaletteItems());
        RecentProjects = new ObservableCollection<string>(LoadRecentProjects());
        _selectedMaterial = MaterialOptions.FirstOrDefault(m => m.Id == "drywall") ?? MaterialOptions.FirstOrDefault();

        SetToolCommand = new RelayCommand(parameter => SetTool(parameter?.ToString()));
        NewProjectCommand = new RelayCommand(_ => LoadProject(ProjectFactory.CreateNewProject(), null, "Created a new project."));
        SampleProjectCommand = new RelayCommand(_ => LoadProject(ProjectFactory.CreateSampleOffice(), null, "Loaded the sample office template."));
        RecoverAutosaveCommand = new AsyncRelayCommand(_ => RecoverAutosaveAsync());
        OpenRecentProjectCommand = new AsyncRelayCommand(_ => OpenRecentProjectAsync(), _ => !string.IsNullOrWhiteSpace(SelectedRecentProject));
        SaveProjectCommand = new AsyncRelayCommand(_ => SaveAsync());
        OpenProjectCommand = new AsyncRelayCommand(_ => OpenAsync());
        RunSimulationCommand = new AsyncRelayCommand(_ => RunSimulationAsync());
        RunExperimentCommand = new AsyncRelayCommand(_ => RunExperimentAsync());
        CancelSimulationCommand = new RelayCommand(_ => CancelActiveOperation());
        RecommendApCommand = new AsyncRelayCommand(_ => RecommendApAsync());
        ExportCsvCommand = new AsyncRelayCommand(_ => ExportCsvAsync(), _ => HeatmapResult is not null);
        ExportSvgCommand = new AsyncRelayCommand(_ => ExportSvgAsync());
        ExportPngCommand = new AsyncRelayCommand(_ => ExportPngAsync(), _ => HeatmapResult is not null);
        ExportPdfCommand = new AsyncRelayCommand(_ => ExportPdfAsync());
        ExportExperimentCsvCommand = new AsyncRelayCommand(_ => ExportExperimentCsvAsync(), _ => LastExperiment is not null);
        ExportReportImageCommand = new AsyncRelayCommand(_ => ExportReportImagesAsync(), _ => LastExperiment is not null);
        ImportMaterialsCommand = new AsyncRelayCommand(_ => ImportMaterialsAsync());
        ExportMaterialsCommand = new AsyncRelayCommand(_ => ExportMaterialsAsync());
        RunWizardCommand = new RelayCommand(_ => RunBeginnerWizard());
        AcceptRecommendationCommand = new RelayCommand(_ => AcceptRecommendation(), _ => PendingRecommendation is not null);
        DeleteSelectedCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedKind != SelectedElementKind.None);
        UndoCommand = new RelayCommand(_ => Undo(), _ => _history.UndoCount > 0);
        RedoCommand = new RelayCommand(_ => Redo(), _ => _history.RedoCount > 0);
        AddRoutePointCommand = new RelayCommand(_ => AddRoutePointAtSelection(), _ => SelectedUser is not null);

        Log("WiFi Studio Pro started.");
    }

    public event EventHandler? CanvasInvalidated;

    public ProjectModel Project
    {
        get => _project;
        private set
        {
            if (SetProperty(ref _project, value))
            {
                OnProjectMetricsChanged();
            }
        }
    }

    public HeatmapResult? HeatmapResult
    {
        get => _heatmapResult;
        private set
        {
            if (SetProperty(ref _heatmapResult, value))
            {
                ExportCsvCommand.RaiseCanExecuteChanged();
                ExportPngCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public OptimizationResult? OptimizationResult
    {
        get => _optimizationResult;
        private set
        {
            if (SetProperty(ref _optimizationResult, value))
            {
                OnPropertyChanged(nameof(PendingRecommendationText));
            }
        }
    }

    public ExperimentRunResult? LastExperiment
    {
        get => _lastExperiment;
        private set
        {
            if (SetProperty(ref _lastExperiment, value))
            {
                ExportExperimentCsvCommand.RaiseCanExecuteChanged();
                ExportReportImageCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public AccessPointRecommendation? PendingRecommendation
    {
        get => _pendingRecommendation;
        private set
        {
            if (SetProperty(ref _pendingRecommendation, value))
            {
                OnPropertyChanged(nameof(PendingRecommendationText));
                AcceptRecommendationCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<MaterialProfile> MaterialOptions { get; }
    public ObservableCollection<PaletteItem> PaletteItems { get; }
    public ObservableCollection<string> RecentProjects { get; }
    public IReadOnlyList<string> FrequencyOptions { get; } = ["2.4 GHz", "5 GHz", "6 GHz"];
    public IReadOnlyList<string> HeatmapModeOptions { get; } =
    [
        "RSSI Heatmap",
        "SNR Heatmap",
        "Interference Heatmap",
        "Best AP Map",
        "Dead Zone Map",
        "User Quality Map"
    ];

    public MaterialProfile? SelectedMaterial
    {
        get => _selectedMaterial;
        set
        {
            if (!SetProperty(ref _selectedMaterial, value) || value is null)
            {
                return;
            }

            if (SelectedWall is null && SelectedObject is null)
            {
                return;
            }

            CaptureForEdit();
            if (SelectedWall is not null)
            {
                SelectedWall.MaterialId = value.Id;
                SelectedWall.OverrideAttenuationDb = value.BaseAttenuationDb;
            }
            else if (SelectedObject is not null)
            {
                SelectedObject.Material = value.Id;
                SelectedObject.AttenuationDb = value.BaseAttenuationDb;
            }

            CommitEdit("Changed material.", autoRecalculate: true);
        }
    }

    public string? SelectedRecentProject
    {
        get => _selectedRecentProject;
        set
        {
            if (SetProperty(ref _selectedRecentProject, value))
            {
                OpenRecentProjectCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedFrequency
    {
        get => _selectedFrequency;
        set
        {
            if (SetProperty(ref _selectedFrequency, value))
            {
                Project.SimulationSettings.FrequencyBand = ParseFrequency(value);
                if (SelectedAccessPoint is not null)
                {
                    CaptureForEdit();
                    SelectedAccessPoint.Band = Project.SimulationSettings.FrequencyBand;
                    CommitEdit("Changed AP frequency.", autoRecalculate: true);
                }
                else
                {
                    ClearAnalysis();
                    TouchProject();
                }
            }
        }
    }

    public string SelectedHeatmapMode
    {
        get => _selectedHeatmapMode;
        set
        {
            if (SetProperty(ref _selectedHeatmapMode, value))
            {
                Project.HeatmapDisplay.Mode = ParseHeatmapMode(value);
                Project.SimulationSettings.HeatmapType = Project.HeatmapDisplay.Mode;
                InvalidateCanvas();
            }
        }
    }

    public CanvasTool ActiveTool
    {
        get => _activeTool;
        private set
        {
            if (SetProperty(ref _activeTool, value))
            {
                OnPropertyChanged(nameof(ActiveToolText));
                StatusText = $"Tool: {ActiveToolText}";
            }
        }
    }

    public PlanObjectType ActiveObjectType
    {
        get => _activeObjectType;
        private set => SetProperty(ref _activeObjectType, value);
    }

    public string ActiveToolText => ActiveTool switch
    {
        CanvasTool.Wall => "Wall",
        CanvasTool.AccessPoint => "AP",
        CanvasTool.Object => PlanObjectPreset.For(ActiveObjectType).Name,
        CanvasTool.User => PlanObjectPreset.For(ActiveObjectType).Name,
        CanvasTool.RoutePoint => "Route Point",
        _ => "Select"
    };

    public bool SnapEnabled
    {
        get => _snapEnabled;
        set => SetProperty(ref _snapEnabled, value);
    }

    public bool StructuresVisible
    {
        get => Project.LayerState.StructuresVisible;
        set => SetLayerState(Project.LayerState.StructuresVisible, value, v => Project.LayerState.StructuresVisible = v, "Structures");
    }

    public bool ObjectsVisible
    {
        get => Project.LayerState.ObjectsVisible;
        set => SetLayerState(Project.LayerState.ObjectsVisible, value, v => Project.LayerState.ObjectsVisible = v, "Objects");
    }

    public bool AccessPointsVisible
    {
        get => Project.LayerState.AccessPointsVisible;
        set => SetLayerState(Project.LayerState.AccessPointsVisible, value, v => Project.LayerState.AccessPointsVisible = v, "APs");
    }

    public bool UsersVisible
    {
        get => Project.LayerState.UsersVisible;
        set => SetLayerState(Project.LayerState.UsersVisible, value, v => Project.LayerState.UsersVisible = v, "Users");
    }

    public bool HeatmapVisible
    {
        get => Project.LayerState.HeatmapVisible;
        set
        {
            if (Project.LayerState.HeatmapVisible == value)
            {
                return;
            }

            Project.LayerState.HeatmapVisible = value;
            Project.HeatmapDisplay.IsVisible = value;
            Project.ModifiedAtUtc = DateTimeOffset.UtcNow;
            OnPropertyChanged(nameof(HeatmapVisible));
            InvalidateCanvas();
            _ = AutoSaveAsync();
        }
    }

    public string SimulationState
    {
        get => _simulationState;
        private set => SetProperty(ref _simulationState, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string PointerText
    {
        get => _pointerText;
        private set => SetProperty(ref _pointerText, value);
    }

    public double Zoom
    {
        get => _zoom;
        private set
        {
            if (SetProperty(ref _zoom, Math.Clamp(value, 0.4, 3.0)))
            {
                OnPropertyChanged(nameof(ZoomText));
                InvalidateCanvas();
            }
        }
    }

    public string ZoomText => $"{Zoom:P0}";

    public SelectedElementKind SelectedKind
    {
        get => _selectedKind;
        private set
        {
            if (SetProperty(ref _selectedKind, value))
            {
                OnPropertyChanged(nameof(HasSelection));
                DeleteSelectedCommand.RaiseCanExecuteChanged();
                AddRoutePointCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasSelection => SelectedKind != SelectedElementKind.None;

    public int SelectedCount
    {
        get => _selectedCount;
        private set
        {
            if (SetProperty(ref _selectedCount, value))
            {
                OnPropertyChanged(nameof(SelectedCountText));
            }
        }
    }

    public string SelectedElementName
    {
        get => _selectedElementName;
        set
        {
            if (!SetProperty(ref _selectedElementName, value))
            {
                return;
            }

            if (!HasSelection)
            {
                return;
            }

            CaptureForEdit();
            if (SelectedWall is not null) SelectedWall.Name = value;
            if (SelectedAccessPoint is not null) SelectedAccessPoint.Name = value;
            if (SelectedObject is not null) SelectedObject.Name = value;
            if (SelectedUser is not null) SelectedUser.Name = value;
            CommitEdit("Renamed selection.", autoRecalculate: false);
        }
    }

    public string SelectedElementDetails
    {
        get => _selectedElementDetails;
        private set => SetProperty(ref _selectedElementDetails, value);
    }

    public string UserSignalText
    {
        get => _userSignalText;
        private set => SetProperty(ref _userSignalText, value);
    }

    public string OptimizationSummaryText
    {
        get => _optimizationSummaryText;
        private set => SetProperty(ref _optimizationSummaryText, value);
    }

    public string ExperimentSummaryText
    {
        get => _experimentSummaryText;
        private set => SetProperty(ref _experimentSummaryText, value);
    }

    public string ExperimentComparisonText
    {
        get => _experimentComparisonText;
        private set => SetProperty(ref _experimentComparisonText, value);
    }

    public string ExperimentExportDirectoryText
    {
        get => _experimentExportDirectoryText;
        private set => SetProperty(ref _experimentExportDirectoryText, value);
    }

    public ObservableCollection<string> LogLines { get; } = [];
    public ObservableCollection<ExperimentResultRow> ExperimentRows { get; } = [];

    public RelayCommand SetToolCommand { get; }
    public RelayCommand NewProjectCommand { get; }
    public RelayCommand SampleProjectCommand { get; }
    public AsyncRelayCommand RecoverAutosaveCommand { get; }
    public AsyncRelayCommand OpenRecentProjectCommand { get; }
    public AsyncRelayCommand SaveProjectCommand { get; }
    public AsyncRelayCommand OpenProjectCommand { get; }
    public AsyncRelayCommand RunSimulationCommand { get; }
    public AsyncRelayCommand RunExperimentCommand { get; }
    public RelayCommand CancelSimulationCommand { get; }
    public AsyncRelayCommand RecommendApCommand { get; }
    public AsyncRelayCommand ExportCsvCommand { get; }
    public AsyncRelayCommand ExportSvgCommand { get; }
    public AsyncRelayCommand ExportPngCommand { get; }
    public AsyncRelayCommand ExportPdfCommand { get; }
    public AsyncRelayCommand ExportExperimentCsvCommand { get; }
    public AsyncRelayCommand ExportReportImageCommand { get; }
    public AsyncRelayCommand ImportMaterialsCommand { get; }
    public AsyncRelayCommand ExportMaterialsCommand { get; }
    public RelayCommand RunWizardCommand { get; }
    public RelayCommand AcceptRecommendationCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand AddRoutePointCommand { get; }

    public string ProjectName => Project.Name;
    public string FloorSizeText => $"{Project.FloorPlan.WidthCm / 100.0:F1} m x {Project.FloorPlan.HeightCm / 100.0:F1} m";
    public string WallCountText => $"{Project.FloorPlan.Walls.Count}";
    public string ObjectCountText => $"{Project.FloorPlan.Objects.Count}";
    public string UserCountText => $"{Project.FloorPlan.Users.Count}";
    public string ObjectUserCountText => $"{Project.FloorPlan.Objects.Count} / {Project.FloorPlan.Users.Count}";
    public string ApCountText => $"{Project.FloorPlan.AccessPoints.Count}";
    public string AverageRssiText => HeatmapResult is null ? "-" : $"{HeatmapResult.Stats.AverageRssiDbm:F1} dBm";
    public string MinimumRssiText => HeatmapResult is null ? "-" : $"{HeatmapResult.Stats.MinimumRssiDbm:F1} dBm";
    public string CoverageText => HeatmapResult is null ? "-" : $"{HeatmapResult.Stats.CoverageRatio:P1}";
    public string ShadowText => HeatmapResult is null ? "-" : $"{HeatmapResult.Stats.ShadowRatio:P1}";
    public string SampleCountText => HeatmapResult is null ? "-" : $"{HeatmapResult.Stats.SampleCount:N0}";
    public string SelectedCountText => $"Selected {SelectedCount}";
    public string PendingRecommendationText => PendingRecommendation is null
        ? OptimizationResult?.Recommendations.Count > 1
            ? $"Optimized {OptimizationResult.Recommendations.Count} APs / score {OptimizationResult.Score:F1}"
            : "No pending recommendation"
        : $"Best AP: ({PendingRecommendation.Position.X:F0}, {PendingRecommendation.Position.Y:F0}) cm / score {PendingRecommendation.Score:F1}";

    public WallElement? SelectedWall => SelectedKind == SelectedElementKind.Wall
        ? Project.FloorPlan.Walls.FirstOrDefault(w => w.Id == _selectedId)
        : null;

    public AccessPoint? SelectedAccessPoint => SelectedKind == SelectedElementKind.AccessPoint
        ? Project.FloorPlan.AccessPoints.FirstOrDefault(ap => ap.Id == _selectedId)
        : null;

    public PlanObject? SelectedObject => SelectedKind == SelectedElementKind.Object
        ? Project.FloorPlan.Objects.FirstOrDefault(o => o.Id == _selectedId)
        : null;

    public UserLocation? SelectedUser => SelectedKind == SelectedElementKind.User
        ? Project.FloorPlan.Users.FirstOrDefault(u => u.Id == _selectedId)
        : null;

    private PlanElement? SelectedElement => SelectedWall
        ?? (PlanElement?)SelectedAccessPoint
        ?? (PlanElement?)SelectedObject
        ?? SelectedUser;

    public bool SelectedLocked
    {
        get => SelectedElement?.IsLocked ?? false;
        set
        {
            if (SelectedElement is null || SelectedElement.IsLocked == value) return;
            CaptureForEdit();
            SelectedElement.IsLocked = value;
            CommitEdit(value ? "Locked selection." : "Unlocked selection.", autoRecalculate: false);
        }
    }

    public bool SelectedVisible
    {
        get => SelectedElement?.IsVisible ?? false;
        set
        {
            if (SelectedElement is null || SelectedElement.IsVisible == value) return;
            CaptureForEdit();
            SelectedElement.IsVisible = value;
            CommitEdit(value ? "Showed selection." : "Hid selection.", autoRecalculate: true);
        }
    }

    public double SelectedX
    {
        get => SelectedCenter?.X ?? 0;
        set => SetSelectedCenter(new PlanPoint(value, SelectedY), "Changed X.", true);
    }

    public double SelectedY
    {
        get => SelectedCenter?.Y ?? 0;
        set => SetSelectedCenter(new PlanPoint(SelectedX, value), "Changed Y.", true);
    }

    public double SelectedWidth
    {
        get => SelectedObject?.Width ?? 0;
        set
        {
            if (SelectedObject is null) return;
            CaptureForEdit();
            SelectedObject.Width = Math.Max(10, value);
            CommitEdit("Changed width.", autoRecalculate: true);
        }
    }

    public double SelectedPrimarySize
    {
        get => SelectedWall?.LengthCm ?? SelectedObject?.Width ?? 0;
        set
        {
            CaptureForEdit();
            if (SelectedWall is not null) SelectedWall.LengthCm = Math.Max(20, value);
            if (SelectedObject is not null) SelectedObject.Width = Math.Max(10, value);
            CommitEdit("Changed primary size.", autoRecalculate: true);
        }
    }

    public double SelectedHeight
    {
        get => SelectedObject?.Height ?? SelectedWall?.ThicknessCm ?? 0;
        set
        {
            CaptureForEdit();
            if (SelectedObject is not null) SelectedObject.Height = Math.Max(10, value);
            if (SelectedWall is not null) SelectedWall.ThicknessCm = Math.Max(2, value);
            CommitEdit("Changed height/thickness.", autoRecalculate: true);
        }
    }

    public double SelectedRotation
    {
        get => SelectedObject?.Rotation ?? SelectedWall?.RotationDegrees ?? 0;
        set
        {
            CaptureForEdit();
            if (SelectedObject is not null) SelectedObject.Rotation = NormalizeAngle(value);
            if (SelectedWall is not null) SelectedWall.RotationDegrees = NormalizeAngle(value);
            CommitEdit("Changed rotation.", autoRecalculate: true);
        }
    }

    public double SelectedAttenuation
    {
        get => SelectedObject?.AttenuationDb ?? SelectedWall?.OverrideAttenuationDb ?? (SelectedWall is null ? 0 : Project.MaterialOrDefault(SelectedWall.MaterialId).BaseAttenuationDb);
        set
        {
            CaptureForEdit();
            if (SelectedObject is not null) SelectedObject.AttenuationDb = Math.Max(0, value);
            if (SelectedWall is not null) SelectedWall.OverrideAttenuationDb = Math.Max(0, value);
            CommitEdit("Changed attenuation.", autoRecalculate: true);
        }
    }

    public double WallLength
    {
        get => SelectedWall?.LengthCm ?? 0;
        set
        {
            if (SelectedWall is null) return;
            CaptureForEdit();
            SelectedWall.LengthCm = Math.Max(20, value);
            CommitEdit("Changed wall length.", autoRecalculate: true);
        }
    }

    public double ApTxPower
    {
        get => SelectedAccessPoint?.TxPowerDbm ?? 0;
        set
        {
            if (SelectedAccessPoint is null) return;
            CaptureForEdit();
            SelectedAccessPoint.TxPowerDbm = Math.Clamp(value, 1, 30);
            CommitEdit("Changed AP Tx power.", autoRecalculate: true);
        }
    }

    public double ApChannel
    {
        get => SelectedAccessPoint?.Channel ?? 0;
        set
        {
            if (SelectedAccessPoint is null) return;
            CaptureForEdit();
            SelectedAccessPoint.Channel = Math.Max(1, (int)Math.Round(value));
            CommitEdit("Changed AP channel.", autoRecalculate: true);
        }
    }

    public double ApBandwidth
    {
        get => SelectedAccessPoint?.BandwidthMhz ?? 0;
        set
        {
            if (SelectedAccessPoint is null) return;
            CaptureForEdit();
            SelectedAccessPoint.BandwidthMhz = Math.Max(20, (int)Math.Round(value / 20.0) * 20);
            CommitEdit("Changed AP bandwidth.", autoRecalculate: true);
        }
    }

    public double ApAntennaGain
    {
        get => SelectedAccessPoint?.AntennaGainDbi ?? 0;
        set
        {
            if (SelectedAccessPoint is null) return;
            CaptureForEdit();
            SelectedAccessPoint.AntennaGainDbi = Math.Clamp(value, 0, 12);
            CommitEdit("Changed AP antenna gain.", autoRecalculate: true);
        }
    }

    public double ApCoverageTarget
    {
        get => SelectedAccessPoint?.CoverageTargetDbm ?? 0;
        set
        {
            if (SelectedAccessPoint is null) return;
            CaptureForEdit();
            SelectedAccessPoint.CoverageTargetDbm = Math.Clamp(value, -90, -45);
            CommitEdit("Changed AP coverage target.", autoRecalculate: false);
        }
    }

    public bool ApEnabled
    {
        get => SelectedAccessPoint?.IsEnabled ?? false;
        set
        {
            if (SelectedAccessPoint is null || SelectedAccessPoint.IsEnabled == value) return;
            CaptureForEdit();
            SelectedAccessPoint.IsEnabled = value;
            CommitEdit(value ? "Enabled AP." : "Disabled AP.", autoRecalculate: true);
        }
    }

    public double UserWeight
    {
        get => SelectedUser?.Weight ?? 0;
        set
        {
            if (SelectedUser is null) return;
            CaptureForEdit();
            SelectedUser.Weight = Math.Clamp(value, 0.1, 10);
            CommitEdit("Changed user weight.", autoRecalculate: false);
        }
    }

    private PlanPoint? SelectedCenter
    {
        get
        {
            if (SelectedWall is not null) return SelectedWall.Center;
            if (SelectedAccessPoint is not null) return SelectedAccessPoint.Position;
            if (SelectedObject is not null) return SelectedObject.Center;
            if (SelectedUser is not null) return SelectedUser.Position;
            return null;
        }
    }

    public void AddWall(PlanPoint start, PlanPoint end)
    {
        start = SnapIfEnabled(start);
        end = SnapIfEnabled(end);
        var length = GeometryMath.DistanceCm(start, end);
        if (length < 50)
        {
            Log("Wall length is too short; nothing was added.");
            return;
        }

        CaptureForEdit();
        var material = SelectedMaterial ?? Project.MaterialOrDefault("drywall");
        var wall = new WallElement
        {
            Name = $"Wall {Project.FloorPlan.Walls.Count + 1}",
            Center = GeometryMath.Midpoint(start, end),
            LengthCm = length,
            ThicknessCm = 12,
            RotationDegrees = GeometryMath.RotationDegrees(start, end),
            MaterialId = material.Id,
            OverrideAttenuationDb = material.BaseAttenuationDb
        };
        Project.FloorPlan.Walls.Add(wall);
        Select(wall.Id, SelectedElementKind.Wall);
        CommitEdit($"Added a {material.Name} wall.", autoRecalculate: true);
    }

    public void AddAccessPoint(PlanPoint position, string? name = null)
    {
        position = SnapIfEnabled(position);
        if (!GeometryMath.PointInsideFloor(position, Project.FloorPlan) || GeometryMath.PointInsideAnyObstacle(position, Project))
        {
            Log("APs can only be placed inside the floor and outside obstacles.");
            return;
        }

        CaptureForEdit();
        var ap = new AccessPoint
        {
            Name = name ?? $"AP {Project.FloorPlan.AccessPoints.Count + 1}",
            Position = position,
            Band = Project.SimulationSettings.FrequencyBand,
            Channel = Project.SimulationSettings.FrequencyBand == FrequencyBand.Ghz24 ? 6 : 36,
            BandwidthMhz = Project.SimulationSettings.FrequencyBand == FrequencyBand.Ghz24 ? 20 : 40,
            TxPowerDbm = 18
        };
        Project.FloorPlan.AccessPoints.Add(ap);
        Select(ap.Id, SelectedElementKind.AccessPoint);
        CommitEdit($"Added {ap.Name}.", autoRecalculate: true);
    }

    public void AddObject(PlanObjectType type, PlanPoint position)
    {
        if (type is PlanObjectType.Person or PlanObjectType.FixedUser or PlanObjectType.MobileUser or PlanObjectType.UserGroup)
        {
            AddUser(type, position);
            return;
        }

        CaptureForEdit();
        var planObject = PlanObject.CreateDefault(type, SnapIfEnabled(position));
        planObject.Name = $"{PlanObjectPreset.For(type).Name} {Project.FloorPlan.Objects.Count + 1}";
        Project.FloorPlan.Objects.Add(planObject);
        Select(planObject.Id, SelectedElementKind.Object);
        CommitEdit($"Added {planObject.Name}.", autoRecalculate: true);
    }

    public void AddUser(PlanObjectType type, PlanPoint position)
    {
        CaptureForEdit();
        var user = new UserLocation
        {
            Name = $"{PlanObjectPreset.For(type).Name} {Project.FloorPlan.Users.Count + 1}",
            Type = type,
            Position = SnapIfEnabled(position),
            Weight = type == PlanObjectType.UserGroup ? 5 : 2,
            MobilityMode = type == PlanObjectType.MobileUser ? UserMobilityMode.Mobile : UserMobilityMode.Fixed,
            ZIndex = 40
        };
        Project.FloorPlan.Users.Add(user);
        Select(user.Id, SelectedElementKind.User);
        CommitEdit($"Added {user.Name}.", autoRecalculate: false);
    }

    public void SelectAt(PlanPoint position)
    {
        var user = Project.LayerState.UsersVisible ? Project.FloorPlan.Users
            .Where(u => u.IsVisible)
            .OrderBy(u => GeometryMath.DistanceCm(u.Position, position))
            .FirstOrDefault(u => GeometryMath.DistanceCm(u.Position, position) <= 70) : null;
        if (user is not null)
        {
            Select(user.Id, SelectedElementKind.User);
            return;
        }

        var ap = Project.LayerState.AccessPointsVisible ? Project.FloorPlan.AccessPoints
            .Where(a => a.IsVisible)
            .OrderBy(a => GeometryMath.DistanceCm(a.Position, position))
            .FirstOrDefault(a => GeometryMath.DistanceCm(a.Position, position) <= 80) : null;
        if (ap is not null)
        {
            Select(ap.Id, SelectedElementKind.AccessPoint);
            return;
        }

        var planObject = Project.LayerState.ObjectsVisible ? Project.FloorPlan.Objects
            .Where(o => o.IsVisible)
            .OrderByDescending(o => o.ZIndex)
            .FirstOrDefault(o => GeometryMath.PointInPolygon(position, GeometryMath.ObjectFootprint(o))) : null;
        if (planObject is not null)
        {
            Select(planObject.Id, SelectedElementKind.Object);
            return;
        }

        var wall = Project.LayerState.StructuresVisible ? Project.FloorPlan.Walls
            .Where(w => w.IsVisible)
            .OrderBy(w => DistanceToWallCenterline(position, w))
            .FirstOrDefault(w => DistanceToWallCenterline(position, w) <= Math.Max(30, w.ThicknessCm + 20)) : null;
        Select(wall?.Id, wall is null ? SelectedElementKind.None : SelectedElementKind.Wall);
    }

    public bool BeginManipulation(bool duplicate)
    {
        if (!HasSelection)
        {
            return false;
        }

        if (SelectedElement?.IsLocked == true)
        {
            Log("Selection is locked.");
            return false;
        }

        _hadHeatmapBeforeManipulation = HeatmapResult is not null;
        CaptureForEdit();
        if (duplicate)
        {
            DuplicateSelectedCore(new PlanPoint(40, 40));
        }

        return true;
    }

    public void MoveSelectedBy(PlanPoint delta, bool constrainAxis)
    {
        if (!HasSelection || delta == PlanPoint.Zero || SelectedElement?.IsLocked == true)
        {
            return;
        }

        if (constrainAxis)
        {
            delta = Math.Abs(delta.X) >= Math.Abs(delta.Y) ? new PlanPoint(delta.X, 0) : new PlanPoint(0, delta.Y);
        }

        var current = SelectedCenter;
        if (current is null)
        {
            return;
        }

        SetSelectedCenterCore(SnapIfEnabled(new PlanPoint(current.X + delta.X, current.Y + delta.Y)));
        UpdateSelectionText();
        UpdatePointerPosition(SelectedCenter ?? current);
        InvalidateCanvas();
    }

    public void ResizeSelectedBy(double deltaCm)
    {
        if (!HasSelection || SelectedElement?.IsLocked == true)
        {
            return;
        }

        if (SelectedObject is not null)
        {
            SelectedObject.Width = Math.Max(20, SelectedObject.Width + deltaCm);
            SelectedObject.Height = Math.Max(20, SelectedObject.Height + deltaCm);
        }
        else if (SelectedWall is not null)
        {
            SelectedWall.LengthCm = Math.Max(50, SelectedWall.LengthCm + deltaCm);
        }

        TouchProject();
        UpdateSelectionText();
    }

    public void ResizeWallEndpoint(bool moveStart, PlanPoint target)
    {
        var wall = SelectedWall;
        if (wall is null || wall.IsLocked)
        {
            return;
        }

        var endpoints = WallEndpoints(wall);
        var start = moveStart ? SnapIfEnabled(target) : endpoints.Start;
        var end = moveStart ? endpoints.End : SnapIfEnabled(target);
        var length = GeometryMath.DistanceCm(start, end);
        if (length < 50)
        {
            return;
        }

        wall.Center = GeometryMath.Midpoint(start, end);
        wall.LengthCm = length;
        wall.RotationDegrees = NormalizeAngle(GeometryMath.RotationDegrees(start, end));
        TouchProject();
        UpdateSelectionText();
    }

    public void RotateSelectedBy(double degrees)
    {
        if (!HasSelection || SelectedElement?.IsLocked == true)
        {
            return;
        }

        if (SelectedObject is not null) SelectedObject.Rotation = NormalizeAngle(SelectedObject.Rotation + degrees);
        if (SelectedWall is not null) SelectedWall.RotationDegrees = NormalizeAngle(SelectedWall.RotationDegrees + degrees);
        TouchProject();
        UpdateSelectionText();
    }

    public void EndManipulation()
    {
        if (SelectedAccessPoint is not null
            && (!GeometryMath.PointInsideFloor(SelectedAccessPoint.Position, Project.FloorPlan)
                || GeometryMath.PointInsideAnyObstacle(SelectedAccessPoint.Position, Project)))
        {
            StatusText = "Warning: AP is inside an obstacle or outside the floor.";
            Log("Warning: AP is inside an obstacle or outside the floor.");
        }

        CommitEdit("Moved selection.", autoRecalculate: _hadHeatmapBeforeManipulation);
        _hadHeatmapBeforeManipulation = false;
    }

    public void DeleteSelected()
    {
        if (!HasSelection)
        {
            return;
        }

        CaptureForEdit();
        switch (SelectedKind)
        {
            case SelectedElementKind.Wall:
                Project.FloorPlan.Walls.RemoveAll(w => w.Id == _selectedId);
                break;
            case SelectedElementKind.AccessPoint:
                Project.FloorPlan.AccessPoints.RemoveAll(a => a.Id == _selectedId);
                break;
            case SelectedElementKind.Object:
                Project.FloorPlan.Objects.RemoveAll(o => o.Id == _selectedId);
                break;
            case SelectedElementKind.User:
                Project.FloorPlan.Users.RemoveAll(u => u.Id == _selectedId);
                break;
        }

        Select(null, SelectedElementKind.None);
        CommitEdit("Deleted selection.", autoRecalculate: true);
    }

    public void UpdatePointerPosition(PlanPoint position)
    {
        PointerText = $"X {position.X:F0} cm, Y {position.Y:F0} cm";
    }

    public void ZoomBy(double delta)
    {
        Zoom += delta;
    }

    private async Task SaveAsync()
    {
        try
        {
            Project.SchemaVersion = "2.0";
            var savedPath = await _fileService.SaveAsync(Project, _currentProjectPath, CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(savedPath))
            {
                _currentProjectPath = savedPath;
                AddRecentProject(savedPath);
                Log($"Saved project: {Path.GetFileName(savedPath)}");
            }
        }
        catch (Exception ex)
        {
            Log($"Save failed: {ex.Message}");
        }
    }

    private async Task OpenAsync()
    {
        try
        {
            var opened = await _fileService.OpenAsync(CancellationToken.None);
            if (opened is not null)
            {
                ProjectJsonSerializer.Normalize(opened.Value.Project);
                LoadProject(opened.Value.Project, opened.Value.Path, $"Opened project: {Path.GetFileName(opened.Value.Path)}");
                AddRecentProject(opened.Value.Path);
            }
        }
        catch (Exception ex)
        {
            Log($"Open failed: {ex.Message}");
        }
    }

    private async Task OpenRecentProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedRecentProject))
        {
            return;
        }

        try
        {
            var project = await ProjectJsonSerializer.LoadAsync(SelectedRecentProject, CancellationToken.None);
            LoadProject(project, SelectedRecentProject, $"Opened recent project: {Path.GetFileName(SelectedRecentProject)}");
            AddRecentProject(SelectedRecentProject);
        }
        catch (Exception ex)
        {
            Log($"Open recent failed: {ex.Message}");
        }
    }

    private async Task RecoverAutosaveAsync()
    {
        var path = AutosavePath;
        if (!File.Exists(path))
        {
            Log("No autosave recovery file was found.");
            return;
        }

        try
        {
            var project = await ProjectJsonSerializer.LoadAsync(path, CancellationToken.None);
            LoadProject(project, null, "Recovered the autosave project.");
        }
        catch (Exception ex)
        {
            Log($"Autosave recovery failed: {ex.Message}");
        }
    }

    private async Task ExportCsvAsync()
    {
        if (HeatmapResult is null)
        {
            return;
        }

        try
        {
            var path = ExportPath("analysis", ".csv");
            await ProjectExportService.ExportCsvAsync(HeatmapResult, path, CancellationToken.None);
            Log($"Exported CSV: {path}");
        }
        catch (Exception ex)
        {
            Log($"CSV export failed: {ex.Message}");
        }
    }

    private async Task ExportSvgAsync()
    {
        try
        {
            var path = ExportPath("plan", ".svg");
            await ProjectExportService.ExportSvgAsync(Project, path, CancellationToken.None);
            Log($"Exported SVG: {path}");
        }
        catch (Exception ex)
        {
            Log($"SVG export failed: {ex.Message}");
        }
    }

    private async Task ExportPngAsync()
    {
        if (HeatmapResult is null)
        {
            return;
        }

        try
        {
            var path = ExportPath("heatmap", ".png");
            await HeatmapPngExporter.ExportAsync(HeatmapResult, Project.FloorPlan.WidthCm, Project.FloorPlan.HeightCm, path);
            Log($"Exported PNG: {path}");
        }
        catch (Exception ex)
        {
            Log($"PNG export failed: {ex.Message}");
        }
    }

    private async Task ExportPdfAsync()
    {
        try
        {
            var path = ExportPath("report", ".pdf");
            await ProjectExportService.ExportPdfReportAsync(Project, HeatmapResult, path, CancellationToken.None);
            Log($"Exported PDF: {path}");
        }
        catch (Exception ex)
        {
            Log($"PDF export failed: {ex.Message}");
        }
    }

    private async Task RunExperimentAsync()
    {
        CancelActiveOperation();
        _operationCts = new CancellationTokenSource();
        var token = _operationCts.Token;

        try
        {
            SimulationState = "Experiment 0/5";
            StatusText = "Running automated RF experiment";
            ExperimentRows.Clear();
            ExperimentSummaryText = "Experiment is running...";
            LastExperiment = null;
            var progress = new Progress<ExperimentProgress>(value =>
            {
                SimulationState = $"Experiment {value.ConditionIndex}/{value.ConditionCount}";
                StatusText = $"{value.ConditionName}: {value.Stage}";
            });

            var result = await _experimentRunner.RunAsync(Project, progress, token);
            foreach (var row in result.Rows)
            {
                ExperimentRows.Add(row);
            }

            LastExperiment = result;
            ExperimentSummaryText = result.Summary;
            ExperimentComparisonText = BuildExperimentComparisonText(result);
            var directory = await ExportExperimentArtifactsAsync(result, token);
            ExperimentExportDirectoryText = directory;
            SimulationState = "Experiment completed";
            StatusText = $"Experiment results saved to {directory}";
            Log($"Experiment completed: {result.Rows.Count} user measurements across five conditions.");
            Log($"Experiment CSV and heatmaps saved: {directory}");
        }
        catch (OperationCanceledException)
        {
            SimulationState = "Canceled";
            StatusText = "Experiment canceled";
            ExperimentSummaryText = "Experiment was canceled before all conditions completed.";
            ExperimentComparisonText = "Condition 5 comparison was not completed.";
            Log("Experiment canceled.");
        }
        catch (Exception ex)
        {
            SimulationState = "Failed";
            StatusText = "Experiment failed";
            ExperimentSummaryText = $"Experiment failed: {ex.Message}";
            ExperimentComparisonText = "Condition 5 comparison failed.";
            Log($"Experiment failed: {ex.Message}");
        }
    }

    private async Task ExportExperimentCsvAsync()
    {
        if (LastExperiment is null)
        {
            return;
        }

        try
        {
            var path = ExportPath("experiment-results", ".csv");
            await ExperimentCsvExporter.ExportAsync(LastExperiment, path, CancellationToken.None);
            StatusText = $"Experiment CSV saved to {path}";
            Log($"Exported experiment CSV: {path}");
        }
        catch (Exception ex)
        {
            Log($"Experiment CSV export failed: {ex.Message}");
        }
    }

    private async Task ExportReportImagesAsync()
    {
        if (LastExperiment is null)
        {
            Log("Run Experiment before exporting report images.");
            return;
        }

        try
        {
            var directory = await ExportReportImageArtifactsAsync(LastExperiment, CancellationToken.None);
            ExperimentExportDirectoryText = directory;
            StatusText = $"Report images saved to {directory}";
            Log($"Exported report images: {directory}");
        }
        catch (Exception ex)
        {
            Log($"Report image export failed: {ex.Message}");
        }
    }

    private static async Task<string> ExportExperimentArtifactsAsync(ExperimentRunResult result, CancellationToken cancellationToken)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var directory = Path.Combine(ExportDirectory, $"Experiment-{stamp}");
        Directory.CreateDirectory(directory);
        await ExperimentCsvExporter.ExportAsync(result, Path.Combine(directory, "experiment-results.csv"), cancellationToken);
        var imagePaths = new List<string>();

        foreach (var (conditionId, heatmap) in result.Heatmaps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rows = result.Rows.Where(row => row.ConditionId == conditionId).ToList();
            if (!result.DisplayProjects.TryGetValue(conditionId, out var project))
            {
                continue;
            }

            var imagePath = Path.Combine(directory, $"{SafeFileName(conditionId)}-annotated-heatmap.png");
            await ExperimentHeatmapPngExporter.ExportConditionAsync(
                project,
                heatmap,
                rows,
                imagePath,
                widthPx: 2400,
                cancellationToken: cancellationToken);
            imagePaths.Add(imagePath);
        }

        const string optimizedId = "condition-5-user-optimized";
        if (result.BaselineProjects.TryGetValue(optimizedId, out var beforeProject)
            && result.OptimizedProjects.TryGetValue(optimizedId, out var afterProject)
            && result.BaselineHeatmaps.TryGetValue(optimizedId, out var beforeHeatmap)
            && result.OptimizedHeatmaps.TryGetValue(optimizedId, out var afterHeatmap))
        {
            var rows = result.Rows.Where(row => row.ConditionId == optimizedId).ToList();
            var beforePath = Path.Combine(directory, "condition-5-before-annotated-heatmap.png");
            var afterPath = Path.Combine(directory, "condition-5-after-annotated-heatmap.png");
            var deltaPath = Path.Combine(directory, "condition-5-delta-improvement-heatmap.png");
            await ExperimentHeatmapPngExporter.ExportConditionAsync(beforeProject, beforeHeatmap, rows, beforePath, widthPx: 2400, cancellationToken: cancellationToken);
            await ExperimentHeatmapPngExporter.ExportConditionAsync(afterProject, afterHeatmap, rows, afterPath, widthPx: 2400, cancellationToken: cancellationToken);
            await ExperimentHeatmapPngExporter.ExportDifferenceAsync(beforeProject, beforeHeatmap, afterProject, afterHeatmap, rows, deltaPath, widthPx: 2400, cancellationToken: cancellationToken);
            imagePaths.Add(beforePath);
            imagePaths.Add(afterPath);
            imagePaths.Add(deltaPath);
        }

        await ExperimentTextReportExporter.ExportAsync(
            result,
            Path.Combine(directory, "experiment-summary.txt"),
            imagePaths,
            cancellationToken);

        return directory;
    }

    private static async Task<string> ExportReportImageArtifactsAsync(ExperimentRunResult result, CancellationToken cancellationToken)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var directory = Path.Combine(ExportDirectory, $"ExperimentReport-{stamp}");
        Directory.CreateDirectory(directory);
        var imagePaths = new List<string>();

        foreach (var (conditionId, heatmap) in result.Heatmaps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!result.DisplayProjects.TryGetValue(conditionId, out var project))
            {
                continue;
            }

            var rows = result.Rows.Where(row => row.ConditionId == conditionId).ToList();
            var path = Path.Combine(directory, $"report-{SafeFileName(conditionId)}.png");
            await ExperimentReportImageExporter.ExportConditionAsync(
                project,
                heatmap,
                rows,
                path,
                widthPx: 2400,
                cancellationToken: cancellationToken);
            imagePaths.Add(path);
        }

        const string optimizedId = "condition-5-user-optimized";
        if (result.BaselineProjects.TryGetValue(optimizedId, out var beforeProject)
            && result.OptimizedProjects.TryGetValue(optimizedId, out var afterProject)
            && result.BaselineHeatmaps.TryGetValue(optimizedId, out var beforeHeatmap)
            && result.OptimizedHeatmaps.TryGetValue(optimizedId, out var afterHeatmap))
        {
            var rows = result.Rows.Where(row => row.ConditionId == optimizedId).ToList();
            var beforePath = Path.Combine(directory, "report-condition-5-before.png");
            var afterPath = Path.Combine(directory, "report-condition-5-after.png");
            var deltaPath = Path.Combine(directory, "report-condition-5-delta.png");
            await ExperimentReportImageExporter.ExportConditionAsync(beforeProject, beforeHeatmap, rows, beforePath, widthPx: 2400, cancellationToken: cancellationToken);
            await ExperimentReportImageExporter.ExportConditionAsync(afterProject, afterHeatmap, rows, afterPath, widthPx: 2400, cancellationToken: cancellationToken);
            await ExperimentReportImageExporter.ExportDifferenceAsync(beforeProject, beforeHeatmap, afterProject, afterHeatmap, rows, deltaPath, widthPx: 2400, cancellationToken: cancellationToken);
            imagePaths.Add(beforePath);
            imagePaths.Add(afterPath);
            imagePaths.Add(deltaPath);
        }

        await ExperimentTextReportExporter.ExportAsync(
            result,
            Path.Combine(directory, "report-image-summary.txt"),
            imagePaths,
            cancellationToken);

        return directory;
    }

    private static string BuildExperimentComparisonText(ExperimentRunResult result)
    {
        var rows = result.Rows.Where(row => row.ConditionId == "condition-5-user-optimized").ToList();
        if (rows.Count == 0)
        {
            return "Condition 5 comparison has no user measurements.";
        }

        return string.Join(Environment.NewLine, rows.Select(row =>
            $"{row.UserName}: {row.BeforeOptimizationRssi:F1} -> {row.AfterOptimizationRssi:F1} dBm ({row.OptimizationDeltaDb:+0.0;-0.0;0.0} dB), {row.QualityDisplay}"));
    }

    private async Task ImportMaterialsAsync()
    {
        try
        {
            var path = MaterialLibraryPath;
            if (!File.Exists(path))
            {
                Log($"Material import file not found: {path}");
                return;
            }

            var imported = await MaterialLibrarySerializer.LoadAsync(path, CancellationToken.None);
            if (imported.Count == 0)
            {
                Log("Material import file contains no profiles.");
                return;
            }

            CaptureForEdit();
            Project.Materials = imported.ToList();
            MaterialOptions.Clear();
            foreach (var material in Project.Materials)
            {
                MaterialOptions.Add(material);
            }

            SelectedMaterial = MaterialOptions.FirstOrDefault();
            CommitEdit("Imported material library.", autoRecalculate: true);
        }
        catch (Exception ex)
        {
            Log($"Material import failed: {ex.Message}");
        }
    }

    private async Task ExportMaterialsAsync()
    {
        try
        {
            var path = MaterialLibraryPath;
            await MaterialLibrarySerializer.SaveAsync(Project.Materials, path, CancellationToken.None);
            Log($"Exported material library: {path}");
        }
        catch (Exception ex)
        {
            Log($"Material export failed: {ex.Message}");
        }
    }

    private async Task RunSimulationAsync()
    {
        CancelActiveOperation();
        _operationCts = new CancellationTokenSource();
        var token = _operationCts.Token;

        try
        {
            if (_heatmapCache.TryGet(Project, out var cached) && cached is not null)
            {
                HeatmapResult = cached;
                SimulationState = "Cached";
                UpdateStats();
                InvalidateCanvas();
                RecalculateSelectedAnalysis();
                Log("Used cached heatmap results.");
                return;
            }

            SimulationState = "Running 0%";
            StatusText = "Simulation running";
            var progress = new Progress<double>(value => SimulationState = $"Running {value:P0}");
            var result = await _simulationEngine.EvaluateAsync(Project, Project.SimulationSettings, progress, token);
            HeatmapResult = result;
            _heatmapCache.Store(Project, result);
            SimulationState = "Completed";
            StatusText = "Simulation completed";
            UpdateStats();
            RecalculateSelectedAnalysis();
            InvalidateCanvas();
            Log($"RSSI heatmap completed: {result.Stats.SampleCount:N0} samples, coverage {result.Stats.CoverageRatio:P1}.");
        }
        catch (OperationCanceledException)
        {
            SimulationState = "Canceled";
            StatusText = "Simulation canceled";
            Log("Simulation canceled.");
        }
        catch (Exception ex)
        {
            SimulationState = "Failed";
            StatusText = "Simulation failed";
            Log($"Simulation failed: {ex.Message}");
        }
    }

    private async Task RecommendApAsync()
    {
        CancelActiveOperation();
        _operationCts = new CancellationTokenSource();
        var token = _operationCts.Token;

        try
        {
            var apCount = Math.Max(1, Project.FloorPlan.AccessPoints.Count);
            SimulationState = apCount == 1 ? "Finding best AP" : $"Finding best {apCount}-AP layout";
            StatusText = apCount == 1
                ? "Finding the most efficient AP location"
                : $"Finding the most efficient spaced layout for {apCount} APs";
            var mode = Project.FloorPlan.Users.Count > 0 ? OptimizationMode.UserQuality : OptimizationMode.Balanced;
            var result = await _optimizer.RecommendLayoutAsync(Project, apCount, token, mode);
            OptimizationResult = result;
            Project.OptimizationResults.Insert(0, result);
            PendingRecommendation = result.Recommendations.FirstOrDefault();
            if (result.Recommendations.Count > 0)
            {
                ApplyRecommendedLayout(result);
            }

            SimulationState = result.Recommendations.Count <= 1 ? "Best AP applied" : $"Best {result.Recommendations.Count}-AP layout applied";
            StatusText = result.Recommendations.Count == 0
                ? "No valid AP location was found"
                : result.Recommendations.Count == 1
                    ? $"AP moved to best location: {result.Recommendations[0].Position.X:F0}, {result.Recommendations[0].Position.Y:F0} cm"
                    : $"{result.Recommendations.Count} APs moved to spaced optimal positions";
            UpdateOptimizationSummary();
            Log(result.Recommendations.Count == 0
                ? "No AP recommendation candidates were found."
                : $"Applied optimized AP layout: {PendingRecommendationText}");
        }
        catch (OperationCanceledException)
        {
            SimulationState = "Canceled";
            Log("AP recommendation canceled.");
        }
        catch (Exception ex)
        {
            SimulationState = "Failed";
            Log($"AP recommendation failed: {ex.Message}");
        }
    }

    private void AcceptRecommendation()
    {
        if (PendingRecommendation is null)
        {
            return;
        }

        var recommendation = PendingRecommendation;
        recommendation.Accepted = true;
        AddAccessPoint(recommendation.Position, "Recommended AP");
        if (SelectedAccessPoint is not null)
        {
            SelectedAccessPoint.TxPowerDbm = recommendation.RecommendedTxPowerDbm;
            SelectedAccessPoint.Channel = recommendation.RecommendedChannel;
            CommitEdit("Applied recommendation settings.", autoRecalculate: true);
        }

        PendingRecommendation = null;
    }

    private void ApplyRecommendedLayout(OptimizationResult result)
    {
        if (result.Recommendations.Count == 0)
        {
            return;
        }

        CaptureForEdit();
        var recommendations = result.Recommendations.ToList();
        var accessPoints = Project.FloorPlan.AccessPoints.ToList();
        while (accessPoints.Count < recommendations.Count)
        {
            var ap = new AccessPoint
            {
                Name = $"AP {accessPoints.Count + 1}",
                Band = Project.SimulationSettings.FrequencyBand
            };
            Project.FloorPlan.AccessPoints.Add(ap);
            accessPoints.Add(ap);
        }

        var assignments = AssignRecommendationsToAccessPoints(accessPoints, recommendations);
        foreach (var (ap, recommendation) in assignments)
        {
            ap.Position = recommendation.Position;
            ap.Band = Project.SimulationSettings.FrequencyBand;
            ap.TxPowerDbm = recommendation.RecommendedTxPowerDbm;
            ap.Channel = recommendation.RecommendedChannel;
            ap.BandwidthMhz = Project.SimulationSettings.FrequencyBand == FrequencyBand.Ghz24 ? 20 : Math.Max(40, ap.BandwidthMhz);
            ap.IsEnabled = true;
            recommendation.AssignedAccessPointId = ap.Id;
            recommendation.AssignedAccessPointName = ap.Name;
            recommendation.Accepted = true;
        }

        var firstAp = assignments.FirstOrDefault().Ap;
        if (firstAp is not null)
        {
            Select(firstAp.Id, SelectedElementKind.AccessPoint);
        }

        _heatmapCache.Clear();
        HeatmapResult = null;
        PendingRecommendation = recommendations.FirstOrDefault();
        TouchProject();
        UpdateStats();
        UpdateSelectionText();
        UpdateOptimizationSummary();
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        Log(assignments.Count == 1
            ? $"Moved {assignments[0].Ap.Name} to the best AP location."
            : $"Moved {assignments.Count} APs to spaced optimized locations.");
        _ = RunSimulationAsync();
    }

    private static List<(AccessPoint Ap, AccessPointRecommendation Recommendation)> AssignRecommendationsToAccessPoints(
        IReadOnlyList<AccessPoint> accessPoints,
        IReadOnlyList<AccessPointRecommendation> recommendations)
    {
        var remaining = recommendations.ToList();
        var assignments = new List<(AccessPoint Ap, AccessPointRecommendation Recommendation)>();
        foreach (var ap in accessPoints.Take(recommendations.Count))
        {
            var recommendation = remaining
                .OrderBy(r => GeometryMath.DistanceCm(ap.Position, r.Position))
                .First();
            assignments.Add((ap, recommendation));
            remaining.Remove(recommendation);
        }

        return assignments;
    }

    private void AddRoutePointAtSelection()
    {
        var user = SelectedUser;
        if (user is null)
        {
            return;
        }

        AddRoutePoint(new PlanPoint(Math.Min(Project.FloorPlan.WidthCm, user.Position.X + 300), user.Position.Y));
    }

    public void AddRoutePoint(PlanPoint point)
    {
        var user = SelectedUser;
        if (user is null)
        {
            Log("Select a user before adding route points.");
            return;
        }

        CaptureForEdit();
        user.MobilityMode = UserMobilityMode.Mobile;
        user.Route.Add(SnapIfEnabled(point));
        CommitEdit("Added route point.", autoRecalculate: false);
    }

    private void Undo()
    {
        var previous = _history.Undo(Project);
        if (previous is null)
        {
            return;
        }

        LoadProject(previous, _currentProjectPath, "Undo.");
    }

    private void Redo()
    {
        var next = _history.Redo(Project);
        if (next is null)
        {
            return;
        }

        LoadProject(next, _currentProjectPath, "Redo.");
    }

    private void CancelActiveOperation()
    {
        if (_operationCts is not null && !_operationCts.IsCancellationRequested)
        {
            _operationCts.Cancel();
        }
    }

    private void LoadProject(ProjectModel project, string? path, string logMessage)
    {
        ProjectJsonSerializer.Normalize(project);
        Project = project;
        _currentProjectPath = path;
        MaterialOptions.Clear();
        foreach (var material in project.Materials)
        {
            MaterialOptions.Add(material);
        }

        SelectedMaterial = MaterialOptions.FirstOrDefault(m => m.Id == "drywall") ?? MaterialOptions.FirstOrDefault();
        _selectedFrequency = FormatFrequency(project.SimulationSettings.FrequencyBand);
        _selectedHeatmapMode = FormatHeatmapMode(project.SimulationSettings.HeatmapType);
        OnPropertyChanged(nameof(SelectedFrequency));
        OnPropertyChanged(nameof(SelectedHeatmapMode));
        Select(null, SelectedElementKind.None);
        ExperimentRows.Clear();
        LastExperiment = null;
        ExperimentSummaryText = "Run Experiment to compare the five structure and material conditions.";
        ExperimentComparisonText = "Condition 5 Before/After comparison will appear after Run Experiment.";
        ExperimentExportDirectoryText = "No experiment export yet.";
        ClearAnalysis();
        Log(logMessage);
    }

    private void SetTool(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            ActiveTool = CanvasTool.Select;
            return;
        }

        if (toolName.StartsWith("Object:", StringComparison.OrdinalIgnoreCase)
            && Enum.TryParse(toolName["Object:".Length..], ignoreCase: true, out PlanObjectType objectType))
        {
            ActiveObjectType = objectType;
            ActiveTool = objectType is PlanObjectType.Person or PlanObjectType.FixedUser or PlanObjectType.MobileUser or PlanObjectType.UserGroup
                ? CanvasTool.User
                : CanvasTool.Object;
            return;
        }

        ActiveTool = Enum.TryParse<CanvasTool>(toolName, ignoreCase: true, out var tool) ? tool : CanvasTool.Select;
    }

    private void Select(string? id, SelectedElementKind kind)
    {
        _selectedId = id;
        SelectedKind = kind;
        SelectedCount = kind == SelectedElementKind.None ? 0 : 1;
        UpdateSelectionText();
        InvalidateCanvas();
    }

    private void SetSelectedCenter(PlanPoint center, string message, bool autoRecalculate)
    {
        if (!HasSelection)
        {
            return;
        }

        CaptureForEdit();
        SetSelectedCenterCore(SnapIfEnabled(center));
        CommitEdit(message, autoRecalculate);
    }

    private void SetSelectedCenterCore(PlanPoint center)
    {
        if (SelectedWall is not null) SelectedWall.Center = center;
        if (SelectedAccessPoint is not null) SelectedAccessPoint.Position = center;
        if (SelectedObject is not null) SelectedObject.Center = center;
        if (SelectedUser is not null) SelectedUser.Position = center;
    }

    private void CaptureForEdit()
    {
        _history.Capture(Project);
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
    }

    private void CommitEdit(string message, bool autoRecalculate)
    {
        TouchProject();
        UpdateSelectionText();
        ClearAnalysis();
        Log(message);
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        if (autoRecalculate)
        {
            _ = RunSimulationAsync();
        }
    }

    private void TouchProject()
    {
        Project.ModifiedAtUtc = DateTimeOffset.UtcNow;
        OnProjectMetricsChanged();
        InvalidateCanvas();
        _ = AutoSaveAsync();
    }

    private void ClearAnalysis()
    {
        _heatmapCache.Clear();
        HeatmapResult = null;
        PendingRecommendation = null;
        UpdateStats();
        RecalculateSelectedAnalysis();
        InvalidateCanvas();
    }

    private void UpdateStats()
    {
        OnPropertyChanged(nameof(AverageRssiText));
        OnPropertyChanged(nameof(MinimumRssiText));
        OnPropertyChanged(nameof(CoverageText));
        OnPropertyChanged(nameof(ShadowText));
        OnPropertyChanged(nameof(SampleCountText));
    }

    private void OnProjectMetricsChanged()
    {
        OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(FloorSizeText));
        OnPropertyChanged(nameof(WallCountText));
        OnPropertyChanged(nameof(ObjectCountText));
        OnPropertyChanged(nameof(UserCountText));
        OnPropertyChanged(nameof(ObjectUserCountText));
        OnPropertyChanged(nameof(ApCountText));
        OnPropertyChanged(nameof(StructuresVisible));
        OnPropertyChanged(nameof(ObjectsVisible));
        OnPropertyChanged(nameof(AccessPointsVisible));
        OnPropertyChanged(nameof(UsersVisible));
        OnPropertyChanged(nameof(HeatmapVisible));
    }

    private void SetLayerState(bool current, bool value, Action<bool> setter, string layerName)
    {
        if (current == value)
        {
            return;
        }

        setter(value);
        Project.ModifiedAtUtc = DateTimeOffset.UtcNow;
        OnProjectMetricsChanged();
        InvalidateCanvas();
        Log($"{layerName} layer {(value ? "shown" : "hidden")}.");
        _ = AutoSaveAsync();
    }

    private void UpdateSelectionText()
    {
        if (SelectedAccessPoint is not null)
        {
            var ap = SelectedAccessPoint;
            _selectedElementName = ap.Name;
            OnPropertyChanged(nameof(SelectedElementName));
            SelectedElementDetails = $"AP / {FormatFrequency(ap.Band)} / {ap.TxPowerDbm:F0} dBm / Ch {ap.Channel} / ({ap.Position.X:F0}, {ap.Position.Y:F0}) cm";
        }
        else if (SelectedWall is not null)
        {
            var wall = SelectedWall;
            var material = Project.MaterialOrDefault(wall.MaterialId);
            _selectedElementName = wall.Name;
            OnPropertyChanged(nameof(SelectedElementName));
            SelectedElementDetails = $"Wall / {material.Name} / {wall.LengthCm / 100.0:F1} m / {wall.RotationDegrees:F0} deg / {SelectedAttenuation:F1} dB";
        }
        else if (SelectedObject is not null)
        {
            var obj = SelectedObject;
            _selectedElementName = obj.Name;
            OnPropertyChanged(nameof(SelectedElementName));
            SelectedElementDetails = $"{PlanObjectPreset.For(obj.Type).Name} / {obj.Material} / {obj.Width:F0} x {obj.Height:F0} cm / {obj.AttenuationDb:F1} dB";
        }
        else if (SelectedUser is not null)
        {
            var user = SelectedUser;
            _selectedElementName = user.Name;
            OnPropertyChanged(nameof(SelectedElementName));
            SelectedElementDetails = $"{user.MobilityMode} user / weight {user.Weight:F1} / ({user.Position.X:F0}, {user.Position.Y:F0}) cm";
        }
        else
        {
            _selectedElementName = "None";
            OnPropertyChanged(nameof(SelectedElementName));
            SelectedElementDetails = "Select an object";
        }

        RecalculateSelectedAnalysis();
        OnPropertyChanged(nameof(SelectedX));
        OnPropertyChanged(nameof(SelectedY));
        OnPropertyChanged(nameof(SelectedWidth));
        OnPropertyChanged(nameof(SelectedPrimarySize));
        OnPropertyChanged(nameof(SelectedHeight));
        OnPropertyChanged(nameof(SelectedRotation));
        OnPropertyChanged(nameof(SelectedAttenuation));
        OnPropertyChanged(nameof(SelectedLocked));
        OnPropertyChanged(nameof(SelectedVisible));
        OnPropertyChanged(nameof(WallLength));
        OnPropertyChanged(nameof(ApTxPower));
        OnPropertyChanged(nameof(ApChannel));
        OnPropertyChanged(nameof(ApBandwidth));
        OnPropertyChanged(nameof(ApAntennaGain));
        OnPropertyChanged(nameof(ApCoverageTarget));
        OnPropertyChanged(nameof(ApEnabled));
        OnPropertyChanged(nameof(UserWeight));
    }

    private void RecalculateSelectedAnalysis()
    {
        if (SelectedUser is null)
        {
            UserSignalText = "No user selected";
            return;
        }

        var analysis = _userAnalyzer.Analyze(Project, SelectedUser);
        var route = SelectedUser.Route.Count > 0 ? _routeAnalyzer.Analyze(Project, SelectedUser) : null;
        UserSignalText = route is null
            ? $"{analysis.ConnectedApName} / {analysis.RssiDbm:F1} dBm / SNR {analysis.SnrDb:F1} dB / {analysis.Quality} / {analysis.Recommendation}"
            : $"{analysis.ConnectedApName} / {analysis.RssiDbm:F1} dBm / {analysis.Quality} / Route avg {route.AverageRssiDbm:F1}, min {route.MinimumRssiDbm:F1}, handovers {route.HandoverCount}";
    }

    private void UpdateOptimizationSummary()
    {
        var recommendations = OptimizationResult?.Recommendations ?? [];
        if (recommendations.Count == 0)
        {
            OptimizationSummaryText = "No optimization result";
            return;
        }

        var first = recommendations[0];
        var apLines = string.Join(Environment.NewLine, recommendations.Select((recommendation, index) =>
            $"{recommendation.AssignedAccessPointName ?? $"AP {index + 1}"} -> ({recommendation.Position.X:F0}, {recommendation.Position.Y:F0}) cm, Tx {recommendation.RecommendedTxPowerDbm:F0} dBm, Ch {recommendation.RecommendedChannel}"));
        var deltas = first.UserDeltas.Count == 0
            ? "No user deltas"
            : string.Join(Environment.NewLine, first.UserDeltas.Select(d =>
                $"{d.UserName}: {d.BeforeRssiDbm:F0} -> {d.AfterRssiDbm:F0} dBm ({d.ImprovementDb:+0;-0;0} dB), {d.BeforeQuality} -> {d.AfterQuality}"));
        OptimizationSummaryText = $"Score {OptimizationResult?.Score:F1}, AP count {recommendations.Count}{Environment.NewLine}{apLines}{Environment.NewLine}{deltas}";
    }

    private void DuplicateSelectedCore(PlanPoint offset)
    {
        switch (SelectedKind)
        {
            case SelectedElementKind.Wall when SelectedWall is not null:
                var wall = Clone(SelectedWall);
                wall.Id = Guid.NewGuid().ToString("N");
                wall.Name = $"{wall.Name} Copy";
                wall.Center = new PlanPoint(wall.Center.X + offset.X, wall.Center.Y + offset.Y);
                Project.FloorPlan.Walls.Add(wall);
                Select(wall.Id, SelectedElementKind.Wall);
                break;
            case SelectedElementKind.AccessPoint when SelectedAccessPoint is not null:
                var ap = Clone(SelectedAccessPoint);
                ap.Id = Guid.NewGuid().ToString("N");
                ap.Name = $"{ap.Name} Copy";
                ap.Position = new PlanPoint(ap.Position.X + offset.X, ap.Position.Y + offset.Y);
                Project.FloorPlan.AccessPoints.Add(ap);
                Select(ap.Id, SelectedElementKind.AccessPoint);
                break;
            case SelectedElementKind.Object when SelectedObject is not null:
                var obj = Clone(SelectedObject);
                obj.Id = Guid.NewGuid().ToString("N");
                obj.Name = $"{obj.Name} Copy";
                obj.Center = new PlanPoint(obj.X + offset.X, obj.Y + offset.Y);
                Project.FloorPlan.Objects.Add(obj);
                Select(obj.Id, SelectedElementKind.Object);
                break;
            case SelectedElementKind.User when SelectedUser is not null:
                var user = Clone(SelectedUser);
                user.Id = Guid.NewGuid().ToString("N");
                user.Name = $"{user.Name} Copy";
                user.Position = new PlanPoint(user.Position.X + offset.X, user.Position.Y + offset.Y);
                Project.FloorPlan.Users.Add(user);
                Select(user.Id, SelectedElementKind.User);
                break;
        }
    }

    private PlanPoint SnapIfEnabled(PlanPoint point) => SnapEnabled ? GeometryMath.Snap(point, Project.FloorPlan.GridSizeCm) : point;

    private void InvalidateCanvas() => CanvasInvalidated?.Invoke(this, EventArgs.Empty);

    private void Log(string message)
    {
        LogLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (LogLines.Count > 120)
        {
            LogLines.RemoveAt(LogLines.Count - 1);
        }
    }

    private async Task AutoSaveAsync()
    {
        if (DateTimeOffset.UtcNow - _lastAutosaveUtc < TimeSpan.FromSeconds(3))
        {
            return;
        }

        _lastAutosaveUtc = DateTimeOffset.UtcNow;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(AutosavePath) ?? ".");
            await ProjectJsonSerializer.SaveAsync(Project, AutosavePath, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            // Autosave should never interrupt editing; explicit Save/Open surfaces errors in the log.
        }
    }

    private void AddRecentProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var existing = RecentProjects.FirstOrDefault(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            RecentProjects.Remove(existing);
        }

        RecentProjects.Insert(0, path);
        while (RecentProjects.Count > 8)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }

        SelectedRecentProject = RecentProjects.FirstOrDefault();
        SaveRecentProjects();
    }

    private void SaveRecentProjects()
    {
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            File.WriteAllText(RecentProjectsPath, System.Text.Json.JsonSerializer.Serialize(RecentProjects.ToArray(), ProjectJsonSerializer.Options));
        }
        catch
        {
            // Recent projects are convenience state only.
        }
    }

    private static IReadOnlyList<string> LoadRecentProjects()
    {
        try
        {
            if (!File.Exists(RecentProjectsPath))
            {
                return [];
            }

            var json = File.ReadAllText(RecentProjectsPath);
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json, ProjectJsonSerializer.Options)?
                .Where(File.Exists)
                .Take(8)
                .ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }

    private void RunBeginnerWizard()
    {
        var project = ProjectFactory.CreateNewProject();
        project.Name = "Wizard RF Plan";
        project.FloorPlan.WidthCm = 1800;
        project.FloorPlan.HeightCm = 1200;
        project.FloorPlan.Walls.Add(new WallElement
        {
            Name = "Wizard wall",
            Center = new PlanPoint(900, 600),
            LengthCm = 1200,
            ThicknessCm = 12,
            MaterialId = "drywall",
            OverrideAttenuationDb = 3
        });
        project.FloorPlan.Users.Add(new UserLocation
        {
            Name = "Primary user",
            Position = new PlanPoint(1350, 600),
            Weight = 3
        });
        project.FloorPlan.AccessPoints.Add(new AccessPoint
        {
            Name = "AP-01",
            Position = new PlanPoint(450, 600),
            Band = project.SimulationSettings.FrequencyBand
        });
        LoadProject(project, null, "Wizard created a starter project.");
        _ = RunSimulationAsync();
    }

    private static string ExportPath(string suffix, string extension)
    {
        Directory.CreateDirectory(ExportDirectory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(ExportDirectory, $"wifi-studio-{suffix}-{stamp}{extension}");
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(character => invalid.Contains(character) ? '-' : character).ToArray());
    }

    private static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WiFiStudioPro");

    private static string RecoveryDirectory => Path.Combine(AppDataDirectory, "Recovery");

    private static string ExportDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WiFiStudioExports");

    private static string AutosavePath => Path.Combine(RecoveryDirectory, "autosave.wifistudio.json");

    private static string RecentProjectsPath => Path.Combine(AppDataDirectory, "recent-projects.json");

    private static string MaterialLibraryPath => Path.Combine(ExportDirectory, "material-library.json");

    private static double DistanceToWallCenterline(PlanPoint point, WallElement wall)
    {
        var endpoints = WallEndpoints(wall);
        return GeometryMath.DistancePointToSegmentCm(point, endpoints.Start, endpoints.End);
    }

    public static (PlanPoint Start, PlanPoint End) WallEndpoints(WallElement wall)
    {
        var radians = wall.RotationDegrees * Math.PI / 180.0;
        var dx = Math.Cos(radians) * wall.LengthCm / 2.0;
        var dy = Math.Sin(radians) * wall.LengthCm / 2.0;
        return (new PlanPoint(wall.Center.X - dx, wall.Center.Y - dy), new PlanPoint(wall.Center.X + dx, wall.Center.Y + dy));
    }

    private static T Clone<T>(T value)
    {
        return System.Text.Json.JsonSerializer.Deserialize<T>(
            System.Text.Json.JsonSerializer.Serialize(value, ProjectJsonSerializer.Options),
            ProjectJsonSerializer.Options)!;
    }

    private static double NormalizeAngle(double degrees)
    {
        var normalized = degrees % 360.0;
        return normalized < 0 ? normalized + 360.0 : normalized;
    }

    private static FrequencyBand ParseFrequency(string value) => value switch
    {
        "2.4 GHz" => FrequencyBand.Ghz24,
        "6 GHz" => FrequencyBand.Ghz6,
        _ => FrequencyBand.Ghz5
    };

    private static string FormatFrequency(FrequencyBand band) => band switch
    {
        FrequencyBand.Ghz24 => "2.4 GHz",
        FrequencyBand.Ghz6 => "6 GHz",
        _ => "5 GHz"
    };

    private static HeatmapType ParseHeatmapMode(string value) => value switch
    {
        "SNR Heatmap" => HeatmapType.Snr,
        "Interference Heatmap" => HeatmapType.Interference,
        "Best AP Map" => HeatmapType.BestAp,
        "Dead Zone Map" => HeatmapType.DeadZone,
        "User Quality Map" => HeatmapType.UserQuality,
        _ => HeatmapType.Rssi
    };

    private static string FormatHeatmapMode(HeatmapType type) => type switch
    {
        HeatmapType.Snr => "SNR Heatmap",
        HeatmapType.Interference => "Interference Heatmap",
        HeatmapType.BestAp => "Best AP Map",
        HeatmapType.DeadZone => "Dead Zone Map",
        HeatmapType.UserQuality => "User Quality Map",
        _ => "RSSI Heatmap"
    };

    private static IEnumerable<PaletteItem> CreatePaletteItems()
    {
        yield return new PaletteItem { Category = "Structure", Label = "Wall", ToolParameter = "Wall" };
        yield return new PaletteItem { Category = "Structure", Label = "Glass Door", ToolParameter = "Object:GlassDoor", ObjectType = PlanObjectType.GlassDoor };
        yield return new PaletteItem { Category = "Structure", Label = "Wood Door", ToolParameter = "Object:WoodDoor", ObjectType = PlanObjectType.WoodDoor };
        yield return new PaletteItem { Category = "Structure", Label = "Column", ToolParameter = "Object:ConcreteColumn", ObjectType = PlanObjectType.ConcreteColumn };
        yield return new PaletteItem { Category = "Structure", Label = "Stairs", ToolParameter = "Object:Stairs", ObjectType = PlanObjectType.Stairs };
        yield return new PaletteItem { Category = "Structure", Label = "Elevator", ToolParameter = "Object:ElevatorShaft", ObjectType = PlanObjectType.ElevatorShaft };

        foreach (var type in new[] { PlanObjectType.Desk, PlanObjectType.Chair, PlanObjectType.Sofa, PlanObjectType.Bed, PlanObjectType.Bookshelf, PlanObjectType.Cabinet, PlanObjectType.ConferenceTable, PlanObjectType.Partition, PlanObjectType.Plant })
        {
            yield return new PaletteItem { Category = "Furniture", Label = PlanObjectPreset.For(type).Name, ToolParameter = $"Object:{type}", ObjectType = type };
        }

        foreach (var type in new[] { PlanObjectType.Tv, PlanObjectType.Refrigerator, PlanObjectType.WashingMachine, PlanObjectType.Microwave, PlanObjectType.ServerRack, PlanObjectType.MetalShelf })
        {
            yield return new PaletteItem { Category = "Electronics", Label = PlanObjectPreset.For(type).Name, ToolParameter = $"Object:{type}", ObjectType = type };
        }

        yield return new PaletteItem { Category = "Network", Label = "AP", ToolParameter = "AccessPoint" };
        yield return new PaletteItem { Category = "Network", Label = "Mesh Node", ToolParameter = "Object:MeshNode", ObjectType = PlanObjectType.MeshNode };
        yield return new PaletteItem { Category = "Network", Label = "Router", ToolParameter = "Object:Router", ObjectType = PlanObjectType.Router };

        yield return new PaletteItem { Category = "User", Label = "Person", ToolParameter = "Object:Person", ObjectType = PlanObjectType.Person };
        yield return new PaletteItem { Category = "User", Label = "Fixed User", ToolParameter = "Object:FixedUser", ObjectType = PlanObjectType.FixedUser };
        yield return new PaletteItem { Category = "User", Label = "Mobile User", ToolParameter = "Object:MobileUser", ObjectType = PlanObjectType.MobileUser };
        yield return new PaletteItem { Category = "User", Label = "User Group", ToolParameter = "Object:UserGroup", ObjectType = PlanObjectType.UserGroup };
    }
}
