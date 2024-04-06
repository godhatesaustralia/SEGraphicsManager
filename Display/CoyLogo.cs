
using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public class CoyLogo
    {
        public static MyGridProgram program;

        List<SpriteGroup> logoPieces;
        IMyTextPanel drawingSurface;
        private float yOffset;
        int groupIndex = 0;

        bool animate = false;
        bool reverse = false;

        int ticks = 0;
        int step = 0;
        int updateFrequency = 1;
        int lastStartStep = 0;
        Dictionary<int, int> alphaDict = new Dictionary<int, int>();
        public Color color;

        public CoyLogo(IMyTextPanel drawingSurface)
        {
            this.drawingSurface = drawingSurface;
            var viewport = new RectangleF((drawingSurface.TextureSize - drawingSurface.SurfaceSize) / 2f, drawingSurface.SurfaceSize);
            yOffset = viewport.Center.Y / 2;

                logoPieces = new List<SpriteGroup> {
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("SquareSimple", new Vector2(245.07f, 51.47f), new Vector2(72.13f, 56.27f), 0.192f),  //Body-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("SquareSimple", new Vector2(282.47f, 76.13f), new Vector2(30.80f, 86.53f), 0.393f),  //Body-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(229.20f, 77.07f), new Vector2(52.67f, 32.53f), -0.427f),     //Body-3
                            Sprite("SquareSimple", new Vector2(209.00f, 84.33f), new Vector2(32.40f, 19.47f), -1.222f), //Body-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(312.5f, 64.91f), new Vector2(53.87f, 66.87f), -0.035f), //Body-5
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(326.49f, 108.37f), new Vector2(77f, 48f), 0.880f),        //Body-6
                            Sprite("Triangle", new Vector2(194.57f, 102.32f), new Vector2(29.33f, 12.53f), -0.341f), //Body-8
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(192.47f, 87.77f), new Vector2(15.30f, 10.57f), -1.298f), //Body-7
                            Sprite("Triangle", new Vector2(242.04f, 17f), new Vector2(50.83f, 12.87f), 2.9f),       //Body-9
                            Sprite("Triangle", new Vector2(211.76f, 23.60f), new Vector2(42.60f, 25.27f), -0.541f), //Body-10
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("SquareSimple", new Vector2(266.47f, 130.67f), new Vector2(79.53f, 11.73f), -0.838f), //backleg1-1
                            Sprite("Triangle", new Vector2(241.5f, 107f), new Vector2(61f, 30f), 2.69f),                 //frontleg-1
                            Sprite("Triangle", new Vector2(177f, 37.40f), new Vector2(53.63f, 44.30f), -1.444f),         //neck-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("RightTriangle", new Vector2(272f, 175f), new Vector2(58.13f, 19.87f), 0.720f), //backleg1-2
                            Sprite("Triangle", new Vector2(210f, 111f), new Vector2(74f, 13.33f), -0.032f),        //frontleg-2
                            Sprite("Triangle", new Vector2(195f, 57.73f), new Vector2(23.79f, 19.43f), -1.381f),   //neck-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("SquareSimple", new Vector2(271.93f, 210.47f), new Vector2(34.93f, 11.33f), -0.811f), //backleg1-3
                            Sprite("RightTriangle", new Vector2(211f, 117f), new Vector2(74.53f, 14.67f), -0.201f),      //frontleg-3
                            Sprite("Triangle", new Vector2(197.5f, 78.73f), new Vector2(24.80f, 7.73f), 2.315f),         //neck-3
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("RightTriangle", new Vector2(249f, 226.75f), new Vector2(31.40f, 29.33f), -1.602f), //backleg1-4
                            Sprite("SquareSimple", new Vector2(172.5f, 134f), new Vector2(31.98f, 13.15f), -1.094f),   //frontleg-4
                            Sprite("Triangle", new Vector2(179.07f, 24.13f), new Vector2(38.31f, 10.00f), 2.317f),     //neck-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle",  new Vector2(298f, 128f), new Vector2(72f, 45f), 0),                //backleg2-1
                            Sprite("Triangle", new Vector2(167f, 152f), new Vector2(19.27f, 9.60f), 1.318f),       //frontleg-5
                            Sprite("RightTriangle", new Vector2(164f, 54f), new Vector2(22.54f, 40.04f), -0.928f), //head-1
                            Sprite("Triangle", new Vector2(147.25f, 107.6f), new Vector2(16.23f, 8.19f), 1.564f),  //nose-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(299f, 180f), new Vector2(73.60f, 59.47f), 3.142f),      //backleg2-2
                            Sprite("Triangle", new Vector2(184.5f, 137.5f), new Vector2(15.47f, 16.48f), -0.008f), //frontleg-6
                            Sprite("SquareSimple", new Vector2(172f, 84f), new Vector2(65.24f, 22.99f), -0.824f),  //head-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(300.40f, 207.27f), new Vector2(103.07f, 15.67f), 1.844f), //backleg2-3
                            Sprite("Triangle", new Vector2(181f, 149f), new Vector2(21.52f, 13f), -0.81f),           //frontleg-7
                            Sprite("Triangle", new Vector2(146.73f, 61.8f), new Vector2(48.48f, 19.08f), -2.533f),   //head-3
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(287.5f, 254f), new Vector2(42.93f, 18.33f), 1.932f),  //backleg2-4
                            Sprite("Triangle", new Vector2(189f, 153f), new Vector2(11.87f, 23f), 0.465f),       //frontleg-8
                            Sprite("Triangle", new Vector2(149.80f, 74.40f), new Vector2(50.55f, 26f), -0.824f), //head-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(347f, 134f), new Vector2(44f, 25.0f), 1.734f),        //tail-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(381f, 190.60f), new Vector2(139.87f, 30.00f),1.277f), //tail-2
                            Sprite("Triangle", new Vector2(155f, 115.07f), new Vector2(16.00f, 10.07f), 1.629f), //nose-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(370f, 219.87f), new Vector2(91.33f, 30.93f), 1.778f),  //tail-3
                            Sprite("Triangle", new Vector2(146.77f, 121.50f), new Vector2(9.93f, 5.67f), 1.597f), //nose-3
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(378f, 257.5f), new Vector2(75.07f, 30.00f), 1.961f),    //tail-4
                            Sprite("Triangle", new Vector2(146.07f, 117.20f), new Vector2(9.60f, 5.60f), -1.558f), //nose-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(134.87f, 35.5f), new Vector2(37.87f, 9.87f), 0.661f),  //ear-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(152.8f, 44.2f), new Vector2(54.40f, 15.13f), -2.496f), //ear-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(178.73f, 210.17f), new Vector2(16.40f, 18.40f), 3.142f), //star1-1
                            Sprite("Triangle", new Vector2(178.73f, 191.83f), new Vector2(16.53f, 18.40f), 0.000f), //star1-2
                        },
                        10,
                        10
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            Sprite("Triangle", new Vector2(192.17f, 201.40f), new Vector2(8.27f, 6.00f), 1.571f),   //star2-1
                            Sprite("Triangle", new Vector2(165.17f, 201.40f), new Vector2(8.27f, 6.00f), -1.571f),  //star2-2
                        },
                        10,
                        10
                    )
                };
        }

        public void Update(string argument)
        {
            if (!animate)
            {
                return;
            }

            //Hack to force screen redraw for high frame rate
            drawingSurface.ContentType = ContentType.TEXT_AND_IMAGE;
            drawingSurface.ContentType = ContentType.SCRIPT;

            ticks += 1;

            if (ticks % updateFrequency == 0)
            {
                step += 1;
            }
            else
            {
                return;
            }

            //Check if the next group of sprites should begin animating
            int nextStep = -1;
            if (reverse && groupIndex != 0)
            {
                nextStep = logoPieces[groupIndex].offset;
            }
            else if (!reverse && groupIndex < logoPieces.Count - 1)
            {
                nextStep = logoPieces[groupIndex + 1].offset;
            }

            if (step - lastStartStep == nextStep && ticks % updateFrequency == 0)
            {
                lastStartStep = step;
                groupIndex = reverse ? groupIndex - 1 : groupIndex + 1;

                if (!alphaDict.ContainsKey(groupIndex) && !reverse)
                {
                    alphaDict.Add(groupIndex, 0);
                }
            }

            //Check if the animation is finished and all pieces are faded in/out
            if (groupIndex == logoPieces.Count - 1 && alphaDict[groupIndex] == 255 && !reverse)
            {
                animate = false;
                reverse = true;
                return;
            }

            if (groupIndex == 0 && alphaDict.Count == 0 && reverse)
            {
                reverse = false;
                animate = false;
                return;
            }

            //Draw stuff
            if (reverse)
            {
                AnimateReverse();
            }
            else
            {
                Animate();
            }
        }

        public void SetAnimate(bool reverse = false)
        {
            this.reverse = reverse;

            step = 0;
            lastStartStep = 0;
            ticks = 0;
            animate = true;
            if (!reverse && !alphaDict.ContainsKey(0))
            {
                alphaDict.Add(0, 0);
            }
        }

        public void Reset()
        {
            var frameClr = drawingSurface.DrawFrame();
            frameClr.Dispose();
            animate = false;
            reverse = false;
            groupIndex = 0;
            alphaDict.Clear();
        }

        private void Animate()
        {
            var frame = drawingSurface.DrawFrame();
            //var v = new Vector2(256, 256);
            //frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", v, 2 * v, color, null, TextAlignment.CENTER));

            for (int i = 0; i <= groupIndex; i++)
            {
                var spriteGroup = logoPieces[i];

                var newAlpha = Math.Min(255, alphaDict[i] + (int)Math.Ceiling(255 / (double)spriteGroup.steps));
                alphaDict[i] = newAlpha;
                
                for (int j = 0; j < spriteGroup.sprites.Count; j++)
                {
                    var sprite = spriteGroup.sprites[j];
                    sprite.Color = newAlpha == 255 ? color: AlphaColor(newAlpha);
                    frame.Add(sprite);
                }
            }

            frame.Dispose();
        }

        private Color AlphaColor(float na)
        {
            return new Color(color.R / na, color.G / na, color.B / na, na);
        }

        private void AnimateReverse()
        {
            var frame = drawingSurface.DrawFrame();
            for (int i = logoPieces.Count - 1; i >= 0; i--)
            {
                if (!alphaDict.ContainsKey(i))
                {
                    continue;
                }

                var spriteGroup = logoPieces[i];
                var newAlpha = 255;
                if (i >= groupIndex)
                {
                    newAlpha = Math.Max(0, alphaDict[i] - (int)Math.Ceiling(255 / (double)spriteGroup.steps));
                    alphaDict[i] = newAlpha;
                }

                if (newAlpha == 0)
                {
                    alphaDict.Remove(i);
                    continue;
                }

                for (int j = 0; j < spriteGroup.sprites.Count; j++)
                {
                    var sprite = spriteGroup.sprites[j];
                    sprite.Color = newAlpha == 255 ? color : AlphaColor(newAlpha);

                    frame.Add(sprite);
                }
            }

            frame.Dispose();
        }

        private MySprite Sprite(string shape, Vector2 pos, Vector2 size, float rotation)
        {
            //TODO: figure out how to rotate for wide LCD
            pos.Y = pos.Y + yOffset;
            return new MySprite(SpriteType.TEXTURE, shape, pos, size, color, rotation: rotation, alignment: TextAlignment.CENTER);
        }
    }

    public class SpriteGroup
    {
        public List<MySprite> sprites;
        public int steps; //how many steps to fade in
        public int offset; //steps to wait to start animating after previous sprite group
        public SpriteGroup(List<MySprite> sprites, int steps = 4, int offset = 2)
        {
            this.sprites = sprites;
            this.steps = steps;
            this.offset = offset;
        }
    }
}