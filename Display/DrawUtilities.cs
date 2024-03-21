using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.AI.Logic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.Entities.Blocks;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading;
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
    public static class Util
    {
        static public SpriteType dType = SpriteType.TEXT;
        static public UpdateFrequency uDef = UpdateFrequency.None;
        static public Color dColor = Color.HotPink;
        static public string bsod = "A problem has been detected and Windows has been shut down to prevent damage \r\nto your computer. \r\nUNMOUNTABLE_BOOT_VOLUME \r\nIf this is the first time you've seen this error screen, \r\nrestart your computer. If this screen appears again, follow \r\nthese steps: \r\nCheck to be sure you have adequate disk space. If a driver is \r\nidentified in the Stop message, disable the driver or check \r\nwith the manufacturer for driver updates. Try changing video \r\nadapters. \r\nCheck with your hardware vendor for any BIOS updates. Disable \r\nBIOS memory options such as caching or shadowing. \r\nIf you need to use Safe Mode to remove or disable components, restart \r\nyour computer, press F8 to select Advanced Startup Options, and then \r\nselect Safe Mode. \r\n \r\nTechnical Information: \r\n*** STOP: 0x000000ED(0x80F128D0, 0xC000009C, 0x00000000, 0x00000000) \r\n \r\n";
        public static int Next(ref int p, int max)
        {
            if (p < max)
                p++;
            if (p == max)
                p = 0;
            return p;
        }
        public static void CreateBarGraph(ref SpriteData d)
        {
            bool horizontal = d.SizeX > d.SizeY || d.SizeX == d.SizeY;
            InfoUtility.GraphStorage.Add(d.uID, new MyTuple<bool, float>(horizontal, horizontal ? d.SizeX : d.SizeY));
        }
        public static void UpdateBarGraph(ref SpriteData data, double pctData)
        { 
            var graph = InfoUtility.GraphStorage[data.uID];
            if (graph.Item1) data.SizeX = Convert.ToSingle(pctData) * graph.Item2;
            else data.SizeY = Convert.ToSingle(pctData) * graph.Item2;
        }

        public static string EncodeSprites(ref LinkedDisplay display)
        // the idea: have this make the requisite SpriteData constructors here bc im too lazy
        // the constructor in question:
        //  public SpriteData(SpriteType type, string Name, string d, float sizeX, float sizeY, TextAlignment alignment,
        //  float posX, float posY, float ros, dColor color, string fontid = "White", UpdateFrequency updateType = UpdateFrequency.None,
        //  string command = "", bool builder = false, string prepend = "") (jesus christ)
        {
            var encodedOutput = "";
            var comma = ", ";
            foreach (var surface in display.Outputs)
            {
                encodedOutput += $"// screen {surface.Key.Name} background color {surface.Key.BackgroundColor}\n";
                foreach (var sprite in surface.Value.Values)
                {
                    encodedOutput += $"new SpriteData({sprite.Type}, {sprite.Name}, {sprite.Data}, {(sprite.Type != SpriteType.TEXT ? sprite.SizeX + comma + sprite.SizeY + comma : "")}" +
                        $"TextAlignment.{sprite.Alignment.ToString().ToUpper()}, {sprite.PosX}, {sprite.PosY}, " +
                        $"{sprite.RorS}, new Color({sprite.Color.R}, {sprite.Color.G}, {sprite.Color.B}, {sprite.Color.A}){(sprite.Type != SpriteType.TEXT ? "" : comma + sprite.FontID)}" +
                        $"{(sprite.Priority != Priority.None ? comma + sprite.Priority.ToString() + comma + sprite.CommandString + comma + sprite.Builder + (sprite.Builder ? comma + sprite.Prepend + comma + sprite.Append : "") : "")});\n";
                }
            }
            return encodedOutput;
        }

    }

    public class IniKeys //avoid allocating new memory for every display (i hope). just seems less dumb.
    {
        public string
            KeyTag,
            ScreenSection,
            SpriteSection,
            List,
            Type,
            Data,
            Size,
            Align,
            Pos,
            Rotation,
            Scale,
            Color,
            Font,
            Command,
            Update,
            UpdateOld,
            Prepend,
            Append;
        public char
            l_coord = '(',
            r_coord = ')',
            new_line = '\n',
            new_entry = '>';
        public void ResetKeys() //yes...this is questionable...
        {
            ScreenSection = "SECT_SCREEN";
            SpriteSection = "SECT_SPRITE";
            List = "LIST";
            Type = "TYPE";
            Data = "DATA";
            Size = "SIZE";
            Align = "ALIGN";
            Pos = "POS";
            Rotation = "ROTATION";
            Scale = "SCALE";
            Color = "COLOR";
            Font = "FONT";
            Command = "CMD";
            Update = "PRIORITY";
            UpdateOld = "UPDT";
            Prepend = "PREP";
            Append = "APP";
        }


    }
}