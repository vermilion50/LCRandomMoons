using GameNetcodeStuff;
using HarmonyLib;
using LethalLevelLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Netcode;
using TerminalApi.Classes;
using static TerminalApi.TerminalApi;

namespace LCRandomMoons.Patches
{
    [HarmonyPatch(typeof(StartOfRound))]
    public class StartOfRoundPatch : NetworkBehaviour
    {
        private static List<ExtendedLevel> allLevels = PatchedContent.ExtendedLevels;
        private static List<ExtendedLevel> freeLevels = new List<ExtendedLevel>();
        private static List<ExtendedLevel> lowLevels = new List<ExtendedLevel>();
        private static List<ExtendedLevel> midLevels = new List<ExtendedLevel>();
        private static List<ExtendedLevel> highLevels = new List<ExtendedLevel>();
        private static List<ExtendedLevel> lastList = freeLevels;
        private static ExtendedLevel selectLevel;

        public static string blacklist;

        public static bool randomDailyMoon;

        private static int allPrice;
        public static int freePrice;

        public static int lowMinPrice;
        public static int lowMaxPrice;
        private static int lowPrice;

        public static int midMinPrice;
        public static int midMaxPrice;
        private static int midPrice;

        public static int highMinPrice;
        public static int highMaxPrice;
        private static int highPrice;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void SetAllMoonsList(StartOfRound __instance)
        {
            freeLevels.Clear();
            lowLevels.Clear();
            midLevels.Clear();
            highLevels.Clear();

            ModBase.InitConfig();

            string[] blacklistArray = blacklist.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var level in allLevels)
            {
                if (blacklistArray.Contains(level.NumberlessPlanetName))
                {
                    continue; 
                }
                if (level.RoutePrice == 0)
                {
                    freeLevels.Add(level);
                }
                if (level.RoutePrice >= lowMinPrice && level.RoutePrice <= lowMaxPrice)
                {
                    lowLevels.Add(level);
                }
                if (level.RoutePrice >= midMinPrice && level.RoutePrice <= midMaxPrice)
                {
                    midLevels.Add(level);
                }
                if (level.RoutePrice >= highMinPrice && level.RoutePrice <= highMaxPrice)
                {
                    highLevels.Add(level);
                }
            }

            allPrice = GetLevelsPrice(allLevels);
            lowPrice = GetLevelsPrice(lowLevels);
            midPrice = GetLevelsPrice(midLevels);
            highPrice = GetLevelsPrice(highLevels);

            TerminalAPI();
            DebugLog();
        }

