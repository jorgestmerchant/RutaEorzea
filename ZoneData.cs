using System.Numerics;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SheetMap = Lumina.Excel.Sheets.Map;

namespace RutaEorzea;

internal sealed class ZoneData
{
    public ZoneGraph Graph { get; }
    public ZoneData(IDataManager data)
    {
        var maps = data.GetExcelSheet<SheetMap>();
        var markers = data.GetSubrowExcelSheet<MapMarker>();
        var exits = new List<ZoneExit>();
        foreach (var source in maps)
        {
            if (source.SizeFactor == 0 || source.TerritoryType.RowId == 0 || source.MapMarkerRange == 0) continue;
            if (!markers.TryGetRow(source.MapMarkerRange, out var rows)) continue;
            foreach (var marker in rows)
            {
                // Only ordinary, visible walking exits. Unmarked connections
                // can require an NPC, ferry or unlock and are not walking links.
                if (marker.DataType != 1 || marker.Icon != 60441 || marker.DataKey.RowId == 0) continue;
                if (!maps.TryGetRow(marker.DataKey.RowId, out var destination) || destination.TerritoryType.RowId == 0) continue;
                var factor = source.SizeFactor / 100f;
                var position = new Vector2((marker.X - 1024) / factor - source.OffsetX,
                    (marker.Y - 1024) / factor - source.OffsetY);
                var name = SpanishName(destination.PlaceName.Value.Name.ToString());
                exits.Add(new(source.TerritoryType.RowId, source.RowId, destination.TerritoryType.RowId,
                    destination.RowId, position, name));
            }
        }
        Graph = new(exits);
    }

    private static string SpanishName(string name) => name switch
    {
        "New Gridania" => "Nueva Gridania", "Old Gridania" => "Vieja Gridania",
        "Central Shroud" => "Bosque Central", "East Shroud" => "Bosque del Este",
        "South Shroud" => "Bosque del Sur", "North Shroud" => "Bosque del Norte",
        _ => name
    };
}
