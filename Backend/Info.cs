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
    public interface IUtility
    {
        void GetBlocks(IMyGridTerminalSystem gts);
        void ListBlocks(StringBuilder builder);
    }
   public class HydroloxUtilities //: IUtility
    {
        public static List<IMyGasTank> 
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
        public static string invalid = "•••";
        public static MyItemType Ice = new MyItemType("MyObjectBuilder_Ore", "Ice");
        public double lastHydrogen = 0, lastIce = 0;
        public TimeSpan lastTime = TimeSpan.Zero;

   
        public void GetBlocks(IMyGridTerminalSystem gts)
        {
            HydrogenTanks.Clear();
            OxygenTanks.Clear();
            gts.GetBlocksOfType(HydrogenTanks, (b) => b.BlockDefinition.SubtypeId.Contains("Hyd"));
            gts.GetBlocksOfType(OxygenTanks, (b) => !HydrogenTanks.Contains(b));
        }

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

        public string HydrogenTime(TimeSpan deltaT, MyGridProgram program)
        {
            if (lastTime == TimeSpan.Zero)
            {
                lastTime = deltaT;
                return invalid;
            }  
            var current = lastTime + deltaT;
           program.Me.CustomData += $"LAST {lastTime} CURRENT {current}\n";
            var pct = HydrogenStatus();program.Me.CustomData += $"PCT {pct}\n";
            var rate = MathHelperD.Clamp(lastHydrogen - pct, 1E-50, double.MaxValue) / (current - lastTime).TotalSeconds;
            program.Me.CustomData += $"RATE {rate}\n";
            var value = pct / rate;
            lastHydrogen = HydrogenStatus();
            if (rate < 1E-15 || double.IsNaN(value) || double.IsInfinity(value))
                return invalid;
            var time = TimeSpan.FromSeconds(value);
            return string.Format("{0,2:D2}h {1,2:D2}m {2,2:D2}s", (long)time.TotalHours, (long)time.TotalMinutes, (long)time.Seconds); 
        }
        public void LastTimeUpdate(TimeSpan deltaT)
        {
        lastTime += deltaT;
        }
        public string IceRate(List<IMyTerminalBlock> blocks, TimeSpan deltaT)
        {
            var current = lastTime + deltaT;
            var amt = 0;
            foreach (var block in blocks)
                InventoryUtilities.TryGetItem(block, ref Ice, ref amt);
            var rate = Math.Abs((lastIce - amt) / (current - lastTime).TotalSeconds);
            lastIce = amt;
            if (rate <=0) return invalid;
            return rate.ToString("{0,6}:F2") + " kg/s";
        }
    }

    public class InventoryUtilities //: IUtility
    {
        public static string myObjectBuilderString = "MyObjectBuilder";
        public static List<IMyTerminalBlock>InventoryBlocks = new List<IMyTerminalBlock>();
        public Dictionary<long, MyItemType[]> ItemStorage;

        public InventoryUtilities()
        {
            ItemStorage = new Dictionary<long, MyItemType[]>();
        }

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
        public void GetBlocks(IMyGridTerminalSystem gts)
        {
            InventoryBlocks.Clear();
            ItemStorage.Clear();
            gts.GetBlocksOfType(InventoryBlocks, (b) => b.HasInventory);    
        }
    }
}