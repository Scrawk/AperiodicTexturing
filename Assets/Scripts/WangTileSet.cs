using System;

using Common.Core.Numerics;
using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{

	public class WangTileSet
	{

		public int NumHColors { get; private set; }

		public int NumVColors { get; private set; }

		public int NumHTiles => NumHColors * NumHColors;

		public int NumVTiles => NumVColors * NumVColors;

		public int NumTiles { get; private set; }

		public int TileSize { get; private set; }

		public WangTile[,] Tiles { get; private set; }

		public WangTileSet(int numHColors, int numVColors, int tileSize)
		{
			NumHColors = numHColors;
			NumVColors = numVColors;
			NumTiles = numHColors * numHColors * numVColors * numVColors;
			TileSize = tileSize;

			Tiles = new WangTile[NumHTiles, NumVTiles];

			var HEdges = TravelEdges(NumHColors);
			var VEdges = TravelEdges(NumVColors);

			for (int j = 0; j < NumVTiles; j++)
			{
				for (int i = 0; i < NumHTiles; i++)
				{
					int left = HEdges[i];
					int bottom = VEdges[j];
					int right = HEdges[i + 1];
					int top = VEdges[j + 1];

					var index = new Index2(i, j);
					var tile = new WangTile(index, left, bottom, right, top, tileSize);

					Tiles[i, j] = tile;
				}
			}

		}

		public WangTile[,] CreateTileMap(int numHTiles, int numVTiles, int seed)
		{
			var tiles = new WangTile[numHTiles, numVTiles];

			for (int i = 0; i < numHTiles; i++)
			{
				for (int j = 0; j < numVTiles; j++)
				{
					tiles[i, j] = new WangTile();
				}
			}

			var rnd = new System.Random(seed);

			for (int j = 0; j < numVTiles; j++)
			{
				for (int i = 0; i < numHTiles; i++)
				{
					int im1 = MathUtil.Wrap(i - 1, numHTiles);
					int ip1 = MathUtil.Wrap(i + 1, numHTiles);
					int jm1 = MathUtil.Wrap(j - 1, numVTiles);
					int jp1 = MathUtil.Wrap(j + 1, numVTiles);

					int left = tiles[im1, j].Right;
					int bottom = tiles[i, jm1].Top;
					int right = tiles[ip1, j].Left;
					int top = tiles[i, jp1].Bottom;

					if (left < 0) left = rnd.Next(0, NumHColors);
					if (bottom < 0) bottom = rnd.Next(0, NumVColors);
					if (right < 0) right = rnd.Next(0, NumHColors);
					if (top < 0) top = rnd.Next(0, NumVColors);

					var index = TileIndex2D(left, bottom, right, top);

					tiles[i, j] = Tiles[index.x, index.y].Copy();
				}
			}

			return tiles;
		}

		private static int[] TravelEdges(int numColors)
		{
			var edges = new int[numColors * numColors + 1];

			for (int i = 0; i < numColors; i++)
			{
				for (int j = 0; j < numColors; j++)
				{
					int index = TileIndex1D(i, j);

					edges[index] = i;
					edges[index + 1] = j;
				}
			}

			return edges;
		}

		public int TileIndex1D(int left, int bottom, int right, int top)
		{
			return (left * (NumVColors * NumHColors * NumVColors) + bottom * (NumHColors * NumVColors) + right * (NumVColors) + top);
		}

		private static int TileIndex1D(int x, int y)
		{
			int index;

			if (x < y)
				index = (2 * x + y * y);
			else if (x == y)
			{
				if (x > 0)
					index = ((x + 1) * (x + 1) - 2);
				else
					index = 0;
			}
			else if (y > 0)
				index = (x * x + 2 * y - 1);
			else
				index = ((x + 1) * (x + 1) - 1);

			return index;
		}

		private static Index2 TileIndex2D(int left, int bottom, int right, int top)
		{
			Index2 index;
			index.x = TileIndex1D(left, right);
			index.y = TileIndex1D(bottom, top);

			return index;
		}

	}

}
