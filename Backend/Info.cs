using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRageMath;
using VRageRender;

namespace IngameScript
{
    public class InfoUtility
    {
        #region fields

        public static bool justStarted;
        internal MyGridProgram Program;
        internal IMyGridTerminalSystem TerminalSystem;
        internal const char
            commandSplit = '$',
            space = ' ';
        internal string invalid = "[NULL]";
        internal StringBuilder Builder;
        internal TimeSpan DeltaT
        {
            get
            {
                return Program.Runtime.TimeSinceLastRun;
            }
        }

        #endregion

        public InfoUtility()
        {
            Builder = new StringBuilder();
        }
        public virtual void Reset(MyGridProgram program)
        {
            Program = program;
            TerminalSystem = program.GridTerminalSystem;
            justStarted = true;
        }
        public virtual void RegisterCommands(ref Dictionary <string, Action<SpriteData>> commands)
        {
        }
    }
   public class GasUtilities : InfoUtility
    {
        public static List<IMyGasTank> 
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
        public static MyItemType Ice = new MyItemType("MyObjectBuilder_Ore", "Ice");
        public double lastHydrogen = 0, lastIce = 0;
        public TimeSpan lastTime = TimeSpan.Zero;

        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            HydrogenTanks.Clear();
            OxygenTanks.Clear();
            TerminalSystem.GetBlocksOfType(HydrogenTanks, (b) => b.BlockDefinition.SubtypeId.Contains("Hyd"));
            TerminalSystem.GetBlocksOfType(OxygenTanks, (b) => !HydrogenTanks.Contains(b));
        }

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands) 
        {           //i fucking hate this, this sucks ass
            commands.Add("!h2%", (b) =>
            b.Data = HydrogenStatus().ToString("#0.##%"));

            commands.Add("!o2%", (b) =>
                b.Data = OxygenStatus().ToString("#0.##%"));

            commands.Add("!h2t", (b) =>
                b.Data = HydrogenTime());

            commands.Add("!ice", (b) =>
                b.Data = IceRate(InventoryUtilities.InventoryBlocks));
        }

        #endregion
        public double HydrogenStatus()
        {
            var amt = 0d;
            var total = amt;
            foreach (var tank in HydrogenTanks)
            {
                amt += tank.FilledRatio * tank.Capacity;
                total += tank.Capacity;
            }
            return amt / total;
        }

        public double OxygenStatus()
        {
            var amt = 0d;
            var total = amt;
            foreach (var tank in OxygenTanks)
            {
                amt += tank.FilledRatio * tank.Capacity;
                total += tank.Capacity;
            }
            return amt / total;
        }

        public string HydrogenTime()
        {
            if (lastTime == TimeSpan.Zero)
            {
                lastTime = DeltaT;
                return invalid;
            }  
            var current = lastTime + DeltaT;
            //program.Me.CustomData += $"LAST {lastTime} CURRENT {current}\n";
            var pct = HydrogenStatus();
            //program.Me.CustomData += $"PCT {pct}\n";
            var rate = MathHelperD.Clamp(lastHydrogen - pct, 1E-50, double.MaxValue) / (current - lastTime).TotalSeconds;
            //program.Me.CustomData += $"RATE {rate}\n";
            var value = pct / rate;
            lastHydrogen = HydrogenStatus();
            if (rate < 1E-15 || double.IsNaN(value) || double.IsInfinity(value))
                return invalid;
            var time = TimeSpan.FromSeconds(value);
            return string.Format("{0,2:D2}h {1,2:D2}m {2,2:D2}s", (long)time.TotalHours, (long)time.TotalMinutes, (long)time.Seconds); 
        }
        public void LastTimeUpdate()
        {
        lastTime += DeltaT;
        }
        public string IceRate(List<IMyTerminalBlock> blocks)
        {
            var current = lastTime + DeltaT;
            var amt = 0;
            foreach (var block in blocks)
                InventoryUtilities.TryGetItem(block, ref Ice, ref amt);
            var rate = Math.Abs((lastIce - amt) / (current - lastTime).TotalSeconds);
            lastIce = amt;
            if (rate <=0) return invalid;
            return rate.ToString("{0,6}:F2") + " kg/s";
        }
    }

    public class InventoryUtilities : InfoUtility
    {
        public static string myObjectBuilderString = "MyObjectBuilder";
        public static List<IMyTerminalBlock>InventoryBlocks = new List<IMyTerminalBlock>();
        public Dictionary<long, MyItemType[]> ItemStorage = new Dictionary<long, MyItemType[]>();

        string[] ammoNames = new string[]
        {
            "[ACN",
            "[GAT",
            "[RKT",
            "[ASL",
            "[ART",
            "[SRG",
            "[LRG"
        };

        public static bool TryGetItem<T>(T block, ref MyItemType itemType, ref int total)
            where T : IMyEntity
        {
            var initial = total;
            if (block.HasInventory)
            {
                var inventory = block.GetInventory();
                if (!inventory.ContainItems(1, itemType))
                    return false;
                total += inventory.GetItemAmount(itemType).ToIntSafe();
            }
            if (initial == total)
                return false;
            return true;
        }
        public static bool TryGetItem<T>(T block, ref MyItemType[] items, ref int total)
            where T : IMyEntity
        {
            var initial = total;
            if (block.HasInventory)
            {
                var inventory = block.GetInventory();
            foreach (var item in items)
                {
                    if (!inventory.ContainItems(1, item))
                        return false;
                    total += inventory.GetItemAmount(item).ToIntSafe();
                }
            }
            if (initial == total)
                return false;
            return true;
        }
        #region InfoUtility
        public override void Reset(MyGridProgram program)
        {
            base.Reset(program);
            InventoryBlocks.Clear();
            ItemStorage.Clear();
            TerminalSystem.GetBlocksOfType(InventoryBlocks, (b) => b.HasInventory);    
        }

        public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
        {
            commands.Add("!ammo", (b) =>
            {
                var amt = 0;

                if (justStarted)
                {
                    b.Data = b.Data.Trim();
                    var ammos = new string[]
                    {
                    "AutocannonClip",
                    "NATO_25x184mm",
                    "Missile200mm",
                    "MediumCalibreAmmo",
                    "LargeCalibreAmmo",
                    "SmallRailgunAmmo",
                    "LargeRailgunAmmo"
                    };
                    foreach (var ammo in ammos)
                        if (b.Data == ammo)
                            ItemStorage.Add(b.UniqueID, new MyItemType[] { new MyItemType($"{myObjectBuilderString}_AmmoMagazine", ammo) });
                    //throw new Exception(" DIE");
                    //if (ammoType == null) ammoType = MyItemType.MakeAmmo(ammos[1]); this seem return null....

                }
                foreach (var block in InventoryBlocks)
                    TryGetItem(block, ref ItemStorage[b.UniqueID][0], ref amt);
                b.Data = amt.ToString();

                if (!b.UseStringBuilder)
                    return;

                if (b.BuilderPrepend.Length > 0)
                    b.Data = $"{b.BuilderPrepend} {b.Data}";

                if (b.BuilderAppend.Length > 0)
                    b.Data = $"{b.Data} {b.BuilderAppend}";
            });

            commands.Add("!ammos", (b) =>
            {
                if (!b.UseStringBuilder)
                    return;

                if (justStarted)
                {
                    var ammos = new string[]
                    {
                    "AutocannonClip",
                    "NATO_25x184mm",
                    "Missile200mm",
                    "MediumCalibreAmmo",
                    "LargeCalibreAmmo",
                    "SmallRailgunAmmo",
                    "LargeRailgunAmmo"
                    };
                    ItemStorage.Add(b.UniqueID, new MyItemType[7]);
                    for (int i = 0; i < 6; ++i) //it's okay!! becausawe im drunk
                        ItemStorage[b.UniqueID][i] = new MyItemType($"{myObjectBuilderString}_AmmoMagazine", ammos[i]);
                }
                for (int i = 0; i < 6; ++i)
                {
                    var amt = 0;
                    foreach (var block in InventoryBlocks)
                    {
                        TryGetItem(block, ref ItemStorage[b.UniqueID][i], ref amt);
                    }

                    if (amt > 0)
                    {
                        var data = amt.ToString();
                        Builder.AppendLine($"{ammoNames[i]} {data}{b.BuilderAppend}");
                    }
                }

                b.Data = Builder.ToString();
                Builder.Clear();
            });

            commands.Add("!item", (b) =>
            {
                var amt = 0;
                MyItemType itemType = new MyItemType();
                if (justStarted)
                    if (b.Data.Contains(commandSplit))
                    {
                        b.Data = b.Data.Trim();
                        var stringParts = b.Data.Split(commandSplit);
                        itemType = new MyItemType($"{myObjectBuilderString}_{stringParts[0]}", stringParts[1]);
                    }
                foreach (var block in InventoryBlocks)
                    TryGetItem(block, ref itemType, ref amt);
                b.Data = amt.ToString();
            });
        }
        #endregion

        public class FlightUtilities : InfoUtility
        {
            IMyCubeGrid Ship;
            IMyShipController Controller;
            Vector3D VZed = Vector3D.Zero;

            #region InfoUtility

            public override void Reset(MyGridProgram program)
            {
                base.Reset(program);
                Ship = program.Me.CubeGrid;
                TerminalSystem.GetBlocksOfType<IMyShipController>(null, (b) =>
                {
                    if (b.CustomName.Contains("[I]") || b.IsMainCockpit)
                        Controller = b;
                    return true;
                });
            }

            public override void RegisterCommands(ref Dictionary<string, Action<SpriteData>> commands)
            {
                commands.Add("!aoa", (b) =>
                {
                    if (Controller == null || Controller.GetNaturalGravity() == VZed)
                    {
                        b.Data = invalid;
                        return;
                    }
                    

                });
            }
            #endregion

            bool GravCheck(out Vector3D grav) //wanted something nice and neat
            {
                grav = VZed;
                if (Controller == null)
                    return false;

                grav = Controller.GetNaturalGravity();
                if (grav == VZed)
                    return false;
                return true;
            }

            public string GetAoA()
            {
                var grav = VZed;
                if (!GravCheck(out grav))
                    return invalid;
                var gridMatrix = Controller.WorldMatrix;
                var 
            }

        }
    }
}