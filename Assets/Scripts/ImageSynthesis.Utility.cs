using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

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

        private static void BlendImages(GridFlowGraph graph, ColorImage2D source, ColorImage2D sink, GreyScaleImage2D mask)
        {
            graph.Iterate((x, y) =>
            {
                if(mask == null)
                {
                    if (graph.IsSink(x, y))
                        source[x, y] = sink[x, y];
                }
                else
                {
                    float a = mask[x, y];
                    source[x, y] = ColorRGBA.Lerp(source[x, y], sink[x, y], a);
                }
            });

            //mask.SaveAsRaw("C:/Users/Justin/OneDrive/Desktop/mask" + i);
        }

        private static void BlendImages(GridFlowGraph graph, Tile source, Tile sink, GreyScaleImage2D mask)
        {
            graph.Iterate((x, y) =>
            {
                if (mask == null)
                {
                    if (graph.IsSink(x, y))
                    {
                        for(int i = 0; i < source.Count; i++)
                        {
                            var image1 = source.Images[i];
                            var image2 = sink.Images[i];
                            image1[x, y] = image2[x, y];
                        }
                    }
                }
                else
                {
                    float a = mask[x, y];

                    for (int i = 0; i < source.Count; i++)
                    {
                        var image1 = source.Images[i];
                        var image2 = sink.Images[i];
                        image1[x, y] = ColorRGBA.Lerp(image1[x, y], image2[x, y], a);
                    }
                }
            });
        }

        private static GridFlowGraph MarkSourceAndSink(GridFlowGraph graph, int sourceArea, Box2i sinkArea)
        {
            graph.Iterate((x, y) =>
            {
                if (x < sourceArea ||
                    y < sourceArea ||
                    x > graph.Width - 1 - sourceArea ||
                    y > graph.Height - 1 - sourceArea)
                {
                    graph.SetLabelAndCapacity(x, y, FLOW_GRAPH_LABEL.SOURCE, 255);
                }
            });

            foreach (var p in sinkArea.EnumerateBounds())
            {
                graph.SetLabelAndCapacity(p.x, p.y, FLOW_GRAPH_LABEL.SINK, 255);
            }

            return graph;
        }

        private static Exemplar FindBestMatch(Tile tile, ExemplarSet set, BinaryImage2D mask)
        {
            var costs = new float[set.Count];

            for(int i = 0; i < set.Count; i++)
            {
                costs[i] = float.PositiveInfinity;

                var exemplar = set[i];

                float cost = 0;
                int count = 0;
                float costModifier = (exemplar.Used + 1.0f) * 1.5f;

                for (int x = 0; x < exemplar.Width; x++)
                {
                    for (int y = 0; y < exemplar.Height; y++)
                    {
                        if (mask != null && !mask[x, y]) continue;

                        var exemplars_pixel = exemplar.Tile.Image[x, y];

                        for (int j = 0; j < tile.Count; j++)
                        {
                            var w = tile.GetWeight(j);
                            if (w <= 0) continue;

                            var tiles_pixel = tile.Images[j][x, y];
    
                            cost += ColorRGBA.SqrDistance(tiles_pixel, exemplars_pixel) * w;
                            count++;
                        }
                    }
                }

                if (count != 0)
                    costs[i] = (cost / count) * costModifier;
            }

            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;

            for (int i = 0; i < set.Count; i++)
            {
                if(costs[i] < bestCost)
                {
                    bestCost = costs[i];
                    bestMatch = set[i];
                }
            }

            return bestMatch;
        }

        private static void BlurGraphCutSeams(ColorImage2D image, GridFlowGraph graph, int thickness, float strength)
        {
            int width = image.Width;
            int height = image.Height;
            var binary = new BinaryImage2D(width, height);

            var points = graph.FindBoundaryPoints(true, true);
            binary.Fill(points, true);
            binary = BinaryImage2D.Dilate(binary, thickness);

            var mask = binary.ToGreyScaleImage();
            var blurred = ColorImage2D.GaussianBlur(image, strength, null, mask, WRAP_MODE.WRAP);
            image.Fill(blurred);
        }

        private static GreyScaleImage2D CreateMaskFromGraph(GridFlowGraph graph, int thickness, float strength)
        {
            var binary = new BinaryImage2D(graph.Width, graph.Height);

            var points = graph.FindBoundaryPoints(true, true);
            binary.Fill(points, true);
            binary = BinaryImage2D.Dilate(binary, thickness);

            var mask = BinaryImage2D.ApproxEuclideanDistance(binary, WRAP_MODE.WRAP);
            mask.Normalize();

            mask.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                    mask[x, y] = 1;
            });

            mask = GreyScaleImage2D.GaussianBlur(mask, strength, null, null, WRAP_MODE.WRAP);

            return mask;
        }

        private static ColorImage2D CreateImageFromGraph(GridFlowGraph graph, ColorRGBA source, ColorRGBA sink)
        {
            var image = new ColorImage2D(graph.Width, graph.Height);

            image.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                    image[x, y] = sink;
                else if (graph.IsSource(x, y))
                    image[x, y] = source;
            });

            return image;
        }

        private static GreyScaleImage2D CreateMaskFromGraph(GridFlowGraph graph, float source, float sink)
        {
            var mask = new GreyScaleImage2D(graph.Width, graph.Height);

            mask.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                    mask[x, y] = sink;
                else if (graph.IsSource(x, y))
                    mask[x, y] = source;
            });

            return mask;
        }

        private static GridFlowGraph CreateGraph(ColorImage2D image1, ColorImage2D image2, BinaryImage2D mask, bool isOrthogonal)
        {
            var graph = new GridFlowGraph(image1.Width, image1.Height, isOrthogonal);
 
            graph.Iterate((x, y) =>
            {
                if (mask != null && !mask[x, y]) return;

                var col1 = image1[x, y];
                var col2 = image2[x, y];

                var w1 = ColorRGBA.SqrDistance(col1, col2) * 255;

                foreach(var i in graph.EnumerateInBoundsDirections(x,y))
                {
                    var col1i = image1[i.x, i.y];
                    var col2i = image2[i.x, i.y];

                    var w2 = ColorRGBA.SqrDistance(col1i, col2i) * 255;

                    var w = MathUtil.Max(1, w1, w2);

                    graph.SetCapacity(x, y, i.z, w);
                }

            });

            return graph;
        }

    }
}
