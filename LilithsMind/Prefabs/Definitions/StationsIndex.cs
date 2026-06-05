// ============================================================
//  StationsIndex — LilithsMind
//  LilithsMind/Prefabs/Definitions/StationsIndex.cs
//
//  [CHANGED] Migrated from bare PrefabGUID + [PrefabName] attribute fields
//            to PrefabDef records. Field names match the prefab string exactly.
//            Names sourced from [PrefabName] attributes; null where absent
//            (special/unused stations). All nullable fields shown explicitly.
//
//  [PERFORMANCE] Static readonly PrefabDef fields — initialised once at
//                class load, zero per-frame cost. Stack-allocated structs,
//                no heap pressure.
// ============================================================

namespace LilithsMind.Prefabs.Definitions;

public static class StationsIndex
{
    // ── Crafting Stations ─────────────────────────────────────────────────────

    public static readonly PrefabDef CraftingStation_Player = new()
    {
        Name    = "PlayerCrafting",
        GuidHash = 1420623103,
        Prefab  = "User",
    };

    public static readonly PrefabDef TM_CraftingStation_SimpleCraftingBench = new()
    {
        Name    = "SimpleWorkbench",
        GuidHash = -1107784271,
        Prefab  = "TM_CraftingStation_SimpleCraftingBench",
        NameKey = "fd7d1d66-58b2-460f-ae82-22f968d6f470",
        DescKey = "7c1bc893-5dec-4baa-aa2f-b096ea95745b",
    };

    public static readonly PrefabDef TM_CraftingStation_WoodworkingBench = new()
    {
        Name    = "WoodworkingBench",
        GuidHash = -332123372,
        Prefab  = "TM_CraftingStation_WoodworkingBench",
        NameKey = "22738d88-d6a3-4692-9ab9-799da92185a1",
        DescKey = "daff14dd-41a5-4821-9013-368a7fd2b3cc",
    };

    public static readonly PrefabDef TM_CraftingStation_Leatherworking = new()
    {
        Name    = "LeatherworkingStation",
        GuidHash = 1779320855,
        Prefab  = "TM_CraftingStation_Leatherworking",
        NameKey = "0a6e7bcb-67ea-41f3-b139-b01c91fb12fd",
        DescKey = "df896ffb-e1aa-47b9-93ae-f7cef28114f2",
    };

    public static readonly PrefabDef TM_CraftingStation_ArtisanTable = new()
    {
        Name    = "ArtisanTable",
        GuidHash = -1718710437,
        Prefab  = "TM_CraftingStation_ArtisanTable",
        NameKey = "8a8bdb9a-1f67-4062-8fcd-99d80e9b217f",
        DescKey = "66382f79-31ac-4590-be7b-f5b8dd5f8514",
    };

    public static readonly PrefabDef TM_CraftingStation_JewelcraftingTable = new()
    {
        Name    = "JewelcraftingTable",
        GuidHash = 508953830,
        Prefab  = "TM_CraftingStation_JewelcraftingTable",
        NameKey = "eef422ec-7d9f-4086-a1df-66033cc6dc3b",
        DescKey = "3e15e3ca-df1d-4a19-a19d-e3a9df260c17",
    };

    public static readonly PrefabDef TM_CraftingStation_TailorBench = new()
    {
        Name    = "TailoringBench",
        GuidHash = -952755594,
        Prefab  = "TM_CraftingStation_TailorBench",
        NameKey = "1320cca5-cb66-495b-987f-c187b2af3078",
        DescKey = "d9a46f8c-101c-4dfd-98ec-8d8be3d7c0d7",
    };

    public static readonly PrefabDef TM_CraftingStation_AlchemyLab_Small = new()
    {
        Name    = "AlchemyTable",
        GuidHash = 181938440,
        Prefab  = "TM_CraftingStation_AlchemyLab_Small",
        NameKey = "d881d6cb-dd44-44b3-a75b-eb438b20f50a",
        DescKey = "ed6dfb2f-19c3-4286-bffc-407e2eb93700",
    };

