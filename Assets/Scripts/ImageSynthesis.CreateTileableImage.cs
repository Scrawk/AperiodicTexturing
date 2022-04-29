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

namespace AperiodicTexturing
{
    public static partial class ImageSynthesis
    {

        public static ColorImage2D CreateTileableImage(ColorImage2D image, ExemplarSet set)
        {
            int width = image.Width;
            int height = image.Height;
            int sourceOffset = 2;
            int sinkOffset = 16;

            var tileable = ColorImage2D.Offset(image, width / 2, height / 2);

            BlurOffsetSeams(tileable, 0.75f);

            var exemplar = FindBestMatch(tileable, set, null);
            exemplar.IncrementUsed();

            var match = exemplar.Image;
            var sinkBounds = new Box2i(sinkOffset, sinkOffset, width - sinkOffset, height - sinkOffset);

            var graph = CreateGraph(tileable, match, null);
            MarkSourceAndSink(graph, sourceOffset, sinkBounds);

            graph.Calculate();
            var mask = CreateMaskFromGraph(graph, 5, 0.75f);
            BlendImages(graph, tileable, match, mask);

            tileable = ColorImage2D.Offset(tileable, width / 2, height / 2);

            return tileable;
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
