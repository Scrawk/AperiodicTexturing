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
using ImageProcessing.Statistics;

namespace AperiodicTexturing
{
    public static partial class ImageSynthesis
    {
        private const string DEUB_FOLDER = "C:/Users/Justin/OneDrive/Desktop/";

        /// <summary>
        /// 
        /// </summary>
        /// <param name="mask"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="points"></param>
        /// <param name="image"></param>
        /// <param name="set"></param>
        /// <param name="rng"></param>
        private static void FillWithRandomFromExemplarSource(int index, List<Point2i> points, ColorImage2D image, ExemplarSet set, System.Random rng)
        {
            foreach (var p in points)
            {
                var pixel = set.GetRandomSourcePixel(index, rng);
                image[p.x, p.y] = pixel;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="dilation"></param>
        /// <param name="strength"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="graph"></param>
        /// <param name="sourceOffset"></param>
        /// <param name="sinkOffset"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image1"></param>
        /// <param name="image2"></param>
        /// <param name="isOrthogonal"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <param name="set"></param>
        /// <param name="mask"></param>
        /// <returns></returns>
        private static Exemplar FindBestMatch(ColorImage2D image, ExemplarSet set, BinaryImage2D mask = null)
        {

            var histo = new ColorHistogram(image, 256);
            var costs = new Tuple<float, Exemplar>[set.ExemplarCount];

            for (int k = 0; k < costs.Length; k++)
            {
                var exemplar = set[k];

                float cost = exemplar.SqrDistance(0, histo);

                float modifier = 1.0f + exemplar.Used * 0.25f;
                cost *= modifier;

                costs[k] = new Tuple<float, Exemplar>(cost, exemplar);
            }

            Array.Sort(costs, (x, y) => x.Item1.CompareTo(y.Item1));

            int trimmed_costs_len = Math.Min(100, costs.Length);
            var trimmed_costs = new Tuple<float, Exemplar>[trimmed_costs_len];

            for (int k = 0; k < trimmed_costs_len; k++)
            {
                var exemplar = costs[k].Item2;

                float cost = 0;
                int count = 0;

                for (int j = 0; j < exemplar.ExemplarSize; j++)
                {
                    for (int i = 0; i < exemplar.ExemplarSize; i++)
                    {
                        if (mask != null && mask[i, j]) continue;

                        var exemplars_pixel = exemplar.GetPixel(0, i, j);
                        var tiles_pixel = image[i, j];

                        count++;
                        cost += ColorRGBA.SqrDistance(exemplars_pixel, tiles_pixel);
                    }
                }

                float modifier = 1.0f + exemplar.Used * 0.25f;
                cost *= modifier;

                if (count != 0)
                    cost = (cost / count) * modifier;
                else
                    cost = float.PositiveInfinity;

                trimmed_costs[k] = new Tuple<float, Exemplar>(cost, exemplar);
            }

            Array.Sort(trimmed_costs, (x, y) => x.Item1.CompareTo(y.Item1));

            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;

            for (int i = 0; i < trimmed_costs.Length; i++)
            {
                if (trimmed_costs[i].Item1 == float.PositiveInfinity) continue;
                if (trimmed_costs[i].Item2 == null) continue;

                if (trimmed_costs[i].Item1 < bestCost)
                {
                    bestCost = trimmed_costs[i].Item1;
                    bestMatch = trimmed_costs[i].Item2;
                }
            }

            return bestMatch;
        }

    }
}
