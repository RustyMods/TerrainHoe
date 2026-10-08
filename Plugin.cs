using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;

namespace TerrainHoe;

[BepInPlugin(ModGUID, ModName, ModVersion)]
public class TerrainHoePlugin : BaseUnityPlugin
{
    internal const string ModName = "MoreTerrainTypes";
    internal const string ModVersion = "1.0.4";
    internal const string Author = "JamesJonesTV";
    private const string ModGUID = Author + "." + ModName;
    private static readonly string ConfigFileName = ModGUID + ".cfg";
    private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
    internal static string ConnectionError = "";
    public readonly Harmony _harmony = new(ModGUID);

    public static readonly ManualLogSource TerrainHoeLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);

    private static readonly ConfigSync ConfigSync = new(ModGUID)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

    private static ConfigEntry<Toggle> _serverConfigLocked = null!;

    public static TerrainHoePlugin instance;

    public enum Toggle
    {
        On = 1,
        Off = 0
    }

    public void Awake()
    {
        Localizer.Load();

        instance = this;
        
        _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
            "If on, the configuration is locked and can be changed by server admins only.");
        _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);


        var assembly = Assembly.GetExecutingAssembly();
        _harmony.PatchAll(assembly);
        SetupWatcher();
    }
    
    public static void LogInfo(string msg) => TerrainHoeLogger.LogInfo(msg);
    public static void LogWarning(string msg) => TerrainHoeLogger.LogWarning(msg);
    public static void LogError(string msg) => TerrainHoeLogger.LogError(msg);
    public static void LogDebug(string msg) => TerrainHoeLogger.LogDebug(msg);
    
    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    public static class FejdStartup_Awake_Patch
    {
        private static void Prefix(FejdStartup __instance)
        {
            PrefabManager._ZNetScene = __instance.m_objectDBPrefab.GetComponent<ZNetScene>();
            PrefabManager._ObjectDB = __instance.m_objectDBPrefab.GetComponent<ObjectDB>();
            
            _ = new Lava("piece_paint_lava", "$piece_lava");
            _ = new AshlandsGrass("piece_paint_ashlands_grass", "$piece_ashlands_grass");
            _ = new PavedGrass("piece_paint_paved_grass", "$piece_paved_grass");
            _ = new PavedDark("piece_paint_paved_cultivated", "$piece_paved_dark");
            _ = new MistlandsGrass("piece_paint_mistlands", "$piece_mistlands_grass");
            _ = new MistlandsDirt("piece_paint_mistlands_dirt", "$piece_mistlands_dirt");
            _ = new BlackForestGrass("piece_paint_blackforest_grass", "$piece_blackforest_grass");
            _ = new PlainsGrass("piece_paint_plains_grass", "$piece_plains_grass");
            _ = new Snow("piece_paint_snow", "$piece_snow");
            _ = new SwampGrass("piece_paint_swamp_grass", "$piece_swamp_grass");
            _ = new MeadowsGrass("piece_paint_meadows_grass", "$piece_meadows_grass");
            _ = new Reset("piece_paint_reset", "$piece_reset_all");
            
            foreach(var tool in IPaint.m_paintTools.Values) tool.SetupConfigs();
        }
    }
    
    [HarmonyPatch(typeof(Player), nameof(Player.SetupPlacementGhost))]
    private static class Player_SetupPlacementGhost_Patch
    {
        private static void Postfix(Player __instance)
        {
            if (__instance.m_placementGhost == null) return;

            if (!__instance.m_placementGhost.TryGetComponent(out Piece piece)) return;
            if (IPaint.TryGetPaintTool(piece.m_name, out IPaint _))
            {
                __instance.m_placementGhost.AddComponent<PaintOptions>();
            }
        }
    }

    [HarmonyPatch(typeof(Player), nameof(Player.CanRotatePiece))]
    private static class Player_CanRotatePiece_Patch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result) return;
            var piece = __instance.m_buildPieces.GetSelectedPiece();
            if (piece == null) return;
            if (IPaint.IsPaintTool(piece))
            {
                __result = true;
            }
        }
    }

    private void OnDestroy()
    {
        Config.Save();
    }

    private void SetupWatcher()
    {
        FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
        watcher.Changed += ReadConfigValues;
        watcher.Created += ReadConfigValues;
        watcher.Renamed += ReadConfigValues;
        watcher.IncludeSubdirectories = true;
        watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
        watcher.EnableRaisingEvents = true;
    }

    private void ReadConfigValues(object sender, FileSystemEventArgs e)
    {
        if (!File.Exists(ConfigFileFullPath)) return;
        try
        {
            TerrainHoeLogger.LogDebug("ReadConfigValues called");
            Config.Reload();
        }
        catch
        {
            TerrainHoeLogger.LogError($"There was an issue loading your {ConfigFileName}");
            TerrainHoeLogger.LogError("Please check your config entries for spelling and format!");
        }
    }

    public ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
        bool synchronizedSetting = true)
    {
        ConfigDescription extendedDescription =
            new(
                description.Description +
                (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                description.AcceptableValues, description.Tags);
        var configEntry = Config.Bind(group, name, value, extendedDescription);
        //var configEntry = Config.Bind(group, name, value, description);

        var syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
        syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

        return configEntry;
    }

    private ConfigEntry<T> config<T>(string group, string name, T value, string description,
        bool synchronizedSetting = true)
    {
        return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }

    private class ConfigurationManagerAttributes
    {
        [UsedImplicitly] public int? Order;
        [UsedImplicitly] public bool? Browsable;
        [UsedImplicitly] public string? Category;
        [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
    }
}