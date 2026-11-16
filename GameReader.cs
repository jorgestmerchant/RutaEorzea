using System.Numerics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace RutaEorzea;

public sealed record MinimapSnapshot(Vector2 Center, float Radius,
    Vector2 PlayerPin, Vector2 Projection, int References, bool Calibrated, Vector2 WorldOrigin);
public sealed record MinimapBounds(Vector2 Center, float Radius);
public sealed record AreaMapSnapshot(Vector2 Min, Vector2 Max, Vector2 Center, Vector2 Offset,
    Vector2 WorldOffset, float SizeFactor, Vector2 Scale, uint Map, uint Territory)
{
    public Vector2 Project(Vector3 world) => RouteMath.AreaMapPoint(RouteMath.Xz(world), Center,
        Offset, WorldOffset, SizeFactor, Scale);
    public Vector2 Unproject(Vector2 screen) => RouteMath.AreaMapWorld(screen, Center,
        Offset, WorldOffset, SizeFactor, Scale);
    public bool Contains(Vector2 screen) => screen.X >= Min.X && screen.Y >= Min.Y && screen.X < Max.X && screen.Y < Max.Y;
}

internal static unsafe class GameReader
{
    public static AreaMapSnapshot? ReadAreaMap(AddonAreaMap* addon, uint currentMap)
    {
        var agent = AgentMap.Instance();
        if (addon == null || !addon->IsVisible || addon->ComponentMap == null || agent == null ||
            agent->SelectedMapId == 0 || agent->SelectedTerritoryId == 0 ||
            (currentMap != 0 && agent->SelectedMapId != currentMap)) return null;
        var owner = addon->ComponentMap->OwnerNode;
        if (owner == null || !Visible(&owner->AtkResNode)) return null;
        var node = &owner->AtkResNode;
        var uiScale = NodeScale(node);
        var a = addon->AreaMap;
        var factor = agent->SelectedMapSizeFactorFloat;
        if (uiScale.X <= 0 || uiScale.Y <= 0 || factor <= 0 || !float.IsFinite(factor) || a.MapScale <= 0 ||
            !float.IsFinite(a.MapScale) || node->Width < 20 || node->Height < 20) return null;
        var min = new Vector2(node->ScreenX, node->ScreenY);
        // The map renderer centers its coordinates in half the complete map
        // dimensions, relative to its component. The frame is slightly smaller.
        var center = min + new Vector2(a.Width, a.Height) * uiScale;
        return new(min + new Vector2(2), min + new Vector2(node->Width, node->Height) * uiScale - new Vector2(2),
            center, new(a.MapOffsetX, a.MapOffsetY), new(agent->SelectedOffsetX, agent->SelectedOffsetY),
            factor, uiScale * a.MapScale, agent->SelectedMapId, agent->SelectedTerritoryId);
    }

    public static Destination MapDestination(AreaMapSnapshot map, Vector2 screen)
    {
        var world = map.Unproject(screen);
        var result = new Destination(0, 0, map.Territory, map.Map,
            new(world.X, 0, world.Y), 0, "Punto personalizado", true, false);
        var best = 18f * 18f;
        void Consider(Vector3 position, string label, bool altitude)
        {
            var distance = Vector2.DistanceSquared(map.Project(position), screen);
            if (distance >= best || !RouteMath.Finite(position)) return;
            best = distance;
            result = result with { Position = position, Label = string.IsNullOrWhiteSpace(label) ? "Icono del mapa" : label,
                HasAltitude = altitude };
        }
        var agent = AgentMap.Instance();
        if (agent == null) return result;
        // Static native map markers use sheet-scaled, centered coordinates (1/16 pixel).
        foreach (var marker in agent->MapMarkers[..Math.Min((int)agent->MapMarkerCount, agent->MapMarkers.Length)])
        {
            var m = marker.MapMarker;
            if (m.IconId == 0) continue;
            var point = new Vector2(m.X / 16f, m.Y / 16f) / map.SizeFactor - map.WorldOffset;
            Consider(new(point.X, 0, point.Y), "Icono del mapa", false);
        }
        // Quest/event markers supply actual world height and translated labels.
        void ConsiderQuest(FFXIVClientStructs.FFXIV.Client.Game.UI.MarkerInfo quest)
        {
            if (quest.ObjectiveId == 0 || quest.MarkerData.LongCount > 64) return;
            foreach (var m in quest.MarkerData)
                if (m.MapId == map.Map && m.TerritoryTypeId == map.Territory)
                    Consider(m.Position, quest.Label.ToString(), true);
        }
        var gameMap = GameMap.Instance();
        if (gameMap != null)
        {
            foreach (var quest in gameMap->QuestMarkers) ConsiderQuest(quest);
            var count = 0;
            foreach (var quest in gameMap->UnacceptedQuestMarkers)
            { if (++count > 512) break; ConsiderQuest(quest); }
        }
        var tooltip = agent->TooltipString.ToString();
        if (best < 18f * 18f && !string.IsNullOrWhiteSpace(tooltip)) result = result with { Label = tooltip };
        return result;
    }

