using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons.Guns;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Policy;
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
    // new (5/11/24)
    // this is a screen - the base unit of script
    // it is a self-contained collection of sprites
    // with a set refresh rate. users can switch between
    // different screens for a given text surface to achieve
    // MFD (mass fish death) functionality
    public class Screen
    {
        #region fields

        public readonly string Name; // used as key
        public HashSet<string> CommandUsers = new HashSet<string>();
        public Dictionary<string, SpriteData> Sprites = new Dictionary<string, SpriteData>();
        public Priority Refresh = 0;
        public readonly IMyTextSurface dsp;
        public Color BG;
        const string m_vn = "_V"; // suffix for vanilla option keys

        #endregion
        public Screen(string n, IMyTextSurface d)
        {
            Name = n;
            dsp = d;
        }
        // TODO - better error reporting
        static void Default(Screen s)
        {
            var ts = s.dsp.TextureSize;
            var ss = s.dsp.SurfaceSize;
            Lib.bsodsTotal++;
            var c = (ts - ss) / 2;
            var r = ts / ss;
            var uh = 20 * r + 0.5f * c;
            var d = new SpriteData(Color.White, s.Name, "", uh.X, uh.Y + (c.Y * 0.4f), c.Length() * 0.005275f, align: TextAlignment.LEFT);
            s.dsp.ScriptBackgroundColor = Color.Blue;
            d.Data = $"{s.dsp.Name}\nSURFACE {s.Name}\nSCREEN SIZE {ss}\nTEXTURE SIZE {ts}\nPOSITION OF THIS THING {uh}\n\n\n{Lib.bsod}";
            d.FontID = "Monospace";
            d.sprCached = SpriteData.CreateSprite(d, true);
            var f = s.dsp.DrawFrame();
            f.Dispose();
        }
        void Die(string n)
        {
            throw new Exception("\nNO KEY FOUND ON " + n + " ON SCREEN " + Name);
        }

        public bool AddSprites(bool mfd, Display d, ref iniWrap ini, Dictionary<string, Action<SpriteData>> cmds, ref byte index, out Priority p)
        {
            var sect = mfd ? Keys.ScreenSection + Name : (d.isSingleScreen ? Keys.SurfaceSection : Keys.SurfaceSection + $"_{index}");
            int i = 0;
            p = Priority.None;

            bool good = true;
            if (ini.HasSection(sect))
            {
                BG = ini.Color(sect, Keys.Color + "_BG");
                var l = ini.String(sect, Keys.List).Split('\n');
                var ct = l.Length;
                if (ct > 0)
                {
                    for (; i < ct; i++)
                    {
                    l[i] = l[i].Trim('>');
                    l[i] = l[i].Trim();
                    //Program.Echo(ns[i]); 
                    }

                    dsp.ContentType = ContentType.SCRIPT;
                    for (i = 0; i < ct; i++)
                    {
                        var spr = Keys.SpriteSection + l[i];
                        if (ini.HasSection(spr))// && !Outputs[surf].ContainsKey(ns[i]))
                        {
                            var s = new SpriteData();
                            s.Name = l[i];

                            if (!d.noVCR && ini.Bool(spr, Keys.Cringe, false))
                            {
                                if (Sprites.ContainsKey(s.Name))
                                    Sprites.Remove(s.Name);
                                continue;
                            }
                            else if (d.noVCR && ini.Bool(spr, Keys.Based, false))
                            {
                                if (Sprites.ContainsKey(s.Name))
                                    Sprites.Remove(s.Name);
                                continue;
                            }

                            s.Type = ini.Type(spr, Keys.Type);
                            s.Data = ini.String(spr, Keys.Data, "FAILED");
                            s.Color = ini.Color(spr, Keys.Color);

                            s.Alignment = ini.Alignment(spr, Keys.Align);
                            if (!ini.TryReadVector2(spr, Keys.Pos, out s.X, out s.Y, Name))
                                Die(l[i]);

                            if (s.Type != Lib.TXT && !ini.TryReadVector2(spr, Keys.Size, out s.sX, out s.sY, Name))
                                Die(l[i]);

                            if (s.Type != Lib.TXT)
                                s.RorS = ini.Float(spr, Keys.Rotation, 0);
                            else
                            {
                                string
                                    def = "White",
                                    vpos = Keys.Pos + m_vn,
                                    vscl = Keys.Scale + m_vn;

                                s.FontID = s.Type == Lib.TXT ? ini.String(spr, Keys.Font, def) : "";
                                if ((s.FontID == "VCR" || s.FontID == "VCRBold") && d.noVCR)
                                {
                                    if (ini.HasKey(spr, vpos) && !ini.TryReadVector2(spr, vpos, out s.X, out s.Y, Name))
                                        Die(l[i]);
                                    s.FontID = def;
                                    if (ini.HasKey(spr, vscl))
                                        s.RorS = ini.Float(spr, vscl, 1);
                                    else
                                        s.RorS = ini.Float(spr, Keys.Scale, 1);
                                    if (ini.HasKey(spr, Keys.Align + m_vn))
                                        s.Alignment = ini.Alignment(spr, Keys.Align + m_vn);
                                    s.Data = ini.String(spr, Keys.Data + m_vn, s.Data);
                                }
                                else
                                    s.RorS = ini.Float(spr, Keys.Scale, 1);
                                if (ini.HasKey(spr, Keys.Rotation))
                                    s.RorS = ini.Float(spr, Keys.Rotation, 0);
                            }

                            s.Priority = (Priority)ini.Int(spr, Keys.Update, 0);
                            p |= s.Priority;

                            if (ini.HasKey(spr, Keys.Command) && s.Priority != 0)
                            {
                                if (s.Priority != Priority.None && s.commandID != "")
                                {
                                    s.uID = d.dEID + index + i;
                                    s.commandID = ini.String(spr, Keys.Command, "!def");
                                    s.Format = ini.String(spr, Keys.Format);
                                    if (!cmds.ContainsKey(s.commandID))
                                        throw new Exception($"PARSE FAILURE: sprite {s.Name} on screen {Name} ({d.Name}) has invalid command {s.commandID}");

                                    CommandUsers.Add(s.Name);
                                    s.Command = cmds[s.commandID];
                                    s.Command.Invoke(s);

                                    if (d.noVCR)
                                    {
                                        if (ini.HasKey(spr, Keys.Prepend + m_vn))
                                            s.Prepend = ini.String(spr, Keys.Prepend + m_vn) + " ";
                                        if (ini.HasKey(spr, Keys.Append + m_vn))
                                            s.Append = " " + ini.String(spr, Keys.Append + m_vn);
                                    }
                                    else
                                    {
                                        if (ini.HasKey(spr, Keys.Prepend))
                                            s.Prepend = ini.String(spr, Keys.Prepend) + " ";
                                        if (ini.HasKey(spr, Keys.Append))
                                            s.Append = " " + ini.String(spr, Keys.Append);
                                    }
                                }
                                s.SetFlags();
                            }
                            s.sprCached = SpriteData.CreateSprite(s, true);
                            Sprites[s.Name] = s;
                        }
                        else
                        {
                            Default(this);
                            good = false;
                        }
                    }
                }
                else
                {
                    Default(this);
                    return false;
                }
            }
            Refresh = p;
            return good;
        }

        //public void Update()
        //{
        //    bool changed = false;
        //    foreach (var n in CommandUsers)
        //        if (Sprites[n].Update())
        //            changed = true;
        //    if (!changed) return;
        //    var f = dsp.DrawFrame();
        //    foreach (var s in Sprites.Values)
        //        if (s.uID == -1)
        //            f.Add(s.sprCached);
        //        else f.Add(SpriteData.CreateSprite(s));
        //    f.Dispose();
        //}
    }

    public class Display
    {
        #region fields

        public Priority BlockRefresh = 0;
        public Dictionary<string, Screen> Outputs = new Dictionary<string, Screen>();
        public Dictionary<int, string> ActiveScreens = new Dictionary<int, string>(); // active n of screens by surface index
        public Dictionary<int, List<string>> ScreenNames = new Dictionary<int, List<string>>();
        public readonly string Name;
        public readonly long dEID;
        public bool isEnabled => Host?.Enabled ?? true;
        public readonly bool isSingleScreen, noVCR;
        public Color logoColor = Color.White;
        bool MFD = false;
        readonly IMyFunctionalBlock Host;
        GraphicsManager _m;
       
        const string m_lg = "_L";  // suffix for logo keys

        #endregion


        public Display(IMyTerminalBlock b, GraphicsManager m, bool c)
        {
            _m = m;
            Name = b.CustomName;
            dEID = b.EntityId;
            noVCR = c;
            IMyFunctionalBlock f = b as IMyFunctionalBlock;
            if (f != null)
                Host = f;
            //var s = b as IMyTextSurfaceProvider;
            isSingleScreen = b is IMyTextSurface; // || s.SurfaceCount == 1;
            // MAREK ROSA I AM COMING TO KILL YOU!!!!! AT YOUR HOUSE!! IN PRAGUE!!!
            //if (b.BlockDefinition.SubtypeName == "LargeBlockInsetEntertainmentCorner")
            //    isSingleScreen = true;
        }

        #region setup

        // [Custom Data Formatting]
        // ...
        // read the pdf u moron. u absolute buffooon

        // if you're someone else reading this...below method basically is part of my first EVER attempt
        // to write some original code for SE. so it;s not good :/. it's a messy piece of shit but it is also
        // the bedrock of this whole thing and it at least tries to be reasonable. this would probably
        // get obliterated on a server but i really still don't know what im doing

        bool TryParse(ref IMyTextSurface surf, ref iniWrap ini, ref byte index, out Priority p)
        {
            string sect;
            var l = new List<string>();
            int i = 0, j;
            p = Priority.None;
            bool good = true;
            if (!isSingleScreen)
                sect = $"{Keys.SurfaceSection}_{index}";
            else
                sect = Keys.SurfaceSection;
            if (ini.HasSection(sect))
            {
                surf.Script = "";
                var f = "~";
                if (ini.HasKey(sect, Keys.Color + m_lg))
                    logoColor = ini.Color(sect, Keys.Color + m_lg);
                if (isSingleScreen && ini.Bool(sect, Keys.Logo))
                {
                    BlockRefresh = Priority.None;
                    Outputs.Clear();
                    return true;
                }
                if (!ini.HasKey(sect, Keys.ScreenList)) // for non-MFDs
                {
                    var sc = new Screen($"def{index}", surf);
                    sc.AddSprites(MFD, this, ref ini, _m.Commands, ref index, out p);
                    Outputs.Add(sc.Name, sc);
                    ActiveScreens.Add(index, sc.Name);
                    return true;
                }
                var n = ini.String(sect, Keys.ScreenList, f);
                if (n == f)
                {
                    var sc = new Screen("FAIL " + index, surf);
                    Default(ref sc);
                    Outputs.Add(sc.Name, sc);              
                    return false;
                }
                MFD = true;
                var ns = n.Split('\n'); // getting names of different screens
                for (; i < ns.Length; i++)
                {
                    ns[i] = ns[i].Trim('>');
                    ns[i] = ns[i].Trim();
                    var sc = new Screen(ns[i], surf);
                    var ok = sc.AddSprites(MFD, this, ref ini, _m.Commands, ref index, out p);
                    if (!ok)
                        Default(ref sc);
                    good &= ok;
                    Outputs.Add(sc.Name, sc);
                    if (i == 0)
                        ActiveScreens.Add(index, sc.Name);
                }
                return good;
            }
            return false;
        }
        static void Default(ref Screen s)
        {
            var ts = s.dsp.TextureSize;
            var ss = s.dsp.SurfaceSize;
            Lib.bsodsTotal++;
            var c = (ts - ss) / 2;
            var r = ts / ss;
            var uh = 20 * r + 0.5f * c;
            var d = new SpriteData(Color.White, s.Name, "", uh.X, uh.Y + (c.Y * 0.4f), c.Length() * 0.005275f, align: TextAlignment.LEFT);
            s.dsp.ScriptBackgroundColor = Color.Blue;
            d.Data = $"{s.dsp.Name}\nSURFACE {s.Name}\nSCREEN SIZE {ss}\nTEXTURE SIZE {ts}\nPOSITION OF THIS THING {uh}\n\n\n{Lib.bsod}";
            d.FontID = "Monospace";
            d.sprCached = SpriteData.CreateSprite(d, true);
            var f = s.dsp.DrawFrame();
            f.Dispose();
        }

        public Priority Setup(IMyTerminalBlock block, bool w = false)
        {
            bool ok = true;
            var p = new iniWrap();
            byte index = 0;
            MyIniParseResult Result;
            Priority ret, pri = ret = Priority.None;
            if (p.CustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    ok &= TryParse(ref DisplayBlock, ref p, ref index, out pri);
                    //Outputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());
                    //if (TryParse(ref DisplayBlock, ref p, ref index, out pri))
                    //    ret = pri;
                    //Refresh.Add(DisplayBlock, pri);
                    ret = pri;

                }
                else if (block is IMyTextSurfaceProvider)
                {
                    var DisplayBlock = (IMyTextSurfaceProvider)block;
                    var SurfaceCount = DisplayBlock.SurfaceCount;
                    for (index = 0; index < SurfaceCount; index++)
                    {
                        if (!p.HasSection($"{Keys.SurfaceSection}_{index}"))
                            continue;
                        var surface = DisplayBlock.GetSurface(index);
                        //if (!Outputs.ContainsKey(surface))
                        //    Outputs.Add(surface, new Dictionary<string, SpriteData>());
                        ok &= TryParse(ref surface, ref p, ref index, out pri);
                        //if (TryParse(ref surface, ref p, ref index, out pri))
                        //    ret |= pri;
                        //else
                        //    Refresh.Add(surface, BlockRefresh.None);
                        //ret |= pri;
                        ret |= pri;
                    }
                }
                p.Dispose();
            }
            else throw new Exception($" PARSE FAILURE: {Name} cd error {Result.Error} at {Result.LineNo}");
            //p.Dispose();
            if (Outputs.Count == 0) return Priority.None;
            if (!w) SetPriority();
            foreach (var scr in ActiveScreens.Values)
            {
                var s = Outputs[scr];
                s.dsp.ScriptBackgroundColor = s.BG;
                var f = s.dsp.DrawFrame();
                foreach (var n in s.CommandUsers)
                    s.Sprites[n].Update();
                foreach (var spr in s.Sprites.Values)
                    if (spr.uID == -1)
                        f.Add(spr.sprCached);
                    else f.Add(SpriteData.CreateSprite(spr));
                f.Dispose();
            }


            //foreach (var scr in Outputs.Values)
            //{
            //    var frame = scr.Key.DrawFrame();
            //    foreach (var sprite in scr.Value)
            //        DrawNewSprite(ref frame, sprite.Value);
            //    frame.Dispose();
            //}
            return ret;
        }

        public void SetPriority()
        {
            foreach (var s in ActiveScreens.Values)
               BlockRefresh |= Outputs[s].Refresh;
        }

        #endregion

        #region setupOld
        //bool TryParseOld(ref IMyTextSurface surf, ref iniWrap ini, ref byte index, out BlockRefresh p)
        //{
        //    string sect;
        //    int i = 0;
        //    p = BlockRefresh.None;
        //    bool good = true;
        //    if (!isSingleScreen)
        //        sect = $"{Keys.SurfaceSection}_{index}";
        //    else
        //        sect = Keys.SurfaceSection;

        //    CommandUsers.Add(surf, new HashSet<string>());
        //    if (!ini.HasSection(sect))
        //        return false;
        //    else
        //    {
        //        surf.Script = "";
        //        surf.ScriptBackgroundColor = ini.Color(sect, Keys.Color + "_BG");
        //        var fail = "~";
        //        if (ini.HasKey(sect, Keys.Color + m_lg))
        //            logoColor = ini.Color(sect, Keys.Color + m_lg);

        //        if (isSingleScreen && ini.Bool(sect, Keys.Logo))
        //        {
        //            BlockRefresh = BlockRefresh.None;
        //            Outputs.Clear();
        //            return true;
        //        }

        //        var names = ini.String(sect, Keys.List, fail);
        //        if (names == fail)
        //        {
        //            DefaultOld(ref surf);
        //            return false;
        //        }
        //        var nArray = names.Split('\n');
        //        for (; i < nArray.Length; i++)
        //        {
        //            nArray[i] = nArray[i].Trim('>');
        //            nArray[i] = nArray[i].Trim();
        //            //Program.Echo(ns[i]); 
        //        }

        //        if (nArray.Length > 0)
        //        {
        //            surf.ContentType = ContentType.SCRIPT;
        //            for (i = 0; i < nArray.Length; i++)
        //            {
        //                var spr = Keys.SpriteSection + nArray[i];
        //                if (ini.HasSection(spr))// && !Outputs[surf].ContainsKey(ns[i]))
        //                {
        //                    var s = new SpriteData();
        //                    s.Name = nArray[i];

        //                    if (!noVCR && ini.Bool(spr, Keys.Cringe, false))
        //                    {
        //                        if (Outputs[surf].ContainsKey(s.Name))
        //                            Outputs[surf].Remove(s.Name);
        //                        continue;
        //                    }
        //                    else if (noVCR && ini.Bool(spr, Keys.Based, false))
        //                    {
        //                        if (Outputs[surf].ContainsKey(s.Name))
        //                            Outputs[surf].Remove(s.Name);
        //                        continue;
        //                    }

        //                    s.Type = ini.Type(spr, Keys.Type);
        //                    s.Data = ini.String(spr, Keys.Data, "FAILED");
        //                    s.Color = ini.Color(spr, Keys.Color);

        //                    s.Alignment = ini.Alignment(spr, Keys.Align);
        //                    if (!ini.TryReadVector2(spr, Keys.Pos, out s.X, out s.Y, Name))
        //                        Die(nArray[i]);

        //                    if (s.Type != Lib.TXT && !ini.TryReadVector2(spr, Keys.Size, out s.sX, out s.sY, Name))
        //                        Die(nArray[i]);

        //                    if (s.Type != Lib.TXT)
        //                        s.RorS = ini.Float(spr, Keys.Rotation, 0);
        //                    else
        //                    {
        //                        string
        //                            def = "White",
        //                            vpos = Keys.Pos + m_vn,
        //                            vscl = Keys.Scale + m_vn;

        //                        s.FontID = s.Type == Lib.TXT ? ini.String(spr, Keys.Font, def) : "";
        //                        if ((s.FontID == "VCR" || s.FontID == "VCRBold") && noVCR)
        //                        {
        //                            if (ini.HasKey(spr, vpos) && !ini.TryReadVector2(spr, vpos, out s.X, out s.Y, Name))
        //                                Die(nArray[i]);
        //                            s.FontID = def;
        //                            if (ini.HasKey(spr, vscl))
        //                                s.RorS = ini.Float(spr, vscl, 1);
        //                            else
        //                                s.RorS = ini.Float(spr, Keys.Scale, 1);
        //                            if (ini.HasKey(spr, Keys.Align + m_vn))
        //                                s.Alignment = ini.Alignment(spr, Keys.Align + m_vn);
        //                            s.Data = ini.String(spr, Keys.Data + m_vn, s.Data);
        //                        }
        //                        else
        //                            s.RorS = ini.Float(spr, Keys.Scale, 1);
        //                        if (ini.HasKey(spr, Keys.Rotation))
        //                            s.RorS = ini.Float(spr, Keys.Rotation, 0);
        //                    }

        //                    s.BlockRefresh = (BlockRefresh)ini.Int(spr, Keys.Update, 0);
        //                    p |= s.BlockRefresh;

        //                    if (ini.HasKey(spr, Keys.Command) && s.BlockRefresh != 0)
        //                    {
        //                        if (s.BlockRefresh != BlockRefresh.None && s.commandID != "")
        //                        {
        //                            s.uID = dEID +  index + i;
        //                            s.commandID = ini.String(spr, Keys.Command, "!def");
        //                            s.Format = ini.String(spr, Keys.Format);
        //                            if (!Commands.ContainsKey(s.commandID))
        //                                throw new Exception($"PARSE FAILURE: sprite {s.Name} on screen {Name} has invalid command {s.commandID}");

        //                            CommandUsers[surf].Add(s.Name);
        //                            s.Command = Commands[s.commandID];
        //                            s.Command.Invoke(s);

        //                            if (noVCR)
        //                            {
        //                                if (ini.HasKey(spr, Keys.Prepend + m_vn))
        //                                    s.Prepend = ini.String(spr, Keys.Prepend + m_vn) + " ";
        //                                if (ini.HasKey(spr, Keys.Append + m_vn))
        //                                    s.Append = " " + ini.String(spr, Keys.Append + m_vn);
        //                            }
        //                            else
        //                            {
        //                                if (ini.HasKey(spr, Keys.Prepend))
        //                                    s.Prepend = ini.String(spr, Keys.Prepend) + " ";
        //                                if (ini.HasKey(spr, Keys.Append))
        //                                    s.Append = " " + ini.String(spr, Keys.Append);
        //                            }
        //                        }
        //                        s.SetFlags();
        //                    }
        //                    s.sprCached = SpriteData.CreateSprite(s, true);
        //                    Outputs[surf][s.Name] = s;
        //                }
        //                else
        //                {
        //                    DefaultOld(ref surf);
        //                    good = false;
        //                }
        //            }
        //        }
        //        else
        //        {
        //            DefaultOld(ref surf);
        //            return false;
        //        }
        //    }
        //    return good;
        //}
        // TODO - better error reporting
        //void DefaultOld(ref IMyTextSurface s)
        //{
        //    Lib.bsodsTotal++;
        //    var c = (s.TextureSize - s.SurfaceSize) / 2;
        //    var r = s.TextureSize / s.SurfaceSize;
        //    var uh = 20 * r + 0.5f * c;
        //    var d = new SpriteData(Color.White, s.Name, "", uh.X, uh.Y + (c.Y * 0.4f), c.Length() * 0.005275f, align: TextAlignment.LEFT);
        //    s.ScriptBackgroundColor = Color.Blue;
        //    d.Data = $"{Name}\nSURFACE {s.Name}\nSCREEN SIZE {s.SurfaceSize}\nTEXTURE SIZE {s.TextureSize}\nPOSITION OF THIS THING {uh}\n\n\n{Lib.bsod}";
        //    d.FontID = "Monospace";
        //    d.sprCached = SpriteData.CreateSprite(d, true);
        //    var f = s.DrawFrame();
        //    if (!Outputs.ContainsKey(s))
        //        Outputs[s].Add(Name, d);
        //    f.Dispose();
        //}
        #endregion

        public void Reset()
        {
            foreach (var scr in Outputs.Values)
            {
                scr.CommandUsers.Clear();
                var f = scr.dsp.DrawFrame();
                f.Add(new MySprite());
                f.Dispose();
            }
            Outputs.Clear();
        }

        public void ForceRedraw()
        {
            foreach (var n in ActiveScreens.Values)
            {
                var acs = Outputs[n];
                var f = acs.dsp.DrawFrame();
                foreach (var s in acs.Sprites.Values)
                    if (s.uID == -1)
                        f.Add(s.sprCached);
                    else f.Add(SpriteData.CreateSprite(s));
                f.Dispose();
            }    
        }

        public void Update(ref Priority p)
        {
            if (!isEnabled) 
                return;
            foreach (var n in ActiveScreens.Values)
                if ((Outputs[n].Refresh & p) != 0)
                {
                    var acs = Outputs[n];
                    bool changed = false;
                    foreach (var sn in acs.CommandUsers)
                        changed |= acs.Sprites[sn].Update();

                    if (!changed)
                        continue;
                    var f = acs.dsp.DrawFrame();
                    foreach (var s in acs.Sprites.Values)
                        if (s.uID == -1)
                            f.Add(s.sprCached);
                        else f.Add(SpriteData.CreateSprite(s));
                    f.Dispose();
                }
        }

    }
}