    public static readonly PrefabDef TM_CraftingStation_Smithy = new()
    {
        Name    = "Smithy",
        GuidHash = -1840926436,
        Prefab  = "TM_CraftingStation_Smithy",
        NameKey = "51ae4d65-4a02-42cd-9bb7-2af131a41740",
        DescKey = "85660508-81e1-4bed-9388-7174fc43a912",
    };

    public static readonly PrefabDef TM_CraftingStation_Anvil = new()
    {
        Name    = "Anvil",
        GuidHash = -437790980,
        Prefab  = "TM_CraftingStation_Anvil",
        NameKey = "35732835-3ae0-4f7e-8adb-1a7ba1e1b3d3",
        DescKey = "63a3e364-126b-44d7-ac55-5fd4f0c4b27a",
    };

    // ── Refinement Stations ───────────────────────────────────────────────────

    public static readonly PrefabDef TM_RefinementStation_Sawmill_Small = new()
    {
        Name    = "Sawmill",
        GuidHash = 1094077710,
        Prefab  = "TM_RefinementStation_Sawmill_Small",
        NameKey = "a5597738-8a46-4a12-b8ed-e4e6c47765d6",
        DescKey = "ee885c8e-d0ec-4093-a944-c3dd0e8cf044",
    };

    public static readonly PrefabDef TM_RefinementStation_Sawmill_Large = new()
    {
        Name    = "AdvancedSawmill",
        GuidHash = -163562336,
        Prefab  = "TM_RefinementStation_Sawmill_Large",
        NameKey = "96f275e2-e705-47e6-853b-b892ac899483",
        DescKey = "ca5889b8-2494-4e71-9a7f-576612acae1b",
    };

    public static readonly PrefabDef TM_RefinementStation_StonecuttingTable_Small = new()
    {
        Name    = "Grinder",
        GuidHash = -600683642,
        Prefab  = "TM_RefinementStation_StonecuttingTable_Small",
        NameKey = "922d6aee-bab0-4c84-8e1b-490b6e8c0906",
        DescKey = "cd97f772-1adb-4a2e-825e-64c47c9f0c25",
    };

    public static readonly PrefabDef TM_RefinementStation_StonecuttingTable_Large = new()
    {
        Name    = "AdvancedGrinder",
        GuidHash = -178579946,
        Prefab  = "TM_RefinementStation_StonecuttingTable_Large",
        NameKey = "c6505c74-d41b-4b9c-b576-b1ccccd8b3f4",
        DescKey = "cd97f772-1adb-4a2e-825e-64c47c9f0c25",
    };

    public static readonly PrefabDef TM_RefinementStation_Furnace_Small = new()
    {
        Name    = "Furnace",
        GuidHash = -1150411622,
        Prefab  = "TM_RefinementStation_Furnace_Small",
        NameKey = "80482750-5fae-476c-8a53-995c2f441079",
        DescKey = "49bcd550-ce3d-4cd6-96f5-ad291f625a1b",
    };

    public static readonly PrefabDef TM_RefinementStation_Furnace_Large = new()
    {
        Name    = "AdvancedFurnace",
        GuidHash = -222851985,
        Prefab  = "TM_RefinementStation_Furnace_Large",
        NameKey = "4765269a-729d-42d0-abc0-257dcbc6d571",
        DescKey = "49bcd550-ce3d-4cd6-96f5-ad291f625a1b",
    };

    public static readonly PrefabDef TM_RefinementStation_BloodPress_Small = new()
    {
        Name    = "BloodPress",
        GuidHash = -300823465,
        Prefab  = "TM_RefinementStation_BloodPress_Small",
        NameKey = "2239d338-d177-4ada-b398-44df090e53df",
        DescKey = "8551294a-378c-4e7e-a59f-61b7d96190e2",
    };

