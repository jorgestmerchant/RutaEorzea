using System.Text;

namespace RutaEorzea;

public static class TrackerTitles
{
    public static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormC)
        .Where(c => !char.IsWhiteSpace(c) && c != '\u200B' && c != '\uFEFF'));
}
