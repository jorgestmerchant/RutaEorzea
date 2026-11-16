namespace RutaEorzea;

public enum QuestDifficulty { Unknown, Low, Suitable, Higher, VeryHigh }

public static class QuestLevels
{
    public static QuestDifficulty Compare(int questLevel, int playerLevel)
    {
        if (questLevel <= 0 || playerLevel <= 0) return QuestDifficulty.Unknown;
        var difference = questLevel - playerLevel;
        if (difference >= 5) return QuestDifficulty.VeryHigh;
        if (difference >= 3) return QuestDifficulty.Higher;
        if (difference <= -6) return QuestDifficulty.Low;
        return QuestDifficulty.Suitable;
    }

    public static string Description(QuestDifficulty difficulty) => difficulty switch
    {
        QuestDifficulty.Low => "Muy por debajo de tu nivel",
        QuestDifficulty.Suitable => "Nivel adecuado para tu personaje",
        QuestDifficulty.Higher => "Por encima de tu nivel",
        QuestDifficulty.VeryHigh => "Muy por encima de tu nivel",
        _ => "Nivel no disponible"
    };
}
