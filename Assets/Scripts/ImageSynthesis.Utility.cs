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
        /// Create a list of points for each pixel in the mask that is set to true.
        /// </summary>
        /// <param name="mask">The mask to create the points from.</param>
        /// <returns>A list of points.</returns>
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
        /// For each point in the list sample the source 
        /// images in the exemplar set and choose a pixel at random.
        /// </summary>
        /// <param name="index">The index of the source image in the set.</param>
        /// <param name="points">The points to fill.</param>
        /// <param name="image">The image to fill.</param>
        /// <param name="set">The exemplar set.</param>
        /// <param name="rng">A random generator.</param>
        private static void FillWithRandomFromExemplarSource(int index, List<Point2i> points, ColorImage2D image, ExemplarSet set, System.Random rng)
        {
            foreach (var p in points)
            {
                var pixel = set.GetRandomSourcePixel(index, rng);
                image[p.x, p.y] = pixel;
            }
        }

        /// <summary>
        /// Creates a mask from a graph where the sink vertices are set to 1.
        /// The mask can then be dilated and blurred if needed.
        /// </summary>
        /// <param name="graph">The graph.</param>
        /// <param name="dilation">The amount to dilate.</param>
        /// <param name="strength">The amount to blur.</param>
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
        /// Marks the sink and source areas of the graph. 
        /// </summary>
        /// <param name="graph">The graph.</param>
        /// <param name="sourceOffset">The border thickness around edge of graph that will be marked as the source. </param>
        /// <param name="sinkOffset">The area in center of graph that will be marked as the sink.</param>
        private static void MarkSourceAndSink(GridFlowGraph graph, int sourceOffset, int sinkOffset)
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
        }

        /// <summary>
        /// Creates a graph from the images and sets the weights to the sqr distance between the edges.
        /// </summary>
        /// <param name="image1"></param>
        /// <param name="image2"></param>
        /// <param name="isOrthogonal">Is the grapgh othogonal, ie no diagonal edges.</param>
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
        /// Finds the exemplar from the set that best matches the image.
        /// Matchs by histograms and then by the pixels in the image.
        /// </summary>
        /// <param name="image">The image to try and match.</param>
        /// <param name="set">The exemplar set.</param>
        /// <param name="timmedCostsSize">The number of images to compare.</param>
        /// <param name="costModifer">The extra cost for exemplars that have been used before (percentage 0-1).</param>
        /// <param name="mask">A mask to control what pixels are compared.</param>
        /// <returns></returns>
        private static Exemplar FindBestMatch(int index, ColorImage2D image, ExemplarSet set, int timmedCostsSize, float costModifer, BinaryImage2D mask = null)
        {
            //Create a the images histogram.
            var histo = new ColorHistogram(image, 256);
            //Create a array costs.
            var costs = new Tuple<float, Exemplar>[set.ExemplarCount];

            costModifer = MathUtil.Clamp01(costModifer);

            //For each exemplar in the set find its cost to the images histogram.
            for (int k = 0; k < costs.Length; k++)
            {
                var exemplar = set[k];
                float cost = exemplar.SqrDistance(index, histo);

                //If a exemplar has  been used before apply a cost modifer.
                float modifier = 1.0f + exemplar.Used * costModifer;
                cost *= modifier;

                costs[k] = new Tuple<float, Exemplar>(cost, exemplar);
            }

            //Sort the costs
            Array.Sort(costs, (x, y) => x.Item1.CompareTo(y.Item1));

            //Take the best matchs
            int trimmed_costs_len = Math.Min(timmedCostsSize, costs.Length);
            var trimmed_costs = new Tuple<float, Exemplar>[trimmed_costs_len];

            //Now compare the full images for the best matchs
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

                        var exemplars_pixel = exemplar.GetPixel(index, i, j);
                        var tiles_pixel = image[i, j];

                        count++;
                        cost += ColorRGBA.SqrDistance(exemplars_pixel, tiles_pixel);
                    }
                }

                float modifier = 1.0f + exemplar.Used * costModifer;
                cost *= modifier;

                if (count != 0)
                    cost = (cost / count) * modifier;
                else
                    cost = float.PositiveInfinity;

                trimmed_costs[k] = new Tuple<float, Exemplar>(cost, exemplar);
            }

            //Sort the best matches
            Array.Sort(trimmed_costs, (x, y) => x.Item1.CompareTo(y.Item1));

            //Best match should be first exemplar.
            return trimmed_costs[0].Item2;
        }

    }
}
