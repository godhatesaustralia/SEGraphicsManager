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
    public static class Lib
    {
        static public Dictionary<long, MyTuple<bool, float>> GraphStorage = new Dictionary<long, MyTuple<bool, float>>();
        static public List<Color> lights = new List<Color>();
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
            if (GraphStorage.ContainsKey(d.uID)) 
                return;
            bool horizontal = d.sX > d.sY || d.sX == d.sY;
            GraphStorage.Add(d.uID, new MyTuple<bool, float>(horizontal, horizontal ? d.sX : d.sY));
        }
        public static void UpdateBarGraph(ref SpriteData data, double pctData)
        { 
            var graph = GraphStorage[data.uID];
            if (graph.Item1) data.sX = Convert.ToSingle(pctData) * graph.Item2;
            else data.sY = Convert.ToSingle(pctData) * graph.Item2;
        }

        public static void ApplyBuilder(SpriteData d)
        {
            StringBuilder builder = new StringBuilder(d.Data);
            builder.Insert(0, d.Prepend);
            builder.Append(d.Append);
            d.Data = builder.ToString();
        }

        //internal static void lockdown(GraphicsManager g)
        //{
        //    var b = new List<IMyTerminalBlock>();
        //    g.Terminal.GetBlockGroupWithName("XCT CIC Lights").GetBlocks(b);
        //    for (int i = 0; i < b.Count; i++)
        //    {
        //        if (g.justStarted)
        //        {
        //            lights.Add(new Color(90, 150, 90));
        //            continue;
        //        }
        //        var c = b[i] as IMyLightingBlock;
        //        var cl = c.Color;
        //        c.Color = lights[i];
        //        lights[i] = cl;
        //    }

        //}

        public static string EncodeSprites(ref LinkedDisplay display)
        // the idea: have this make the requisite SpriteData constructors here bc im too lazy
        // the constructor in question:
        //   public SpriteData(Color color, string name = "", string data = "", float posX = 0, float posY = 0, float ros = float.MinValue,
        //   float szX = 0, float szY = 0, string font = "White", Priority p = Priority.None, SpriteType type = SpriteType.TEXT,
        //   TextAlignment align = TextAlignment.CENTER, string command = "", string prepend = "", string append = "")
        {
            var eOut = "";
            foreach (var surface in display.Outputs)
            {
                eOut += $"//\nscreen {surface.Key.Name} background color {surface.Key.BackgroundColor}\n";
                foreach (var s in surface.Value.Values)
                {
                    eOut += $"new SpriteData(new Color({s.Color.R}, {s.Color.G}, {s.Color.B}, {s.Color.A}), {s.Name}, {s.Data}, ";
                    eOut += $"{s.X}, {s.Y}, {s.RorS}, {s.sX}, {s.sY}, {s.FontID}, {s.Priority}, {s.Type}, {s.Alignment}, {s.Command}, {s.Prepend}, {s.Append});\n";
                }
            }
            return eOut;
        }

    }

    public class IniKeys //avoid allocating new memory for every display (i hope). just seems less dumb.
    {
        public string
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
        public readonly char
            vectorL = '(',
            vectorR = ')',
            entry = '>';
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
            Prepend = "PREP";
            Append = "APP";
        }


    }
}