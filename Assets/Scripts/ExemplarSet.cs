using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Shapes;
using Common.Core.Directions;

using ImageProcessing.Images;
using System.Collections;

namespace AperiodicTexturing
{
    public class ExemplarSet : IEnumerable<Exemplar>
    {
        /// <summary>
        /// Create a new exemplat set.
        /// </summary>
        /// <param name="source">The source mage the exemplars are created from.</param>
        /// <param name="exemplarSize">The size of a exemplar.</param>
        public ExemplarSet(ColorImage2D source, int exemplarSize)
        {
            Source = source.Copy();
            ExemplarSize = exemplarSize;
            Exemplars = new List<Exemplar>();
        }

        /// <summary>
        /// The numer of exemplars in the set.
        /// </summary>
        public int Count => Exemplars.Count;

        /// <summary>
        /// The width and height of a exemplars image.
        /// </summary>
        public int ExemplarSize { get; private set; }

        /// <summary>
        /// The exemplars
        /// </summary>
        private List<Exemplar> Exemplars { get; set; }

        /// <summary>
        /// The exemplars source image.
        /// </summary>
        private ColorImage2D Source { get; set; }

        /// <summary>
        /// Get the exemplar at index i.
        /// </summary>
        /// <param name="i">The index of the exemplar to get.</param>
        /// <returns>The exemplar at index i.</returns>
        public Exemplar this[int i]
        {
            get { return Exemplars[i]; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("[ExemplarSet: Count={0}, Size={1}]", Count, ExemplarSize);
        }

        /// <summary>
        /// Enumerate through the exemplas in set.
        /// </summary>
        /// <returns>The exemplar.</returns>
        public IEnumerator<Exemplar> GetEnumerator()
        {
            foreach (var exemplar in Exemplars)
                yield return exemplar;
        }

        /// <summary>
        /// Enumerate through the exemplas in set.
        /// </summary>
        /// <returns>The exemplar.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Clear the set of all exemplars.
        /// </summary>
        public void Clear()
        {
            Exemplars.Clear();
        }

        /// <summary>
        /// Reset the used count of each exemplar in the set.
        /// </summary>
        public void ResetUsedCount()
        {
            foreach (var exemplar in Exemplars)
                exemplar.ResetUsed();
        }

        /// <summary>
        /// Create new variants of each exemplar and add the to the set.
        /// </summary>
        /// <param name="flags">The types of exemplars to create.</param>
        public void CreateVariants(EXEMPLAR_VARIANT flags)
        {
            var variants = new List<Exemplar>();

            foreach (var exemplar in Exemplars)
            {
                var v = exemplar.CreateVariants(flags);
                variants.AddRange(v);
            }

            Exemplars.AddRange(variants);
        }

        /// <summary>
        /// Get a list of random exemplar.
        /// </summary>
        /// <param name="count">The number of exemplars to get. 
        /// If the count is larger than the set size then all exemplars are returned.</param>
        /// <param name="seed">The seed for the random generator.</param>
        /// <returns>The list of exemplars.</returns>
        public List<Exemplar> GetRandomExemplars(int count, int seed)
        {
            count = Math.Max(count, 0);
            var exemplars = new List<Exemplar>();

            if (count >= Count)
            {
                //If the requested number of exemplars to create is
                //larger that the actual number then just return a
                //list of all the exemplars.
                exemplars.AddRange(Exemplars);
            }
            else
            {
                var rnd = new Random(seed);

                while (exemplars.Count != count)
                {
                    //Select a random exemplar.
                    int index = rnd.Next(0, Exemplars.Count);
                    var exemplar = Exemplars[index];

                    //If it has not already been selected add it to the list.
                    if (!exemplars.Contains(exemplar))
                        exemplars.Add(exemplar);
                }
            }

            return exemplars;
        }

        /// <summary>
        /// Create a new set of exemplars by dividing the sources image into even parts.
        /// Presumes the exemplar size divides evenly in the source image size.
        /// </summary>
        public void CreateExemplarsFromCrop()
        {
            Clear();

            var images = ColorImage2D.Crop(Source, Source.Width / ExemplarSize, Source.Height / ExemplarSize);

            foreach(var image in images)
                Exemplars.Add(new Exemplar(image));
        }

        /// <summary>
        /// Create a new set of exeplars by taking random parts of the source image.
        /// </summary>
        /// <param name="count">The number of exemplas to create. 
        /// The actual number of exemplars to create maybe smaller 
        /// than this if new area of the source image run out.</param>
        /// <param name="seed">The seed for the random generator.</param>
        /// <param name="maxCoverage">The max amount (in percentage 0-1) of the source image already 
        /// sampled that a new exemplar can be created from.</param>
        public void CreateExemplarsFromRandom(int count, int seed, float maxCoverage)
        {
            Clear();

            var mask = new BinaryImage2D(Source.Width, Source.Height);
 
            var rnd = new Random(seed);

            //Safety break in case no new exemplars can be created.
            int fails = 0;

            while (Exemplars.Count < count && fails < 1000)
            {
                //Select a random place in the source image.
                int x = rnd.Next(0, Source.Width - ExemplarSize - 1);
                int y = rnd.Next(0, Source.Height - ExemplarSize - 1);

                //Get the amount of pixels in this area of the image
                //that have already been used to create new exemplars.
                var coverage = GetCoverage(mask, x, y);

                //If the number of pixels areadly sample in this
                //area is about the threshold then count as fail and move on.
                //This makes sure the exemplars create or no too simlar but
                //can still overlap some what.
                if (coverage > maxCoverage)
                {
                    fails++;
                    continue;
                }

                //Mark this area of the image as having been sampled.
                AddCoverage(mask, x, y);

                var exemplar = ColorImage2D.Crop(Source, new Box2i(x, y, x + ExemplarSize, y + ExemplarSize));
                Exemplars.Add(new Exemplar(exemplar));
            }
        }

        /// <summary>
        /// Get the percentage of pixels in a area of the mask that are set to true.
        /// </summary>
        /// <param name="mask">The mask image thats the same size as the source image.</param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Set a area of the mask images pixels to true.
        /// </summary>
        /// <param name="mask">The mask image thats the same size as the source image.</param>
        /// <param name="x"></param>
        /// <param name="y"></param>
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
    }
}
