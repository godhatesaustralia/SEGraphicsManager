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
using System.Diagnostics;
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
        public double RuntimeMS, WorstRun, AverageRun;
        public string Tag, GCM, Name;
        public TimeSpan Reference;
        public Dictionary<string, Action<SpriteData>> Commands;
        public List<DisplayBase> Displays, Pr10, Pr100;
        public HashSet<InfoUtility> InfoUtilities; //hash set for now
        public List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
        HashSet<IMyTerminalBlock> DisplayBlocks = new HashSet<IMyTerminalBlock>();
        Queue<double> runtimes = new Queue<double>();

        public IniKeys Keys;
        StringBuilder Builder;
        int ptr, min = 128;
        int Current
        {
            get
            {
                if (ptr < Displays.Count)
                    ptr++;
                if (ptr == Displays.Count)
                    ptr = 0;
                return ptr;
            }
        }
        bool frozen = false, classic = false, setupComplete = false, didThis = false;
        #endregion

        public GraphicsManager(MyGridProgram program, string t)
        {
            Program = program;
            GCM = t;
            Terminal = program.GridTerminalSystem;
            Me = program.Me;
            Commands = new Dictionary<string, Action<SpriteData>>();
            Displays = new List<DisplayBase>();
            InfoUtilities = new HashSet<InfoUtility>();
            Builder = new StringBuilder();
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            var p = new Parser();
            var result = new MyIniParseResult();
            if (p.CustomData(Me, out result))
            {
                Tag = p.String(GCM, "tag", GCM);
                Name = p.String(GCM, "group name", "Screen Control");
                classic = !p.Bool(GCM, "cycle", true);
                if (classic)
                    Pr10 = null;
                else
                    Pr10 = new List<DisplayBase>(p.Byte(GCM, "p10", 2));
            }
            else throw new Exception($" PARSE FAILURE: {Me.CustomName} cd error {result.Error} at {result.LineNo}");
            Commands.Add("!def", (b) => { return; });
        }

        public void Clear()
        {
            Commands.Clear();
            Displays.Clear();
            Blocks.Clear();
            DisplayBlocks.Clear();
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
                var l = new List<IMyTerminalBlock>();
                g.GetBlocks(l);
                DisplayBlocks = l.ToHashSet();
                Program.Echo("If you want to actually read this, get Digi's Build Info mod and use it to copy this text.\nThe requisite button should be to the bottom right of where this text is showing up.\n\n");
                if (classic)
                {
                    
                    foreach (var block in DisplayBlocks)
                    {
                        var d = new LinkedDisplay(block, ref Commands, ref Program, ref Keys);
                        d.Setup(block);
                        Displays.Add(d);
                    }                
                    DisplayBlocks.Clear();
                }
                setupComplete = classic;
                InfoUtility.justStarted = !classic;
            }
        }

        void createDisplay(IMyTerminalBlock b)
        {
            var d = new LinkedDisplay(b, ref Commands, ref Program, ref Keys);
            d.Setup(b);
            DisplayBlocks.Remove(b);
            if (classic)
                Displays.Add(d);
            else
            {
                var p = (byte)d.Priority;
                if (p == 1 && Pr10.Count < Pr10.Capacity)
                    Pr10.Add(d);
                else if (p != 0)
                    Displays.Add(d);
            }
        }

        void UpdateTimes()
        {
            Reference = Program.Runtime.TimeSinceLastRun;
            RuntimeMS += Reference.TotalMilliseconds;
            RuntimeMSRounded = (long)RuntimeMS;
            Frame++;
        }

        void RunSetup()
        {
            if (DisplayBlocks == null || DisplayBlocks.Count == 0)
            { setupComplete = true; InfoUtility.justStarted = false; }
            else
                createDisplay(DisplayBlocks.First());
        }

        void FastDraw(ref Priority p)
        {
            if ((p & Priority.High) != 0 && Pr10.Count > 0)
                foreach (var d in Pr10)
                    d.Update(ref p);

            Displays[Current].Update(ref p);
        }
        void ClassicDraw(ref Priority p)
        {
            foreach (var d in Displays)
                d.Update(ref p);
        }
        public void Update(string arg, UpdateType source)
        {
            UpdateTimes();
            if (!classic && !setupComplete)
                RunSetup();
            Priority p = Priority.Low;
            p |= Frame % 10 == 0 ? Priority.High : Priority.None;
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
            if (!setupComplete) return;
            if (classic)
                ClassicDraw(ref p);
            else
                FastDraw(ref p);
            if (Frame > min)
            {
                var rt = Program.Runtime.LastRunTimeMs;
                runtimes.Enqueue(rt);
                if (runtimes.Count > 10) runtimes.Dequeue();
                if (Frame % 10 == 0)
                {
                    foreach (var t in runtimes)
                        AverageRun += t;
                    AverageRun /= runtimes.Count;
                }
                if (WorstRun < rt) WorstRun = rt;
                string n = "";
                foreach (var d in Displays)
                    n += $"{d.Name}\n";
                string r = $"[[GRAPHICS MANAGER]]\n{(classic ? "CLASSIC" : "PERFORMANCE")} MODE\n";
                r += $"RUNS - {Frame}\nSRC - {source}" + $"\nAVG RUNTIME - {AverageRun.ToString("0.####")} ms\nWORST RUNTIME - {WorstRun} ms\nACTIVE SCREENS - {n}";
                Program.Echo(r);
            }
        }
    }
}