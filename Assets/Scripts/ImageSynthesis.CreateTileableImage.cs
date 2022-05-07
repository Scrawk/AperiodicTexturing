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
        /// For each tile in tiles array make the tile tileable.
        /// </summary>
        /// <param name="tiles">The array of tiles to make tileable.</param>
        /// <param name="set">The exemplar set that contains images that can 
        /// be used for filling patchs in the tiles if needed. </param>
        /// <param name="sinkOffset">Determines the patched area of the tiles. 
        /// A larger value will result in slower build times.</param>
        /// <param name="token">A threading token used to traking progress, 
        /// cancelling and sending messages to main thread.</param>
        /// <returns></returns>
        public static Tile[] CreateTileableImages(IList<Tile> tiles, ExemplarSet set, int sinkOffset, ThreadingToken token = null)
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
                tileables[i] = CreateTileableImageStage1(tiles[i]);
            }, token);

            //Find the best in the exemplar set for each tile
            var matches = FindBestMatches(tileables, set, sinkOffset);

            //Reset progress and start stage 2.
            if (token != null)
            {
                token.EnqueueMessage("Stage 2 of 3");
                token.ResetProgress();
            }

            //For each tile run a run stage 2 on its own thread.
            ThreadingBlock1D.ParallelAction(count, 1, (i) =>
            {
                var match = matches[i];
                var tile = tileables[i];

                CreateTileableImageStage2(tile, match, sinkOffset, true);

            }, token);

            //From here we are repeating stage 2.
            //This is optional but we result in better tiles.
            //If stage 2 is run only once then the tiles can
            //still have visible seems where they were offset.

            //Find the best in the exemplar set for each tile
            matches = FindBestMatches(tileables, set, sinkOffset);

            //Reset progress and start stage 2 a seconf time.
            if (token != null)
            {
                token.EnqueueMessage("Stage 3 of 3");
                token.ResetProgress();
            }

            //For each tile run a run stage 2 a second time on its own thread.
            ThreadingBlock1D.ParallelAction(count, 1, (i) =>
            {
                var match = matches[i];
                var tile = tileables[i];

                CreateTileableImageStage2(tile, match, sinkOffset, false);

            }, token);

            //Return the tiles that all should now be tileable.
            return tileables;
        }

        /// <summary>
        /// Stage 1 will offset each tilesome the edges are now tileable
        /// but the seem will now run through the center of the tiles.
        /// The seem is blurred to hide it a little and will be fixed
        /// in stage 2.
        /// </summary>
        /// <param name="tile">The tile to make tileable.</param>
        /// <returns></returns>
        private static Tile CreateTileableImageStage1(Tile tile)
        {
            int width = tile.Width;
            int height = tile.Height;
            int xoffset = width / 2;
            int yoffset = height / 2;
            int thickness = 5;

            //Copy the tile and offset
            var tileable = tile.Copy();
            tileable.Offset(xoffset, yoffset);

            //Create the lines that will cover the seems in the tile.
            var horzontal = new Segment2f(0, height / 2, width, height / 2);
            var vertical = new Segment2f(width / 2, 0, width / 2, height);

            //Create a mask that covers the seem in the tile.
            //This is the area that will be blurred.
            var mask = CreateBlurOffsetSeamsMask(width, height, horzontal, vertical, thickness);

            //Blur the seems in the tile.
            tile.Blur(mask, null, 0.75f, WRAP_MODE.WRAP);

            return tileable;
        }

        /// <summary>
        /// Stage 2 will blend the tile with another so the original tile 
        /// will remain at the edges but the center area that contains the seems 
        /// ver by the best matching tile. The blend is creaed by using a graph cut.
        /// </summary>
        /// <param name="tile">The tile to make tieable.</param>
        /// <param name="match">The best matching tile that will be blended with the other tile.</param>
        /// <param name="sinkOffset">How far to offset from the tiles edges to markas the sink area.</param>
        /// <param name="offset">Should the tile be offset.</param>
        private static void CreateTileableImageStage2(Tile tile, Tile match, int sinkOffset, bool offset)
        {
            int width = tile.Width;
            int height = tile.Height;

            //The source area is the thickness of a border around
            //the tiles edges that will be marked as the source.
            //This area will be left unchanged.
            int sourceOffset = 2;

            //The sink area will be a square in the center
            //and will be changed to the to the matches contents.
            //There will be a gap between the source and sink areas
            //and this is where the graph cut will go through and the tiles blended together.
            var sinkBounds = new Box2i(sinkOffset, sinkOffset, width - 1 - sinkOffset, height - 1 - sinkOffset);

            for(int i = 0; i < tile.Count; i++)
            {
                var tileImage = tile.Images[i];
                var matchImage = match.Images[i];

                //Create the graph and mark what areas are the source and sink.
                var graph = CreateGraph(tileImage, matchImage, null, true);
                MarkSourceAndSink(graph, sourceOffset, sinkBounds);

                //Calculate the graph cut. This is the slow part.
                graph.Calculate();

                //Create a mask from the graph cut and use it to blend the tile and match together.
                var mask = CreateMaskFromGraph(graph, 5, 0.75f);
                BlendImages(graph, tileImage, matchImage, mask);
            }

            //If required offset the tile.
            if(offset)
                tile.Offset(width / 2, height / 2);

        }

        /// <summary>
        /// For each tile find the tile in the exemplar set that 
        /// best matchs it.
        /// </summary>
        /// <param name="tiles">The tiles to find a match for.</param>
        /// <param name="set">The exemplar set to find the match</param>
        /// <returns></returns>
        private static Tile[] FindBestMatches(IList<Tile> tiles, ExemplarSet set, int sinkOffset)
        {

            int width = tiles[0].Width;
            int height = tiles[0].Height;

            //Create a mask that is false where the sink is (a square area in middle of tile) and true
            //where the source and unlabeled areas are (A border around the edges). This will
            //restrict the matching to the pixels in the graph the cut will go through.
            var sinkBounds = new Box2i(sinkOffset, sinkOffset, width - 1 - sinkOffset, height - 1 - sinkOffset);
            var mask = new BinaryImage2D(width, height, true);
            mask.DrawBox(sinkBounds, ColorRGBA.Black, true);

            var matches = new Tile[tiles.Count];

            for(int i = 0; i < tiles.Count; i++)
            {
                var exemplar = FindBestMatch(tiles[i], set, null);

                if (exemplar == null)
                    throw new NullReferenceException("Exemplar is null");

                //Increment the exemplar as being used.
                //Used tiles will have a greater cost and will 
                //reduce the chance of the same tile being used multiple times.
                exemplar.IncrementUsed();

                matches[i] = exemplar.Tile;
            }

            return matches;
        }

        /// <summary>
        /// Creates a mask in the areas the segments cover.
        /// </summary>
        /// <param name="width">The width of the mask.</param>
        /// <param name="height">The height of the masks.</param>
        /// <param name="horzontal">A segment covering were the mask is to be created.</param>
        /// <param name="vertical">A segment covering were the mask is to be created.</param>
        /// <param name="thickness">The thickness of the segmens in the mask.</param>
        /// <returns></returns>
        private static GreyScaleImage2D CreateBlurOffsetSeamsMask(int width, int height, Segment2f horzontal, Segment2f vertical, int thickness)
        {
            var mask = new GreyScaleImage2D(width, height);

            //Draw the segments into the mask. These areas will now be white.
            mask.DrawLine(horzontal, ColorRGBA.White);
            mask.DrawLine(vertical, ColorRGBA.White);

            //dilate the masked areas so they are thicker.
            mask = GreyScaleImage2D.Dilate(mask, thickness);

            return mask;
        }

    }
}
