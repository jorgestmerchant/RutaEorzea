using System.Numerics;

namespace RutaEorzea;

public sealed record Destination(uint QuestId, uint LevelId, uint Territory, uint Map,
    Vector3 Position, float Radius, string Label, bool Custom = false, bool HasAltitude = true);

public static class QuestTargets
{
    public static Destination? Choose(IReadOnlyList<Destination> choices, uint territory, Vector3 player,
        uint preferredLevel = 0)
    {
        if (preferredLevel != 0 && choices.FirstOrDefault(x => x.LevelId == preferredLevel) is { } preferred)
            return preferred;
        return choices.Where(x => x.Territory == territory)
            .OrderBy(x => Vector3.DistanceSquared(player, x.Position)).FirstOrDefault() ?? choices.FirstOrDefault();
    }

    public static bool StillAvailable(Destination destination, IReadOnlyList<Destination> choices) =>
        choices.Any(x => x.LevelId == destination.LevelId && x.Territory == destination.Territory &&
            x.Map == destination.Map && Vector3.DistanceSquared(x.Position, destination.Position) < .25f);
}
