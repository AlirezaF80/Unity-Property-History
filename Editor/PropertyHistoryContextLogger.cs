using System;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.Text.RegularExpressions;
using VYaml.Serialization;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Linq;
using Debug = UnityEngine.Debug;

[InitializeOnLoad]
public static class PropertyHistoryContextLogger
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;
        YamlSerializer.DefaultOptions = new YamlSerializerOptions
        {
            Resolver = CompositeResolver.Create(new IYamlFormatterResolver[]
            {
                StandardResolver.Instance,
                UnityResolver.Instance,
            })
        };
    }

    private static void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property)
    {
        var propertyCopy = property.Copy();
        menu.AddItem(new GUIContent("Show Property Git History (Last Commit)"), false, () => {
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

        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(targetObject, out _, out long fileID))
        {
            Debug.LogWarning($"Could not get File ID for object '{targetObject.name}'.");
            return;
        }

        if (targetObject is AssetImporter)
        {
            assetPath += ".meta";
        }

        string gitLogArgs = $"log -1 --pretty=format:\"%H|%an|%s\" -- \"{assetPath}\"";
        string commitInfo = GitUtils.RunGitCommand(gitLogArgs);

        if (string.IsNullOrEmpty(commitInfo) || commitInfo.Contains("fatal:"))
        {
            logMessage.AppendLine($"--- Git History for {property.displayName} ---");
            logMessage.AppendLine($"<b>Asset Path:</b> {assetPath}");
            logMessage.AppendLine("<b>No Git history found for this file.</b>");
            Debug.Log(logMessage.ToString());
            return;
        }

        string[] parts = commitInfo.Split('|');
        string commitHash = parts[0];
        string author = parts[1];
        string message = parts[2];

        string gitShowArgs = $"show {commitHash}:\"{assetPath}\"";
        string fileContent = GitUtils.RunGitCommand(gitShowArgs);

        var historicalValue = ComplexParseYaml(fileContent, property, fileID);

        logMessage.AppendLine($"--- Git History for {property.displayName} ---");
        logMessage.AppendLine($"<b>Asset Path:</b> {assetPath}");
        logMessage.AppendLine($"<b>Commit:</b> {commitHash.Substring(0, 7)}");
        logMessage.AppendLine($"<b>Author:</b> {author}");
        logMessage.AppendLine($"<b>Message:</b> {message}");
        logMessage.AppendLine($"<b>Value at Commit:</b> {historicalValue}");
        logMessage.AppendLine("-----------------------------------------");

        Debug.Log(logMessage.ToString());
    }

    // --- THIS IS THE CORRECTED METHOD ---
    private static string ComplexParseYaml(string fileContent, SerializedProperty property, long fileID)
    {
        var fileIdString = $"&{fileID}";
        var documents = fileContent.Split(new[] { "---" }, StringSplitOptions.RemoveEmptyEntries);
        string targetDocument = documents.FirstOrDefault(doc => doc.Contains(fileIdString));

        // if (targetDocument == null)
        // {
            // return "<i>Object not found in historical commit. It may have been added more recently.</i>";
        // }

        try
        {
            byte[] fileBytes = Encoding.UTF8.GetBytes(targetDocument);
            // Deserialize to a generic object first
            var deserializedData = YamlSerializer.Deserialize<object>(fileBytes);

            // Now, check if the deserialized data is a dictionary, and cast it.
            if (deserializedData is IDictionary<object, object> docDict)
            {
                // The actual properties are typically nested under a key matching the object's type name.
                if (docDict.TryGetValue(property.serializedObject.targetObject.GetType().Name, out var rootObject))
                {
                    var foundValue = FindValueInDeserializedObject(rootObject, property.propertyPath);
                    return foundValue != null ? foundValue.ToString() : "<i>Property not found at this commit.</i>";
                }
                return "<i>Could not find root object type in YAML document.</i>";
            }
            return "<i>Unexpected YAML structure (root is not a mapping).</i>";
        }
        catch (Exception e)
        {
            return $"<i>Failed to parse YAML: {e.Message}</i>";
        }
    }

    // --- THIS METHOD IS UPDATED TO USE IDictionary FOR BETTER PRACTICE ---
    private static object FindValueInDeserializedObject(object currentObject, string propertyPath)
    {
        var pathParts = propertyPath.Split('.');
        
        foreach (var part in pathParts)
        {
            if (currentObject == null) return null;

            var arrayMatch = Regex.Match(part, @"(\w+)\[(\d+)\]");
            if (arrayMatch.Success)
            {
                string collectionName = arrayMatch.Groups[1].Value;
                int index = int.Parse(arrayMatch.Groups[2].Value);
                
                if (currentObject is IDictionary<object, object> dict && dict.TryGetValue(collectionName, out var collectionObj))
                {
                    if (collectionObj is IList<object> list && index < list.Count)
                    {
                        currentObject = list[index];
                    }
                    else return null; 
                }
                else return null;
            }
            else
            {
                if (part == "Array" && currentObject is IDictionary<object, object>)
                {
                    // Skip the "Array" part of a path like "m_MyArray.Array.data[0]"
                    continue;
                }
                if (currentObject is IDictionary<object, object> dict && dict.TryGetValue(part, out var nextObject))
                {
                    currentObject = nextObject;
                }
                else return null;
            }
        }
        return currentObject;
    }

    private static string GetAssetPath(Object obj)
    {
        string path = AssetDatabase.GetAssetPath(obj);
        if (!string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (obj is Component component)
        {
            GameObject go = component.gameObject;
            string prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            if (!string.IsNullOrEmpty(prefabPath)) return prefabPath;
            
            if (go.scene.path != null && !string.IsNullOrEmpty(go.scene.path)) return go.scene.path;
        }
        
        return null;
    }
}
