using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Shapes;
using Common.Core.Extensions;

using ImageProcessing.Images;

namespace AperiodicTexturing
{
    /// <summary>
    /// 
    /// </summary>
    public class Tile
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="width"></param>
        /// <param name="height"></param>
        public Tile(int width, int height)
        {
            Images = new List<ColorImage2D>();

            Width = width;
            Height = height;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        public Tile(ColorImage2D image)
        {
            Images = new List<ColorImage2D>();
            Images.Add(image);

            Width = image.Width;
            Height = image.Height;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="images"></param>
        public Tile(IList<ColorImage2D> images)
        {
            Images = new List<ColorImage2D>(images);
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
        /// The tiles images.
        /// </summary>
        public List<ColorImage2D> Images { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return string.Format("[Tile: Count={0}, Width={1}, Height={2}, Images={3}]",
                Count, Width, Height, GetImageNames());
        }

        /// <summary>
        /// Get all the images names as a string for debugging.
        /// </summary>
        /// <returns></returns>
        private string GetImageNames()
        {
            string images = "{";
            for (int i = 0; i < Images.Count; i++)
            {
                images += Images[i].Name;

                if (i != Images.Count - 1)
                    images += ", ";
            }
            images += "}";

            return images;
        }

        /// <summary>
        /// Create a deep copy of the tile.
        /// </summary>
        /// <returns>A deep copy of the tile.</returns>
        public Tile Copy()
        {
            var images = new List<ColorImage2D>();
            foreach (var image in Images)
                images.Add(image.Copy());

            var copy = new Tile(images);

            return copy;
        }

        /// <summary>
        /// Copy the images in tile.
        /// </summary>
        /// <param name="images">The images to copy.</param>
        public void Fill(IList<ColorImage2D> images)
        {
            Images.Clear();

            for (int i = 0; i < images.Count; i++)
            {
                Images.Add(images[i].Copy());
            }
        }

        /// <summary>
        /// Offset each image.
        /// </summary>
        /// <param name="xoffset">The amount to offset on the x axis.</param>
        /// <param name="yoffset">The amount to offset on the y axis.</param>
        public void Offset(int xoffset, int yoffset)
        {
            foreach(var image in Images)
            {
                var offset = ColorImage2D.Offset(image, xoffset, yoffset);
                image.Fill(offset);
            }
        }

        /// <summary>
        /// Offset each image in the tile by half.
        /// </summary>
        /// <param name="forwards">The offset direction.</param>
        public void HalfOffset(bool forwards)
        {
            foreach (var image in Images)
            {
                var offset = ColorImage2D.HalfOffset(image, forwards);
                image.Fill(offset);
            }
        }

        /// <summary>
        /// Blur the tile.
        /// </summary>
        /// <param name="mask">The mask that determines what areas will be blurred and the blur strength. 
        /// Is optional and can be null.</param>
        /// <param name="bounds">A box that determines what areas will be blurred. 
        /// Is optional and can be null.</param>
        /// <param name="strength">The blurs strength.</param>
        /// <param name="wrap">The wrap mode used to determine how out of bounds pixels are handled.</param>
        public void Blur(GreyScaleImage2D mask, Box2i? bounds, float strength, WRAP_MODE wrap)
        {
            foreach(var image in Images)
            {
                var blurred = ColorImage2D.GaussianBlur(image, strength, bounds, mask, wrap);
                image.Fill(blurred);
            }
        }



    }
}
