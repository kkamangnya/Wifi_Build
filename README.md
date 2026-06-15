# WiFi Studio Pro

Windows-only Wi-Fi RF Planner MVP built with C#/.NET 10, WinUI 3, Windows App SDK, MVVM, JSON project files, and xUnit tests.

## Architecture

```text
/src
  /WiFiStudio.App
    App.xaml
    MainWindow.xaml
    Controls/PlanCanvasControl.xaml
    ViewModels/
    Services/
    Resources/
  /WiFiStudio.Core
    Models/
    Geometry/
    Simulation/
    Optimization/
    Serialization/
  /WiFiStudio.Rendering
    Canvas/
    Heatmaps/
    Layers/
  /WiFiStudio.Tests
    SimulationTests/
    GeometryTests/
    SerializationTests/
/samples
  sample-office.wifistudio.json
/resources
  material-library.json
```

`WiFiStudio.Core` owns all JSON models, RF math, geometry, serialization, and AP placement logic. `WiFiStudio.Rendering` converts RF results into cached heatmap raster data. `WiFiStudio.App` contains WinUI 3 UI, file pickers, MVVM view models, and canvas interaction.

## MVP Features

- New project and sample office template.
- JSON save/open, recent project list, autosave recovery, and schema v2 multi-floor fields.
- 2D canvas with grid, snap toggle, zoom wheel, wall drawing, AP placement, object placement, user placement, route-point placement, selection, drag move, wall endpoint handles, resize/rotate handles, Ctrl+drag duplicate, Delete, Undo, and Redo.
- Layer controls for heatmap, structures, objects, APs, and users plus per-selection visible/locked state.
- Extended object palette: desks, chairs, sofas, beds, bookshelves, cabinets, partitions, doors, columns, stairs, elevator shafts, appliances, server racks, metal shelves, APs, mesh nodes, routers, people, fixed users, mobile users, and user groups.
- Material selection for selected walls and objects with frequency-specific attenuation multipliers.
- AP inspector editing for Tx power, channel, bandwidth, antenna gain, coverage target, and enabled state.
- 2.4 GHz, 5 GHz, and 6 GHz RSSI simulation.
- FSPL + wall material attenuation + door/window partial attenuation + object attenuation + co-channel/adjacent-channel interference formula.
- Async simulation with cancellation and region-aware heatmap cache invalidation.
- RSSI, SNR, interference, best AP, dead zone, and user quality heatmap modes.
- User markers show quality color, RSSI, serving AP, and recommendation details in the inspector.
- First-pass user-aware AP recommendation with before/after user RSSI deltas, AP count/channel/Tx recommendations, and accept action.
- Route simulation summary and canvas markers for handover and dead-zone samples.
- Experiment Mode runs five repeatable structure/material conditions, compares user and area RSSI, calculates user-centered AP optimization deltas, and generates an analysis summary.
- Experiment runs automatically export a result CSV and one heatmap PNG per condition to `Documents\WiFiStudioExports\Experiment-<timestamp>`.
- `Report Image` export creates report-friendly PNGs using Segoe UI or Noto Sans KR fonts at 1920px or higher, including condition heatmaps and Condition 5 Before/After/Delta comparison images.
- Export: CSV analysis, SVG plan, PNG heatmap, PDF summary report, and material library JSON.
- Beginner wizard command for a starter RF plan.

## Build

Requirements:

- Windows 11 or recent Windows 10 build with Windows App SDK support.
- .NET SDK 10.0.300 or newer.

Commands:

```powershell
dotnet restore WiFiStudio.slnx
dotnet build WiFiStudio.slnx
dotnet test src\WiFiStudio.Tests\WiFiStudio.Tests.csproj
dotnet run --project src\WiFiStudio.App\WiFiStudio.App.csproj
dotnet publish src\WiFiStudio.App\WiFiStudio.App.csproj -c Release -r win-x64 --self-contained true -o artifacts\publish\WiFiStudio.App
```

## Shortcuts

- `Ctrl+N`: new project
- `Ctrl+O`: open project
- `Ctrl+S`: save project
- `Ctrl+R`: run RSSI simulation
- `Ctrl+K`: command palette
- `Ctrl+Z`: undo
- `Ctrl+Y`: redo
- `Delete`: delete selection
- `Ctrl+drag`: duplicate while moving
- `Shift+drag`: constrain move axis
- Mouse wheel over canvas: zoom

## RF Model

```text
RSSI = TxPowerDbm - FSPL - MaterialLoss - InterferencePenalty
FSPL(dB) = 32.44 + 20log10(distance_km) + 20log10(frequency_mhz)
```

Distances below 1 meter are clamped for numerical stability. Wall and furniture attenuation are accumulated along the direct AP-to-sample segment. The current optimizer uses a coarse grid candidate search and excludes wall/furniture interiors.