    public static bool MapOwnsPoint(AddonAreaMap* addon, Vector2 screen)
    {
        if (addon == null) return false;
        var stage = AtkStage.Instance();
        if (stage == null || stage->RaptureAtkUnitManager == null) return false;
        var collision = new AddonCollision();
        stage->RaptureAtkUnitManager->GetAddonCollision(&collision, (short)screen.X, (short)screen.Y);
        return collision.UnitBase == &addon->AtkUnitBase;
    }
    internal static object? InspectAreaMap(AddonAreaMap* addon)
    {
        if (addon == null || addon->ComponentMap == null) return null;
        var component = addon->ComponentMap;
        object? Node(AtkResNode* n) => n == null ? null : new { NodeId = n->NodeId, ScreenX = n->ScreenX, ScreenY = n->ScreenY,
            X = n->X, Y = n->Y, Width = n->Width, Height = n->Height, ScaleX = n->ScaleX, ScaleY = n->ScaleY,
            OriginX = n->OriginX, OriginY = n->OriginY,
            Center = NodeCenter(n), Scale = NodeScale(n), Visible = Visible(n) };
        var a = addon->AreaMap;
        var clips = new List<object>();
        for (var i = 0; i < component->UldManager.NodeListCount && i < 2048; i++)
        {
            var node = component->UldManager.NodeList[i];
            if (node != null && (int)node->Type == 10) clips.Add(Node(node)!);
        }
        return new { Native = new { a.X, a.Y, a.Width, a.Height, a.MapScale, a.MapOffsetX, a.MapOffsetY,
            a.MapCenterX, a.MapCenterY, a.PlayerMarkerX, a.PlayerMarkerY, a.PlayerPositionX, a.PlayerPositionY,
            a.MarkerPositionScaling }, Component = new { MapScale = component->MapScale,
            MapOffsetX = component->MapOffsetX, MapOffsetY = component->MapOffsetY,
            MapWidth = component->MapWidth, MapHeight = component->MapHeight },
            Root = Node(addon->RootNode), Owner = Node(component->OwnerNode == null ? null : &component->OwnerNode->AtkResNode),
            Image = Node(component->BaseMapImage == null ? null : &component->BaseMapImage->AtkResNode),
            Frame = Node(component->MapFrameImage == null ? null : &component->MapFrameImage->AtkResNode),
            Player = Node(a.PlayerPin == null ? null : &a.PlayerPin->AtkResNode), Clips = clips };
    }

