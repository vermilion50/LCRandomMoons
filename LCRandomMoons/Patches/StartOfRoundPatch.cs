using HarmonyLib;
using LethalLevelLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using TerminalApi.Classes;
using static TerminalApi.TerminalApi;
using LethalModDataLib.Attributes;
using LethalModDataLib.Enums;


namespace LCRandomMoons.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPatch : NetworkBehaviour
    {
        private static List<ExtendedLevel> _allMoons = PatchedContent.ExtendedLevels;
        private static List<ExtendedLevel> _freeMoons = new List<ExtendedLevel>();
        private static List<ExtendedLevel> _lowMoons = new List<ExtendedLevel>();
        private static List<ExtendedLevel> _midMoons = new List<ExtendedLevel>();
        private static List<ExtendedLevel> _highMoons = new List<ExtendedLevel>();
        private static List<ExtendedLevel> _lastRoute;

        [ModData(SaveWhen.OnSave, LoadWhen.OnLoad, SaveLocation.CurrentSave)]
        private static string _lastRouteCategory;
        private static readonly Dictionary<string, List<ExtendedLevel>> RouteCategories = new Dictionary<string, List<ExtendedLevel>>()
        {
            { "free", _freeMoons },
            { "low", _lowMoons },
            { "mid", _midMoons },
            { "high", _highMoons },
            { "all", _allMoons }
        };

        public static string CompanyName;
        private static ExtendedLevel _company;

        public static string Blacklist;
        private static string[] _blacklistArray;

        public static bool RandomDailyMoon;
        public static bool RandomDailyMoonRepeat;

        public static bool CustomPriceAll;
        public static int AllPrice;

        public static bool CustomPriceFree;
        public static int FreePrice;

        public static bool CustomPriceLow;
        public static int LowPrice;
        public static int LowMinPrice;
        public static int LowMaxPrice;

        public static bool CustomPriceMid;
        public static int MidPrice;
        public static int MidMinPrice;
        public static int MidMaxPrice;

        public static bool CustomPriceHigh;
        public static int HighPrice;
        public static int HighMinPrice;
        public static int HighMaxPrice;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void SetAllMoonsList()
        {
            _freeMoons.Clear();
            _lowMoons.Clear();
            _midMoons.Clear();
            _highMoons.Clear();

            ModBase.InitConfig();
            _blacklistArray = Blacklist.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var level in _allMoons)
            {
                if (CompanyName == level.NumberlessPlanetName) _company = level;
                if (_blacklistArray.Contains(level.NumberlessPlanetName)) continue;

                if (level.RoutePrice == 0) _freeMoons.Add(level);
                if (level.RoutePrice >= LowMinPrice && level.RoutePrice <= LowMaxPrice) _lowMoons.Add(level);
                if (level.RoutePrice >= MidMinPrice && level.RoutePrice <= MidMaxPrice) _midMoons.Add(level);
                if (level.RoutePrice >= HighMinPrice && level.RoutePrice <= HighMaxPrice) _highMoons.Add(level);
            }
           
            if (!CustomPriceAll) AllPrice = GetLevelsPrice(_allMoons);
            if (!CustomPriceFree) FreePrice = GetLevelsPrice(_freeMoons);
            if (!CustomPriceLow) LowPrice = GetLevelsPrice(_lowMoons);
            if (!CustomPriceMid) MidPrice = GetLevelsPrice(_midMoons);
            if (!CustomPriceHigh) HighPrice = GetLevelsPrice(_highMoons);

            if (string.IsNullOrEmpty(_lastRouteCategory))
            {
                SetLastRoute(_freeMoons);
            }
            else if (RouteCategories.TryGetValue(_lastRouteCategory, out var route))
            {
                SetLastRoute(route);
            }
            else
            {
                ModBase.Logger.LogError($"Invalid route category: {_lastRouteCategory}");
                SetLastRoute(_freeMoons);
            }

            TerminalAPI();
            DebugLog();
        }


        [HarmonyPatch("SetShipReadyToLand")]
        [HarmonyPostfix]
        static void RandomDailyMoonR()
        {
            if (!RandomDailyMoon) return;
            ModBase.Logger.LogInfo($"[RandomDailyMoon] Enable: true");

            var currentMoon = LevelManager.CurrentExtendedLevel.NumberlessPlanetName;
            ModBase.Logger.LogInfo($"[RandomDailyMoon] Current moon: {currentMoon}");

            if (_blacklistArray.Contains(currentMoon))
            {
                ModBase.Logger.LogInfo("[RandomDailyMoon] Auto-travel from blacklisted moon forbidden");
                return;
            }

            if (TimeOfDay.Instance.daysUntilDeadline == 3)
            {
                SetLastRoute(_freeMoons);
                ModBase.Logger.LogInfo("[RandomDailyMoon] First Day Quota. Auto-travel disabled.");
                return;
            }

            var localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null || !localPlayer.IsHost || !StartOfRound.Instance.CanChangeLevels())
            {
                ModBase.Logger.LogInfo("[RandomDailyMoon] Can't change levels");
                return;
            }

            var isLastDay = TimeOfDay.Instance.daysUntilDeadline == 0;
            var nextLevel = isLastDay ? _company : GetRandomMoon(_lastRoute);

            if (RandomDailyMoonRepeat && nextLevel != _company)
            {
                int attempts = 0;
                const int maxAttempts = 10;

                while (nextLevel == LevelManager.CurrentExtendedLevel && attempts < maxAttempts)
                {
                    nextLevel = GetRandomMoon(_lastRoute);
                    attempts++;
                }

                if (attempts >= maxAttempts)
                {
                    ModBase.Logger.LogWarning("[RandomDailyMoon] Failed to find unique moon after 10 attempts");
                }
            }

            ModBase.Logger.LogInfo($"[RandomDailyMoon] Next moon: {nextLevel.NumberlessPlanetName} " +
                                $"{(isLastDay ? "(Last Day)" : "")}");

            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
            StartOfRound.Instance.ChangeLevelServerRpc(
                nextLevel.SelectableLevel.levelID,
                terminal.groupCredits
            );
        }

        public static string SetMoon(List<ExtendedLevel> moonlist, int moonlistListPrice, string routeName)
        {
            var localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer == null || !localPlayer.IsHost || !StartOfRound.Instance.CanChangeLevels())
            {
                return $"\n Only the Host can set the route \n You must be on orbit \n\n";
            }

            SetLastRoute(moonlist);

            var selectLevel = GetRandomMoon(moonlist);
            if (selectLevel == null)
            {
                return $" \n {routeName} list not contain moons \n\n";
            }

            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (terminal.groupCredits < moonlistListPrice)
            {
                return $"Not enough credits! Need {moonlistListPrice}, have {terminal.groupCredits} \n\n";
            }

            terminal.groupCredits -= moonlistListPrice;
            terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
            StartOfRound.Instance.ChangeLevelServerRpc(selectLevel.SelectableLevel.levelID, terminal.groupCredits);

            return $"\n Route: {routeName} ({moonlistListPrice}) \n\n " +
                            $"The ship is headed to the [{selectLevel.NumberlessPlanetName}] \n\n " +
                            $"RiskLevel: {selectLevel.SelectableLevel.riskLevel} \n " +
                            $"Weather: {selectLevel.SelectableLevel.currentWeather} \n\n";
        }

        public static void SetLastRoute(List<ExtendedLevel> route)
        {
            _lastRoute = route;
            _lastRouteCategory = RouteCategories
                .FirstOrDefault(x => x.Value == route).Key;
        }

        public static int GetLevelsPrice(List<ExtendedLevel> Levels)
        {
            int price = 0;
            int count = 0;
            if (Levels.Count != 0)
            {
                foreach (var level in Levels)
                {
                    if (level.RoutePrice > 0)
                    {
                        price += level.RoutePrice;
                        count++;
                    }
                }
                if (count != 0) price /= count;             
            }
            return price;
        }

        public static ExtendedLevel GetRandomMoon(List<ExtendedLevel> moonlist)
        {
            if (moonlist.Count == 0) { return null; }
            System.Random rnd = new System.Random();
            int r = rnd.Next(moonlist.Count);
            return moonlist[r];
        }

        public static string GetMoonsList()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Selected route: " + _lastRouteCategory); 
            sb.AppendLine();
            sb.AppendLine($"All Route: {AllPrice} credits");
            sb.AppendLine("-------------------------");
            sb.AppendLine("*all*");
            sb.AppendLine();
            sb.AppendLine($"Free Levels: {FreePrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in _freeMoons)
            {
                string weatherType = "";
                if (level.SelectableLevel.currentWeather != LevelWeatherType.None)
                {
                    weatherType = $" ({level.SelectableLevel.currentWeather.ToString()})";
                }
                sb.AppendLine(level.SelectableLevel.riskLevel + " " + level.NumberlessPlanetName + weatherType);
            }

            sb.AppendLine();
            sb.AppendLine($"Low Route: {LowPrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in _lowMoons)
            {
                string weatherType = "";
                if (level.SelectableLevel.currentWeather != LevelWeatherType.None)
                {
                    weatherType = $" ({level.SelectableLevel.currentWeather.ToString()})";
                }
                sb.AppendLine(level.SelectableLevel.riskLevel + " " + level.NumberlessPlanetName + weatherType);
            }

            sb.AppendLine();
            sb.AppendLine($"Mid Route: {MidPrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in _midMoons)
            {
                string weatherType = "";
                if (level.SelectableLevel.currentWeather != LevelWeatherType.None)
                {
                    weatherType = $" ({level.SelectableLevel.currentWeather.ToString()})";
                }
                sb.AppendLine(level.SelectableLevel.riskLevel + " " + level.NumberlessPlanetName + weatherType);
            }

            sb.AppendLine();
            sb.AppendLine($"High Route: {HighPrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in _highMoons)
            {
                string weatherType = "";
                if (level.SelectableLevel.currentWeather != LevelWeatherType.None)
                {
                    weatherType = $" ({level.SelectableLevel.currentWeather.ToString()})";
                }
                sb.AppendLine(level.SelectableLevel.riskLevel + " " + level.NumberlessPlanetName + weatherType);
            }
            return sb.ToString() + "\n\n";
        }

        private static void TerminalAPI()
        {
            AddCommand("rd help", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("Syntax: rd <command>");
                    sb.AppendLine("");
                    sb.AppendLine("help - display available commands");
                    sb.AppendLine("list - display list all the moons by category");
                    sb.AppendLine("route - display selected route");
                    sb.AppendLine("");
                    sb.AppendLine("all - select random moon from [all] route");
                    sb.AppendLine("free - select random moon from [free] route");
                    sb.AppendLine("low - select random moon from [low] route");
                    sb.AppendLine("mid - select random moon from [mid] route");
                    sb.AppendLine("high - select random moon from [high] route");
                    sb.AppendLine("");
                    sb.AppendLine("Example: \"rd mid\" travels ship to a random moon from route \"mid\"");
                    sb.AppendLine("\n");
                    return sb.ToString();
                },
                Category = "Other"
            });

            AddCommand("rd list", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return GetMoonsList();
                },
                Category = "Other"
            });

            AddCommand("rd route", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return "Selected route: "+_lastRouteCategory+ "\n\n";
                },
                Category = "Other"
            });

            AddCommand("rd all", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(_allMoons, AllPrice, "[All]");
                },
                Category = "Other"
            });

            AddCommand("rd free", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(_freeMoons, FreePrice, "[Free]");
                },
                Category = "Other"
            });

            AddCommand("rd low", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(_lowMoons, LowPrice, "[Low]");
                },
                Category = "Other"
            });

            AddCommand("rd mid", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(_midMoons, MidPrice, "[Mid]");
                },
                Category = "Other"
            });

            AddCommand("rd high", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(_highMoons, HighPrice, "[High]");
                },
                Category = "Other"
            });
        }

        public static void DebugLog()
        {
            ModBase.Logger.LogInfo($"All Route: {AllPrice} credits");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"Free Route: {FreePrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in _freeMoons)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"Low Route: {LowPrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in _lowMoons)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"Mid Route: {MidPrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in _midMoons)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"High Route: {HighPrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in _highMoons)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
        }
    }
}
