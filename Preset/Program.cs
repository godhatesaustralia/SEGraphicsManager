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
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public GraphicsManager Manager;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
            Manager = new GraphicsManager(this);
            // NO TOUCH DISPLAY ;)
            Manager.useCustomDisplays = false;
            Manager.Keys = new IniKeys();

            Manager.InfoUtilities.Add(new GasUtilities());
            Manager.InfoUtilities.Add(new InventoryUtilities());
            Manager.InfoUtilities.Add(new FlightUtilities());
            Manager.Init();
        }
        public void Main(string argument, UpdateType updateSource)
        {
            Manager.Update(argument, updateSource);
        }
    }
}
