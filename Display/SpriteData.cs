using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Library.Utils;
using VRageMath;

namespace IngameScript
{
    public class SpriteData
    {
        #region fields
        const string l = "list";
        static int idBase = 3;
        bool isList, noFormat, hide;
        public MySprite Sprite;
        public Action<SpriteData> Command = null;
        public double Unit = 1;
        public Priority Priority;
        public int uID = -1; // this field is only set if sprite is using a cmd.
        public string Name;
        public string
            LastData,
            Format,
            Prepend,
            Append;
        public Color Color;
        public string Data
        {
            get { return Sprite.Data; }
            set
            {
                if (value == LastData) return;
                LastData = Data;
                Sprite.Data = value;
            }
        }

        public SpriteData[] Children;

        public bool Builder { get; private set; }

        #endregion

        public SpriteData()
        {
        }
        public SpriteData(Color c, string n, string d, float X, float Y, float scale = 1, string font = "White", Priority p = Priority.None, TextAlignment align = TextAlignment.CENTER, string prep = "", string app = "", string fmat = "", string cmd = "")
        {
            Name = n;
            Sprite = new MySprite(Lib.TXT, d, new Vector2(X, Y), null, c, font, align, scale);
            if ((p | Priority.None) != 0)
            {
                ++idBase;
                uID = idBase;
            }
            Priority = p;
            Format = fmat;
            Prepend = prep;
            Append = app;
            LastData = d;
            SetFlags(cmd);
        }
        public SpriteData(Color c, string n = "", string d = "", float X = 0, float Y = 0, float rotation = 0, float szX = 0, float szY = 0, Priority p = Priority.None, TextAlignment align = TextAlignment.CENTER, string cmd = "", string format = "", bool clip = false)
        {
            rotation = MathHelper.ToRadians(rotation);
            Vector2
                pos = new Vector2(X, Y),
                sz = new Vector2(szX, szY);
            if (!clip)
                Sprite = new MySprite(SpriteType.TEXTURE, d, pos, sz, c, null, align, rotation);
            else
                Sprite = new MySprite(SpriteType.CLIP_RECT, d, pos, sz, c, null, align, rotation);
            Name = n;
            Priority = p; //NONE = 0,  high (every 10) = 1, low = 2
            if ((p | Priority.None) != 0)
            {
                ++idBase;
                uID = idBase;
            }
            Format = format;
            SetFlags(cmd);
        }
        public void SetData(double value, string def = "")
        {
            if (isList || noFormat)
                Data = value.ToString(def);
            else Data = value.ToString(Format);
        }
        // variant for tags (string t)
        public void SetData(double value, string t, string def)
        {
            if (isList || noFormat)
                Data = t + value.ToString(def);

            else Data = t + value.ToString(Format);
        }

        public void SetData(string line, bool list = false)
        {
            if (list)
                Sprite.Data += "\n" + line;
            else Data = line;
        }

        static void ApplyBuilder(SpriteData d)
        {
            if (d.isList/* || d.Data == "••"*/) return;
            StringBuilder builder = new StringBuilder(d.Data);
            builder.Insert(0, d.Prepend);
            builder.Append(d.Append);
            d.Data = builder.ToString();
        }

        // note (5/23/24): THE ORDER IS VERY VERY IMPORTANT
        public bool CheckUpdate()
        {
            Command.Invoke(this);
            var b = Data != LastData;
            if (b)
                LastData = Data;
            if (Builder) ApplyBuilder(this);          
            return b;
        }

        public void SetFlags(string c)
        {
            Builder = Prepend != "" || Append != "";
            isList = c.Contains(l);
            noFormat = Format == "";
            if (!Builder) return;
            if (Prepend != null && Prepend.Contains("\n"))
                Prepend.Trim();
            if (Append != null && Append.Contains("\n"))
                Append.Trim();
        }

    }
}