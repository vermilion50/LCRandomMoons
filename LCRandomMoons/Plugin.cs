using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using LCRandomMoons.Patches;
using Patch = LCRandomMoons.Patches.StartOfRoundPatch;


namespace LCRandomMoons
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class ModBase : BaseUnityPlugin
    {
        public const string modGUID = "KF.LCRandomMoons";
        private const string modName = "RandomMoons";
        private const string modVersion = "1.1.3";

        private readonly Harmony harmony = new Harmony(modGUID);
        public static ModBase Instance;
        public static new ManualLogSource Logger;
        private static ConfigFile configFile;

        public static void InitConfig()
        {

            Patch.Blacklist = configFile.Bind<string>("Global", "Blacklist moons", "Liquidation,Embrion,Galetry,Gordion", "Moons that will never make the roster. ").Value;

            Patch.RandomDailyMoon = configFile.Bind<bool>("Random Daily Moon", "Enable daily moon", false, "Random moon every day within the selected route.").Value;
            Patch.RandomDailyMoonRepeat = configFile.Bind<bool>("Random Daily Moon", "Block repeats", true, "When “Random Daily Moon” enable, prevents the same moon from repeating.").Value;
            Patch.CompanyName = configFile.Bind<string>("Random Daily Moon", "Company moon", "Gordion", "When “Random Daily Moon” enable, the last day of the quota will send a ship to that moon.").Value;

            Patch.LowMinPrice = configFile.Bind<int>("Range price", "Low min", 1, "Minimum moon cost to get on the route").Value;
            Patch.LowMaxPrice = configFile.Bind<int>("Range price", "Low max", 500, "Maximum moon cost to get on the route").Value;
            Patch.MidMinPrice = configFile.Bind<int>("Range price", "Mid min", 501, "Minimum moon cost to get on the route").Value;
            Patch.MidMaxPrice = configFile.Bind<int>("Range price", "Mid max", 1500, "Maximum moon cost to get on the route").Value;
            Patch.HighMinPrice = configFile.Bind<int>("Range price", "High min", 1501, "Minimum moon cost to get on the route").Value;
            Patch.HighMaxPrice = configFile.Bind<int>("Range price", "High max", 10000, "Maximum moon cost to get on the route").Value;

            Patch.CustomPriceAll = configFile.Bind<bool>("Custom Price", "All set manual price", false, "You can manually set the cost All route").Value;
            Patch.AllPrice = configFile.Bind<int>("Custom Price", "All route price", 0, "Cost of Free route").Value;
            Patch.CustomPriceFree = configFile.Bind<bool>("Custom Price", "Free set manual price", false, "You can manually set the cost Free route").Value;
            Patch.FreePrice = configFile.Bind<int>("Custom Price", "Free route price", 0, "Cost of All route").Value;
            Patch.CustomPriceLow = configFile.Bind<bool>("Custom Price", "Low set manual price", false, "You can manually set the cost Low route").Value;
            Patch.LowPrice = configFile.Bind<int>("Custom Price", "Low route price", 0, "Cost of Low route").Value;
            Patch.CustomPriceMid = configFile.Bind<bool>("Custom Price", "Mid set manual price", false, "You can manually set the cost Mid route").Value;
            Patch.MidPrice = configFile.Bind<int>("Custom Price", "Mid route price", 0, "Cost of Mid route").Value;
            Patch.CustomPriceHigh = configFile.Bind<bool>("Custom Price", "High set manual price", false, "You can manually set the cost High route").Value;
            Patch.HighPrice = configFile.Bind<int>("Custom Price", "High route price", 0, "Cost of High route").Value;
        }

        void Awake()
        {

            if (Instance == null)
            {
                Instance = this;
            }
            Logger = BepInEx.Logging.Logger.CreateLogSource(ModBase.modGUID);
            configFile = base.Config;
            InitConfig();

            harmony.PatchAll(typeof(ModBase));
            harmony.PatchAll(typeof(StartOfRoundPatch));
        }


    }

}
