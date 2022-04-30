using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
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
		/// <param name="tileSize">The size of the tile.</param>
		public WangTile(int left, int bottom, int right, int top, int tileSize)
		{
			TileSize = tileSize;
			Image = new ColorImage2D(tileSize, tileSize);

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
		/// The size of the tiles image data.
		/// </summary>
		public int TileSize { get; private set; }

		/// <summary>
		/// The tiles image.
		/// </summary>
		public ColorImage2D Image { get; private set; }

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
			var copy = new WangTile(Left, Bottom, Right, Top, TileSize);
			copy.Index1 = Index1;
			copy.Index2 = Index2;
			copy.Image = Image.Copy();

			return copy;
		}

		/// <summary>
		/// Colors the edges of the image according to its edge color id.
		/// </summary>
		/// <param name="thickness">The thickness of the border.</param>
		/// <param name="alpha">The colors alpha.</param>
		public void AddEdgeColor(int thickness, float alpha)
		{
			int size = TileSize;
			ColorRGBA a = new ColorRGBA(1, 1, 1, alpha);

			Image.DrawBox(0, thickness+1, thickness, size - thickness - 1, Colors[Left] * a, true);
			Image.DrawBox(thickness+1, 0, size - thickness - 1, thickness, Colors[Bottom] * a, true);
			Image.DrawBox(size - thickness, thickness+1, size, size - thickness - 1, Colors[Right] * a, true);
			Image.DrawBox(thickness+1, size - thickness, size - thickness - 1, size, Colors[Top] * a, true);
		}

	}

}
