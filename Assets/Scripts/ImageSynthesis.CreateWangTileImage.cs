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

namespace AperiodicTexturing
{
    public static partial class ImageSynthesis
    {

        public static void CreateWangTileImage(WangTile tile, IList<ColorImage2D> tileables)
        {
            var map = CreateMap(tile);

            var colors = new List<int>();

            foreach(var color in tile.Edges)
            {
                if (!colors.Contains(color))
                    colors.Add(color);
            }

            colors.Sort();

            for(int i = 0; i < colors.Count; i++)
            {
                if(i == 0)
                {
                    int color = colors[i];
                    tile.Image.Fill(tileables[color]);
                }
                else
                {
                    int color = colors[i];

                    var tileable = tileables[color];

                    var mask = CreateMask(map, color);

                    var graph = CreateGraph(map, mask, tile.Image, tileable);

                    MarkSourceAndSink(graph, color, map, mask);

                    graph.Calculate();

                    tile.Image.Iterate((x, y) =>
                    {
                        if (graph.IsSink(x, y))
                            tile.Image[x, y] = tileable[x, y];
                    });

                    BlurGraphCutSeams(tile, graph, 0.75f);
                }
            }

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

        private static BinaryImage2D CreateMask(GreyScaleImage2D map, int color)
        {
            var mask = new BinaryImage2D(map.Size);

            mask.Iterate((x, y) =>
            {
                if (map[x, y] != color) return;

                for (int i = 0; i < 8; i++)
                {
                    int xi = x + D8.OFFSETS[i, 0];
                    int yi = y + D8.OFFSETS[i, 1];

                    if (mask.NotInBounds(xi, yi)) continue;

                    if (map[xi, yi] != color)
                    {
                        mask[x, y] = true;
                        break;
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

        private static GridFlowGraph CreateGraph(GreyScaleImage2D map, BinaryImage2D mask, ColorImage2D image1, ColorImage2D image2)
        {
            var graph = new GridFlowGraph(map.Width, map.Height);

            graph.Iterate((x, y) =>
            {
                if (mask != null && !mask[x, y]) return;

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

        private static void MarkSourceAndSink(GridFlowGraph graph, int color, GreyScaleImage2D map, BinaryImage2D mask)
        {
            graph.Iterate((x, y) =>
            {
                if (mask[x, y]) return;

                if (map[x, y] == color)
                    graph.SetSink(x, y, 255);
                else
                    graph.SetSource(x, y, 255);
            });
        }

        private static void BlurGraphCutSeams(WangTile tile, GridFlowGraph graph, float strength)
        {
            var image = tile.Image;
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

    }
}
