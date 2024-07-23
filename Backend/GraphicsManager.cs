using Microsoft.VisualBasic;
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public class GraphicsManager
    {
        #region fields
        // the important ones
        // pinkie swear to not modify externally
        public MyGridProgram Program;
        public IMyGridTerminalSystem Terminal;
        public IMyProgrammableBlock Me;
        public IMyShipController Controller;

        public string Tag, GCM, Name;
        public Dictionary<string, Action<SpriteData>> Commands = new Dictionary<string, Action<SpriteData>>();
        //Dictionary<string, bool> inUse = new Dictionary<string, bool>();
        // name to  (list #, list index)
        Dictionary<string, MyTuple<int, int>> _displaysMaster = new Dictionary<string, MyTuple<int, int>>();
        List<Display> Displays, FastDisplays, Static;
        List<CoyLogo> logos = new List<CoyLogo>();
        MyCommandLine _cmd = new MyCommandLine();
        List<UtilityBase> Utilities;
        InventoryUtilities Inventory;

        HashSet<IMyTerminalBlock> DisplayBlocks = new HashSet<IMyTerminalBlock>();

        public bool justStarted => !setupComplete;
       
        int dPtr, iPtr, // display pointers
            min, fast; // min - frames to wait for echo, fast - determines BlockRefresh.Fast
        const int rtMax = 10; // theoretically accurate for update10
        double totalRt, RuntimeMS, WorstRun, AverageRun;
        Queue<double> runtimes = new Queue<double>(rtMax);
        bool frozen = false, setupComplete, draw, useLogo, isCringe, larp;
        TimeSpan addLarp;
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
            FastDisplays = new List<Display>();
            Displays = new List<Display>();
            Static = new List<Display>();
            Utilities = new List<UtilityBase>();
            Inventory = new InventoryUtilities(t, new DebugAPI(program));
            if (DateTime.Now.Date.Year % 4 == 0)
                addLarp = TimeSpan.FromDays(36524);
            else addLarp = TimeSpan.FromDays(36525);
            var result = new MyIniParseResult();
            using (var p = new iniWrap())
                if (p.CustomData(Me, out result))
                {
                    Tag = p.String(GCM, "tag", GCM);
                    Name = p.String(GCM, "groupName", "Screen Control");
                    fast = 60 / p.Int(GCM, "maxDrawPerSecond", 4);
                    min = p.Int(GCM, "minFrame", 256);
                    isCringe = p.Bool(GCM, "vanillaFont", false);
                    useLogo = p.Bool(GCM, "logo", false);
                    larp = p.Bool(GCM, "larp", true);
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

        void Clear(bool full)
        {
            dPtr = -1;
            iniWrap.total = 0;
            if (full) Commands.Clear();
            Displays.Clear();
            DisplayBlocks.Clear();
            _displaysMaster.Clear();
            _cmd.Clear();
            Lib.GraphStorage.Clear();
        }

        public void AddUtil(UtilityBase b) => Utilities.Add(b);

        public void Init(bool full = true)
        {
            Program.Runtime.UpdateFrequency |= UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
            setupComplete = false;
            Clear(full);
            if (Me.CubeGrid.IsStatic) // lazy hack bullshit
                Utilities.Remove(Utilities.Find(b => b is FlightUtilities));
            if (full)
            {
                Frame = WorstFrame = 0;
                RuntimeMS = WorstRun = AverageRun = totalRt = 0;
                Commands.Add("!def", b => { return; }); // safety
                Commands.Add("!date", b => b.Data = (larp ? DateTime.Now + addLarp : DateTime.Now).Date.Date.ToShortDateString());
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
            
            var g = Terminal.GetBlockGroupWithName(Tag + " " + Name);
            if (g == null)
                throw new Exception($"Block group not found. Script is looking for \"{Tag} {Name}\".");
            var l = new List<IMyTerminalBlock>();
            g.GetBlocks(l);
            DisplayBlocks = l.ToHashSet();
            RunSetup();

            if (full && DisplayBlocks.Count <= 6)
                while (DisplayBlocks.Count > 0)
                    RunSetup();
        }
        string timeFormat(int num) => num < 10 ? $"0{num}" : $"{num}";

        void RunSetup()
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
                var d = new Display(b, this, isCringe);
                var p = Priority.None;
                var st = b.BlockDefinition.SubtypeName;
                if (useLogo && (st.Contains("LCDLarge") || st == "LargeFullBlockLCDPanel" || st == "LargeTextPanel")) // keen
                {
                    using (var ini = new iniWrap())
                    {
                        int c = logos.Count;
                        CoyLogo l;
                        if (ini.CustomData(b) && !ini.Bool(Keys.SurfaceSection, "IGNORE_L", false))
                        {
                            l = new CoyLogo(
                                b as IMyTextPanel,
                                ini.Bool(Keys.SurfaceSection, "FRAME_L", false),
                                ini.Bool(Keys.SurfaceSection, "TEXT_L", false),
                                ini.Bool(Keys.SurfaceSection, "VCR_L", false));
                            l.SetAnimate();
                            p = d.Setup(b, true);
                            logos.Add(l);
                            logos[c].color = d.logoColor;
                        }
                        else 
                        p = d.Setup(b);
                    }
                }
                else p = d.Setup(b);
                if (p == Priority.Fast && FastDisplays.Count < FastDisplays.Capacity)
                {
                    FastDisplays.Add(d);
                    _displaysMaster[d.Name] = MyTuple.Create(0, FastDisplays.IndexOf(d));
                }
                else if ((p & Priority.Normal) != 0)
                {
                    Displays.Add(d);
                    _displaysMaster[d.Name] = MyTuple.Create(1, Displays.IndexOf(d));
                }
                else
                {
                    Static.Add(d);
                    _displaysMaster[d.Name] = MyTuple.Create(2, Static.IndexOf(d));
                }
                DisplayBlocks.Remove(b);
            }
        }

        void Wipe(ref List<Display> ds)
        {
            if (ds.Count > 0)
                foreach (var d in ds)
                    d.Reset();
        }

        void SetPriorities(ref List<Display> ds)
        {
            if (ds.Count > 0)
                foreach (var d in ds)
                    d.SetPriority();
        }

        public void Update(string arg, UpdateType source)
        {
            RuntimeMS += Program.Runtime.TimeSinceLastRun.TotalMilliseconds;
            Frame++;
            var rt = Program.Runtime.LastRunTimeMs;
            if (WorstRun < rt) { WorstRun = rt; WorstFrame = Frame; }
            totalRt += rt;
            if (runtimes.Count == rtMax)
                runtimes.Dequeue();
            runtimes.Enqueue(rt);
            if (!setupComplete)
                RunSetup();
            p = Priority.Normal;
            if (Frame <= min && useLogo)
            {
                draw = false;
                foreach (var l in logos)
                    l.Update();
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
                Program.Echo($"RUNS - {Frame}\nRUNTIME - {rt} ms\nAVG - {AverageRun:0.####} ms\nPARSE CYCLES - {iniWrap.total}\nMYINI INSTANCES - {iniWrap.Count}\nFAILURES - {Lib.bsodsTotal}");
            }

            if (arg != "")
            {
                var c = '!';
                if (arg.Contains(c))
                {
                    int j = 0, k;
                    var urg = arg.Split(c);
                    // if (urg.Length == 3) 
                    {
                        for (; j < urg.Length; j++)
                            urg[j] = urg[j].Trim().Trim(c);
                        if (_displaysMaster.ContainsKey(urg[2]) && int.TryParse(urg[1], out j))
                        {
                            k = _displaysMaster[urg[2]].Item2;
                            switch (_displaysMaster[urg[2]].Item1)
                            {
                                case 0:
                                   FastDisplays[k].MFDSwitch(j, urg[0]);
                                    break;
                                case 1:
                                default:
                                   Displays[k].MFDSwitch(j, urg[0]);
                                    break;
                                case 2:
                                    Static[k].MFDSwitch(j, urg[0]);
                                    break;
                            }
                        }
                    }
                }
                else
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
                        case "update":
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
                                    Program.Runtime.UpdateFrequency = Lib.NONE;
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
                                Program.Runtime.UpdateFrequency = Lib.NONE;
                                return;
                            }
                        default: { break; }
                    }
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

            if (Frame > min)
            {
                if (fast)
                {
                    AverageRun = 0;
                    foreach (var qr in runtimes)
                        AverageRun += qr;
                    AverageRun /= rtMax;
                }
                string r = "[[GRAPHICS MANAGER]]\n\n";
                if (draw) r += $"DRAWING DISPLAY {dPtr + 1}/{Displays.Count}";
                else if (Inventory.needsUpdate)
                    r += $"INV {Inventory.Pointer}/{Inventory.Count}";
                else r += $"UTILS {iPtr + 1}/{Utilities.Count} - {Utilities[iPtr].Name}";

                r += $"\nRUNS - {Frame}\nRUNTIME - {rt} ms\nAVG - {AverageRun:0.####} ms\nWORST - {WorstRun} ms, F{WorstFrame}";
                //r = Inventory.DebugString; 
                Program.Echo(r);
            }
        }
    }
}