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
			Index = new Index2(-1, -1);
			Edges = new int[]
			{
				-1,-1,-1,-1
			};
		}

		public WangTile(Index2 index, int left, int bottom, int right, int top, int tileSize)
		{
			Index = index;
			TileSize = tileSize;
			Image = new ColorImage2D(tileSize, tileSize);
			Map = new GreyScaleImage2D(tileSize, tileSize);
			Mask = new BinaryImage2D(tileSize, tileSize);

			Edges = new int[]
			{
				left, bottom, right, top
			};
		}

		public Index2 Index { get; private set; }

		public int Left => Edges[0];

		public int Bottom => Edges[1];

		public int Right => Edges[2];

		public int Top => Edges[3];

		public int[] Edges { get; private set; }

		public int TileSize { get; private set; }

		public ColorImage2D Image { get; private set; }

		public GreyScaleImage2D Map { get; private set; }

		public BinaryImage2D Mask { get; private set; }

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
			copy.Map = Map.Copy();
			copy.Mask = Mask.Copy();

			return copy;
		}

		public void FillImage(IList<ColorImage2D> images)
        {
			Image.Fill((x, y) =>
			{
				var index = (int)Map[x, y];
				var pixel = images[index][x, y];

				return pixel;
			});
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

		public void CreateMap()
		{
			if (IsConst)
			{
				Map.Fill(Left);
			}
			else
			{
				var c00 = new Point2i(0, 0);
				var c01 = new Point2i(0, TileSize);
				var c10 = new Point2i(TileSize, 0);
				var c11 = new Point2i(TileSize, TileSize);
				var mid = new Point2i(TileSize / 2, TileSize / 2);

				Map.DrawTriangle(mid, c00, c01, new ColorRGBA(Left, 1), true);
				Map.DrawTriangle(mid, c00, c10, new ColorRGBA(Bottom, 1), true); 
				Map.DrawTriangle(mid, c10, c11, new ColorRGBA(Right, 1), true);
				Map.DrawTriangle(mid, c01, c11, new ColorRGBA(Top, 1), true);
			}
		}
		public void CreateMask()
		{
			if (IsConst) return;

			var mid = new Point2i(TileSize / 2, TileSize / 2);

			if (Left != Bottom)
				Mask.DrawLine(mid, new Point2i(0, 0), ColorRGBA.White);

			if (Bottom != Right)
				Mask.DrawLine(mid, new Point2i(TileSize, 0), ColorRGBA.White);

			if (Right != Top)
				Mask.DrawLine(mid, new Point2i(TileSize, TileSize), ColorRGBA.White);

			if (Top != Left)
				Mask.DrawLine(mid, new Point2i(0, TileSize), ColorRGBA.White);
		}

	}

}
