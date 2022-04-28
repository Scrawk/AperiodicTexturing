
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using UnityEngine;
using UnityEditor;

using Common.Core.Colors;
using ImageProcessing.Images;

namespace AperiodicTexturing
{
    class CreateTileablesWidow : EditorWindow
    {

        private static Texture2D m_source;

        private static int m_numTiles = 1;

        private static int m_tileSize = 128;

        private static int m_seed = 0;

        private static string m_folderName = "Results";

        private static string m_fileName = "Tile";

        private ColorImage2D[] m_tiles;

        private ColorImage2D m_image;

        private ExemplarSet m_set;

        private bool m_isRunning, m_outputSaved;

        [MenuItem("Window/Aperiodic Texturing/Create Tileable Images")]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(CreateTileablesWidow));
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Create a number of tileable textures from the source texture.");

            m_numTiles = EditorGUILayout.IntField("Number of tiles", m_numTiles);
            m_tileSize = EditorGUILayout.IntField("Tile Size", m_tileSize);
            m_seed = EditorGUILayout.IntField("Seed", m_seed);

            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);
            m_fileName = EditorGUILayout.TextField("File name", m_fileName);

            m_source = (Texture2D)EditorGUILayout.ObjectField("Source", m_source, typeof(Texture2D), false);

            EditorGUI.BeginDisabledGroup(m_isRunning);

            if (GUILayout.Button(GetButtonText()))
            {
                if(Validate())
                {
                    m_image = ToImage(m_source);

                    m_set = new ExemplarSet(m_image, m_tileSize);
                    m_set.CreateExemplarsFromRandom(m_seed, m_numTiles, 0.5f);
                    m_tiles = new ColorImage2D[m_set.Count];

                    Run();
                }
            }

            EditorGUI.EndDisabledGroup();

            if(!m_isRunning && !m_outputSaved && m_tiles != null)
            {
                SaveTiles();
                m_outputSaved = true;
                m_tiles = null;
            }

        }

        private string GetButtonText()
        {
            if (!m_isRunning)
                return "Create tiles";
            else
                return "Running (This could take awhile)";
        }

        private bool Validate()
        {
            if (m_source == null)
            {
                Debug.Log("Source texture is null.");
                return false;
            }

            if (!m_source.isReadable)
            {
                Debug.Log("Source texture is not readable.");
                return false;
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
                m_isRunning = true;

                if (m_set.Count < m_numTiles)
                {
                    Debug.Log("Failed to find the required number of tiles in sources texture.");
                    return;
                }

                for (int i = 0; i < m_set.Count; i++)
                {
                    var exemplar = m_set.Exemplars[i];
                    m_tiles[i] = ImageSynthesis.CreateTileableImage(exemplar.Image, m_set);
                }

                m_isRunning = false;
            });
         }

        private void SaveTiles()
        {
            string folderName = Application.dataPath + "/" + m_folderName;

            for (int i = 0; i < m_tiles.Length; i++)
            {
                var tile = m_tiles[i];
                if (tile == null) continue;

                var tex = ToTexture(tile);

                string fileName = folderName + "/" + m_fileName + i + ".png";

                System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

                Debug.Log("Saved tile " + fileName);
            }

            AssetDatabase.Refresh();
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

        private Texture2D ToTexture(ColorImage2D image)
        {
            var tex = new Texture2D(image.Width, image.Height, TextureFormat.ARGB32, false);

            for (int x = 0; x < image.Width; x++)
            {
                for (int y = 0; y < image.Height; y++)
                {
                    tex.SetPixel(x, y, image[x, y].ToColor());
                }
            }

            tex.Apply();

            return tex;
        }

    }

}
