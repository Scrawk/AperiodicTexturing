using System;
using System.Collections.Generic;

using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{
    public class Exemplar
    {

        public Exemplar(ColorImage2D image)
        {
            Image = image;
        }

        public Exemplar(ColorImage2D image, Exemplar original)
        {
            Image = image;
            Original = original;
        }

        public int Width => Image.Width;

        public int Height => Image.Height;

        public int Used { get; private set; }

        public bool IsVariant => Original != null;

        public ColorRGB this[int x, int y]
        {
            get { return Image[x, y]; }
        }

        public ColorImage2D Image { get; private set; }

        private Exemplar Original { get; set; }

        public override string ToString()
        {
            return String.Format("[Exemplar: Width={0}, Height={1}, Used={2}, IsVariant={3}]",
                Width, Height, Used, IsVariant);
        }

        public void IncrementUsed()
        {
            Used++;
        }

        public void ResetUsed()
        {
            Used = 0;
        }

        public List<Exemplar> CreateVariants()
        {
            var variants = new List<Exemplar>();

            variants.Add(new Exemplar(ColorImage2D.Rotate90(Image), this));
            variants.Add(new Exemplar(ColorImage2D.Rotate180(Image), this));
            variants.Add(new Exemplar(ColorImage2D.Rotate270(Image), this));

            return variants;
        }

    }
}
