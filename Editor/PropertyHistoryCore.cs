using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PropertyHistoryTool
{
    /// <summary>
    /// Core logic for retrieving property history from Git
    /// </summary>
    public static class PropertyHistoryCore
    {
        /// <summary>
        /// Gets the property history for a given property data
        /// </summary>
        /// <param name="propertyData">The property data to retrieve history for</param>
        /// <returns>List of commit information with property values</returns>
        public static List<CommitInfo> GetPropertyHistory(PropertyData propertyData)
        {
            var result = new List<CommitInfo>();
            
            if (string.IsNullOrEmpty(propertyData.AssetPath))
            {
                return result;
            }

            // If is asset importer, we need to adjust the asset path to point to the actual asset file.
            if (propertyData.TargetObject is AssetImporter)
            {
                // assetPath += ".meta";
                return result; // AssetImporter not supported yet
            }

            // Get Git commit history
            string gitLogArgs = $"log --pretty=format:\"%H|%an|%s\" -- \"{propertyData.AssetPath}\"";
            string allCommitsInfo = GitUtils.RunGitCommand(gitLogArgs);

            if (string.IsNullOrEmpty(allCommitsInfo) || allCommitsInfo.Contains("fatal:"))
            {
                return result; // No Git history found
            }
            
            string[] commitLines = allCommitsInfo.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            object previousValue = null;

            foreach (var commitLine in commitLines)
            {
                string[] parts = commitLine.Split('|');
                if (parts.Length < 3) continue;

                string commitHash = parts[0];
                string author = parts[1];
                string message = parts[2];

                string gitShowArgs = $"show {commitHash}:\"{propertyData.AssetPath}\"";
                string fileContent = GitUtils.RunGitCommand(gitShowArgs);

                if (string.IsNullOrEmpty(fileContent))
                {
                    continue;
                }

                // Extract property value from this commit
                var currentValue = YamlValueExtractor.ExtractPropertyValue(fileContent, propertyData.FileID, propertyData.PropertyPath);

                // Only add if this is the first commit or the value changed
                if (result.Count == 0 || !Equals(currentValue, previousValue))
                {
                    result.Add(new CommitInfo(commitHash, author, message, currentValue));
                    previousValue = currentValue;
                }
            }

            return result;
        }
        
        /// <summary>
        /// Tries to get the File ID for a target object
        /// </summary>
        public static bool TryGetFileID(Object targetObject, out long fileID)
        {
            fileID = 0;
            bool fileIdFound = false;

            // Determine the correct object to get the File ID from.
            // If it's a prefab instance, we need the source asset.
            if (PrefabUtility.IsPartOfPrefabInstance(targetObject))
            {
                var objectForFileId = PrefabUtility.GetCorrespondingObjectFromSource(targetObject);
                // If we have a valid object (either the original or the prefab source), try to get its ID.
                if (objectForFileId != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(objectForFileId, out _, out fileID))
                    fileIdFound = true;
            }

            // If that fails, it might be a scene object, or we are in the prefab editor (Prefab Stage).
            if (!fileIdFound)
            {
                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject);
                if (globalId.identifierType is (int)GlobalObjectIdType.ImportedAsset or (int)GlobalObjectIdType.SceneObject)
                {
                    fileID = (long)globalId.targetObjectId;
                    fileIdFound = true;
                }
            }

            return fileIdFound;
        }
        
        /// <summary>
        /// Gets the asset path for a Unity object
        /// </summary>
        public static string GetAssetPath(Object obj)
        {
            // First, try the most direct method to get an asset path. This works for assets in the Project window.
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                return path;
            }

            // If the object is a component, it might be in a scene or in the prefab editor.
            if (obj is not Component component)
            {
                return null;
            }

            GameObject go = component.gameObject;

            // Check if we are in Prefab Stage (i.e., editing a prefab asset directly).
            var prefabStage = PrefabStageUtility.GetPrefabStage(go);
            if (prefabStage != null && !string.IsNullOrEmpty(prefabStage.assetPath))
            {
                return prefabStage.assetPath;
            }

            // Check if it's an instance of a prefab in a scene.
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabPath))
            {
                return prefabPath;
            }

            // If it's not a prefab, it might be a regular object in a saved scene.
            if (go.scene.path != null && !string.IsNullOrEmpty(go.scene.path))
            {
                return go.scene.path;
            }

            // If all checks fail, we cannot determine the asset path.
            return null;
        }
        
        /// <summary>
        /// Prepares PropertyData from a SerializedProperty
        /// </summary>
        public static bool PreparePropertyData(SerializedProperty property, out PropertyData propertyData)
        {
            propertyData = PropertyData.FromSerializedProperty(property);
            propertyData.AssetPath = GetAssetPath(propertyData.TargetObject);

            if (string.IsNullOrEmpty(propertyData.AssetPath))
            {
                return false;
            }

            if (propertyData.TargetObject is AssetImporter)
            {
                // TODO: Support for AssetImporter
                return false;
            }

            if (!TryGetFileID(propertyData.TargetObject, out long fileID))
            {
                return false;
            }

            propertyData.FileID = fileID;
            return true;
        }
        
        private enum GlobalObjectIdType
        {
            Null = 0,
            ImportedAsset = 1,
            SceneObject = 2,
            SourceAsset = 3,
        }
    }
}