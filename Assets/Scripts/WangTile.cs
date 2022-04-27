using System;
using System.Collections.Generic;

using Common.Core.Numerics;
using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{

	public class WangTile
	{

		public WangTile()
		{
			Index = new Point2i(-1, -1);
			Edges = new int[]
			{
				-1,-1,-1,-1
			};
		}

		public WangTile(Point2i index, int left, int bottom, int right, int top, int tileSize)
		{
			Index = index;
			TileSize = tileSize;
			Image = new ColorImage2D(tileSize, tileSize);

			Edges = new int[]
			{
				left, bottom, right, top
			};
		}

		public Point2i Index { get; private set; }

		public int Left => Edges[0];

		public int Bottom => Edges[1];

		public int Right => Edges[2];

		public int Top => Edges[3];

		public int[] Edges { get; private set; }

		public int TileSize { get; private set; }

		public ColorImage2D Image { get; private set; }

		private static ColorRGBA[] Colors = new ColorRGBA[]
		{
			new ColorRGBA(1,0,0,1), new ColorRGBA(0,1,0,1), new ColorRGBA(0,0,1,1),
			new ColorRGBA(1,1,0,1), new ColorRGBA(1,0,1,1), new ColorRGBA(0,1,1,1)
		};

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

		public override string ToString()
		{
			return string.Format("[WangTile: Index={0}, left={1}, bottom={2}, right={3}, top={4}]",
				Index, Left, Bottom, Right, Top);
		}

		public WangTile Copy()
		{
			var copy = new WangTile(Index, Left, Bottom, Right, Top, TileSize);
			copy.Image = Image.Copy();

			return copy;
		}

		public void AddEdgeColor(int thickness, float alpha)
		{
			int size = TileSize;
			ColorRGBA a = new ColorRGBA(1, 1, 1, alpha);

			Image.DrawBox(0, thickness, thickness, size - thickness, Colors[Left] * a, true);
			Image.DrawBox(thickness, 0, size - thickness, thickness, Colors[Bottom] * a, true);
			Image.DrawBox(size - thickness, thickness, size, size - thickness, Colors[Right] * a, true);
			Image.DrawBox(thickness, size - thickness, size - thickness, size, Colors[Top] * a, true);
		}

	}

}
