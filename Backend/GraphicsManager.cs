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
        public IMyGridTerminalSystem Terminal;
        public IMyProgrammableBlock Me;

        public long
        Frame,
        RuntimeMSRounded;
        public double RuntimeMS;
        public string Tag, GCM, Name;

        public Dictionary<string, Action<SpriteData>> Commands;
        public HashSet<DisplayBase> Displays;
        public HashSet<InfoUtility> InfoUtilities; //hash set for now
        public List<IMyTerminalBlock> Blocks;
            
        public IniKeys Keys;
        internal StringBuilder Builder;

        bool frozen = false;
        #endregion

        public GraphicsManager(MyGridProgram program, string t)
        {
            Program = program;
            GCM = t;
            Terminal = program.GridTerminalSystem;
            Me = program.Me;
            Commands = new Dictionary<string, Action<SpriteData>>();
            Displays = new HashSet<DisplayBase>();
            InfoUtilities = new HashSet<InfoUtility>();
            Blocks = new List<IMyTerminalBlock>();
            Builder = new StringBuilder();
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            var p = new Parser();
            var result = new MyIniParseResult();
            if (p.CustomData(Me, out result))
            {
                Tag = p.String(GCM, "tag", GCM);
                Name = p.String(GCM, "group name", "Screen Control");
            }
            else throw new Exception($" PARSE FAILURE: {Me.CustomName} cd error {result.Error} at {result.LineNo}");
            Commands.Add("!def", (b) =>{ return; });
        }

        public void Clear()
        {
            Commands.Clear();
            Displays.Clear();
            Blocks.Clear();
        }


        public void Init()
        {
            Clear();
            Frame = 0;
            RuntimeMSRounded = 0;
            RuntimeMS = 0;
            Terminal.GetBlocksOfType(Blocks);

            foreach (InfoUtility utility in InfoUtilities)
                utility.Reset(Program);
            foreach (InfoUtility utility in InfoUtilities)
                utility.RegisterCommands(ref Commands);

           if (useCustomDisplays)
            {
                
                Keys.ResetKeys(); // lol. lmao
                List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
                var g = Terminal.GetBlockGroupWithName(Tag + " " + Name);
                if (g == null) throw new Exception($"Block group not found. Script is looking for \"{Tag} {Name}\".");
                g.GetBlocks(blocks);
                foreach (var block in blocks)
                {
                    Program.Echo(block.CustomName);
                    var dsp = new LinkedDisplay(block, ref Commands, ref Program, ref Keys);
                    Displays.Add(dsp);
                    dsp.Setup(block);
                    Program.Echo($"Parsing {block.CustomName}:");
                    foreach (var surface in dsp.Outputs)
                    {
                        Program.Echo($"SURFACE {surface.Key.DisplayName} LOADED\n");
                        Program.Echo($"SURFACE UPDATE {dsp.RefreshFreqencies[surface.Key]}");
                    }
                    //block.CustomData = InfoUtilities.EncodeSprites(ref dsp);
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
                arg = arg.ToLower();
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
                                Program.Runtime.UpdateFrequency = Utilities.Update;
                                frozen = true;
                                break;
                            }
                        }

                    default: { break; }
                }
            }
            var sflags = Utilities.Converter(source);
            var tflags = (UpdateFrequency)1;
            //intel subsystem(maybe?): DO SOMETHING!!!!
            foreach (LinkedDisplay dsp in Displays)
            {
                if ((dsp.UpdateFrequency & sflags) != 0)
                    dsp.Update(ref source);
                tflags |= dsp.UpdateFrequency;
            }
            Program.Runtime.UpdateFrequency = tflags;
                
           if (Frame > 1000)
            {
                string r = $"RUNS - {Frame}\nSRC - {source}\nRUNTIME - {Program.Runtime.LastRunTimeMs} ms";
                Program.Echo(r);
            }
        }
    }
}