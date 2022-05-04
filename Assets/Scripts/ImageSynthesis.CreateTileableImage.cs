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
        public static Tile[] CreateTileableImages(ExemplarSet set, int sinkOffset, ThreadingToken token = null)
        {
            int count = set.Count;
            var tiles = new Tile[count];
 
            if (token != null)
            {
                token.EnqueueMessage("Stage 1 of 3");
                token.Steps = count;
            }
                
            ThreadingBlock1D.ParallelAction(count, 1, (i) =>
            {
                var exemplar = set[i];
                tiles[i] = CreateTileableImageStage1(exemplar.Tile);

            }, token);

            var exemplars = FindBestMatches(tiles, set);

            if (token != null)
            {
                token.EnqueueMessage("Stage 2 of 3");
                token.ResetProgress();
            }

            ThreadingBlock1D.ParallelAction(count, 1, (i) =>
            {
                var exemplar = exemplars[i];
                var tile = tiles[i];

                CreateTileableImageStage2(tile, exemplar, sinkOffset, true);

            }, token);

            exemplars = FindBestMatches(tiles, set);

            if (token != null)
            {
                token.EnqueueMessage("Stage 3 of 3");
                token.ResetProgress();
            }

            ThreadingBlock1D.ParallelAction(count, 1, (i) =>
            {
                var exemplar = exemplars[i];
                var tile = tiles[i];

                CreateTileableImageStage2(tile, exemplar, sinkOffset, false);

            }, token);

            return tiles;
        }

        private static Tile CreateTileableImageStage1(Tile tile)
        {
            int width = tile.Width;
            int height = tile.Height;

            var tileable = tile.Copy();
            tileable.Offset(width / 2, height / 2);

            foreach (var image in tileable.Images)
            {
                BlurOffsetSeams(image, 0.75f);
            }

            return tileable;
        }

        private static void CreateTileableImageStage2(Tile tile, Exemplar match, int sinkOffset, bool offset)
        {
            int width = tile.Width;
            int height = tile.Height;
            int sourceOffset = 2;

            var sinkBounds = new Box2i(sinkOffset, sinkOffset, width - 1 - sinkOffset, height - 1 - sinkOffset);

            var graph = CreateGraph(tile.Image, match.Tile.Image, null);
            MarkSourceAndSink(graph, sourceOffset, sinkBounds);

            graph.Calculate();
            var mask = CreateMaskFromGraph(graph, 5, 0.75f);
            BlendImages(graph, tile, match.Tile, mask);

            if(offset)
            {
                tile.Offset(width / 2, height / 2);
            }
        }

        private static Exemplar[] FindBestMatches(IList<Tile> tiles, ExemplarSet set)
        {
            var exemplars = new Exemplar[tiles.Count];

            for(int i = 0; i < tiles.Count; i++)
            {
                var image = tiles[i].Image;
                var exemplar = FindBestMatch(image, set, null);
                exemplar.IncrementUsed();

                exemplars[i] = exemplar;
            }

            return exemplars;
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

    }
}
