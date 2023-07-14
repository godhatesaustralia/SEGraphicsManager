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
        public List<IMyTerminalBlock> AllBlocks, InventoryBlocks;
        public List<string> IniKeys;

        bool justStarted = true; //shit ass bool for init
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
            AllBlocks = new List<IMyTerminalBlock>();
            IniKeys = new List<string>(12);
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        
        public void RegisterCommands()
        {
            
            Commands.Add("!h2%", (b) =>
            {
                if (justStarted) throw new Exception($" {b == null}");
                var last = b.Data;
                List<IMyGasTank> tanks = new List<IMyGasTank>();
                TerminalSystem.GetBlocksOfType(tanks);
                var amt = 0d;
                var total = amt;
                foreach (var tank in tanks)
                {
                    amt += tank.FilledRatio * tank.Capacity;
                    total += tank.Capacity;
                }
                var pct = amt / total;
                b.Data = pct.ToString("#0.##%"); //keep this shrimple for now
            });
            Commands.Add("!ammo", (b) =>
            {
                MyItemType ammoType = new MyItemType();
                if (justStarted)
                {
                    b.Data = b.Data.Trim();
                    var ammos = new string[]
                    {
                    "AutocannonClip",
                    "NATO_25x184mm",
                    "Missile200mm",
                    "MediumCalibreAmmo",
                    "LargeCalibreAmmo",
                    "SmallRailgunAmmo",
                    "LargeRailgunAmmo"
                    };
                    foreach (var ammo in ammos)        
                        if (b.Data == ammo)
                            ammoType = new MyItemType("MyObjectBuilder_AmmoMagazine", ammo);
                    //throw new Exception(" DIE");
                    //if (ammoType == null) ammoType = MyItemType.MakeAmmo(ammos[1]); this seem return null....
                    
                }
                var amt = 0;
                foreach (var block in AllBlocks)
                    SharedUtilities.TryGetItem(block, ammoType, ref amt);
                b.Data = amt.ToString();
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
            TerminalSystem.GetBlocksOfType(AllBlocks);
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
                foreach (var surface in display.DisplayOutputs)
                {
                    Program.Echo($"SURFACE {surface.Key.DisplayName} LOADED\n");
                }      
            }
            justStarted = false;
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

            var sourceflags = SharedUtilities.UpdateConverter(source);
            
            //intel subsystem(maybe?): DO SOMETHING!!!!
            foreach (LinkedDisplay display in Displays)
                if ((display.UpdateFrequency & sourceflags) != 0)
                    display.Update(ref source);
                
           if (Frame > 1000)
                Program.Echo($"<CYCLE: {Frame}>");
        }

    }
}