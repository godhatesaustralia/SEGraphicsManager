using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        public Program()
        {
            CoyLogo.program = this;
            FastDisplays = new List<Display>();
            Displays = new List<Display>();
            Static = new List<Display>();
            Inventory = new InventoryUtilities(GCM, new DebugAPI(this));
            if (DateTime.Now.Date.Year % 4 == 0)
                addLarp = TimeSpan.FromDays(36524);
            else addLarp = TimeSpan.FromDays(36525);

            var result = new MyIniParseResult();
            using (var p = new IniWrap())
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

                    GTS.GetBlocksOfType<IMyShipController>(null, b =>
                    {
                        if ((b.CustomName.Contains(ctrl) || b.IsMainCockpit) && b.IsSameConstructAs(Me))
                            Controller = b;
                        return false;
                    });

                    var grps = p.String(Lib.HDR, "groups").Split('\n');
                    if (grps != null) foreach (var g in grps)
                            _groups.Add(g, CreateGroup(g, p.String(Lib.HDR, g)));
                }
                else throw new Exception($" PARSE FAILURE: {Me.CustomName} cd error {result.Error} at {result.LineNo}");
        }

        public void Main(string argument, UpdateType updateSource)
        {
            RuntimeMS += Runtime.TimeSinceLastRun.TotalMilliseconds;
            Frame++;
            var rt = Runtime.LastRunTimeMs;

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
                    foreach (var d  in FastDisplays)
                        d.SetPriority();
                    foreach (var d in Displays)
                        d.SetPriority();
                    foreach (var d in Static)
                        d.ForceRedraw();
                    draw = true;
                    p |= Priority.Once;
                }
                Echo($"RUNS - {Frame}\nRUNTIME - {rt} ms\nAVG - {AverageRun:0.####} ms\nPARSE CYCLES - {IniWrap.total}\nMYINI INSTANCES - {IniWrap.Count}\nFAILURES - {Lib.bsodsTotal}");
            }

            if (argument != "")
            {
                var c = '!';
                if (argument.Contains(Lib.CMD))
                {
                    int j = 0, k;
                    var urg = argument.Split(c);
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
                    argument = argument.ToLower();
                    switch (argument)
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
                                    Runtime.UpdateFrequency = UpdateFrequency.Update1;
                                    frozen = false;
                                    break;
                                }
                                else
                                {
                                    Runtime.UpdateFrequency = Lib.NONE;
                                    frozen = true;
                                    break;
                                }
                            }
                        case "slow":
                            {
                                Runtime.UpdateFrequency = UpdateFrequency.Update100;
                                break;
                            }
                        case "wipe":
                            {
                                foreach (var d in FastDisplays)
                                    d.Reset();
                                foreach (var d in Displays)
                                    d.Reset();
                                foreach (var d in Static)
                                    d.Reset();
                                Echo("All displays wiped, ready to restart.");
                                Runtime.UpdateFrequency = Lib.NONE;
                                return;
                            }
                        default: { break; }
                    }
                }
            }
            if (!setupComplete) return;

            bool fast = (updateSource & UpdateType.Update10) != 0;
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
                //Utilities[Lib.Next(ref iPtr, Utilities.Count)].Update();
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
                string r = "===<GRAPHICS MANAGER>===\n\n";
                if (draw) r += $">DRAWING DISPLAY {dPtr + 1}/{Displays.Count}";
                else if (Inventory.needsUpdate)
                    r += $"INV {Inventory.Pointer}/{Inventory.Count}";
                //else r += $"UTILS {iPtr + 1}/{Utilities.Count} - {Utilities[iPtr].Name}";

                r += $"\nRUNS - {Frame}\nRUNTIME - {rt} ms\nAVG - {AverageRun:0.####} ms\nWORST - {WorstRun} ms, F{WorstFrame}";
                //r = Inventory.DebugString; 
                //Program.Echo(r);
            }
        }
    }
}
