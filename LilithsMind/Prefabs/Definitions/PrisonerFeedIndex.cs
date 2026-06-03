namespace LilithsMind.Prefabs.Definitions;

public static class PrisonerFeedIndex
{
    // Prisoner Feed "Items"
    // These hold info on Misery, Health, Blood Quality, and mutation chances

    public static readonly PrefabDef FakeItem_FeedPrisoner_BloodSnapper = new()
    {
        Name    = "FedBloodSnapper",
        GuidHash = 526090146,
        Prefab  = "FakeItem_FeedPrisoner_BloodSnapper",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_Corrupted = new()
    {
        Name    = "FedCorruptedFish",
        GuidHash = 714743556,
        Prefab  = "FakeItem_FeedPrisoner_Corrupted",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_FatGoby = new()
    {
        Name    = "FedFatGoby",
        GuidHash = -811840389,
        Prefab  = "FakeItem_FeedPrisoner_FatGoby",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_FierceStinger = new()
    {
        Name    = "FedFierceStinger",
        GuidHash = -114411609,
        Prefab  = "FakeItem_FeedPrisoner_FierceStinger",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_GoldenRiverBass = new()
    {
        Name    = "FedGoldenRiverBass",
        GuidHash = -684874624,
        Prefab  = "FakeItem_FeedPrisoner_GoldenRiverBass",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_IrradiantGruel = new()
    {
        Name    = "FedGruel",
        GuidHash = -1798608844,
        Prefab  = "FakeItem_FeedPrisoner_IrradiantGruel",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_RainbowTrout = new()
    {
        Name    = "FedRainbowTrout",
        GuidHash = 1814558673,
        Prefab  = "FakeItem_FeedPrisoner_RainbowTrout",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_Rat = new()
    {
        Name    = "FedRat",
        GuidHash = 1110550218,
        Prefab  = "FakeItem_FeedPrisoner_Rat",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_SageFish = new()
    {
        Name    = "FedSageFish",
        GuidHash = 172410251,
        Prefab  = "FakeItem_FeedPrisoner_SageFish",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_SwampDweller = new()
    {
        Name    = "FedSwampDweller",
        GuidHash = -314251399,
        Prefab  = "FakeItem_FeedPrisoner_SwampDweller",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_FeedPrisoner_TwilightSnapper = new()
    {
        Name    = "FedTwilightSnapper",
        GuidHash = -1205777419,
        Prefab  = "FakeItem_FeedPrisoner_TwilightSnapper",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_Prisoner_ExtractedBloodPotion = new()
    {
        Name    = "ExtractedBloodPotion",
        GuidHash = -1871776321,
        Prefab  = "FakeItem_Prisoner_ExtractedBloodPotion",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_Prisoner_ExtractedBloodwine = new()
    {
        Name    = "ExtractedBloodwine",
        GuidHash = -1624770558,
        Prefab  = "FakeItem_Prisoner_ExtractedBloodwine",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef FakeItem_Prisoner_ExtractEssence = new()
    {
        Name    = "ExtractedEssence",
        GuidHash = -911541799,
        Prefab  = "FakeItem_Prisoner_ExtractEssence",
        NameKey = null,
        DescKey = null,
    };

    // ── Prisoner Feed "Recipes"
    // These recipes are the Feed "Actions" on a prisoner and work like recipes
    // They take in inputs, and give outputs, if an output is a "Feed" item then it can affect their health and misery

    public static readonly PrefabDef Recipe_Misc_ExtractEssencePrisoner = new()
    {
        Name    = "ExtractEssence",
        GuidHash = 1716338316,
        Prefab  = "Recipe_Misc_ExtractEssencePrisoner",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_BloodSnapper = new()
    {
        Name    = "FeedBloodSnapper",
        GuidHash = 956953141,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_BloodSnapper",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_Corrupted = new()
    {
        Name    = "FeedCorruptedFish",
        GuidHash = 493259323,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_Corrupted",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_FatGoby = new()
    {
        Name    = "FeedFatGoby",
        GuidHash = -2047246570,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_FatGoby",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_FierceStinger = new()
    {
        Name    = "FeedFierceStinger",
        GuidHash = -37587809,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_FierceStinger",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_GoldenRiverBass = new()
    {
        Name    = "FeedGoldenRiverBass",
        GuidHash = 1816434122,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_GoldenRiverBass",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_RainbowTrout = new()
    {
        Name    = "FeedRainbowTrout",
        GuidHash = -1206171767,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_RainbowTrout",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_SageFish = new()
    {
        Name    = "FeedSageFish",
        GuidHash = 1800570390,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_SageFish",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_SwampDweller = new()
    {
        Name    = "FeedSwampDweller",
        GuidHash = -460272822,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_SwampDweller",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Fish_TwilightSnapper = new()
    {
        Name    = "FeedTwilightSnapper",
        GuidHash = -252411567,
        Prefab  = "Recipe_Misc_FeedPrisoner_Fish_TwilightSnapper",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_IrradiantGruel = new()
    {
        Name    = "FeedGruel",
        GuidHash = -279936313,
        Prefab  = "Recipe_Misc_FeedPrisoner_IrradiantGruel",
    };

    public static readonly PrefabDef Recipe_Misc_FeedPrisoner_Rat = new()
    {
        Name    = "FeedRat",
        GuidHash = 1469101010,
        Prefab  = "Recipe_Misc_FeedPrisoner_Rat",
    };
}