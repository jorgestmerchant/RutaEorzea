using Dalamud.Configuration;
using System.Numerics;

namespace RutaEorzea;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool FollowQuestClicks { get; set; } = true;
    public bool ShowTrackerButtons { get; set; } = true;
    public bool ShowQuestLevels { get; set; } = true;
    public bool ShowQuestRewards { get; set; } = true;
    public bool MapRightClick { get; set; } = true;
    public bool ShowSettingsButton { get; set; } = true;
    public float SettingsButtonAngle { get; set; } = 135;
    public uint SavedQuestId { get; set; }
    public uint SavedLevelId { get; set; }
    public bool FollowMapFlag { get; set; } = true;
    public bool ShowMinimap { get; set; } = true;
    public bool ShowAreaMap { get; set; } = true;
    public bool ShowWorldPath { get; set; } = false;
    public bool ShowArrow { get; set; } = true;
    public bool CameraRelativeArrow { get; set; } = true;
    public bool AlignArrowWithMinimap { get; set; } = true;
    public float LineWidth { get; set; } = 3;
    public Vector4 RouteColor { get; set; } = new(0.2f, 1f, 0.7f, 0.95f);
}
