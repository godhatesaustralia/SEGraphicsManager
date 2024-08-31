using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
using VRage.Game.GUI.TextPanel;

namespace IngameScript
{
    public class IniWrap : IDisposable
    {
        static List<MyIni> IniParsers = new List<MyIni>();
        static int IniCount = 0;
        public static int total = 0;
        public static int Count => IniParsers.Count;
        MyIni _ini;
        static MyIniParseResult result;

        public IniWrap()
        {
            ++IniCount;
            ++total;
            if (IniParsers.Count < IniCount)
                IniParsers.Add(new MyIni());
            _ini = IniParsers[IniCount - 1];

            _ini.Clear();
        }

        public bool CustomData(IMyTerminalBlock b, out MyIniParseResult r)
        {
            var output = _ini.TryParse(b.CustomData, out result);
            r = result;
            return output;
        }

        public bool CustomData(IMyTerminalBlock b)
        {
            var output = _ini.TryParse(b.CustomData, out result);
            return output;
        }

        public bool HasSection(string s) => _ini.ContainsSection(s);

        public bool HasKey(string s, string k) => _ini.ContainsKey(s, k);

        public float Float(string s, string k, float def = 1) => _ini.Get(s, k).ToSingle(def);

        public int Int(string s, string k, int def = 0) => _ini.Get(s, k).ToInt32(def);

        public bool Bool(string s, string k, bool def = false) => _ini.Get(s, k).ToBoolean(def);

        public string String(string s, string k, string def = "") => _ini.Get(s, k).ToString(def);

        public SpriteType Type(string s, string k)
        {
            var t = _ini.Get(s, k);
            string c = "";
            if (t.ToByte(3) != 3)
                return (SpriteType)t.ToByte(2);
            else
                c = t.ToString(c).ToLower();
            switch (c) // *sigh*
            {
                case "shape":
                case "sprite":
                    return SpriteType.TEXTURE;
                case "clip":
                    return SpriteType.CLIP_RECT;
                case "text":
                default:
                    return SpriteType.TEXT;
            }

        }
        public TextAlignment Alignment(string s, string k)
        {
            var a = _ini.Get(s, k);
            string c = "";
            if (a.ToByte(3) != 3)
                return (TextAlignment)a.ToByte(2);
            else
                c = a.ToString(c).ToLower();
            switch (c) // oh what the hell
            {
                case "l":
                case "left":
                    return TextAlignment.LEFT;
                case "r":
                case "right":
                    return TextAlignment.RIGHT;
                case "c":
                case "center":
                default:
                    return TextAlignment.CENTER;
            }
        }

        public bool TryReadVector2(string s, string k, out float x, out float y, string n = "")
        {
            x = y = 0;
            string ln = _ini.Get(s, k).ToString();
            if (ln == "")
                return false;
            var v = ln.Split(',');
            //return false;
            try
            {
                x = float.Parse(v[0].Trim('('));
                y = float.Parse(v[1].Trim(')'));
            }
            catch (Exception)
            {
                throw new Exception($"\nError reading {k} floats for {s} in {n}: \n{v[0]} and {v[1]}");
            }
            return true;
        }

        public Color Color(string s, string k, string def = "")
        {
            byte r, g, b, a;
            def = _ini.Get(s, k).ToString(def).ToLower();
            if (def.Length != 6 && def.Length != 8)
                return VRageMath.Color.HotPink; //safety
            r = Hex(def, 0, 2);
            g = Hex(def, 2, 2);
            b = Hex(def, 4, 2);
            a = def.Length == 8 ? Hex(def, 6, 2) : byte.MaxValue;
            return new Color(r, g, b, a);
        }

        // some bullshit
        public static Color Color(string c)
        {
            byte r, g, b, a;
            r = Hex(c, 0, 2);
            g = Hex(c, 2, 2);
            b = Hex(c, 4, 2);
            a = c.Length == 8 ? Hex(c, 6, 2) : byte.MaxValue;
            return new Color(r, g, b, a);
        }
        static byte Hex(string input, int start, int length) => Convert.ToByte(input.Substring(start, length), 16);

        public override string ToString() => _ini.ToString();

        public void Dispose()
        {
            _ini.Clear();
            IniCount--;
        }
    }
}
