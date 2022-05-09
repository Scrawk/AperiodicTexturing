using System;
using System.Collections.Generic;

using Common.Core.Colors;
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
        /// Create a new exemplar.
        /// </summary>
        /// <param name="size">The tile width and height.</param>
        public Exemplar(int size)
        {
            Tile = new Tile(size, size);
        }

        /// <summary>
        /// Create a new exemplar.
        /// </summary>
        /// <param name="image">The exemplars image.</param>
        public Exemplar(ColorImage2D image)
        {
            Tile = new Tile(image);
        }

        /// <summary>
        /// Create a new exemplar thats a variant of the original exemplar.
        /// </summary>
        /// <param name="images">The exemplars imagse.</param>
        public Exemplar(IList<ColorImage2D> images)
        {
            Tile = new Tile(images);
        }

        /// <summary>
        /// Create a new exemplar thats a variant of the original exemplar.
        /// </summary>
        /// <param name="images">The exemplars imagse.</param>
        /// <param name="original">The original exemplar this one is a variant of.</param>
        public Exemplar(IList<ColorImage2D> images, Exemplar original)
        {
            Tile = new Tile(images);
            Original = original;
        }

        /// <summary>
        /// The number of images in the tile.
        /// </summary>
        public int Count => Tile.Count;

        /// <summary>
        /// The exemplars size on the x axis.
        /// </summary>
        public int Width => Tile.Width;

        /// <summary>
        /// The exemplars size on the y axis.
        /// </summary>
        public int Height => Tile.Height;

        /// <summary>
        /// Has this exemplar been used.
        /// </summary>
        public int Used { get; private set; }

        /// <summary>
        /// Is the exemplar a variant of another one.
        /// </summary>
        public bool IsVariant => Original != null;

        /// <summary>
        /// The exemplars images.
        /// </summary>
        public Tile Tile { get; private set; }

        /// <summary>
        /// The original exemplar this one is a variant of.
        /// If not a variant this image will be null.
        /// </summary>
        private Exemplar Original { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("[Exemplar: Count={0}, Width={1}, Height={2}, Used={3}, IsVariant={4}]",
                Count, Width, Height, Used, IsVariant);
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
        /// Create new variants of this exemplar.
        /// </summary>
        /// <param name="flags">The type of variant to create.</param>
        /// <returns>A list of new variants.</returns>
        public List<Exemplar> CreateVariants(EXEMPLAR_VARIANT flags)
        {
            var variants = new List<Exemplar>();

            foreach(var image in Tile.Images)
            {
                var images = new List<ColorImage2D>();

                if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE90))
                    images.Add(ColorImage2D.Rotate90(image));

                if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE180))
                    images.Add(ColorImage2D.Rotate180(image));

                if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE270))
                    images.Add(ColorImage2D.Rotate270(image));

                if (flags.HasFlag(EXEMPLAR_VARIANT.MIRROR_HORIZONTAL))
                    images.Add(ColorImage2D.FlipHorizontal(image));

                if (flags.HasFlag(EXEMPLAR_VARIANT.MIRROR_VERTICAL))
                    images.Add(ColorImage2D.FlipVertical(image));

                if(images.Count > 0)
                    variants.Add(new Exemplar(images, this));
            }

            return variants;
        }

    }
}
