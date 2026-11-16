using System.Numerics;

namespace RutaEorzea;

public readonly record struct RouteProgress(Vector3 Foot, int Segment, float Remaining, float DistanceFromRoute);

public static class RouteMath
{
    public static Vector2 Xz(Vector3 p) => new(p.X, p.Z);
    public static Vector2? MinimapProjection(float markerScale, float sheetFactor, float uiScale, float rotation)
    {
        var scale = markerScale * sheetFactor * uiScale;
        if (!float.IsFinite(scale) || markerScale <= 0 || sheetFactor <= 0 || uiScale <= 0 ||
            scale < .05f || scale > 30 || !float.IsFinite(rotation)) return null;
        return new Vector2(MathF.Cos(rotation), MathF.Sin(rotation)) * scale;
    }
    public static Vector2 AreaMapPoint(Vector2 world, Vector2 center, Vector2 mapOffset,
        Vector2 worldOffset, float sizeFactor, Vector2 scale) =>
        center + ((world + worldOffset) * sizeFactor - mapOffset) * scale;
    public static Vector2 AreaMapWorld(Vector2 screen, Vector2 center, Vector2 mapOffset,
        Vector2 worldOffset, float sizeFactor, Vector2 scale) =>
        ((screen - center) / scale + mapOffset) / sizeFactor - worldOffset;
    public static Vector2 ScreenDirection(Vector2 worldDelta, float headingFromNorth)
    {
        if (worldDelta.LengthSquared() < .0001f) return new(0, -1);
        var relative = MathF.Atan2(worldDelta.X, -worldDelta.Y) - headingFromNorth;
        return new(MathF.Sin(relative), -MathF.Cos(relative));
    }
    public static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);

    public static Vector3 SteeringPoint(IReadOnlyList<Vector3> path, RouteProgress progress, Vector3 player)
    {
        if (progress.DistanceFromRoute > 2 && Vector3.DistanceSquared(progress.Foot, player) > .25f)
            return progress.Foot;
        for (var i = progress.Segment + 1; i < path.Count; i++)
            if (Vector3.DistanceSquared(path[i], player) > .25f) return path[i];
        return path[^1];
    }

    public static Vector2? MinimapRouteDirection(IReadOnlyList<Vector3> path, RouteProgress progress,
        Vector3 player, Vector2 projection)
    {
        if (path.Count < 2) return null;
        var origin = progress.DistanceFromRoute > 2 ? player : progress.Foot;
        var next = SteeringPoint(path, progress, origin);
        var screen = Project(Xz(next - origin), projection);
        return float.IsFinite(screen.X) && float.IsFinite(screen.Y) && screen.LengthSquared() > .0001f
            ? Vector2.Normalize(screen) : null;
    }

    // A short probe along the next segment remains near the player even when
    // the waypoint is behind the camera. Its projected screen tangent agrees
    // with the rendered route and includes the camera's actual pitch and yaw.
    public static Vector2? ProjectedDirection(Vector3 player, Vector3 steeringPoint,
        Func<Vector3, Vector2?> project)
    {
        var delta = steeringPoint - player;
        if (!Finite(delta) || delta.LengthSquared() < .0001f) return null;
        var unit = Vector3.Normalize(delta);
        foreach (var height in new[] { .15f, 1f })
        {
            var origin = player + Vector3.UnitY * height;
            var start = project(origin);
            if (!start.HasValue) continue;
            foreach (var length in new[] { .5f, .1f })
            {
                var end = project(origin + unit * length);
                if (!end.HasValue) continue;
                var screenDelta = end.Value - start.Value;
                if (float.IsFinite(screenDelta.X) && float.IsFinite(screenDelta.Y) && screenDelta.LengthSquared() > .01f)
                    return Vector2.Normalize(screenDelta);
            }
        }
        return null;
    }

    public static Vector2? ViewDirection(Vector3 delta, Matrix4x4 view, float projectionM34)
    {
        if (MathF.Abs(projectionM34) < .0001f) return null;
        var local = Vector3.TransformNormal(delta, view);
        var result = new Vector2(local.X, -local.Z * MathF.Sign(projectionM34));
        return float.IsFinite(result.X) && float.IsFinite(result.Y) && result.LengthSquared() > .0001f
            ? Vector2.Normalize(result) : null;
    }

    // Work in 3D when locating the active segment: bridges and upper floors must not
    // select a different floor just because it shares the same map coordinates.
    public static RouteProgress Progress(IReadOnlyList<Vector3> path, Vector3 player)
    {
        if (path.Count < 2) return new(player, 0, 0, float.PositiveInfinity);
        var best = float.MaxValue;
        var foot = path[0];
        var segment = 0;
        for (var i = 0; i < path.Count - 1; ++i)
        {
            var delta = path[i + 1] - path[i];
            var length = delta.LengthSquared();
            var t = length > 0.00001f ? Math.Clamp(Vector3.Dot(player - path[i], delta) / length, 0, 1) : 0;
            var point = path[i] + delta * t;
            var distance = Vector3.DistanceSquared(player, point);
            if (distance >= best) continue;
            best = distance;
            foot = point;
            segment = i;
        }
        var remaining = Vector3.Distance(foot, path[segment + 1]);
        for (var i = segment + 1; i < path.Count - 1; ++i)
            remaining += Vector3.Distance(path[i], path[i + 1]);
        return new(foot, segment, remaining, MathF.Sqrt(best));
    }

    public static Vector3 Ahead(IReadOnlyList<Vector3> path, RouteProgress progress, float distance)
    {
        var from = progress.Foot;
        for (var i = progress.Segment + 1; i < path.Count; ++i)
        {
            var length = Vector3.Distance(from, path[i]);
            if (length >= distance && length > 0.001f)
                return Vector3.Lerp(from, path[i], distance / length);
            distance -= length;
            from = path[i];
        }
        return path[^1];
    }

    // Clip mathematically to the round minimap, not just a rectangular ImGui clip.
    public static bool ClipCircle(ref Vector2 a, ref Vector2 b, Vector2 center, float radius)
    {
        var d = b - a;
        var p = a - center;
        var aa = d.LengthSquared();
        if (aa < 1e-8f) return p.LengthSquared() <= radius * radius;
        var bb = 2 * Vector2.Dot(p, d);
        var cc = p.LengthSquared() - radius * radius;
        var discriminant = bb * bb - 4 * aa * cc;
        if (discriminant < 0) return false;
        var root = MathF.Sqrt(discriminant);
        var lo = Math.Max(0, (-bb - root) / (2 * aa));
        var hi = Math.Min(1, (-bb + root) / (2 * aa));
        if (lo > hi) return false;
        b = a + d * hi;
        a += d * lo;
        return true;
    }

    // Fit a rotation + uniform scale around the player's native map pin.
    // Reject inconsistent observations (edge-clamped map icons, wrong coordinate
    // convention, another floor). Never draw a guessed route on the native map.
    public static bool FitProjection(Vector2 playerWorld, Vector2 playerScreen,
        IReadOnlyList<(Vector2 World, Vector2 Screen)> references, out Vector2 complex)
    {
        complex = default;
        if (references.Count < 2) return false;
        double real = 0, imaginary = 0, denominator = 0;
        foreach (var item in references)
        {
            var w = item.World - playerWorld;
            var s = item.Screen - playerScreen;
            if (w.LengthSquared() < 4) continue;
            real += Vector2.Dot(w, s);
            imaginary += w.X * s.Y - w.Y * s.X;
            denominator += w.LengthSquared();
        }
        if (denominator < 8) return false;
        complex = new((float)(real / denominator), (float)(imaginary / denominator));
        var scale = complex.Length();
        if (!float.IsFinite(scale) || scale < 0.05f || scale > 30) return false;
        var inliers = 0;
        foreach (var item in references)
        {
            var predicted = Project(item.World - playerWorld, complex) + playerScreen;
            if (Vector2.Distance(predicted, item.Screen) < 5) ++inliers;
        }
        return inliers >= 2 && inliers >= references.Count * 0.75f;
    }

    public static Vector2 Project(Vector2 delta, Vector2 complex) =>
        new(delta.X * complex.X - delta.Y * complex.Y, delta.X * complex.Y + delta.Y * complex.X);
}
