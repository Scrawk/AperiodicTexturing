using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Shapes;
using Common.Core.Directions;

using ImageProcessing.Images;

namespace AperiodicTexturing
{
    public class ExemplarSet
    {

        public ExemplarSet(int exemplarSize)
        {
            ExemplarSize = exemplarSize;
            Exemplars = new List<Exemplar>();
        }

        public int Count => Exemplars.Count;

        public int ExemplarSize { get; private set; }

        public List<Exemplar> Exemplars { get; private set; }

        public override string ToString()
        {
            return String.Format("[ExemplarSet: Count={0}, Size={1}]", Count, ExemplarSize);
        }

        public void ResetUsedCount()
        {
            foreach (var exemplar in Exemplars)
                exemplar.ResetUsed();
        }

        public List<Exemplar> GetRandomExemplars(int count, int seed)
        {
            count = Math.Max(count, 0);
            var exemplars = new List<Exemplar>();

            if (count >= Count)
            {
                exemplars.AddRange(Exemplars);
            }
            else
            {
                var rnd = new Random(seed);

                while (exemplars.Count != count)
                {
                    int index = rnd.Next(0, Exemplars.Count);

                    var exemplar = Exemplars[index];

                    if (!exemplars.Contains(exemplar))
                        exemplars.Add(exemplar);
                }
            }

            return exemplars;
        }

        public (Exemplar, Point2i) FindBestMatch(ColorImage2D image, BinaryImage2D mask, int maxOffset)
        {
            Exemplar bestMatch = null;
            float bestCost = float.PositiveInfinity;
            Point2i bestOffset = new Point2i();

            foreach (var exemplar in Exemplars)
            {
                if (exemplar.Image == image)
                    continue;

                if (exemplar.Used > 0)
                    continue;

                float cost = 0;
                int count = 0;
                Point2i offset = new Point2i();

                if (maxOffset <= 0)
                {
                    for (int x = 0; x < exemplar.Width; x++)
                    {
                        for (int y = 0; y < exemplar.Height; y++)
                        {
                            if (mask != null && !mask[x, y]) continue;

                            var pixel1 = image[x, y];
                            var pixel2 = exemplar[x, y];

                            cost += ColorRGB.SqrDistance(pixel1, pixel2);
                            count++;
                        }
                    }

                    if (count == 0) continue;
                    cost /= count;
                }
                else
                {
                    var pair = FindBestOffset(image, exemplar.Image, mask, maxOffset);
                    offset = pair.Item1;
                    cost = pair.Item2;
                }

                if (cost < bestCost)
                {
                    bestCost = cost;
                    bestMatch = exemplar;
                    bestOffset = offset;
                }
            }

            return (bestMatch, bestOffset);
        }

        private (Point2i, float) FindBestOffset(ColorImage2D image, ColorImage2D image2, BinaryImage2D mask, int offset)
        {
            Point2i bestOffset = new Point2i();
            float bestCost = float.PositiveInfinity;

            for (int i = -offset; i < offset; i++)
            {
                for (int j = -offset; j < offset; j++)
                {
                    float cost = 0;
                    int count = 0;

                    for (int x = 0; x < image.Width; x++)
                    {
                        for (int y = 0; y < image.Height; y++)
                        {
                            if (mask != null && !mask[x, y]) continue;

                            var pixel1 = image[x, y];
                            var pixel2 = image2[x + i, y + j];

                            cost += ColorRGB.SqrDistance(pixel1, pixel2);
                            count++;
                        }
                    }

                    if (count == 0) continue;
                    cost /= count;

                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        bestOffset = new Point2i(i, j);
                    }
                }
            }

            return (bestOffset, bestCost);
        }

        public void CreateExemplarFromCrop(ColorImage2D image)
        {
            var croppedImages = ColorImage2D.Crop(image, image.Width / ExemplarSize, image.Height / ExemplarSize);
            Exemplars = CreateVariants(croppedImages);
        }

        public void CreateExemplarFromRandom(ColorImage2D image, int seed, int count)
        {
            var mask = new BinaryImage2D(image.Width, image.Height);
            var exemplars = new List<Exemplar>();

            var rnd = new Random(seed);
            int fails = 0;

            while (exemplars.Count < count && fails < 1000)
            {
                int x = rnd.Next(0, image.Width - ExemplarSize - 1);
                int y = rnd.Next(0, image.Height - ExemplarSize - 1);

                var coverage = GetCoverage(mask, x, y);

                if (coverage > 0.5f)
                {
                    fails++;
                    continue;
                }

                AddCoverage(mask, x, y);

                var exemplar = ColorImage2D.Crop(image, new Box2i(x, y, x + ExemplarSize, y + ExemplarSize));
                exemplars.Add(new Exemplar(exemplar));
            }

            Exemplars = CreateVariants(exemplars);
        }

        private float GetCoverage(BinaryImage2D mask, int x, int y)
        {
            int count = 0;

            for (int j = 0; j < ExemplarSize; j++)
            {
                for (int i = 0; i < ExemplarSize; i++)
                {
                    if (mask[x + i, y + j])
                        count++;
                }
            }

            return count / (float)(ExemplarSize * ExemplarSize);
        }

        private void AddCoverage(BinaryImage2D mask, int x, int y)
        {
            for (int j = 0; j < ExemplarSize; j++)
            {
                for (int i = 0; i < ExemplarSize; i++)
                {
                    mask[x + i, y + j] = true;
                }
            }
        }

        private List<Exemplar> CreateVariants(List<ColorImage2D> images)
        {
            var variants = new List<Exemplar>();

            foreach (var image in images)
            {
                var exemplar = new Exemplar(image);
                var v = exemplar.CreateVariants();

                variants.Add(exemplar);
                variants.AddRange(v);
            }

            return variants;
        }

        private List<Exemplar> CreateVariants(List<Exemplar> exemplars)
        {
            var variants = new List<Exemplar>();

            foreach (var exemplar in exemplars)
            {
                var v = exemplar.CreateVariants();

                variants.Add(exemplar);
                variants.AddRange(v);
            }

            return variants;
        }
    }
}
