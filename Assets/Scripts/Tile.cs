using System;
using System.Collections.Generic;

using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{

    public class Tile
    {
        public Tile(int width, int height)
        {
            Images = new List<ColorImage2D>();

            Width = width;
            Height = height;
        }

        public Tile(ColorImage2D image)
        {
            Images = new List<ColorImage2D>();
            Images.Add(image);

            Width = image.Width;
            Height = image.Height;
        }

        public Tile(IList<ColorImage2D> images)
        {
            Images = new List<ColorImage2D>();
            foreach (var image in images)
                Images.Add(image);

            Width = images[0].Width;
            Height = images[0].Height;
        }

        /// <summary>
        /// The tiles size on the x axis.
        /// </summary>
        public int Width { get; private set; }

        /// <summary>
        /// The tiles size on the y axis.
        /// </summary>
        public int Height { get; private set; }

        /// <summary>
        /// The number of images in the tile.
        /// </summary>
        public int Count => Images.Count;

        /// <summary>
        /// The tiles first images.
        /// </summary>
        public ColorImage2D Image => Images[0];

        /// <summary>
        /// The tiles images.
        /// </summary>
        public List<ColorImage2D> Images { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return String.Format("[Tile: Count={0}, Width={1}, Height={2}]",
                Count, Width, Height);
        }

        /// <summary>
        /// Create a deep copy of the tile.
        /// </summary>
        /// <returns></returns>
        public Tile Copy()
        {
            var images = new List<ColorImage2D>();
            foreach (var image in Images)
                images.Add(image.Copy());

            var copy = new Tile(images);
            return copy;
        }

        /// <summary>
        /// Offset each image.
        /// </summary>
        /// <param name="xoffset"></param>
        /// <param name="yoffset"></param>
        public void Offset(int xoffset, int yoffset)
        {
            for(int i = 0; i < Count; i++)
            {
                Images[i] = ColorImage2D.Offset(Images[i], xoffset, yoffset);
            }
        }

    }
}
