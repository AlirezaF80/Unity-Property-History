using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using PropertyHistoryTool;

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
        if (!PropertyHistoryCore.PreparePropertyData(property, out PropertyData propertyData))
        {
            Debug.LogWarning("Could not prepare property data for history retrieval.");
            return;
        }

        Debug.Log($"[PropertyHistory] AssetPath: {propertyData.AssetPath}, FileID: {propertyData.FileID}, PropertyPath: {propertyData.PropertyPath}");

        var history = PropertyHistoryCore.GetPropertyHistory(propertyData);
        DisplayPropertyHistory(propertyData, history);
    }

    private static void DisplayPropertyHistory(PropertyData propertyData, List<CommitInfo> history)
    {
        var logMessage = new StringBuilder();
        
        logMessage.AppendLine($"--- Git History for {propertyData.DisplayName} ---");
        logMessage.AppendLine($"<b>Asset Path:</b> {propertyData.AssetPath}");
        logMessage.AppendLine("-----------------------------------------");

        if (history.Count == 0)
        {
            logMessage.AppendLine("<b>No changes found for this property in the file's history.</b>");
            Debug.Log(logMessage.ToString());
            return;
        }

        foreach (var commit in history)
        {
            logMessage.AppendLine($"<b>Commit:</b> {commit.ShortHash}");
            logMessage.AppendLine($"<b>Author:</b> {commit.Author}");
            logMessage.AppendLine($"<b>Message:</b> {commit.Message}");
            logMessage.AppendLine($"<b>Value:</b> {commit.Value ?? "[null]"}");
            logMessage.AppendLine("-----------------------------------------");
        }

        Debug.Log(logMessage.ToString());
    }
}
