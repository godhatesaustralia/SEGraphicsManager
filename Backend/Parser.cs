using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;
using VRage.Input;

namespace IngameScript
{
    public class Parser : IDisposable
    {
        static List<MyIni> IniParsers = new List<MyIni>();
        static int IniCount = 0;
        MyIni myIni;
        public MyIniParseResult result;

        public Parser()
        {
            ++IniCount;
            if (IniParsers.Count < IniCount)
                IniParsers.Add(new MyIni());

            myIni = IniParsers[IniParsers.Count - 1];

            myIni.Clear();
        }
        public bool TryParseCustomData(IMyTerminalBlock block, out MyIniParseResult Result)
        {
            var output = myIni.TryParse(block.CustomData, out result);
            Result = result; 
            return output;
        }
        public bool ContainsSection(string aSection)
        {
            return myIni.ContainsSection(aSection);
        }

        public bool ContainsKey(string aSection, string aKey)
        {
            return myIni.ContainsKey(aSection, aKey);
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
        public Color ParseColor(string aSection, string aKey, string aDefault = "")
        {
            byte r, g, b, a;
            aDefault = myIni.Get(aSection, aKey).ToString(aDefault);
            if (aDefault.Length != 8) 
                return SharedUtilities.defaultColor; //safety
            r = HexParse(aDefault, 0, 2);
            g = HexParse(aDefault, 2, 2);
            b = HexParse(aDefault, 4, 2);
            a = HexParse(aDefault, 6, 2);
            return new Color(r, g, b, a);
        }
        public byte HexParse(string input, int start, int length)
        {
            return Convert.ToByte(input.Substring(start, length), 16);

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
