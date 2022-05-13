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

        /// <summary>
        /// Creates a copy of each tile and makes all the tiles images tileable.
        /// </summary>
        /// <param name="tiles">The tiles to make tilable.</param>
        /// <param name="set">The exemplar set to sample from when filling patches.</param>
        /// <param name="seed"></param>
        /// <param name="token"></param>
        /// <returns>A array of tiles with tileable images.</returns>
        public static Tile[] CreateTileableImages(IList<Tile> tiles, ExemplarSet set, int seed, ThreadingToken token = null)
        {
            //For each tiles a new tileable tile will be created.
            int count = tiles.Count;
            var tileables = new Tile[count];

            //If a token is being used set how many steps there are.
            // Each step represents a tile being created on its own thread.
            if (token != null)
            {
                token.EnqueueMessage("Stage 1 of 1");
                token.Steps = count;
            }

            //For each tile run a run stage 1 on its own thread.
            ThreadingBlock1D.ParallelAction(count, 1, (i) =>
            {
                var rng = new System.Random(seed + i);
                tileables[i] = CreateTileableImage(i, tiles[i], set, rng);

            }, token);

            return tileables;
        }

        /// <summary>
        /// Create the tileable image by offsetting the image by half and 
        /// then patching the seams dividing the image.
        /// </summary>
        /// <param name="index">The tile index in the tiles array.</param>
        /// <param name="tile">The tile containing the images to make tileable.</param>
        /// <param name="set">The exemplar set used for patching.</param>
        /// <param name="rng">A random number generator.</param>
        /// <returns>A new tile where all its images are tileable.</returns>
        private static Tile CreateTileableImage(int index, Tile tile, ExemplarSet set, System.Random rng)
        {
            int width = tile.Width;
            int height = tile.Height;
            int seamsMaskThickness = 0;

            //Copy the tile and offset
            var tileable = tile.Copy();
            tileable.HalfOffset(true);

            //Create the lines that will cover the seems in the tile.
            var horzontal = new Segment2f(0, height / 2, width, height / 2);
            var vertical = new Segment2f(width / 2, 0, width / 2, height);

            //Create a mask that covers the seams in the tile.
            //This is the area that will be blurred.
            var mask = CreateTiles_CreateOffsetSeamsMask(width, height, horzontal, vertical, seamsMaskThickness);

            //Get a list of all the masked points in the mask.
            //Randomize the points. This is the order the patchs will be filled.
            var points = GetMaskedPoints(mask);
            points.Shuffle(rng);

            //Fill the areas of the image with random pixels that match the source image.
            //FillWithRandomFromExemplarSource(0, points, tileable.Image, set, rng);

            //Fill the areas of the image that need to be filled to black
            //For debugging so its easy to see if a area has been missed.
            tileable.Image.Fill(points, ColorRGBA.Black);

            //Patch any parts of the images that 
            CreateTiles_FillPatches(points, set, mask, tileable.Image);

            return tileable;
        }

        /// <summary>
        /// Patch any missing parts of the image from samples from the exemplar set.
        /// </summary>
        /// <param name="points">The pixel indices in the image that need to be filled.</param>
        /// <param name="set">The exemplar set to sample from.</param>
        /// <param name="mask">The mask determines if the pixels have been filled or not.</param>
        /// <param name="image">The image to patch.</param>
        private static void CreateTiles_FillPatches(List<Point2i> points, ExemplarSet set, BinaryImage2D mask, ColorImage2D image)
        {
            int exemplarSize = set.ExemplarSize;
            int halfExemplarSize = exemplarSize / 2;
            int sourceOffset = 2;
            int sinkOffset = halfExemplarSize - 4;
            int blendMaskDilation = 0;
            float blendMaskBlur = 0;

            //create a reusable search object for the graph cut.
            //Will help reduce the number garbage objects created.
            var search = new GridFlowSearch(exemplarSize, exemplarSize);

            int count = 0;

            foreach (var p in points)
            {
                int x = p.x;
                int y = p.y;

                //If already been fill continue.
                if (!mask[x, y]) continue;

                if (count++ > 0) return;

                //Create a box covering the area that needs to be filled.
                var box = new Box2i(x - halfExemplarSize, y - halfExemplarSize, x + halfExemplarSize, y + halfExemplarSize);

                //Cut out the area of the image and mask that need to be filled.
                var imageCrop = ColorImage2D.Crop(image, box, WRAP_MODE.WRAP);
                var maskCrop = ColorImage2D.Crop(mask, box, WRAP_MODE.WRAP);

                //Findd the exemplar in the set that best fits the cropped image.
                var match = FindBestMatch(imageCrop, set, maskCrop);

                //Create the cut that should best blend the
                //match and the crop image together.
                var graph = CreateGraph(imageCrop, match, true);
                MarkSourceAndSink(graph, sourceOffset, sinkOffset);

                //Create the cut.
                //Provide a reusable search object as a optimization. 
                search.Clear();
                graph.Calculate(search);

                //Create the mask used to blend the match and crop together.
                var blendMask = CreateMaskFromGraph(graph, blendMaskDilation, blendMaskBlur);

                //Blend the match and crop together.
                var blendedImage = BlendImages(graph, imageCrop, match, blendMask);

                //Fill the image with blended match
                image.Fill(blendedImage, box, WRAP_MODE.WRAP);

                //Update the areas of the mask that have been filled.
                CreateTiles_FillMask(box, mask, blendMask);

            };
        }

        /// <summary>
        /// Create the mask that covers the seams in the image and need to be filled.
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <param name="horzontal">A segment that covers the horzontal seam.</param>
        /// <param name="vertical">A segment that covers the vertical seam.</param>
        /// <param name="dilation">How much to dilate the seams.</param>
        /// <returns>A mask that contains true values for the areas that need to be patched.</returns>
        private static BinaryImage2D CreateTiles_CreateOffsetSeamsMask(int width, int height, Segment2f horzontal, Segment2f vertical, int dilation)
        {
            var binary = new BinaryImage2D(width, height);

            //Draw the segments into the mask. These areas will now be white.
            binary.DrawLine(horzontal, ColorRGBA.White);
            binary.DrawLine(vertical, ColorRGBA.White);

            //dilate the masked areas so they are thicker.
            if(dilation > 0)
                binary = BinaryImage2D.Dilate(binary, dilation);

            return binary;
        }

        /// <summary>
        /// Updates the mask by setting any areas of the mask that have been filled to false value.
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="mask"></param>
        /// <param name="blendMask"></param>
        private static void CreateTiles_FillMask(Box2i bounds, BinaryImage2D mask, GreyScaleImage2D blendMask)
        {
            for (int y = bounds.Min.y, j = 0; y < bounds.Max.y; y++, j++)
            {
                for (int x = bounds.Min.x, i = 0; x < bounds.Max.x; x++, i++)
                {
                    if (blendMask.GetValue(x, y, WRAP_MODE.WRAP) == 0)
                        continue;

                    mask.SetValue(i, j, false, WRAP_MODE.WRAP);
                }
            }
        }

    }

}
