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
        public Dictionary<string, Action<SpriteData>> Commands;
        public Dictionary<IMyTextSurface, Dictionary<string, SpriteData>> Outputs;
        public Dictionary<IMyTextSurface, UpdateFrequency> RefreshFreqencies;
        public string Name;
        public long dEID;

        #endregion

        public abstract void Setup(IMyTerminalBlock block);

        public abstract void Update(ref UpdateType sourceFlags);

        protected void DrawNewSprite(ref MySpriteDrawFrame frame, SpriteData data)
        {
            if (data.uID == -1)
            {
                frame.Add(data.sprCached);
                return;
            }
            //Program.Me.CustomData += $"\n{s.Type}, \n{s.Data}, \n{s.Size}, \n{s.Position}, \n{s.Color}, \n{s.Alignment}\n";
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
            RefreshFreqencies = new Dictionary<IMyTextSurface, UpdateFrequency>();
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
        bool TryAddSprites(ref IMyTextSurface thisSurface, ref Parser myParser, ref byte index, out UpdateFrequency screenFrequency)
        {
            string sect;
            screenFrequency = Utilities.Update;
            var good = true;
            if (!isSingleScreen)
                sect = $"{Keys.ScreenSection}_{index}";
            else
                sect = Keys.ScreenSection;
            Program.Echo(sect);
            CommandUsers.Add(thisSurface, new HashSet<string>());
            if (myParser.hasSection(sect))
            {
                thisSurface.ContentType = ContentType.SCRIPT;
                thisSurface.Script = "";
                thisSurface.ScriptBackgroundColor = myParser.Color(sect, Keys.ColorKey + "_BG");
                var names = myParser.String(sect, Keys.ListKey);
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
                            //Program.Me.CustomData += s.Name + new_line;
                            // >TYPE
                            s.Type = myParser.Type(nametag, Keys.TypeKey);
                            //Program.Me.CustomData += s.Type.ToString() + new_line;
                            // >DATA
                            if (myParser.hasKey(nametag, Keys.DataKey))
                                s.Data = myParser.String(nametag, Keys.DataKey, "FAILED");
                            else s.Data = "";
                            //Program.Me.CustomData += s.Data + new_line;
                            // >SIZE
                            CartesianReader(ref s, ref myParser, Keys.SizeKey, nametag);
                            //Program.Me.CustomData += $"[{s.SizeX}, {s.SizeY}]" + new_line;
                            // >ALIGN
                            s.Alignment = myParser.Alignment(nametag, Keys.AlignKey);
                            //Program.Me.CustomData += s.Alignment.ToString() + new_line;
                            // >POSITION
                            CartesianReader(ref s, ref myParser, Keys.PositionKey, nametag);
                            //Program.Me.CustomData += $"[{s.PosX}, {s.PosY}]" + new_line;
                            //COLOR
                            s.Color = myParser.Color(nametag, Keys.ColorKey);
                            // >ROTATION/SCALE
                            s.RorS = myParser.Float(nametag, (s.Type == Utilities.defaultType ? Keys.ScaleKey : Keys.RotationKey), float.NaN);
                            if (float.IsNaN(s.RorS))
                                s.RorS = myParser.Float(nametag, (s.Type == Utilities.defaultType ? Keys.RotationKey : Keys.ScaleKey));
                            //Program.Me.CustomData += $"[{s.RorS}]" + new_line;
                            // >FONT
                            if (myParser.hasKey(nametag, Keys.FontKey))
                                s.FontID = s.Type == Utilities.defaultType ? myParser.String(nametag, Keys.FontKey, "Monospace") : "";
                            // >UPDATE
                            if (myParser.hasKey(nametag, Keys.UpdateKey))
                            {
                                var update = myParser.Byte(nametag, Keys.UpdateKey, 0);
                                s.CommandFrequency = (UpdateFrequency)update;
                                screenFrequency |= s.CommandFrequency;
                       
                            }
                            else
                                s.CommandFrequency = Utilities.Update;
                            
                            // >COMMAND
                            if (myParser.hasKey(nametag, Keys.CommandKey) && s.CommandFrequency != 0)
                            {
                                if (s.CommandFrequency != Utilities.Update && s.CommandString != "")
                                {
                                    // uID
                                    s.uID = dEID + index + Array.IndexOf(nArray, name);
                                    // >PREPEND
                                    if (s.Builder && myParser.hasKey(nametag, Keys.PrependKey))
                                        s.Prepend = myParser.String(nametag, Keys.PrependKey) + " ";
                                    // >APPEND
                                    if (s.Builder && myParser.hasKey(nametag, Keys.AppendKey))
                                        s.Append = " " + myParser.String(nametag, Keys.AppendKey);
                                    // >USEBUILDER
                                    s.Builder = !s.Prepend.Equals("") || !s.Append.Equals("");
                                    s.CommandString = s.CommandFrequency == Utilities.Update ? "" : myParser.String(nametag, Keys.CommandKey, "!def");
                                    if (!Commands.ContainsKey(s.CommandString))
                                        throw new Exception($"PARSE FAILURE: sprite {s.Name} on screen {Name} has invalid command {s.CommandString}");
                                    CommandUsers[thisSurface].Add(s.Name);
                                    s.Command = Commands[s.CommandString];
                                    s.Command.Invoke(s);
                                }
                            }
                            s.sprCached = SpriteData.createSprite(s, true);
                            // We're done!
                            if (!Outputs[thisSurface].ContainsKey(s.Name))
                                Outputs[thisSurface].Add(s.Name, s);
                        }
                        else
                            good = false;
                    }
            }             
            return good;
        }

        void CartesianReader(ref SpriteData sprite, ref Parser myParser, string key, string nametag)
        {
            var coords = myParser.String(nametag, key).Split(',');

            if (key == Keys.PositionKey)
            {
                sprite.PosX = float.Parse(coords.First().Trim(Keys.l_coord));
                sprite.PosY = float.Parse(coords.Last().Trim(Keys.r_coord));
            }

            else if (key == Keys.SizeKey && sprite.Type != Utilities.defaultType)
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
            var freq = Utilities.Update;
                if (MyParser.CustomData(block, out Result))
                {
                    if (block is IMyTextSurface)
                    {
                        var DisplayBlock = (IMyTextSurface)block;
                        Outputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());

                        isSingleScreen = true;
                        if (TryAddSprites(ref DisplayBlock, ref MyParser, ref index, out freq))
                        {
                            RefreshFreqencies.Add(DisplayBlock, freq);
                        }

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
                            if (TryAddSprites(ref surface, ref MyParser, ref index, out freq)/* && RefreshFreqencies.hasKey(surface) WHAT TEH FUCK WHY DID I DO THIS*/)
                                RefreshFreqencies.Add(surface, freq);
                            else RefreshFreqencies.Add(surface, Utilities.Update);
                        }
                    }
                }
                else throw new Exception($" PARSE FAILURE: {Name} cd error {Result.Error} at {Result.LineNo}");
                MyParser.Dispose();

                foreach (var display in Outputs)
                {
                    var frame = display.Key.DrawFrame();
                    //                var piss = display.Key.TextureSize * 0.5f;
                    foreach (var sprite in display.Value)
                    {
                        DrawNewSprite(ref frame, sprite.Value);
                        //Program.Me.CustomData += s.Value.Name + new_line;
                    }
                    frame.Dispose();
                }

                foreach (var updateFrequency in RefreshFreqencies.Values)
                {
                    // Program.Echo($"{UpdateFrequency} |= {updateFrequency}");
                    UpdateFrequency |= updateFrequency;
                    // Program.Echo($"{UpdateFrequency}");
                }
            
        }

        public override void Update(ref UpdateType sourceFlags)
        {
            var sourceFreqFlags = Utilities.Converter(sourceFlags);
            foreach (var display in Outputs)
                if ((RefreshFreqencies[display.Key] & sourceFreqFlags) != 0) //is display frequency the same as frequency of update source?
                {                                                                   // i.e. do we update display on this tick
                    //Program.Me.CustomData += $"UPDATED {display}, {Program.Runtime.TimeSinceLastRun}\n";
                    foreach (var name in display.Value.Keys)
                        if (CommandUsers[display.Key].Contains(name) && (display.Value[name].CommandFrequency & sourceFreqFlags) != 0) //is command frequency the same as frequency of update source?
                        {
                            display.Value[name].Command.Invoke(display.Value[name]);
                            if (display.Value[name].Builder)
                                InfoUtility.ApplyBuilder(display.Value[name]);
                        }//i.e. do we run command on this tick
                    var frame = display.Key.DrawFrame();
                    var piss = display.Key.TextureSize * 0.5f;
                    foreach (var sprite in display.Value)
                        DrawNewSprite(ref frame, sprite.Value);
                    frame.Dispose();
                }

        }

    }
}