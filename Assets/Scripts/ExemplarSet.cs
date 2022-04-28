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

        public ExemplarSet(ColorImage2D source, int exemplarSize)
        {
            Source = source.Copy();
            ExemplarSize = exemplarSize;
            Exemplars = new List<Exemplar>();
        }

        public int Count => Exemplars.Count;

        public int ExemplarSize { get; private set; }

        public List<Exemplar> Exemplars { get; private set; }

        public ColorImage2D Source { get; private set; }

        public override string ToString()
        {
            return String.Format("[ExemplarSet: Count={0}, Size={1}]", Count, ExemplarSize);
        }

        public void Clear()
        {
            Exemplars.Clear();
        }

        public void ResetUsedCount()
        {
            foreach (var exemplar in Exemplars)
                exemplar.ResetUsed();
        }

        public void CreateVariants()
        {
            var variants = new List<Exemplar>();

            foreach (var exemplar in Exemplars)
            {
                var v = exemplar.CreateVariants();
                variants.AddRange(v);
            }

            Exemplars.AddRange(variants);
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

        public void CreateExemplarsFromCrop()
        {
            Clear();

            var croppedImages = ColorImage2D.Crop(Source, Source.Width / ExemplarSize, Source.Height / ExemplarSize);
            Exemplars = CreateVariants(croppedImages);
        }

        public void CreateExemplarsFromRandom(int seed, int count, float maxCoverage)
        {
            Clear();

            var mask = new BinaryImage2D(Source.Width, Source.Height);
 
            var rnd = new Random(seed);
            int fails = 0;

            while (Exemplars.Count < count && fails < 1000)
            {
                int x = rnd.Next(0, Source.Width - ExemplarSize - 1);
                int y = rnd.Next(0, Source.Height - ExemplarSize - 1);

                var coverage = GetCoverage(mask, x, y);

                if (coverage > maxCoverage)
                {
                    fails++;
                    continue;
                }

                AddCoverage(mask, x, y);

                var exemplar = ColorImage2D.Crop(Source, new Box2i(x, y, x + ExemplarSize, y + ExemplarSize));
                Exemplars.Add(new Exemplar(exemplar));
            }
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

    }
}
