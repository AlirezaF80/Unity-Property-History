using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using PropertyHistoryTool;

[InitializeOnLoad]
public static class PropertyHistoryContextWindow
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.contextualPropertyMenu += OnPropertyContextMenu;
    }

    private static void OnPropertyContextMenu(GenericMenu menu, SerializedProperty property)
    {
        var propertyCopy = property.Copy();
        menu.AddItem(new GUIContent("Show Property History in Window"), false, () =>
        {
            ShowPropertyHistoryInWindow(propertyCopy);
        });
    }

    private static void ShowPropertyHistoryInWindow(SerializedProperty property)
    {
        if (!PropertyHistoryCore.PreparePropertyData(property, out PropertyData propertyData))
        {
            Debug.LogWarning("Could not prepare property data for history retrieval.");
            return;
        }

        Debug.Log($"[PropertyHistory] AssetPath: {propertyData.AssetPath}, FileID: {propertyData.FileID}, PropertyPath: {propertyData.PropertyPath}");

        // Open the Property History Window and load the property data
        var window = EditorWindow.GetWindow<PropertyHistoryWindow>("Property History");
        window.LoadPropertyData(propertyData);
    }
}