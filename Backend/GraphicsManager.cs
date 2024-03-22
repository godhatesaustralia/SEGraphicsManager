using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public class GraphicsManager
    {
        #region fields

        public bool useCustomDisplays;
        public MyGridProgram Program;
        public IMyGridTerminalSystem Terminal;
        public IMyProgrammableBlock Me;

        public string Tag, GCM, Name;

        Dictionary<string, Action<SpriteData>> Commands;
        List<DisplayBase> Displays, FastDisplays, Static;

        public List<UtilityBase> Utilities;
        public InventoryUtilities Inventory;

        public List<IMyTerminalBlock> Blocks = new List<IMyTerminalBlock>();
        HashSet<IMyTerminalBlock> DisplayBlocks = new HashSet<IMyTerminalBlock>();
  
        public IniKeys Keys;
        public bool justStarted => !setupComplete;

        private int dPtr, iPtr, // display pointers
            min = 96, fast; // min - frames to wait for echo, fast - determines Priority.Fast
        private double totalRt, RuntimeMS, WorstRun, AverageRun;
        private bool frozen = false, setupComplete, draw;
        private long Frame, WorstFrame;
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
            Utilities = new List<UtilityBase>();
            var p = new iniWrap();
            var result = new MyIniParseResult();
            if (p.CustomData(Me, out result))
            {
                Tag = p.String(GCM, "tag", GCM);
                Name = p.String(GCM, "groupName", "Screen Control");
                fast = 60 / p.Byte(GCM, "maxDrawPerSecond", 4);
                FastDisplays = new List<DisplayBase>();
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
            Lib.GraphStorage.Clear();
            Lib.lights.Clear();
        }

        public void Init(bool auto = true)
        {
            Program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
            setupComplete = false;
            Clear(auto);
            if (auto)
            {
                Terminal.GetBlocksOfType(Blocks);
                Frame = WorstFrame = 0;
                RuntimeMS = WorstRun = AverageRun = totalRt = 0;
                Inventory.Setup(ref Commands);
                foreach (UtilityBase utility in Utilities)
                    utility.Setup(ref Commands);
            }
            //Lib.lockdown(this);
            Inventory.Reset(this, Program);
            foreach (UtilityBase utility in Utilities)
                utility.Reset(this, Program);

            if (useCustomDisplays)
                GetDisplays();

            RunSetup();

            if (auto && DisplayBlocks.Count <= 6)
                while (DisplayBlocks.Count > 0)
                    RunSetup();
        }

        private void UpdateTimes()
        {
            RuntimeMS += Program.Runtime.TimeSinceLastRun.TotalMilliseconds;
            //RuntimeMSRounded = (long)RuntimeMS;
            Frame++;
        }

        private void RunSetup()
        {
            if (DisplayBlocks.Count == 0)
                setupComplete = true;
            else
            {
                var b = DisplayBlocks.First();
                var d = new LinkedDisplay(b, ref Commands, ref Program, ref Keys);
                d.Setup(b);
                DisplayBlocks.Remove(b);
                var p = d.Priority;
                if (p == Priority.High && FastDisplays.Count < FastDisplays.Capacity)
                    FastDisplays.Add(d);
                else if ((p & Priority.Normal) != 0)
                    Displays.Add(d);
                else Static.Add(d);
            }
        }

        private void GetDisplays()
        {
            Keys.ResetKeys(); // lol. lmao
            var g = Terminal.GetBlockGroupWithName(Tag + " " + Name);
            if (g == null)
                throw new Exception($"Block group not found. Script is looking for \"{Tag} {Name}\".");
            var l = new List<IMyTerminalBlock>();
            g.GetBlocks(l);
            DisplayBlocks = l.ToHashSet();
        }

        private void Wipe(ref List<DisplayBase> ds)
        {
            if (ds.Count > 0)
                foreach (var d in ds)
                    d.Reset();
        }

        public void Update(string arg, UpdateType source)
        {
            UpdateTimes();
            if (!setupComplete)
                RunSetup();
            var p = Priority.Normal;
            p |= Frame % fast == 0 ? Priority.Fast : Priority.None;
            if (arg != "")
            {
                arg = arg.ToLower();
                switch (arg)
                {
                    case "restart":
                        {
                            Init();
                            break;
                        }
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
                                Program.Runtime.UpdateFrequency = Lib.uDef;
                                frozen = true;
                                break;
                            }
                        }
                    case "slow":
                        {
                            Program.Runtime.UpdateFrequency = UpdateFrequency.Update100;
                            break;
                        }
                    case "wipe":
                        {
                            Wipe(ref FastDisplays);
                            Wipe(ref Displays);
                            Wipe(ref Static);
                            Program.Echo("All displays wiped, ready to restart.");
                            Program.Runtime.UpdateFrequency = Lib.uDef;
                            return;
                        }
                    case "bm":
                        {
                            //Lib.lockdown(this);
                            break;
                        }
                    default: { break; }
                }
            }
            if (!setupComplete) return;
            if ((p & Priority.Fast) != 0)
                foreach (var d in FastDisplays)
                    d.Update(ref p);
            if (draw)
            {
                Displays[Lib.Next(ref dPtr, Displays.Count)].Update(ref p);
                if (dPtr == 0)
                {
                    Inventory.needsUpdate = true;
                    draw = false;
                }
            }
            else if (Inventory.needsUpdate)
                Inventory.Update();
            else
            {
                Utilities[Lib.Next(ref iPtr, Utilities.Count)].Update();
                draw = iPtr == 0;
            }
            var rt = Program.Runtime.LastRunTimeMs;
            if (WorstRun < rt) { WorstRun = rt; WorstFrame = Frame; }
            totalRt += rt;
            if (Frame > min)
            {
                if ((p & Priority.Fast) != 0)
                    AverageRun = totalRt / Frame;
                string n = "";
                foreach (var d in Displays)
                    n += $"{d.Name}\n";
                string r = "[[GRAPHICS MANAGER]]\n\n";
                if (draw) r += $"DRAWING DISPLAY {dPtr + 1}/{Displays.Count}";
                else if (Inventory.needsUpdate)
                    r += $"INV {Inventory.Pointer}/{Inventory.Items.Count}";
                else r += $"UTILS {iPtr + 1}/{Utilities.Count} - {Utilities[iPtr].Name}";

                r += $"\nRUNS - {Frame}\nRUNTIME - {rt} ms\nAVG - {AverageRun.ToString("0.####")} ms\nWORST - {WorstRun} ms, F{WorstFrame}\n";
                Program.Echo(r);
            }
        }
    }
}