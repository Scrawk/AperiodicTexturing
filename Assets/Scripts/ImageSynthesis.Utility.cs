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
        private const string DEUB_FOLDER = "C:/Users/Justin/OneDrive/Desktop/";

        private static List<Point2i> GetMaskedPoints(BinaryImage2D mask)
        {
            var points = new List<Point2i>();
            mask.Iterate((x, y) =>
            {
                if (mask[x, y])
                {
                    points.Add(new Point2i(x, y));
                }
            });

            return points;
        }

        private static void FillWithRandomFromExemplarSource(int index, List<Point2i> points, ColorImage2D image, ExemplarSet set, System.Random rng)
        {
            foreach (var p in points)
            {
                var pixel = set.GetRandomSourcePixel(index, rng);
                image[p.x, p.y] = pixel;
            }
        }

        private static GreyScaleImage2D CreateMaskFromGraph(GridFlowGraph graph, int dilation, float strength)
        {
            var mask = new GreyScaleImage2D(graph.Width, graph.Height);

            mask.Iterate((x, y) =>
            {
                if (graph.IsSink(x, y))
                    mask[x, y] = 1;
            });

            if (dilation > 0)
                mask = GreyScaleImage2D.Dilate(mask, dilation);

            if (strength > 0)
                mask = GreyScaleImage2D.GaussianBlur(mask, strength, null, null, WRAP_MODE.WRAP);

            return mask;
        }

        private static ColorImage2D BlendImages(GridFlowGraph graph, ColorImage2D source, ColorImage2D sink, GreyScaleImage2D mask)
        {
            var image = new ColorImage2D(graph.Width, graph.Height);
            image.Fill(source);

            graph.Iterate((x, y) =>
            {
                if (mask == null)
                {
                    if (graph.IsSink(x, y))
                        image[x, y] = sink[x, y];
                }
                else
                {
                    float a = mask[x, y];
                    image[x, y] = ColorRGBA.Lerp(source[x, y], sink[x, y], a);
                }
            });

            return image;
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

        private static GridFlowGraph MarkSourceAndSink(GridFlowGraph graph, int sourceOffset, int sinkOffset)
        {

            graph.Iterate((x, y) =>
            {
                if (x < sourceOffset ||
                    y < sourceOffset ||
                    x > graph.Width - 1 - sourceOffset ||
                    y > graph.Height - 1 - sourceOffset)
                {
                    graph.SetLabelAndCapacity(x, y, FLOW_GRAPH_LABEL.SOURCE, 255);
                }
                else if(x > sinkOffset && x < graph.Width - sinkOffset - 1 && 
                        y > sinkOffset && y < graph.Height - sinkOffset - 1)
                {
                    graph.SetLabelAndCapacity(x, y, FLOW_GRAPH_LABEL.SINK, 255);
                }
            });

            return graph;
        }

        private static GridFlowGraph CreateGraph(ColorImage2D image1, ColorImage2D image2, bool isOrthogonal)
        {
            var graph = new GridFlowGraph(image1.Width, image1.Height, isOrthogonal);
 
            graph.Iterate((x, y) =>
            {
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

        private static ColorImage2D FindBestMatch(ColorImage2D image, ExemplarSet set, BinaryImage2D mask = null)
        {

            var costs = new Tuple<float, Exemplar>[set.ExemplarCount];

            for (int k = 0; k < costs.Length; k++)
            {
                var exemplar = set[k];

                if (exemplar == null)
                {
                    costs[k] = new Tuple<float, Exemplar>(float.PositiveInfinity, null);
                    continue;
                }

                //float cost = image.SqrDistance(exemplar.Tile.Image);
                //costs[k] = new Tuple<float, Exemplar>(cost, exemplar);

                float cost = 0;
                int count = 0;

                for (int j = 0; j < exemplar.Height; j++)
                {
                    for (int i = 0; i < exemplar.Width; i++)
                    {
                        if (mask != null && mask[i, j]) continue;

                        var exemplars_pixel = exemplar.Tile.Image[i, j];
                        var tiles_pixel = image[i, j];

                        count++;
                        cost += ColorRGBA.SqrDistance(exemplars_pixel, tiles_pixel);
                    }
                }

                if (count != 0)
                    cost = cost / count;
                else
                    cost = float.PositiveInfinity;

                costs[k] = new Tuple<float, Exemplar>(cost, exemplar);
            }

            Array.Sort(costs, (x, y) => x.Item1.CompareTo(y.Item1));

            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;

            for (int i = 0; i < costs.Length; i++)
            {
                if (costs[i].Item1 == float.PositiveInfinity) continue;
                if (costs[i].Item2 == null) continue;

                if (costs[i].Item1 < bestCost)
                {
                    bestCost = costs[i].Item1;
                    bestMatch = costs[i].Item2;
                }
            }

            if (bestMatch == null)
                return null;
            else
                return bestMatch.Tile.Image;
        }

    }
}
