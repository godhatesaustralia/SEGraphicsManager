﻿using Sandbox.Engine.Platform.VideoMode;
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
    public static class SharedUtilities
    {
        static public SpriteType defaultType = SpriteType.TEXT;
        static public UpdateFrequency defaultUpdate = UpdateFrequency.None;
        static public Color defaultColor = Color.HotPink;

        public static UpdateFrequency UpdateConverter(UpdateType source)
        {
            var updateFrequency = UpdateFrequency.None; //0000
            if ((source & UpdateType.Update1) != 0) updateFrequency |= UpdateFrequency.Update1; //0001
            if ((source & UpdateType.Update10) != 0) updateFrequency |= UpdateFrequency.Update10; //0010
            if ((source & UpdateType.Update100) != 0) updateFrequency |= UpdateFrequency.Update100;//0100
            return updateFrequency;
        }

        public static void UpdateBarGraph(ref SpriteData data, double pctData) // will not work with square. L
        {
            if (InfoUtility.justStarted)
            {
                bool horizontal = data.SpriteSizeX > data.SpriteSizeY;
                InfoUtility.GraphStorage.Add(data.UniqueID, new MyTuple<bool, float>(horizontal, horizontal ? data.SpriteSizeX : data.SpriteSizeY));
            }

            var graph = InfoUtility.GraphStorage[data.UniqueID];
            if (graph.Item1) data.SpriteSizeX = Convert.ToSingle(pctData) * graph.Item2;
            else data.SpriteSizeY = Convert.ToSingle(pctData) * graph.Item2;
        }

        public static string EncodeSprites(ref LinkedDisplay display)
        // the idea: have this make the requisite SpriteData constructors here bc im too lazy
        // the constructor in question:
        //  public SpriteData(SpriteType type, string name, string data, float sizeX, float sizeY, TextAlignment alignment,
        //  float posX, float posY, float ros, Color color, string fontid = "White", UpdateFrequency updateType = UpdateFrequency.None,
        //  string command = "", bool builder = false, string prepend = "") (jesus christ)
        {
            var encodedOutput = "";
            var comma = ", ";
            foreach (var surface in display.DisplayOutputs)
            {
                encodedOutput += $"// screen {surface.Key.Name} background color {surface.Key.BackgroundColor}\n";
                foreach (var sprite in surface.Value.Values)
                {
                    encodedOutput += $"new SpriteData({sprite.spriteType}, {sprite.Name}, {sprite.Data}, {(sprite.spriteType != SpriteType.TEXT ? sprite.SpriteSizeX + comma + sprite.SpriteSizeY + comma : "")}" +
                        $"TextAlignment.{sprite.SpriteAlignment.ToString().ToUpper()}, {sprite.SpritePosX}, {sprite.SpritePosY}, " +
                        $"{sprite.SpriteRorS}, new Color({sprite.SpriteColor.R}, {sprite.SpriteColor.G}, {sprite.SpriteColor.B}, {sprite.SpriteColor.A}){(sprite.spriteType != SpriteType.TEXT ? "" : comma + sprite.FontID)}" +
                        $"{(sprite.CommandFrequency != UpdateFrequency.None ? comma + sprite.CommandFrequency.ToString() + comma + sprite.CommandString + comma + sprite.UseStringBuilder + (sprite.UseStringBuilder ? comma + sprite.BuilderPrepend + comma + sprite.BuilderAppend : "") : "")});\n";
                }
            }
            return encodedOutput;
        }

    }

    public class DisplayIniKeys //avoid allocating new memory for every display (i hope). just seems less retarded.
    {
        public string
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
            UpdateKey,
            BuilderKey,
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
            BuilderKey = "K_BUILD";
            PrependKey = "K_PREP";
            AppendKey = "K_APP";
        }


    }
}