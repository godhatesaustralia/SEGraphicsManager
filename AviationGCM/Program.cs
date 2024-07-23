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

            Manager.AddUtil(new PowerUtilities());
            Manager.AddUtil(new EngineUtilities(tag));
            Manager.AddUtil(new CoreWeaponUtilities());
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            Manager.Update(argument, updateSource);
        }
    }
}
