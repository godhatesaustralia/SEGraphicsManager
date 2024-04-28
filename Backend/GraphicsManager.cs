﻿using Sandbox.ModAPI.Ingame;
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

        public MyGridProgram Program;
        public IMyGridTerminalSystem Terminal;
        public IMyProgrammableBlock Me;
        public IMyShipController Controller;

        public string Tag, GCM, Name;

        Dictionary<string, Action<SpriteData>> Commands = new Dictionary<string, Action<SpriteData>>();
        Dictionary<string, bool> inUse = new Dictionary<string, bool>();
        List<DisplayBase> Displays, FastDisplays, Static;
        List<CoyLogo> logos = new List<CoyLogo>();

        public List<UtilityBase> Utilities;
        public InventoryUtilities Inventory;

        HashSet<IMyTerminalBlock> DisplayBlocks = new HashSet<IMyTerminalBlock>();

        public IniKeys Keys;
        public bool justStarted => !setupComplete;
       
        int dPtr, iPtr, // display pointers
            min = 256, fast; // min - frames to wait for echo, fast - determines Priority.Fast
        const int rtMax = 10; // theoretically accurate for update10
        double totalRt, RuntimeMS, WorstRun, AverageRun;
        Queue<double> runtimes = new Queue<double>(rtMax);
        bool frozen = false, setupComplete, draw, useCustomDisplays, useLogo, isCringe;
        long Frame, WorstFrame;
        public long F => Frame;
        Priority p;
        #endregion

        public GraphicsManager(MyGridProgram program, string t)
        {
            Program = program;
            GCM = t;
            Terminal = program.GridTerminalSystem;
            Me = program.Me;
            FastDisplays = new List<DisplayBase>();
            Displays = new List<DisplayBase>();
            Static = new List<DisplayBase>();
            Utilities = new List<UtilityBase>();
            var result = new MyIniParseResult();
            using (var p = new iniWrap())
                if (p.CustomData(Me, out result))
                {
                    Tag = p.String(GCM, "tag", GCM);
                    Name = p.String(GCM, "groupName", "Screen Control");
                    fast = 60 / p.Byte(GCM, "maxDrawPerSecond", 4);
                    useCustomDisplays = p.Bool(GCM, "custom", true);
                    isCringe = p.Bool(GCM, "vanillaFont", false);
                    useLogo = p.Bool(GCM, "logo", false);
                    var ctrl = p.String(GCM, "shipCTRL", "[I]");
                    Terminal.GetBlocksOfType<IMyShipController>(null, b =>
                    {
                        if ((b.CustomName.Contains(ctrl) || b.IsMainCockpit) && b.IsSameConstructAs(Program.Me))
                            Controller = b;
                        return true;
                    });
                }
                else throw new Exception($" PARSE FAILURE: {Me.CustomName} cd error {result.Error} at {result.LineNo}");
        }

        public void Clear(bool full)
        {
            dPtr = -1;
            iniWrap.total = 0;
            if (full) Commands.Clear();
            Displays.Clear();
            DisplayBlocks.Clear();
            Lib.GraphStorage.Clear();
            Lib.lights.Clear();
        }

        public void Init(bool full = true)
        {
            Program.Runtime.UpdateFrequency |= UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
            setupComplete = false;
            Clear(full);

            if (full)
            {
                Frame = WorstFrame = 0;
                RuntimeMS = WorstRun = AverageRun = totalRt = 0;
                Commands.Add("!def", b => { return; });
                Commands.Add("!date", b =>
                {
                    b.Data = DateTime.Now.Date.Date.ToShortDateString();
                });
                Commands.Add("!time", b =>
                {
                    var time = DateTime.Now.TimeOfDay;
                    b.Data = $"{timeFormat(time.Hours)}:{timeFormat(time.Minutes)}";
                });
                Commands.Add("!rt", b => b.SetData(AverageRun, "0.###"));

                foreach (UtilityBase utility in Utilities)
                {
                    utility.Reset(this, Program);
                    utility.Setup(ref Commands);
                }
            }
            else
            foreach (UtilityBase utility in Utilities)
                utility.Reset(this, Program);

            Inventory.Reset(this, Program);
            Inventory.Setup(ref Commands);

            if (useCustomDisplays)
                GetDisplays();

            RunSetup();

            if (full && DisplayBlocks.Count <= 6)
                while (DisplayBlocks.Count > 0)
                    RunSetup();
        }
        private string timeFormat(int num) => num < 10 ? $"0{num}" : $"{num}";
        private void UpdateTimes()
        {
            RuntimeMS += Program.Runtime.TimeSinceLastRun.TotalMilliseconds;
            //RuntimeMSRounded = (long)RuntimeMS;
            Frame++;
        }

        private void RunSetup()
        {
            if (DisplayBlocks.Count == 0)
            {
                setupComplete = true;
                if (Displays.Count == 0)
                {
                    Program.Runtime.UpdateFrequency = UpdateFrequency.None;
                    Program.Echo("No commands detected on active displays. Script shutting down.");
                    draw = false;
                }
            }
            else
            {
                var b = DisplayBlocks.First();
                var d = new LinkedDisplay(b, ref Commands, ref Program, ref Keys, isCringe);
                var p = Priority.None;
                var st = b.BlockDefinition.SubtypeName;
                if (useLogo && (st.Contains("LCDLarge") || st == "LargeFullBlockLCDPanel" || st == "LargeTextPanel")) // keen
                {
                    using (var ini = new iniWrap())
                    {
                        int c = logos.Count;
                        CoyLogo l;
                        if (ini.CustomData(b))
                        {
                            l = new CoyLogo(
                                b as IMyTextPanel,
                                ini.Bool(Keys.ScreenSection, "FRAME_L", false),
                                ini.Bool(Keys.ScreenSection, "TEXT_L", false),
                                ini.Bool(Keys.ScreenSection, "VCR_L", false));
                        }
                        else l = new CoyLogo(b as IMyTextPanel);
                        l.SetAnimate();
                        logos.Add(l);
                        p = d.Setup(b, true);
                        logos[c].color = d.logoColor;
                    }
                }
                else p = d.Setup(b);
                if (p == Priority.High && FastDisplays.Count < FastDisplays.Capacity)
                    FastDisplays.Add(d);
                else if ((p & Priority.Normal) != 0)
                    Displays.Add(d);
                else Static.Add(d);

                DisplayBlocks.Remove(b);
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

        private void SetPriorities(ref List<DisplayBase> ds)
        {
            if (ds.Count > 0)
                foreach (var d in ds)
                    d.SetPriority();
        }


        public void Update(string arg, UpdateType source)
        {
            UpdateTimes();
            if (!setupComplete)
                RunSetup();
            p = Priority.Normal;
            if (Frame <= min && useLogo)
            {
                draw = false;
                foreach (var l in logos)
                    l.Update("");
                if (Frame == min)
                {
                    //throw new Exception($"\nTOTAL {iniWrap.total} INICOUNT {iniWrap.IniCount} LIST COUNT {iniWrap.Count}");
                    SetPriorities(ref FastDisplays);
                    SetPriorities(ref Displays);
                    foreach (var d in Static)
                        d.ForceRedraw();
                    draw = true;
                    p |= Priority.Once;
                }
                Program.Echo($"RUNS - {Frame}\nPARSE CYCLES - {iniWrap.total}\nMYINI INSTANCES - {iniWrap.Count}\nFAILURES - {Lib.bsodsTotal}");
            }

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
                    case "vcr":
                        {
                            isCringe = !isCringe;
                            Init(false);
                            break;
                        }
                    case "freeze":
                    case "toggle":
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
                    default: { break; }
                }
            }
            if (!setupComplete) return;

            bool fast = (source & UpdateType.Update10) != 0;
            if (fast)
            {
                p |= Priority.Fast;
                foreach (var d in FastDisplays)
                    d.Update(ref p);
            }
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
            if (runtimes.Count == rtMax)
                runtimes.Dequeue();
            runtimes.Enqueue(rt);

            if (Frame > min)
            {
                if (fast)
                {
                    AverageRun = 0;
                    foreach (var qr in runtimes)
                        AverageRun += qr;
                    AverageRun /= rtMax;
                }
                string n = "";
                foreach (var d in Displays)
                    n += $"{d.Name}\n";
                string r = "[[GRAPHICS MANAGER]]\n\n";
                if (draw) r += $"DRAWING DISPLAY {dPtr + 1}/{Displays.Count}";
                else if (Inventory.needsUpdate)
                    r += $"INV {Inventory.Pointer}/{Inventory.Count}";
                else r += $"UTILS {iPtr + 1}/{Utilities.Count} - {Utilities[iPtr].Name}";

                r += $"\nRUNS - {Frame}\nRUNTIME - {rt} ms\nAVG - {AverageRun.ToString("0.####")} ms\nWORST - {WorstRun} ms, F{WorstFrame}";
                //r = Inventory.DebugString; 
                Program.Echo(r);
            }
        }
    }
}