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
        public DisplayIniKeys Keys;

        internal const string
            myObjectBuilderString = "MyObjectBuilder";
        internal const char
            commandSplit = '$',
            space = ' ';
        internal StringBuilder Builder;
        


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
            Keys = new DisplayIniKeys();
            Builder = new StringBuilder();
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }
        // SO...command formatting. Depends on the general command, but here's the idea
        // this is all for the K_DATA field of the sprite.
        // <required param 1>$<required param 2>$...$<required param n>
        public void RegisterCommands()
        {
            
            Commands.Add("!h2%", (b) =>
            {
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
                var amt = 0;
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
                            ammoType = new MyItemType($"{myObjectBuilderString}_AmmoMagazine", ammo);
                    //throw new Exception(" DIE");
                    //if (ammoType == null) ammoType = MyItemType.MakeAmmo(ammos[1]); this seem return null....
                    
                }  
                foreach (var block in AllBlocks)
                    SharedUtilities.TryGetItem(block, ammoType, ref amt);
                b.Data = amt.ToString();

                if (!b.UseStringBuilder)      
                    return;
                
                if (b.BuilderPrepend.Length > 0)
                    b.Data = $"{b.BuilderPrepend} {b.Data}";

                if (b.BuilderAppend.Length > 0)
                    b.Data = $"{b.Data} {b.BuilderAppend}";
            });

            Commands.Add("!item", (b) =>
            {
                var amt = 0;
                MyItemType itemType = new MyItemType();
                if (justStarted)
                    if (b.Data.Contains(commandSplit))
                    {
                       b.Data = b.Data.Trim();
                       var stringParts = b.Data.Split(commandSplit);
                       itemType = new MyItemType($"{myObjectBuilderString}_{stringParts[0]}", stringParts[1]);
                    }
                foreach (var block in AllBlocks)
                    SharedUtilities.TryGetItem(block, itemType, ref amt);
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
            Keys.ResetKeys(); // lol. lmao
            TerminalSystem.GetBlocksOfType(AllBlocks);
            TerminalSystem.GetBlocksOfType(InventoryBlocks, (b) => b.HasInventory);//WHY ISN'T IT POSSIBLE
            RegisterCommands();
            
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            TerminalSystem.GetBlockGroupWithName("LCT Screen Control").GetBlocks(blocks);
            foreach (var block in blocks)
            {
                var display = new LinkedDisplay(block, ref Commands, ref Program, ref Keys);
                Displays.Add(display);
                display.Setup(block);
                foreach (var surface in display.DisplayOutputs)
                {
                    Program.Echo($"SURFACE {surface.Key.DisplayName} LOADED\n");
                    Program.Echo($"SURFACE UPDATE {display.DisplayRefreshFreqencies[surface.Key]}");
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