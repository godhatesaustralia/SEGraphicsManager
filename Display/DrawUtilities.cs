using Sandbox.Engine.Platform.VideoMode;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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
    public static class DrawUtilities
    {
        static public SpriteType defaultType = SpriteType.TEXT;
        public static void DrawNewSprite(ref MySpriteDrawFrame frame, SpriteData data)
        {
                var sprite = new MySprite()
                {
                    Type = data.spriteType,
                    Data = data.Data,
                    Position = new Vector2(data.SpritePosX, data.SpritePosY),
                    RotationOrScale = data.SpriteRorS,
                    Color = data.SpriteColor,
                    Alignment = data.SpriteAlignment,
                    FontId = data.FontID
                };
            frame.Add(sprite);
        }
        public static void SetupBarGraph(ref SpriteData sprite, float pctData)
        {
        if (sprite.SpriteSizeX > sprite.SpriteSizeY)
                sprite.SpriteSizeX *= pctData;
        else if (sprite.SpriteSizeY > sprite.SpriteSizeX)
                sprite.SpriteSizeY *= pctData;
        }
    }

    public static class UpdateUtilities
    {
        public static UpdateFrequency UpdateConverter(UpdateType source)
        {
            var updateFrequency = UpdateFrequency.None; //0000
            if ((source & UpdateType.Update1) != 0) updateFrequency |= UpdateFrequency.Update1; //0001
            if ((source & UpdateType.Update10) != 0) updateFrequency |= UpdateFrequency.Update10; //0010
            if ((source & UpdateType.Update100) != 0) updateFrequency |= UpdateFrequency.Update100;//0100
            return updateFrequency;
        }
    }
}