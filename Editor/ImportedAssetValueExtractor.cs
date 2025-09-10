using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ImportedAssetValueExtractor
{
    public static object ExtractOldValue(string fileContent, SerializedProperty property, long localId)
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
}