namespace LilithsMind.Prefabs.Definitions;

public static class SaddleIndex
{
    public static readonly PrefabDef Item_Saddle_Basic = new()
    {
        Name    = "VampireHorseSaddle",
        GuidHash = -1209228232,
        Prefab  = "Item_Saddle_Basic",
        NameKey = "35eddf7b-975d-48c4-b86c-bdf20573485f",
        DescKey = "2003859e-b621-4a0b-b2b8-e0576f2a6f15",
    };

    // DLC: Eternal Dominance
    public static readonly PrefabDef Item_Saddle_Blackfang_DLC = new()
    {
        Name    = "DarkvenomWarSaddle",
        GuidHash = -1793846754,
        Prefab  = "Item_Saddle_Blackfang_DLC",
    };

    // DLC: Sinister Evolution
    public static readonly PrefabDef Item_Saddle_Gloomrot_DLC = new()
    {
        Name    = "PlagueChemistsSaddle",
        GuidHash = -554782766,
        Prefab  = "Item_Saddle_Gloomrot_DLC",
        NameKey = "215334e1-0dd0-46ff-9955-d1d9e191e143",
        DescKey = "1859107d-b6bf-494d-8db7-6ac5b4a95978",
    };

    // DLC: Legacy of Castlevania
    public static readonly PrefabDef Item_Saddle_PMKSkeleton_DLC = new()
    {
        Name    = "RowdainsSteedSaddle",
        GuidHash = -1270904319,
        Prefab  = "Item_Saddle_PMKSkeleton_DLC",
    };
}
