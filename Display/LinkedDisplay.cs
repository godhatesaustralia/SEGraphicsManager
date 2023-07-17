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
        internal DisplayIniKeys Keys;
        public string DisplayName;
        public long DisplayID;
        bool isSingleScreen;

        #endregion

        public LinkedDisplay(IMyTerminalBlock block, ref Dictionary<string, Action<SpriteData>> commandsDict, ref MyGridProgram program, ref DisplayIniKeys keys)
        {
            Commands = commandsDict;
            CommandUsers = new Dictionary<IMyTextSurface, HashSet<string>>();
            DisplayRefreshFreqencies = new Dictionary<IMyTextSurface, UpdateFrequency>();
            DisplayOutputs = new Dictionary<IMyTextSurface, Dictionary<string, SpriteData>>();
            DisplayName = block.CustomName;
            DisplayID = block.EntityId;
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
        internal virtual bool TryAddSprites(ref IMyTextSurface thisSurface, ref Parser myParser, ref byte index, out UpdateFrequency screenFrequency)
        {
            string ScreenSection;
            screenFrequency = SharedUtilities.defaultUpdate;
            var didNotFail = true;
            if (!isSingleScreen)
                ScreenSection = $"{Keys.ScreenSection}_{index}";
            else
                ScreenSection = Keys.ScreenSection;
            Program.Echo(ScreenSection);
            CommandUsers.Add(thisSurface, new HashSet<string>());
            if (myParser.ContainsSection(ScreenSection))
            {
                thisSurface.ContentType = ContentType.SCRIPT;
                thisSurface.Script = "";
                thisSurface.ScriptBackgroundColor = myParser.ParseColor(ScreenSection, Keys.ColorKey + "_BG");
                var names = myParser.ParseString(ScreenSection, Keys.ListKey);
                var namesArray = names.Split(Keys.new_line);
                for (int i = 0; i < namesArray.Length; ++i)
                { namesArray[i] = namesArray[i].Trim(Keys.new_entry); namesArray[i] = namesArray[i].Trim(); Program.Echo(namesArray[i]); }

                if (namesArray.Count() > 0)
                    foreach (var name in namesArray)
                    {
                        
                        var nametag = $"{Keys.SpriteSection}_{name}";

                        if (myParser.ContainsSection(nametag) && namesArray.Contains(name))
                        {

                            SpriteData sprite = new SpriteData();
                            //Name
                            sprite.Name = name;
                            //Program.Me.CustomData += sprite.Name + new_line;
                            // >TYPE
                            sprite.spriteType = (SpriteType)myParser.ParseByte(nametag, Keys.TypeKey);
                            //Program.Me.CustomData += sprite.spriteType.ToString() + new_line;
                            // >DATA
                            sprite.Data = myParser.ParseString(nametag, Keys.DataKey, "FAILED");
                            //Program.Me.CustomData += sprite.Data + new_line;
                            // >SIZE
                            CartesianReader(ref sprite, ref myParser, Keys.SizeKey, nametag);
                            //Program.Me.CustomData += $"[{sprite.SpriteSizeX}, {sprite.SpriteSizeY}]" + new_line;
                            // >ALIGN
                            sprite.SpriteAlignment = (TextAlignment)myParser.ParseByte(nametag, Keys.AlignKey);
                            //Program.Me.CustomData += sprite.SpriteAlignment.ToString() + new_line;
                            // >POSITION
                            CartesianReader(ref sprite, ref myParser, Keys.PositionKey, nametag);
                            //Program.Me.CustomData += $"[{sprite.SpritePosX}, {sprite.SpritePosY}]" + new_line;
                            //COLOR
                            sprite.SpriteColor = myParser.ParseColor(nametag, Keys.ColorKey);
                            // >ROTATION/SCALE
                            sprite.SpriteRorS = myParser.ParseFloat(nametag, Keys.RotationScaleKey);
                            //Program.Me.CustomData += $"[{sprite.SpriteRorS}]" + new_line;
                            // >FONT
                            if (myParser.ContainsKey(nametag, Keys.FontKey))
                                sprite.FontID = sprite.spriteType == SharedUtilities.defaultType ? myParser.ParseString(nametag, Keys.FontKey, "Monospace") : "";
                            // >UPDATE
                            if (myParser.ContainsKey(nametag, Keys.UpdateKey))
                            {
                                sprite.CommandFrequency = (UpdateFrequency)myParser.ParseByte(nametag, Keys.UpdateKey, 0);
                                screenFrequency |= sprite.CommandFrequency;
                            }
                            else
                                sprite.CommandFrequency = SharedUtilities.defaultUpdate;
                            
                            // >COMMAND
                            if (myParser.ContainsKey(nametag, Keys.CommandKey) && sprite.CommandFrequency != 0)
                            {
                                if (sprite.CommandFrequency != SharedUtilities.defaultUpdate && sprite.CommandString != "")
                                {
                                    // UniqueID
                                    sprite.UniqueID = DisplayID + index + Array.IndexOf(namesArray, name);
                                    // >USEBUILDER
                                    if (myParser.ContainsKey(nametag, Keys.BuilderKey))
                                        sprite.UseStringBuilder = myParser.ParseBool(nametag, Keys.BuilderKey);
                                    // >PREPEND
                                    if (sprite.UseStringBuilder && myParser.ContainsKey(nametag, Keys.PrependKey))
                                        sprite.BuilderPrepend = myParser.ParseString(nametag, Keys.PrependKey);
                                    // >APPEND
                                    if (sprite.UseStringBuilder && myParser.ContainsKey(nametag, Keys.AppendKey))
                                        sprite.BuilderAppend = myParser.ParseString(nametag, Keys.AppendKey);

                                    sprite.CommandString = sprite.CommandFrequency == SharedUtilities.defaultUpdate ? "" : myParser.ParseString(nametag, Keys.CommandKey, "!def");
                                    CommandUsers[thisSurface].Add(sprite.Name);
                                    sprite.Command = Commands[sprite.CommandString];
                                    sprite.Command.Invoke(sprite);
                                }
                            }

                            // We're done!
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

            if (key == Keys.PositionKey)
            {
                sprite.SpritePosX = float.Parse(coords.First().Trim(Keys.l_coord));
                sprite.SpritePosY = float.Parse(coords.Last().Trim(Keys.r_coord));
            }

            else if (key == Keys.SizeKey && sprite.spriteType != SharedUtilities.defaultType)
            {
                sprite.SpriteSizeX = float.Parse(coords.First().Trim(Keys.l_coord));
                sprite.SpriteSizeY = float.Parse(coords.Last().Trim(Keys.r_coord));
            }
        }

        public virtual void Setup<T>(T block)
            where T : IMyTerminalBlock
        {
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
                    isSingleScreen = false;
                    var DisplayBlock = (IMyTextSurfaceProvider)block;
                    var SurfaceCount = DisplayBlock.SurfaceCount;
                    
                    for (index = 0; index < SurfaceCount; ++index)
                    {
                        var surface = DisplayBlock.GetSurface(index);
                        if (!DisplayOutputs.ContainsKey(surface))
                            DisplayOutputs.Add(surface, new Dictionary<string, SpriteData>());
                        if (TryAddSprites(ref surface, ref MyParser, ref index, out freq)/* && DisplayRefreshFreqencies.ContainsKey(surface) WHAT TEH FUCK WHY DID I DO THIS*/)
                            DisplayRefreshFreqencies.Add(surface, freq);
                        else DisplayRefreshFreqencies.Add(surface, SharedUtilities.defaultUpdate);
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
                    //Program.Me.CustomData += sprite.Value.Name + new_line;
                }                 
                frame.Dispose();
            }

            foreach (var updateFrequency in DisplayRefreshFreqencies.Values)
            {
                Program.Echo($"{UpdateFrequency} |= {updateFrequency}");
                UpdateFrequency |= updateFrequency;
                Program.Echo($"{UpdateFrequency}");
            }
                
                
        }

        public void DrawNewSprite(ref MySpriteDrawFrame frame, ref Vector2 center, SpriteData data)
        {
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
                MathHelper.ToRadians(data.SpriteRorS)
                    );
            //Program.Me.CustomData += $"\n{sprite.Type}, \n{sprite.Data}, \n{sprite.Size}, \n{sprite.Position}, \n{sprite.Color}, \n{sprite.Alignment}\n";
            frame.Add(sprite);

        }

        public virtual void Update(ref UpdateType sourceFlags)
        {
            var sourceFreqFlags = SharedUtilities.UpdateConverter(sourceFlags);
            foreach (var display in DisplayOutputs)
                if ((DisplayRefreshFreqencies[display.Key] & sourceFreqFlags) != 0) //is display frequency the same as frequency of update source?
                {                                                                   // i.e. do we update display on this tick
                    Program.Me.CustomData += $"UPDATED {display}, {Program.Runtime.TimeSinceLastRun}\n";
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