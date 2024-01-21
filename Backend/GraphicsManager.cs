using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.EntityComponents;
using Sandbox.Gui;
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

        public bool useCustomDisplays;

        public MyGridProgram Program;
        public IMyGridTerminalSystem TerminalSystem;
        public IMyProgrammableBlock Me;

        public long
        Frame,
        RuntimeMSRounded;
        public double RuntimeMS;
        public string tag;

        public Dictionary<string, Action<SpriteData>> Commands;
        public HashSet<DisplayBase> Displays;
        public HashSet<InfoUtility> Utilities; //hash set for now
        public List<IMyTerminalBlock> AllBlocks;
            
        public DisplayIniKeys Keys;
        internal StringBuilder Builder;

        bool frozen = false;
        #endregion

        public GraphicsManager(MyGridProgram program)
        {
            Program = program;
            TerminalSystem = program.GridTerminalSystem;
            Me = program.Me;
            Commands = new Dictionary<string, Action<SpriteData>>();
            Displays = new HashSet<DisplayBase>();
            Utilities = new HashSet<InfoUtility>();
            AllBlocks = new List<IMyTerminalBlock>();
            Builder = new StringBuilder();
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            var p = new Parser();
            var result = new MyIniParseResult();
            if (p.TryParseCustomData(Me, out result))
                tag = p.ParseString("GCM", "tag");
            else throw new Exception($" PARSE FAILURE: {Me.CustomName} cd error {result.Error} at {result.LineNo}");
        }

        public void Clear()
        {
            Commands.Clear();
            Displays.Clear();
            AllBlocks.Clear();
        }


        public void Init()
        {
            Clear();
            Frame = 0;
            RuntimeMSRounded = 0;
            RuntimeMS = 0;
            TerminalSystem.GetBlocksOfType(AllBlocks);

            foreach (InfoUtility utility in Utilities)
                utility.Reset(Program);
            foreach (InfoUtility utility in Utilities)
                utility.RegisterCommands(ref Commands);

           if (useCustomDisplays)
            {
                Keys.ResetKeys(); // lol. lmao
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                TerminalSystem.GetBlockGroupWithName(tag + " Screen Control").GetBlocks(blocks);
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
                    //block.CustomData = SharedUtilities.EncodeSprites(ref display);
                }
            }

            InfoUtility.justStarted = false;
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
                    case "freeze":
                        {
                            if (frozen)
                            {
                                Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
                                frozen = false;
                                break;
                            }
                            else
                            {
                                Program.Runtime.UpdateFrequency = SharedUtilities.defaultUpdate;
                                frozen = true;
                                break;
                            }
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
                Program.Echo($"runtime: {Program.Runtime.LastRunTimeMs} ms");
            }
        }
    }
}