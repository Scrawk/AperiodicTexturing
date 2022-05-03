
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
    class CreateColorTilesWidow : EditorWindow
    {
        private static int m_numHColors = 2;

        private static int m_numVColors = 2;

        private static int m_tileSize = 128;

        private static int m_thickness = 4;

        private static float m_alpha = 1;

        private static string m_folderName = "Textures Results";

        private static string m_tileFileName = "ColorTile";

        private static bool m_appendTileSize = true;

        private static Color m_backGroundColor = Color.black;

        private int NumTileables => Math.Max(m_numHColors, m_numVColors);

        [MenuItem("Window/Aperiodic Texturing/Create Color Tiles (Debug)")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CreateColorTilesWidow));
        }

        private void OnGUI()
        {
            titleContent.text = "Color tile creator";
            EditorGUILayout.LabelField("Create the color tiles for debugging.");

            EditorGUILayout.Space();

            m_numHColors = Mathf.Clamp(EditorGUILayout.IntField("Number of horizonal colors", m_numHColors), 2, 4);
            m_numVColors = Mathf.Clamp(EditorGUILayout.IntField("Number of vertical colors", m_numVColors), 2, 4);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile size", m_tileSize), 64);
            m_thickness = Mathf.Clamp(EditorGUILayout.IntField("Thickness", m_thickness), 1, 32);
            m_alpha = Mathf.Clamp(EditorGUILayout.FloatField("Aplha", m_alpha), 0, 1);
            m_backGroundColor = EditorGUILayout.ColorField("Back ground color", m_backGroundColor);

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_tileFileName = EditorGUILayout.TextField("Tile file name", m_tileFileName);
            m_appendTileSize = EditorGUILayout.Toggle("Append tile size", m_appendTileSize);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create"))
            {
                if (Validate())
                {
                    Run();
                }
            }

        }

        private bool Validate()
        {
            string folderName = Application.dataPath + "/" + m_folderName;

            if (!System.IO.Directory.Exists(folderName))
            {
                Debug.Log("Output folder does not exist.");
                Debug.Log(folderName);
                return false;
            }

            return true;
        }

        private void Run()
        {

            var set = new WangTileSet(m_numHColors, m_numVColors, m_tileSize);
            set.AddEdgeColor(m_thickness, 1);

            int tileTextureWidth = set.NumHTiles;
            int tileTextureHeight = set.NumVTiles;

            int width = m_tileSize * tileTextureWidth;
            int height = m_tileSize * tileTextureHeight;

            Color[] pixels = new Color[width * height];

            for (int x = 0; x < tileTextureWidth; x++)
            {
                for (int y = 0; y < tileTextureHeight; y++)
                {
                    var tile = set.Tiles[x, y];

                    for (int i = 0; i < m_tileSize; i++)
                    {
                        for (int j = 0; j < m_tileSize; j++)
                        {
                            int xi = x * m_tileSize + i;
                            int yj = y * m_tileSize + j;

                            var col = tile.Image[i, j].ToColor();

                            if (col == Color.black)
                                pixels[xi + yj * width] = m_backGroundColor;
                            else
                                pixels[xi + yj * width] = Color.Lerp(m_backGroundColor, col, m_alpha);    
                        }
                    }
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);
            tex.SetPixels(pixels);
            tex.Apply();

            string folderName = Application.dataPath + "/" + m_folderName;
            string hv = m_numHColors + "x" + m_numVColors;
            string size = m_appendTileSize ? "_" + m_tileSize : "";
            string fileName = folderName + "/" + m_tileFileName + hv + size + ".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

            AssetDatabase.Refresh();
        }

    }

}
