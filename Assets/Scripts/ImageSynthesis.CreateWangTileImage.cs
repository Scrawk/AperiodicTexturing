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

        public static void CreateWangTileImage(WangTileSet tileSet, IList<Tile> tileables, ExemplarSet exemplarSet, int sinkOffset, ThreadingToken token = null)
        {
            var tiles = tileSet.ToFlattenedList();

            if (token != null)
            {
                token.EnqueueMessage("Stage 1 of 2");
                token.Steps = tiles.Count;
            }
                
            int blockSize = ThreadingBlock1D.BlockSize(tiles.Count, 8);

            ThreadingBlock1D.ParallelAction(tiles.Count, blockSize, (i) =>
            {
                CreateWangTileImageStage1(tiles[i], tileables);

            }, token);

            var exemplars = FindBestMatches(tileSet, exemplarSet);

            if (token != null)
            {
                token.EnqueueMessage("Stage 2 of 2");
                token.ResetProgress();
            }

            ThreadingBlock1D.ParallelAction(tiles.Count, blockSize, (i) =>
            {
                var tile = tiles[i];
                int index = tile.Index1;
                var exemplar = exemplars[index];
                CreateWangTileImageStage2(tile, exemplar.Tile, sinkOffset);

            }, token);

        }

        private static void CreateWangTileImageStage1(WangTile tile, IList<Tile> tileables)
        {
            
            var colors = GetSortedColors(tile);
            var search = new GridFlowSearch(tile.Width, tile.Height);

            for (int i = 0; i < colors.Count; i++)
            {
                if(i == 0)
                {
                    int color = colors[i];
                    tile.Fill(tileables[color].Images);
                }
                else
                {
                    search.Clear();
                    int color = colors[i];
                    var tileable = tileables[color];

                    var map = CreateMap(tile);
                    var mask = CreateMask(map, color, 3);

                    var graph = CreateGraph(tile.Tile.Image, tileable.Image, mask, true);

                    MarkSourceAndSink(graph, color, map, mask);

                    graph.Calculate(search);
                    BlendImages(graph, tile.Tile, tileable, null);

                    BlurGraphCutSeams(tile.Tile.Image, graph, 2, 0.75f);
                }
            }

        }

        private static void CreateWangTileImageStage2(WangTile tile, Tile match, int sinkOffset)
        {
            int width = tile.Width;
            int height = tile.Height;
            int sourceOffset = 2;

            var sinkBounds = new Box2i(sinkOffset, sinkOffset, width - 1 - sinkOffset, height - 1 - sinkOffset);

            var graph = CreateGraph(tile.Tile.Image, match.Image, null, true);
            MarkSourceAndSink(graph, sourceOffset, sinkBounds);

            graph.Calculate();
            var blend = CreateMaskFromGraph(graph, 5, 0.75f);
            BlendImages(graph, tile.Tile.Image, match.Image, blend);
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

            foreach(var wtile in tileSet.Tiles)
            {
                Exemplar exemplar = FindBestMatch(wtile.Tile, set, null);
                exemplar.IncrementUsed();

                var index = wtile.Index1;
                exemplars[index] = exemplar;
            }

            return exemplars;
        }

        private static GreyScaleImage2D CreateMap(WangTile tile)
        {
            int width = tile.Width;
            int height = tile.Height;
            var map = new GreyScaleImage2D(width, height);

            if (tile.IsConst)
            {
                map.Fill(tile.Left);
            }
            else
            {
                var c00 = new Point2i(0, 0);
                var c01 = new Point2i(0, height);
                var c10 = new Point2i(width, 0);
                var c11 = new Point2i(width, height);
                var mid = new Point2i(width / 2, height / 2);

                map.DrawTriangle(mid, c00, c01, new ColorRGBA(tile.Left, 1), true);
                map.DrawTriangle(mid, c00, c10, new ColorRGBA(tile.Bottom, 1), true);
                map.DrawTriangle(mid, c10, c11, new ColorRGBA(tile.Right, 1), true);
                map.DrawTriangle(mid, c01, c11, new ColorRGBA(tile.Top, 1), true);
            }

            return map;
        }

        private static BinaryImage2D CreateMask(GreyScaleImage2D map, int color, int thickness)
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

            mask = BinaryImage2D.Dilate(mask, thickness);

            var bounds = mask.Bounds;
            var corners = bounds.GetCorners();

            foreach(var p in bounds.EnumeratePerimeter())
            {
                if (!corners.Contains(p))
                    mask[p.x, p.y] = false;
            }

            return mask;
        }

        private static void MarkSourceAndSink(GridFlowGraph graph, int color, GreyScaleImage2D map, BinaryImage2D mask)
        {
            graph.Iterate((x, y) =>
            {
                if (mask[x, y]) return;

                if (map[x, y] == color)
                    graph.SetLabelAndCapacity(x, y, FLOW_GRAPH_LABEL.SINK, 255);
                else
                    graph.SetLabelAndCapacity(x, y, FLOW_GRAPH_LABEL.SOURCE, 255);
            });
        }

        private static void BlurGraphCutSeams(WangTile tile, GridFlowGraph graph, float strength)
        {
            var image = tile.Tile.Image;
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
