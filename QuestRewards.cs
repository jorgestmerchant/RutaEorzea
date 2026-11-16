using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace RutaEorzea;

internal sealed record RewardItem(uint Id, uint Icon, string Name, uint Count, bool HighQuality = false);
internal sealed record QuestRewardInfo(int Level, uint Gil, ulong? BaseExperience, bool ExperienceScales,
    IReadOnlyList<RewardItem> Guaranteed, IReadOnlyList<RewardItem> Choices,
    IReadOnlyList<string> Unlocks, bool HasSpecialRewards);

internal sealed class QuestRewards(IDataManager data)
{
    private readonly Dictionary<uint, QuestRewardInfo?> cache = [];

    public QuestRewardInfo? Read(uint questId)
    {
        questId &= 0xFFFF;
        if (cache.TryGetValue(questId, out var cached)) return cached;
        if (questId == 0 || !data.GetExcelSheet<Quest>().TryGetRow(questId | 0x10000, out var q))
            return cache[questId] = null;
        var level = q.ClassJobLevel[0];
        ulong? experience = q.ExpFactor == 0 ? 0UL : null;
        if (level > 0 && data.GetExcelSheet<ParamGrow>().TryGetRow(level, out var growth))
            experience = RewardMath.BaseExperience(q.ExpFactor, growth.ScaledQuestXP, growth.QuestExpModifier);
        var guaranteed = new List<RewardItem>();
        var choices = new List<RewardItem>();
        var unlocks = new List<string>();
        var special = false;
        void AddItem(List<RewardItem> list, uint id, uint count, bool hq = false)
        {
            if (id == 0 || count == 0) return;
            if (!data.GetExcelSheet<Item>().TryGetRow(id, out var item)) { special = true; return; }
            list.Add(new(id, item.Icon, item.Name.ToString(), count, hq));
        }
        for (var i = 0; i < q.Reward.Count; i++)
        {
            var reward = q.Reward[i];
            if (reward.RowId == 0) continue;
            if (reward.Is<Item>()) AddItem(guaranteed, reward.RowId, q.ItemCountReward[i]);
            else special = true; // Class-dependent relic rewards or reputation tables.
        }
        for (var i = 0; i < q.ItemCatalyst.Count; i++)
            AddItem(guaranteed, q.ItemCatalyst[i].RowId, q.ItemCountCatalyst[i]);
        AddItem(guaranteed, q.CurrencyReward.RowId, q.CurrencyRewardCount);
        for (var i = 0; i < q.OptionalItemReward.Count; i++)
            AddItem(choices, q.OptionalItemReward[i].RowId, q.OptionalItemCountReward[i], q.OptionalItemIsHQReward[i]);
        if (q.EmoteReward.RowId != 0 && q.EmoteReward.IsValid)
            unlocks.Add($"Gesto: {q.EmoteReward.Value.Name}");
        if (q.ActionReward.RowId != 0 && q.ActionReward.IsValid)
            unlocks.Add($"Acción: {q.ActionReward.Value.Name}");
        foreach (var action in q.GeneralActionReward)
            if (action.RowId != 0 && action.IsValid) unlocks.Add($"Acción general: {action.Value.Name}");
        if (q.OtherReward.RowId != 0 && q.OtherReward.IsValid && !q.OtherReward.Value.Name.IsEmpty)
            unlocks.Add(q.OtherReward.Value.Name.ToString());
        if (q.QuestRewardOtherDisplay.RowId != 0 && q.QuestRewardOtherDisplay.IsValid &&
            !q.QuestRewardOtherDisplay.Value.Name.IsEmpty)
            unlocks.Add(q.QuestRewardOtherDisplay.Value.Name.ToString());
        if (q.SystemReward.Any(x => x != 0) || q.TomestoneCountReward > 0 || q.ReputationReward > 0) special = true;
        return cache[questId] = new(level, q.GilReward, experience, q.LevelMax > 0,
            guaranteed, choices, unlocks.Distinct().ToList(), special);
    }

    public int ExpToNext(int level) => level > 0 && data.GetExcelSheet<ParamGrow>().TryGetRow((uint)level, out var row)
        ? row.ExpToNext : 0;
}
