using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace IngameScript
{
    public class CoyLogo
    {
        public static MyGridProgram program;

        List<SpriteGroup> _pieces;
        IMyTextPanel _drawSurf;
        float yOffset;

        int
            ticks = 0,
            step = 0,
            updateFrequency = 1,
            lastStartStep = 0,
            groupIndex = 0;

        bool 
            animate = false,
            reverse = false;
        // char save (maybe)
        const string
            SQS = "SquareSimple",
            RGT = "RightTriangle",
            TRI = "Triangle";

        /// <summary>
        /// alpha value map
        /// </summary>
        Dictionary<int, int> _aDict = new Dictionary<int, int>();
        public Color Color;

        public CoyLogo(IMyTextPanel surface, bool f = false, bool txt = false, bool vcr = false)
        {
            _drawSurf = surface;
            var viewport = new RectangleF((surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);
            yOffset = viewport.Center.Y / 2;

                _pieces = new List<SpriteGroup> {
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(SQS, V2(245.07f, 51.47f), V2(72.13f, 56.27f), 0.192f),  //Body-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(SQS, V2(282.47f, 76.13f), V2(30.80f, 86.53f), 0.393f),  //Body-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(229.20f, 77.07f), V2(52.67f, 32.53f), -0.427f),     //Body-3
                            SPR(SQS, V2(209.00f, 84.33f), V2(32.40f, 19.47f), -1.222f), //Body-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(312.5f, 64.91f), V2(53.87f, 66.87f), -0.035f), //Body-5
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(326.49f, 108.37f), V2(77f, 48f), 0.880f),        //Body-6
                            SPR(TRI, V2(194.57f, 102.32f), V2(29.33f, 12.53f), -0.341f), //Body-8
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(192.47f, 87.77f), V2(15.30f, 10.57f), -1.298f), //Body-7
                            SPR(TRI, V2(242.04f, 17f), V2(50.83f, 12.87f), 2.9f),       //Body-9
                            SPR(TRI, V2(211.76f, 23.60f), V2(42.60f, 25.27f), -0.541f), //Body-10
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(SQS, V2(266.47f, 130.67f), V2(79.53f, 11.73f), -0.838f), //backleg1-1
                            SPR(TRI, V2(241.5f, 107f), V2(61f, 30f), 2.69f),                 //frontleg-1
                            SPR(TRI, V2(177f, 37.40f), V2(53.63f, 44.30f), -1.444f),         //neck-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(RGT, V2(272f, 175f), V2(58.13f, 19.87f), 0.720f), //backleg1-2
                            SPR(TRI, V2(210f, 111f), V2(74f, 13.33f), -0.032f),        //frontleg-2
                            SPR(TRI, V2(195f, 57.73f), V2(23.79f, 19.43f), -1.381f),   //neck-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(SQS, V2(271.93f, 210.47f), V2(34.93f, 11.33f), -0.811f), //backleg1-3
                            SPR(RGT, V2(211f, 117f), V2(74.53f, 14.67f), -0.201f),      //frontleg-3
                            SPR(TRI, V2(197.5f, 78.73f), V2(24.80f, 7.73f), 2.315f),         //neck-3
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(RGT, V2(249f, 226.75f), V2(31.40f, 29.33f), -1.602f), //backleg1-4
                            SPR(SQS, V2(172.5f, 134f), V2(31.98f, 13.15f), -1.094f),   //frontleg-4
                            SPR(TRI, V2(179.07f, 24.13f), V2(38.31f, 10.00f), 2.317f),     //neck-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI,  V2(298f, 128f), V2(72f, 45f), 0),                //backleg2-1
                            SPR(TRI, V2(167f, 152f), V2(19.27f, 9.60f), 1.318f),       //frontleg-5
                            SPR(RGT, V2(164f, 54f), V2(22.54f, 40.04f), -0.928f), //head-1
                            SPR(TRI, V2(147.25f, 107.6f), V2(16.23f, 8.19f), 1.564f),  //nose-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(299f, 180f), V2(73.60f, 59.47f), 3.142f),      //backleg2-2
                            SPR(TRI, V2(184.5f, 137.5f), V2(15.47f, 16.48f), -0.008f), //frontleg-6
                            SPR(SQS, V2(172f, 84f), V2(65.24f, 22.99f), -0.824f),  //head-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(300.40f, 207.27f), V2(103.07f, 15.67f), 1.844f), //backleg2-3
                            SPR(TRI, V2(181f, 149f), V2(21.52f, 13f), -0.81f),           //frontleg-7
                            SPR(TRI, V2(146.73f, 61.8f), V2(48.48f, 19.08f), -2.533f),   //head-3
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(287.5f, 254f), V2(42.93f, 18.33f), 1.932f),  //backleg2-4
                            SPR(TRI, V2(189f, 153f), V2(11.87f, 23f), 0.465f),       //frontleg-8
                            SPR(TRI, V2(149.80f, 74.40f), V2(50.55f, 26f), -0.824f), //head-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(347f, 134f), V2(44f, 25.0f), 1.734f),        //tail-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(381f, 190.60f), V2(139.87f, 30.00f),1.277f), //tail-2
                            SPR(TRI, V2(155f, 115.07f), V2(16.00f, 10.07f), 1.629f), //nose-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(370f, 219.87f), V2(91.33f, 30.93f), 1.778f),  //tail-3
                            SPR(TRI, V2(146.77f, 121.50f), V2(9.93f, 5.67f), 1.597f), //nose-3
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(378f, 257.5f), V2(75.07f, 30.00f), 1.961f),    //tail-4
                            SPR(TRI, V2(146.07f, 117.20f), V2(9.60f, 5.60f), -1.558f), //nose-4
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(134.87f, 35.5f), V2(37.87f, 9.87f), 0.661f),  //ear-1
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(152.8f, 44.2f), V2(54.40f, 15.13f), -2.496f), //ear-2
                        }
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(178.73f, 210.17f), V2(16.40f, 18.40f), 3.142f), //star1-1
                            SPR(TRI, V2(178.73f, 191.83f), V2(16.53f, 18.40f), 0.000f), //star1-2
                        },
                        10,
                        10
                    ),
                    new SpriteGroup(
                        new List<MySprite> {
                            SPR(TRI, V2(192.17f, 201.40f), V2(8.27f, 6.00f), 1.571f),   //star2-1
                            SPR(TRI, V2(165.17f, 201.40f), V2(8.27f, 6.00f), -1.571f),  //star2-2
                        },
                        10,
                        10
                    ),
                };
            if (txt)
                _pieces.Add(new SpriteGroup(
                        new List<MySprite>
                        {
                            new MySprite(SpriteType.TEXT, "COYOTE ORBITAL", V2(256, 432), null, Color, vcr ? "VCRBold" : "White", rotation: vcr ? 1.125f : 2.25f)
                        },
                        10,
                        10
                    ));
            if (f)
                _pieces.AddOrInsert(new SpriteGroup
                (
                new List<MySprite> {
                            SPR("SquareHollow", V2(256, 128), V2(512, 512), 0)
                    }
                ), 0);
        }

        public void Update()
        {
            if (!animate)
            {
                return;
            }

            //Hack to force screen redraw for high frame rate
            _drawSurf.ContentType = ContentType.TEXT_AND_IMAGE;
            _drawSurf.ContentType = ContentType.SCRIPT;

            ticks += 1;

            if (ticks % updateFrequency == 0)
            {
                step += 1;
            }
            else
            {
                return;
            }

            //Check if the next group of list should begin animating
            int nextStep = -1;
            if (reverse && groupIndex != 0)
            {
                nextStep = _pieces[groupIndex].offset;
            }
            else if (!reverse && groupIndex < _pieces.Count - 1)
            {
                nextStep = _pieces[groupIndex + 1].offset;
            }

            if (step - lastStartStep == nextStep && ticks % updateFrequency == 0)
            {
                lastStartStep = step;
                groupIndex = reverse ? groupIndex - 1 : groupIndex + 1;

                if (!_aDict.ContainsKey(groupIndex) && !reverse)
                {
                    _aDict.Add(groupIndex, 0);
                }
            }

            //Check if the animation is finished and all pieces are faded in/out
            if (groupIndex == _pieces.Count - 1 && _aDict[groupIndex] == 255 && !reverse)
            {
                animate = false;
                reverse = true;
                return;
            }

            if (groupIndex == 0 && _aDict.Count == 0 && reverse)
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

        public void SetAnimate(bool r = false)
        {
            reverse = r;

            step = 0;
            lastStartStep = 0;
            ticks = 0;
            animate = true;
            if (!r && !_aDict.ContainsKey(0))
            {
                _aDict.Add(0, 0);
            }
        }

        public void Reset()
        {
            var frameClr = _drawSurf.DrawFrame();
            frameClr.Dispose();
            animate = false;
            reverse = false;
            groupIndex = 0;
            _aDict.Clear();
        }

        void Animate()
        {
            var frame = _drawSurf.DrawFrame();
            //var v = V2(256, 256);
            //frame.Add(new MySprite(SpriteType.TEXTURE, "SquareHollow", v, 2 * v, color, null, TextAlignment.CENTER));

            for (int i = 0; i <= groupIndex; i++)
            {
                var sGroup = _pieces[i];

                var newAlpha = Math.Min(255, _aDict[i] + (int)Math.Ceiling(255 / (double)sGroup.steps));
                _aDict[i] = newAlpha;
                
                for (int j = 0; j < sGroup.sprites.Count; j++)
                {
                    var s = sGroup.sprites[j];
                    s.Color = newAlpha == 255 ? Color: AlphaColor(newAlpha);
                    frame.Add(s);
                }
            }

            frame.Dispose();
        }

        Color AlphaColor(float na)
        {
            return new Color(Color.R / na, Color.G / na, Color.B / na, na);
        }

        void AnimateReverse()
        {
            var frame = _drawSurf.DrawFrame();
            for (int i = _pieces.Count - 1; i >= 0; i--)
            {
                if (!_aDict.ContainsKey(i))
                {
                    continue;
                }

                var sGroup = _pieces[i];
                var newAlpha = 255;
                if (i >= groupIndex)
                {
                    newAlpha = Math.Max(0, _aDict[i] - (int)Math.Ceiling(255 / (double)sGroup.steps));
                    _aDict[i] = newAlpha;
                }

                if (newAlpha == 0)
                {
                    _aDict.Remove(i);
                    continue;
                }

                for (int j = 0; j < sGroup.sprites.Count; j++)
                {
                    var sprite = sGroup.sprites[j];
                    sprite.Color = newAlpha == 255 ? Color : AlphaColor(newAlpha);

                    frame.Add(sprite);
                }
            }

            frame.Dispose();
        }

        MySprite SPR(string shape, Vector2 pos, Vector2 size, float rotation)
        {
            //TODO: figure out how to rotate for wide LCD
            pos.Y += yOffset;
            return new MySprite(SpriteType.TEXTURE, shape, pos, size, Color, rotation: rotation, alignment: TextAlignment.CENTER);
        }
        Vector2 V2(float x, float y) => new Vector2(x, y);
    }

    public class SpriteGroup
    {
        public List<MySprite> sprites;
        public int 
            steps, //how many steps to fade in
            offset; //steps to wait to start animating after previous sprite group
        public SpriteGroup(List<MySprite> list, int s = 4, int o = 2)
        {
            sprites = list;
            steps = s;
            offset = o;
        }
    }
}