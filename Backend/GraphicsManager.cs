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
using System.Security.Cryptography;
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
    public class GraphicsManager
    {
        #region fields

        public MyGridProgram Program;
        public IMyGridTerminalSystem TerminalSystem;
        public IMyProgrammableBlock Me;
        public IMyCubeGrid HostShip;
        public IMyShipController Controller;

        public long
        Frame = 0,
        RuntimeMSRounded = 0;
        public double RuntimeMS = 0;

        public Dictionary<string, Action<SpriteData>> Commands;
        public HashSet<LinkedDisplay> Displays;
        public List<string> IniKeys;

        #endregion

        public GraphicsManager(MyGridProgram program)
        {
            Program = program;
            TerminalSystem = program.GridTerminalSystem;
            Me = program.Me;
            HostShip = Me.CubeGrid;
            TerminalSystem.GetBlocksOfType<IMyShipController>(null, (b) =>
            {
                if (b.CustomName.Contains("[I]"))
                    Controller = b;
                return true;
            });
            Commands = new Dictionary<string, Action<SpriteData>>();
            Displays = new HashSet<LinkedDisplay>();
            IniKeys = new List<string>(12);
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        
        public void RegisterCommands()
        {
            Commands.Add("test", (b) =>
            {

            });
        }

        #region CustomDataFormat

        // [KEYS]
        // K0 = [SCREEN 
        // K1 = [SPRITE 
        // K2 = >LIST 
        // K3 = >TYPE
        // K4 = >DATA
        // K5 = >SIZE
        // K6 = >ALIGN
        // K7 = >POSITION 
        // K8 = >ROTATION/SCALE
        // K9 = >COLOR 
        // K10 = >FONT 
        // K11 = >UPDATE

        #endregion

        public void Init()
        {

            RegisterCommands();
            //var section = "KEYS";
            //Parser myParser = new Parser();
            //MyIniParseResult Result;
            //if (myParser.TryParseCustomData(Me, out Result))
            //    if (myParser.ContainsSection(section))
            //    {

            //    }
            //else throw new Exception($" KEY PARSE FAILURE: {Result.Error} at {Result.LineNo}");
            
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            TerminalSystem.GetBlockGroupWithName("LCT Screen Control").GetBlocks(blocks);
            foreach (var block in blocks)
            {
                var display = new LinkedDisplay(block, ref Commands, ref Program);
                Displays.Add(display);
                display.Setup(block);
            }
        }

        void UpdateTimes()
        {
            RuntimeMS += Program.Runtime.TimeSinceLastRun.TotalMilliseconds;
            RuntimeMSRounded = (long)RuntimeMS;
            Frame++;
        }

        public void Update(UpdateType source)
        {
            UpdateTimes();
            //intel subsystem(maybe?): DO SOMETHING!!!!
            foreach (LinkedDisplay display in Displays)
                display.Update(ref source);
            Program.Echo($"[CYCLE: {Frame}]");
        }

    }
}