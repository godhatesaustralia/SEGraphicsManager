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
        static public SpriteType defaultType = SpriteType.TEXT;
        static public UpdateFrequency Update = UpdateFrequency.None;
        static public Color defaultColor = Color.HotPink;

        public static UpdateFrequency Converter(UpdateType source)
        {
            var updateFrequency = UpdateFrequency.None; //0000
            if ((source & UpdateType.Update1) != 0) updateFrequency |= UpdateFrequency.Update1; //0001
            if ((source & UpdateType.Update10) != 0) updateFrequency |= UpdateFrequency.Update10; //0010
            if ((source & UpdateType.Update100) != 0) updateFrequency |= UpdateFrequency.Update100;//0100
            return updateFrequency;
        }

        public static void CreateBarGraph(ref SpriteData data)
        {
            bool horizontal = data.SizeX > data.SizeY;
            InfoUtility.GraphStorage.Add(data.uID, new MyTuple<bool, float>(horizontal, horizontal ? data.SizeX : data.SizeY));
        }
        public static void UpdateBarGraph(ref SpriteData data, double pctData) // will not work with square. L
        { 
            var graph = InfoUtility.GraphStorage[data.uID];
            if (graph.Item1) data.SizeX = Convert.ToSingle(pctData) * graph.Item2;
            else data.SizeY = Convert.ToSingle(pctData) * graph.Item2;
        }

        public static string EncodeSprites(ref LinkedDisplay display)
        // the idea: have this make the requisite SpriteData constructors here bc im too lazy
        // the constructor in question:
        //  public SpriteData(SpriteType type, string Name, string data, float sizeX, float sizeY, TextAlignment alignment,
        //  float posX, float posY, float ros, Color color, string fontid = "White", UpdateFrequency updateType = UpdateFrequency.None,
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
                        $"{(sprite.CommandFrequency != UpdateFrequency.None ? comma + sprite.CommandFrequency.ToString() + comma + sprite.CommandString + comma + sprite.Builder + (sprite.Builder ? comma + sprite.Prepend + comma + sprite.Append : "") : "")});\n";
                }
            }
            return encodedOutput;
        }

    }

    public class IniKeys //avoid allocating new memory for every display (i hope). just seems less retarded.
    {
        public string
            KeyTag,
            ScreenSection,
            SpriteSection,
            ListKey,
            TypeKey,
            DataKey,
            SizeKey,
            AlignKey,
            PositionKey,
            RotationKey,
            ScaleKey,
            ColorKey,
            FontKey,
            CommandKey,
            UpdateKey,
            PrependKey,
            AppendKey;
        public char
            l_coord = '(',
            r_coord = ')',
            new_line = '\n',
            new_entry = '>';
        public void ResetKeys() //yes...this is questionable...
        {
            ScreenSection = "SECT_SCREEN";
            SpriteSection = "SECT_SPRITE";
            ListKey = "LIST";
            TypeKey = "TYPE";
            DataKey = "DATA";
            SizeKey = "SIZE";
            AlignKey = "ALIGN";
            PositionKey = "POS";
            RotationKey = "ROTATION";
            ScaleKey = "SCALE";
            ColorKey = "COLOR";
            FontKey = "FONT";
            CommandKey = "CMD";
            UpdateKey = "UPDT";
            PrependKey = "PREP";
            AppendKey = "APP";
        }


    }
}