namespace RutaEorzea;

public static class RewardMath
{
    // Base formula from the game's Quest and ParamGrow sheets. Wide arithmetic
    // avoids overflowing before dividing by 100. Bonuses are deliberately excluded.
    public static ulong BaseExperience(ushort factor, ushort scaledQuestXp, byte modifier) =>
        (ulong)factor * scaledQuestXp * modifier / 100;

    public static double? LevelPercentage(ulong experience, int expToNext) =>
        expToNext > 0 ? experience * 100d / expToNext : null;
}
