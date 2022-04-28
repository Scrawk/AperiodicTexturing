using System;
using System.Collections.Generic;

using UnityEngine;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Shapes;
using Common.Core.Directions;
using Common.Core.Extensions;
using Common.GraphTheory.GridGraphs;

using ImageProcessing.Images;
using ImageProcessing.Pixels;

namespace AperiodicTexturing
{
    public static partial class ImageSynthesis
    {

        public static ColorImage2D CreateTileableImage(ColorImage2D image, ExemplarSet set)
        {
            int width = image.Width;
            int height = image.Height;
            int sourceOffset = 2;
            int sinkOffset = 16;

            var tileable = ColorImage2D.Offset(image, width / 2, height / 2);

            BlurOffsetSeams(tileable, 0.75f);

            var sinkBounds = new Box2i(sinkOffset, sinkOffset, width - sinkOffset, height - sinkOffset);

            var exemplar = FindBestMatch(tileable, set, null);
            exemplar.IncrementUsed();

            var match = exemplar.Image;
            var graph = CreateGraph(tileable, match, sourceOffset, sinkBounds);

            graph.Calculate();

            graph.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                    tileable[x, y] = match[x, y];
            });

            BlurGraphCutSeams(tileable, graph, 0.5f);

            return tileable;
        }

        private static Exemplar FindBestMatch(ColorImage2D image, ExemplarSet set, BinaryImage2D mask)
        {
            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;

            foreach (var exemplar in set.Exemplars)
            {
                if (exemplar.Image == image || exemplar.Used > 0)
                    continue;

                float cost = 0;
                int count = 0;

                for (int x = 0; x < exemplar.Width; x++)
                {
                    for (int y = 0; y < exemplar.Height; y++)
                    {
                        if (mask != null && !mask[x, y]) continue;

                        var pixel1 = image[x, y];
                        var pixel2 = exemplar[x, y];

                        cost += ColorRGB.SqrDistance(pixel1, pixel2);
                        count++;
                    }
                }

                if (count == 0) continue;
                cost /= count;

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestMatch = exemplar;
                }
            }

            return bestMatch;
        }

        private static void BlurOffsetSeams(ColorImage2D image, float strength)
        {
            int width = image.Width;
            int height = image.Height;

            var horzontal = new Segment2f(0, height / 2, width, height / 2);
            var vertical = new Segment2f(width / 2, 0, width / 2, height);

            var binary = new BinaryImage2D(width, height);
            binary.DrawLine(horzontal, ColorRGBA.White);
            binary.DrawLine(vertical, ColorRGBA.White);

            binary = BinaryImage2D.Dilate(binary, 5);

            var mask = binary.ToGreyScaleImage();
            var blurred = ColorImage2D.GaussianBlur(image, strength, null, mask, WRAP_MODE.WRAP);
            image.Fill(blurred);
        }

        private static void BlurGraphCutSeams(ColorImage2D image, GridFlowGraph graph, float strength)
        {
            int width = image.Width;
            int height = image.Height;
            var binary = new BinaryImage2D(width, height);

            var points = graph.FindBoundaryPoints(true, true);
            binary.Fill(points, true);
            binary = BinaryImage2D.Dilate(binary, 2);

            var mask = binary.ToGreyScaleImage();
            var blurred = ColorImage2D.GaussianBlur(image, strength, null, mask, WRAP_MODE.WRAP);
            image.Fill(blurred);
        }

        private static GridFlowGraph CreateGraph(ColorImage2D image, ColorImage2D match, int sourceArea, Box2i sinkArea)
        {
            var graph = new GridFlowGraph(image.Width, image.Height);

            graph.Iterate((x, y) =>
            {
                var col1 = match[x, y];
                var col2 = image[x, y];

                var w1 = ColorRGB.SqrDistance(col1, col2) * 255;

                for (int i = 0; i < 8; i++)
                {
                    int xi = x + D8.OFFSETS[i, 0];
                    int yi = y + D8.OFFSETS[i, 1];

                    if (image.NotInBounds(xi, yi)) continue;

                    var col1i = match[xi, yi];
                    var col2i = image[xi, yi];

                    var w2 = ColorRGB.SqrDistance(col1i, col2i) * 255;

                    var w = MathUtil.Max(1, w1, w2);

                    graph.SetCapacity(x, y, i, w);
                }

            });

            graph.Iterate((x, y) =>
            {
                if (x < sourceArea ||
                    y < sourceArea ||
                    x > graph.Width - sourceArea ||
                    y > graph.Height - sourceArea)
                {
                    graph.SetSource(x, y, 255);
                }

            });

            foreach (var p in sinkArea.EnumerateBounds())
            {
                graph.SetSink(p.x, p.y, 255);
            }

            return graph;
        }

    }
}
