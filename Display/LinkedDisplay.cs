using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.EntityComponents;
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
    public abstract class DisplayBase
    {
        #region fields

        protected MyGridProgram Program;
        protected Dictionary<IMyTextSurface, HashSet<string>> CommandUsers;
        public Priority Priority;
        public Dictionary<string, Action<SpriteData>> Commands;
        public Dictionary<IMyTextSurface, Dictionary<string, SpriteData>> Outputs;
        public Dictionary<IMyTextSurface, Priority> Refresh;
        public readonly string Name;
        public readonly long dEID;
        protected Priority val;

        #endregion

        public DisplayBase(IMyTerminalBlock b)
        {
            Name = b.CustomName;
            dEID = b.EntityId;
            Priority = Priority.None;
        }

        public void Reset()
        {
            Refresh.Clear();
            CommandUsers.Clear();
            foreach (var scr in Outputs.Keys)
            {
                var f = scr.DrawFrame();
                f.Add(new MySprite());
                f.Dispose();
            }
            Outputs.Clear();
        }

        public abstract Priority Setup(IMyTerminalBlock block, bool wait = false);

        public abstract void Update(ref Priority p);

        public void ForceRedraw()
        {
            foreach (var display in Outputs)
            {
                var frame = display.Key.DrawFrame();
                foreach (var sprite in display.Value)
                    DrawNewSprite(ref frame, sprite.Value);
                frame.Dispose();
            }
        }

        // breaking this out, to run only when m_lg is done - i think it makes more sense
        public void SetPriority()
        {
            foreach (var val in Refresh.Values)
                Priority |= val;
        }

        protected void DrawNewSprite(ref MySpriteDrawFrame frame, SpriteData data)
        {
            if (data.uID == -1)
            {
                frame.Add(data.sprCached);
                return;
            }
            //Program.helm.CustomData += $"\n{s.dType}, \n{s.Data}, \n{s.Size}, \n{s.Position}, \n{s.dColor}, \n{s.Alignment}\n";
            frame.Add(SpriteData.CreateSprite(data));
        }

    }

    public class LinkedDisplay : DisplayBase
    {
        readonly IniKeys Keys;
        readonly IMyFunctionalBlock Host;
        bool isEnabled => Host?.Enabled ?? true;
        bool isSingleScreen, noVCR;
        const string m_lg = "_L", m_vn = "_V";

        public Color logoColor = Color.White;

        public LinkedDisplay(IMyTerminalBlock block, ref Dictionary<string, Action<SpriteData>> commandsDict, ref MyGridProgram program, ref IniKeys keys, bool cringe) : base(block)
        {
            Commands = commandsDict;
            CommandUsers = new Dictionary<IMyTextSurface, HashSet<string>>();
            Refresh = new Dictionary<IMyTextSurface, Priority>();
            Outputs = new Dictionary<IMyTextSurface, Dictionary<string, SpriteData>>();
            Program = program;
            Keys = keys;
            noVCR = cringe;
            IMyFunctionalBlock f = block as IMyFunctionalBlock;
            if (f != null)
                Host = f;
            //var s = block as IMyTextSurfaceProvider;
            isSingleScreen = block is IMyTextSurface; // || s.SurfaceCount == 1;
            // MAREK ROSA I AM COMING TO KILL YOU!!!!! AT YOUR HOUSE!! IN PRAGUE!!!
            //if (block.BlockDefinition.SubtypeName == "LargeBlockInsetEntertainmentCorner")
            //    isSingleScreen = true;
        }

        // [Custom Data Formatting]
        // ...
        // read the pdf u moron. u absolute buffooon

        bool TryAddSprites(ref IMyTextSurface surf, ref iniWrap ini, ref byte index, out Priority p)
        {
            // idt the way this works is representative of any kind of good practices
            // but for better or worse it is kind of the bedrock of this whole script thing
            // so i'm just going to leave it alone
            string sect;
            p = Priority.None;
            var good = true;
            if (!isSingleScreen)
                sect = $"{Keys.ScreenSection}_{index}";
            else
                sect = Keys.ScreenSection;
            //Program.Echo(sect);
            CommandUsers.Add(surf, new HashSet<string>());
            if (!ini.hasSection(sect))
                return false;
            else
            {
                surf.Script = "";
                surf.ScriptBackgroundColor = ini.Color(sect, Keys.Color + "_BG");
                var fail = "~";
                if (ini.hasKey(sect, Keys.Color + m_lg))
                    logoColor = ini.Color(sect, Keys.Color + m_lg);

                if (isSingleScreen && ini.Bool(sect, Keys.Logo))
                {
                    Priority = Priority.None;
                    Outputs.Clear();
                    return true;
                }

                var names = ini.String(sect, Keys.List, fail);
                if (names == fail)
                {
                    Default(ref surf);
                    return false;
                }
                var nArray = names.Split('\n');
                for (int i = 0; i < nArray.Length; ++i)
                {
                    nArray[i] = nArray[i].Trim(Keys.entry);
                    nArray[i] = nArray[i].Trim();
                    //Program.Echo(nArray[i]); 
                }

                if (nArray.Length > 0)
                {
                    surf.ContentType = ContentType.SCRIPT;
                    foreach (var name in nArray)
                    {
                        var spr = $"{Keys.SpriteSection}_{name}";
                        if (ini.hasSection(spr) && nArray.Contains(name))
                        {
                            SpriteData s = new SpriteData();
                            s.Name = name;

                            if (!noVCR)
                            {
                                if (ini.Bool(spr, Keys.Cringe, false))
                                {
                                    if (Outputs[surf].ContainsKey(s.Name))
                                        Outputs[surf].Remove(s.Name);
                                    continue;
                                }
                            }
                            else if (ini.Bool(spr, Keys.Based, false))
                            {
                                if (Outputs[surf].ContainsKey(s.Name))
                                    Outputs[surf].Remove(s.Name);
                                continue;
                            }

                            s.Type = ini.Type(spr, Keys.Type);
                            s.Data = ini.String(spr, Keys.Data, "FAILED");
                            s.Color = ini.Color(spr, Keys.Color);

                            s.Alignment = ini.Alignment(spr, Keys.Align);
                            readVector2(ref s, ref ini, Keys.Size, spr);
                            readVector2(ref s, ref ini, Keys.Pos, spr);

                            if (s.Type != Lib.dType)
                                s.RorS = ini.Float(spr, Keys.Rotation, 0);

                            else
                            {
                                string
                                    def = "White",
                                    vpos = Keys.Pos + m_vn,
                                    vscl = Keys.Scale + m_vn;

                                s.FontID = s.Type == Lib.dType ? ini.String(spr, Keys.Font, def) : "";
                                if ((s.FontID == "VCR" || s.FontID == "VCRBold") && noVCR)
                                {
                                    if (ini.hasKey(spr, vpos))
                                        readVector2(ref s, ref ini, vpos, spr);
                                    s.FontID = def;
                                    if (ini.hasKey(spr, vscl))
                                        s.RorS = ini.Float(spr, vscl, 1);
                                    else
                                        s.RorS = ini.Float(spr, Keys.Scale, 1);
                                    if (ini.hasKey(spr, Keys.Align + m_vn))
                                        s.Alignment = ini.Alignment(spr, Keys.Align + m_vn);
                                    s.Data = ini.String(spr, Keys.Data + m_vn, s.Data);
                                }
                                else
                                    s.RorS = ini.Float(spr, Keys.Scale, 1);
                                if (ini.hasKey(spr, Keys.Rotation))
                                    s.RorS = ini.Float(spr, Keys.Rotation, 0);
                            }

                            s.Priority = (Priority)ini.Byte(spr, Keys.Update, 0);
                            p |= s.Priority;

                            if (ini.hasKey(spr, Keys.Command) && s.Priority != 0)
                            {
                                if (s.Priority != Priority.None && s.commandID != "")
                                {
                                    s.uID = dEID + index + Array.IndexOf(nArray, name);
                                    s.commandID = ini.String(spr, Keys.Command, "!def");

                                    if (!Commands.ContainsKey(s.commandID))
                                        throw new Exception($"PARSE FAILURE: sprite {s.Name} on screen {Name} has invalid command {s.commandID}");

                                    CommandUsers[surf].Add(s.Name);
                                    s.Command = Commands[s.commandID];
                                    s.Command.Invoke(s);

                                    if (noVCR)
                                    {
                                        if (ini.hasKey(spr, Keys.Prepend + m_vn))
                                            s.Prepend = ini.String(spr, Keys.Prepend + m_vn) + " ";
                                        if (ini.hasKey(spr, Keys.Append + m_vn))
                                            s.Append = " " + ini.String(spr, Keys.Append + m_vn);
                                    }
                                    else
                                    {
                                        if (ini.hasKey(spr, Keys.Prepend))
                                            s.Prepend = ini.String(spr, Keys.Prepend) + " ";
                                        if (ini.hasKey(spr, Keys.Append))
                                            s.Append = " " + ini.String(spr, Keys.Append);
                                    }
                                    s.SetBuilder();
                                }
                            }
                            s.sprCached = SpriteData.CreateSprite(s, true);
                            if (!Outputs[surf].ContainsKey(s.Name))
                                Outputs[surf].Add(s.Name, s);
                        }
                        else
                        {
                            Default(ref surf);
                            good = false;
                        }
                    }
                }
                else Default(ref surf);
            }
            //Program.Echo($"surface {surf.DisplayName} LOADED, priority {p}");
            return good;
        }

        private void Default(ref IMyTextSurface s)
        {
            var c = (s.TextureSize - s.SurfaceSize) / 2;
            var r = s.TextureSize / s.SurfaceSize;
            var uh = 20 * r + 0.5f * c;
            var d = new SpriteData(Color.White, Name, "", uh.X, uh.Y + (c.Y * 0.4f), c.Length() * 0.005275f, align: TextAlignment.LEFT);
            s.ScriptBackgroundColor = Color.Blue;
            d.Data = $"{Name}\nSURFACE {s.Name}\nSCREEN SIZE {s.SurfaceSize}\nTEXTURE SIZE {s.TextureSize}\nPOSITION OF THIS THING {uh}\n\n\n{Lib.bsod}";
            d.FontID = "Monospace";
            d.sprCached = SpriteData.CreateSprite(d, true);
            var f = s.DrawFrame();
            Outputs[s].Add(Name, d);
            f.Dispose();
        }

        private void readVector2(ref SpriteData sprite, ref iniWrap ini, string key, string nametag)
        {
            var pos = ini.String(nametag, key).Split(',');
            try
            {
                if (key == Keys.Pos || key == Keys.Pos + m_vn)
                {
                    sprite.X = float.Parse(pos.First().Trim(Keys.vectorL));
                    sprite.Y = float.Parse(pos.Last().Trim(Keys.vectorR));
                }

                else if (key == Keys.Size && sprite.Type != Lib.dType)
                {
                    sprite.sX = float.Parse(pos.First().Trim(Keys.vectorL));
                    sprite.sY = float.Parse(pos.Last().Trim(Keys.vectorR));
                }
            }
            catch (Exception)
            {
                throw new Exception($"\nError reading {key.ToLower()} floats for {Name}:{sprite.Name}.");
            }
        }

        public override Priority Setup(IMyTerminalBlock block, bool w = false)
        {
            iniWrap p = new iniWrap();
            byte index = 0;
            MyIniParseResult Result;
            Priority ret, pri = ret = Priority.None;
            if (p.CustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    Outputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());
                    if (TryAddSprites(ref DisplayBlock, ref p, ref index, out pri))
                        Refresh.Add(DisplayBlock, pri);
                    ret = pri;
                }
                else if (block is IMyTextSurfaceProvider)
                {
                    var DisplayBlock = (IMyTextSurfaceProvider)block;
                    var SurfaceCount = DisplayBlock.SurfaceCount;

                    for (index = 0; index < SurfaceCount; ++index)
                    {
                        if (!p.hasSection($"{Keys.ScreenSection}_{index}"))
                            continue;
                        var surface = DisplayBlock.GetSurface(index);
                        if (!Outputs.ContainsKey(surface))
                            Outputs.Add(surface, new Dictionary<string, SpriteData>());

                        if (TryAddSprites(ref surface, ref p, ref index, out pri))
                            Refresh.Add(surface, pri);
                        else
                            Refresh.Add(surface, Priority.None);
                        ret |= pri;
                    }
                }
            }
            else throw new Exception($" PARSE FAILURE: {Name} cd error {Result.Error} at {Result.LineNo}");
            p.Dispose();
            if (Outputs.Count == 0) return Priority.None;
            if (!w) SetPriority();

            foreach (var kvp in CommandUsers)
                foreach (var s in kvp.Value)
                    Outputs[kvp.Key][s].Update();

            foreach (var display in Outputs)
            {
                var frame = display.Key.DrawFrame();
                foreach (var sprite in display.Value)
                    DrawNewSprite(ref frame, sprite.Value);
                frame.Dispose();
            }
            return ret;
        }
        public override void Update(ref Priority p)
        {

            if (!isEnabled) return;
            foreach (var display in Outputs)
                if ((Refresh[display.Key] & p) != 0)
                {
                    bool changed = false;
                    foreach (var n in display.Value.Keys)
                        if (CommandUsers[display.Key].Contains(n))
                        {
                            if (display.Value[n].Update())
                                changed = true;
                        }
                    var frame = display.Key.DrawFrame();
                    if (!changed) continue;
                    foreach (var sprite in display.Value)
                        DrawNewSprite(ref frame, sprite.Value);

                    frame.Dispose();
                }
        }

    }
}