    private static Vector2 NodeScale(AtkResNode* node)
    {
        var scale = Vector2.One;
        for (var depth = 0; node != null && depth++ < 32; node = node->ParentNode)
            scale *= new Vector2(node->ScaleX, node->ScaleY);
        return scale;
    }
    public static Vector2? CameraDirection(Vector3 delta)
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance();
        if (manager == null || manager->Camera == null) return null;
        var scene = &manager->Camera->SceneCamera;
        if (scene->RenderCamera == null) return null;
        return RouteMath.ViewDirection(delta, scene->ViewMatrix, scene->RenderCamera->ProjectionMatrix.M34);
    }
    public static object? InspectCamera()
    {
        var manager = FFXIVClientStructs.FFXIV.Client.Game.Control.CameraManager.Instance();
        if (manager == null || manager->Camera == null) return null;
        var scene = &manager->Camera->SceneCamera;
        if (scene->RenderCamera == null) return null;
        return new { View = (Matrix4x4)scene->ViewMatrix, Projection = (Matrix4x4)scene->RenderCamera->ProjectionMatrix };
    }
    public static object InspectMinimap(AddonNaviMap* a)
    {
        var markers = new List<object>();
        if (a == null) return new { Exists = false };
        foreach (var m in a->NaviMap.NaviMapMarkers)
            if (m.IconId != 0 && m.ComponentNode != null)
                markers.Add(new { m.IconId, m.X, m.Y, Center = NodeCenter(&m.ComponentNode->AtkResNode),
                    Visible = Visible(&m.ComponentNode->AtkResNode) });
        return new { Exists = true, IsVisible = a->IsVisible,
            Scaling = a->NaviMap.MarkerPositionScaling, NorthLocked = a->NaviMap.NorthLockedUp,
            CompassRotation = a->NaviMap.CompassFrame == null ? 0 : a->NaviMap.CompassFrame->Rotation,
            CompassScale = a->NaviMap.CompassFrame == null ? Vector2.One : NodeScale(a->NaviMap.CompassFrame),
            NativePlayer = new Vector2(a->NaviMap.PlayerPositionX, a->NaviMap.PlayerPositionY),
            Factor = AgentMap.Instance() == null ? 0 : AgentMap.Instance()->CurrentMapSizeFactorFloat,
            Mask = a->Mask == null ? null : (object)new { Width = a->Mask->Width, Height = a->Mask->Height, Center = NodeCenter(&a->Mask->AtkResNode) },
            Player = a->NaviMap.PlayerPin == null ? null : (object)NodeCenter(&a->NaviMap.PlayerPin->AtkResNode),
            Markers = markers };
    }
    public static object Inspect()
    {
        var a = AgentMap.Instance();
        var m = GameMap.Instance();
        var quests = new List<object>();
        var links = new List<object>();
        if (a != null) foreach (var l in a->MiniMapQuestLinkContainer.Markers)
            if (l.Valid != 0) links.Add(new { l.QuestId, l.LevelId, l.SourceMapId, l.TargetMapId });
        if (m != null) foreach (ref var q in m->QuestMarkers)
            if (q.ObjectiveId != 0 && q.MarkerData.LongCount <= 64)
                quests.Add(new { q.ObjectiveId, Label = q.Label.ToString(), Count = q.MarkerData.LongCount });
        return new { Title = a == null ? "" : a->CurrentOpenMapInfo.TitleString.ToString(), Type = a == null ? -1 : (int)a->CurrentOpenMapInfo.Type,
            Quest = a == null ? 0 : a->CurrentOpenMapInfo.QuestId,
            Map = a == null ? 0 : a->SelectedMapId, Quests = quests, Links = links };
    }
    public static (uint Quest, uint Map) SelectedQuest()
    {
        var agent = AgentMap.Instance();
        if (agent == null || agent->CurrentOpenMapInfo.Type != MapType.QuestLog) return default;
        var id = agent->CurrentOpenMapInfo.QuestId & 0xFFFF;
        if (id == 0)
        {
            var title = agent->CurrentOpenMapInfo.TitleString.ToString();
            var map = GameMap.Instance();
            if (map != null && !string.IsNullOrWhiteSpace(title))
                foreach (ref var quest in map->QuestMarkers)
                    if (string.Equals(quest.Label.ToString(), title, StringComparison.Ordinal))
                    { id = quest.ObjectiveId & 0xFFFF; break; }
        }
        return (id, agent->SelectedMapId);
    }

    public static List<Destination> QuestDestinations(uint questId)
    {
        var map = GameMap.Instance();
        var result = new List<Destination>();
        if (map == null || questId == 0) return result;
        foreach (ref var quest in map->QuestMarkers)
        {
            if ((quest.ObjectiveId & 0xFFFF) != questId) continue;
            if (quest.MarkerData.LongCount > 64) continue;
            var label = quest.Label.ToString();
            foreach (var marker in quest.MarkerData)
            {
                if (marker.TerritoryTypeId == 0 || marker.MapId == 0 || !RouteMath.Finite(marker.Position)) continue;
                var candidate = new Destination(questId, marker.LevelId, marker.TerritoryTypeId,
                    marker.MapId, marker.Position, marker.Radius, label);
                if (result.Any(x => x.Territory == candidate.Territory && x.Map == candidate.Map &&
                                    Vector3.DistanceSquared(x.Position, candidate.Position) < 0.25f)) continue;
                result.Add(candidate);
            }
        }
        if (result.Count == 0)
        {
            var count = 0;
            foreach (var quest in map->UnacceptedQuestMarkers)
            {
                if (++count > 512) break;
                if ((quest.ObjectiveId & 0xFFFF) != questId || quest.MarkerData.LongCount > 64) continue;
                foreach (var marker in quest.MarkerData)
                    if (marker.TerritoryTypeId != 0 && marker.MapId != 0 && RouteMath.Finite(marker.Position))
                        result.Add(new(questId, marker.LevelId, marker.TerritoryTypeId, marker.MapId,
                            marker.Position, marker.Radius, quest.Label.ToString()));
            }
        }
        return result;
    }

    public static Destination? Flag()
    {
        var agent = AgentMap.Instance();
        if (agent == null || agent->FlagMarkerCount == 0) return null;
        var flag = agent->FlagMapMarkers[0];
        if (!float.IsFinite(flag.XFloat) || !float.IsFinite(flag.YFloat)) return null;
        return new(0, 0, flag.TerritoryId, flag.MapId,
            new(flag.XFloat, 0, flag.YFloat), 0, "Bandera del mapa");
    }

    internal static bool Visible(AtkResNode* node)
    {
        for (var depth = 0; node != null && depth < 32; depth++, node = node->ParentNode)
            if ((node->NodeFlags & NodeFlags.Visible) == 0) return false;
        return true;
    }

    private static Vector2 NodeCenter(AtkResNode* node)
    {
        var scale = Vector2.One;
        var depth = 0;
        for (var parent = node; parent != null && depth++ < 32; parent = parent->ParentNode)
            scale *= new Vector2(parent->ScaleX, parent->ScaleY);
        return new(node->ScreenX + node->Width * scale.X * 0.5f,
            node->ScreenY + node->Height * scale.Y * 0.5f);
    }

    public static MinimapBounds? ReadMinimapBounds(AddonNaviMap* addon)
    {
        if (addon == null || !addon->IsVisible || addon->Mask == null)
            return null;
        var mask = &addon->Mask->AtkResNode;
        if (!Visible(mask)) return null;
        var center = NodeCenter(mask);
        var maskScale = 1f;
        var depth = 0;
        for (var node = mask; node != null && depth++ < 32; node = node->ParentNode) maskScale *= MathF.Abs(node->ScaleX);
        var radius = Math.Min(mask->Width, mask->Height) * maskScale * 0.5f - 4;
        if (radius < 20 || radius > 800) return null;
        return new(center, radius);
    }

    public static MinimapSnapshot? ReadMinimap(AddonNaviMap* addon, Vector3 player)
    {
        var bounds = ReadMinimapBounds(addon);
        if (bounds == null || addon->NaviMap.PlayerPin == null) return null;
        var center = bounds.Center;
        var radius = bounds.Radius;
        // The player icon rotates about its pivot; its unrotated node bounds do
        // not describe its visual center. The native map is centered on player.
        var playerPin = center;
        var agent = AgentMap.Instance();
        if (agent == null) return null;
        var native = &addon->NaviMap;
        if (native->CompassFrame == null && !native->NorthLockedUp) return null;
        var rotation = native->CompassFrame == null ? 0 : native->CompassFrame->Rotation;
        var factor = agent->CurrentMapSizeFactorFloat;
        var scale = NodeScale(&addon->Mask->AtkResNode).X;
        var projection = RouteMath.MinimapProjection(native->MarkerPositionScaling, factor, scale, rotation);
        if (!projection.HasValue) return null;
        // Use the renderer's own player position to avoid frame-order jitter
        // between the moving character and the already laid-out map icons.
        var origin = new Vector2(native->PlayerPositionX, native->PlayerPositionY) / factor
            - new Vector2(agent->CurrentOffsetX, agent->CurrentOffsetY);
        if (!float.IsFinite(origin.X) || !float.IsFinite(origin.Y) ||
            Vector2.DistanceSquared(origin, RouteMath.Xz(player)) > 25) origin = RouteMath.Xz(player);
        return new(center, radius, playerPin, projection.Value, 0, true, origin);
    }
}
