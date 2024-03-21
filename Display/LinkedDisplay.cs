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
        public string Name;
        public long dEID;

        #endregion

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
        IniKeys Keys;
        bool isSingleScreen;

        public LinkedDisplay(IMyTerminalBlock block, ref Dictionary<string, Action<SpriteData>> commandsDict, ref MyGridProgram program, ref IniKeys keys)
        {
            Commands = commandsDict;
            CommandUsers = new Dictionary<IMyTextSurface, HashSet<string>>();
            Refresh = new Dictionary<IMyTextSurface, Priority>();
            Outputs = new Dictionary<IMyTextSurface, Dictionary<string, SpriteData>>();
            Name = block.CustomName;
            dEID = block.EntityId;
            Program = program;
            Keys = keys;
        }


        // [Custom Data Formatting]
        // ...
        // read the pdf u moron. u absolute buffooon

        bool TryAddSprites(ref IMyTextSurface surf, ref Parser myParser, ref byte index, out Priority p)
        {
            string sect;
            p = Priority.None;
            var good = true;
            if (!isSingleScreen)
                sect = $"{Keys.ScreenSection}_{index}";
            else
                sect = Keys.ScreenSection;
            Program.Echo(sect);
            CommandUsers.Add(surf, new HashSet<string>());
            if (!myParser.hasSection(sect))
                return false;
            else
            {
                surf.Script = "";
                surf.ScriptBackgroundColor = myParser.Color(sect, Keys.Color + "_BG");
                var fail = "~";
                var names = myParser.String(sect, Keys.List, fail);
                if (names == fail)
                {
                    Default(ref surf);
                    return false;
                }
                var nArray = names.Split(Keys.new_line);
                for (int i = 0; i < nArray.Length; ++i)
                { nArray[i] = nArray[i].Trim(Keys.new_entry); nArray[i] = nArray[i].Trim(); Program.Echo(nArray[i]); }

                if (nArray.Length > 0)
                {
                    surf.ContentType = ContentType.SCRIPT;
                    foreach (var name in nArray)
                    {
                        var nametag = $"{Keys.SpriteSection}_{name}";
                        if (myParser.hasSection(nametag) && nArray.Contains(name))
                        {
                            SpriteData s = new SpriteData();
                            //Name
                            s.Name = name;
                            // >TYPE
                            s.Type = myParser.Type(nametag, Keys.Type);
                            // >DATA
                            if (myParser.hasKey(nametag, Keys.Data))
                                s.Data = myParser.String(nametag, Keys.Data, "FAILED");
                            else s.Data = "";
                            // >SIZE
                            CartesianReader(ref s, ref myParser, Keys.Size, nametag);
                            // >ALIGN
                            s.Alignment = myParser.Alignment(nametag, Keys.Align);
                            // >POSITION
                            CartesianReader(ref s, ref myParser, Keys.Pos, nametag);
                            //COLOR
                            s.Color = myParser.Color(nametag, Keys.Color);
                            // >ROTATION/SCALE
                            if (s.Type == Util.dType) s.RorS = myParser.Float(nametag, Keys.Scale, 1);
                            else s.RorS = myParser.Float(nametag, Keys.Scale, 0);
                            // >FONT
                            if (myParser.hasKey(nametag, Keys.Font))
                                s.FontID = s.Type == Util.dType ? myParser.String(nametag, Keys.Font, "Monospace") : "";
                            // >UPDATE
                            bool old = myParser.hasKey(nametag, Keys.UpdateOld);

                            if (myParser.hasKey(nametag, Keys.Update) || old)
                            {
                                var update = old ? myParser.Byte(nametag, Keys.Update, 0) : myParser.Byte(nametag, Keys.Update, 0);
                                s.Priority = (Priority)update;
                                p |= s.Priority;
                            }
                            else
                                s.Priority = Priority.None;

                            // >COMMAND
                            if (myParser.hasKey(nametag, Keys.Command) && s.Priority != 0)
                            {
                                if (s.Priority != Priority.None && s.CommandString != "")
                                {
                                    // uID
                                    s.uID = dEID + index + Array.IndexOf(nArray, name);
                                    // >PREPEND
                                    if (myParser.hasKey(nametag, Keys.Prepend))
                                        s.Prepend = myParser.String(nametag, Keys.Prepend) + " ";
                                    // >APPEND
                                    if (myParser.hasKey(nametag, Keys.Append))
                                        s.Append = " " + myParser.String(nametag, Keys.Append);
                                    // >USEBUILDER
                                    s.Builder = !s.Prepend.Equals("") || !s.Append.Equals("");
                                    s.CommandString = s.Priority == Priority.None ? "" : myParser.String(nametag, Keys.Command, "!def");
                                    if (!Commands.ContainsKey(s.CommandString))
                                        throw new Exception($"PARSE FAILURE: sprite {s.Name} on screen {Name} has invalid command {s.CommandString}");
                                    CommandUsers[surf].Add(s.Name);
                                    s.Command = Commands[s.CommandString];
                                    s.Command.Invoke(s);
                                }
                            }
                            s.sprCached = SpriteData.createSprite(s, true);
                            // We're done!
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
            Program.Echo($"surface {surf.DisplayName} LOADED, priority {p}");
            return good;
        }

        private void Default(ref IMyTextSurface s)
        { 
            var c = (s.TextureSize - s.SurfaceSize) / 2;
            var d = new SpriteData();
            s.ScriptBackgroundColor = Color.Blue;
            d.Name = Name;
            d.Data = $"{Name}\nSURFACE {s.Name}\nSCREEN SIZE {s.SurfaceSize}\nTEXTURE SIZE {s.TextureSize}\n\n\n{Util.bsod}";
            d.FontID = "Monospace";
            d.Color = Color.White;
            d.PosX = c.X; 
            d.PosY = c.Y;
            d.RorS = 0.4375f;
            d.sprCached = SpriteData.createSprite(d, true);
            var f = s.DrawFrame();
            Outputs[s].Add(Name, d);
            f.Dispose();
        }

        void CartesianReader(ref SpriteData sprite, ref Parser myParser, string key, string nametag)
        {
            var coords = myParser.String(nametag, key).Split(',');

            if (key == Keys.Pos)
            {
                sprite.PosX = float.Parse(coords.First().Trim(Keys.l_coord));
                sprite.PosY = float.Parse(coords.Last().Trim(Keys.r_coord));
            }

            else if (key == Keys.Size && sprite.Type != Util.dType)
            {
                sprite.SizeX = float.Parse(coords.First().Trim(Keys.l_coord));
                sprite.SizeY = float.Parse(coords.Last().Trim(Keys.r_coord));
            }
        }

        public override void Setup(IMyTerminalBlock block)
        {
            Parser MyParser = new Parser();
            byte index = 0;
            MyIniParseResult Result;
            var pri = Priority.None;
            if (MyParser.CustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    Outputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());
                    isSingleScreen = true;
                    if (TryAddSprites(ref DisplayBlock, ref MyParser, ref index, out pri))
                    {
                        Refresh.Add(DisplayBlock, pri);
                    }
                }
                else if (block is IMyTextSurfaceProvider)
                {
                    isSingleScreen = false;
                    var DisplayBlock = (IMyTextSurfaceProvider)block;
                    var SurfaceCount = DisplayBlock.SurfaceCount;

                    for (index = 0; index < SurfaceCount; ++index)
                    {
                        if (!MyParser.hasSection($"{Keys.ScreenSection}_{index}")) continue;
                        var surface = DisplayBlock.GetSurface(index);
                        if (!Outputs.ContainsKey(surface))
                            Outputs.Add(surface, new Dictionary<string, SpriteData>());
                        if (TryAddSprites(ref surface, ref MyParser, ref index, out pri))
                            Refresh.Add(surface, pri);
                        else
                            Refresh.Add(surface, Priority.None);
                    }
                }
            }
            else throw new Exception($" PARSE FAILURE: {Name} cd error {Result.Error} at {Result.LineNo}");
            MyParser.Dispose();

            foreach (var display in Outputs)
            {
                var frame = display.Key.DrawFrame();
                foreach (var sprite in display.Value)
                {
                    DrawNewSprite(ref frame, sprite.Value);
                    //Program.Me.CustomData += s.Value.Name + new_line;
                }
                frame.Dispose();
            }

            foreach (var val in Refresh.Values)
            {
                Priority |= (Priority)((byte)val);
            }
        }

        public override void Update(ref Priority p)
        {
            foreach (var display in Outputs)
                if ((Refresh[display.Key] & p) != 0) //is display priority the same as current update's priority?
                {                                                                   // i.e. do we update display on this tick
                    foreach (var n in display.Value.Keys)
                        if (CommandUsers[display.Key].Contains(n)) //is command priority the same as current update priority?
                        {
                            display.Value[n].Command.Invoke(display.Value[n]);
                            if (display.Value[n].Builder)
                                InfoUtility.ApplyBuilder(display.Value[n]);
                        }//i.e. do we run command on this tick
                    var frame = display.Key.DrawFrame();
                    foreach (var sprite in display.Value)
                        DrawNewSprite(ref frame, sprite.Value);
                    frame.Dispose();
                }

        }

    }
}