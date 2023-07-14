using Sandbox.ModAPI.Ingame;
using System;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public class SpriteData
    {
        #region fields

        public SpriteType spriteType;
        public TextAlignment SpriteAlignment;
        public Action<SpriteData> Command;
        public UpdateFrequency CommandFrequency;
        public float
            SpriteSizeX,
            SpriteSizeY,
            SpritePosX,
            SpritePosY,
            SpriteRorS;
        public string
            Name,
            Data,
            FontID = "DEBUG",
            CommandString;
        public Color SpriteColor;

        #endregion

        public SpriteData()
        {
            spriteType = SharedUtilities.defaultType;
        }

        public SpriteData(SpriteType type, string name, string data, float sizeX, float sizeY, TextAlignment alignment, float posX, float posY, float ros, Color color, string fontid = "White", UpdateFrequency updateType = UpdateFrequency.None, string command = "")
        {
            spriteType = type;
            Name = name;    
            Data = data;
            SpriteSizeX = sizeX;
            SpriteSizeY = sizeY;
            SpriteAlignment = alignment;
            SpritePosX = posX;
            SpritePosY = posY;
            SpriteRorS = ros;
            SpriteColor = color;
            FontID = fontid;
            CommandFrequency = updateType; //NONE = 0, 1 = 1, 10 = 2, 100 = 3, ONCE = 8
            CommandString = command;     
        }
    }
}