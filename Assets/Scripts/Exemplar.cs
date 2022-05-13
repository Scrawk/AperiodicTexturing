using System;
using System.Collections.Generic;

using Common.Core.Colors;
using Common.Core.Numerics;
using ImageProcessing.Images;

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
    public class Exemplar
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
        /// 
        /// </summary>
        private Point2i Index { get; set; }

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
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("[Exemplar: SourceCount={0}, ExemplarSize={1}, Used={2}, Variant={3}]",
                SourceCount, ExemplarSize, Used, Variant);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Exemplar Copy()
        {
            var copy = new Exemplar(Index, ExemplarSize, Sources, Variant);
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

            var index = GetIndex(x, y);
            return Sources[i].GetPixel(index.x, index.y, wrap);
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
        /// <param name="i"></param>
        /// <returns></returns>
        public ColorImage2D GetImageCopy(int i)
        {
            var image = new ColorImage2D(ExemplarSize, ExemplarSize);
            var source = Sources[i];

            image.Fill((x, y) =>
            {
                var index = GetIndex(x, y);
                return source[index.x, index.y];
            });

            return image;
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
        private Point2i GetIndex(int x, int y)
        {
            var index = new Point2i(Index.x + x, Index.y + y);

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
