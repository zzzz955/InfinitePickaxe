namespace InfinitePickaxe.Client.UI.Game
{
    public enum GemType
    {
        AttackSpeed = 0,
        CritChance = 1,
        CritDamage = 2
    }

    public enum GemTier
    {
        Common = 1,
        Uncommon = 2,
        Rare = 3,
        Epic = 4,
        Legend = 5
    }

    public enum GemSelectionRole
    {
        None = 0,
        Base = 1,
        Material = 2,
        Convert = 3
    }

    public sealed class GemData
    {
        public int Id;
        public GemTier Tier;
        public GemType Type;
    }
}
