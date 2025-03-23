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
        private const string modVersion = "1.0.2";

        private readonly Harmony harmony = new Harmony(modGUID);
        public static ModBase Instance;
        public static new ManualLogSource Logger;
        private static ConfigFile configFile;

        public static void InitConfig()
        {
            Patch.randomDailyMoon = configFile.Bind<bool>("Global", "Random daily moon", false, "Random moon every day").Value;
            Patch.blacklist = configFile.Bind<string>("Global", "Blacklist moons", "Liquidation,Galetry,Gordion", "Moons that will never make the roster. ").Value;
            Patch.freePrice = configFile.Bind<int>("Global", "Free moons price", 0, "You can set the cost of free moons").Value;

            Patch.lowMinPrice = configFile.Bind<int>("Moons list", "(Low)Min Range Price", 1, "Minimum moon value to get on the list").Value;
            Patch.lowMaxPrice = configFile.Bind<int>("Moons list", "(Low)Max Range Price", 500, "Maximum moon value to get on the list").Value;
            Patch.midMinPrice = configFile.Bind<int>("Moons list", "(Mid)Min Range Price", 501, "Minimum moon value to get on the list").Value;
            Patch.midMaxPrice = configFile.Bind<int>("Moons list", "(Mid)Max Range Price", 1500, "Maximum moon value to get on the list").Value;
            Patch.highMinPrice = configFile.Bind<int>("Moons list", "(High)Min Range Price", 1501, "Minimum moon value to get on the list").Value;
            Patch.highMaxPrice = configFile.Bind<int>("Moons list", "(High)Max Range Price", 10000, "Maximum moon value to get on the list").Value;
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
