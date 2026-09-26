using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PrefabDiffChecker
{
    public static class PrefabSnapshotBuilder
    {
        public static PrefabSnapshot Build(
            GameObject prefab)
        {
            if (prefab == null)
                return null;

            PrefabSnapshot snapshot =
                new PrefabSnapshot();

            snapshot.assetPath =
                AssetDatabase.GetAssetPath(prefab);

            snapshot.prefabName =
                prefab.name;

            snapshot.root =
                BuildObject(
                    prefab.transform,
                    "");

            return snapshot;
        }

        private static ObjectSnapshot BuildObject(
            Transform transform,
            string parentHumanPath)
        {
            ObjectSnapshot snapshot =
                new ObjectSnapshot();

            snapshot.name =
                transform.name;

            snapshot.siblingIndex =
                transform.GetSiblingIndex();

            snapshot.activeSelf =
                transform.gameObject.activeSelf;

            snapshot.hierarchyPath =
                string.IsNullOrEmpty(parentHumanPath)
                    ? transform.name
                    : parentHumanPath + "/" + transform.name;

            snapshot.hierarchyKey =
                BuildHierarchyKey(transform);

            ReadComponents(
                transform.gameObject,
                snapshot);

            for (int i = 0;
                 i < transform.childCount;
                 i++)
            {
                Transform child =
                    transform.GetChild(i);

                snapshot.children.Add(
                    BuildObject(
                        child,
                        snapshot.hierarchyPath));
            }

            return snapshot;
        }

        private static string BuildHierarchyKey(
            Transform transform)
        {
            List<string> parts =
                new List<string>();

            Transform current =
                transform;

            while (current != null)
            {
                parts.Add(
                    current.name +
                    "[" +
                    current.GetSiblingIndex() +
                    "]");

                current =
                    current.parent;
            }

            parts.Reverse();

            return string.Join(
                "/",
                parts.ToArray());
        }

        private static void ReadComponents(
            GameObject gameObject,
            ObjectSnapshot objectSnapshot)
        {
            Component[] components =
                gameObject.GetComponents<Component>();

            Dictionary<string, int> typeCounts =
                new Dictionary<string, int>();

            for (int i = 0;
                 i < components.Length;
                 i++)
            {
                Component component =
                    components[i];

                if (component == null)
                {
                    ComponentSnapshot missing =
                        new ComponentSnapshot();

                    missing.typeName =
                        "MissingScript";

                    missing.displayName =
                        "Missing Script";

                    missing.typeIndex = 0;

                    objectSnapshot.components.Add(
                        missing);

                    continue;
                }

                System.Type type =
                    component.GetType();

                string typeName =
                    type.FullName;

                int typeIndex = 0;

                if (typeCounts.ContainsKey(typeName))
                {
                    typeIndex =
                        typeCounts[typeName];

                    typeCounts[typeName] =
                        typeIndex + 1;
                }
                else
                {
                    typeCounts.Add(
                        typeName,
                        1);
                }

                ComponentSnapshot componentSnapshot =
                    new ComponentSnapshot();

                componentSnapshot.typeName =
                    typeName;

                componentSnapshot.displayName =
                    ObjectNames.NicifyVariableName(
                        type.Name);

                componentSnapshot.typeIndex =
                    typeIndex;

                ReadSerializedProperties(
                    component,
                    componentSnapshot);

                objectSnapshot.components.Add(
                    componentSnapshot);
            }
        }

        private static void ReadSerializedProperties(
            Component component,
            ComponentSnapshot snapshot)
        {
            SerializedObject serializedObject;

            try
            {
                serializedObject =
                    new SerializedObject(component);

                serializedObject.Update();
            }
            catch
            {
                return;
            }

            SerializedProperty iterator =
                serializedObject.GetIterator();

            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iterator.propertyPath == "m_Script")
                    continue;

                PropertySnapshot property =
                    CreatePropertySnapshot(iterator);

                snapshot.properties.Add(property);
            }
        }

        private static PropertySnapshot
            CreatePropertySnapshot(
                SerializedProperty property)
        {
            PropertySnapshot snapshot =
                new PropertySnapshot();

            snapshot.path =
                property.propertyPath;

            snapshot.displayName =
                property.displayName;

            snapshot.typeName =
                property.propertyType.ToString();

            snapshot.valueKind =
                ConvertValueKind(
                    property.propertyType);

            snapshot.value =
                PropertyToString(property);

            snapshot.depth =
                property.depth;

            snapshot.isArray =
                property.isArray &&
                property.propertyType ==
                SerializedPropertyType.Generic;

            if (property.propertyType ==
                SerializedPropertyType.ObjectReference)
            {
                snapshot.objectReference =
                    property.objectReferenceValue;

                if (snapshot.objectReference != null)
                {
                    snapshot.objectReferencePath =
                        AssetDatabase.GetAssetPath(
                            snapshot.objectReference);
                }
            }

            return snapshot;
        }

        private static ValueKind ConvertValueKind(
            SerializedPropertyType type)
        {
            switch (type)
            {
                case SerializedPropertyType.Integer:
                    return ValueKind.Integer;

                case SerializedPropertyType.Boolean:
                    return ValueKind.Boolean;

                case SerializedPropertyType.Float:
                    return ValueKind.Float;

                case SerializedPropertyType.String:
                    return ValueKind.String;

                case SerializedPropertyType.Color:
                    return ValueKind.Color;

                case SerializedPropertyType.ObjectReference:
                    return ValueKind.ObjectReference;

                case SerializedPropertyType.LayerMask:
                    return ValueKind.LayerMask;

                case SerializedPropertyType.Enum:
                    return ValueKind.Enum;

                case SerializedPropertyType.Vector2:
                    return ValueKind.Vector2;

                case SerializedPropertyType.Vector3:
                    return ValueKind.Vector3;

                case SerializedPropertyType.Vector4:
                    return ValueKind.Vector4;

                case SerializedPropertyType.Rect:
                    return ValueKind.Rect;

                case SerializedPropertyType.Bounds:
                    return ValueKind.Bounds;

                case SerializedPropertyType.AnimationCurve:
                    return ValueKind.AnimationCurve;

                case SerializedPropertyType.Generic:
                    return ValueKind.Generic;
            }

#if UNITY_2017_2_OR_NEWER
            switch (type)
            {
                case SerializedPropertyType.Vector2Int:
                    return ValueKind.Vector2Int;

                case SerializedPropertyType.Vector3Int:
                    return ValueKind.Vector3Int;

                case SerializedPropertyType.RectInt:
                    return ValueKind.RectInt;

                case SerializedPropertyType.BoundsInt:
                    return ValueKind.BoundsInt;
            }
#endif

            return ValueKind.Unknown;
        }

        private static string PropertyToString(
            SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    return property.intValue.ToString();

                case SerializedPropertyType.Boolean:
                    return property.boolValue
                        ? "True"
                        : "False";

                case SerializedPropertyType.Float:
                    return FormatFloat(
                        property.floatValue);

                case SerializedPropertyType.String:
                    return property.stringValue ?? "";

                case SerializedPropertyType.Color:
                    return "#" +
                           ColorUtility.ToHtmlStringRGBA(
                               property.colorValue);

                case SerializedPropertyType.ObjectReference:
                {
                    Object obj =
                        property.objectReferenceValue;

                    if (obj == null)
                        return "None";

                    string path =
                        AssetDatabase.GetAssetPath(obj);

                    if (string.IsNullOrEmpty(path))
                    {
                        return obj.name +
                               " [" +
                               obj.GetType().Name +
                               "]";
                    }

                    return obj.name +
                           " [" +
                           obj.GetType().Name +
                           "] @ " +
                           path;
                }

                case SerializedPropertyType.LayerMask:
                    return property.intValue.ToString();

                case SerializedPropertyType.Enum:
                {
                    int index =
                        property.enumValueIndex;

                    if (property.enumDisplayNames != null &&
                        index >= 0 &&
                        index <
                        property.enumDisplayNames.Length)
                    {
                        return property.enumDisplayNames[index];
                    }

                    return index.ToString();
                }

                case SerializedPropertyType.Vector2:
                    return FormatVector2(
                        property.vector2Value);

                case SerializedPropertyType.Vector3:
                    return FormatVector3(
                        property.vector3Value);

                case SerializedPropertyType.Vector4:
                    return FormatVector4(
                        property.vector4Value);

                case SerializedPropertyType.Rect:
                {
                    Rect r =
                        property.rectValue;

                    return
                        "X " + FormatFloat(r.x) +
                        "   Y " + FormatFloat(r.y) +
                        "   W " + FormatFloat(r.width) +
                        "   H " + FormatFloat(r.height);
                }

                case SerializedPropertyType.Bounds:
                {
                    Bounds b =
                        property.boundsValue;

                    return
                        "Center " +
                        FormatVector3(b.center) +
                        "   Size " +
                        FormatVector3(b.size);
                }

                case SerializedPropertyType.AnimationCurve:
                {
                    AnimationCurve curve =
                        property.animationCurveValue;

                    int count =
                        curve != null &&
                        curve.keys != null
                            ? curve.keys.Length
                            : 0;

                    return "Animation Curve (" +
                           count +
                           " keys)";
                }

#if UNITY_2017_2_OR_NEWER
                case SerializedPropertyType.Vector2Int:
                {
                    Vector2Int v =
                        property.vector2IntValue;

                    return
                        "X " + v.x +
                        "   Y " + v.y;
                }

                case SerializedPropertyType.Vector3Int:
                {
                    Vector3Int v =
                        property.vector3IntValue;

                    return
                        "X " + v.x +
                        "   Y " + v.y +
                        "   Z " + v.z;
                }

                case SerializedPropertyType.RectInt:
                {
                    RectInt r =
                        property.rectIntValue;

                    return
                        "X " + r.x +
                        "   Y " + r.y +
                        "   W " + r.width +
                        "   H " + r.height;
                }

                case SerializedPropertyType.BoundsInt:
                {
                    BoundsInt b =
                        property.boundsIntValue;

                    return
                        "Position " +
                        b.position +
                        "   Size " +
                        b.size;
                }
#endif

                case SerializedPropertyType.Generic:
                {
                    if (property.isArray)
                    {
                        return "Size " +
                               property.arraySize;
                    }

                    return "";
                }

                default:
                    return
                        "<" +
                        property.propertyType +
                        ">";
            }
        }

        private static string FormatVector2(
            Vector2 value)
        {
            return
                "X " + FormatFloat(value.x) +
                "   Y " + FormatFloat(value.y);
        }

        private static string FormatVector3(
            Vector3 value)
        {
            return
                "X " + FormatFloat(value.x) +
                "   Y " + FormatFloat(value.y) +
                "   Z " + FormatFloat(value.z);
        }

        private static string FormatVector4(
            Vector4 value)
        {
            return
                "X " + FormatFloat(value.x) +
                "   Y " + FormatFloat(value.y) +
                "   Z " + FormatFloat(value.z) +
                "   W " + FormatFloat(value.w);
        }

        private static string FormatFloat(
            float value)
        {
            return value.ToString("0.#####");
        }
    }
}
