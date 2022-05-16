using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental;
using UnityEditor.Presets;
using UnityEngine;

namespace AperiodicTexturing
{
    /// <summary>
    /// This sample class applies Presets automatically to Assets in the folder containing the Preset and any subfolders.
    /// The code is divided into three parts that set up importer dependencies to make sure the importers of all Assets stay deterministic.
    ///
    /// OnPreprocessAsset:
    /// This method goes from the root folder down to the Asset folder for each imported asset
    /// and registers a CustomDependency to each folder in case a Preset is added/removed at a later time.
    /// It then loads all Presets from that folder and tries to apply them to the Asset importer.
    /// If it is applied, the method adds a direct dependency to each Preset so that the Asset can be re-imported when the Preset values are changed.
    /// </summary>
    public class EnforcePresetPostProcessor : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            // The  if(assetPath....)  line ensures that the asset path starts with "Assets/" so that the AssetPostprocessor is not applied to Assets in a package.
            // The Asset extension cannot end with .cs to avoid triggering a code compilation every time a Preset is created or removed.
            // The Asset extension cannot end with .preset so that Presets don't depend on themselves, which would cause an infinite import loop.
            // There may be more exceptions to add here depending on your project.
            if (assetPath.StartsWith("Assets/") && !assetPath.EndsWith(".cs") && !assetPath.EndsWith(".preset"))
            {
                var path = Path.GetDirectoryName(assetPath);
                ApplyPresetsFromFolderRecursively(path);
            }
        }
        void ApplyPresetsFromFolderRecursively(string folder)
        {
            // Apply Presets in order starting from the parent folder to the Asset so that the Preset closest to the Asset is applied last.
            var parentFolder = Path.GetDirectoryName(folder);
            if (!string.IsNullOrEmpty(parentFolder))
                ApplyPresetsFromFolderRecursively(parentFolder);
            // Add a dependency to the folder Preset custom key
            // so whenever a Preset is added to or removed from this folder, the Asset is re-imported.
            context.DependsOnCustomDependency($"PresetPostProcessor_{folder}");
            // Find all Preset Assets in this folder. Use the System.Directory method instead of the AssetDatabase
            // because the import may run in a separate process which prevents the AssetDatabase from performing a global search.
            var presetPaths =
                Directory.EnumerateFiles(folder, "*.preset", SearchOption.TopDirectoryOnly)
                    .OrderBy(a => a);
            foreach (var presetPath in presetPaths)
            {
                // Load the Preset and try to apply it to the importer.
                var preset = AssetDatabase.LoadAssetAtPath<Preset>(presetPath);
                // The script adds a Presets dependency to an Asset in two cases:
                //1 If the Asset is imported before the Preset, the Preset will not load because it is not yet imported.
                //Adding a dependency between the Asset and the Preset allows the Asset to be re-imported so that Unity loads
                //the assigned Preset and can try to apply its values.
                //2 If the Preset loads successfully, the ApplyTo method returns true if the Preset applies to this Asset's import settings.
                //Adding the Preset as a dependency to the Asset ensures that any change in the Preset values will re-import the Asset using the new values.
                if (preset == null || preset.ApplyTo(assetImporter))
                {
                    // Using DependsOnArtifact here because Presets are native assets and using DependsOnSourceAsset would not work.
                    context.DependsOnArtifact(presetPath);
                }
            }
        }
        /// <summary>
        /// This method with the didDomainReload argument will be called every time the project is being loaded or the code is compiled.
        /// It is very important to set all of the hashes correctly at startup
        /// because Unity does not apply the OnPostprocessAllAssets method to previously imported Presets
        /// and the CustomDependencies are not saved between sessions and need to be rebuilt every time.
        /// </summary>
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets,
            string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (didDomainReload)
            {
                // AssetDatabase.FindAssets uses a glob filter to avoid importing all objects in the project.
                // This glob search only looks for .preset files.
                var allPaths = AssetDatabase.FindAssets("glob:\"**.preset\"")
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .OrderBy(a => a)
                    .ToList();
                bool atLeastOnUpdate = false;
                string previousPath = string.Empty;
                Hash128 hash = new Hash128();
                for (var index = 0; index < allPaths.Count; index++)
                {
                    var path = allPaths[index];
                    var folder = Path.GetDirectoryName(path);
                    if (folder != previousPath)
                    {
                        // When a new folder is found, create a new CustomDependency with the Preset name and the Preset type.
                        if (previousPath != string.Empty)
                        {
                            AssetDatabase.RegisterCustomDependency($"PresetPostProcessor_{previousPath}", hash);
                            atLeastOnUpdate = true;
                        }
                        hash = new Hash128();
                        previousPath = folder;
                    }
                    // Append both path and Preset type to make sure Assets get re-imported whenever a Preset type is changed.
                    hash.Append(path);
                    hash.Append(AssetDatabase.LoadAssetAtPath<Preset>(path).GetTargetFullTypeName());
                }
                // Register the last path.
                if (previousPath != string.Empty)
                {
                    AssetDatabase.RegisterCustomDependency($"PresetPostProcessor_{previousPath}", hash);
                    atLeastOnUpdate = true;
                }
                // Only trigger a Refresh if there is at least one dependency updated here.
                if (atLeastOnUpdate)
                    AssetDatabase.Refresh();
            }
        }
    }
    /// <summary>
    /// InitPresetDependencies:
    /// This method is called when the project is loaded. It finds every imported Preset in the project.
    /// For each folder containing a Preset, create a CustomDependency from the folder name and a hash from the list of Preset names and types in the folder.
    ///
    /// OnAssetsModified:
    /// Whenever a Preset is added, removed, or moved from a folder, the CustomDependency for this folder needs to be updated
    /// so Assets that may depend on those Presets are reimported.
    ///
    /// TODO: Ideally each CustomDependency should also be dependent on the PresetType,
    /// so Textures are not re-imported by adding a new FBXImporterPreset in a folder.
    /// This makes the InitPresetDependencies and OnPostprocessAllAssets methods too complex for the purpose of this example.
    /// Unity suggests having the CustomDependency follow the form "Preset_{presetType}_{folder}",
    /// and the hash containing only Presets of the given presetType in that folder.
    /// </summary>
    public class UpdateFolderPresetDependency : AssetsModifiedProcessor
    {
        /// <summary>
        /// The OnAssetsModified method is called whenever an Asset has been changed in the project.
        /// This methods determines if any Preset has been added, removed, or moved
        /// and updates the CustomDependency related to the changed folder.
        /// </summary>
        protected override void OnAssetsModified(string[] changedAssets, string[] addedAssets, string[] deletedAssets, AssetMoveInfo[] movedAssets)
        {
            HashSet<string> folders = new HashSet<string>();
            foreach (var asset in changedAssets)
            {
                // A Preset has been changed, so the dependency for this folder must be updated in case the Preset type has been changed.
                if (asset.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(asset));
                }
            }
            foreach (var asset in addedAssets)
            {
                // A new Preset has been added, so the dependency for this folder must be updated.
                if (asset.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(asset));
                }
            }
            foreach (var asset in deletedAssets)
            {
                // A Preset has been removed, so the dependency for this folder must be updated.
                if (asset.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(asset));
                }
            }
            foreach (var movedAsset in movedAssets)
            {
                // A Preset has been moved, so the dependency for the previous and new folder must be updated.
                if (movedAsset.destinationAssetPath.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(movedAsset.destinationAssetPath));
                }
                if (movedAsset.sourceAssetPath.EndsWith(".preset"))
                {
                    folders.Add(Path.GetDirectoryName(movedAsset.sourceAssetPath));
                }
            }
            // Do not add a dependency update for no reason.
            if (folders.Count != 0)
            {
                // The dependencies need to be updated outside of the AssetPostprocessor calls.
                // Register the method to the next Editor update.
                EditorApplication.delayCall += () =>
                {
                    DelayedDependencyRegistration(folders);
                };
            }
        }
        /// <summary>
        /// This method loads all Presets in each of the given folder paths
        /// and updates the CustomDependency hash based on the Presets currently in that folder.
        /// </summary>
        static void DelayedDependencyRegistration(HashSet<string> folders)
        {
            foreach (var folder in folders)
            {
                var presetPaths =
                    AssetDatabase.FindAssets("glob:\"**.preset\"", new[] { folder })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .Where(presetPath => Path.GetDirectoryName(presetPath) == folder)
                        .OrderBy(a => a);
                Hash128 hash = new Hash128();
                foreach (var presetPath in presetPaths)
                {
                    // Append both path and Preset type to make sure Assets get re-imported whenever a Preset type is changed.
                    hash.Append(presetPath);
                    hash.Append(AssetDatabase.LoadAssetAtPath<Preset>(presetPath).GetTargetFullTypeName());
                }
                AssetDatabase.RegisterCustomDependency($"PresetPostProcessor_{folder}", hash);
            }
            // Manually trigger a Refresh
            // so that the AssetDatabase triggers a dependency check on the updated folder hash.
            AssetDatabase.Refresh();
        }
    }
}