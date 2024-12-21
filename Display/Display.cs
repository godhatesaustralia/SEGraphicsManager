using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    public static class Keys
    {
        public const string
             SurfaceSection = "SURFACE",
             ScreenSection = "SCREEN_",
             SpriteSection = "SPRITE_", // always named, so just bake in the underscore 
             Cycle = "CYCLE",
             ScreenList = "SCREENS",
             List = "LIST",
             Logo = "LOGO",
             Type = "TYPE",
             Data = "DATA",
             Size = "SIZE",
             Align = "ALIGN",
             Pos = "POS",
             Rotation = "ROTATION",
             Scale = "SCALE",
             Color = "COLOR",
             Font = "FONT",
             Format = "FORMAT",
             Command = "CMD",
             Key = "KEY",
             Update = "PRIORITY",
             Conditions = "CONDITIONS",
             Based = "BASED",
             Cringe = "VNLA",
             Prepend = "PREP",
             Append = "APP";
    }

    // new (5/11/24)
    // this is a screen - the base unit of script
    // it is a self-contained collection of sprites
    // with a set refresh rate. users can switch between
    // different screens for a given text surface to achieve
    // MFD (mass fish death) functionality
    public class Screen
    {
        // you aint built for these
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
        static bool Default(Screen s)
        {
            var ts = s.dsp.TextureSize;
            var ss = s.dsp.SurfaceSize;
            Lib.bsodsTotal++;
            Lib.bsods += (Lib.bsodsTotal > 1 ? ", " : "") + s.Name;
            var c = (ts - ss) / 2;
            var r = ts / ss;
            var uh = 20 * r + 0.5f * c;
            var d = new SpriteData(Color.White, s.Name, "", uh.X, uh.Y + (c.Y * 0.4f), c.Length() * 0.005275f, align: TextAlignment.LEFT, font: "Monospace");
            var f = s.dsp.DrawFrame();
            s.dsp.ScriptBackgroundColor = Color.Blue;
            d.Data = $"{s.dsp.Name}\nSURFACE {s.Name}\nSCREEN SIZE {ss}\nTEXTURE SIZE {ts}\nPOSITION OF THIS THING {uh}\n\n\n{Lib.bsod}";

            f.Dispose();
            return false;
        }
        void Die(string n)
        {
            throw new Exception("\nNO KEY FOUND ON " + n + " ON SCREEN " + Name);
        }

        public bool AddSprites(bool mfd, Display d, ref IniWrap ini, Dictionary<string, Action<SpriteData>> cmds, ref byte index, out Priority p)
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
                    }

                    SpriteType type;
                    TextAlignment align;
                    float x, y, sx, sy, ros;
                    string dat, font = null;
                    var c = Color.HotPink;
                    dsp.ContentType = ContentType.SCRIPT;
                    
                    for (i = 0; i < ct; i++)
                    {
                        sx = sy = 0;
                        var spr = Keys.SpriteSection + l[i];
                        if (ini.HasSection(spr))// && !Outputs[surf].ContainsKey(ns[i]))
                        {
                            if (!d.noVCR && ini.Bool(spr, Keys.Cringe, false))
                            {
                                if (Sprites.ContainsKey(l[i]))
                                    Sprites.Remove(l[i]);
                                continue;
                            }
                            else if (d.noVCR && ini.Bool(spr, Keys.Based, false))
                            {
                                if (Sprites.ContainsKey(l[i]))
                                    Sprites.Remove(l[i]);
                                continue;
                            }

                            dat = ini.String(spr, Keys.Data, "FAILED");
                            type = ini.Type(spr, Keys.Type);
                            c = ini.Color(spr, Keys.Color);                  
                            align = ini.Alignment(spr, Keys.Align);

                            if (!ini.TryReadVector2(spr, Keys.Pos, out x, out y, Name))
                                Die(l[i]);

                            if (type != Lib.TXT && !ini.TryReadVector2(spr, Keys.Size, out sx, out sy, Name))
                                Die(l[i]);

                            if (type != Lib.TXT)
                                ros = ini.Float(spr, Keys.Rotation, 0);
                            else
                            {
                                string
                                    def = "White",
                                    vpos = Keys.Pos + m_vn,
                                    vscl = Keys.Scale + m_vn;

                                font = type == Lib.TXT ? ini.String(spr, Keys.Font, def) : "";
                                if ((font == "VCR" || font == "VCRBold") && d.noVCR)
                                {
                                    if (ini.HasKey(spr, vpos) && !ini.TryReadVector2(spr, vpos, out x, out y, Name))
                                        Die(l[i]);
                                    font = def;

                                    if (ini.HasKey(spr, vscl))
                                        ros = ini.Float(spr, vscl, 1);
                                    else ros = ini.Float(spr, Keys.Scale, 1);

                                    if (ini.HasKey(spr, Keys.Align + m_vn))
                                        align = ini.Alignment(spr, Keys.Align + m_vn);
                                    dat = ini.String(spr, Keys.Data + m_vn, dat);
                                }
                                else
                                {
                                    ros = ini.Float(spr, Keys.Scale, 1);
                                    if (ini.HasKey(spr, Keys.Rotation))
                                        ros = ini.Float(spr, Keys.Rotation, 0);
                                }
                            }

                            var p2 = (Priority)ini.Int(spr, Keys.Update, 0);
                            var s = type == Lib.TXT ? new SpriteData(c, l[i], dat, x, y, ros, font, p2, align) : new SpriteData(c, l[i], dat, x, y, ros, sx, sy, p2, align, clip: type == SpriteType.CLIP_RECT);

                            p |= s.Priority;

                            if (ini.HasKey(spr, Keys.Command)) 
                            {
                                var cmd = ini.String(spr, Keys.Command, "!def");
                                if (cmd == "!screen")
                                    s.Data = ini.String(spr, Keys.Format) == "caps" ? Name.ToUpper() : Name;
                                else
                                {
                                    s.Format = ini.String(spr, Keys.Format);
                                    s.Key = ini.String(spr, Keys.Key);

                                    if (!cmds.ContainsKey(cmd))
                                        throw new Exception($"PARSE FAILURE: sprite {s.Name} on screen {Name} ({d.Name}) has invalid command {cmd}");

                                    if (ini.HasKey(spr, Keys.Conditions)) // is it conditional
                                    {
                                        var sc = new SpriteConditional(s);
                                        sc.CreateMappings(ini.String(spr, Keys.Conditions));
                                        s = sc;
                                    }

                                    CommandUsers.Add(s.Name);
                                    s.Command = cmds[cmd];
                                    s.Command.Invoke(s);

                                    if (d.noVCR) // switch to vanilla prepends
                                    {
                                        if (ini.HasKey(spr, Keys.Prepend + m_vn))
                                            s.Prepend = ini.String(spr, Keys.Prepend + m_vn) + " ";

                                        if (ini.HasKey(spr, Keys.Append + m_vn))
                                            s.Append = " " + ini.String(spr, Keys.Append + m_vn);
                                    }
                                    else // normal case
                                    {
                                        if (ini.HasKey(spr, Keys.Prepend))
                                            s.Prepend = ini.String(spr, Keys.Prepend) + " ";

                                        if (ini.HasKey(spr, Keys.Append))
                                            s.Append = " " + ini.String(spr, Keys.Append);
                                    }
                                   
                                }

                                s.SetFlags(cmd);
                            }
                            Sprites[s.Name] = s;
                        }
                        else good = Default(this);
                    }
                }
                else return Default(this);
            }
            Refresh = p;
            return good;
        }

    }

    public class Display
    {
        #region fields
        public Priority BlockRefresh = 0;
        public long NextRefreshF = REF_T;
        public Dictionary<string, Screen> Outputs = new Dictionary<string, Screen>();
        public Dictionary<int, string> ActiveScreens = new Dictionary<int, string>(); // active n of screens by surface index
        public Dictionary<int, HashSet<string>> ScreenNames = new Dictionary<int, HashSet<string>>();
        Program _p;
        public readonly string Name;
        public readonly long dEID;
        public bool isEnabled => Host?.Enabled ?? true;
        public readonly bool isSingleScreen, noVCR;
        public Color logoColor = Color.White;
        readonly IMyFunctionalBlock Host;
        const string m_lg = "_L";  // suffix for logo keys
        const int REF_T = 320;
        static MySprite X = new MySprite();
        #endregion


        public Display(IMyTerminalBlock b, Program p, bool c)
        {
            _p = p;
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

        bool TryParse(ref IMyTextSurface surf, ref IniWrap ini, ref byte index, out Priority p)
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

                var mfd = ini.HasKey(sect, Keys.ScreenList);
                if (!mfd) // for non-MFDs
                {
                    var sc = new Screen($"def{index}", surf);
                    sc.AddSprites(mfd, this, ref ini, _p.Commands, ref index, out p);
                    Outputs.Add(sc.Name, sc);
                    ActiveScreens.Add(index, sc.Name);
                    if (ScreenNames.ContainsKey(index))
                        ScreenNames[index].Add(sc.Name);
                    else ScreenNames[index] = new HashSet<string> { sc.Name };
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

                var ns = n.Split('\n'); // getting names of different screens
                for (; i < ns.Length; i++)
                {
                    ns[i] = ns[i].Trim('>');
                    ns[i] = ns[i].Trim();
                    var sc = new Screen(ns[i], surf);
                    var ok = sc.AddSprites(mfd, this, ref ini, _p.Commands, ref index, out p);
                    if (!ok)
                        Default(ref sc);

                    good &= ok;
                    Outputs.Add(sc.Name, sc);

                    if (i == 0)
                        ActiveScreens.Add(index, sc.Name);

                    if (ScreenNames.ContainsKey(index))
                        ScreenNames[index].Add(sc.Name);
                    else ScreenNames[index] = new HashSet<string> { sc.Name };
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
            Lib.bsods += (Lib.bsodsTotal > 1 ? ", " : "") + s.Name;
            var c = (ts - ss) / 2;
            var r = ts / ss;
            var uh = 20 * r + 0.5f * c;
            var d = new SpriteData(Color.White, s.Name, "", uh.X, uh.Y + (c.Y * 0.4f), c.Length() * 0.005275f, align: TextAlignment.LEFT, font: "Monospace");
            s.dsp.ScriptBackgroundColor = Color.Blue;
            var f = s.dsp.DrawFrame();
            f.Dispose();
        }

        public Priority Setup(IMyTerminalBlock block, bool w = false)
        {
            bool ok = true;
            var p = new IniWrap();
            byte index = 0;
            MyIniParseResult Result;
            Priority ret, pri = ret = Priority.None;
            if (p.CustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    ok &= TryParse(ref DisplayBlock, ref p, ref index, out pri);
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
                    s.Sprites[n].CheckUpdate();

                foreach (var spr in s.Sprites.Values)
                    f.Add(spr.Sprite);

                f.Dispose();
            }
            return ret;
        }

        public void SetPriority()
        {
            foreach (var s in ActiveScreens.Values)
               BlockRefresh |= Outputs[s].Refresh;
        }

        #endregion

        public void MFDSwitch(int i, string n)
        {
            if (!(ScreenNames.ContainsKey(i) && ScreenNames[i].Contains(n)))
                return;

            ActiveScreens[i] = n;
            var f = Outputs[n].dsp.DrawFrame();
            foreach (var s in Outputs[n].Sprites.Values)
                f.Add(s.Sprite);

            f.Dispose();
        }


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
                        f.Add(s.Sprite);
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
                    bool changed = false, refresh = _p.F >= NextRefreshF;
                    foreach (var sn in acs.CommandUsers)
                        changed |= acs.Sprites[sn].CheckUpdate();

                    if (!changed && !refresh)
                        continue;
                
                    var f = acs.dsp.DrawFrame();

                    if (refresh)
                    {
                        f.Add(X);
                        NextRefreshF += REF_T;
                    }

                    foreach (var s in acs.Sprites.Values)
                         f.Add(s.Sprite);
                    f.Dispose();
                }
        }

    }
}