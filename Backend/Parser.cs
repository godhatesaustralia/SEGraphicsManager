using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace IngameScript
{
    public class Parser : IDisposable
    {
        static List<MyIni> IniParsers = new List<MyIni>();
        static int IniCount = 0;
        MyIni myIni;

        public Parser()
        {
            ++IniCount;
            if (IniParsers.Count < IniCount)
                IniParsers.Add(new MyIni());

            myIni = IniParsers[IniParsers.Count - 1];

            myIni.Clear();
        }
        public bool TryParseCustomData(IMyTerminalBlock block)
        {
            return myIni.TryParse(block.CustomData);
        }
        public bool ContainsSection(string aSection)
        {
            return myIni.ContainsSection(aSection);
        }
        public float ParseFloat(string aSection, string aKey, float aDefault = 1)
        {
            return myIni.Get(aSection, aKey).ToSingle(aDefault);
        }
        public double ParseDouble(string aSection, string aKey, double aDefault = 0)
        {
            return myIni.Get(aSection, aKey).ToDouble(aDefault);
        }

        public byte ParseByte(string aSection, string aKey, byte aDefault = 2)
        {
            return myIni.Get(aSection, aKey).ToByte(aDefault);
        }

        public int ParseInt(string aSection, string aKey, int aDefault = 0)
        {
            return myIni.Get(aSection, aKey).ToInt32(aDefault);
        }
        public bool ParseBool(string aSection, string aKey, bool aDefault = false)
        {
            return myIni.Get(aSection, aKey).ToBoolean(aDefault);
        }
        public string ParseString(string aSection, string aKey, string aDefault = "")
        {
            return myIni.Get(aSection, aKey).ToString(aDefault);
        }
        public Color ParseColor(string aSection, string aKey, string aDefault = "FA3232FF")
        {
            byte r, g, b, a;
            myIni.Get(aSection, aKey).ToString(aDefault);
            r = HexParse(aDefault, 0, 2);
            g = HexParse(aDefault, 2, 2);
            b = HexParse(aDefault, 4, 2);
            a = HexParse(aDefault, 6, 2);
            return new Color(r, g, b, a);
        }
        public byte HexParse(string input, int start, int length)
        {
            return byte.Parse(input.Substring(start, length), NumberStyles.AllowHexSpecifier);
        }
        public bool StringContains(string aSection, string tag)
        {
            return aSection.Contains(tag);
        }
        public void Dispose()
        {
            IniCount--;
        }
    }
}
