using UnityEditor;
using UnityEngine;

namespace PropertyHistoryTool
{
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
            menu.AddItem(new GUIContent("Show Property History Window"), false, () =>
            {
                if (!GitUtils.IsGitInstalled())
                    EditorUtility.DisplayDialog("Git Not Found", "Git is not installed or not found in PATH.", "OK");
                else if (!GitUtils.IsGitRepository())
                    EditorUtility.DisplayDialog("Not a Git Repository", "The current project is not a Git repository.", "OK");
                else
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
}