    public static readonly PrefabDef TM_RefinementStation_BloodPress_Large = new()
    {
        Name    = "AdvancedBloodPress",
        GuidHash = -684391635,
        Prefab  = "TM_RefinementStation_BloodPress_Large",
        NameKey = "7e6d6f7e-11ca-4f8f-80c8-febc3dcf1495",
        DescKey = "8551294a-378c-4e7e-a59f-61b7d96190e2",
    };

    public static readonly PrefabDef TM_RefinementStation_Tannery_Small = new()
    {
        Name    = "Tannery",
        GuidHash = -635885386,
        Prefab  = "TM_RefinementStation_Tannery_Small",
        NameKey = "ac86b4a2-2e71-43b6-ae2f-e3167ca40a3a",
        DescKey = "95a56ff0-82de-4179-97e9-507f03aabc6f",
    };

    public static readonly PrefabDef TM_RefinementStation_Tannery_Large = new()
    {
        Name    = "AdvancedTannery",
        GuidHash = -1422196107,
        Prefab  = "TM_RefinementStation_Tannery_Large",
        NameKey = "3621246b-27de-4d18-afd9-3bc61c820c9c",
        DescKey = "95a56ff0-82de-4179-97e9-507f03aabc6f",
    };

    public static readonly PrefabDef TM_RefinementStation_Loom_Small = new()
    {
        Name    = "Loom",
        GuidHash = -16328955,
        Prefab  = "TM_RefinementStation_Loom_Small",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef TM_RefinementStation_Loom_Large = new()
    {
        Name    = "AdvancedLoom",
        GuidHash = 1299929048,
        Prefab  = "TM_RefinementStation_Loom_Large",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef TM_RefinementStation_PaperPress_Small = new()
    {
        Name    = "PaperPress",
        GuidHash = -1628971842,
        Prefab  = "TM_RefinementStation_PaperPress_Small",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef TM_RefinementStation_GemCutting = new()
    {
        Name    = "GemCuttingTable",
        GuidHash = -21483617,
        Prefab  = "TM_RefinementStation_GemCutting",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef TM_RefinementStation_Fabricator = new()
    {
        Name    = "Fabricator",
        GuidHash = -465055967,
        Prefab  = "TM_RefinementStation_Fabricator",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef TM_SalvageStation_Table = new()
    {
        Name    = "Devourer",
        GuidHash = -1719849142,
        Prefab  = "TM_SalvageStation_Table",
        NameKey = null,
        DescKey = null,
    };

    // ── Unit Stations ─────────────────────────────────────────────────────────

    public static readonly PrefabDef TM_UnitStation_VerminNest = new()
    {
        Name    = "VerminNest",
        GuidHash = 150776081,
        Prefab  = "TM_UnitStation_VerminNest",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef TM_UnitStation_Tomb = new()
    {
        Name    = "Tomb",
        GuidHash = 1127059420,
        Prefab  = "TM_UnitStation_Tomb",
        NameKey = null,
        DescKey = null,
    };

    public static readonly PrefabDef TM_UnitStation_NetherGate = new()
    {
        Name    = "StygianSummoningCircle",
        GuidHash = -218354895,
        Prefab  = "TM_UnitStation_NetherGate",
        NameKey = null,
        DescKey = null,
    };

    // ── Special Stations — Do Not Use ─────────────────────────────────────────

    public static readonly PrefabDef TM_CraftingStation_AncestralForge = new()
    {
        Name    = "AncestralForge",
        GuidHash = 48521126,
        Prefab  = "TM_CraftingStation_AncestralForge",
        NameKey = "b3a5426c-2969-496d-a2ef-ed78d7eadb83",
        DescKey = "1486f7c0-cf11-46f4-89b6-8bb73ccacf0f",
    };

    public static readonly PrefabDef TM_CraftingStation_BloodMixer = new()
    {
        Name    = "BloodMixer",
        GuidHash = -969931747,
        Prefab  = "TM_CraftingStation_BloodMixer",
        NameKey = "8b2257a3-f137-4e28-a036-4988a703d2fe",
        DescKey = "06d26ee0-729b-40fd-854c-6494e2cf1fc5",
    };

    public static readonly PrefabDef TM_CraftingStation_FusionForge = new()
    {
        Name    = "FusionForge",
        GuidHash = -1286344051,
        Prefab  = "TM_CraftingStation_FusionForge",
        NameKey = "f2d9189f-ccef-4b33-9aee-df8c6e71b9eb",
        DescKey = "99d39fc6-7dda-4c2f-9e88-26ae3b1f98ca",
    };

    public static readonly PrefabDef TM_CraftingStation_Stables = new()
    {
        Name    = "Stables",
        GuidHash = 472278220,
        Prefab  = "TM_CraftingStation_Stables",
        NameKey = "be1846f8-db74-41fa-9c9a-25ac76434ad5",
        DescKey = "ce54f153-f389-4848-ba04-e5bf20a245e7",
    };

    public static readonly PrefabDef TM_RefinementStation_TeslaLightningRod = new()
    {
        Name    = "LightningRod",
        GuidHash = 1311814093,
        Prefab  = "TM_RefinementStation_TeslaLightningRod",
        NameKey = "9db39e81-dcb5-479d-b4cf-972000aa12a1",
        DescKey = "b31852c6-4cd1-4fdd-ba04-76ead4188aee",
    };

    // ── Unused — Do Not Use ───────────────────────────────────────────────────

    public static readonly PrefabDef TM_CraftingStation_Altar_Frost = new()
    {
        Name    = "FrostAltar",
        GuidHash = -609878016,
        Prefab  = "TM_CraftingStation_Altar_Frost",
        NameKey = "0a69be72-68ee-448d-8aef-e5353fabba2a",
        DescKey = "8b3cc6ad-4a77-4b2c-b0b4-b0668413fd0e",
    };

    public static readonly PrefabDef TM_CraftingStation_Altar_Spectral = new()
    {
        Name    = "SpectralAltar",
        GuidHash = -64110296,
        Prefab  = "TM_CraftingStation_Altar_Spectral",
        NameKey = "9d42edac-3387-489b-af6c-e64923391fab",
        DescKey = "7204fecc-1ad6-4ece-96c9-1ca0731edafa",
    };

    public static readonly PrefabDef TM_CraftingStation_Altar_Unholy = new()
    {
        Name    = "UnholyAltar",
        GuidHash = -676962218,
        Prefab  = "TM_CraftingStation_Altar_Unholy",
        NameKey = "0e40a092-2405-481b-982a-5a296858968e",
        DescKey = "7204fecc-1ad6-4ece-96c9-1ca0731edafa",
    };

    public static readonly PrefabDef TM_CraftingStation_ArtisansCorner = new()
    {
        Name    = "ArtisansCorner",
        GuidHash = 1121480632,
        Prefab  = "TM_CraftingStation_ArtisansCorner",
        NameKey = "ba78a518-b999-480f-bdcc-5542cc5491bc",
        DescKey = "1cbe1041-4a50-4a03-83bf-e8979600bf31",
    };

    public static readonly PrefabDef TM_CraftingStation_BloodBank = new()
    {
        Name    = "BloodBank",
        GuidHash = -452732692,
        Prefab  = "TM_CraftingStation_BloodBank",
        NameKey = "db058eb8-bed8-4254-8761-98b90a65b614",
        DescKey = "f9659cc7-ffcc-4658-b06d-f2290ee1e803",
    };

    public static readonly PrefabDef TM_CraftingStation_MetalworkStation = new()
    {
        Name    = "MetalworkStation",
        GuidHash = 2014944075,
        Prefab  = "TM_CraftingStation_MetalworkStation",
        NameKey = "3298e556-0c56-4b0e-a39e-26b9fcd974b0",
        DescKey = "25ccc69e-4884-42d1-90dc-86a8882b567b",
    };
}