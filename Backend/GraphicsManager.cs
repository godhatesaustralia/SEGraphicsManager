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
        Frame,
        RuntimeMSRounded;
        public double RuntimeMS;

        public Dictionary<string, Action<SpriteData>> Commands;
        public Dictionary<long, MyItemType[]> ItemStorage;
        public HashSet<LinkedDisplay> Displays;
        public List<IMyTerminalBlock> AllBlocks, InventoryBlocks;
            
        public DisplayIniKeys Keys;
        public HydroloxUtilities TankUtils;

        internal const string
            myObjectBuilderString = "MyObjectBuilder";
        internal const char
            commandSplit = '$',
            space = ' ';
        internal string[] ammoNames = new string[]
        {
            "[ACN",
            "[GAT",
            "[RKT",
            "[ASL",
            "[ART",
            "[SRG",
            "[LRG"
        };
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
            ItemStorage = new Dictionary<long, MyItemType[]>();
            Displays = new HashSet<LinkedDisplay>();
            AllBlocks = new List<IMyTerminalBlock>();
            InventoryBlocks = new List<IMyTerminalBlock>();
            Keys = new DisplayIniKeys();
            Builder = new StringBuilder();
            TankUtils = new HydroloxUtilities();
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        public void Clear()
        {
            Commands.Clear();
            ItemStorage.Clear();
            Displays.Clear();
            AllBlocks.Clear();
            InventoryBlocks.Clear();
        }

        // SO...command formatting. Depends on the general command, but here's the idea
        // this is all for the K_DATA field of the sprite.
        // <required param 1>$<required param 2>$...$<required param n>
        public void RegisterCommands()
        {
            
            Commands.Add("!h2%", (b) =>
            {
                b.Data = HydroloxUtilities.HydrogenStatus().ToString("#0.##%"); //keep this shrimple for now
            });

            Commands.Add("!h2t", (b) =>
            {
                b.Data = TankUtils.HydrogenTime(Program.Runtime.TimeSinceLastRun, Program);
            });

            Commands.Add("!ammo", (b) =>
            {
                var amt = 0;
               
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
                            ItemStorage.Add(b.UniqueID, new MyItemType[] { new MyItemType($"{myObjectBuilderString}_AmmoMagazine", ammo) });
                    //throw new Exception(" DIE");
                    //if (ammoType == null) ammoType = MyItemType.MakeAmmo(ammos[1]); this seem return null....
                    
                }  
                foreach (var block in InventoryBlocks)
                    SharedUtilities.TryGetItem(block, ItemStorage[b.UniqueID][0], ref amt);
                b.Data = amt.ToString();

                if (!b.UseStringBuilder)      
                    return;
                
                if (b.BuilderPrepend.Length > 0)
                    b.Data = $"{b.BuilderPrepend} {b.Data}";

                if (b.BuilderAppend.Length > 0)
                    b.Data = $"{b.Data} {b.BuilderAppend}";
            });

            Commands.Add("!ammos", (b) =>
            {
                if (!b.UseStringBuilder)
                    return;

                if (justStarted)
                {
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
                    ItemStorage.Add(b.UniqueID, new MyItemType[7]);
                    for (int i = 0; i < 6; ++i) //it's okay!! becausawe im drunk
                            ItemStorage[b.UniqueID][i] = new MyItemType($"{myObjectBuilderString}_AmmoMagazine", ammos[i]);
                }
                for (int i = 0; i < 6; ++i)
                {
                    var amt = 0;
                    foreach (var block in InventoryBlocks)
                    {
                        SharedUtilities.TryGetItem(block, ItemStorage[b.UniqueID][i], ref amt);
                        Me.CustomData += $"{block.CustomName} = {amt} // {i}\n";
                    }
                        
                    if (amt > 0)
                    {
                        var data = amt.ToString();
                        Builder.AppendLine($"{ammoNames[i]} {data}{b.BuilderAppend}");
                    }
                }

                b.Data = Builder.ToString();
                Builder.Clear();
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
                foreach (var block in InventoryBlocks)
                    SharedUtilities.TryGetItem(block, itemType, ref amt);
                b.Data = amt.ToString();
            });
        }

        public void Init()
        {
            Clear();
            HydroloxUtilities.GetBlocks(TerminalSystem);
            Frame = 0;
            RuntimeMSRounded = 0;
            RuntimeMS = 0;
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

        public void Update(string arg, UpdateType source)
        {
            UpdateTimes();
            if (arg != "")
            {
                switch (arg)
                {
                    case "reset":
                        {
                            Init();
                            break;
                        }
                    default: { break; }
                }
            }
            var sourceflags = SharedUtilities.UpdateConverter(source);
            var targetflags = (UpdateFrequency)1;
            //intel subsystem(maybe?): DO SOMETHING!!!!
            foreach (LinkedDisplay display in Displays)
            {
                if ((display.UpdateFrequency & sourceflags) != 0)
                    display.Update(ref source);
                targetflags |= display.UpdateFrequency;
            }

            Program.Runtime.UpdateFrequency = targetflags;
                
           if (Frame > 1000)
            {
                Program.Echo($"cycle: {Frame}");
                Program.Echo($"source: {source}");
                Program.Echo($"runtime: {Program.Runtime.LastRunTimeMs}");
            }
                
           
        }

    }
}