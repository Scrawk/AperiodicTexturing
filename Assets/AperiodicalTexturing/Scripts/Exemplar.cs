using System;
using System.Collections.Generic;

using Common.Core.Colors;
using Common.Core.Numerics;
using ImageProcessing.Images;
using ImageProcessing.Statistics;

namespace AperiodicTexturing
{
    [Flags]
    public enum EXEMPLAR_VARIANT
    {
        NONE = 0,
        ROTATE90 = 1,
        ROTATE180 = 2,
        ROTATE270 = 4,
        MIRROR_HORIZONTAL = 8,
        MIRROR_VERTICAL = 16,
        ALL = ~0
    }

    /// <summary>
    /// A exemplar is a sub-image of a larger images and
    /// is used as a example image for image synthesis.
    /// </summary>
    public class Exemplar : IComparable<Exemplar>
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="size"></param>
        /// <param name="source"></param>
        public Exemplar(Point2i index, int size, ColorImage2D source)
        {
            Index = index;
            ExemplarSize = size;
            Sources = new List<ColorImage2D>();
            Sources.Add(source);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="size"></param>
        /// <param name="sources"></param>
        public Exemplar(Point2i index, int size, IList<ColorImage2D> sources)
        {
            Index = index;
            ExemplarSize = size;
            Sources = new List<ColorImage2D>(sources);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="size"></param>
        /// <param name="sources"></param>
        /// <param name="original"></param>
        public Exemplar(Point2i index, int size, IList<ColorImage2D> sources, EXEMPLAR_VARIANT variant)
        {
            Index = index;
            ExemplarSize = size;
            Sources = new List<ColorImage2D>(sources);
            Variant = variant;
        }

        /// <summary>
        /// The exemplars pixel index in the source image.
        /// </summary>
        private Point2i Index { get; set; }

        /// <summary>
        /// The exemplars cost value that can be set to sort the exemplar.
        /// </summary>
        public float Cost { get; set; }

        /// <summary>
        /// The number of images in the tile.
        /// </summary>
        public int SourceCount => Sources.Count;

        /// <summary>
        /// The exemplars size on the x axis.
        /// </summary>
        public int ExemplarSize { get; private set; }

        /// <summary>
        /// Has this exemplar been used.
        /// </summary>
        public int Used { get; private set; }

        /// <summary>
        /// Is the exemplar a variant of another one.
        /// </summary>
        public EXEMPLAR_VARIANT Variant { get; private set; }

        /// <summary>
        /// The exemplars source image.
        /// </summary>
        private List<ColorImage2D> Sources { get; set; }

        /// <summary>
        /// The exemplars optional images. Null and not created by default.
        /// </summary>
        private List<ColorImage2D> Images { get; set; }

        /// <summary>
        /// The exemplars optional histograms. Null and not created by default.
        /// </summary>
        private List<ColorHistogram> Histograms { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("[Exemplar: Index={0}, SourceCount={1}, ExemplarSize={2}, Used={3}, Variant={4}]",
                Index, SourceCount, ExemplarSize, Used, Variant);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Exemplar Copy()
        {
            var copy = new Exemplar(Index, ExemplarSize, Sources, Variant);

            if(Images != null)
            {
                copy.Images = new List<ColorImage2D>(SourceCount);
                foreach (var image in Images)
                    copy.Images.Add(image.Copy());
            }

            if (Histograms != null)
            {
                copy.Histograms = new List<ColorHistogram>(SourceCount);
                foreach (var histo in Histograms)
                    copy.Histograms.Add(histo.Copy());
            }

            return copy;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <param name="wrap"></param>
        /// <returns></returns>
        public ColorRGBA GetPixel(int i,  int x, int y, WRAP_MODE wrap = WRAP_MODE.CLAMP)
        {
            if (i < 0 || i >= SourceCount)
                throw new ArgumentOutOfRangeException("Index out of source images range.");

            if(Images != null)
            {
                var index = GetIndex(x, y, false);
                return Images[i].GetPixel(index.x, index.y, wrap);
            }
            else
            {
                var index = GetIndex(x, y, true);
                return Sources[i].GetPixel(index.x, index.y, wrap);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="index"></param>
        /// <param name="sources"></param>
        public void AddImages(Point2i index, IList<ColorImage2D> sources)
        {
            Index = index;
            Sources.Clear();
            Sources.AddRange(sources);
        }

        /// <summary>
        /// 
        /// </summary>
        public void CreateImages()
        {
            Images = new List<ColorImage2D>(SourceCount);

            for (int i = 0; i < SourceCount; i++)
                Images.Add(GetImageCopy(i));
        }

        /// <summary>
        /// 
        /// </summary>
        public void CreateHistograms()
        {
            Histograms = new List<ColorHistogram>(SourceCount);

            for (int i = 0; i < SourceCount; i++)
            {
                ColorImage2D image = null;

                if (Images != null)
                    image = Images[i];
                else
                    image = GetImageCopy(i);

                Histograms.Add(new ColorHistogram(image, 256));
            }
        }

        /// <summary>
        /// Compare the exemplar to another by there costs.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(Exemplar other)
        {
            return Cost.CompareTo(other.Cost);
        }

        /// <summary>
        /// Find the square distance between this exemplars histogram and the other.
        /// </summary>
        /// <param name="i">The images histogram index to compare to.</param>
        /// <param name="histo">The other histogram.</param>
        /// <returns>The square distance between the two.</returns>
        public float HistogramSqrDistance(int i, ColorHistogram histo)
        {
            if (Histograms == null)
                throw new NullReferenceException("Histograms have not been created.");

            return Histograms[i].SqrDistance(histo);
        }

        /// <summary>
        /// Find the square distance between each images edges for each image in the exemplar.
        /// This can help determine which exemplars will create better tileable textures.
        /// </summary>
        /// <returns>The square distance between the edges of the images.</returns>
        public float EdgeSqrDistance()
        {
            float sqdist = 0;
            int count = 0;

            for(int i = 0; i < SourceCount; i++)
            {
                for(int x = 0; x < ExemplarSize; x++)
                {
                    var p1 = GetPixel(i, x, 0, WRAP_MODE.WRAP);
                    var p2 = GetPixel(i, x, -1, WRAP_MODE.WRAP);

                    var p3 = GetPixel(i, x, ExemplarSize - 1, WRAP_MODE.WRAP);
                    var p4 = GetPixel(i, x, ExemplarSize, WRAP_MODE.WRAP);

                    sqdist += ColorRGBA.SqrDistance(p1, p2);
                    sqdist += ColorRGBA.SqrDistance(p3, p4);

                    count += 2;
                }

                for (int y = 0; y < ExemplarSize; y++)
                {
                    var p1 = GetPixel(i, 0, y, WRAP_MODE.WRAP);
                    var p2 = GetPixel(i, -1, y, WRAP_MODE.WRAP);

                    var p3 = GetPixel(i, ExemplarSize - 1, y, WRAP_MODE.WRAP);
                    var p4 = GetPixel(i, ExemplarSize, y, WRAP_MODE.WRAP);

                    sqdist += ColorRGBA.SqrDistance(p1, p2);
                    sqdist += ColorRGBA.SqrDistance(p3, p4);

                    count += 2;
                }
            }

            if (count > 0)
                sqdist /= count;
            else
                sqdist = float.PositiveInfinity;

            return sqdist;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public ColorImage2D GetImageCopy(int i)
        {
            if(Images != null)
            {
                return Images[i].Copy();
            }
            else
            {
                var image = new ColorImage2D(ExemplarSize, ExemplarSize);
                var source = Sources[i];

                image.Fill((x, y) =>
                {
                    var index = GetIndex(x, y, true);
                    return source.GetPixel(index.x, index.y, WRAP_MODE.WRAP);
                });

                return image;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Tile GetTileCopy()
        {
            var images = new List<ColorImage2D>();

            for (int i = 0; i < SourceCount; i++)
                images.Add(GetImageCopy(i));

            return new Tile(images);
        }

        /// <summary>
        /// Increment the used count by 1.
        /// </summary>
        public void IncrementUsed()
        {
            Used++;
        }

        /// <summary>
        /// Reset the used count to 0.
        /// </summary>
        public void ResetUsed()
        {
            Used = 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private Point2i GetIndex(int x, int y, bool isSource)
        {
            Point2i index;

            if(isSource)
                index = new Point2i(Index.x + x, Index.y + y);
            else
                index = new Point2i(x, y);

            switch (Variant)
            {
                case EXEMPLAR_VARIANT.NONE:
                    return index;

                case EXEMPLAR_VARIANT.ROTATE90:
                    return new Point2i(index.y, ExemplarSize - 1 - index.x);

                case EXEMPLAR_VARIANT.ROTATE180:
                    return new Point2i(ExemplarSize - 1 - index.x, ExemplarSize - 1 - index.y);

                case EXEMPLAR_VARIANT.ROTATE270:
                    return new Point2i(ExemplarSize - 1 - index.y, index.x);

                case EXEMPLAR_VARIANT.MIRROR_HORIZONTAL:
                    return new Point2i(ExemplarSize - index.x - 1, index.y);
         
                case EXEMPLAR_VARIANT.MIRROR_VERTICAL:
                    return new Point2i(index.x, ExemplarSize - index.y - 1);
            }

            return index;
        }

        /// <summary>
        /// Create new variants of this exemplar.
        /// </summary>
        /// <param name="flags">The type of variant to create.</param>
        /// <returns>A list of new variants.</returns>
        public List<Exemplar> CreateVariants(EXEMPLAR_VARIANT flags)
        {
            var variants = new List<Exemplar>();

            if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE90))
                variants.Add(new Exemplar(Index, ExemplarSize, Sources, EXEMPLAR_VARIANT.ROTATE90));

            if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE180))
                variants.Add(new Exemplar(Index, ExemplarSize, Sources, EXEMPLAR_VARIANT.ROTATE180));

            if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE270))
                variants.Add(new Exemplar(Index, ExemplarSize, Sources, EXEMPLAR_VARIANT.ROTATE270));

            if (flags.HasFlag(EXEMPLAR_VARIANT.MIRROR_HORIZONTAL))
                variants.Add(new Exemplar(Index, ExemplarSize, Sources, EXEMPLAR_VARIANT.MIRROR_HORIZONTAL));

            if (flags.HasFlag(EXEMPLAR_VARIANT.MIRROR_VERTICAL))
                variants.Add(new Exemplar(Index, ExemplarSize, Sources, EXEMPLAR_VARIANT.MIRROR_VERTICAL));

            return variants;
        }

    }
}
