using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Scripting;
using VRageMath;
using VRageRender;

namespace IngameScript
{
    public class PresetDisplay : DisplayBase
    {
        public PresetDisplay(IMyTerminalBlock block, ref Dictionary<string, Action<SpriteData>> commandsDict, ref MyGridProgram program) 
        {
            Commands = commandsDict;
            CommandUsers = new Dictionary<IMyTextSurface, HashSet<string>>();
            Refresh = new Dictionary<IMyTextSurface, Priority>();
            Outputs = new Dictionary<IMyTextSurface, Dictionary<string, SpriteData>>();
            Name = block.CustomName;
            dEID = block.EntityId;
            Program = program;
            // after this we manually fill out shit in display refresh freq, display outputs in program constructor. yeah it sucks but whatever u know
        }

        public override void Setup(IMyTerminalBlock block)
        {
            throw new Exception(" die");
        }

        public override void Update(ref Priority p)
        {
            throw new Exception(" die");
        }
    }
}