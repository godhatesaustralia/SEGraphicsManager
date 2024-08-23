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

            Manager.AddUtil(new FlightUtilities(tag));
            Manager.AddUtil(new GasUtilities());
            Manager.AddUtil(new PowerUtilities());
            //Manager.AddUtil(new BlockUtilities(tag));
            //Manager.AddUtil(new ThrustUtilities(tag));

            //Manager.Utilities.Add(new CoreWeaponUtilities());
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
                Manager.Update(argument, updateSource);
        }
    }
}
