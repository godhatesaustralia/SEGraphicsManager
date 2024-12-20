using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    public partial class Program : MyGridProgram
    {
        #region fields
        public IMyGridTerminalSystem GTS => GridTerminalSystem;
        public IMyShipController Controller;

        public string Tag, GCM = "GCM", Name;
        public Dictionary<string, Action<SpriteData>> Commands = new Dictionary<string, Action<SpriteData>>();

        // name to  (list #, list index)
        Dictionary<string, MyTuple<int, int>> _displaysMaster = new Dictionary<string, MyTuple<int, int>>();
        List<Display> Displays, FastDisplays, Static;
        List<CoyLogo> logos = new List<CoyLogo>();
        MyCommandLine _cmd = new MyCommandLine();
        InventoryUtilities Inventory;

        HashSet<IMyTerminalBlock> DisplayBlocks = new HashSet<IMyTerminalBlock>();

        int dPtr, iPtr, // display pointers
            min, fast; // min - frames to wait for echo, fast - determines BlockRefresh.Fast
        const int rtMax = 10; // theoretically accurate for update10
        double totalRt, RuntimeMS, WorstRun, AverageRun;
        Queue<double> runtimes = new Queue<double>(rtMax);
        bool frozen = false, draw, useLogo, larp;
        public bool SetupComplete, isCringe;
        TimeSpan addLarp;
        long Frame, WorstFrame;
        public long F => Frame;
        Priority p;
        #endregion

        void Clear(bool full)
        {
            dPtr = -1;
            IniWrap.total = 0;
            if (full) Commands.Clear();
            Displays.Clear();
            DisplayBlocks.Clear();
            _displaysMaster.Clear();
            _cmd.Clear();
            Lib.GraphStorage.Clear();
        }

        public void Init(bool full = true)
        {
            Runtime.UpdateFrequency |= UpdateFrequency.Update1 | UpdateFrequency.Update10 | UpdateFrequency.Update100;
            SetupComplete = false;
            Clear(full);
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

            }

            Inventory.Reset(this);
            Inventory.Setup(ref Commands);

            var g = GTS.GetBlockGroupWithName(Tag + " " + Name);
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
                SetupComplete = true;
                if (Displays.Count == 0)
                {
                    Runtime.UpdateFrequency = UpdateFrequency.None;
                    Echo("No commands detected on active displays. Script shutting down.");
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
                    using (var ini = new IniWrap())
                    {
                        int c = logos.Count;
                        CoyLogo l;
                        if (ini.CustomData(b) && !ini.Bool(Keys.SurfaceSection, "IGNORE_L", false))
                        {
                            l = new CoyLogo
                            (
                                b as IMyTextPanel,
                                ini.Bool(Keys.SurfaceSection, "FRAME_L", false),
                                ini.Bool(Keys.SurfaceSection, "TEXT_L", false),
                                ini.Bool(Keys.SurfaceSection, "VCR_L", false)
                            );

                            l.SetAnimate();
                            p = d.Setup(b, true);
                            logos.Add(l);
                            logos[c].Color = d.logoColor;
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
            
    }
}