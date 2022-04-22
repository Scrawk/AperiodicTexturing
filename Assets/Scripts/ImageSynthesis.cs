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
    public static class ImageSynthesis
    {


        public static ColorImage2D MakeTileable(ColorImage2D image, ExemplarSet set)
        {
            int width = image.Width;
            int height = image.Height;
            int cutOffset = 2;
            int sinkOffsetX = width / 2 - 48;
            int sinkOffsetY = height / 2 - 48;
            int matchOffset = Math.Min(2, cutOffset);

            var tileable = ColorImage2D.Offset(image, width / 2, height / 2);

            BlurOffsetSeams(tileable, 0.75f);

            var cutBounds = new Box2i(cutOffset, cutOffset, width - 1 - cutOffset, height - 1 - cutOffset);
            var sinkBounds = new Box2i(sinkOffsetX, sinkOffsetY, width - 1 - sinkOffsetX, height - 1 - sinkOffsetY);

            var mask = new BinaryImage2D(width, height);
            mask.DrawBox(cutBounds, ColorRGBA.White, true);
            mask.DrawBox(sinkBounds, ColorRGBA.Black, true);

            var pair = FindBestMatch(tileable, set, null, 0);
            var exemplar = pair.Item1;
            var offset = pair.Item2;

            exemplar.IncrementUsed();
            
            var match = ColorImage2D.Offset(exemplar.Image, offset.x, offset.y);

            var graph = CreateGraph(tileable, mask, match, cutBounds, sinkBounds);
            var cost = PerformGraphCut(graph, tileable, match, cutOffset);

            //BlurGraphCutSeams(tileable, graph, cutOffset, 0.5f);

            BlendGraphCutSeams(tileable, set, graph, cutOffset);

            return tileable;
        }

        public static void CreateTileImage(WangTile tile, ExemplarSet set)
        {
            if (tile.IsConst)
                return;

            int width = tile.TileSize;
            int height = tile.TileSize;
            int sinkOffset = 20;

            var sourceBounds = new Box2i(0, 0, width - 1, height - 1);
            var sinkBounds = new Box2i(sinkOffset, sinkOffset, width - 1 - sinkOffset, height - 1 - sinkOffset);

            var mask = CreateColorEdgeMask(tile, 5);

            foreach (var p in sourceBounds.EnumeratePerimeter())
                mask[p.x, p.y] = true;

            foreach (var p in sinkBounds.EnumerateBounds())
                mask[p.x, p.y] = true;

            var pair = FindBestMatch(tile.Image, set, mask, 0);

            if (pair.Item1 == null)
                return;

            pair.Item1.IncrementUsed();
            var match = pair.Item1.Image;
            var image = tile.Image;

            var graph = CreateGraph(image, match, sourceBounds, sinkBounds);

            graph.Calculate();

            image.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                    image[x, y] = match[x, y];
            });

            BlurSeamsAndEdgeLines(tile, graph, 0.5f);

        }

        private static BinaryImage2D CreateColorEdgeMask(WangTile tile,  int thickness)
        {
            int size = tile.TileSize;
            var mask = new BinaryImage2D(size, size);

            var mid = new Point2i(size / 2, size / 2);

            if (tile.Left != tile.Bottom)
                mask.DrawLine(mid, new Point2i(0, 0), ColorRGBA.White);

            if (tile.Bottom != tile.Right)
                mask.DrawLine(mid, new Point2i(size, 0), ColorRGBA.White);

            if (tile.Right != tile.Top)
                mask.DrawLine(mid, new Point2i(size, size), ColorRGBA.White);

            if (tile.Top != tile.Left)
                mask.DrawLine(mid, new Point2i(0, size), ColorRGBA.White);

            return BinaryImage2D.Dilate(mask, thickness);
        }

        private static (Exemplar, Point2i) FindBestMatch(ColorImage2D image, ExemplarSet set, BinaryImage2D mask, int maxOffset)
        {
            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;
            Point2i bestOffset = new Point2i();

            foreach (var exemplar in set.Exemplars)
            {
                if (exemplar.Image == image)
                    continue;

                if (exemplar.Used > 0)
                    continue;

                float cost = 0;
                int count = 0;
                Point2i offset = new Point2i();

                if (maxOffset <= 0)
                {
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
                }
                else
                {
                    var pair = FindBestOffset(image, exemplar.Image, mask, maxOffset);
                    offset = pair.Item1;
                    cost = pair.Item2;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestMatch = exemplar;
                    bestOffset = offset;
                }
            }

            return (bestMatch, bestOffset);
        }

        private static (Point2i, float) FindBestOffset(ColorImage2D image, ColorImage2D image2, BinaryImage2D mask, int offset)
        {
            Point2i bestOffset = new Point2i();
            float bestCost = float.PositiveInfinity;

            for (int i = -offset; i < offset; i++)
            {
                for (int j = -offset; j < offset; j++)
                {
                    float cost = 0;
                    int count = 0;

                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            if (mask != null && !mask[x, y]) continue;

                            var pixel1 = image[x, y];
                            var pixel2 = image2[x + i, y + j];

                            cost += ColorRGB.SqrDistance(pixel1, pixel2);
                            count++;
                        }
                    }

                    if (count == 0) continue;
                    cost /= count;

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestOffset = new Point2i(i, j);
                    }
                }
            }

            return (bestOffset, bestCost);
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
            mask = GreyScaleImage2D.GaussianBlur(mask, 0.5f, null, null, WRAP_MODE.WRAP);

            var blurred = ColorImage2D.GaussianBlur(image, strength, null, mask, WRAP_MODE.WRAP);
            image.Fill(blurred);
        }

        private static void BlurGraphCutSeams(ColorImage2D image, GridFlowGraph graph, int offset, float strength)
        {
            int width = image.Width;
            int height = image.Height;
            var binary = new BinaryImage2D(width, height);

            var points = graph.FindBoundaryPoints();
            foreach (var p in points)
                binary[p.x + offset, p.y + offset] = true;

            binary = BinaryImage2D.Dilate(binary, 2);

            var mask = binary.ToGreyScaleImage();
            mask = GreyScaleImage2D.GaussianBlur(mask, 0.5f, null, null, WRAP_MODE.WRAP);

            var blurred = ColorImage2D.GaussianBlur(image, strength, null, mask, WRAP_MODE.WRAP);
            image.Fill(blurred);

            mask.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/mask.raw");
        }

        private static void BlendGraphCutSeams(ColorImage2D image, ExemplarSet set, GridFlowGraph graph, int offset)
        {
            int width = image.Width;
            int height = image.Height;
            var mask = new BinaryImage2D(width, height);

            var points = graph.FindBoundaryPoints();
            foreach (var p in points)
                mask[p.x + offset, p.y + offset] = true;

            mask = BinaryImage2D.Dilate(mask, 2);

            image.Iterate((x, y) =>
            {
                //if (mask[x, y])
                //    image.SetPixel(x, y, ColorRGB.Black);
            });

            var rnd = new System.Random(0);

            image.Iterate((x, y) =>
            {
                if (mask[x, y])
                {
                    var pixel = FindBestMatch(x, y, image, null, set, rnd, 9, 1000);

                    image.SetPixel(x, y, pixel);
                    mask[x, y] = false;
                }
            });

        }

        private static ColorRGB FindBestMatch(int x, int y, ColorImage2D image, BinaryImage2D mask, ExemplarSet set, System.Random rnd, int window, int samples)
        {
            ColorRGB bestMatch = new ColorRGB();
            float bestCost = float.PositiveInfinity;
            int half = window / 2;

            for(int s = 0; s < samples; s++)
            {
                int i = rnd.Next(half, set.Source.Width - half);
                int j = rnd.Next(half, set.Source.Height - half);

                int count = 0;
                float cost = 0;

                for(int m = -half; m <= half; m++)
                {
                    for (int n = -half; n <= half; n++)
                    {
                        int xm = x + m;
                        int yn = y + n;

                        int im = i + m;
                        int jn = j + n;

                        if (image.NotInBounds(xm, yn)) continue;
                        if (mask != null && mask[xm, yn]) continue;

                        var p1 = image[xm, yn];
                        var p2 = set.Source[im, jn];
                        var sd = ColorRGB.SqrDistance(p1, p2);

                        cost += sd;
                        count++;
                    }

                    if (count == 0) continue;
                    cost /= count;

                    if(cost < bestCost)
                    {
                        bestCost = cost;
                        bestMatch = set.Source[i, j];
                    }
                }
            }

            return bestMatch;
        }

        private static void BlurSeamsAndEdgeLines(WangTile tile, GridFlowGraph graph, float strength)
        {
            var image = tile.Image;
            int width = image.Width;
            int height = image.Height;
            var binary = new BinaryImage2D(width, height);

            var points = graph.FindBoundaryPoints();
            foreach (var p in points)
                binary[p.x, p.y] = true;

            DrawEdgeLines(tile, binary, graph);

            binary = BinaryImage2D.Dilate(binary, 2);

            var mask2 = binary.ToGreyScaleImage();
            mask2 = GreyScaleImage2D.GaussianBlur(mask2, 0.5f, null, null, WRAP_MODE.WRAP);

            var blurred = ColorImage2D.GaussianBlur(image, strength, null, mask2, WRAP_MODE.WRAP);
            image.Fill(blurred);
        }

        private static void DrawEdgeLines(WangTile tile, BinaryImage2D binary, GridFlowGraph graph)
        {
            int Size = tile.TileSize;

            var mask = CreateMaskFromSink(graph);

            var mid = new Point2i(Size / 2, Size / 2);

            if (tile.Left != tile.Bottom)
                binary.DrawLine(mid, new Point2i(0, 0), ColorRGBA.White, mask);

            if (tile.Bottom != tile.Right)
                binary.DrawLine(mid, new Point2i(Size, 0), ColorRGBA.White, mask);

            if (tile.Right != tile.Top)
                binary.DrawLine(mid, new Point2i(Size, Size), ColorRGBA.White, mask);

            if (tile.Top != tile.Left)
                binary.DrawLine(mid, new Point2i(0, Size), ColorRGBA.White, mask);

        }

        private static GreyScaleImage2D CreateMaskFromSink(GridFlowGraph graph)
        {
            var mask = new GreyScaleImage2D(graph.Width, graph.Height);

            graph.Iterate((x, y) =>
            {
                if (!graph.IsSink(x, y))
                    mask[x, y] = 1.0f;
            });

            return mask;
        }

        private static float PerformGraphCut(GridFlowGraph graph, ColorImage2D image, ColorImage2D match, int cutOffset)
        {
            graph.Calculate();

            float cost = 0;
            int count = 0;

            graph.Iterate((x, y) =>
            {
                int xo = x + cutOffset;
                int yo = y + cutOffset;

                if (graph.IsSink(x, y))
                {
                    cost += ColorRGB.SqrDistance(image[xo, yo], match[xo, yo]);
                    count++;

                    image[xo, yo] = match[xo, yo];
                }
            });

            if (count > 0)
                cost /= count;

            return cost;
        }

        private static GridFlowGraph CreateGraph(ColorImage2D image, BinaryImage2D mask, ColorImage2D match, Box2i cutBounds, Box2i sinkBounds)
        {
            int cutOffset = cutBounds.Min.x;
            var graph = new GridFlowGraph(cutBounds.Width + 1, cutBounds.Height + 1);

            graph.Iterate((x, y) =>
            {
                int xo = x + cutOffset;
                int yo = y + cutOffset;

                if (mask[xo, yo])
                {
                    var col1 = match[xo, yo];
                    var col2 = image[xo, yo];

                    var w1 = ColorRGB.SqrDistance(col1, col2) * 255;

                    for (int i = 0; i < 8; i++)
                    {
                        int xi = xo + D8.OFFSETS[i, 0];
                        int yi = yo + D8.OFFSETS[i, 1];

                        if (xi < 0 || xi >= graph.Width) continue;
                        if (yi < 0 || yi >= graph.Height) continue;
                        if (!mask[xi, yi]) continue;

                        var col1i = match[xi, yi];
                        var col2i = image[xi, yi];

                        var w2 = ColorRGB.SqrDistance(col1i, col2i) * 255;

                        var w = MathUtil.Max(1, w1, w2);

                        graph.SetCapacity(x, y, i, w);

                    }
                }
            });

            foreach (var p in cutBounds.EnumeratePerimeter())
            {
                graph.SetSource(p.x - cutOffset, p.y - cutOffset, 255);
            }

            var expanded = sinkBounds;
            expanded.Min -= 1;
            expanded.Max += 2;
            foreach (var p in expanded.EnumerateBounds())
            {
                graph.SetSink(p.x - cutOffset, p.y - cutOffset, 255);
            }

            return graph;
        }

        private static GridFlowGraph CreateGraph(ColorImage2D image1, ColorImage2D image2, Box2i sourceBounds, Box2i sinkBounds)
        {
            var graph = new GridFlowGraph(image1.Width, image1.Height);

            graph.Iterate((x, y) =>
            {
                var col1 = image1[x, y];
                var col2 = image2[x, y];

                var w1 = ColorRGB.SqrDistance(col1, col2) * 255;

                for (int i = 0; i < 8; i++)
                {
                    int xi = x + D8.OFFSETS[i, 0];
                    int yi = y + D8.OFFSETS[i, 1];

                    if (xi < 0 || xi >= graph.Width) continue;
                    if (yi < 0 || yi >= graph.Height) continue;

                    var col1i = image1[xi, yi];
                    var col2i = image2[xi, yi];

                    var w2 = ColorRGB.SqrDistance(col1i, col2i) * 255;

                    var w = MathUtil.Max(1, w1, w2);

                    graph.SetCapacity(x, y, i, w);
                }

            });

            foreach (var p in sourceBounds.EnumeratePerimeter())
            {
                graph.SetSource(p.x, p.y, 255);
            }

            var expanded = sinkBounds;
            expanded.Min -= 1;
            expanded.Max += 1;
            foreach (var p in expanded.EnumerateBounds())
            {
                graph.SetSink(p.x, p.y, 255);
            }

            return graph;
        }

    }
}
