
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

using UnityEngine;
using UnityEditor;

using Common.Core.Threading;
using Common.Core.Time;
using ImageProcessing.Images;

namespace AperiodicTexturing
{

    /// <summary>
    /// A editoer script that takes a series of images and creates tileable textures.
    /// </summary>
    class CreateTileablesWidow : EditorWindow
    {

        /// <summary>
        /// The source images the tiles will be created from.
        /// </summary>
        private static Texture2D[] m_source;

        /// <summary>
        /// The number of tiles to be created.
        /// </summary>
        private static int m_numTiles = 1;

        /// <summary>
        /// THe tiles size.
        /// </summary>
        private static int m_tileSize = 256;

        /// <summary>
        /// The area around the tiles border that
        /// can be blended with another tile.
        /// Has a big effect on how fast the algorithm will.
        /// </summary>
        private static int m_blendArea = 16;

        /// <summary>
        /// Number of times to sample the source 
        /// image when creating the exemplar set.
        /// </summary>
        private static int m_samples = 100;

        /// <summary>
        /// The weights used for each source image and are 
        /// used to adjust its contribution when comparing tiles.
        /// </summary>
        private static float[] m_weights;

        /// <summary>
        /// The seed used for the random generator.
        /// </summary>
        private static int m_seed = 0;

        /// <summary>
        /// What variants should the exemplar set create.
        /// Will results in more variations being create
        /// that could result in better matches.
        /// </summary>
        private static EXEMPLAR_VARIANT m_varients;

        /// <summary>
        /// Should muti-threading be used when creating the tiles.
        /// </summary>
        private static bool m_useThreading = true;

        /// <summary>
        /// Is the source image provided tileable.
        /// This can makes sampling the texture easier if true.
        /// </summary>
        private static bool m_sourceIsTileable = true;

        /// <summary>
        /// Folder the results will be output to.
        /// The default folder contains a preset that will 
        /// automatically change the textures settings to what is reconmmended.
        /// </summary>
        private static string m_folderName = "Textures Results";

        /// <summary>
        /// The files names of the output tiles
        /// </summary>
        private static string[] m_fileNames;

        /// <summary>
        /// The tiles images created from source texture.
        /// </summary>
        private List<ColorImage2D> m_images;

        /// <summary>
        /// The tiles passed into the algorithm to make tileable.
        /// </summary>
        private List<Tile> m_tiles;

        /// <summary>
        /// The tiles created by the algorithm.
        /// </summary>
        private Tile[] m_tileables;

        /// <summary>
        /// The exemplar set the tiles are created from.
        /// </summary>
        private ExemplarSet m_set;

        private bool m_isRunning;

        private Exception m_exception;

        private ThreadingToken m_token;

        private string m_message;

