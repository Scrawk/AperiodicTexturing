
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

using UnityEngine;
using UnityEditor;

using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{
    class CreateAperiodicTilesWidow : EditorWindow
    {

        private static Texture2D[] m_tileables;

        private static int m_numHColors = 2;

        private static int m_numVColors = 2;

        private static int m_mappingWidth = 8;

        private static int m_mappingHeight = 8;

        private static int m_tileSize = 128;

        private static int m_seed = 0;

        private static bool m_addEdgeColors;

        private static string m_folderName = "Textures Results";

        private static string m_tileFileName = "AperiodicTile";

        private static string m_mappingFileName = "AperiodicMapping";

        private bool m_isRunning;

        private static ColorImage2D[] m_images;

        private static WangTile[,] m_mapping;

        private static WangTileSet m_tileSet;

        private int NumTileables => Math.Max(m_numHColors, m_numVColors);

        [MenuItem("Window/Aperiodic Texturing/Create Aperiodic Tiles")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CreateAperiodicTilesWidow));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create the aperoidic tile and mappinp texture.");

            EditorGUILayout.Space();

            EditorGUI.BeginDisabledGroup(m_isRunning);

            m_numHColors = EditorGUILayout.IntField("Number of horizonal colors", m_numHColors);
            m_numVColors = EditorGUILayout.IntField("Number of vertical colors", m_numVColors);
            m_mappingWidth = EditorGUILayout.IntField("Mapping texture width", m_mappingWidth);
            m_mappingHeight = EditorGUILayout.IntField("Mapping texture height", m_mappingHeight);
            m_tileSize = EditorGUILayout.IntField("Tile size", m_tileSize);
            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            m_addEdgeColors = EditorGUILayout.Toggle("Create edge color tile", m_addEdgeColors);

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_tileFileName = EditorGUILayout.TextField("Tile file name", m_tileFileName);
            m_mappingFileName = EditorGUILayout.TextField("Mapping file name", m_mappingFileName);

            EditorGUILayout.Space();

            if (m_tileables == null)
                m_tileables = new Texture2D[4];

            m_tileables[0] = (Texture2D)EditorGUILayout.ObjectField("Tileable0", m_tileables[0], typeof(Texture2D), false);
            m_tileables[1] = (Texture2D)EditorGUILayout.ObjectField("Tileable1", m_tileables[1], typeof(Texture2D), false);
            m_tileables[2] = (Texture2D)EditorGUILayout.ObjectField("Tileable2", m_tileables[2], typeof(Texture2D), false);
            m_tileables[3] = (Texture2D)EditorGUILayout.ObjectField("Tileable3", m_tileables[3], typeof(Texture2D), false);

            EditorGUILayout.Space();

            if (GUILayout.Button(GetButtonText()))
            {
                if (Validate())
                {
                    m_images = new ColorImage2D[NumTileables];
                    for (int i = 0; i < NumTileables; i++)
                        m_images[i] = ToImage(m_tileables[i]);

                    m_tileSet = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);

                    m_isRunning = true;

                    Run();

                    //EditorUtility.DisplayProgressBar("Creating tiles", "Running (This could take awhile)", 0);
                }
            }

            EditorGUI.EndDisabledGroup();

            //if(!m_isRunning)
            //    EditorUtility.ClearProgressBar();

        }

        private string GetButtonText()
        {
            if (!m_isRunning)
                return "Create";
            else
                return "Running (This could take awhile)";
        }

        private bool Validate()
        {
            for (int i = 0; i < NumTileables; i++)
            {
                if (m_tileables[i] == null)
                {
                    Debug.Log("Tileable" + i + " texture is null.");
                    return false;
                }

                if (!m_tileables[i].isReadable)
                {
                    Debug.Log("Tileable" + i + " texture is not readable.");
                    return false;
                }
            }

            string folderName = Application.dataPath + "/" + m_folderName;

            if (!System.IO.Directory.Exists(folderName))
            {
                Debug.Log("Output folder does not exist.");
                Debug.Log(folderName);
                return false;
            }

            return true;
        }

        private async void Run()
        {

            await Task.Run(() =>
            {
                foreach (var tile in m_tileSet.Tiles)
                {
                    ImageSynthesis.CreateWangTileImage(tile, m_images);
                }

                m_mapping = m_tileSet.CreateTileMap(m_mappingHeight, m_mappingWidth, m_seed);

            }).ContinueWith((task) =>
            {
                SaveTiles(false);

                if(m_addEdgeColors)
                {
                    foreach (var tile in m_tileSet.Tiles)
                        tile.AddEdgeColor(4, 0.5f);

                    SaveTiles(true);
                }

                SaveMapping();
                m_isRunning = false;

            }, TaskScheduler.FromCurrentSynchronizationContext());

        }

        private void SaveTiles(bool hasEdgeColors)
        {
            int tileTextureWidth = m_tileSet.NumHTiles;
            int tileTextureHeight = m_tileSet.NumVTiles;

            int width = m_tileSize * tileTextureWidth;
            int height = m_tileSize * tileTextureHeight;

            Color[] pixels = new Color[width * height];

            for (int x = 0; x < tileTextureWidth; x++)
            {
                for (int y = 0; y < tileTextureHeight; y++)
                {
                    var tile = m_tileSet.Tiles[x, y];

                    for (int i = 0; i < m_tileSize; i++)
                    {
                        for (int j = 0; j < m_tileSize; j++)
                        {
                            int xi = x * m_tileSize + i;
                            int yj = y * m_tileSize + j;

                            pixels[xi + yj * width] = tile.Image[i, j].ToColor();
                        }
                    }
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string folderName = Application.dataPath + "/" + m_folderName;
            string hv = m_numHColors + "x" + m_numVColors;
            string colors = hasEdgeColors ? "_Colors" : "";
            string fileName = folderName + "/" + m_tileFileName + hv + colors  +".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

            AssetDatabase.Refresh();
        }

        private void SaveMapping()
        {
            int width = m_mapping.GetLength(0);
            int height = m_mapping.GetLength(1);

            Color32[] pixels = new Color32[width * height];

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    var index = m_mapping[i, j].Index;
                    pixels[i + j * width] = new Color32((byte)index.x, (byte)index.y, 0, 0);
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false, true);
            tex.SetPixels32(pixels);
            tex.Apply();

            string folderName = Application.dataPath + "/" + m_folderName;
            string hv = m_numHColors + "x" + m_numVColors;
            string fileName = folderName + "/" + m_mappingFileName + hv + ".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

        }

        private ColorImage2D ToImage(Texture2D tex)
        {
            var image = new ColorImage2D(tex.width, tex.height);

            image.Fill((x, y) =>
            {
                return tex.GetPixel(x, y).ToColorRGB();
            });

            return image;
        }

    }

}
