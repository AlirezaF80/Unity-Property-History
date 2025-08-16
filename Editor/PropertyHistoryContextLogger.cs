using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

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

        GlobalObjectId globalId = GlobalObjectId.GetGlobalObjectIdSlow(targetObject);
        if (globalId.identifierType != 2) // 2 = Asset-based object
        {
            Debug.LogWarning($"Could not get File ID for object '{targetObject.name}'. Object is not a persistent asset.");
            return;
        }

        long fileID = (long)globalId.targetObjectId;

        if (targetObject is AssetImporter)
        {
            assetPath += ".meta";
        }

        string gitLogArgs = $"log --pretty=format:\"%H|%an|%s\" -- \"{assetPath}\"";
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

            string gitShowArgs = $"show {commitHash}:\"{assetPath}\"";
            string fileContent = GitUtils.RunGitCommand(gitShowArgs);

            if (string.IsNullOrEmpty(fileContent))
            {
                continue;
            }

            var currentValue = ExtractOldValue(fileContent, property, fileID);

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

    private static object ExtractOldValue(string fileContent, SerializedProperty property, long localId)
    {
        // WARNING: This is risky. It can have side-effects on the editor, is slow,
        // and may fail if the temporary asset cannot be loaded correctly.

        const string tempDir = "Assets/PropertyHistoryTemp";
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        string tempAssetPath = Path.Combine(tempDir, Guid.NewGuid() + ".asset");

        try
        {
            File.WriteAllText(tempAssetPath, fileContent);
            AssetDatabase.ImportAsset(tempAssetPath, ImportAssetOptions.ForceSynchronousImport);

            Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(tempAssetPath);
            if (allAssets == null || allAssets.Length == 0)
            {
                Debug.LogWarning("Could not load any assets from the temporary historical file.");
                return "[Could not load historical asset]";
            }

            Object targetObject = null;
            foreach (var asset in allAssets)
            {
                // Important: Some objects in an asset file can be null (e.g. missing references)
                if (asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out _, out long fileID))
                {
                    if (fileID == localId)
                    {
                        targetObject = asset;
                        break;
                    }
                }
            }

            if (targetObject == null)
            {
                // Fallback for objects that might not be directly identifiable, like GameObjects in a scene.
                // This is less reliable and might pick the wrong object if multiple have the same name.
                // targetObject = allAssets[0];
                // Debug.LogWarning($"Could not find object with File ID {localId}. Falling back to the first object in the asset: {targetObject.name}. This may not be the correct object.");
                Debug.LogWarning($"Could not find object with File ID {localId} in historical asset. Ensure the object exists in the historical file.");

                return "[Object not found in historical asset]";
            }

            SerializedObject historicalObject = new SerializedObject(targetObject);
            SerializedProperty historicalProperty = historicalObject.FindProperty(property.propertyPath);

            if (historicalProperty == null)
            {
                return "[Property not found in historical object]";
            }
            return GetPropertyValue(historicalProperty);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to extract old value via SerializedObject: {e.Message}");
            return "[Error during extraction]";
        }
        finally
        {
            AssetDatabase.DeleteAsset(tempAssetPath);
        }
    }

    private static object GetPropertyValue(SerializedProperty prop)
    {
        switch (prop.propertyType)
        {
            case SerializedPropertyType.Integer:
                return prop.intValue;
            case SerializedPropertyType.Boolean:
                return prop.boolValue;
            case SerializedPropertyType.Float:
                return prop.floatValue;
            case SerializedPropertyType.String:
                return prop.stringValue;
            case SerializedPropertyType.Color:
                return prop.colorValue;
            case SerializedPropertyType.ObjectReference:
                return prop.objectReferenceValue;
            case SerializedPropertyType.LayerMask:
                return prop.intValue;
            case SerializedPropertyType.Enum:
                return prop.enumDisplayNames[prop.enumValueIndex];
            case SerializedPropertyType.Vector2:
                return prop.vector2Value;
            case SerializedPropertyType.Vector3:
                return prop.vector3Value;
            case SerializedPropertyType.Vector4:
                return prop.vector4Value;
            case SerializedPropertyType.Rect:
                return prop.rectValue;
            case SerializedPropertyType.ArraySize:
                return prop.arraySize;
            case SerializedPropertyType.Character:
                return (char)prop.intValue;
            case SerializedPropertyType.AnimationCurve:
                return prop.animationCurveValue;
            case SerializedPropertyType.Bounds:
                return prop.boundsValue;
            case SerializedPropertyType.Quaternion:
                return prop.quaternionValue;
            case SerializedPropertyType.ExposedReference:
                return prop.exposedReferenceValue;
            case SerializedPropertyType.FixedBufferSize:
                return prop.fixedBufferSize;
            case SerializedPropertyType.Vector2Int:
                return prop.vector2IntValue;
            case SerializedPropertyType.Vector3Int:
                return prop.vector3IntValue;
            case SerializedPropertyType.RectInt:
                return prop.rectIntValue;
            case SerializedPropertyType.BoundsInt:
                return prop.boundsIntValue;
            case SerializedPropertyType.ManagedReference:
                return prop.managedReferenceValue;
            case SerializedPropertyType.Hash128:
                return prop.hash128Value;
            case SerializedPropertyType.Generic:
                // For generic properties, we can return the serialized property itself for further inspection.
                // This is useful for complex types like lists or custom classes.
                return prop.serializedObject.targetObject; // Return the whole object for further inspection.
            default:
                return $"[{prop.propertyType.ToString()} not supported]";
        }
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
}
