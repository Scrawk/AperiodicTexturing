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
        /// <summary>
        /// 
        /// </summary>
        /// <param name="tileSet"></param>
        /// <param name="tileables"></param>
        /// <param name="exemplarSet"></param>
        /// <param name="seed"></param>
        /// <param name="token"></param>
        public static void CreateWangTileImage(WangTileSet tileSet, IList<Tile> tileables, ExemplarSet exemplarSet, int seed, ThreadingToken token = null)
        {
            var tiles = tileSet.ToFlattenedList();

            var sets = new ExemplarSet[tiles.Count];
            exemplarSet.CreateExemplarHistograms();

            //Create a set for each tile for thread safety.
            for (int i = 0; i < sets.Length; i++)
                sets[i] = exemplarSet.Copy();

            //If a token is being used set how many steps there are.
            // Each step represents a tile being created on its own thread.
            if (token != null)
            {
                token.EnqueueMessage("Stage 1 of 1");
                token.Steps = tiles.Count;
            }
            
            //Create 8 threads and divide the work load over them.
            int blockSize = ThreadingBlock1D.BlockSize(tiles.Count, 8);
            ThreadingBlock1D.ParallelAction(tiles.Count, blockSize, (i) =>
            {
                CreateWangTileImage(tiles[i], tileables, sets[i], seed + i);

            }, token);

        }

        /// <summary>
        /// Patch each tile in the wang tile set.
        /// </summary>
        /// <param name="wtile">The wang tile to patch.</param>
        /// <param name="tileables">The tileable images used to fill the tiles.</param>
        /// <param name="set">The exemplars that contain the patches.</param>
        /// <param name="seed">A seed for the random generator.</param>
        private static void CreateWangTileImage(WangTile wtile, IList<Tile> tileables, ExemplarSet set, int seed, ThreadingToken token = null)
        {
            var colors = CreateWangTiles_GetSortedColors(wtile);
            var rng = new System.Random(seed);

            int maskThickness = 0;

            for (int i = 0; i < colors.Count; i++)
            {
                if (token != null && token.Cancelled)
                    return;

                if(i == 0)
                {
                    //If this is the first tile it just consists of one image.
                    //Nothing to blend, just fill image.
                    int color = colors[i];
                    wtile.Fill(tileables[color].Images);
                }
                else
                {
                    //for each source image in set create a tileable image.
                    for (int j = 0; j < set.SourceCount; j++)
                    {
                        int color = colors[i];
                        var tile = wtile.Tile;
                        var image = tile.Images[j];

                        //Create a map the describes which pixels contain which tile.
                        var map = CreateWangTiles_CreateMapFromWangTile(wtile);

                        //Create a mask that covers the seams where different tiles meet.
                        var mask = CreateWangTiles_CreateMask(map, color, maskThickness);

                        //Fill the tiles based on the map.
                        CreateWangTiles_FillFromMap(j, map, image, tileables);

                        //Get a list of all the masked points in the mask.
                        //Randomize the points. This is the order the patchs will be filled.
                        var points = GetMaskedPoints(mask);
                        points.Shuffle(rng);

                        //Fill the areas of the image with random pixels that match the source image.
                        FillWithRandomFromExemplarSource(j, points, image, set, rng);

                        //Reset the used count
                        set.ResetUsedCount();

                        //Fill the patched areas of the image
                        CreateWangTiles_FillPatches(points, set, mask, image);
                    }

                }
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <param name="set"></param>
        /// <param name="mask"></param>
        /// <param name="image"></param>
        private static void CreateWangTiles_FillPatches(List<Point2i> points, ExemplarSet set, BinaryImage2D mask, ColorImage2D image, ThreadingToken token = null)
        {
            int exemplarSize = set.ExemplarSize;
            int halfExemplarSize = exemplarSize / 2;
            int quaterExemplarSize = exemplarSize / 4;
            int sourceOffset = 2;
            int sinkOffset = halfExemplarSize - 4;
            int blendMaskDilation = 2;
            float blendMaskBlur = 0.5f;
            int trimmedCostsSize = 100;
            float costModifer = 0.25f;

            foreach (var p in points)
            {
                //Check if cancelled.
                if (token != null && token.Cancelled)
                    return;

                int x = p.x;
                int y = p.y;

                //Skip any points that are to close to the edge.
                if (x < quaterExemplarSize || x > image.Width - quaterExemplarSize - 1)
                    continue;

                if (y < quaterExemplarSize || y > image.Height - quaterExemplarSize - 1)
                    continue;

                if (!mask[x, y]) continue;

                var box = new Box2i(x - halfExemplarSize, y - halfExemplarSize, x + halfExemplarSize, y + halfExemplarSize);
                var imageCrop = ColorImage2D.Crop(image, box, WRAP_MODE.WRAP);
                var maskCrop = ColorImage2D.Crop(mask, box, WRAP_MODE.WRAP);

                var exemplar = FindBestMatch(0, imageCrop, set, trimmedCostsSize, costModifer, maskCrop);
                if (exemplar == null) continue;

                exemplar.IncrementUsed();
                var match = exemplar.GetImageCopy(0);

                var graph = CreateGraph(image, match, false);
                MarkSourceAndSink(graph, sourceOffset, sinkOffset);
                graph.Calculate();

                var blend = CreateMaskFromGraph(graph, blendMaskDilation, blendMaskBlur);

                CreateWangTiles_UpdateImageAndMask(box, image, imageCrop, match, mask, blend);

            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        private static List<int> CreateWangTiles_GetSortedColors(WangTile tile)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tile"></param>
        /// <returns></returns>
        private static GreyScaleImage2D CreateWangTiles_CreateMapFromWangTile(WangTile tile)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <param name="image"></param>
        /// <param name="tileables"></param>
        private static void CreateWangTiles_FillFromMap(int index, GreyScaleImage2D map, ColorImage2D image, IList<Tile> tileables)
        {
            image.Fill((x, y) =>
            {
                int index = (int)map[x, y];
                return tileables[index].Images[index][x, y];
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="map"></param>
        /// <param name="color"></param>
        /// <param name="thickness"></param>
        /// <returns></returns>
        private static BinaryImage2D CreateWangTiles_CreateMask(GreyScaleImage2D map, int color, int thickness)
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

            if(thickness > 0)
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="image"></param>
        /// <param name="crop"></param>
        /// <param name="match"></param>
        /// <param name="mask"></param>
        /// <param name="blend"></param>
        private static void CreateWangTiles_UpdateImageAndMask(Box2i bounds, ColorImage2D image, ColorImage2D crop, ColorImage2D match, BinaryImage2D mask, GreyScaleImage2D blend)
        {
            for (int y = bounds.Min.y, j = 0; y < bounds.Max.y; y++, j++)
            {
                for (int x = bounds.Min.x, i = 0; x < bounds.Max.x; x++, i++)
                {
                    var p1 = crop[i, j];
                    var p2 = match[i, j];
                    var a = blend[i, j];

                    if (mask[i, j] || a >= 1)
                    {
                        mask.SetValue(x, y, false, WRAP_MODE.WRAP);
                        image.SetPixel(x, y, p2, WRAP_MODE.WRAP, BLEND_MODE.NONE);
                    }
                    else if (a > 0)
                    {
                        var p = ColorRGBA.Lerp(p1, p2, a);

                        mask.SetValue(x, y, false, WRAP_MODE.WRAP);
                        image.SetPixel(x, y, p, WRAP_MODE.WRAP, BLEND_MODE.NONE);
                    }

                }
            }
        }

    }
}
