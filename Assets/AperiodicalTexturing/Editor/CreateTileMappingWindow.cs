
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
    class CreateTileMappingWidow : EditorWindow
    {
        private static int m_numHColors = 2;

        private static int m_numVColors = 2;

        private static int m_mappingWidth = 256;

        private static int m_mappingHeight = 256;

        private static int m_seed = 0;

        private static string m_folderName = "Textures Mapping";

        private static string m_mappingFileName = "AperiodicMapping";

        private int NumTileables => Math.Max(m_numHColors, m_numVColors);

        [MenuItem("Window/Aperiodic Texturing/Create Tile Mapping")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CreateTileMappingWidow));
        }

        private void OnGUI()
        {
            titleContent.text = "Tile mapping creator";
            EditorGUILayout.LabelField("Create the mapping texture.");

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("The mapping texture must be linear, no mipmaps, no compression, and use point sampling.");
  
            EditorGUILayout.Space();

            m_numHColors = Mathf.Clamp(EditorGUILayout.IntField("Number of horizonal colors", m_numHColors), 2, 4);
            m_numVColors = Mathf.Clamp(EditorGUILayout.IntField("Number of vertical colors", m_numVColors), 2, 4);
            m_mappingWidth = Mathf.Max(EditorGUILayout.IntField("Mapping texture width", m_mappingWidth), 2);
            m_mappingHeight = Mathf.Max(EditorGUILayout.IntField("Mapping texture height", m_mappingHeight), 2);

            EditorGUILayout.Space();

            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            if (GUILayout.Button("Generate seed"))
                m_seed = GUID.Generate().GetHashCode();

            EditorGUILayout.Space();

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_mappingFileName = EditorGUILayout.TextField("Mapping file name", m_mappingFileName);

            EditorGUILayout.Space();

            if (GUILayout.Button("Create"))
            {
                if (Validate())
                {
                    var tileSet = new WangTileSet(m_numHColors, m_numVColors, 128);
                    var mapping = tileSet.CreateTileMap(m_mappingHeight, m_mappingWidth, m_seed);

                    SaveMapping(mapping);
                }
            }

            EditorGUI.EndDisabledGroup();

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

        private void SaveMapping(WangTile[,] mapping)
        {
            int width = mapping.GetLength(0);
            int height = mapping.GetLength(1);

            Color32[] pixels = new Color32[width * height];

            for (int i = 0; i < width; i++)
            {
                for (int j = 0; j < height; j++)
                {
                    var index = mapping[i, j].Index2;
                    pixels[i + j * width] = new Color32((byte)index.x, (byte)index.y, 0, 0);
                }
            }

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false, true);
            tex.SetPixels32(pixels);
            tex.Apply();

            string folderName = Application.dataPath + "/" + m_folderName;
            string hv = m_numHColors + "x" + m_numVColors;
            string size = m_mappingWidth + "x" + m_mappingHeight;
            string fileName = folderName + "/" + m_mappingFileName + hv + "_" + size + ".png";

            System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

            Debug.Log("Saved texture " + fileName);

            AssetDatabase.Refresh();

        }

    }

}
