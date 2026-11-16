using System.Numerics;

namespace RutaEorzea;

public sealed record ZoneExit(uint From, uint FromMap, uint To, uint ToMap, Vector2 Position, string Name);

public sealed class ZoneGraph(IEnumerable<ZoneExit> exits)
{
    private readonly List<ZoneExit> edges = exits.Where(x => x.From != x.To).ToList();
    public IEnumerable<ZoneExit> Adjacent(uint territory) => edges.Where(x => x.From == territory).DistinctBy(x => x.To);

    public ZoneExit? NextExit(uint from, uint map, uint destination, Vector2 position)
    {
        if (from == destination) return null;
        // Reverse breadth-first search: minimize zone crossings, then choose
        // the nearest available first exit on the player's current map.
        var distances = new Dictionary<uint, int> { [destination] = 0 };
        var queue = new Queue<uint>();
        queue.Enqueue(destination);
        while (queue.TryDequeue(out var zone))
            foreach (var edge in edges.Where(x => x.To == zone))
                if (distances.TryAdd(edge.From, distances[zone] + 1)) queue.Enqueue(edge.From);
        return edges.Where(x => x.From == from && x.FromMap == map && distances.ContainsKey(x.To))
            .OrderBy(x => distances[x.To])
            .ThenBy(x => Vector2.DistanceSquared(position, x.Position))
            .FirstOrDefault();
    }
}
