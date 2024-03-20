using Sandbox.ModAPI.Ingame;
using System;
using System.Runtime.Remoting.Messaging;
using System.Security.Policy;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public class SpriteData
    {
        #region fields

        public SpriteType Type;
        public TextAlignment Alignment;
        public Action<SpriteData> Command;
        public Priority Priority;
        public long uID = -1; // this field is only set if sprite is using a command.
        public float
            SizeX,
            SizeY,
            PosX,
            PosY,
            RorS,
            Length;
        public string
            Name,
            Data,
            FontID = "DEBUG",
            CommandString,
            Prepend,
            Append;
        public Color Color;

        public SpriteData[] Children;

        public bool
            Builder; //for commands, whether to apply stringbuilder (and to attempt parse of stringbuilder param)
        public MySprite sprCached;

        #endregion

        public SpriteData()
        {
            Type = Util.dType;
            Priority = Priority.None;
            Command = null;
            uID = -1;
            Builder = false;
            Prepend = Append = "";
        }

        public SpriteData(SpriteType type, string name, string data, float sizeX, float sizeY, TextAlignment align, float posX, float posY, float ros, Color color, string font = "White", Priority p = Priority.None, string command = "", bool builder = false, string prepend = "", string append = "")
        {
            Type = type;
            Name = name;    
            Data = data;
            SizeX = sizeX;
            SizeY = sizeY;
            Alignment = align;
            PosX = posX;
            PosY = posY;
            RorS = ros;
            Color = color;
            FontID = font;
            Priority = p; //NONE = 0,  high (every 10) = 1, low = 2
            CommandString = command;
            Builder = builder;
            Prepend = prepend;
            Append = append;
        }

        public static MySprite createSprite(SpriteData d, bool start = false)
        {
            if (d.uID == -1 && !start)
                return d.sprCached;
            else
            {
                var sprite = d.Type == Util.dType ? new MySprite(
                d.Type,
                d.Data,
                new Vector2(d.PosX, d.PosY),
                null,
                d.Color,
                d.FontID,
                d.Alignment,
                d.RorS
                )
                : new MySprite(
                d.Type,
                d.Data,
                new Vector2(d.PosX, d.PosY),
                new Vector2(d.SizeX, d.SizeY),
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