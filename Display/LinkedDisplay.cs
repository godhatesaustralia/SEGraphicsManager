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

        public IMyTextSurfaceProvider thisBlock;
        public UpdateFrequency UpdateFrequency;
        public Dictionary<IMyTextSurface, Dictionary<string, SpriteData>> DisplayOutputs;
        public Dictionary<string, Action<SpriteData>> Commands;
        public Dictionary<IMyTextSurface, HashSet<string>> CommandUsers;
        public Dictionary<IMyTextSurface, UpdateFrequency> DisplayRefreshFreqencies;
        public int SurfaceCount;
        public List<string> Keys;
        public string
            DisplayName,
            ScreenSection,
            SpriteSection,
            ListSection,
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
        internal char[]
            toTrim,
            toSplit;
        bool isSingleScreen;

        #endregion

        public LinkedDisplay(IMyTerminalBlock block, ref Dictionary<string, Action<SpriteData>> commandsDict, ref List<string> keys)
        {
            thisBlock = block as IMyTextSurfaceProvider;
            Commands = commandsDict;
            DisplayName = block.CustomName;
            SurfaceCount = thisBlock.SurfaceCount;
            isSingleScreen = SurfaceCount == 1;
            toTrim = new char[] {'[', ']'};
            toSplit = new char[] { ',', ' ', '\n' };
            Keys = keys;
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
        internal virtual bool TryAddSprites(ref Parser myParser, ref byte index, out UpdateFrequency screenFrequency)
        {
            screenFrequency = UpdateFrequency.None;
            var nameList = new List<string>();
            var didNotFail = true;
            var ScreenSection = isSingleScreen ? (this.ScreenSection + "]") : (this.ScreenSection + $" {index}]");
            var surface = thisBlock.GetSurface(index);
            CommandUsers.Add(surface, new HashSet<string>());
            if (myParser.ContainsSection(ScreenSection)) 
            {
                surface.ContentType = ContentType.SCRIPT; 
                surface.Script = "";
                surface.ScriptBackgroundColor = myParser.ParseColor(ScreenSection, ColorKey + "_BG");
                nameList = myParser.ParseString(ScreenSection, ListSection, "").Trim(toTrim).Split(toSplit).ToList();
                if (nameList.Count > 0)
                    foreach (var name in nameList)
                    {
                        try
                        {
                            var nametag = SpriteSection + $" {name}]";
 
                            if (myParser.ContainsSection(nametag) && nameList.Contains(name))
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
                                sprite.FontID = sprite.spriteType == DrawUtilities.defaultType ? myParser.ParseString(nametag, FontKey, "White") : "";
                                // >UPDATE
                                sprite.CommandFrequency = (UpdateFrequency)myParser.ParseByte(nametag, UpdateKey, 0);
                                screenFrequency |= sprite.CommandFrequency;
                                // >COMMAND
                                sprite.CommandString = sprite.CommandFrequency == UpdateFrequency.None ? "" : myParser.ParseString(nametag, CommandKey, "");
                                if (sprite.CommandFrequency != UpdateFrequency.None && sprite.CommandString != "")
                                {
                                    CommandUsers[surface].Add(sprite.Name);
                                    sprite.Command = Commands[sprite.CommandString];
                                    sprite.Command.Invoke(sprite);                                  
                                }
                                DisplayOutputs[surface].Add(sprite.Name, sprite);
                            }
                            else
                                didNotFail = false;
                        }
                        catch (Exception ex)
                        {
                            var sourceString = ex.Source;
                            didNotFail = false;
                            //do something
                            return didNotFail;
                        }                                             
                    }
            }
            return didNotFail;
        }
        
        internal void CartesianReader(ref SpriteData sprite, ref Parser myParser, string key, string nametag)
        {
            var coords = myParser.ParseString(nametag, key, "").Trim(toTrim).Split(toSplit);
            if (key == SizeKey)
                if (sprite.spriteType != DrawUtilities.defaultType)
                {
                    sprite.SpriteSizeX = float.Parse(coords.First());
                    sprite.SpriteSizeY = float.Parse(coords.Last());
                }
            
            else if (key == PositionKey)
            {
                sprite.SpritePosX = float.Parse(coords.First());
                sprite.SpritePosY = float.Parse(coords.Last());
            }
        }

        internal void SetKeys() //yes...this is questionable...
        {
            ScreenSection = Keys[0];
            SpriteSection = Keys[1];
            ListSection = Keys[2];
            TypeKey = Keys[3];
            DataKey = Keys[4];
            SizeKey = Keys[5];
            AlignKey = Keys[6];
            PositionKey = Keys[7];
            RotationScaleKey = Keys[8];
            ColorKey = Keys[9];
            FontKey = Keys[10];
            CommandKey = ">CMD"; //DON'T ASK
            UpdateKey = Keys[11];
        }

        public virtual void Setup() 
        {
            SetKeys();
            Parser MyParser = new Parser();
            var block = thisBlock as IMyTerminalBlock;
            byte index = 0;
            if(MyParser.TryParseCustomData(block))
            {
                while (index <= SurfaceCount - 1)
                {
                    var freq = UpdateFrequency.None;
                    if (TryAddSprites(ref MyParser, ref index, out freq))
                    {
                        DisplayOutputs.Add(thisBlock.GetSurface(index), new Dictionary<string, SpriteData>());
                        DisplayRefreshFreqencies.Add(thisBlock.GetSurface(index), freq);
                        if (isSingleScreen)
                            return;
                    }

                        ++index;
                }               
            }

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