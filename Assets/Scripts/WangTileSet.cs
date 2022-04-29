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

					var tile = new WangTile(left, bottom, right, top, tileSize);
					tile.Index1 = i + j * NumHTiles;
					tile.Index2 = new Point2i(i, j);

					Tiles[i, j] = tile;
				}
			}

		}

		public void AddEdgeColor(int thickness, float alpha)
        {
			foreach (var tile in Tiles)
				tile.AddEdgeColor(thickness, alpha);
        }

		public WangTile[,] CreateTileMap(int numHTiles, int numVTiles, int seed)
		{
			var tiles = new WangTile[numHTiles, numVTiles];

			var rnd = new Random(seed);

			for (int j = 0; j < numVTiles; j++)
			{
				for (int i = 0; i < numHTiles; i++)
				{
					WangTile tile = null;
					int count = 0;

					do
					{
						tile = GetNextTile(i, j, tiles, rnd);
					}
					while (IsNeighbourTile(i, j, tiles, tile) && count++ < 10);

					tiles[i, j] = tile.Copy();
				}
			}

			return tiles;
		}

		private WangTile GetNextTile(int i, int j, WangTile[,] tiles, Random rnd)
        {
			int numHTiles = tiles.GetLength(0);
			int numVTiles = tiles.GetLength(1);

			int im1 = MathUtil.Wrap(i - 1, numHTiles);
			int ip1 = MathUtil.Wrap(i + 1, numHTiles);
			int jm1 = MathUtil.Wrap(j - 1, numVTiles);
			int jp1 = MathUtil.Wrap(j + 1, numVTiles);

			int left = tiles[im1, j] != null ? tiles[im1, j].Right : -1;
			int bottom = tiles[i, jm1] != null ? tiles[i, jm1].Top : -1;
			int right = tiles[ip1, j] != null ? tiles[ip1, j].Left : -1;
			int top = tiles[i, jp1] != null ? tiles[i, jp1].Bottom : -1;

			if (left < 0) left = rnd.Next(0, NumHColors);
			if (bottom < 0) bottom = rnd.Next(0, NumVColors);
			if (right < 0) right = rnd.Next(0, NumHColors);
			if (top < 0) top = rnd.Next(0, NumVColors);

			var index = TileIndex2D(left, bottom, right, top);

			return Tiles[index.x, index.y];
		}

		private bool IsNeighbourTile(int i, int j, WangTile[,] tiles, WangTile tile)
        {
			int numHTiles = tiles.GetLength(0);
			int numVTiles = tiles.GetLength(1);

			for (int x = -1; x <= 1; x++)
            {
				for (int y = -1; y <= 1; y++)
				{
					if (x == 0 && y == 0) continue;

					int ix = MathUtil.Wrap(i - x, numHTiles);
					int jy = MathUtil.Wrap(j - y, numVTiles);

					if (tiles[ix, jy] == null)
						continue;

					if (tiles[ix, jy].Index2 == tile.Index2)
						return true;
				}
			}

			return false;
		}

		private int[] TravelEdges(int numColors)
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

		private int TileIndex1D(int x, int y)
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

		private Point2i TileIndex2D(int left, int bottom, int right, int top)
		{
			Point2i index;
			index.x = TileIndex1D(left, right);
			index.y = TileIndex1D(bottom, top);

			return index;
		}

	}

}
