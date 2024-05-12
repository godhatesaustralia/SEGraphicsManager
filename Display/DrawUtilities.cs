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
    static public class Lib
    {
        static public Dictionary<long, MyTuple<bool, float>> GraphStorage = new Dictionary<long, MyTuple<bool, float>>();
        static public int bsodsTotal = 0;
        static public SpriteType TXT = SpriteType.TEXT;
        static public UpdateFrequency NONE = UpdateFrequency.None;
        static public Color PINK = Color.HotPink;
        static public string bsod = "A problem has been detected and Windows has been shut down to prevent damage \r\nto your computer. \r\nUNMOUNTABLE_BOOT_VOLUME \r\nIf this is the first time you've seen this error screen, \r\nrestart your computer. If this screen appears again, follow \r\nthese steps: \r\nCheck to be sure you have adequate disk space. If a driver is \r\nidentified in the Stop message, disable the driver or check \r\nwith the manufacturer for driver updates. Try changing video \r\nadapters. \r\nCheck with your hardware vendor for any BIOS updates. Disable \r\nBIOS memory options such as caching or shadowing. \r\nIf you need to use Safe Mode to remove or disable components, restart \r\nyour computer, press F8 to select Advanced Startup Options, and then \r\nselect Safe Mode. \r\n \r\nTechnical Information: \r\n*** STOP: 0x000000ED(0x80F128D0, 0xC000009C, 0x00000000, 0x00000000) \r\n \r\n";

        static public int Next(ref int p, int max)
        {
            if (p < max)
                p++;
            if (p == max)
                p = 0;
            return p;
        }
        static public void CreateBarGraph(ref SpriteData d)
        {
            if (GraphStorage.ContainsKey(d.uID))
                return;
            bool horizontal = d.sX > d.sY || d.sX == d.sY;
            GraphStorage.Add(d.uID, new MyTuple<bool, float>(horizontal, horizontal ? d.sX : d.sY));
        }
        static public void UpdateBarGraph(ref SpriteData data, double pctData)
        {
            var graph = GraphStorage[data.uID];
            if (graph.Item1) data.sX = Convert.ToSingle(pctData) * graph.Item2;
            else data.sY = Convert.ToSingle(pctData) * graph.Item2;
        }

        //static public string EncodeSprites(ref Display display)
        // the idea: have this make the requisite SpriteData constructors here bc im too lazy
        // the constructor in question:
        //   public SpriteData(Color color, string name = "", string value = "", float posX = 0, float posY = 0, float ros = float.MinValue,
        //   float szX = 0, float szY = 0, string font = "White", BlockRefresh p = BlockRefresh.None, SpriteType type = SpriteType.TEXT,
        //   TextAlignment align = TextAlignment.CENTER, string command = "", string prepend = "", string append = "")
        //{
        //    var eOut = "";
        //    foreach (var surface in display.Outputs)
        //    {
        //        eOut += $"//\nscreen {surface.Key.Name} background color {surface.Key.BackgroundColor}\n";
        //        foreach (var s in surface.Value.Values)
        //        {
        //            eOut += $"new SpriteData(new Color({s.Color.R}, {s.Color.G}, {s.Color.B}, {s.Color.A}), {s.Name}, {s.Data}, ";
        //            eOut += $"{s.X}, {s.Y}, {s.RorS}, {s.sX}, {s.sY}, {s.FontID}, {s.BlockRefresh}, {s.Type}, {s.Alignment}, {s.Command}, {s.Prepend}, {s.Append});\n";
        //        }
        //    }
        //    return eOut;
        //}

    }

    public class DebugAPI
    {
        public readonly bool ModDetected;

        /// <summary>
        /// Changing this will affect OnTop draw for all future draws that don't have it specified.
        /// </summary>
        public bool DefaultOnTop;

        /// <summary>
        /// Recommended to be used at start of Main(), unless you wish to draw things persistently and remove them manually.
        /// <para>Removes everything except AdjustNumber and chat messages.</para>
        /// </summary>
        public void RemoveDraw() => _removeDraw?.Invoke(_pb);
        Action<IMyProgrammableBlock> _removeDraw;

        /// <summary>
        /// Removes everything that was added by this API (except chat messages), including DeclareAdjustNumber()!
        /// <para>For calling in Main() you should use <see cref="RemoveDraw"/> instead.</para>
        /// </summary>
        public void RemoveAll() => _removeAll?.Invoke(_pb);
        Action<IMyProgrammableBlock> _removeAll;

        /// <summary>
        /// You can store the integer returned by other methods then remove it with this when you wish.
        /// <para>Or you can not use this at all and call <see cref="RemoveDraw"/> on every Main() so that your drawn things live a single PB run.</para>
        /// </summary>
        public void Remove(int id) => _remove?.Invoke(_pb, id);
        Action<IMyProgrammableBlock, int> _remove;

        public int DrawPoint(Vector3D origin, Color color, float radius = 0.2f, float seconds = DefaultSeconds, bool? onTop = null) => _point?.Invoke(_pb, origin, color, radius, seconds, onTop ?? DefaultOnTop) ?? -1;
        Func<IMyProgrammableBlock, Vector3D, Color, float, float, bool, int> _point;

        public int DrawLine(Vector3D start, Vector3D end, Color color, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _line?.Invoke(_pb, start, end, color, thickness, seconds, onTop ?? DefaultOnTop) ?? -1;
        Func<IMyProgrammableBlock, Vector3D, Vector3D, Color, float, float, bool, int> _line;

        public int DrawAABB(BoundingBoxD bb, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _aabb?.Invoke(_pb, bb, color, (int)style, thickness, seconds, onTop ?? DefaultOnTop) ?? -1;
        Func<IMyProgrammableBlock, BoundingBoxD, Color, int, float, float, bool, int> _aabb;

        public int DrawOBB(MyOrientedBoundingBoxD obb, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _obb?.Invoke(_pb, obb, color, (int)style, thickness, seconds, onTop ?? DefaultOnTop) ?? -1;
        Func<IMyProgrammableBlock, MyOrientedBoundingBoxD, Color, int, float, float, bool, int> _obb;

        public int DrawSphere(BoundingSphereD sphere, Color color, Style style = Style.Wireframe, float thickness = DefaultThickness, int lineEveryDegrees = 15, float seconds = DefaultSeconds, bool? onTop = null) => _sphere?.Invoke(_pb, sphere, color, (int)style, thickness, lineEveryDegrees, seconds, onTop ?? DefaultOnTop) ?? -1;
        Func<IMyProgrammableBlock, BoundingSphereD, Color, int, float, int, float, bool, int> _sphere;

        public int DrawMatrix(MatrixD matrix, float length = 1f, float thickness = DefaultThickness, float seconds = DefaultSeconds, bool? onTop = null) => _matrix?.Invoke(_pb, matrix, length, thickness, seconds, onTop ?? DefaultOnTop) ?? -1;
        Func<IMyProgrammableBlock, MatrixD, float, float, float, bool, int> _matrix;

        /// <summary>
        /// Adds a HUD marker for a world position.
        /// <para>White is used if <paramref name="color"/> is null.</para>
        /// </summary>
        public int DrawGPS(string name, Vector3D origin, Color? color = null, float seconds = DefaultSeconds) => _gps?.Invoke(_pb, name, origin, color, seconds) ?? -1;
        Func<IMyProgrammableBlock, string, Vector3D, Color?, float, int> _gps;

        /// <summary>
        /// Adds a notification center on screen. Do not give 0 or lower <paramref name="seconds"/>.
        /// </summary>
        public int PrintHUD(string message, Font font = Font.Debug, float seconds = 2) => _printHUD?.Invoke(_pb, message, font.ToString(), seconds) ?? -1;
        Func<IMyProgrammableBlock, string, string, float, int> _printHUD;

        /// <summary>
        /// Shows a message in chat as if sent by the PB (or whoever you want the sender to be)
        /// <para>If <paramref name="sender"/> is null, the PB's CustomName is used.</para>
        /// <para>The <paramref name="font"/> affects the fontface and color of the entire message, while <paramref name="senderColor"/> only affects the sender name's color.</para>
        /// </summary>
        public void PrintChat(string message, string sender = null, Color? senderColor = null, Font font = Font.Debug) => _chat?.Invoke(_pb, message, sender, senderColor, font.ToString());
        Action<IMyProgrammableBlock, string, string, Color?, string> _chat;

        /// <summary>
        /// Used for realtime adjustments, allows you to hold the specified key/button with mouse scroll in order to adjust the <paramref name="initial"/> number by <paramref name="step"/> amount.
        /// <para>Add this once at start then store the returned id, then use that id with <see cref="GetAdjustNumber(int)"/>.</para>
        /// </summary>
        public void DeclareAdjustNumber(out int id, double initial, double step = 0.05, Input modifier = Input.Control, string label = null) => id = _adjustNumber?.Invoke(_pb, initial, step, modifier.ToString(), label) ?? -1;
        Func<IMyProgrammableBlock, double, double, string, string, int> _adjustNumber;

        /// <summary>
        /// See description for: <see cref="DeclareAdjustNumber(double, double, Input, string)"/>.
        /// <para>The <paramref name="noModDefault"/> is returned when the mod is not present.</para>
        /// </summary>
        public double GetAdjustNumber(int id, double noModDefault = 1) => _getAdjustNumber?.Invoke(_pb, id) ?? noModDefault;
        Func<IMyProgrammableBlock, int, double> _getAdjustNumber;

        /// <summary>
        /// Gets simulation tick since this session started. Returns -1 if mod is not present.
        /// </summary>
        public int GetTick() => _tick?.Invoke() ?? -1;
        Func<int> _tick;

        /// <summary>
        /// Gets time from Stopwatch which is accurate to nanoseconds, can be used to measure code execution time.
        /// Returns TimeSpan.Zero if mod is not present.
        /// </summary>
        public TimeSpan GetTimestamp() => _timestamp?.Invoke() ?? TimeSpan.Zero;
        Func<TimeSpan> _timestamp;

        /// <summary>
        /// Use with a using() statement to measure a chunk of code and get the time difference in a callback.
        /// <code>
        /// using(Debug.Measure((t) => Echo($"diff={t}")))
        /// {
        ///    // code to measure
        /// }
        /// </code>
        /// This simply calls <see cref="GetTimestamp"/> before and after the inside code.
        /// </summary>
        public MeasureToken Measure(Action<TimeSpan> call) => new MeasureToken(this, call);

        /// <summary>
        /// <see cref="Measure(Action{TimeSpan})"/>
        /// </summary>
        public MeasureToken Measure(string prefix) => new MeasureToken(this, (t) => PrintHUD($"{prefix} {t.TotalMilliseconds} ms"));

        public struct MeasureToken : IDisposable
        {
            DebugAPI API;
            TimeSpan Start;
            Action<TimeSpan> Callback;

            public MeasureToken(DebugAPI api, Action<TimeSpan> call)
            {
                API = api;
                Callback = call;
                Start = API.GetTimestamp();
            }

            public void Dispose()
            {
                Callback?.Invoke(API.GetTimestamp() - Start);
            }
        }

        public enum Style { Solid, Wireframe, SolidAndWireframe }
        public enum Input { MouseLeftButton, MouseRightButton, MouseMiddleButton, MouseExtraButton1, MouseExtraButton2, LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt, Tab, Shift, Control, Alt, Space, PageUp, PageDown, End, Home, Insert, Delete, Left, Up, Right, Down, D0, D1, D2, D3, D4, D5, D6, D7, D8, D9, A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z, NumPad0, NumPad1, NumPad2, NumPad3, NumPad4, NumPad5, NumPad6, NumPad7, NumPad8, NumPad9, Multiply, Add, Separator, Subtract, Decimal, Divide, F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12 }
        public enum Font { Debug, White, Red, Green, Blue, DarkBlue }

        const float DefaultThickness = 0.02f;
        const float DefaultSeconds = -1;

        IMyProgrammableBlock _pb;

        /// <summary>
        /// NOTE: if mod is not present then methods will simply not do anything, therefore you can leave the methods in your released code.
        /// </summary>
        /// <param name="program">pass `this`.</param>
        /// <param name="drawOnTopDefault">set the default for onTop on all objects that have such an option.</param>
        public DebugAPI(MyGridProgram program, bool drawOnTopDefault = false)
        {
            if (program == null) throw new Exception("Pass `this` into the API, not null.");

            DefaultOnTop = drawOnTopDefault;
            _pb = program.Me;

            var methods = _pb.GetProperty("DebugAPI")?.As<IReadOnlyDictionary<string, Delegate>>()?.GetValue(_pb);
            if (methods != null)
            {
                Assign(out _removeAll, methods["RemoveAll"]);
                Assign(out _removeDraw, methods["RemoveDraw"]);
                Assign(out _remove, methods["Remove"]);
                Assign(out _point, methods["Point"]);
                Assign(out _line, methods["Line"]);
                Assign(out _aabb, methods["AABB"]);
                Assign(out _obb, methods["OBB"]);
                Assign(out _sphere, methods["Sphere"]);
                Assign(out _matrix, methods["Matrix"]);
                Assign(out _gps, methods["GPS"]);
                Assign(out _printHUD, methods["HUDNotification"]);
                Assign(out _chat, methods["Chat"]);
                Assign(out _adjustNumber, methods["DeclareAdjustNumber"]);
                Assign(out _getAdjustNumber, methods["GetAdjustNumber"]);
                Assign(out _tick, methods["Tick"]);
                Assign(out _timestamp, methods["Timestamp"]);

                RemoveAll(); // cleanup from past compilations on this same PB

                ModDetected = true;
            }
        }

        void Assign<T>(out T field, object method) => field = (T)method;
    }
}

