using Sandbox.ModAPI.Ingame;
using System;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRage.Library.Utils;
using VRageMath;

namespace IngameScript
{
    public class SpriteData
    {
        #region fields
        const string l = "list";
        bool isList, noFormat;
        public SpriteType Type;
        public TextAlignment Alignment;
        public Action<SpriteData> Command = null;
        public Priority Priority;
        public long uID = -1; // this field is only set if sprite is using a command.
        public float
            sX,
            sY,
            X,
            Y,
            RorS;
        public string
            Name,
            Data,
            DataPrev,
            Format,
            FontID = "White",
            commandID,
            Prepend,
            Append;
        public Color Color;

        public SpriteData[] Children;

        public bool Builder { get; private set; }

        public MySprite sprCached;

        #endregion

        public SpriteData()
        {
        }

        public SpriteData(Color color, string name = "", string data = "", float posX = 0, float posY = 0, float ros = float.MinValue,  float szX = 0, float szY = 0, string font = "White", Priority p = Priority.None, SpriteType type = SpriteType.TEXT, TextAlignment align = TextAlignment.CENTER, string command = "", string prepend = "", string append = "", string format = "")
        {
            uID = -1;
            Color = color;
            Type = type;
            Name = name;    
            Data = data;
            if (Type != Lib.dType)
            {
                sX = szX;
                sY = szY;
            }
            Alignment = align;
            X = posX;
            Y = posY;
            if (RorS == float.MinValue)
                RorS = Type == Lib.dType ? 1 : 0; 
            else RorS = ros;
            Priority = p; //NONE = 0,  high (every 10) = 1, low = 2
            commandID = command;
            if (Type == Lib.dType)
            {
                FontID = font;
                Format = format;
                Prepend = prepend;
                Append = append;
                SetFlags();
            } 
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

        private static void ApplyBuilder(SpriteData d)
        {
            if (d.isList/* || d.Data == "••"*/) return;
            StringBuilder builder = new StringBuilder(d.Data);
            builder.Insert(0, d.Prepend);
            builder.Append(d.Append);
            d.Data = builder.ToString();
        }

        public bool Update()
        {
            Command.Invoke(this);
            if (Builder) ApplyBuilder(this);
            var b = Data != DataPrev && commandID != "";
            DataPrev = Data;
            return b;
        }

        public void SetFlags()
        {
            Builder = Prepend != "" || Append != "";
            isList = commandID.Contains(l);
            noFormat = Format == "";
            if (!Builder) return;
            if (Prepend != null && Prepend.Contains("\n")) 
                Prepend.Trim();
            if (Append != null && Append.Contains("\n")) 
                Append.Trim();
        }

        public static MySprite CreateSprite(SpriteData d, bool start = false)
        {
            if (d.uID == -1 && !start)
                return d.sprCached;
            else
            {
                var sprite = d.Type == Lib.dType ? new MySprite(
                d.Type,
                d.Data,
                new Vector2(d.X, d.Y),
                null,
                d.Color,
                d.FontID,
                d.Alignment,
                d.RorS
                )
                : new MySprite(
                d.Type,
                d.Data,
                new Vector2(d.X, d.Y),
                new Vector2(d.sX, d.sY),
                d.Color,
                null,
                d.Alignment,
                MathHelper.ToRadians(d.RorS)
            );
    
                return sprite;
            }
        }

    }
}