        [HarmonyPatch("SetShipReadyToLand")]
        [HarmonyPostfix]
        static void RandomDailyMoon()
        {
            if (randomDailyMoon == true)
            {
                ModBase.Logger.LogInfo($"RandomDailyMoon: true");
                ModBase.Logger.LogInfo($"RandomDailyMoon: last moon {LevelManager.CurrentExtendedLevel.NumberlessPlanetName}");
                string[] blacklistArray = blacklist.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (!blacklistArray.Contains(LevelManager.CurrentExtendedLevel.NumberlessPlanetName))
                {
                    if (TimeOfDay.Instance.daysUntilDeadline != 3)
                    {
                        PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
                        if (localPlayer != null && localPlayer.IsHost && StartOfRound.Instance.CanChangeLevels())
                        {
                            Terminal terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
                            terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);
                            selectLevel = GetRandomMoon(lastList);
                            ModBase.Logger.LogInfo($"RandomDailyMoon: next moon {selectLevel.NumberlessPlanetName}");
                            StartOfRound.Instance.ChangeLevelServerRpc(
                                selectLevel.SelectableLevel.levelID, terminal.groupCredits);
                        }
                        else { 
                            ModBase.Logger.LogInfo($"Can't change levels");
                        }
                    } else {
                        lastList = freeLevels;
                        ModBase.Logger.LogInfo($"First Day Quota. Auto-travel disabled.");
                    }
                } else
                {
                    ModBase.Logger.LogInfo($"RandomDailyMoon: Auto-travel from the blacklist moons forbidden");
                }
            }
        }

        public static string SetMoon(List<ExtendedLevel> moonlist, int moonlistListPrice, string routeName)
        {
            PlayerControllerB localPlayer = GameNetworkManager.Instance.localPlayerController;
            if (localPlayer != null && localPlayer.IsHost && StartOfRound.Instance.CanChangeLevels())
            {
                lastList = moonlist;
                selectLevel = GetRandomMoon(moonlist);
                if (selectLevel != null)
                {
                    Terminal terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
                    if (terminal.groupCredits >= moonlistListPrice)
                    {
                        terminal.groupCredits -= moonlistListPrice;
                        terminal.SyncGroupCreditsServerRpc(terminal.groupCredits, terminal.numberOfItemsInDropship);

                        StartOfRound.Instance.ChangeLevelServerRpc(
                            selectLevel.SelectableLevel.levelID,
                            terminal.groupCredits
                            );

                        return $"\n Route: {routeName} ({moonlistListPrice}) \n\n " +
                            $"The ship is headed to the [{selectLevel.NumberlessPlanetName}] \n\n " +
                            $"RiskLevel: {selectLevel.SelectableLevel.riskLevel} \n " +
                            $"Weather: {selectLevel.SelectableLevel.currentWeather} \n\n";
                    }
                    return $"Not enough credits! Need {moonlistListPrice}, have {terminal.groupCredits} \n\n";                   
                }
                return $" \n {routeName} list not contain moons \n\n";
            }
            return $"\n Only the Host can set the route \n You must be on orbit \n\n";
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
                price = price / count;
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

            sb.AppendLine($"All Levels: {allPrice} credits");
            sb.AppendLine();
            sb.AppendLine($"Free Levels: {freePrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in freeLevels)
            {
                string weatherType = "";
                if (level.SelectableLevel.currentWeather != LevelWeatherType.None)
                {
                    weatherType = $" ({level.SelectableLevel.currentWeather.ToString()})";
                }
                sb.AppendLine(level.SelectableLevel.riskLevel + " " + level.NumberlessPlanetName + weatherType);
            }

            sb.AppendLine();
            sb.AppendLine($"Low Levels: {lowPrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in lowLevels)
            {
                string weatherType = "";
                if (level.SelectableLevel.currentWeather != LevelWeatherType.None)
                {
                    weatherType = $" ({level.SelectableLevel.currentWeather.ToString()})";
                }
                sb.AppendLine(level.SelectableLevel.riskLevel + " " + level.NumberlessPlanetName + weatherType);
            }

            sb.AppendLine();
            sb.AppendLine($"Mid Levels: {midPrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in midLevels)
            {
                string weatherType = "";
                if (level.SelectableLevel.currentWeather != LevelWeatherType.None)
                {
                    weatherType = $" ({level.SelectableLevel.currentWeather.ToString()})";
                }
                sb.AppendLine(level.SelectableLevel.riskLevel + " " + level.NumberlessPlanetName + weatherType);
            }

            sb.AppendLine();
            sb.AppendLine($"High Levels: {highPrice} credits");
            sb.AppendLine("-------------------------");
            foreach (var level in highLevels)
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
                    sb.AppendLine("list - display a list all the moons by category");
                    sb.AppendLine("");
                    sb.AppendLine("all - select random moon from [all] list");
                    sb.AppendLine("free - select random moon from [free] list");
                    sb.AppendLine("low - select random moon from [low] list");
                    sb.AppendLine("mid - select random moon from [mid] list");
                    sb.AppendLine("high - select random moon from [high] list");
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

            AddCommand("rd all", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(allLevels, allPrice, "[All]");
                },
                Category = "Other"
            });

            AddCommand("rd free", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(freeLevels, freePrice, "[Free]");
                },
                Category = "Other"
            });

            AddCommand("rd low", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(lowLevels, lowPrice, "[Low]");
                },
                Category = "Other"
            });

            AddCommand("rd mid", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(midLevels, midPrice, "[Mid]");
                },
                Category = "Other"
            });

            AddCommand("rd high", new CommandInfo()
            {
                DisplayTextSupplier = () =>
                {
                    return SetMoon(highLevels, highPrice, "[High]");
                },
                Category = "Other"
            });
        }

        public static void DebugLog()
        {
            ModBase.Logger.LogInfo($"All Levels: {allPrice} credits");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"FreeLevels: {freePrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in freeLevels)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"lowLevels: {lowPrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in lowLevels)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"midLevels: {midPrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in midLevels)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
            ModBase.Logger.LogInfo($"highLevels: {highPrice} credits");
            ModBase.Logger.LogInfo("-------------------------");
            foreach (var level in highLevels)
            {
                ModBase.Logger.LogInfo(level.NumberlessPlanetName + " Pr: " + level.RoutePrice + " ID: " + level.SelectableLevel.levelID);
            }
            ModBase.Logger.LogInfo("-------------------------");
            ModBase.Logger.LogInfo("");
        }
    }
}