        /// <summary>
        /// 
        /// </summary>
        [MenuItem("Window/Aperiodic Texturing/Create Tileable Images")]
        public static void ShowWindow()
        {
            var window = EditorWindow.GetWindow(typeof(CreateTileablesWidow));
            window.minSize = new Vector2(500, 700);
            window.maxSize = new Vector2(500, 700);
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnGUI()
        {
            DrawTitle();

            EditorGUILayout.Space();
            EditorGUI.BeginDisabledGroup(m_isRunning);

            DrawMain();

            EditorGUILayout.Space();

            DrawSeed();

            EditorGUILayout.Space();

            DrawFileNames();

            EditorGUILayout.Space();

            DrawWeights();

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();

            DrawSourceTextures();

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            if (GUILayout.Button(GetRunButtonText()))
            {
                if(Validate())
                {
                    CreateImages();
                    CreateTiles();
                    CreateExemplarSet();
                    ResetBeforeRunning();
                    Run();
                }
            }

            EditorGUI.EndDisabledGroup();

        }

        /// <summary>
        /// 
        /// </summary>
        private void Update()
        {
            // if running update the progress bar.

            if (m_isRunning && m_token != null)
            {
                //Find the amount of progress that has happened from the token.
                float progress = m_token.PercentageProgress();
                string estimatedTime = "";

                //If the algorithm has progressed at least 10% then
                //dispay this as the progress, if not just show thats its calculating.
                if (progress > 0.1f)
                    estimatedTime = m_token.EstimatedCompletionTime().ToString("F2") + m_token.TimePeriodUnit;
                else
                    estimatedTime = "(Calculating...)";

                //There will be messages from the token about what stage the agorithm is at.
                if (m_token.NumMessages > 0)
                    m_message = m_token.DequeueMessage();

                EditorUtility.DisplayProgressBar("Creating tiles", m_message + " Estimated completion time " + estimatedTime, progress);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void OnDestroy()
        {
            EditorUtility.ClearProgressBar();

            if (m_token != null)
                m_token.Cancelled = true;
        }

        /// <summary>
        /// Draw the title and description.
        /// </summary>
        private void DrawTitle()
        {
            var wrapStyle = new GUIStyle(GUI.skin.GetStyle("label"));
            wrapStyle.wordWrap = true;

            titleContent.text = "Tileable texture creator";
            EditorGUILayout.LabelField("Create a number of tileable textures from the source texture.", wrapStyle);
        }

        /// <summary>
        /// Draw the main block of properties.
        /// </summary>
        private void DrawMain()
        {
            m_numTiles = Mathf.Max(EditorGUILayout.IntField("Number of tiles", m_numTiles), 1);
            m_tileSize = Mathf.Max(EditorGUILayout.IntField("Tile Size", m_tileSize), 128);
            m_blendArea = Mathf.Clamp(EditorGUILayout.IntField("Blend area", m_blendArea), 8, 32);
            m_samples = Mathf.Max(EditorGUILayout.IntField("Samples", m_samples), 4);
            m_varients = (EXEMPLAR_VARIANT)EditorGUILayout.EnumFlagsField("Varients", m_varients);
            m_useThreading = EditorGUILayout.Toggle("Use multi-threading", m_useThreading);
            m_sourceIsTileable = EditorGUILayout.Toggle("Source is tileable", m_sourceIsTileable);
        }

        /// <summary>
        /// Draw the seed and the generate button.
        /// </summary>
        private void DrawSeed()
        {
            m_seed = EditorGUILayout.IntField("Seed", m_seed);
            if (GUILayout.Button("Generate seed"))
                m_seed = GUID.Generate().GetHashCode();
        }

        /// <summary>
        /// Draw the folder location and the tile names.
        /// </summary>
        private void DrawFileNames()
        {
            m_folderName = EditorGUILayout.TextField("Output folder", m_folderName);

            if (m_fileNames == null)
            {
                m_fileNames = new string[]
                {
                    "Tileable",
                    "Tileable",
                    "Tileable",
                    "Tileable",
                };
            }

            for (int i = 0; i < 4; i++)
                m_fileNames[i] = EditorGUILayout.TextField("File name " + i, m_fileNames[i]);
        }

        /// <summary>
        /// Draw the weights that will be assigned to each tile.
        /// </summary>
        private void DrawWeights()
        {
            if (m_weights == null)
            {
                m_weights = new float[]
                {
                    1, 0, 0, 0
                };
            }
                
            for (int i = 0; i < m_weights.Length; i++)
                m_weights[i] = Math.Max(0, EditorGUILayout.FloatField("Weights", m_weights[i]));
        }

        /// <summary>
        /// Draw the sources images.
        /// </summary>
        private void DrawSourceTextures()
        {
            var wrapStyle = new GUIStyle(GUI.skin.GetStyle("label"));
            wrapStyle.wordWrap = true;

            EditorGUILayout.LabelField("Source textures. Albedo then 3 optional textures.", wrapStyle, GUILayout.Width(150));

            var texOptions = new GUILayoutOption[]
            {
                GUILayout.Width(64),
                GUILayout.Height(64)
            };

            if (m_source == null)
                m_source = new Texture2D[4];

            for (int i = 0; i < 4; i++)
                m_source[i] = (Texture2D)EditorGUILayout.ObjectField(m_source[i], typeof(Texture2D), false, texOptions);
        }

        /// <summary>
        /// Gets what the text on the run button should be.
        /// </summary>
        /// <returns></returns>
        private string GetRunButtonText()
        {
            if (!m_isRunning)
                return "Create";
            else
                return "Running";
        }

        /// <summary>
        /// Check that the input is valid before running the algorithm.
        /// </summary>
        /// <returns></returns>
        private bool Validate()
        {
            int width = 0;
            int height = 0;

            for(int i = 0; i < 4; i++)
            {
                //The first texture can not be null but all the others are optional.
                var tex = m_source[i];
                if (tex == null && i != 0) continue;

                //The texture can not be null.
                if (tex == null)
                {
                    Debug.Log($"Source texture {i} is null.");
                    return false;
                }

                //The texture must be readable
                if (!tex.isReadable)
                {
                    Debug.Log($"Source texture {i} is not readable.");
                    return false;
                }

                //If the is the first texture get its size.
                //If not the first texture then check thats its the same size as first.
                if (i == 0)
                {
                    width = tex.width;
                    height = tex.height;
                }
                else if (tex.width != width || tex.height != height)
                {
                    Debug.Log("Source textures must all be the same size .");
                    return false;
                }

            }

            //Check that the weights are not all 0.

            float sum = 0;
            foreach (var w in m_weights)
                sum += w;

            if(sum == 0)
            {
                Debug.Log("At least one tile must have a weight greater than 0.");
                return false;
            }

            //Check that the output foldere exists.

            string folderName = Application.dataPath + "/" + m_folderName;

            if (!System.IO.Directory.Exists(folderName))
            {
                Debug.Log("Output folder does not exist.");
                Debug.Log(folderName);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Run the algorithm on a seperate thread as to 
        /// not block the main application.
        /// </summary>
        private async void Run()
        {

            await Task.Run(() =>
            {
                try
                {
                    m_token.StartTimer();

                    //Make the tiles tileable.
                    m_tileables = ImageSynthesis.CreateTileableImages_TEST(m_tiles, m_set, m_blendArea, m_token);

                    Debug.Log("Tile creation time: " + m_token.StopTimer() + "s");
                }
                catch(Exception e)
                {
                    m_exception = e;
                }

            }).ContinueWith((task) =>
            {
                //Once the tiles have been created then continue onto the main thread.
                //Check if the algorithm had a exception while running.
                //If not save the tiles.

                if(m_exception != null)
                {
                    Debug.Log("Failed to create textures due to a exception.");
                    Debug.Log(m_exception);
                }
                else
                {
                    SaveTiles();
                }

                m_isRunning = false;
                EditorUtility.ClearProgressBar();

            }, TaskScheduler.FromCurrentSynchronizationContext());

        }
        
        /// <summary>
        /// 
        /// </summary>
        private void SaveTiles()
        {
            string folderName = Application.dataPath + "/" + m_folderName;

            for (int i = 0; i < m_tileables.Length; i++)
            {
                var tile = m_tileables[i];

                for (int j = 0; j < tile.Count; j++)
                {
                    var tex = ToTexture(tile.Images[j]);
                    var id = i.ToString() + j.ToString();

                    string fileName = folderName + "/" + m_fileNames[j] + id + ".png";

                    System.IO.File.WriteAllBytes(fileName, tex.EncodeToPNG());

                    Debug.Log("Saved texture " + fileName);
                }

            }

            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 
        /// </summary>
        private void ResetBeforeRunning()
        {
            m_isRunning = true;
            m_exception = null;
            m_message = "";
            m_token = new ThreadingToken();
            m_token.UseThreading = m_useThreading;
            m_token.TimePeriodFormat = TIME_PERIOD.SECONDS;
        }

        /// <summary>
        /// 
        /// </summary>
        private void CreateExemplarSet()
        {
            //Create the exemplars from the images.
            m_set = new ExemplarSet(m_images, m_sourceIsTileable, 16);

            //Create the required number of tiles needed by randomly sampling frm the exemplar.
            //m_set.CreateExemplarsFromRandom(m_samples, m_seed, 0.25f);
            m_set.CreateExemplarsFromCrop();
            m_set.CreateMipmaps();

            //Create the variants from the exempars in the set.
            //m_set.CreateVariants(m_varients);

            //Check if the required number of tiles have been created.
            //if (m_set.Count < m_numTiles)
            //{
            //    Debug.Log("Failed to find the required number of tiles in sources texture. Use a larger source texture or a smaller tile size.");
            //    return;
            //}

            Debug.Log("m_set " + m_set);
        }

        private void CreateTiles()
        {
            var set = new ExemplarSet(m_images, m_sourceIsTileable, m_tileSize);

            set.CreateExemplarsFromRandom(m_numTiles, m_seed, 0.1f);
            m_tiles = set.GetRandomTiles(m_numTiles, m_seed);

            //Assign the weights to the tiles and name the images (for debugging).
            for (int i = 0; i < m_tiles.Count; i++)
            {
                var tile = m_tiles[i];
                tile.SetWeights(m_weights);

                for (int j = 0; j < tile.Images.Count; j++)
                {
                    tile.Images[j].Name = $"Tile{i}_Image{j}";
                }
            }

            Debug.Log("set " + set);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private void CreateImages()
        {
            m_images = new List<ColorImage2D>();
            for (int i = 0; i < m_source.Length; i++)
            {
                if (m_source[i] == null) continue;
                m_images.Add(ToImage(m_source[i]));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tex"></param>
        /// <returns></returns>
        private ColorImage2D ToImage(Texture2D tex)
        {
            var image = new ColorImage2D(tex.width, tex.height);

            image.Fill((x, y) =>
            {
                return tex.GetPixel(x, y).ToColorRGBA();
            });

            image.Name = tex.name;

            return image;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
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
