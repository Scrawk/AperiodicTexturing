using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using UnityEngine;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Shapes;
using Common.Core.Directions;
using Common.Core.Extensions;
using Common.Core.Threading;
using Common.GraphTheory.GridGraphs;

using ImageProcessing.Images;

namespace AperiodicTexturing
{

    public static partial class ImageSynthesis
    {

        public static void CreateWangTileImage(WangTileSet tileSet, IList<ColorImage2D> tileables, ExemplarSet exemplarSet, ThreadingToken token = null)
        {
            var tiles = tileSet.ToFlattenedList();

            if (token != null)
                token.Steps = tiles.Count * 2;

            int blockSize = ThreadingBlock1D.BlockSize(tiles.Count, 8);

            ThreadingBlock1D.ParallelAction(tileSet.NumTiles, blockSize, (i) =>
            {
                CreateWangTileImageStage1(tiles[i], tileables);

            }, token);

            var exemplars = FindBestMatches(tileSet, exemplarSet);

            ThreadingBlock1D.ParallelAction(tileSet.NumTiles, blockSize, (i) =>
            {
                var tile = tiles[i];
                int index = tile.Index1;
                var exemplar = exemplars[index];
                CreateWangTileImageStage2(tile, exemplar.Image);

            }, token);

            /*
            if (token != null && token.UseThreading)
            {
                var tiles = new List<WangTile>(tileSet.Tiles.Length);
                foreach (var tile in tileSet.Tiles)
                    tiles.Add(tile);

                Parallel.ForEach(tiles, tile =>
                {
                    if (token.Cancelled) return;

                    CreateWangTileImageStage1(tile, tileables);
                    token.IncrementProgess();
                });

                var exemplars = FindBestMatches(tileSet, exemplarSet);

                Parallel.ForEach(tiles, tile =>
                {
                    if (token.Cancelled) return;

                    int index = tile.Index1;
                    var exemplar = exemplars[index];
                    CreateWangTileImageStage2(tile, exemplar.Image);
                    token.IncrementProgess();
                });
            }
            else
            {
                foreach (var tile in tileSet.Tiles)
                {
                    if (token.Cancelled) return;

                    CreateWangTileImageStage1(tile, tileables);
                    token.IncrementProgess();
                }

                var exemplars = FindBestMatches(tileSet, exemplarSet);

                foreach (var tile in tileSet.Tiles)
                {
                    if (token.Cancelled) return;

                    int index = tile.Index1;
                    var exemplar = exemplars[index];
                    CreateWangTileImageStage2(tile, exemplar.Image);
                    token.IncrementProgess();
                }
            }
            */

        }

        private static void CreateWangTileImageStage1(WangTile tile, IList<ColorImage2D> tileables)
        {
            
            var colors = GetSortedColors(tile);

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
                    var map = CreateMap(tile);

                    var mask = CreateMask(map, color);
                    var graph = CreateGraph(tile.Image, tileable, mask);

                    MarkSourceAndSink(graph, color, map, mask);

                    graph.Calculate();
                    BlendImages(graph, tile.Image, tileable, null);

                    BlurGraphCutSeams(tile.Image, graph, 2, 0.75f);
                }
            }

        }

        private static void CreateWangTileImageStage2(WangTile tile, ColorImage2D match)
        {
            int size = tile.TileSize;
            int sourceOffset = 2;
            int sinkOffset = 16;

            var sinkBounds = new Box2i(sinkOffset, sinkOffset, size - sinkOffset, size - sinkOffset);

            var graph = CreateGraph(tile.Image, match, null);
            MarkSourceAndSink(graph, sourceOffset, sinkBounds);

            graph.Calculate();
            var blend = CreateMaskFromGraph(graph, 5, 0.75f);
            BlendImages(graph, tile.Image, match, blend);
        }

        private static List<int> GetSortedColors(WangTile tile)
        {
            var colors = new List<int>();

            foreach (var color in tile.Edges)
            {
                if (!colors.Contains(color))
                    colors.Add(color);
            }

            colors.Sort();

            return colors;
        }

        private static Exemplar[] FindBestMatches(WangTileSet tileSet, ExemplarSet set)
        {
            var exemplars = new Exemplar[tileSet.NumTiles];

            foreach(var tile in tileSet.Tiles)
            {
                Exemplar exemplar = FindBestMatch(tile.Image, set, null);
                exemplar.IncrementUsed();

                var index = tile.Index1;
                exemplars[index] = exemplar;
            }

            return exemplars;
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
