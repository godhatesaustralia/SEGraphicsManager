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
        WorstFrame;
        public double RuntimeMS, WorstRun, AverageRun;
        public string Tag, GCM, Name;
        Dictionary<string, Action<SpriteData>> Commands;
        List<DisplayBase> Displays, FastDisplays, Static;
        public List<InfoUtility> InfoUtilities; //hash set for now
        public InventoryUtilities Inventory;
        public List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
        HashSet<IMyTerminalBlock> DisplayBlocks = new HashSet<IMyTerminalBlock>();
        double totalRt;
        public IniKeys Keys;
        StringBuilder Builder;
        int dPtr, iPtr, min = 128;

        bool frozen = false, setupComplete, draw;
        #endregion

        public GraphicsManager(MyGridProgram program, string t)
        {
            Program = program;
            GCM = t;
            Terminal = program.GridTerminalSystem;
            Me = program.Me;
            Commands = new Dictionary<string, Action<SpriteData>>();
            Displays = new List<DisplayBase>();
            Static = new List<DisplayBase>();
            InfoUtilities = new List<InfoUtility>();
            Builder = new StringBuilder();
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            var p = new Parser();
            var result = new MyIniParseResult();
            if (p.CustomData(Me, out result))
            {
                Tag = p.String(GCM, "tag", GCM);
                Name = p.String(GCM, "group name", "Screen Control");
                FastDisplays = new List<DisplayBase>(p.Byte(GCM, "fast", 2));
            }
            else throw new Exception($" PARSE FAILURE: {Me.CustomName} cd error {result.Error} at {result.LineNo}");
            Commands.Add("!def", (b) => { return; });
        }

        public void Clear(bool auto)
        {
            dPtr = -1;
            if (auto) Commands.Clear();
            Displays.Clear();
            Blocks.Clear();
            DisplayBlocks.Clear();
        }


        public void Init(bool auto = true)
        {
            setupComplete = false;
            Clear(auto);
            Terminal.GetBlocksOfType(Blocks);
            Inventory.Reset(Program);
            foreach (InfoUtility utility in InfoUtilities)
                utility.Reset(Program);

            if (auto)
            {
                Frame = WorstFrame = 0;
                RuntimeMS = WorstRun = AverageRun = totalRt = 0;
                Inventory.Setup(ref Commands);
                foreach (InfoUtility utility in InfoUtilities)
                    utility.Setup(ref Commands);
            }

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
            }
        }

        void UpdateTimes()
        {
            RuntimeMS += Program.Runtime.TimeSinceLastRun.TotalMilliseconds;
            //RuntimeMSRounded = (long)RuntimeMS;
            Frame++;
        }

        void RunSetup()
        {
            if (DisplayBlocks == null || DisplayBlocks.Count == 0)
            {
                setupComplete = true; InfoUtility.justStarted = false;
            }
            else
            {
                var b = DisplayBlocks.First();
                var d = new LinkedDisplay(b, ref Commands, ref Program, ref Keys);
                d.Setup(b);
                DisplayBlocks.Remove(b);
                var p = d.Priority;
                if (p == Priority.High && FastDisplays.Count < FastDisplays.Capacity)
                    FastDisplays.Add(d);
                else if ((p & Priority.High) != 0)
                    Displays.Add(d);
                else Static.Add(d);
            }
        }
        void DataCycle(ref Priority p)
        {
            if (!Inventory.updated)
                Inventory.Update();
            draw = iPtr == 0;
            InfoUtilities[Util.Next(ref iPtr, InfoUtilities.Count)].Update();
        }
        void DrawCycle(ref Priority p)
        {
            Displays[Util.Next(ref dPtr, Displays.Count)].Update(ref p);
            if (dPtr == 0)
                Inventory.updated = draw = false;
        }

        void Reset(ref List<DisplayBase> ds)
        {
            if (ds.Count > 0)
                foreach(var d in ds)
                    d.Reset();
        }

        public void Update(string arg, UpdateType source)
        {
            UpdateTimes();
            if (!setupComplete)
                RunSetup();
            Priority p = Priority.Normal;
            p |= Frame % 10 == 0 ? Priority.Fast : Priority.None;
            if (arg != "")
            {
                arg = arg.ToLower();
                switch (arg)
                {
                    case "reset":
                        {
                            Init(false);
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
                                Program.Runtime.UpdateFrequency = Util.uDef;
                                frozen = true;
                                break;
                            }
                        }
                    case "flick":
                        {
                            Reset(ref FastDisplays);
                            Reset(ref Displays);
                            Reset(ref Static);
                            Init(false);
                            break;
                        }
                    default: { break; }
                }
            }
            if (!setupComplete) return;
            if ((p & Priority.Fast) != 0)
                foreach (var d in FastDisplays)
                    d.Update(ref p);
            if (!draw)
                DataCycle(ref p);
            else
                DrawCycle(ref p);

            var rt = Program.Runtime.LastRunTimeMs;
            if (WorstRun < rt) { WorstRun = rt; WorstFrame = Frame; }
            totalRt += rt;
            if (true)
            {
                if ((p & Priority.Fast) != 0)
                    AverageRun = totalRt / Frame;
                string n = "";
                foreach (var d in Displays)
                    n += $"{d.Name}\n";
                string r = "[[GRAPHICS MANAGER]]\n\n";
                if (draw) r += $"DRAWING DISPLAY {dPtr}/{Displays.Count}";
                else
                    r += $"INV {Inventory.Pointer}/{Inventory.Items.Count} | {InfoUtilities[iPtr].Name} - UTILS {iPtr}/{InfoUtilities.Count}";
                r += $"\nRUNS - {Frame}\nRUNTIME - {rt} ms\nAVG - {AverageRun.ToString("0.####")} ms\nWORST - {WorstRun} ms, F{WorstFrame}\n";
                Program.Echo(r);
            }
        }
    }
}