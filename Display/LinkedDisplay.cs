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

        public UpdateFrequency UpdateFrequency;
        public Priority Priority;
        public Dictionary<string, Action<SpriteData>> Commands;
        public Dictionary<IMyTextSurface, Dictionary<string, SpriteData>> Outputs;
        public Dictionary<IMyTextSurface, Priority> Refresh;
        public string Name;
        public long dEID;

        #endregion

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

        #region CustomDataFormat

        // [Custom Data Formatting]
        //
        // I didn't want the list...but look, it simplifies parsing custom data SO, so much
        // split at '>', trim at '\n'
        //
        //[SECT_SCREEN_0]
        // K_COLOR_BG FA6464FF
        // K_LIST = 
        // |>FRAME
        //[SECT_SPRITE_FRAME]
        // K_TYPE = {byte 0/2/4} => if this is 2, we can skip size section
        // K_DATA = SquareSimple
        // K_SIZE = (50, 50)
        // K_ALIGN = 2 => {byte 0/1/2}
        // K_COORD = (256, 256) => ALWAYS in screen coordinates
        // K_ROTSCAL = 0
        // K_COLOR = FA6464FF
        // K_FONT = "White" => don't even LOOK for this if it's a texture
        // K_UPDATE = 0x1
        // K_CMD = !...
        // K_BUILD = false
        // K_PREP = default
        // K_APP = default
        // [SECT_SPRITE_TOPBAR]
        //   ...
        // and so on

        #endregion
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
            if (myParser.hasSection(sect))
            {
                surf.ContentType = ContentType.SCRIPT;
                surf.Script = "";
                surf.ScriptBackgroundColor = myParser.Color(sect, Keys.Color + "_BG");
                var names = myParser.String(sect, Keys.List);
                var nArray = names.Split(Keys.new_line);
                for (int i = 0; i < nArray.Length; ++i)
                { nArray[i] = nArray[i].Trim(Keys.new_entry); nArray[i] = nArray[i].Trim(); Program.Echo(nArray[i]); }

                if (nArray.Count() > 0)
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
                            s.RorS = myParser.Float(nametag, (s.Type == Utilities.dType ? Keys.Scale : Keys.Rotation), float.NaN);
                            if (float.IsNaN(s.RorS))
                                s.RorS = myParser.Float(nametag, (s.Type == Utilities.dType ? Keys.Rotation : Keys.Scale));
                            // >FONT
                            if (myParser.hasKey(nametag, Keys.Font))
                                s.FontID = s.Type == Utilities.dType ? myParser.String(nametag, Keys.Font, "Monospace") : "";
                            // >UPDATE
                            bool old = myParser.hasKey(nametag, Keys.UpdateOld);
                            if (myParser.hasKey(nametag, Keys.Update) || old)
                            {
                                var update = old ? myParser.Byte(nametag, Keys.Update, 0) : myParser.Byte(nametag, Keys.Update, 0);
                                if (update == 4) update = 2; // backwards compatible
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
                            good = false;
                    }
            }
            else scrDefault(surf);
            Program.Echo($"surface {surf.DisplayName} LOADED, priority {p}");
            return good;
        }

        void scrDefault(IMyTextSurface s)
        {
            s.ContentType = (ContentType)3;
            s.Alignment = (TextAlignment)2;
            var t = $"{Name}\nSURFACE {s.Name}\nSCREEN SIZE {s.SurfaceSize}\nTEXTURE SIZE {s.TextureSize}";
            s.WriteText(t);
        }

        void CartesianReader(ref SpriteData sprite, ref Parser myParser, string key, string nametag)
        {
            var coords = myParser.String(nametag, key).Split(',');

            if (key == Keys.Pos)
            {
                sprite.PosX = float.Parse(coords.First().Trim(Keys.l_coord));
                sprite.PosY = float.Parse(coords.Last().Trim(Keys.r_coord));
            }

            else if (key == Keys.Size && sprite.Type != Utilities.dType)
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
            var freq = Priority.None;
            if (MyParser.CustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    Outputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());

                    isSingleScreen = true;
                    if (TryAddSprites(ref DisplayBlock, ref MyParser, ref index, out freq))
                    {
                        Refresh.Add(DisplayBlock, freq);
                    }
                    else scrDefault(DisplayBlock);

                }
                else if (block is IMyTextSurfaceProvider)
                {
                    isSingleScreen = false;
                    var DisplayBlock = (IMyTextSurfaceProvider)block;
                    var SurfaceCount = DisplayBlock.SurfaceCount;

                    for (index = 0; index < SurfaceCount; ++index)
                    {
                        var surface = DisplayBlock.GetSurface(index);
                        if (!Outputs.ContainsKey(surface))
                            Outputs.Add(surface, new Dictionary<string, SpriteData>());
                        if (TryAddSprites(ref surface, ref MyParser, ref index, out freq))
                            Refresh.Add(surface, freq);
                        else
                        {
                            scrDefault(surface);
                            Refresh.Add(surface, Priority.None);
                        }
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

            foreach (var ufrq in Refresh.Values)
            {
                // Program.Echo($"{UpdateFrequency} |= {ufrq}");
                //UpdateFrequency |= ufrq;
                Priority |= (Priority)((byte)ufrq);
                // Program.Echo($"{UpdateFrequency}");
            }

        }

        public override void Update(ref Priority p)
        {
            foreach (var display in Outputs)
                if ((Refresh[display.Key] & p) != 0) //is display priority the same as current update's priority?
                {                                                                   // i.e. do we update display on this tick
                    foreach (var name in display.Value.Keys)
                        if (CommandUsers[display.Key].Contains(name) && (display.Value[name].Priority & p) != 0) //is command priority the same as current update priority?
                        {
                            display.Value[name].Command.Invoke(display.Value[name]);
                            if (display.Value[name].Builder)
                                InfoUtility.ApplyBuilder(display.Value[name]);
                        }//i.e. do we run command on this tick
                    var frame = display.Key.DrawFrame();
                    foreach (var sprite in display.Value)
                        DrawNewSprite(ref frame, sprite.Value);
                    frame.Dispose();
                }

        }

    }
}