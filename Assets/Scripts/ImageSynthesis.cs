using System;
using System.Collections.Generic;
using System.Linq;

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

        public static void CreateTileImage(WangTile tile, IList<ColorImage2D> tilables)
        {
            if(tile.IsConst)
            {
                int color = tile.Left;
                tile.Image.Fill(tilables[color]);
                return;
            }

            var map = CreateMap(tile);
            var mask = CreateMask(map, 0, 1);

            var graph = CreateGraph(tilables[0], tilables[1]);

            MarkSourceAndSink(graph, 0, 1, map, mask);

            PerformGraphCut(graph, tile.Image, tilables[0], tilables[1]);

            var m = CreateImageFromGraph(graph, true, false);
            m.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/mask.raw");
            tile.Image.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/tile.raw");
        }

        private static GreyScaleImage2D CreateMap(WangTile tile)
        {
            int size = tile.TileSize;
            var map = new GreyScaleImage2D(size, size);

            if (tile.IsConst)
            {
                map.Fill(tile.Left);
            }
            else
            {
                var c00 = new Point2i(0, 0);
                var c01 = new Point2i(0, size);
                var c10 = new Point2i(size, 0);
                var c11 = new Point2i(size, size);
                var mid = new Point2i(size / 2, size / 2);

                map.DrawTriangle(mid, c00, c01, new ColorRGBA(tile.Left, 1), true);
                map.DrawTriangle(mid, c00, c10, new ColorRGBA(tile.Bottom, 1), true);
                map.DrawTriangle(mid, c10, c11, new ColorRGBA(tile.Right, 1), true);
                map.DrawTriangle(mid, c01, c11, new ColorRGBA(tile.Top, 1), true);
            }

            return map;
        }

        private static BinaryImage2D CreateMask(GreyScaleImage2D map, int color1, int color2)
        {
            var mask = new BinaryImage2D(map.Size);

            mask.Iterate((x, y) =>
            {
                if(map[x, y] == color1)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        int xi = x + D8.OFFSETS[i, 0];
                        int yi = y + D8.OFFSETS[i, 1];

                        if (mask.NotInBounds(xi, yi)) continue;

                        if(map[xi, yi] == color2)
                        {
                            mask[x, y] = true;
                            break;
                        }

                    }
                }

            });

            mask = BinaryImage2D.Dilate(mask, 5);

            var bounds = mask.Bounds;
            var corners = bounds.GetCorners();

            foreach(var p in bounds.EnumeratePerimeter())
            {
                if (!corners.Contains(p))
                    mask[p] = false;
            }

            return mask;
        }

        private static void FillImage(ColorImage2D image, GreyScaleImage2D map, IList<ColorImage2D> tileables)
        {
            image.Fill((x, y) =>
            {
                var index = (int)map[x, y];
                var pixel = tileables[index][x, y];

                return pixel;
            });
        }

        private static GridFlowGraph CreateGraph(ColorImage2D image1, ColorImage2D image2)
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

            return graph;
        }

        private static void MarkSourceAndSink(GridFlowGraph graph, int color1, int color2, GreyScaleImage2D map, BinaryImage2D mask)
        {
            graph.Iterate((x, y) =>
            {
                if (mask[x, y]) return;

                if (map[x, y] != color1)
                    graph.SetSource(x, y, 255);
                else if(map[x, y] != color2)
                    graph.SetSink(x, y, 255);
            });
        }

        private static void PerformGraphCut(GridFlowGraph graph, ColorImage2D image, ColorImage2D image1, ColorImage2D image2)
        {
            graph.Calculate();

            image.Fill((x, y) =>
            {
                if (graph.IsSource(x, y))
                    return image1[x, y];
                else
                    return image2[x, y];
            });
        }

        private static GreyScaleImage2D CreateImageFromGraph(GridFlowGraph graph, bool source, bool sink)
        {
            var mask = new GreyScaleImage2D(graph.Width, graph.Height);

            graph.Iterate((x, y) =>
            {
                if (source && graph.IsSink(x, y))
                    mask[x, y] = 1.0f;

                if (sink && graph.IsSource(x, y))
                    mask[x, y] = 1.0f;
            });

            return mask;
        }


        /*

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

            mask.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/mask.raw");

            var match = FindBestMatch(tile.Image, set, mask);
            var image = tile.Image;

            var graph = CreateGraph(image, match.Image, sourceBounds, sinkBounds);
            PerformGraphCut(graph, image, match.Image);

            BlurSeamsAndEdgeLines(tile, graph, 0.5f);

            tile.Image.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/tile.raw");

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

        private static Exemplar FindBestMatch(ColorImage2D image, ExemplarSet set, BinaryImage2D mask)
        {
            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;

            foreach (var exemplar in set.Exemplars)
            {
                if (exemplar.Image == image)
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

        private static void BlurSeamsAndEdgeLines(WangTile tile, GridFlowGraph graph, float strength)
        {
            var image = tile.Image;
            int width = image.Width;
            int height = image.Height;
            var binary = new BinaryImage2D(width, height);

            var points = graph.FindBoundaryPoints(true, false);
            binary.Fill(points, true);

            DrawEdgeLines(tile, binary, graph);

            binary = BinaryImage2D.Dilate(binary, 2);

            var mask = binary.ToGreyScaleImage();
            //mask = GreyScaleImage2D.GaussianBlur(mask2, 0.5f, null, null, WRAP_MODE.WRAP);

            var blurred = ColorImage2D.GaussianBlur(image, strength, null, mask, WRAP_MODE.WRAP);
            image.Fill(blurred);

            mask.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/mask2.raw");
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

        private static void PerformGraphCut(GridFlowGraph graph, ColorImage2D image, ColorImage2D match)
        {
            graph.Calculate();

            graph.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                {
                    image[x, y] = match[x, y];
                }
            });
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

            foreach (var p in sinkBounds.EnumerateBounds())
            {
                graph.SetSink(p.x, p.y, 255);
            }

            var sink = CreateMaskFromSink(graph);
            sink.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/sink.raw");

            return graph;
        }

        */

    }
}
