using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        GraphicsManager Manager;
        string tag = "GCM";
        public Program()
        {
            CoyLogo.program = this;
            Manager = new GraphicsManager(this, tag);
            Manager.Keys = new IniKeys();
            
            Manager.Inventory = new InventoryUtilities(this, tag, new DebugAPI(this));
            Manager.Utilities.Add(new FlightUtilities(tag));
            Manager.Utilities.Add(new GasUtilities());
            Manager.Utilities.Add(new PowerUtilities());
            Manager.Utilities.Add(new BlockUtilities(tag));
            Manager.Utilities.Add(new ThrustUtilities(tag));
            //Manager.Utilities.Add(new CoreWeaponUtilities());
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
                Manager.Update(argument, updateSource);


        }
    }
}
