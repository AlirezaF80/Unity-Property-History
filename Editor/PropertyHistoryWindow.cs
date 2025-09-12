using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using PropertyHistoryTool;

namespace PropertyHistoryTool
{
    /// <summary>
    /// Editor window for displaying property history in a user-friendly interface
    /// </summary>
    public class PropertyHistoryWindow : EditorWindow
    {
        private PropertyData currentPropertyData;
        private List<CommitInfo> propertyHistory;
        private Vector2 scrollPosition;
        private bool isLoading;
        private string errorMessage;
        private float splitViewRatio = 0.3f;
        private bool isResizing;
        private Rect resizeHandleRect;
        
        private GUIStyle headerStyle;
        private GUIStyle commitStyle;
        private GUIStyle valueStyle;
        private GUIStyle errorStyle;
        
        [MenuItem("Window/Property History/Property History Window")]
        public static void ShowWindow()
        {
            GetWindow<PropertyHistoryWindow>("Property History");
        }
        
        private void OnEnable()
        {
            // Initialize styles
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                margin = new RectOffset(0, 0, 10, 5)
            };
            
            commitStyle = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
                margin = new RectOffset(0, 0, 5, 2)
            };
            
            valueStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                stretchHeight = false,
                margin = new RectOffset(0, 0, 2, 10)
            };
            
            errorStyle = new GUIStyle(EditorStyles.helpBox)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            
            // Subscribe to selection changes
            Selection.selectionChanged += OnSelectionChanged;
        }
        
        private void OnDisable()
        {
            // Unsubscribe from selection changes
            Selection.selectionChanged -= OnSelectionChanged;
        }
        
        private void OnSelectionChanged()
        {
            // Auto-refresh when selection changes if we have a property loaded
            if (currentPropertyData != null)
            {
                LoadPropertyHistory(currentPropertyData);
            }
        }
        
        private void OnGUI()
        {
            EditorGUILayout.Space(5);
            
            // Draw header
            EditorGUILayout.LabelField("Property History Viewer", EditorStyles.largeLabel);
            EditorGUILayout.Space(5);
            
            // Draw current property info or load button
            if (currentPropertyData == null)
            {
                DrawLoadPropertySection();
            }
            else
            {
                DrawPropertyInfoSection();
            }
            
            EditorGUILayout.Space(10);
            
            // Draw history section
            if (currentPropertyData != null)
            {
                DrawHistorySection();
            }
        }
        
        private void DrawLoadPropertySection()
        {
            EditorGUILayout.LabelField("No property selected", EditorStyles.centeredGreyMiniLabel);
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Load Property from Selection"))
            {
                LoadPropertyFromSelection();
            }
            
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox("Select a property in the Inspector and click the button above, or right-click on any property and choose 'Show Full Property Git History'.", MessageType.Info);
        }
        
        private void DrawPropertyInfoSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Current Property", headerStyle);
            EditorGUILayout.LabelField($"Name: {currentPropertyData.DisplayName}");
            EditorGUILayout.LabelField($"Asset: {currentPropertyData.AssetPath}");
            EditorGUILayout.LabelField($"Property Path: {currentPropertyData.PropertyPath}");
            EditorGUILayout.LabelField($"File ID: {currentPropertyData.FileID}");
            
            EditorGUILayout.Space(5);
            
            if (GUILayout.Button("Load Different Property"))
            {
                currentPropertyData = null;
                propertyHistory = null;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawHistorySection()
        {
            // Draw resize handle
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect resizeRect = GUILayoutUtility.GetRect(20, 3);
            GUI.DrawTexture(resizeRect, EditorGUIUtility.whiteTexture);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // Handle resize
            EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDown && resizeRect.Contains(Event.current.mousePosition))
            {
                isResizing = true;
            }
            if (isResizing)
            {
                splitViewRatio = Event.current.mousePosition.y / position.height;
                splitViewRatio = Mathf.Clamp(splitViewRatio, 0.2f, 0.8f);
                if (Event.current.type == EventType.MouseUp)
                {
                    isResizing = false;
                }
                Repaint();
            }
            
            // Draw loading or error state
            if (isLoading)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Loading property history...", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }
            
            if (!string.IsNullOrEmpty(errorMessage))
            {
                EditorGUILayout.BeginVertical(errorStyle);
                EditorGUILayout.LabelField(errorMessage);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Draw history
            if (propertyHistory == null || propertyHistory.Count == 0)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("No history found for this property.", EditorStyles.centeredGreyMiniLabel);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // Draw commit history
            EditorGUILayout.LabelField($"History ({propertyHistory.Count} commits)", headerStyle);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            foreach (var commit in propertyHistory)
            {
                DrawCommitInfo(commit);
            }
            
            EditorGUILayout.EndScrollView();
        }
        
        private void DrawCommitInfo(CommitInfo commit)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // Commit header
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Commit: {commit.ShortHash}", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy Hash", GUILayout.Width(100)))
            {
                GUIUtility.systemCopyBuffer = commit.Hash;
            }
            EditorGUILayout.EndHorizontal();
            
            // Commit details
            EditorGUILayout.LabelField($"Author: {commit.Author}", commitStyle);
            EditorGUILayout.LabelField($"Message: {commit.Message}", commitStyle);
            
            // Property value
            EditorGUILayout.LabelField("Property Value:", EditorStyles.boldLabel);
            string valueText = commit.Value?.ToString() ?? "[null]";
            EditorGUILayout.TextArea(valueText, valueStyle);
            
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
        
        private void LoadPropertyFromSelection()
        {
            // Try to get the active property from the Inspector
            var activeEditor = EditorWindow.focusedWindow;
            if (activeEditor == null || activeEditor.GetType().Name != "InspectorWindow")
            {
                errorMessage = "Please select a property in the Inspector first.";
                return;
            }
            
            // This is a simplified approach - in a real implementation, you might need
            // to use reflection to access the Inspector's selected property
            errorMessage = "Please right-click on a property in the Inspector and select 'Show Full Property Git History'.";
        }
        
        public void LoadPropertyData(PropertyData propertyData)
        {
            currentPropertyData = propertyData;
            errorMessage = null;
            LoadPropertyHistory(propertyData);
        }
        
        private void LoadPropertyHistory(PropertyData propertyData)
        {
            isLoading = true;
            errorMessage = null;
            Repaint();
            
            // Load history in a background operation to avoid freezing the UI
            EditorApplication.delayCall += () =>
            {
                try
                {
                    propertyHistory = PropertyHistoryCore.GetPropertyHistory(propertyData);
                    isLoading = false;
                }
                catch (Exception ex)
                {
                    errorMessage = $"Error loading property history: {ex.Message}";
                    isLoading = false;
                }
                finally
                {
                    Repaint();
                }
            };
        }
    }
}