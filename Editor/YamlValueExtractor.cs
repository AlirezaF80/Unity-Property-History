using System;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Core;

public static class YamlValueExtractor
{
    public static string ExtractPropertyValue(string fileContent, long localId, string propertyPath)
    {
        try
        {
            var yaml = new YamlStream();
            yaml.Load(new StringReader(fileContent));

            // 1. Find the root node of the correct object using its file ID (anchor)
            YamlMappingNode objectRootNode = FindObjectNodeByAnchor(yaml, localId.ToString());
            if (objectRootNode == null)
            {
                return "[Object not found in historical asset]";
            }

            // The actual properties are under a single child key (e.g., "Transform", "MonoBehaviour")
            var propertiesNode = objectRootNode.Children.FirstOrDefault().Value as YamlMappingNode;
            if (propertiesNode == null)
            {
                return "[Could not find property root in object]";
            }

            // 2. Traverse the property path
            string[] pathParts = propertyPath.Split('.');
            YamlNode finalNode = TraversePath(propertiesNode, pathParts);

            if (finalNode == null)
            {
                return "[Property not found at this commit]";
            }
            
            // 3. Format the final YAML node into a readable string
            return FormatNode(finalNode);
        }
        catch (Exception ex)
        {
            return $"[Extraction Error: {ex.Message}]";
        }
    }

    private static YamlMappingNode FindObjectNodeByAnchor(YamlStream yaml, string anchor)
    {
        foreach (var document in yaml.Documents)
        {
            if (document.RootNode.Anchor == anchor)
            {
                return document.RootNode as YamlMappingNode;
            }
        }
        return null;
    }

    private static YamlNode TraversePath(YamlMappingNode startNode, string[] path)
    {
        YamlNode currentNode = startNode;
        foreach (string part in path)
        {
            if (currentNode is YamlMappingNode mappingNode)
            {
                // Try to find the next part of the path in the current node's children
                if (mappingNode.Children.TryGetValue(new YamlScalarNode(part), out YamlNode nextNode))
                {
                    currentNode = nextNode;
                }
                else
                {
                    // Path does not exist in this version of the file
                    return null; 
                }
            }
            else
            {
                // We tried to traverse deeper, but the current node is not a mapping (e.g., it's a scalar value)
                return null;
            }
        }
        return currentNode;
    }

    /// <summary>
    /// Dynamically formats any YamlNode into a compact, human-readable string.
    /// This is the key to making the tool versatile.
    /// </summary>
    private static string FormatNode(YamlNode node)
    {
        if (node is YamlScalarNode scalar)
        {
            // Simple value (string, number, bool)
            return scalar.Value ?? "null";
        }
        
        if (node is YamlMappingNode mapping)
        {
            // Complex object (Vector3, Color, ObjectReference)
            var sb = new StringBuilder("{ ");
            sb.Append(string.Join(", ", mapping.Children.Select(kvp => $"{kvp.Key}: {FormatNode(kvp.Value)}")));
            sb.Append(" }");
            return sb.ToString();
        }

        if (node is YamlSequenceNode sequence)
        {
            // Array or List
            var sb = new StringBuilder("[");
            sb.Append(string.Join(", ", sequence.Children.Select(FormatNode)));
            sb.Append("]");
            return sb.ToString();
        }

        return "[Unsupported YAML Node Type]";
    }
}