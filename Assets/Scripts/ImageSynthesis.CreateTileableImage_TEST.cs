using System;
using System.Collections.Generic;

using UnityEngine;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Shapes;
using Common.Core.Threading;
using Common.Core.Extensions;
using Common.GraphTheory.GridGraphs;

using ImageProcessing.Images;

namespace AperiodicTexturing
{
    public static partial class ImageSynthesis
    {
        public static Tile[] CreateTileableImages_TEST(IList<Tile> tiles, ExemplarSet set, ThreadingToken token = null)
        {
            //For each tiles a new tileable tile will be created.
            int count = tiles.Count;
            var tileables = new Tile[count];

            //If a token is being used set how many steps there are.
            // Each step represents a tile being created on its own thread.
            if (token != null)
            {
                token.EnqueueMessage("Stage 1 of 3");
                token.Steps = count;
            }

            //For each tile run a run stage 1 on its own thread.
            ThreadingBlock1D.ParallelAction(count, 1, (i) =>
            {
                tileables[i] = CreateTileableImageStage1_TEST(tiles[i], set);
            }, token);

            return tileables;
        }

        private static Tile CreateTileableImageStage1_TEST(Tile tile, ExemplarSet set)
        {
            int width = tile.Width;
            int height = tile.Height;
            int xoffset = width / 2;
            int yoffset = height / 2;
            int thickness = 10;

            //Copy the tile and offset
            var tileable = tile.Copy();
            tileable.Offset(xoffset, yoffset);

            //Create the lines that will cover the seems in the tile.
            var horzontal = new Segment2f(0, height / 2, width, height / 2);
            var vertical = new Segment2f(width / 2, 0, width / 2, height);

            //Create a mask that covers the seem in the tile.
            //This is the area that will be blurred.
            var mask = CreateOffsetSeamsMask(width, height, horzontal, vertical, thickness);

            var rnd = new System.Random(0);

            var points = new List<Point2i>();
            tileable.Image.Iterate((x, y) =>
            {
                if (mask[x, y])
                {
                    points.Add(new Point2i(x, y));

                    int i = rnd.Next(0, width);
                    int j = rnd.Next(0, height);

                    var pixel = tileable.Image[i, j];
                    tileable.Image[x, y] = ColorRGBA.Black;
                }
            });

            points.Shuffle(rnd);

            int exemplarSize = set.ExemplarSize;
            int halfExemplarSize = set.ExemplarSize / 2;

            foreach (var p in points)
            {
                int x = p.x;
                int y = p.y;

                if (!mask[x, y]) continue;

                var box = new Box2i(x - halfExemplarSize, y - halfExemplarSize, x + halfExemplarSize, y + halfExemplarSize);
                var crop = ColorImage2D.Crop(tileable.Image, box, 0, WRAP_MODE.WRAP);

                var match = FindBestMatch_TEST(crop, set, mask);

                var graph = CreateGraph(crop, match, null, false);
                MarkSourceAndSink(graph, 2, halfExemplarSize - 4);
                graph.Calculate();

                var blendMask = CreateMaskFromGraph_TEST(graph, 2, 0.5f);

                var blendedImage = BlendImages_TEST(graph, crop, match, blendMask);

                var m = blendMask.ToBinaryImage();
                m.Invert();

                tileable.Image.Fill(blendedImage, box, WRAP_MODE.WRAP);
                mask.Fill(box, m, false, WRAP_MODE.WRAP);

            };

            return tileable;
        }

        private static BinaryImage2D CreateOffsetSeamsMask(int width, int height, Segment2f horzontal, Segment2f vertical, int thickness)
        {
            var binary = new BinaryImage2D(width, height);

            //Draw the segments into the mask. These areas will now be white.
            binary.DrawLine(horzontal, ColorRGBA.White);
            binary.DrawLine(vertical, ColorRGBA.White);

            //dilate the masked areas so they are thicker.
            binary = BinaryImage2D.Dilate(binary, thickness);

            var mask = BinaryImage2D.ApproxEuclideanDistance(binary, WRAP_MODE.WRAP);
            mask.Normalize();

            var rnd = new System.Random(0);

            mask.Iterate((x, y) =>
            {
                float m = mask[x, y];

                if (m > 0 && rnd.NextFloat() > m && rnd.NextFloat() > 0.1f)
                {
                    mask[x, y] = 0;
                }
            });

            mask.DrawLine(horzontal, ColorRGBA.White);
            mask.DrawLine(vertical, ColorRGBA.White);

            return mask.ToBinaryImage();
        }

        private static GreyScaleImage2D CreateMaskFromGraph_TEST(GridFlowGraph graph, int thickness, float strength)
        {
            var mask = new GreyScaleImage2D(graph.Width, graph.Height);

            mask.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                    mask[x, y] = 1;
            });

            if(thickness > 0)
                mask = GreyScaleImage2D.Dilate(mask, thickness);

            if(strength > 0)
                mask = GreyScaleImage2D.GaussianBlur(mask, strength, null, null, WRAP_MODE.WRAP);

            return mask;
        }

        private static ColorImage2D BlendImages_TEST(GridFlowGraph graph, ColorImage2D source, ColorImage2D sink, GreyScaleImage2D mask)
        {
            var image = new ColorImage2D(graph.Width, graph.Height);
            image.Fill(source);

            graph.Iterate((x, y) =>
            {
                if (mask == null)
                {
                    if (graph.IsSink(x, y))
                        image[x, y] = sink[x, y];
                }
                else
                {
                    float a = mask[x, y];
                    image[x, y] = ColorRGBA.Lerp(source[x, y], sink[x, y], a);
                }
            });

            return image;
        }

        private static ColorImage2D FindBestMatch_TEST(ColorImage2D image, ExemplarSet set, BinaryImage2D mask)
        {

            var costs = new Tuple<float, Exemplar>[set.ExemplarCount];

            for (int e = 0; e < costs.Length; e++)
            {
                var exemplar = set[e];

                if (exemplar == null)
                {
                    costs[e] = new Tuple<float, Exemplar>(float.PositiveInfinity, null);
                    continue;
                }

                float cost = 0;
                int count = 0;

                for (int j = 0; j < exemplar.Height; j++)
                {
                    for (int i = 0; i < exemplar.Width; i++)
                    {
                        var exemplars_pixel = exemplar.Tile.Image[i, j];
                        var tiles_pixel = image[i, j];

                        count++;
                        cost += ColorRGBA.SqrDistance(exemplars_pixel, tiles_pixel);
                    }
                }

                if (count != 0)
                    cost = cost / count;
                else
                    cost = float.PositiveInfinity;

                costs[e] = new Tuple<float, Exemplar>(cost, exemplar);
            }

            Array.Sort(costs, (x, y) => x.Item1.CompareTo(y.Item1));

            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;

            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].Item1 == float.PositiveInfinity) continue;
                if (costs[i].Item2 == null) continue;

                if (costs[i].Item1 < bestCost)
                {
                    bestCost = costs[i].Item1;
                    bestMatch = costs[i].Item2;
                }
            }

            if (bestMatch == null)
                return null;
            else
                return bestMatch.Tile.Image;
        }

    }

}
