using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PropertyHistoryContextLogger
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;
    }

    private static void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property)
    {
        var propertyCopy = property.Copy();
        menu.AddItem(new GUIContent("Show Full Property Git History"), false, () =>
        {
            ShowPropertyHistory(propertyCopy);
        });
    }

    private static void ShowPropertyHistory(SerializedProperty property)
    {
        var logMessage = new StringBuilder();
        Object targetObject = property.serializedObject.targetObject;
        string assetPath = GetAssetPath(targetObject);

        if (string.IsNullOrEmpty(assetPath))
        {
            Debug.LogWarning("Could not find asset path for the selected object.");
            return;
        }

        // TODO: If is asset importer, we need to adjust the asset path to point to the actual asset file.
        if (targetObject is AssetImporter importer)
        {
            // assetPath += ".meta";
            Debug.LogWarning("AssetImporter detected. Full property history for AssetImporters is not yet supported.");
            return;
        }

        if (!TryGetFileID(targetObject, out long fileID))
        {
            Debug.LogWarning($"Could not get File ID for object '{targetObject.name}'. Object is not a persistent asset.");
            return;
        }

        Debug.Log($"[PropertyHistory] AssetPath: {assetPath}, FileID: {fileID}, PropertyPath: {property.propertyPath}");

        string gitLogArgs = $"log --pretty=format:\"%H|%an|%s\" -- \"{assetPath}\"" ;
        string allCommitsInfo = GitUtils.RunGitCommand(gitLogArgs);

        if (string.IsNullOrEmpty(allCommitsInfo) || allCommitsInfo.Contains("fatal:"))
        {
            logMessage.AppendLine($"--- Git History for {property.displayName} ---");
            logMessage.AppendLine($"<b>Asset Path:</b> {assetPath}");
            logMessage.AppendLine("<b>No Git history found for this file.</b>");
            Debug.Log(logMessage.ToString());
            return;
        }
        
        string[] commitLines = allCommitsInfo.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        logMessage.AppendLine($"--- Git History for {property.propertyPath} ---");
        logMessage.AppendLine($"<b>Asset Path:</b> {assetPath}");
        logMessage.AppendLine("-----------------------------------------");

        object previousValue = null;
        int changesFound = 0;

        foreach (var commitLine in commitLines)
        {
            string[] parts = commitLine.Split('|');
            if (parts.Length < 3) continue;

            string commitHash = parts[0];
            string author = parts[1];
            string message = parts[2];

            string gitShowArgs = $"show {commitHash}:\"{assetPath}\"" ;
            string fileContent = GitUtils.RunGitCommand(gitShowArgs);

            if (string.IsNullOrEmpty(fileContent))
            {
                continue;
            }

            // var currentValue = ImportedAssetValueExtractor.ExtractOldValue(fileContent, property, fileID);
            var currentValue = YamlValueExtractor.ExtractPropertyValue(fileContent, fileID, property.propertyPath);

            if (changesFound == 0 || !Equals(currentValue, previousValue))
            {
                logMessage.AppendLine($"<b>Commit:</b> {commitHash.Substring(0, 7)}");
                logMessage.AppendLine($"<b>Author:</b> {author}");
                logMessage.AppendLine($"<b>Message:</b> {message}");
                logMessage.AppendLine($"<b>Value:</b> {currentValue ?? "[null]"}");
                logMessage.AppendLine("-----------------------------------------");

                previousValue = currentValue;
                changesFound++;
            }
        }

        if (changesFound == 0)
        {
            logMessage.AppendLine("<b>No changes found for this property in the file's history.</b>");
        }

        Debug.Log(logMessage.ToString());
    }

    private static bool TryGetFileID(Object targetObject, out long fileID)
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

    private static string GetAssetPath(Object obj)
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
    
    private enum GlobalObjectIdType
    {
        Null = 0,
        ImportedAsset = 1,
        SceneObject = 2,
        SourceAsset = 3,
    }
}
