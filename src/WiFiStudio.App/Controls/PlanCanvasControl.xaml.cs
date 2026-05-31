using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Windows.System;
using Windows.UI;
using WiFiStudio.App.ViewModels;
using WiFiStudio.Core.Geometry;
using WiFiStudio.Core.Models;
using WiFiStudio.Core.Simulation;
using WiFiStudio.Rendering.Heatmaps;

namespace WiFiStudio.App.Controls;

public sealed partial class PlanCanvasControl : UserControl
{
    public static readonly DependencyProperty ViewModelProperty =
        DependencyProperty.Register(
            nameof(ViewModel),
            typeof(MainViewModel),
            typeof(PlanCanvasControl),
            new PropertyMetadata(null, OnViewModelChanged));

    private const double BasePixelsPerCm = 0.25;
    private readonly UserSignalAnalyzer _userAnalyzer = new();
    private readonly UserRouteSimulationEngine _routeAnalyzer = new();
    private PlanPoint? _wallStart;
    private PlanPoint? _lastDragPoint;
    private Line? _previewLine;
    private DragMode _manipulationMode = DragMode.None;

    public PlanCanvasControl()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    public MainViewModel? ViewModel
    {
        get => (MainViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var control = (PlanCanvasControl)dependencyObject;
        if (args.OldValue is MainViewModel oldVm)
        {
            oldVm.CanvasInvalidated -= control.ViewModelOnCanvasInvalidated;
        }

        if (args.NewValue is MainViewModel newVm)
        {
            newVm.CanvasInvalidated += control.ViewModelOnCanvasInvalidated;
        }

        control.Render();
    }

    private void ViewModelOnCanvasInvalidated(object? sender, EventArgs e) => Render();

    private void Render()
    {
        DrawingCanvas.Children.Clear();
        _previewLine = null;

        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        var project = viewModel.Project;
        var scale = CanvasScale;
        var width = Math.Max(1, project.FloorPlan.WidthCm * scale);
        var height = Math.Max(1, project.FloorPlan.HeightCm * scale);
        DrawingCanvas.Width = width;
        DrawingCanvas.Height = height;

        if (project.LayerState.HeatmapVisible && project.HeatmapDisplay.IsVisible)
        {
            DrawHeatmap(viewModel, width, height);
        }

        DrawGrid(project.FloorPlan, scale, width, height);
        if (project.LayerState.StructuresVisible)
        {
            DrawWalls(project, scale);
        }

        if (project.LayerState.ObjectsVisible)
        {
            DrawObjects(project, scale);
        }

        if (project.LayerState.AccessPointsVisible)
        {
            DrawAccessPoints(project, scale);
        }

        if (project.LayerState.UsersVisible)
        {
            DrawUsers(viewModel, scale);
        }
        DrawPendingRecommendation(viewModel, scale);
        DrawSelection(viewModel, scale);
    }

    private double CanvasScale => BasePixelsPerCm * (ViewModel?.Zoom ?? 1.0);

    private void DrawHeatmap(MainViewModel viewModel, double width, double height)
    {
        if (viewModel.HeatmapResult is null)
        {
            return;
        }

        var raster = HeatmapRasterizer.Rasterize(
            viewModel.HeatmapResult,
            viewModel.Project.FloorPlan.WidthCm,
            viewModel.Project.FloorPlan.HeightCm,
            Math.Max(1, (int)Math.Round(width)),
            Math.Max(1, (int)Math.Round(height)));

        var bitmap = new WriteableBitmap(raster.Width, raster.Height);
        using (var stream = bitmap.PixelBuffer.AsStream())
        {
            stream.Write(raster.BgraPixels, 0, raster.BgraPixels.Length);
        }

        bitmap.Invalidate();
        var image = new Image
        {
            Source = bitmap,
            Width = width,
            Height = height,
            Stretch = Stretch.Fill,
            Opacity = Math.Clamp(viewModel.Project.HeatmapDisplay.Opacity, 0.1, 1.0),
            IsHitTestVisible = false
        };
        DrawingCanvas.Children.Add(image);
    }

    private void DrawGrid(FloorPlan floor, double scale, double width, double height)
    {
        var gridPx = floor.GridSizeCm * scale;
        if (gridPx < 6)
        {
            return;
        }

        var brush = new SolidColorBrush(Color.FromArgb(36, 120, 130, 145));
        for (var x = 0.0; x <= width; x += gridPx)
        {
            DrawingCanvas.Children.Add(new Line
            {
                X1 = x,
                X2 = x,
                Y1 = 0,
                Y2 = height,
                Stroke = brush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            });
        }

        for (var y = 0.0; y <= height; y += gridPx)
        {
            DrawingCanvas.Children.Add(new Line
            {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = brush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            });
        }
    }

    private void DrawWalls(ProjectModel project, double scale)
    {
        foreach (var wall in project.FloorPlan.Walls.Where(w => w.IsVisible))
        {
            var material = project.MaterialOrDefault(wall.MaterialId);
            var fill = new SolidColorBrush(ParseColor(material.Color, Color.FromArgb(255, 170, 170, 170)));
            var rect = new Rectangle
            {
                Width = Math.Max(1, wall.LengthCm * scale),
                Height = Math.Max(2, wall.ThicknessCm * scale),
                RadiusX = 1,
                RadiusY = 1,
                Fill = fill,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 49, 52, 58)),
                StrokeThickness = 1,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform { Angle = wall.RotationDegrees }
            };
            Canvas.SetLeft(rect, wall.Center.X * scale - rect.Width / 2.0);
            Canvas.SetTop(rect, wall.Center.Y * scale - rect.Height / 2.0);
            DrawingCanvas.Children.Add(rect);
        }
    }

    private void DrawObjects(ProjectModel project, double scale)
    {
        foreach (var planObject in project.FloorPlan.Objects.Where(o => o.IsVisible).OrderBy(o => o.ZIndex))
        {
            var preset = PlanObjectPreset.For(planObject.Type);
            var material = project.MaterialOrDefault(planObject.Material);
            var rect = new Rectangle
            {
                Width = Math.Max(6, planObject.Width * scale),
                Height = Math.Max(6, planObject.Height * scale),
                RadiusX = 4,
                RadiusY = 4,
                Fill = new SolidColorBrush(ParseColor(material.Color, Color.FromArgb(210, 150, 150, 150))),
                Stroke = new SolidColorBrush(Color.FromArgb(255, 76, 84, 96)),
                StrokeThickness = 1,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new RotateTransform { Angle = planObject.Rotation }
            };
            Canvas.SetLeft(rect, planObject.X * scale - rect.Width / 2.0);
            Canvas.SetTop(rect, planObject.Y * scale - rect.Height / 2.0);
            DrawingCanvas.Children.Add(rect);

            var label = new TextBlock
            {
                Text = preset.Name,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, planObject.X * scale - rect.Width / 2.0);
            Canvas.SetTop(label, planObject.Y * scale - rect.Height / 2.0 - 16);
            DrawingCanvas.Children.Add(label);
        }
    }

    private void DrawAccessPoints(ProjectModel project, double scale)
    {
        foreach (var ap in project.FloorPlan.AccessPoints.Where(a => a.IsVisible))
        {
            var x = ap.Position.X * scale;
            var y = ap.Position.Y * scale;
            var coverageRadius = CoverageRadiusCm(ap) * scale;
            var coverage = new Ellipse
            {
                Width = coverageRadius * 2,
                Height = coverageRadius * 2,
                Stroke = new SolidColorBrush(Color.FromArgb(80, 30, 144, 255)),
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(18, 30, 144, 255)),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(coverage, x - coverageRadius);
            Canvas.SetTop(coverage, y - coverageRadius);
            DrawingCanvas.Children.Add(coverage);

            var radius = 11.0;
            var ellipse = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = new SolidColorBrush(Colors.DodgerBlue),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            Canvas.SetLeft(ellipse, x - radius);
            Canvas.SetTop(ellipse, y - radius);
            DrawingCanvas.Children.Add(ellipse);

            var label = new TextBlock
            {
                Text = ap.Name,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x + radius + 4);
            Canvas.SetTop(label, y - 10);
            DrawingCanvas.Children.Add(label);
        }
    }

    private void DrawUsers(MainViewModel viewModel, double scale)
    {
        foreach (var user in viewModel.Project.FloorPlan.Users.Where(u => u.IsVisible))
        {
            var analysis = _userAnalyzer.Analyze(viewModel.Project, user);
            var color = analysis.Quality switch
            {
                LinkQuality.Excellent or LinkQuality.Good => Colors.LimeGreen,
                LinkQuality.Fair => Colors.Gold,
                LinkQuality.Poor => Colors.DarkOrange,
                _ => Colors.Red
            };
            var x = user.Position.X * scale;
            var y = user.Position.Y * scale;
            var marker = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 2
            };
            Canvas.SetLeft(marker, x - 9);
            Canvas.SetTop(marker, y - 9);
            DrawingCanvas.Children.Add(marker);

            var text = new TextBlock
            {
                Text = $"{user.Name} {analysis.RssiDbm:F0} dBm {analysis.ConnectedApName}",
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(text, x + 12);
            Canvas.SetTop(text, y - 18);
            DrawingCanvas.Children.Add(text);

            if (user.Route.Count > 0)
            {
                DrawUserRoute(viewModel.Project, user, scale);
            }
        }
    }

    private void DrawUserRoute(ProjectModel project, UserLocation user, double scale)
    {
        var points = new List<PlanPoint> { user.Position };
        points.AddRange(user.Route);
        for (var i = 0; i < points.Count - 1; i++)
        {
            DrawingCanvas.Children.Add(new Line
            {
                X1 = points[i].X * scale,
                Y1 = points[i].Y * scale,
                X2 = points[i + 1].X * scale,
                Y2 = points[i + 1].Y * scale,
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 3, 3 },
                IsHitTestVisible = false
            });
        }

        var route = _routeAnalyzer.Analyze(project, user);
        string? previousAp = null;
        foreach (var sample in route.Samples)
        {
            var isDeadZone = sample.Quality == LinkQuality.DeadZone;
            var isHandover = previousAp is not null && sample.ServingApId is not null && sample.ServingApId != previousAp;
            previousAp = sample.ServingApId ?? previousAp;
            if (!isDeadZone && !isHandover)
            {
                continue;
            }

            var radius = isDeadZone ? 7.0 : 5.0;
            var marker = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = new SolidColorBrush(isDeadZone ? Colors.Red : Colors.Cyan),
                Stroke = new SolidColorBrush(Colors.White),
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(marker, sample.X * scale - radius);
            Canvas.SetTop(marker, sample.Y * scale - radius);
            DrawingCanvas.Children.Add(marker);
        }
    }

    private void DrawPendingRecommendation(MainViewModel viewModel, double scale)
    {
        var recommendation = viewModel.PendingRecommendation;
        if (recommendation is null)
        {
            return;
        }

        var x = recommendation.Position.X * scale;
        var y = recommendation.Position.Y * scale;
        var radius = 16.0;
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(Colors.LimeGreen),
            StrokeThickness = 3,
            Fill = new SolidColorBrush(Color.FromArgb(48, 39, 201, 63))
        };
        Canvas.SetLeft(ellipse, x - radius);
        Canvas.SetTop(ellipse, y - radius);
        DrawingCanvas.Children.Add(ellipse);
    }

    private void DrawSelection(MainViewModel viewModel, double scale)
    {
        switch (viewModel.SelectedKind)
        {
            case SelectedElementKind.Wall when viewModel.SelectedWall is not null:
                DrawWallSelection(viewModel.SelectedWall, scale);
                break;
            case SelectedElementKind.AccessPoint when viewModel.SelectedAccessPoint is not null:
                DrawPointSelection(viewModel.SelectedAccessPoint.Position, scale, 18);
                break;
            case SelectedElementKind.Object when viewModel.SelectedObject is not null:
                DrawObjectSelection(viewModel.SelectedObject, scale);
                break;
            case SelectedElementKind.User when viewModel.SelectedUser is not null:
                DrawPointSelection(viewModel.SelectedUser.Position, scale, 16);
                break;
        }
    }

    private void DrawWallSelection(WallElement wall, double scale)
    {
        var endpoints = MainViewModel.WallEndpoints(wall);
        DrawingCanvas.Children.Add(new Line
        {
            X1 = endpoints.Start.X * scale,
            Y1 = endpoints.Start.Y * scale,
            X2 = endpoints.End.X * scale,
            Y2 = endpoints.End.Y * scale,
            Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
            StrokeThickness = Math.Max(3, wall.ThicknessCm * scale + 2),
            Opacity = 0.65,
            IsHitTestVisible = false
        });
        DrawHandle(endpoints.Start, scale, Colors.White);
        DrawHandle(endpoints.End, scale, Colors.White);
        DrawHandle(new PlanPoint(wall.Center.X, wall.Center.Y - 90), scale, Colors.DeepSkyBlue);
        var label = new TextBlock
        {
            Text = $"{wall.LengthCm / 100.0:F1} m / {wall.RotationDegrees:F0} deg",
            FontSize = 11,
            Foreground = new SolidColorBrush(Colors.White),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, wall.Center.X * scale + 10);
        Canvas.SetTop(label, wall.Center.Y * scale + 10);
        DrawingCanvas.Children.Add(label);
    }

    private void DrawObjectSelection(PlanObject planObject, double scale)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(8, planObject.Width * scale + 8),
            Height = Math.Max(8, planObject.Height * scale + 8),
            Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 4, 3 },
            Fill = new SolidColorBrush(Colors.Transparent),
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new RotateTransform { Angle = planObject.Rotation },
            IsHitTestVisible = false
        };
        Canvas.SetLeft(rect, planObject.X * scale - rect.Width / 2.0);
        Canvas.SetTop(rect, planObject.Y * scale - rect.Height / 2.0);
        DrawingCanvas.Children.Add(rect);
        DrawHandle(new PlanPoint(planObject.X + planObject.Width / 2.0, planObject.Y + planObject.Height / 2.0), scale, Colors.White);
        DrawHandle(new PlanPoint(planObject.X, planObject.Y - planObject.Height / 2.0 - 80), scale, Colors.DeepSkyBlue);
    }

    private void DrawPointSelection(PlanPoint point, double scale, double radius)
    {
        var ellipse = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
            StrokeThickness = 2,
            Fill = new SolidColorBrush(Colors.Transparent),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(ellipse, point.X * scale - radius);
        Canvas.SetTop(ellipse, point.Y * scale - radius);
        DrawingCanvas.Children.Add(ellipse);
        DrawHandle(new PlanPoint(point.X + 70, point.Y - 70), scale, Colors.DeepSkyBlue);
    }

    private void DrawHandle(PlanPoint point, double scale, Color color)
    {
        var handle = new Rectangle
        {
            Width = 10,
            Height = 10,
            Fill = new SolidColorBrush(color),
            Stroke = new SolidColorBrush(Colors.Black),
            StrokeThickness = 1,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(handle, point.X * scale - 5);
        Canvas.SetTop(handle, point.Y * scale - 5);
        DrawingCanvas.Children.Add(handle);
    }

    private void DrawingCanvas_OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        var point = ToPlanPoint(e.GetCurrentPoint(DrawingCanvas).Position);
        if (viewModel.ActiveTool == CanvasTool.Wall)
        {
            _wallStart = GeometryMath.Snap(point, viewModel.Project.FloorPlan.GridSizeCm);
            DrawingCanvas.CapturePointer(e.Pointer);
        }
        else if (viewModel.ActiveTool == CanvasTool.AccessPoint)
        {
            viewModel.AddAccessPoint(point);
        }
        else if (viewModel.ActiveTool is CanvasTool.Object or CanvasTool.User)
        {
            viewModel.AddObject(viewModel.ActiveObjectType, point);
        }
        else if (viewModel.ActiveTool == CanvasTool.RoutePoint)
        {
            viewModel.AddRoutePoint(point);
        }
        else
        {
            viewModel.SelectAt(point);
            if (viewModel.HasSelection)
            {
                _lastDragPoint = point;
                _manipulationMode = HitHandle(viewModel, point);
                if (viewModel.BeginManipulation(e.KeyModifiers.HasFlag(VirtualKeyModifiers.Control)))
                {
                    DrawingCanvas.CapturePointer(e.Pointer);
                }
                else
                {
                    _lastDragPoint = null;
                    _manipulationMode = DragMode.None;
                }
            }
        }
    }

    private void DrawingCanvas_OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        var point = ToPlanPoint(e.GetCurrentPoint(DrawingCanvas).Position);
        viewModel.UpdatePointerPosition(point);

        if (_wallStart is not null)
        {
            DrawPreviewLine(_wallStart, GeometryMath.Snap(point, viewModel.Project.FloorPlan.GridSizeCm));
        }
            else if (_lastDragPoint is not null && _manipulationMode != DragMode.None)
        {
            var delta = new PlanPoint(point.X - _lastDragPoint.X, point.Y - _lastDragPoint.Y);
            if (_manipulationMode == DragMode.Move)
            {
                viewModel.MoveSelectedBy(delta, e.KeyModifiers.HasFlag(VirtualKeyModifiers.Shift));
            }
            else if (_manipulationMode == DragMode.Resize)
            {
                viewModel.ResizeSelectedBy((delta.X + delta.Y) * 0.5);
            }
            else if (_manipulationMode == DragMode.WallStart)
            {
                viewModel.ResizeWallEndpoint(moveStart: true, point);
            }
            else if (_manipulationMode == DragMode.WallEnd)
            {
                viewModel.ResizeWallEndpoint(moveStart: false, point);
            }
            else if (_manipulationMode == DragMode.Rotate)
            {
                viewModel.RotateSelectedBy(delta.X * 0.25);
            }

            _lastDragPoint = point;
        }
    }

    private void DrawingCanvas_OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        var viewModel = ViewModel;
        if (viewModel is null)
        {
            return;
        }

        if (_wallStart is not null)
        {
            var point = GeometryMath.Snap(ToPlanPoint(e.GetCurrentPoint(DrawingCanvas).Position), viewModel.Project.FloorPlan.GridSizeCm);
            viewModel.AddWall(_wallStart, point);
            _wallStart = null;
        }
        else if (_lastDragPoint is not null)
        {
            viewModel.EndManipulation();
            _lastDragPoint = null;
            _manipulationMode = DragMode.None;
        }

        DrawingCanvas.ReleasePointerCaptures();
    }

    private void DrawingCanvas_OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        var delta = e.GetCurrentPoint(DrawingCanvas).Properties.MouseWheelDelta > 0 ? 0.1 : -0.1;
        ViewModel?.ZoomBy(delta);
        e.Handled = true;
    }

    private PlanPoint ToPlanPoint(Point point)
    {
        return new PlanPoint(point.X / CanvasScale, point.Y / CanvasScale);
    }

    private void DrawPreviewLine(PlanPoint start, PlanPoint end)
    {
        if (_previewLine is null)
        {
            _previewLine = new Line
            {
                Stroke = new SolidColorBrush(Colors.DeepSkyBlue),
                StrokeThickness = 3,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };
            DrawingCanvas.Children.Add(_previewLine);
        }

        var scale = CanvasScale;
        _previewLine.X1 = start.X * scale;
        _previewLine.Y1 = start.Y * scale;
        _previewLine.X2 = end.X * scale;
        _previewLine.Y2 = end.Y * scale;
    }

    private static Color ParseColor(string hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return fallback;
        }

        var value = hex.Trim().TrimStart('#');
        if (value.Length != 6 || !int.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out var number))
        {
            return fallback;
        }

        return Color.FromArgb(255, (byte)(number >> 16), (byte)(number >> 8), (byte)number);
    }

    private DragMode HitHandle(MainViewModel viewModel, PlanPoint point)
    {
        var center = viewModel.SelectedWall?.Center
            ?? viewModel.SelectedAccessPoint?.Position
            ?? viewModel.SelectedObject?.Center
            ?? viewModel.SelectedUser?.Position;
        if (center is null)
        {
            return DragMode.None;
        }

        if (viewModel.SelectedObject is not null)
        {
            var obj = viewModel.SelectedObject;
            if (GeometryMath.DistanceCm(point, new PlanPoint(obj.X + obj.Width / 2.0, obj.Y + obj.Height / 2.0)) < 55)
            {
                return DragMode.Resize;
            }

            if (GeometryMath.DistanceCm(point, new PlanPoint(obj.X, obj.Y - obj.Height / 2.0 - 80)) < 60)
            {
                return DragMode.Rotate;
            }
        }

        if (viewModel.SelectedWall is not null)
        {
            var wall = viewModel.SelectedWall;
            var endpoints = MainViewModel.WallEndpoints(wall);
            if (GeometryMath.DistanceCm(point, endpoints.Start) < 55)
            {
                return DragMode.WallStart;
            }

            if (GeometryMath.DistanceCm(point, endpoints.End) < 55)
            {
                return DragMode.WallEnd;
            }

            if (GeometryMath.DistanceCm(point, new PlanPoint(wall.Center.X, wall.Center.Y - 90)) < 60)
            {
                return DragMode.Rotate;
            }
        }

        return DragMode.Move;
    }

    private static double CoverageRadiusCm(AccessPoint ap)
    {
        var frequency = WiFiStudio.Core.Simulation.RfCalculator.FrequencyMhz(ap.Band);
        var target = ap.CoverageTargetDbm;
        var budget = ap.TxPowerDbm + ap.AntennaGainDbi - target - 32.44 - 20.0 * Math.Log10(frequency);
        var distanceKm = Math.Pow(10.0, budget / 20.0);
        return Math.Clamp(distanceKm * 1000.0 * 100.0, 120, 1600);
    }

    private enum DragMode
    {
        None,
        Move,
        Resize,
        WallStart,
        WallEnd,
        Rotate
    }
}
