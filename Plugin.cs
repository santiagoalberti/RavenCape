using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ItemManager;
using JetBrains.Annotations;
using ServerSync;
using System;
using System.IO;
using System.Reflection;

namespace RavenCape
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class RavenCapePlugin : BaseUnityPlugin
    {
        internal const string ModName = "RavenCape";
        internal const string ModVersion = "2.2.0";
        internal const string Author = "Tequila";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;

        internal static string ConnectionError = "";

        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource RavenCapeLogger =
            BepInEx.Logging.Logger.CreateLogSource(ModName);

        private static readonly ConfigSync ConfigSync = new(ModGUID)
        { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        public void Awake()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            Item tqRavenCape = new("raven_cape_bundle", "ravenCape_tq");  // If your folder name is "assets" like the default. You would use this syntax.
            // Item ironFangAxe = new("ironfang", "IronFangAxe", "IronFang"); // If your asset is in a custom folder named IronFang and not the default "assets" folder. You would use this syntax.

            tqRavenCape.Name.English("Raven Cape"); // You can use this to fix the display name in code
            tqRavenCape.Description.English("Cape made of feathers, improves archery skill.");
            tqRavenCape.Crafting.Add("piece_workbench", 2); // Custom crafting stations can be specified as a string
            tqRavenCape.RequiredItems.Add("Feathers", 25);
            tqRavenCape.RequiredItems.Add("WolfHairBundle", 10);
            tqRavenCape.RequiredItems.Add("GreydwarfEye", 5);
            tqRavenCape.RequiredUpgradeItems.Add("WolfHairBundle", 15); // Upgrade requirements are per item, even if you craft two at the same time
            tqRavenCape.RequiredUpgradeItems.Add("WolfHairBundle", 5); // 10 Silver: You need 10 silver for level 2, 20 silver for level 3, 30 silver for level 4
            tqRavenCape.CraftAmount = 1; // We really want to dual wield these

            // You can optionally pass in a configuration option of your own to determine if the recipe is enabled or not. To use the example, uncomment both of the lines below.
            //_recipeIsActiveConfig = config("IronFangAxe", "IsRecipeEnabled",Toggle.On, "Determines if the recipe is enabled for this prefab");
            //ironFangAxe.RecipeIsActive = _recipeIsActiveConfig;

            // If you have something that shouldn't go into the ObjectDB, like vfx or sfx that only need to be added to ZNetScene
            //ItemManager.PrefabManager.RegisterPrefab(PrefabManager.RegisterAssetBundle("ironfang"), "axeVisual",
            //        false); // If our axe has a special visual effect, like a glow, we can skip adding it to the ObjectDB this way
            //ItemManager.PrefabManager.RegisterPrefab(PrefabManager.RegisterAssetBundle("ironfang"), "axeSound",
            //        false); // Same for special sound effects

            // You can also pass in a game object to register a prefab. Example blank GameObject created and registered below.
            //GameObject blankGameObject = new GameObject();
            //ItemManager.PrefabManager.RegisterPrefab(blankGameObject, true);

            //Item heroBlade = new("heroset", "HeroBlade");
            //heroBlade.Crafting.Add(ItemManager.CraftingTable.Workbench, 2);
            //heroBlade.RequiredItems.Add("Wood", 5);
            //heroBlade.RequiredItems.Add("DeerHide", 2);
            //heroBlade.RequiredUpgradeItems.Add("Wood", 2);
            //heroBlade.RequiredUpgradeItems.Add("Flint", 2); // You can even add new items for the upgrade

            //Item heroShield = new("heroset", "HeroShield");
            //heroShield["My first recipe"].Crafting.Add(ItemManager.CraftingTable.Workbench, 1); // You can add multiple recipes for the same item, by giving the recipe a name
            //heroShield["My first recipe"].RequiredItems.Add("Wood", 10);
            //heroShield["My first recipe"].RequiredItems.Add("Flint", 5);
            //heroShield["My first recipe"].RequiredUpgradeItems.Add("Wood", 5);
            //heroShield["My alternate recipe"].Crafting.Add(ItemManager.CraftingTable.Forge, 1); // And this is our second recipe then
            //heroShield["My alternate recipe"].RequiredItems.Add("Bronze", 2);
            //heroShield["My alternate recipe"].RequiredUpgradeItems.Add("Bronze", 1);
            //heroShield.Snapshot(); // I don't have an icon for this item in my asset bundle, so I will let the ItemManager generate one automatically
            // The icon for the item will have the same rotation as the item in unity

            //_ = new Conversion(heroBlade) // For some reason, we want to be able to put a hero shield into a smelter, to get a hero blade
            //{
            //    Input = "HeroShield",
            //    Piece = ConversionPiece.Smelter
            //};

            //heroShield.DropsFrom.Add("Greydwarf", 0.3f, 1, 2); // A Greydwarf has a 30% chance, to drop 1-2 hero shields.

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
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
                RavenCapeLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                RavenCapeLogger.LogError($"There was an issue loading your {ConfigFileName}");
                RavenCapeLogger.LogError("Please check your config entries for spelling and format!");
            }
        }

        #region ConfigOptions

        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        private static ConfigEntry<Toggle> _recipeIsActiveConfig = null!;

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
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

        #endregion ConfigOptions
    }
}