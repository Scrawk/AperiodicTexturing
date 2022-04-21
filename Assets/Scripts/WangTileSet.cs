using System;
using System.Collections.Generic;

using Common.Core.Colors;
using ImageProcessing.Images;

public class WangTile
{

	public WangTile()
	{
		id = -1;
		e0 = -1;
		e1 = -1;
		e2 = -1;
		e3 = -1;
	}

	public WangTile(int id, int e0, int e1, int e2, int e3, int tileSize)
	{
		this.id = id;
		this.e0 = e0;
		this.e1 = e1;
		this.e2 = e2;
		this.e3 = e3;

		TileSize = tileSize;
		Image = new ColorImage2D(tileSize, tileSize);
	}

	public readonly int id, e0, e1, e2, e3;

	public readonly int TileSize;

	public ColorImage2D Image { get; private set; }

	public WangTile Copy()
    {
		var copy = new WangTile(id, e0, e1, e2, e3, TileSize);
		copy.Image = Image.Copy();
		
		return copy;
    }

}

public class WangTileSet
{
	
	public int NumHColors { get; private set; }

	public int NumVColors { get; private set; }

	public int NumHTiles => NumHColors * NumHColors;

	public int NumVTiles => NumVColors * NumVColors;

	public int NumTiles => Tiles.Length;

	public int TileSize { get; private set; }

	public WangTile[] Tiles { get; private set; }

	private static ColorRGB[] Colors = new ColorRGB[]
	{
			new ColorRGB(1,0,0), new ColorRGB(0,1,0), new ColorRGB(0,0,1),
			new ColorRGB(1,1,0), new ColorRGB(1,0,1), new ColorRGB(0,1,1)
	};

	public WangTileSet(int numHColors, int numVColors, int tileSize)
	{
		NumHColors = numHColors;
		NumVColors = numVColors;
		TileSize = tileSize;

		int size = numHColors*numHColors*numVColors*numVColors;
		
		Tiles = new WangTile[size];
		
		for (int sEdge = 0; sEdge < NumHColors; sEdge++)
		{
			for (int eEdge = 0; eEdge < NumVColors; eEdge++)
			{
				for (int nEdge = 0; nEdge < NumHColors; nEdge++)
				{
					for (int wEdge = 0; wEdge < NumVColors; wEdge++)
					{
						int index = GetIndex(sEdge, eEdge, nEdge, wEdge);
						var tile = new WangTile(index, sEdge, eEdge, nEdge, wEdge, tileSize);

						AddEdgeColor(sEdge, eEdge, nEdge, wEdge, tile.Image);

						Tiles[index] = tile;
					}
				}
			}
		}

	}
	
	public int GetIndex(int e0, int e1, int e2, int e3)
	{
		return (e0*(NumVColors * NumHColors * NumVColors) + e1*(NumHColors * NumVColors) + e2*(NumVColors) + e3);
	}

	private void AddEdgeColor(int sEdge, int eEdge, int nEdge, int wEdge, ColorImage2D col)
	{
		int border = 4;
		float alpha = 0.5f;
		int size = TileSize;

		int num = Colors.GetLength(0);

		for (int i = border; i < size - border; i++)
		{
			for (int j = 0; j < border; j++)
			{
				col[i, j] += Colors[sEdge % num] * alpha;

				col[i, j + size - border] += Colors[nEdge % num] * alpha;

				col[j, i] += Colors[eEdge % num] * alpha;

				col[j + size - border, i] += Colors[wEdge % num] * alpha;
			}
		}
	}

	public WangTile[,] OrthogonalCompaction()
	{
	    int height = NumHColors * NumHColors;
	    int width = NumVColors * NumVColors;
	
        var result = new WangTile[height,width];

		var travelHEdges = TravelEdges(0, NumHColors - 1);
		var travelVEdges = TravelEdges(0, NumVColors - 1);

	    for(int i = 0; i < height; i++)
		{
            for(int j = 0; j < width; j++)
            {
                int hIndex0 = i % (NumHColors * NumHColors);
                int hIndex2 = hIndex0 + 1;
                int vIndex1 = j % (NumVColors * NumVColors);
                int vIndex3 = vIndex1 + 1;
                
                int e0 = travelHEdges[hIndex0];
                int e3 = travelVEdges[vIndex1];
                int e2 = travelHEdges[hIndex2];
                int e1 = travelVEdges[vIndex3];

                int index = GetIndex(e0, e1, e2, e3);

				result[i, j] = Tiles[index].Copy();

			}
	    }

		return result;
	}
	
	public WangTile[,] SequentialTiling(int numRowTiles, int numColTiles, int seed)
	{
        var result = new WangTile[numRowTiles,numColTiles];

		for (int i = 0; i < numRowTiles; i++)
		{
			for (int j = 0; j < numColTiles; j++)
			{
				result[i, j] = new WangTile();
			}
		}

		var rnd = new System.Random(seed);
		
        for(int i = 0; i < numRowTiles; i++)
		{
            for(int j = 0; j < numColTiles; j++)
            {
                int e0, e1, e2, e3;

                e0 = result[(i+numRowTiles-1)%numRowTiles,j].e2;
                e1 = result[i,(j+1)%numColTiles].e3;
                e2 = result[(i+1)%numRowTiles,j].e0;
                e3 = result[i,(j+numColTiles-1)%numColTiles].e1;
                
                if(e0 < 0) e0 = rnd.Next(0, int.MaxValue)% NumHColors;
                if(e1 < 0) e1 = rnd.Next(0, int.MaxValue)% NumVColors;
                if(e2 < 0) e2 = rnd.Next(0, int.MaxValue)% NumHColors;
                if(e3 < 0) e3 = rnd.Next(0, int.MaxValue)% NumVColors;

				int index = GetIndex(e0, e1, e2, e3);
		
                result[i,j] = Tiles[index].Copy();
            }
		}
	    
	    return result;
	}
	
	private static int[] TravelEdges(int startNode, int endNode)
	{
	    int numNodes = (endNode - startNode + 1);
	    
        var result = new int[numNodes*numNodes + 1];
	    
	    for(int i = startNode; i <= endNode; i++)
		{
	        for(int j = startNode; j <= endNode; j++)
	        {
	            int index = EdgeOrdering(i - startNode, j - startNode);
	
	            result[index] = i - startNode;
	            result[index + 1] = j - startNode;
	        }
		}
	    
	    return result;
	}

	private static int EdgeOrdering(int x, int y)
	{
	    if(x < y)
	        return (2*x + y*y);
	    else if(x == y)
	    {
	        if(x > 0)
	            return ((x+1)*(x+1) - 2);
	        else
	            return 0;
	    }
	    else
	    {
	        if(y > 0)
	            return (x*x + 2*y - 1);
	        else
	            return ((x+1)*(x+1) - 1);
	    }
	}

	public static int TileLocation(WangTile[,] tiling, int id, ref int row, ref int col)
	{
	    int result = 0;
	
	    for(int i = 0; i < tiling.GetLength(0); i++)
		{
	        for(int j = 0; j < tiling.GetLength(1); j++)
	        {
	            if(tiling[i,j].id == id)
	            {
	                result++;
	                row = i; col = j;
	            }
	        }
		}
	    
	    return result;
	}

}
