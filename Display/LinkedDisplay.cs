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
    public class LinkedDisplay
    {
        #region fields

        public UpdateFrequency UpdateFrequency;
        public MyGridProgram Program;
        public Dictionary<IMyTextSurface, Dictionary<string, SpriteData>> DisplayOutputs;
        public Dictionary<string, Action<SpriteData>> Commands;
        public Dictionary<IMyTextSurface, HashSet<string>> CommandUsers;
        public Dictionary<IMyTextSurface, UpdateFrequency> DisplayRefreshFreqencies;
        public string
            DisplayName,
            ScreenSection,
            SpriteSection,
            ListKey,
            TypeKey,
            DataKey,
            SizeKey,
            AlignKey,
            PositionKey,
            RotationScaleKey,
            ColorKey,
            FontKey,
            CommandKey,
            UpdateKey;
        internal char
            l_coord = '(',
            r_coord = ')',
            new_line = '\n',
            new_entry = '>';
        bool isSingleScreen;

        #endregion

        public LinkedDisplay(IMyTerminalBlock block, ref Dictionary<string, Action<SpriteData>> commandsDict, ref MyGridProgram program)
        {
            Commands = commandsDict;
            CommandUsers = new Dictionary<IMyTextSurface, HashSet<string>>();
            DisplayRefreshFreqencies = new Dictionary<IMyTextSurface, UpdateFrequency>();
            DisplayOutputs = new Dictionary<IMyTextSurface, Dictionary<string, SpriteData>>();
            DisplayName = block.CustomName;
            Program = program;
        }

        #region CustomDataFormat

        // [Custom Data Formatting]
        //
        // I didn't want the list...but look, it simplifies parsing custom data SO, so much
        // split at '>', trim at '\n'
        //
        //[SCREEN 0]
        // >COLOR_BG FA6464FF
        // >LIST = [FRAME, TOPBAR, ...]
        //[SPRITE FRAME]
        // >TYPE {byte 0/2/4} => if this is 2, we can skip size section
        // >DATA SquareSimple
        // >SIZE[50, 50]
        // >ALIGN {byte 0/1/2}
        // >POSITION[256, 256] => ALWAYS in screen coordinates
        // >ROTATION/SCALE 0
        // >COLOR FA6464FF
        // >FONT don't even LOOK for this if it's a texture
        // >UPDATE 0x1
        // >CMD !h2
        // [SPRITE TOPBAR]
        //   ...
        // and so on

        #endregion
        internal virtual bool TryAddSprites(ref IMyTextSurface thisSurface, ref Parser myParser, ref byte index, out UpdateFrequency screenFrequency)
        {
            screenFrequency = SharedUtilities.defaultUpdate;
            var didNotFail = true;
            if (!isSingleScreen)
                ScreenSection = $"{ScreenSection}_{index}";
            CommandUsers.Add(thisSurface, new HashSet<string>());
            if (myParser.ContainsSection(ScreenSection))
            {
                thisSurface.ContentType = ContentType.SCRIPT;
                thisSurface.Script = "";
                thisSurface.ScriptBackgroundColor = myParser.ParseColor(ScreenSection, ColorKey + "_BG");
                var names = myParser.ParseString(ScreenSection, ListKey);
                var namesArray = names.Split(new_line);
                for (int i = 0; i < namesArray.Length; ++i)
                { namesArray[i] = namesArray[i].Trim(new_entry); namesArray[i] = namesArray[i].Trim(); Program.Echo(namesArray[i]); }

                if (namesArray.Count() > 0)
                    foreach (var name in namesArray)
                    {
                        
                        var nametag = $"{SpriteSection}_{name}";

                        if (myParser.ContainsSection(nametag) && namesArray.Contains(name))
                        {
                            SpriteData sprite = new SpriteData();
                            //Name
                            sprite.Name = name;
                            //Program.Me.CustomData += sprite.Name + new_line;
                            // >TYPE
                            sprite.spriteType = (SpriteType)myParser.ParseByte(nametag, TypeKey);
                            //Program.Me.CustomData += sprite.spriteType.ToString() + new_line;
                            // >DATA
                            sprite.Data = myParser.ParseString(nametag, DataKey, "FAILED");
                            //Program.Me.CustomData += sprite.Data + new_line;
                            // >SIZE
                            CartesianReader(ref sprite, ref myParser, SizeKey, nametag);
                            //Program.Me.CustomData += $"[{sprite.SpriteSizeX}, {sprite.SpriteSizeY}]" + new_line;
                            // >ALIGN
                            sprite.SpriteAlignment = (TextAlignment)myParser.ParseByte(nametag, AlignKey);
                            //Program.Me.CustomData += sprite.SpriteAlignment.ToString() + new_line;
                            // >POSITION
                            CartesianReader(ref sprite, ref myParser, PositionKey, nametag);
                            //Program.Me.CustomData += $"[{sprite.SpritePosX}, {sprite.SpritePosY}]" + new_line;
                            //COLOR
                            sprite.SpriteColor = myParser.ParseColor(nametag, ColorKey);
                            // >ROTATION/SCALE
                            sprite.SpriteRorS = myParser.ParseFloat(nametag, RotationScaleKey);
                            //Program.Me.CustomData += $"[{sprite.SpriteRorS}]" + new_line;
                            // >FONT
                            if (myParser.ContainsKey(nametag, FontKey))
                                sprite.FontID = sprite.spriteType == SharedUtilities.defaultType ? myParser.ParseString(nametag, FontKey, "White") : "";
                            // >UPDATE
                            if (myParser.ContainsKey(nametag, UpdateKey))
                            {
                                sprite.CommandFrequency = (UpdateFrequency)myParser.ParseByte(nametag, UpdateKey, 0);
                                screenFrequency |= sprite.CommandFrequency;
                            }
                            else
                                sprite.CommandFrequency = SharedUtilities.defaultUpdate;
                            
                            // >COMMAND
                            if (myParser.ContainsKey(nametag, CommandKey))
                            {
                                if (sprite.CommandFrequency != SharedUtilities.defaultUpdate && sprite.CommandString != "")
                                {
                                    sprite.CommandString = sprite.CommandFrequency == SharedUtilities.defaultUpdate ? "" : myParser.ParseString(nametag, CommandKey, "!def");
                                    CommandUsers[thisSurface].Add(sprite.Name);
                                    sprite.Command = Commands[sprite.CommandString];
                                    sprite.Command.Invoke(sprite);
                                }
                            }
                            if (!DisplayOutputs[thisSurface].ContainsKey(sprite.Name))
                                DisplayOutputs[thisSurface].Add(sprite.Name, sprite);
                        }
                        else
                            didNotFail = false;

                    }
            }
            return didNotFail;
        }

        internal void CartesianReader(ref SpriteData sprite, ref Parser myParser, string key, string nametag)
        {
            var coords = myParser.ParseString(nametag, key).Split(',');

            if (key == PositionKey)
            {
                sprite.SpritePosX = float.Parse(coords.First().Trim(l_coord));
                sprite.SpritePosY = float.Parse(coords.Last().Trim(r_coord));
            }

            else if (key == SizeKey && sprite.spriteType != SharedUtilities.defaultType)
            {
                sprite.SpriteSizeX = float.Parse(coords.First().Trim(l_coord));
                sprite.SpriteSizeY = float.Parse(coords.Last().Trim(r_coord));
            }
        }

        internal void SetKeys() //yes...this is questionable...
        {
            ScreenSection = "SECT_SCREEN";
            SpriteSection = "SECT_SPRITE";
            ListKey = "K_LIST";
            TypeKey = "K_TYPE";
            DataKey = "K_DATA";
            SizeKey = "K_SIZE";
            AlignKey = "K_ALIGN";
            PositionKey = "K_COORD";
            RotationScaleKey = "K_ROTSCAL";
            ColorKey = "K_COLOR";
            FontKey = "K_FONT";
            CommandKey = "K_CMD";
            UpdateKey = "K_UPDT";
        }

        public virtual void Setup<T>(T block)
            where T : IMyTerminalBlock
        {
            SetKeys();
            Parser MyParser = new Parser();
            byte index = 0;          
            MyIniParseResult Result;
            var freq = SharedUtilities.defaultUpdate;

            if (MyParser.TryParseCustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    DisplayOutputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());

                    isSingleScreen = true;
                    if (TryAddSprites(ref DisplayBlock, ref MyParser, ref index, out freq))
                    {
                        DisplayRefreshFreqencies.Add(DisplayBlock, freq);
                    }

                }
                else if (block is IMyTextSurfaceProvider)
                {
                    //TODO: I fucked up here somewhere...
                    isSingleScreen = false;
                    var DisplayBlock = (IMyTextSurfaceProvider)block;
                    var SurfaceCount = DisplayBlock.SurfaceCount;
                    var surface = DisplayBlock.GetSurface(index);
                    while (index <= SurfaceCount - 1)
                    {
                        if (!DisplayOutputs.ContainsKey(surface))
                            DisplayOutputs.Add(surface, new Dictionary<string, SpriteData>());
                        if (TryAddSprites(ref surface, ref MyParser, ref index, out freq))
                        {
                            DisplayRefreshFreqencies.Add(surface, freq);
                            ++index;
                        }
                    }
                }
                
            }
            else throw new Exception($" PARSE FAILURE: {DisplayName} cd error {Result.Error} at {Result.LineNo}");
            MyParser.Dispose();

            foreach (var display in DisplayOutputs)
            {
                var frame = display.Key.DrawFrame();
                var piss = display.Key.TextureSize * 0.5f;
                foreach (var sprite in display.Value)
                {
                    
                    DrawNewSprite(ref frame, ref piss, sprite.Value);
                    Program.Me.CustomData += sprite.Value.Name + new_line;
                }                 
                frame.Dispose();
            }

            foreach (var updateFrequency in DisplayRefreshFreqencies.Values)
                UpdateFrequency |= updateFrequency;
        }

        public void DrawNewSprite(ref MySpriteDrawFrame frame, ref Vector2 center, SpriteData data)
        {
            //this is kind of a retarded setup but it still is shortedr
            //sprite.Type = data.spriteType;
            //sprite.Data = data.Data;

            //if (sprite.Type != SharedUtilities.defaultType)
            //    sprite.Size = new Vector2(data.SpriteSizeY, data.SpriteSizeX);

            //sprite.Alignment = data.SpriteAlignment;
            //sprite.Position = new Vector2(data.SpritePosX, data.SpritePosY);
            //sprite.Color = data.SpriteColor;

            //if (sprite.Type == SharedUtilities.defaultType)
            //    sprite.FontId = data.FontID;

            //sprite.RotationOrScale = data.SpriteRorS;
            //frame.Add(sprite);
            var sprite = data.spriteType == SharedUtilities.defaultType ? new MySprite(
                data.spriteType,
                data.Data,
                new Vector2(data.SpritePosX, data.SpritePosY), // + center,
                null,
                data.SpriteColor,
                data.FontID,
                data.SpriteAlignment,
                data.SpriteRorS
                )
                : new MySprite(
                data.spriteType,
                data.Data,
                new Vector2(data.SpritePosX, data.SpritePosY), // + center,
                new Vector2(data.SpriteSizeX, data.SpriteSizeY),
                data.SpriteColor,
                null,
                data.SpriteAlignment,
                data.SpriteRorS
                    );
            Program.Me.CustomData += $"\n{sprite.Type}, \n{sprite.Data}, \n{sprite.Size}, \n{sprite.Position}, \n{sprite.Color}, \n{sprite.Alignment}\n";
            frame.Add(sprite);

        }

        public virtual void Update(ref UpdateType sourceFlags)
        {
            var sourceFreqFlags = SharedUtilities.UpdateConverter(sourceFlags);

            foreach (var display in DisplayOutputs)
                if ((DisplayRefreshFreqencies[display.Key] & sourceFreqFlags) != 0) //is display frequency the same as frequency of update source?
                {                                                                   // i.e. do we update display on this tick
                    foreach (var name in display.Value.Keys)
                        if (CommandUsers[display.Key].Contains(name) && (display.Value[name].CommandFrequency & sourceFreqFlags) != 0) //is command frequency the same as frequency of update source?
                            display.Value[name].Command.Invoke(display.Value[name]);                                                   //i.e. do we run command on this tick

                    var frame = display.Key.DrawFrame();
                    var piss = display.Key.TextureSize * 0.5f;
                    foreach (var sprite in display.Value)
                        DrawNewSprite(ref frame, ref piss, sprite.Value);
                    frame.Dispose();
                }

        }

    }
}