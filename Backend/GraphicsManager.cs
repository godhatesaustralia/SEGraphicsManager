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
using System.Dynamic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Schema;
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
        public double RuntimeMS, WorstRun;
        public string Tag, GCM, Name;
        public int DisplayCount = 0;

        public Dictionary<string, Action<SpriteData>> Commands;
        public HashSet<DisplayBase> Displays;
        DisplayBase[] displays; 
        public HashSet<InfoUtility> InfoUtilities; //hash set for now
        public List<IMyTerminalBlock> 
            Blocks = new List<IMyTerminalBlock>(), 
            DisplayBlocks = new List<IMyTerminalBlock>();

        public IniKeys Keys;
        internal StringBuilder Builder;
        int ptr, inc = Utilities.increment, turns, c = 0;
        UpdateType src = UpdateType.Update1 | UpdateType.Update100;
        UpdateFrequency flg = UpdateFrequency.Update100;
        bool frozen = false, spreadUpdates = false, done = false;
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
            DisplayBlocks = null;
            DisplayBlocks = new List<IMyTerminalBlock>();
            ptr = 0;
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
                var g = Terminal.GetBlockGroupWithName(Tag + " " + Name);
                if (g == null) 
                    throw new Exception($"Block group not found. Script is looking for \"{Tag} {Name}\".");

                g.GetBlocks(DisplayBlocks);
                DisplayCount = DisplayBlocks.Count;
                spreadUpdates = DisplayCount > (2 * inc);
                done = !spreadUpdates;
                displays = spreadUpdates ? new DisplayBase[DisplayBlocks.Count] : null;
                turns = spreadUpdates ? 1 + (displays.Length / inc) : 0;
                Program.Echo("If you want to actually read this, get Digi's Build Info mod and use it to copy this text.\nThe requisite button should be to the bottom right of where this text is showing up.\n\n");
                if (!spreadUpdates) // goofy
                    foreach (var block in DisplayBlocks)
                        addDisplay(block);
                else
                {
                    LongInit();
                    return;
                }
                InfoUtility.justStarted = !done;
            }
        }
        private void addDisplay(IMyTerminalBlock b, int i = -1)
        {
            Program.Echo($"\n\nParsing {b.CustomName}:");
            var dsp = new LinkedDisplay(b, ref Commands, ref Program, ref Keys);
            dsp.Setup(b);
            //b.CustomData = InfoUtilities.EncodeSprites(ref dsp);
            if (i == -1)
                Displays.Add(dsp);
            else if (displays != null && i < displays.Length)
            {
 //               Terminal.GetBlockWithName("Cockpit [I]").CustomData += $"AT {i} PTR {ptr}, TOTAL {DisplayBlocks.Count} {done}\n";
                displays[i] = dsp;
            }
        }
        private void UpdateTimes()
        {
            RuntimeMS += Program.Runtime.TimeSinceLastRun.TotalMilliseconds;
            RuntimeMSRounded = (long)RuntimeMS;
            Frame++;
        }
        private bool LongInit()
        {
            if (done) return true; // goofy
            for (int i = ptr; i < (ptr + inc) && i < displays.Length; i++)
                    addDisplay(DisplayBlocks[i], i);
            ptr += inc;
            done = ptr >= DisplayBlocks.Count && ptr >= displays.Length;
            return done;
        }

        public void Update(string arg, UpdateType source)
        {
            if (!done)
                if (!LongInit())
                    return;
                else { ptr = 0; InfoUtility.justStarted = false; }
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
                                Program.Runtime.UpdateFrequency = Utilities.uDef;
                                frozen = true;
                                break;
                            }
                        }

                    default: { break; }
                }
            }
            bool 
                start = Frame < 20,
                cycle = c != 0;
            var sflags = Utilities.Converter(source);
            var tflags = (UpdateFrequency)1;
            if (!spreadUpdates)
                foreach (LinkedDisplay dsp in Displays)
                {
                    if ((dsp.UpdateFrequency & sflags) != 0)
                        dsp.Update(ref source);
                    tflags |= dsp.UpdateFrequency;
                }
            else
            {
                if (start || (sflags & flg) != 0)
                {
                    cycle = true;
                    c = turns;
                }
                else c = 0;
                    //string s = "";
                for (int i = ptr; i < (ptr + inc) && i < displays.Length; i++)
                    if (cycle)
                    {
                        displays[i].Update(ref src);
                        tflags |= displays[i].UpdateFrequency;
                        
                    }
                if (cycle && c != turns) c--;
                ptr++;
                ptr = ptr >= DisplayCount ? 0 : ptr;
            }
            Program.Runtime.UpdateFrequency = tflags;
            if (Frame > 256)
            {
                var rt = Program.Runtime.LastRunTimeMs;
                if (WorstRun < rt) WorstRun = rt;
                string r = "[[GRAPHICS MANAGER]]\n\n";
                r += $"RUNS - {Frame}\nSRC - {source}" + $"\nRUNTIME - {rt} ms\nWORST RUNTIME - {WorstRun} ms\nSCREEN PROVIDERS - {DisplayCount}";
                Program.Echo(r);
            }
        }
    }
}