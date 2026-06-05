
## File Structure

```
Naming Conventions:
[*Patch] - Harmony patch that injects before or after game code
[*Patcher] - Modifices ECS component Data
[*Injector] - Injects values into game systems outside of ECS
[*Service] - Static class that performs work
[*Queue] - Holds work items to be done over time at a controlled rate
[*Builder] - Builds complex objects or data structures into more manageable data
[*Cache] - Stored built data that only gets rebuilt when values change
[*Data] - Runtime structure of a container that holds data values
[*Payload] - Envelope of data for sending over network
[*Def] - Defines the structure of a single entity
[*Index] - Static collection of values that may be looked up
[*Enum] - Named set of constant values
[*Registry] - Runtime lookup table populated dynamically
[*Config] - Defines settings and writes config files
[*Logger] - Logging utility for console messages
[*Extensions] - Methods of commonly used code
[*Sender] - Sends information over network
[*System] - Recurring logic systems
[*Loader] - Reads and merges data for use
```
```
[LilithsMind]
    LilithsMind.csproj
    [Network]
        LilithRecipeData.cs
        LilithStationData.cs
        ServerEventPayload.cs
        ServerSyncPayload.cs
    [Prefabs]
        PrefabDef.cs
        [Definitions]
            *Index.cs
```
```
[LilithsHeart]
    LilithsHeart.csproj
    HeartPlugin.cs
    [Config]
        HeartConfig.cs
        HeartPathIndex.cs
        ItemAppearanceConfig.cs
    [Events]
        HeartEventBus.cs
        HeartEventIndex.cs
    [Foundation]
        EntityExtensions.cs
        Heart.cs
        HeartLogger.cs
    [Modules]
        HeartModuleRegistry.cs
        HeartModuleData.cs
    [Network]
        SyncPayloadCache.cs
        SyncSender.cs
    [Patches]
        ClientConnectPatch.cs
        InitializationPatch.cs
    [Services]
        LocalizationService.cs
        PrefabNameResolver.cs
```
```
[LilithsSoul]
    LilithsSoul.csproj
    SoulPlugin.cs
        [Config]
            SoulConfig.cs
            SoulPathIndex.cs
        [Foundation]
            EntityExtensions.cs
            Soul.cs
            SoulLogger.cs
        [Network]
            SyncReceiver.cs
        [Patches]
            ClientChatSystemPatch.cs
            ClientInitPatch.cs
        [Services]
            LocalizationInjector.cs
            RecipePatcher.cs
            ServerRegistry.cs
```
```
[LilithsCookbook]
    LilithsCookbook.csproj
    [Config]
        CookbookConfig.cs
    [Data]
        CookbookItemData.cs
        CookbookRecipeData.cs
        CookbookStationData.cs
    [Systems]
        CookbookConfigBuilder.cs
        CookbookLoader.cs
        RecipeSystem.cs
        StationSystem.cs
```