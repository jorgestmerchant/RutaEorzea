using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace RutaEorzea;

internal sealed class RewardCard(QuestRewards rewards, ITextureProvider textures)
{
    private static readonly Vector4 Gold = new(.94f, .83f, .54f, 1);
    private static readonly Vector4 Muted = new(.72f, .75f, .72f, 1);

    public void Draw(TrackerRow quest, int playerLevel, bool selected, string routeStatus)
    {
        var info = rewards.Read(quest.QuestId);
        if (info == null) return;
        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(.045f, .065f, .055f, .98f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.64f, .54f, .32f, 1));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.93f, .94f, .91f, 1));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(12));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 7);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1);
        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 370);
        ImGui.TextColored(Gold, quest.Label);
        ImGui.TextColored(Muted, $"Nivel {info.Level} · {(quest.MainScenario ? "Historia principal" : "Recompensas de misión")}");
        ImGui.Separator();
        if (info.BaseExperience.HasValue)
        {
            ImGui.TextColored(new Vector4(.4f, 1, .55f, 1), $"EXP base aprox.: {info.BaseExperience.Value:N0}");
            if (info.BaseExperience.Value > 0 && !info.ExperienceScales &&
                RewardMath.LevelPercentage(info.BaseExperience.Value, rewards.ExpToNext(playerLevel)) is { } percentage)
                ImGui.TextColored(Muted, $"Comparación base: ≈ {percentage:0.#} % de un nivel actual");
        }
        else ImGui.TextColored(Muted, "EXP: consulta el diario");
        ImGui.TextColored(Gold, $"Gil: {info.Gil:N0}");
        if (info.Guaranteed.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(Gold, "Recibirás");
            foreach (var item in info.Guaranteed) DrawItem(item);
        }
        if (info.Choices.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(Gold, "Elige UNA recompensa");
            foreach (var item in info.Choices) DrawItem(item);
        }
        if (info.Unlocks.Count > 0)
        {
            ImGui.Separator();
            ImGui.TextColored(Gold, "Desbloqueos");
            foreach (var unlock in info.Unlocks) ImGui.TextUnformatted($"• {unlock}");
        }
        ImGui.Separator();
        ImGui.TextColored(Muted, info.ExperienceScales
            ? "La EXP puede escalar con tu nivel. Confirma la cifra en el diario."
            : "EXP base sin bonificaciones; el importe final puede variar.");
        if (info.HasSpecialRewards)
            ImGui.TextColored(Muted, "Hay recompensas especiales o según tu clase: consulta también el diario.");
        ImGui.TextColored(Muted, "Nombres de objetos según el idioma del cliente.");
        ImGui.TextColored(Muted, "Casilla de misión: seguir o detener la ruta.");
        if (selected) ImGui.TextWrapped(routeStatus);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
        ImGui.PopStyleVar(3);
        ImGui.PopStyleColor(3);
    }

    private void DrawItem(RewardItem item)
    {
        var icon = textures.GetFromGameIcon(new GameIconLookup(item.Icon, item.HighQuality)).GetWrapOrDefault();
        if (icon != null)
        {
            ImGui.Image(icon.Handle, new Vector2(23));
            ImGui.SameLine();
        }
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted($"{item.Name} ×{item.Count}{(item.HighQuality ? " · HQ" : "")}");
    }
}
