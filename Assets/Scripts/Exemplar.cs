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
        /// <param name="image">The exemplars image.</param>
        public Exemplar(ColorImage2D image)
        {
            Image = image;
        }

        /// <summary>
        /// Create a new exemplar thats a variant of the original exemplar.
        /// </summary>
        /// <param name="image">The exemplars image.</param>
        /// <param name="original">The original exemplar this one is a variant of.</param>
        public Exemplar(ColorImage2D image, Exemplar original)
        {
            Image = image;
            Original = original;
        }

        /// <summary>
        /// The Examples size on the x axis.
        /// </summary>
        public int Width => Image.Width;

        /// <summary>
        /// The Examples size on the y axis.
        /// </summary>
        public int Height => Image.Height;

        /// <summary>
        /// Has this exemplar been used.
        /// </summary>
        public int Used { get; private set; }

        /// <summary>
        /// Is the exemplar a variant of another one.
        /// </summary>
        public bool IsVariant => Original != null;

        /// <summary>
        /// Access the exemplars image.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        public ColorRGB this[int x, int y]
        {
            get { return Image[x, y]; }
        }

        /// <summary>
        /// The exemaplars image.
        /// </summary>
        public ColorImage2D Image { get; private set; }

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
            return String.Format("[Exemplar: Width={0}, Height={1}, Used={2}, IsVariant={3}]",
                Width, Height, Used, IsVariant);
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

            if(flags.HasFlag(EXEMPLAR_VARIANT.ROTATE90))
                variants.Add(new Exemplar(ColorImage2D.Rotate90(Image), this));

            if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE180))
                variants.Add(new Exemplar(ColorImage2D.Rotate180(Image), this));

            if (flags.HasFlag(EXEMPLAR_VARIANT.ROTATE270))
                variants.Add(new Exemplar(ColorImage2D.Rotate270(Image), this));

            if (flags.HasFlag(EXEMPLAR_VARIANT.MIRROR_HORIZONTAL))
                variants.Add(new Exemplar(ColorImage2D.FlipHorizontal(Image), this));

            if (flags.HasFlag(EXEMPLAR_VARIANT.MIRROR_VERTICAL))
                variants.Add(new Exemplar(ColorImage2D.FlipVertical(Image), this));

            return variants;
        }

    }
}
