using UnityEditor;
using UnityEngine;

namespace PropertyHistoryTool
{
    /// <summary>
    /// Contains all the information needed to retrieve property history
    /// </summary>
    public class PropertyData
    {
        /// <summary>
        /// The Unity asset path (e.g., "Assets/MyPrefab.prefab")
        /// </summary>
        public string AssetPath { get; set; }
        
        /// <summary>
        /// The file ID of the object in the asset
        /// </summary>
        public long FileID { get; set; }
        
        /// <summary>
        /// The property path (e.g., "m_LocalPosition.x")
        /// </summary>
        public string PropertyPath { get; set; }
        
        /// <summary>
        /// The display name of the property (e.g., "Position.X")
        /// </summary>
        public string DisplayName { get; set; }
        
        /// <summary>
        /// The target Unity object
        /// </summary>
        public Object TargetObject { get; set; }
        
        /// <summary>
        /// Creates a PropertyData instance from a SerializedProperty
        /// </summary>
        public static PropertyData FromSerializedProperty(SerializedProperty property)
        {
            return new PropertyData
            {
                TargetObject = property.serializedObject.targetObject,
                PropertyPath = property.propertyPath,
                DisplayName = property.displayName
            };
        }
    }
}