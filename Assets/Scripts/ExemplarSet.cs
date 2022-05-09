using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Shapes;
using Common.Core.Directions;

using ImageProcessing.Images;
using System.Collections;

using UnityEngine;

namespace AperiodicTexturing
{
    public class ExemplarSet
    {
        /// <summary>
        /// Create a new exemplat set.
        /// </summary>
        /// <param name="source">The source image the exemplars are created from.</param>
        /// <param name="sourceIsTileable">Is the source texture tileable.</param>
        /// <param name="exemplarSize">The size of a exemplar.</param>
        public ExemplarSet(ColorImage2D source, bool sourceIsTileable, int exemplarSize)
        {
            Sources = new List<ColorImage2D>();
            Sources.Add(source);

            SourceIsTileable = sourceIsTileable;
            ExemplarSize = exemplarSize;
            Exemplars = new List<Exemplar>();
        }

        /// <summary>
        /// Create a new exemplat set.
        /// </summary>
        /// <param name="sources">The source image the exemplars are created from is the first image in the list. 
        /// Any otyhers are optional textures like heights, normasl, metalness, etc.</param>
        /// <param name="sourceIsTileable">Is the source texture tileable.</param>
        /// <param name="exemplarSize">The size of a exemplar.</param>
        public ExemplarSet(IList<ColorImage2D> sources, bool sourceIsTileable, int exemplarSize)
        {
            if (sources.Count == 0)
                throw new ArgumentException("The sources array must have at least 1 image.");

            var size = sources[0].Size;
            Sources = new List<ColorImage2D>();

            for(int i = 0; i < sources.Count; i++)
            {
                var source = sources[i];
                if (source.Size != size)
                    throw new ArgumentException("All the source images must be the same size.");

                Sources.Add(source);
            }
                
            SourceIsTileable = sourceIsTileable;
            ExemplarSize = exemplarSize;
            Exemplars = new List<Exemplar>();
        }

        /// <summary>
        /// The numer of exemplars in the set.
        /// </summary>
        public int ExemplarCount => Exemplars.Count;

        /// <summary>
        /// The width and height of a exemplars image.
        /// </summary>
        public int ExemplarSize { get; private set; }

        /// <summary>
        /// Is the source texture tileable.
        /// </summary>
        public bool SourceIsTileable { get; private set; }

        /// <summary>
        /// The exemplars
        /// </summary>
        private List<Exemplar> Exemplars { get; set; }

        /// <summary>
        /// The exemplars source image.
        /// </summary>
        private List<ColorImage2D> Sources { get; set; }

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
            return String.Format("[ExemplarSet: ExemplarCount={0}, ExemplarSize={1}, SourceIsTileable={2}]",
                ExemplarCount, ExemplarSize, SourceIsTileable);
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
        /// Get the percentage of exemplars in the set have been used.
        /// </summary>
        /// <returns>The percentage of used exemplars from 0 to 1.</returns>
        public float PercentageUsed()
        {
            if (ExemplarCount == 0) return 0;

            int used = 0;

            foreach (var exemplar in Exemplars)
                if (exemplar.Used > 0)
                    used++;

            return used / (float)ExemplarCount;
        }

        /// <summary>
        /// Create the mipmaps for each exemplar is set.
        /// </summary>
        public void CreateMipmaps()
        {
            foreach (var exemplar in Exemplars)
                exemplar.Tile.CreateMipmaps();
        }

        /// <summary>
        /// Create new variants of each exemplar and add the to the set.
        /// </summary>
        /// <param name="flags">The types of exemplars to create.</param>
        public void CreateVariants(EXEMPLAR_VARIANT flags)
        {
            if (flags == EXEMPLAR_VARIANT.NONE)
                return;

            var variants = new List<Exemplar>();

            foreach (var exemplar in Exemplars)
            {
                var v = exemplar.CreateVariants(flags);
                variants.AddRange(v);
            }

            Exemplars.AddRange(variants);
        }

        /// <summary>
        /// Get a list of each exemplars tiles.
        /// Tile are deep copied.
        /// </summary>
        /// <returns>The list of tiles.</returns>
        public List<Tile> GetTiles()
        {
            var tiles = new List<Tile>(ExemplarCount);

            foreach (var exemplar in Exemplars)
                tiles.Add(exemplar.Tile.Copy());

            return tiles;
        }

