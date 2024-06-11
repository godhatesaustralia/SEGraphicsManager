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
        bool isList, noFormat;
        public MySprite Sprite;
        public Action<SpriteData> Command = null;
        public Priority Priority;
        public int uID = -1; // this field is only set if sprite is using a cmd.
        public string Name;
        public string
            LastData,
            Format,
            Prepend,
            Append;

        public virtual string Data
        {
            get { return Sprite.Data; }
            set { Sprite.Data = value; }
        }

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

        public virtual void SetData(double value, string def = "")
        {
            if (isList || noFormat)
                Data = value.ToString(def);
            else Data = value.ToString(Format);
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
        public virtual bool CheckUpdate()
        {
            Command.Invoke(this);
            if (Builder) ApplyBuilder(this);
            var b = Data != LastData;
            LastData = Data;
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

    // this is genuinely the worst object i have ever made
    public class SpriteConditional : SpriteData
    {
        double _data, _lastData;
        const char k = '$';
        Func<bool>[] _conditions;
        MySprite _default;
        public override string Data
        {
            get { return Data; }
            set { }
        }
        public SpriteConditional(SpriteData d)
        {
            uID = d.uID;
            Name = d.Name;
            Sprite = d.Sprite;
            Command = d.Command;
            Priority = d.Priority;
        }
        public void CreateMappings(string s)
        {
            string[] 
                ln,
                l = s.Contains("\n") ? s.Split('\n') : new string[] { s };
            //  ^ split string if applicable

            _conditions = new Func<bool>[l.Length];
            for(int i = 0; i < l.Length; i++)
            {
                // formatting: [0 - data point]$[1 - comparator]$[3 - color/hide]
                // e.g. 1.234$<=$64FA64 sets color to 64FA64 if data <= 1.234
                
                ln = l[i].Split(k);
                if (ln.Length != 3) continue;

                var d = double.Parse(ln[0]);
                var b = Condition(ln[1], d);
                if (ln[2] != "hide")
                {
                    var c = iniWrap.Color(ln[2]); // parse color
                    _conditions[i] = () =>
                    {
                        var r = b.Invoke();
                        if (r) Sprite.Color = c;
                        return r;
                    };
                }
                else
                {
                    _default = Sprite;
                    _conditions[i] = () =>
                    {
                        var r = b.Invoke();
                        if (r)
                        {
                            Sprite = new MySprite();
                            _lastData = _data;
                        }
                        else if (Sprite.Data == null)
                            Sprite = _default;
                        return r;
                    };
                }
            }
        }

        Func<bool> Condition(string c, double d)
        {
            switch (c)
            {
                case "=":
                case "==":
                default:
                    return () => _data == d;
                case "<":
                    return () => _data < d;
                case ">":
                    return () => _data > d;
                case "<=":
                case "=<":
                    return () => _data <= d;     
                case ">=":
                case "=>":
                    return () => _data >= d;
                case "!=":
                    return () => _data != d;
            }
        }

        public override void SetData(double value, string def = "") => _data = value;

        public override bool CheckUpdate()
        {
            Command.Invoke(this);
            var b = _data != _lastData;
            if (b)
            {
                for (int i = 0; i < _conditions.Length; i++)
                    if (_conditions[i].Invoke())
                        break;
            }
            return b;
        }
    }
}