using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.GameFonts;
using Dalamud.Interface.ManagedFontAtlas;

namespace RutaEorzea;

internal sealed class NavigationHud : IDisposable
{
    private readonly IFontHandle body;
    private readonly IFontHandle number;
    private const float Width = 302;
    private const float Height = 104;
    private float displayedAngle;
    private bool angleInitialized;

    public NavigationHud(IFontAtlas atlas)
    {
        body = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Axis, 16));
        number = atlas.NewGameFontHandle(new GameFontStyle(GameFontFamily.Jupiter, 26));
    }

    public void Draw(string title, string caption, string value, Vector2? direction, bool complete, string tooltip)
    {
        ImGui.SetNextWindowPos(new Vector2(750, 130), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(Width, Height), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0);
        var flags = ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoBackground |
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoFocusOnAppearing;
        if (ImGui.Begin("Guía de ruta###RutaEorzeaArrow", flags))
        {
            ImGui.InvisibleButton("##moverGuia", new Vector2(Width, Height));
            var hovered = ImGui.IsItemHovered();
            if (ImGui.IsItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left, 4))
                ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
            var origin = ImGui.GetWindowPos();
            var draw = ImGui.GetWindowDrawList();
            var center = origin + new Vector2(46, 52);
            var cardMin = origin + new Vector2(42, 10);
            var cardMax = origin + new Vector2(298, 94);
            draw.AddRectFilled(cardMin + new Vector2(2, 3), cardMax + new Vector2(2, 3), C(0x80000000), 10);
            draw.AddRectFilled(cardMin, cardMax, C(0xEB1B1917), 10);
            draw.AddRect(cardMin, cardMax, C(0xFF493A29), 10, ImDrawFlags.None, 3);
            draw.AddRect(cardMin + Vector2.One, cardMax - Vector2.One, C(0xFF9B855A), 9, ImDrawFlags.None, 1);
            draw.AddLine(origin + new Vector2(94, 36), origin + new Vector2(282, 36), C(0x685E4C34), 1);
            draw.AddLine(origin + new Vector2(95, 87), origin + new Vector2(279, 87), C(0x403F3629), 1);

            // A polished gold rim, dark inset and small cardinal engravings.
            draw.AddCircleFilled(center + new Vector2(0, 3), 37, C(0x90000000), 64);
            draw.AddCircleFilled(center, 36, C(0xFF292018), 64);
            draw.AddCircleFilled(center, 32, C(0xFF6E5839), 64);
            draw.AddCircleFilled(center, 29, C(0xFF171D1B), 64);
            draw.AddCircle(center, 28, C(0xFF9B8455), 64, 1);
            for (var i = 0; i < 64; i++)
            {
                var angle = i * MathF.Tau / 64;
                var next = (i + 1) * MathF.Tau / 64;
                var light = (1 - MathF.Sin(angle)) / 2;
                var tint = Vector4.Lerp(new(.37f, .28f, .16f, 1), new(.93f, .85f, .64f, 1), light);
                draw.AddLine(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 34,
                    center + new Vector2(MathF.Cos(next), MathF.Sin(next)) * 34,
                    ImGui.ColorConvertFloat4ToU32(tint), 2.5f);
            }
            for (var i = 0; i < 4; i++)
            {
                var axis = new Vector2(MathF.Cos(i * MathF.PI / 2), MathF.Sin(i * MathF.PI / 2));
                draw.AddLine(center + axis * 23, center + axis * 26, C(0xFF9B8455), 1);
            }
            if (direction.HasValue)
            {
                var desired = MathF.Atan2(direction.Value.Y, direction.Value.X);
                if (!angleInitialized) { displayedAngle = desired; angleInitialized = true; }
                var difference = MathF.IEEERemainder(desired - displayedAngle, MathF.Tau);
                displayedAngle += difference * (1 - MathF.Exp(-14 * ImGui.GetIO().DeltaTime));
                var forward = new Vector2(MathF.Cos(displayedAngle), MathF.Sin(displayedAngle));
                var side = new Vector2(-forward.Y, forward.X);
                var tip = center + forward * 22;
                var left = center - forward * 16 + side * 10;
                var notch = center - forward * 8;
                var right = center - forward * 16 - side * 10;
                var shadow = new Vector2(0, 2);
                draw.AddTriangleFilled(tip + shadow, left + shadow, right + shadow, C(0xB0000000));
                draw.AddTriangleFilled(tip, left, notch, C(0xFF967644));
                draw.AddTriangleFilled(tip, notch, right, C(0xFFEDE0B3));
                draw.AddLine(tip, left, C(0xFFF4E8C8), 1);
                draw.AddLine(left, notch, C(0xFF56422D), 1);
                draw.AddLine(notch, right, C(0xFF56422D), 1);
                draw.AddLine(right, tip, C(0xFFF4E8C8), 1);
                draw.AddLine(notch, tip, C(0xFFCAB17C), 1);
                draw.AddCircleFilled(center, 2, C(0xFFE4C892), 16);
            }
            else if (complete)
            {
                draw.AddLine(center + new Vector2(-11, 0), center + new Vector2(-3, 8), C(0xFFE9DBAF), 3);
                draw.AddLine(center + new Vector2(-3, 8), center + new Vector2(13, -10), C(0xFFE9DBAF), 3);
                angleInitialized = false;
            }
            else
            {
                var phase = (float)ImGui.GetTime() * 2;
                for (var i = 0; i < 8; i++)
                {
                    var angle = phase + i * MathF.Tau / 8;
                    var color = ImGui.ColorConvertFloat4ToU32(new Vector4(.84f, .73f, .49f, (i + 1) / 8f));
                    draw.AddCircleFilled(center + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 13, 2, color, 12);
                }
                angleInitialized = false;
            }
            using (body.Push())
            {
                ShadowText(draw, origin + new Vector2(94, 15), C(0xFFE8DECA), Fit(title, 188));
                ShadowText(draw, origin + new Vector2(95, 69), C(0xFFACA99D), Fit(caption, 185));
            }
            using (number.Push()) ShadowText(draw, origin + new Vector2(94, 37), C(0xFFF2E5C4), value);
            if (hovered)
            {
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(title);
                ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 340);
                ImGui.TextWrapped(tooltip);
                ImGui.PopTextWrapPos();
                ImGui.TextDisabled("Arrastra para mover la guía.");
                ImGui.EndTooltip();
            }
        }
        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private static uint C(uint argb) => (argb & 0xFF00FF00) | ((argb & 0x00FF0000) >> 16) | ((argb & 0x000000FF) << 16);

    private static string Fit(string text, float width)
    {
        if (ImGui.CalcTextSize(text).X <= width) return text;
        while (text.Length > 1 && ImGui.CalcTextSize(text + "…").X > width) text = text[..^1];
        return text + "…";
    }

    private static void ShadowText(ImDrawListPtr draw, Vector2 position, uint color, string text)
    {
        draw.AddText(position + new Vector2(1, 2), C(0xE0000000), text);
        draw.AddText(position, color, text);
    }

    public void Dispose() { body.Dispose(); number.Dispose(); }
}