        /// <summary>
        /// Get a list of random tiles.
        /// Tile are deep copied.
        /// </summary>
        /// <param name="count">The number of tiles to get. 
        /// If the count is larger than the set size then all tiles are returned.</param>
        /// <param name="seed">The seed for the random generator.</param>
        /// <returns>The list of tiles.</returns>
        public List<Tile> GetRandomTiles(int count, int seed)
        {
            var exemplars = GetRandomExemplars(count, seed);

            var tiles = new List<Tile>(exemplars.Count);

            foreach (var exemplar in exemplars)
                tiles.Add(exemplar.Tile.Copy());

            return tiles;
        }

        /// <summary>
        /// Assign a weight to each tile in the set.
        /// </summary>
        /// <param name="weights">A array of floats contains values >= 0.</param>
        public void SetWeights(IList<float> weights)
        {
            for (int i = 0; i < ExemplarCount; i++)
                Exemplars[i].Tile.SetWeights(weights);
        }

        /// <summary>
        /// Get a list of random exemplars.
        /// </summary>
        /// <param name="count">The number of exemplars to get. 
        /// If the count is larger than the set size then all exemplars are returned.</param>
        /// <param name="seed">The seed for the random generator.</param>
        /// <returns>The list of exemplars.</returns>
        public List<Exemplar> GetRandomExemplars(int count, int seed)
        {
            count = Math.Max(count, 0);
            var exemplars = new List<Exemplar>();

            if (count >= ExemplarCount)
            {
                //If the requested number of exemplars to create is
                //larger that the actual number then just return a
                //list of all the exemplars.
                exemplars.AddRange(Exemplars);
            }
            else
            {
                var rnd = new System.Random(seed);

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
        public void CreateExemplarsFromCrop(int overlap = 0)
        {
            Clear();

            int width = Sources[0].Width;
            int height = Sources[0].Height;

            var images = new List<List<ColorImage2D>>(Sources.Count);

            for(int i = 0; i < Sources.Count; i++)
            {
                var source = Sources[i];
                var crop = ColorImage2D.Crop(source, width / ExemplarSize, height / ExemplarSize, overlap);

                for (int j = 0; j < crop.Count; j++)
                {
                    crop[j].Name = source.Name + " _crop_" + j;
                }

                images.Add(crop);
            }

            if (images.Count == 0)
                return;

            int numExemplars = images[0].Count;

            for (int i = 0; i < numExemplars; i++)
            {
                var exemplar = new Exemplar(ExemplarSize);
                Exemplars.Add(exemplar);
            }

            for (int i = 0; i < images.Count; i++)
            {
                for (int j = 0; j < images[i].Count; j++)
                {
                    var image = images[i][j];
                    var exemplar = Exemplars[j];

                    exemplar.Tile.Images.Add(image);
                }
            }

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

            int width = Sources[0].Width;
            int height = Sources[0].Height;

            var mask = new BinaryImage2D(width, height);
            var rnd = new System.Random(seed);

            //Safety break in case no new exemplars can be created.
            int fails = 0;

            while (Exemplars.Count < count && fails < 1000)
            {
                int border = SourceIsTileable ? 0 : ExemplarSize;

                //Select a random place in the source image.
                int x = rnd.Next(0, width - border - 1);
                int y = rnd.Next(0, height - border - 1);

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

                var exemplar_images = new List<ColorImage2D>();

                for(int i = 0; i < Sources.Count; i++)
                {
                    var box = new Box2i(x, y, x + ExemplarSize, y + ExemplarSize);
                    var image = ColorImage2D.Crop(Sources[i], box, 0, WRAP_MODE.WRAP);
                    image.Name = "RandomImage" + i;

                    exemplar_images.Add(image);
                }

                Exemplars.Add(new Exemplar(exemplar_images));
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
                    if (mask.GetValue(x + i, y + j, WRAP_MODE.WRAP))
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
                    mask.SetValue(x + i, y + j, true, WRAP_MODE.WRAP);
                }
            }
        }
    }
}
