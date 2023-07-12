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
            screenFrequency = DrawUtilities.defaultUpdate;
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
                            // >TYPE
                            sprite.spriteType = (SpriteType)myParser.ParseByte(nametag, TypeKey);
                            // >DATA
                            sprite.Data = myParser.ParseString(nametag, DataKey, "FAILED");
                            // >SIZE
                            CartesianReader(ref sprite, ref myParser, SizeKey, nametag);
                            // >ALIGN
                            sprite.SpriteAlignment = (TextAlignment)myParser.ParseByte(nametag, AlignKey);
                            // >POSITION
                            CartesianReader(ref sprite, ref myParser, PositionKey, nametag);
                            // >ROTATION/SCALE
                            sprite.SpriteRorS = myParser.ParseFloat(nametag, RotationScaleKey);
                            // >FONT
                            if (myParser.ContainsKey(nametag, FontKey))
                                sprite.FontID = sprite.spriteType == DrawUtilities.defaultType ? myParser.ParseString(nametag, FontKey, "White") : "";
                            // >UPDATE
                            if (myParser.ContainsKey(nametag, UpdateKey))
                            {
                                sprite.CommandFrequency = (UpdateFrequency)myParser.ParseByte(nametag, UpdateKey, 0);
                                screenFrequency |= sprite.CommandFrequency;
                            }
                            else
                            {
                                sprite.CommandFrequency = DrawUtilities.defaultUpdate;
                                continue;
                            }
                            // >COMMAND
                            if (myParser.ContainsKey(nametag, CommandKey))
                            {
                                if (sprite.CommandFrequency != DrawUtilities.defaultUpdate && sprite.CommandString != "")
                                {
                                    sprite.CommandString = sprite.CommandFrequency == DrawUtilities.defaultUpdate ? "" : myParser.ParseString(nametag, CommandKey, "!def");
                                    CommandUsers[thisSurface].Add(sprite.Name);
                                    sprite.Command = Commands[sprite.CommandString];
                                    sprite.Command.Invoke(sprite);
                                }
                            }
                                

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
            
            if (key == SizeKey)
                if (sprite.spriteType != DrawUtilities.defaultType)
                {
                    sprite.SpriteSizeX = float.Parse(coords.First().Trim(l_coord));
                    sprite.SpriteSizeY = float.Parse(coords.Last().Trim(r_coord));
                }

                else if (key == PositionKey)
                {
                    sprite.SpritePosX = float.Parse(coords.First().Trim(l_coord));
                    sprite.SpritePosY = float.Parse(coords.Last().Trim(r_coord));
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
            var freq = DrawUtilities.defaultUpdate;

            if (MyParser.TryParseCustomData(block, out Result))
            {
                if (block is IMyTextSurface)
                {
                    var DisplayBlock = (IMyTextSurface)block;
                    isSingleScreen = true;
                    if (TryAddSprites(ref DisplayBlock, ref MyParser, ref index, out freq))
                    {
                        DisplayOutputs.Add(DisplayBlock, new Dictionary<string, SpriteData>());
                        DisplayRefreshFreqencies.Add(DisplayBlock, freq);
                    }

                }
                else if (block is IMyTextSurfaceProvider)
                {
                    isSingleScreen = false;
                    var DisplayBlock = (IMyTextSurfaceProvider)block;
                    var SurfaceCount = DisplayBlock.SurfaceCount;
                    var surface = DisplayBlock.GetSurface(index);
                    while (index <= SurfaceCount - 1)
                    {
                        if (TryAddSprites(ref surface, ref MyParser, ref index, out freq))
                        {
                            DisplayOutputs.Add(surface, new Dictionary<string, SpriteData>());
                            DisplayRefreshFreqencies.Add(surface, freq);
                            ++index;
                        }
                    }
                }
                
            }
            else throw new Exception($" PARSE FAILURE: {DisplayName} cd error {Result.Error} at {Result.LineNo}");

            foreach (var display in DisplayOutputs)
            {
                var frame = display.Key.DrawFrame();
                foreach (var sprite in display.Value)
                    DrawUtilities.DrawNewSprite(ref frame, sprite.Value);
                frame.Dispose();
            }

            foreach (var updateFrequency in DisplayRefreshFreqencies.Values)
                UpdateFrequency |= updateFrequency;
        }

        public virtual void Update(ref UpdateType sourceFlags)
        {
            var sourceFreqFlags = UpdateUtilities.UpdateConverter(sourceFlags);

            foreach (var display in DisplayOutputs)
                if ((DisplayRefreshFreqencies[display.Key] & sourceFreqFlags) != 0) //is display frequency the same as frequency of update source?
                {                                                                   // i.e. do we update display on this tick
                    foreach (var name in display.Value.Keys)
                        if (CommandUsers[display.Key].Contains(name) && (display.Value[name].CommandFrequency & sourceFreqFlags) != 0) //is command frequency the same as frequency of update source?
                            display.Value[name].Command.Invoke(display.Value[name]);                                                   //i.e. do we run command on this tick

                    var frame = display.Key.DrawFrame();

                    foreach (var sprite in display.Value)
                        DrawUtilities.DrawNewSprite(ref frame, sprite.Value);
                    frame.Dispose();
                }

        }

    }
}