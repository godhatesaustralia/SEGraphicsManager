using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
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
   public class HydroloxUtilities
    {
        public static List<IMyGasTank> 
            HydrogenTanks = new List<IMyGasTank>(),
            OxygenTanks = new List<IMyGasTank>();
       public static List<IMyGasGenerator> Generators = new List<IMyGasGenerator>();
        public static string invalid = "•••";
       public double lastHydrogenValue;
       public TimeSpan lastTime = TimeSpan.Zero;

   
        public static void GetBlocks(IMyGridTerminalSystem gts)
        {
            HydrogenTanks.Clear();
            OxygenTanks.Clear();
            Generators.Clear();
            gts.GetBlocksOfType(HydrogenTanks, (b) => b.BlockDefinition.SubtypeId.Contains("Hydrogen"));
            gts.GetBlocksOfType(OxygenTanks, (b) => b.BlockDefinition.SubtypeId.Contains("Oxygen"));
            gts.GetBlocksOfType(Generators);
        }

        public static double HydrogenStatus()
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
            var rate = MathHelperD.Clamp(lastHydrogenValue - pct, 1E-50, double.MaxValue) / (current - lastTime).TotalSeconds;
            program.Me.CustomData += $"RATE {rate}\n";
            var value = pct / rate;
            lastTime = current;
            lastHydrogenValue = HydrogenStatus();
            if (rate < 1E-15 || double.IsNaN(value) || double.IsInfinity(value))
                return invalid;
            var time = TimeSpan.FromSeconds(value);
            return string.Format("{0,2:D2}h {1,2:D2}m {2,2:D2}s", (long)time.TotalHours, (long)time.TotalMinutes, (long)time.Seconds);
             
        }
    }
}