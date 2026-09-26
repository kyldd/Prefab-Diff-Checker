using System.Collections.Generic;
using UnityEngine;

namespace PrefabDiffChecker
{
    public enum ChangeType
    {
        Unchanged,
        Added,
        Removed,
        Modified,
        Moved
    }

    public enum ValueKind
    {
        Unknown,
        Integer,
        Boolean,
        Float,
        String,
        Color,
        ObjectReference,
        LayerMask,
        Enum,
        Vector2,
        Vector3,
        Vector4,
        Rect,
        Bounds,
        Vector2Int,
        Vector3Int,
        RectInt,
        BoundsInt,
        AnimationCurve,
        Generic
    }

    public class PropertySnapshot
    {
        public string path;
        public string displayName;
        public string typeName;

        public ValueKind valueKind;

        public string value;

        public Object objectReference;
        public string objectReferencePath;

        public int depth;
        public bool isArray;
    }

    public class ComponentSnapshot
    {
        public string typeName;
        public string displayName;

        public int typeIndex;

        public List<PropertySnapshot> properties =
            new List<PropertySnapshot>();
    }

    public class ObjectSnapshot
    {
        public string name;

        public string hierarchyKey;
        public string hierarchyPath;

        public int siblingIndex;
        public bool activeSelf;

        public List<ComponentSnapshot> components =
            new List<ComponentSnapshot>();

        public List<ObjectSnapshot> children =
            new List<ObjectSnapshot>();
    }

    public class PrefabSnapshot
    {
        public string assetPath;
        public string prefabName;

        public ObjectSnapshot root;
    }

    public class PropertyDiff
    {
        public ChangeType changeType;

        public string path;
        public string displayName;

        public string oldValue;
        public string newValue;

        public PropertySnapshot oldProperty;
        public PropertySnapshot newProperty;
    }

    public class ComponentDiff
    {
        public ChangeType changeType;

        public string typeName;
        public string displayName;

        public int typeIndex;

        public ComponentSnapshot oldComponent;
        public ComponentSnapshot newComponent;

        public List<PropertyDiff> properties =
            new List<PropertyDiff>();

        public bool expanded = true;
    }

    public class ObjectDiff
    {
        public ChangeType changeType;

        public string name;
        public string hierarchyPath;
        public string hierarchyKey;

        public ObjectSnapshot oldObject;
        public ObjectSnapshot newObject;

        public List<ComponentDiff> components =
            new List<ComponentDiff>();

        public List<ObjectDiff> children =
            new List<ObjectDiff>();

        public bool expanded = true;

        public bool HasVisibleChanges()
        {
            if (changeType != ChangeType.Unchanged)
                return true;

            for (int i = 0; i < components.Count; i++)
            {
                ComponentDiff component =
                    components[i];

                if (component.changeType !=
                    ChangeType.Unchanged)
                {
                    return true;
                }

                for (int p = 0;
                     p < component.properties.Count;
                     p++)
                {
                    if (component.properties[p].changeType !=
                        ChangeType.Unchanged)
                    {
                        return true;
                    }
                }
            }

            for (int i = 0; i < children.Count; i++)
            {
                if (children[i].HasVisibleChanges())
                    return true;
            }

            return false;
        }
    }

    public class PrefabDiffResult
    {
        public PrefabSnapshot oldSnapshot;
        public PrefabSnapshot newSnapshot;

        public ObjectDiff root;

        public int addedObjects;
        public int removedObjects;
        public int modifiedObjects;

        public int addedComponents;
        public int removedComponents;
        public int modifiedComponents;

        public int modifiedProperties;
    }
}
