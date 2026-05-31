using WiFiStudio.Core.Models;

namespace WiFiStudio.Core.Geometry;

public static class GeometryMath
{
    public static PlanPoint Snap(PlanPoint point, double gridSizeCm)
    {
        if (gridSizeCm <= 0)
        {
            return point;
        }

        return new PlanPoint(
            Math.Round(point.X / gridSizeCm) * gridSizeCm,
            Math.Round(point.Y / gridSizeCm) * gridSizeCm);
    }

    public static double DistanceCm(PlanPoint a, PlanPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public static double DistanceMeters(PlanPoint a, PlanPoint b) => DistanceCm(a, b) / 100.0;

    public static double RotationDegrees(PlanPoint from, PlanPoint to)
    {
        return Math.Atan2(to.Y - from.Y, to.X - from.X) * 180.0 / Math.PI;
    }

    public static PlanPoint Midpoint(PlanPoint a, PlanPoint b) => new((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0);

    public static IReadOnlyList<PlanPoint> WallFootprint(WallElement wall)
    {
        return RotatedRectangle(wall.Center, new PlanSize(wall.LengthCm, wall.ThicknessCm), wall.RotationDegrees);
    }

    public static IReadOnlyList<PlanPoint> FurnitureFootprint(FurnitureElement furniture)
    {
        return RotatedRectangle(furniture.Center, furniture.SizeCm, furniture.RotationDegrees);
    }

    public static IReadOnlyList<PlanPoint> DoorFootprint(DoorElement door)
    {
        return RotatedRectangle(door.Center, new PlanSize(door.WidthCm, 12), door.RotationDegrees);
    }

    public static IReadOnlyList<PlanPoint> WindowFootprint(WindowElement window)
    {
        return RotatedRectangle(window.Center, new PlanSize(window.WidthCm, 8), window.RotationDegrees);
    }

    public static IReadOnlyList<PlanPoint> ObjectFootprint(PlanObject planObject)
    {
        return RotatedRectangle(planObject.Center, planObject.Size, planObject.Rotation);
    }

    public static IReadOnlyList<PlanPoint> RotatedRectangle(PlanPoint center, PlanSize size, double rotationDegrees)
    {
        var hw = size.Width / 2.0;
        var hh = size.Height / 2.0;
        var local = new[]
        {
            new PlanPoint(-hw, -hh),
            new PlanPoint(hw, -hh),
            new PlanPoint(hw, hh),
            new PlanPoint(-hw, hh)
        };

        var radians = rotationDegrees * Math.PI / 180.0;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        return local
            .Select(p => new PlanPoint(center.X + p.X * cos - p.Y * sin, center.Y + p.X * sin + p.Y * cos))
            .ToArray();
    }

    public static bool SegmentIntersectsWall(PlanPoint from, PlanPoint to, WallElement wall)
    {
        return SegmentIntersectsPolygon(from, to, WallFootprint(wall));
    }

    public static bool SegmentIntersectsFurniture(PlanPoint from, PlanPoint to, FurnitureElement furniture)
    {
        return SegmentIntersectsPolygon(from, to, FurnitureFootprint(furniture));
    }

    public static bool SegmentIntersectsDoor(PlanPoint from, PlanPoint to, DoorElement door)
    {
        return SegmentIntersectsPolygon(from, to, DoorFootprint(door));
    }

    public static bool SegmentIntersectsWindow(PlanPoint from, PlanPoint to, WindowElement window)
    {
        return SegmentIntersectsPolygon(from, to, WindowFootprint(window));
    }

    public static bool SegmentIntersectsObject(PlanPoint from, PlanPoint to, PlanObject planObject)
    {
        return SegmentIntersectsPolygon(from, to, ObjectFootprint(planObject));
    }

    public static bool SegmentIntersectsPolygon(PlanPoint from, PlanPoint to, IReadOnlyList<PlanPoint> polygon)
    {
        if (polygon.Count < 3)
        {
            return false;
        }

        if (PointInPolygon(from, polygon) || PointInPolygon(to, polygon))
        {
            return true;
        }

        for (var i = 0; i < polygon.Count; i++)
        {
            var a = polygon[i];
            var b = polygon[(i + 1) % polygon.Count];
            if (SegmentsIntersect(from, to, a, b))
            {
                return true;
            }
        }

        return false;
    }

    public static bool PointInsideAnyObstacle(PlanPoint point, ProjectModel project)
    {
        return project.FloorPlan.Walls.Any(w => PointInPolygon(point, WallFootprint(w)))
            || project.FloorPlan.Doors.Any(d => PointInPolygon(point, DoorFootprint(d)))
            || project.FloorPlan.Windows.Any(w => PointInPolygon(point, WindowFootprint(w)))
            || project.FloorPlan.Furniture.Any(f => PointInPolygon(point, FurnitureFootprint(f)))
            || project.FloorPlan.Objects.Any(o => o.BlocksSignal && PointInPolygon(point, ObjectFootprint(o)));
    }

    public static bool PointInsideFloor(PlanPoint point, FloorPlan floor)
    {
        return point.X >= 0 && point.Y >= 0 && point.X <= floor.WidthCm && point.Y <= floor.HeightCm;
    }

    public static double DistancePointToSegmentCm(PlanPoint point, PlanPoint start, PlanPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= double.Epsilon)
        {
            return DistanceCm(point, start);
        }

        var t = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
        t = Math.Clamp(t, 0.0, 1.0);
        var projection = new PlanPoint(start.X + t * dx, start.Y + t * dy);
        return DistanceCm(point, projection);
    }

    public static bool PointInPolygon(PlanPoint point, IReadOnlyList<PlanPoint> polygon)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var pi = polygon[i];
            var pj = polygon[j];
            var intersects = ((pi.Y > point.Y) != (pj.Y > point.Y))
                && (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y + double.Epsilon) + pi.X);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static bool SegmentsIntersect(PlanPoint p1, PlanPoint p2, PlanPoint q1, PlanPoint q2)
    {
        var o1 = Orientation(p1, p2, q1);
        var o2 = Orientation(p1, p2, q2);
        var o3 = Orientation(q1, q2, p1);
        var o4 = Orientation(q1, q2, p2);

        if (o1 != o2 && o3 != o4)
        {
            return true;
        }

        return o1 == 0 && OnSegment(p1, q1, p2)
            || o2 == 0 && OnSegment(p1, q2, p2)
            || o3 == 0 && OnSegment(q1, p1, q2)
            || o4 == 0 && OnSegment(q1, p2, q2);
    }

    private static int Orientation(PlanPoint a, PlanPoint b, PlanPoint c)
    {
        var value = (b.Y - a.Y) * (c.X - b.X) - (b.X - a.X) * (c.Y - b.Y);
        if (Math.Abs(value) < 0.000001)
        {
            return 0;
        }

        return value > 0 ? 1 : 2;
    }

    private static bool OnSegment(PlanPoint a, PlanPoint b, PlanPoint c)
    {
        return b.X <= Math.Max(a.X, c.X) + 0.000001
            && b.X + 0.000001 >= Math.Min(a.X, c.X)
            && b.Y <= Math.Max(a.Y, c.Y) + 0.000001
            && b.Y + 0.000001 >= Math.Min(a.Y, c.Y);
    }
}
