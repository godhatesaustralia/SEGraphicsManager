using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;

namespace IngameScript
{
    static public class Keys
    {
        static public readonly string
             ScreenSection = "SECT_SCREEN",
             SpriteSection = "SECT_SPRITE",
             List = "LIST",
             Logo = "LOGO",
             Type = "TYPE",
             Data = "DATA",
             Size = "SIZE",
             Align = "ALIGN",
             Pos = "POS",
             Rotation = "ROTATION",
             Scale = "SCALE",
             Color = "COLOR",
             Font = "FONT",
             Format = "FORMAT",
             Command = "CMD",
             Update = "PRIORITY",
             Based = "BASED",
             Cringe = "VNLA",
             Prepend = "PREP",
             Append = "APP";
    }

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
            Manager.AddUtil(new BlockUtilities(tag));
            Manager.AddUtil(new ThrustUtilities(tag));

            //Manager.Utilities.Add(new CoreWeaponUtilities());
            Manager.Init();
        }

        public void Main(string argument, UpdateType updateSource)
        {
                Manager.Update(argument, updateSource);
        }
    }
}
