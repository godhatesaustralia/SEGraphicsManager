using Sandbox.ModAPI.Ingame;
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
            
            Manager.Inventory = new InventoryUtilities(this, tag);
            Manager.Utilities.Add(new FlightUtilities(tag));
            Manager.Utilities.Add(new GasUtilities(ref Manager.Inventory));
            Manager.Utilities.Add(new PowerUtilities(ref Manager.Inventory));
            Manager.Utilities.Add(new WeaponUtilities());
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Manager.Update(argument, updateSource);
        }
    }
}
