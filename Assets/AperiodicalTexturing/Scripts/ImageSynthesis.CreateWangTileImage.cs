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
        /// <param name="wtileSet"></param>
        /// <param name="tileables"></param>
        /// <param name="patchSet"></param>
        /// <param name="seed"></param>
        /// <param name="token"></param>
        public static void CreateWangTileImage(WangTileSet wtileSet, IList<Tile> tileables, ExemplarSet patchSet, ExemplarSet exemplarSet, int seed, ThreadingToken token = null)
        {
            //Create a 1D array from the tiles 2D array.
            var wtiles = wtileSet.ToFlattenedList();

            //Create the histograms which are used to speed up finding the best matches.
            patchSet.CreateExemplarHistograms();

            //Name exemplars for debugging
            for (int i = 0; i < exemplarSet.Exemplars.Count; i++)
                exemplarSet.Exemplars[i].Name = "Exemplar" + i;

            //Create a set for each tile for thread safety.
            var sets = new ExemplarSet[wtiles.Count];
            for (int i = 0; i < sets.Length; i++)
                sets[i] = patchSet.Copy();

            //If a token is being used set how many steps there are.
            // Each step represents a tile being created on its own thread.
            if (token != null)
            {
                token.EnqueueMessage("Stage 1 of 2");
                token.Steps = wtiles.Count;
            }

            //Create 8 threads and divide the work load over them.
            int blockSize = ThreadingBlock1D.BlockSize(wtiles.Count, 8);
            ThreadingBlock1D.ParallelAction(wtiles.Count, blockSize, (i) =>
            {
                CreateWangTileImageStage1(wtiles[i], tileables, sets[i], seed + i);

            }, token);

            if (token != null)
            {
                token.ResetProgress();
                token.EnqueueMessage("Stage 2 of 2");
                token.Steps = wtiles.Count;
            }

            int exemplarSize = exemplarSet.ExemplarSize;
            int sourceOffset = 4;
            int sinkOffset = 32 + sourceOffset;

            //Create the mask where the images will be matched.
            //Any source or sink areas dont need to the matched only the spaces between them do.
            var mask = CreateMaskFromSourceAndSink(exemplarSize, sourceOffset, sinkOffset);

            //Find the best matchs for each tile.
            //Needed in stage 2 but cant be threaded.
            //Returning a null match is possible.
            var matchs = new Exemplar[wtiles.Count];
            for(int i = 0; i < wtiles.Count; i++)
            {
                matchs[i] = FindBestMatch(wtiles[i], exemplarSet, mask);
            }

            //Create 8 threads and divide the work load over them.
            blockSize = ThreadingBlock1D.BlockSize(wtiles.Count, 8);
            ThreadingBlock1D.ParallelAction(wtiles.Count, blockSize, (i) =>
            {
                var match = matchs[i];
                CreateWangTileImageStage2(wtiles[i], match, sourceOffset, sinkOffset, token);

            }, token);

        }

        /// <summary>
        /// Stage 1 will find where the different tiles meet and 
        /// patch the seems by sampling from the exemplar set.
        /// </summary>
        /// <param name="wtile">The wang tile to patch.</param>
        /// <param name="tileables">The tileable images used to fill the tiles.</param>
        /// <param name="set">The exemplars that contain the patches.</param>
        /// <param name="seed">A seed for the random generator.</param>
        private static void CreateWangTileImageStage1(WangTile wtile, IList<Tile> tileables, ExemplarSet set, int seed, ThreadingToken token = null)
        {
            
            var rng = new System.Random(seed);
            int numImages = tileables[0].Images.Count;
            int maskThickness = 0;

            //Create a new empty image for each tileable;
            var tile = wtile.Tile;
            tile.CreateImages(numImages);

            //Create a map the describes which pixels contain which tile.
            var map = CreateWangTiles_CreateMapFromWangTile(wtile);

            //Fill the images with the tileable they map to.
            for (int i = 0; i < numImages; i++)
                CreateWangTiles_FillFromMap(i, tile.Images[i], map, tileables);

            //Dont need to doing any more for tiles with one color.
            if (wtile.IsConst)
                return;

            //Get a list of all the edge colors in the tile and sort from lowest to highest.
            var colors = CreateWangTiles_GetSortedColors(wtile);

            for (int i = 0; i < colors.Count; i++)
            {
                int color = colors[i];

                //for each source image in set create a tileable image.
                for (int j = 0; j < tile.Images.Count; j++)
                {
                    if (token != null && token.Cancelled)
                        return;

                    var image = tile.Images[j];

                    //Create a mask that covers the seams where different tiles meet.
                    var mask = CreateWangTiles_CreateMask(map, color, maskThickness);

                    //Get a list of all the masked points in the mask.
                    //Randomize the points. This is the order the patchs will be filled.
                    var points = GetMaskedPoints(mask);
                    points.Shuffle(rng);

                    //Fill the areas of the image with random pixels that match the source image.
                    FillWithRandomFromExemplarSource(j, points, image, set, rng);

                    //Debug
                    //image.Fill(points, ColorRGBA.Black);

                    //Reset the used count
                    set.ResetUsedCount();

                    //Fill the patched areas of the image
                    CreateWangTiles_FillPatches(j, points, set, mask, image);
                }
            }

        }

        /// <summary>
        /// Finds the best match in the exemplar set.
        /// Can return null if none is found.
        /// </summary>
        /// <param name="wtile">The tile to find the match for.</param>
        /// <param name="exemplarSet">THe set of possible matches.</param>
        /// <returns></returns>
        private static Exemplar FindBestMatch(WangTile wtile, ExemplarSet exemplarSet, BinaryImage2D mask)
        {
            //Const tiles dont need to be changed so return null.
            if (wtile.IsConst)
                return null;

            //The amount in percentage the used exemplars costs are increased.
            float costModifier = 0.5f;

            var tile = wtile.Tile;
            var image = tile.Images[0];

            //Find the best match based on the first image in the tile which should be the albedo.
            var exemplar = FindBestMatch(0, image, exemplarSet, costModifier, mask);
            if (exemplar == null)
                return null;

            //Increment that the exemplar has been used.
            //This will stop the same exemplar being selected for every tile.
            exemplar.IncrementUsed();

            return exemplar;
        }

        /// <summary>
        /// Stage two will used the best matching exemplar and blend it with the tiles.
        /// The tiles borders will be preserved so the tile is still wang tileable
        /// but the repeating center contents should be unique for each tile.
        /// </summary>
        /// <param name="wtile">The wang tile.</param>
        /// <param name="exemplar">THe best matching exemplar based on the albedo.</param>
        /// <param name="token">THe threading token used to cancell.</param>
        private static void CreateWangTileImageStage2(WangTile wtile, Exemplar exemplar, int sourceOffset, int sinkOffset, ThreadingToken token = null)
        {
            //if exemplar is null or tile is const nothing to do.
            if (exemplar == null ||  wtile.IsConst)
                return;

            int exemplarSize = exemplar.ExemplarSize;
            int halfExemplarSize = exemplarSize / 2;
            int blendMaskDilation = 2;
            float blendMaskBlur = 0.75f;

            //Create a reusable search data structure 
            //to help reduce amount of garbage generated by the graph cut.
            var search = new GridFlowSearch(exemplarSize, exemplarSize);

            for (int j = 0; j < wtile.Tile.Images.Count; j++)
            {
                if (token != null && token.Cancelled)
                    return;

                var tile = wtile.Tile;
                var image = tile.Images[j];
                var match = exemplar.GetImageCopy(j);

                //Create the graph and mark the source/sing areas.
                var graph = CreateGraph(image, match, true);
                MarkSourceAndSink(graph, sourceOffset, sinkOffset);

                //Perform the graph cut. This is the slowest bit.
                search.Clear();
                graph.Calculate(search);

                //Create a mask based on the cut and blend the image and match together.
                var blend = CreateBlendMaskFromGraph(graph, blendMaskDilation, blendMaskBlur);
                CreateWangTiles_BlendImages(image, match, blend);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="points"></param>
        /// <param name="set"></param>
        /// <param name="mask"></param>
        /// <param name="image"></param>
        private static void CreateWangTiles_FillPatches(int index, List<Point2i> points, ExemplarSet set, BinaryImage2D mask, ColorImage2D image, ThreadingToken token = null)
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

            var search = new GridFlowSearch(exemplarSize, exemplarSize);

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

                //Skip any points that are to close to the edge.
                if (y < quaterExemplarSize || y > image.Height - quaterExemplarSize - 1)
                    continue;

                if (!mask[x, y]) continue;

                var box = new Box2i(x - halfExemplarSize, y - halfExemplarSize, x + halfExemplarSize, y + halfExemplarSize);
                var imageCrop = ColorImage2D.Crop(image, box, WRAP_MODE.WRAP);
                var maskCrop = ColorImage2D.Crop(mask, box, WRAP_MODE.WRAP);

                var exemplar = FindBestMatchWithHistograms(index, imageCrop, set, trimmedCostsSize, costModifer, maskCrop);
                if (exemplar == null) continue;

                exemplar.IncrementUsed();
                var match = exemplar.GetImageCopy(index);

                var graph = CreateGraph(imageCrop, match, false);
                MarkSourceAndSink(graph, sourceOffset, sinkOffset);

                search.Clear();
                graph.Calculate(search);

                var blend = CreateBlendMaskFromGraph(graph, blendMaskDilation, blendMaskBlur);

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
        /// <param name="imageIndex"></param>
        /// <param name="image"></param>
        /// <param name="map"></param>
        /// <param name="tileables"></param>
        private static void CreateWangTiles_FillFromMap(int imageIndex, ColorImage2D image, GreyScaleImage2D map, IList<Tile> tileables)
        {
            image.Fill((x, y) =>
            {
                int mapIndex = (int)map[x, y];
                return tileables[mapIndex].Images[imageIndex][x, y];
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="match"></param>
        /// <param name="blend"></param>
        private static void CreateWangTiles_BlendImages(ColorImage2D image, ColorImage2D match, GreyScaleImage2D blend)
        {
            image.Fill((x, y) =>
            {
                var p1 = image[x, y];
                var p2 = match[x, y];
                var a = blend[x, y];
;
                return ColorRGBA.Lerp(p1, p2, a);
            });

        }

    }
}
