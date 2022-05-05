using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
using Common.Core.Extensions;
using ImageProcessing.Images;

namespace AperiodicTexturing
{
	/// <summary>
	/// A wang tile consists of a square tile where each edge is assigned a number commonly referred to as a color.
	/// The edges color is then used to determine what other tiles it can be adjacent to in a tiling pattern.
	/// This implementation presumes the edges numbers range from 0 to 3.
	/// https://en.wikipedia.org/wiki/Wang_tile
	/// </summary>
	public class WangTile
	{
		/// <summary>
		/// Create a new wang tile.
		/// </summary>
		/// <param name="left">The tiles edge color on its left side (-x)</param>
		/// <param name="bottom">The tiles edge color on its bottom side (-y)</param>
		/// <param name="right">The tiles edge color on its right side (+x)</param>
		/// <param name="top">The tiles edge color on its top side (+y)</param>
		/// <param name="tileSize">The tiles size.</param>
		/// <param name="height">The tiles height.</param>
		public WangTile(int left, int bottom, int right, int top, int tileSize)
			: this(left, bottom, right, top, tileSize, tileSize)
		{

		}

		/// <summary>
		/// Create a new wang tile.
		/// </summary>
		/// <param name="left">The tiles edge color on its left side (-x)</param>
		/// <param name="bottom">The tiles edge color on its bottom side (-y)</param>
		/// <param name="right">The tiles edge color on its right side (+x)</param>
		/// <param name="top">The tiles edge color on its top side (+y)</param>
		/// <param name="width">The tiles width.</param>
		/// <param name="height">The tiles height.</param>
		public WangTile(int left, int bottom, int right, int top, int width, int height)
		{
			Tile = new Tile(width, height);

			Edges = new int[]
			{
				left, bottom, right, top
			};
		}

		/// <summary>
		/// A 1D number representing the tiles index in a 1D array.
		/// </summary>
		public int Index1 { get; set; }

		/// <summary>
		/// A 2D number representing the tiles index in a 2D array.
		/// </summary>
		public Point2i Index2 { get; set; }

		/// <summary>
		/// The tiles edge color on its left side (-x)
		/// </summary>
		public int Left => Edges[0];

		/// <summary>
		/// The tiles edge color on its bottom side (-y)
		/// </summary>
		public int Bottom => Edges[1];

		/// <summary>
		/// The tiles edge color on its right side (+x)
		/// </summary>
		public int Right => Edges[2];

		/// <summary>
		/// The tiles edge color on its top side (+y)
		/// </summary>
		public int Top => Edges[3];

		/// <summary>
		/// The tiles edge colors.
		/// </summary>
		public int[] Edges { get; private set; }

		/// <summary>
		/// The width of  the tile.
		/// </summary>
		public int Width => Tile.Width;

		/// <summary>
		/// The height of  the tile.
		/// </summary>
		public int Height => Tile.Height;

		/// <summary>
		/// The tiles image.
		/// </summary>
		public Tile Tile { get; private set; }

		/// <summary>
		/// A list of colors the tiles edges can be represented as.
		/// </summary>
		private static ColorRGBA[] Colors = new ColorRGBA[]
		{
			new ColorRGBA(1,0,0,1), new ColorRGBA(0,1,0,1), new ColorRGBA(0,0,1,1),
			new ColorRGBA(1,1,0,1), new ColorRGBA(1,0,1,1), new ColorRGBA(0,1,1,1)
		};

		/// <summary>
		/// Are all the edge colors in this the same.
		/// </summary>
		public bool IsConst
		{
			get
			{
				var first = Edges[0];
				for (int i = 1; i < 4; i++)
					if (Edges[i] != first)
						return false;

				return true;
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.Format("[WangTile: Index={0}, left={1}, bottom={2}, right={3}, top={4}]",
				Index2, Left, Bottom, Right, Top);
		}

		/// <summary>
		/// Create a deep copy of the tile.
		/// </summary>
		/// <returns></returns>
		public WangTile Copy()
		{
			var copy = new WangTile(Left, Bottom, Right, Top, Width, Height);
			copy.Index1 = Index1;
			copy.Index2 = Index2;
			copy.Tile = Tile.Copy();

			return copy;
		}

		/// <summary>
		/// Copy the images in tile.
		/// </summary>
		/// <param name="images">The images to copy.</param>
		public void Fill(IList<ColorImage2D> images)
        {
			Tile.Fill(images);
        }

		/// <summary>
		/// Colors the edges of the image according to its edge color id.
		/// </summary>
		/// <param name="thickness">The thickness of the border.</param>
		/// <param name="alpha">The colors alpha.</param>
		public void AddEdgeColor(int thickness, float alpha)
		{
			ColorRGBA a = new ColorRGBA(1, 1, 1, alpha);

			Tile.Image.DrawBox(0, thickness+1, thickness, Width - thickness - 1, Colors[Left] * a, true);
			Tile.Image.DrawBox(thickness+1, 0, Width - thickness - 1, thickness, Colors[Bottom] * a, true);
			Tile.Image.DrawBox(Width - thickness, thickness+1, Width, Height - thickness - 1, Colors[Right] * a, true);
			Tile.Image.DrawBox(thickness+1, Height - thickness, Width - thickness - 1, Height, Colors[Top] * a, true);
		}

	}

}
