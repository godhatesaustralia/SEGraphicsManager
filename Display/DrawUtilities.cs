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
    public static class Utilities
    {
        static public int increment = 9;
        static public SpriteType dType = SpriteType.TEXT;
        static public UpdateFrequency
            uDef = UpdateFrequency.None,
            u10 = UpdateFrequency.Update10,
            u100 = UpdateFrequency.Update100;
        static public Color dColor = Color.HotPink;


        public static UpdateFrequency Converter(UpdateType source)
        {
            var updateFrequency = UpdateFrequency.None; //0000
            if ((source & UpdateType.Update1) != 0) updateFrequency |= UpdateFrequency.Update1; //0001
            if ((source & UpdateType.Update10) != 0) updateFrequency |= UpdateFrequency.Update10; //0010
            if ((source & UpdateType.Update100) != 0) updateFrequency |= UpdateFrequency.Update100;//0100
            return updateFrequency;
        }
        public static UpdateType LConverter(UpdateFrequency f)
        {
            var u = UpdateType.None; //0000
            if ((f & UpdateFrequency.Update1) != 0) u |= UpdateType.Update1; //0001
            if ((f & u10) != 0) u |= UpdateType.Update10; //0010
            if ((f & u100) != 0) u |= UpdateType.Update100;//0100
            return u;
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