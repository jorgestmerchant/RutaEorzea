using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using GameMap = FFXIVClientStructs.FFXIV.Client.Game.UI.Map;

namespace RutaEorzea;

internal sealed record TrackerRow(uint QuestId, string Label, Vector2 Position, float Height, float Right,
    bool MainScenario, Vector2 TextPosition, float TextWidth);

internal static unsafe class TrackerReader
{
    public static object Inspect(AtkUnitBase* addon)
    {
        var scenario = AgentScenarioTree.Instance();
        var ids = scenario == null || scenario->Data == null ? Array.Empty<ushort>() : scenario->Data->MainScenarioQuestIds.ToArray();
        var nodes = new List<object>();
        var visited = new HashSet<nint>();
        void Visit(AtkUldManager* manager, int depth)
        {
            if (manager == null || depth > 12 || manager->NodeListCount > 2048) return;
            for (var i = 0; i < manager->NodeListCount; i++)
            {
                var n = manager->NodeList[i];
                if (n == null || !visited.Add((nint)n) || visited.Count > 4096) continue;
                if (n->Type == NodeType.Text)
                {
                    var t = (AtkTextNode*)n;
                    var collision = new AddonCollision();
                    var stage = AtkStage.Instance();
                    if (stage != null && stage->RaptureAtkUnitManager != null)
                        stage->RaptureAtkUnitManager->GetAddonCollision(&collision,(short)(n->ScreenX+n->Width/2),(short)(n->ScreenY+n->Height/2));
                    if (t->NodeText.BufUsed < 4096) nodes.Add(new { Depth = depth, Collision = collision.UnitBase == null ? "" : collision.UnitBase->NameString,
                        NodeId = n->NodeId, ScreenX = n->ScreenX, ScreenY = n->ScreenY, Width = n->Width, Height = n->Height,
                        Visible = GameReader.Visible(n), Text = SeString.Parse(t->NodeText.AsSpan()).TextValue });
                }
                else if ((ushort)n->Type >= 1000 && ((AtkComponentNode*)n)->Component != null)
                    Visit(&((AtkComponentNode*)n)->Component->UldManager, depth + 1);
            }
        }
        if (addon != null) Visit(&addon->UldManager, 0);
        return new { Exists = addon != null, Ids = ids, Path = scenario == null || scenario->Data == null ? -1 : scenario->Data->MSQPathIndex, Nodes = nodes };
    }
    // Read the game's current title positions instead of assuming a fixed HUD
    // location or the order of accepted quests. Never change native UI nodes.
    public static List<TrackerRow> Read(AtkUnitBase* addon, bool mainScenario = false)
    {
        var result = new List<TrackerRow>();
        var map = GameMap.Instance();
        if (addon == null || !addon->IsVisible || map == null) return result;
        var quests = new Dictionary<string, (uint Id, string Label)>();
        uint mainQuest = 0;
        if (mainScenario)
        {
            var scenario = AgentScenarioTree.Instance();
            if (scenario == null || scenario->Data == null || scenario->Data->MSQPathIndex >= 3) return result;
            mainQuest = scenario->Data->MainScenarioQuestIds[scenario->Data->MSQPathIndex];
            if (mainQuest == 0) return result;
        }
        foreach (ref var quest in map->QuestMarkers)
        {
            if (quest.ObjectiveId == 0 || (mainScenario && (quest.ObjectiveId & 0xFFFF) != mainQuest)) continue;
            var label = quest.Label.ToString();
            var key = TrackerTitles.Normalize(label);
            if (key.Length > 0) quests.TryAdd(key, (quest.ObjectiveId & 0xFFFF, label));
        }
        if (mainScenario)
        {
            var count = 0;
            foreach (var quest in map->UnacceptedQuestMarkers)
            {
                if (++count > 512) break;
                if ((quest.ObjectiveId & 0xFFFF) != mainQuest) continue;
                var label = quest.Label.ToString();
                var key = TrackerTitles.Normalize(label);
                if (key.Length > 0) quests.TryAdd(key, (mainQuest, label));
            }
        }
        var visited = new HashSet<nint>();
        void VisitManager(AtkUldManager* manager, int depth)
        {
            if (depth > 12 || manager == null || manager->NodeListCount > 2048) return;
            for (var i = 0; i < manager->NodeListCount; i++)
            {
                var node = manager->NodeList[i];
                if (node == null || visited.Count >= 4096 || !visited.Add((nint)node) || !GameReader.Visible(node)) continue;
                if (node->Type == NodeType.Text)
                {
                    var text = (AtkTextNode*)node;
                    if (text->NodeText.BufUsed > 4096) continue;
                    var plain = SeString.Parse(text->NodeText.AsSpan()).TextValue;
                    var key = TrackerTitles.Normalize(plain);
                    (uint Id, string Label) quest;
                    if (mainScenario)
                    {
                        // Read the title node; identity comes from the native agent.
                        if (node->NodeId != 6 || string.IsNullOrWhiteSpace(plain)) continue;
                        quest = (mainQuest, plain);
                    }
                    else if (!quests.TryGetValue(key, out quest)) continue;
                    if (result.Any(x => x.QuestId == quest.Id)) continue;
                    var scale = Vector2.One;
                    var steps = 0;
                    for (var p = node; p != null && steps++ < 32; p = p->ParentNode)
                        scale *= new Vector2(p->ScaleX, p->ScaleY);
                    var position = new Vector2(node->ScreenX, node->ScreenY);
                    if (!float.IsFinite(position.X) || !float.IsFinite(position.Y)) continue;
                    var right = position.X + MathF.Abs(node->Width * scale.X);
                    var height = MathF.Abs(node->Height * scale.Y);
                    ushort drawWidth = 0, drawHeight = 0;
                    text->GetTextDrawSize(&drawWidth, &drawHeight);
                    var textWidth = MathF.Min(MathF.Abs(drawWidth * scale.X), right - position.X);
                    var alignment = (int)text->AlignmentType % 3;
                    var textX = alignment == 2 ? right - textWidth : alignment == 1 ? position.X + (right - position.X - textWidth) / 2 : position.X;
                    var stage = AtkStage.Instance();
                    if (stage != null && stage->RaptureAtkUnitManager != null)
                    {
                        var collision = new AddonCollision();
                        stage->RaptureAtkUnitManager->GetAddonCollision(&collision,
                            (short)Math.Clamp((position.X + right) / 2, 0, short.MaxValue),
                            (short)Math.Clamp(position.Y + height / 2, 0, short.MaxValue));
                        if (collision.UnitBase != null && collision.UnitBase != addon) continue;
                    }
                    result.Add(new(quest.Id, quest.Label, position, height, right, mainScenario,
                        new(textX, position.Y), textWidth));
                }
                else if ((ushort)node->Type >= 1000)
                {
                    var component = ((AtkComponentNode*)node)->Component;
                    if (component != null) VisitManager(&component->UldManager, depth + 1);
                }
            }
        }
        VisitManager(&addon->UldManager, 0);
        return result.OrderBy(x => x.Position.Y).ToList();
    }
}
