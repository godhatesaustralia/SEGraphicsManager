﻿using Sandbox.Engine.Platform.VideoMode;
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

        #endregion

        public DisplayBase(IMyTerminalBlock b)
        {
            Name = b.CustomName;
            dEID = b.EntityId;
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

        public abstract void Setup(IMyTerminalBlock block);

        public abstract void Update(ref Priority p);

        protected void DrawNewSprite(ref MySpriteDrawFrame frame, SpriteData data)
        {
            if (data.uID == -1)
            {
                frame.Add(data.sprCached);
                return;
            }
            //Program.Me.CustomData += $"\n{s.dType}, \n{s.Data}, \n{s.Size}, \n{s.Position}, \n{s.dColor}, \n{s.Alignment}\n";
            frame.Add(SpriteData.createSprite(data));
        }

    }

    public class LinkedDisplay : DisplayBase
    {
        readonly IniKeys Keys;
        bool isSingleScreen;

        public LinkedDisplay(IMyTerminalBlock block, ref Dictionary<string, Action<SpriteData>> commandsDict, ref MyGridProgram program, ref IniKeys keys) : base(block)
        {
            Commands = commandsDict;
            CommandUsers = new Dictionary<IMyTextSurface, HashSet<string>>();
            Refresh = new Dictionary<IMyTextSurface, Priority>();
            Outputs = new Dictionary<IMyTextSurface, Dictionary<string, SpriteData>>();
            Program = program;
            Keys = keys;
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
                        var nametag = $"{Keys.SpriteSection}_{name}";
                        if (ini.hasSection(nametag) && nArray.Contains(name))
                        {
                            SpriteData s = new SpriteData();

                            s.Name = name;
                            s.Type = ini.Type(nametag, Keys.Type);
                            s.Data = ini.String(nametag, Keys.Data, "FAILED");
                            s.Color = ini.Color(nametag, Keys.Color);

                            s.Alignment = ini.Alignment(nametag, Keys.Align);
                            parseVector(ref s, ref ini, Keys.Size, nametag);
                            parseVector(ref s, ref ini, Keys.Pos, nametag);

                            if (s.Type == Lib.dType) 
                                s.RorS = ini.Float(nametag, Keys.Scale, 1);
                            else 
                                s.RorS = ini.Float(nametag, Keys.Scale, 0);

                            s.FontID = s.Type == Lib.dType ? ini.String(nametag, Keys.Font, "Monospace") : "";
                            s.Priority = (Priority)ini.Byte(nametag, Keys.Update, 0);
                            p |= s.Priority;

                            if (ini.hasKey(nametag, Keys.Command) && s.Priority != 0)
                            {
                                if (s.Priority != Priority.None && s.commandID != "")
                                {
                                    s.uID = dEID + index + Array.IndexOf(nArray, name);
                                    s.commandID = ini.String(nametag, Keys.Command, "!def");

                                    if (!Commands.ContainsKey(s.commandID))
                                        throw new Exception($"PARSE FAILURE: sprite {s.Name} on screen {Name} has invalid command {s.commandID}");

                                    CommandUsers[surf].Add(s.Name);
                                    s.Command = Commands[s.commandID];
                                    s.Command.Invoke(s);
                                    if (ini.hasKey(nametag, Keys.Prepend))
                                        s.Prepend = ini.String(nametag, Keys.Prepend) + " ";

                                    if (ini.hasKey(nametag, Keys.Append))
                                        s.Append = " " + ini.String(nametag, Keys.Append);

                                    s.setSB();
                                }
                            }
                            s.sprCached = SpriteData.createSprite(s, true);
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
            var d = new SpriteData(Color.White, Name, "", c.X + (c.X * 0.4f), c.Y + (c.Y * 0.4f), 0.4375f);
            s.ScriptBackgroundColor = Color.Blue;
            d.Data = $"{Name}\nSURFACE {s.Name}\nSCREEN SIZE {s.SurfaceSize}\nTEXTURE SIZE {s.TextureSize}\n\n\n{Lib.bsod}";
            d.FontID = "Monospace";
            d.sprCached = SpriteData.createSprite(d, true);
            var f = s.DrawFrame();
            Outputs[s].Add(Name, d);
            f.Dispose();
        }

        private void parseVector(ref SpriteData sprite, ref iniWrap myParser, string key, string nametag)
        {
            var coords = myParser.String(nametag, key).Split(',');

            if (key == Keys.Pos)
            {
                sprite.X = float.Parse(coords.First().Trim(Keys.vectorL));
                sprite.Y = float.Parse(coords.Last().Trim(Keys.vectorR));
            }

            else if (key == Keys.Size && sprite.Type != Lib.dType)
            {
                sprite.sX = float.Parse(coords.First().Trim(Keys.vectorL));
                sprite.sY = float.Parse(coords.Last().Trim(Keys.vectorR));
            }
        }

        public override void Setup(IMyTerminalBlock block)
        {
            iniWrap p = new iniWrap();
            byte index = 0;
            MyIniParseResult Result;
            var pri = Priority.None;
            if (p.CustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    Outputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());
                    isSingleScreen = true;
                    if (TryAddSprites(ref DisplayBlock, ref p, ref index, out pri))
                        Refresh.Add(DisplayBlock, pri);
                }
                else if (block is IMyTextSurfaceProvider)
                {
                    isSingleScreen = false;
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
                    }
                }
            }
            else throw new Exception($" PARSE FAILURE: {Name} cd error {Result.Error} at {Result.LineNo}");
            p.Dispose();

            foreach (var display in Outputs)
            {
                var frame = display.Key.DrawFrame();
                foreach (var sprite in display.Value)
                    DrawNewSprite(ref frame, sprite.Value);
                frame.Dispose();
            }
            foreach (var val in Refresh.Values)
                Priority |= (Priority)((byte)val);

            foreach (var kvp in CommandUsers)
                foreach (var s in kvp.Value)
                    Outputs[kvp.Key][s].Command.Invoke(Outputs[kvp.Key][s]);

        }

        public override void Update(ref Priority p)
        {
            foreach (var display in Outputs)
                if ((Refresh[display.Key] & p) != 0) 
                {                                                                  
                    foreach (var n in display.Value.Keys)
                        if (CommandUsers[display.Key].Contains(n))
                        {
                            display.Value[n].Command.Invoke(display.Value[n]);
                            if (display.Value[n].Builder)
                                Lib.ApplyBuilder(display.Value[n]);
                        }
                    var frame = display.Key.DrawFrame();
                    foreach (var sprite in display.Value)
                        DrawNewSprite(ref frame, sprite.Value);

                    frame.Dispose();
                }

        }

    }
}