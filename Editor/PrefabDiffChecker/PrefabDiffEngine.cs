using System;
using System.Collections.Generic;

namespace PrefabDiffChecker
{
    public static class PrefabDiffEngine
    {
        private class MatchContext
        {
            public MatchResult matchResult;

            public Dictionary<ObjectSnapshot, ObjectMatch>
                oldToMatch =
                    new Dictionary<ObjectSnapshot, ObjectMatch>();

            public Dictionary<ObjectSnapshot, ObjectMatch>
                newToMatch =
                    new Dictionary<ObjectSnapshot, ObjectMatch>();

            public Dictionary<ObjectSnapshot, ObjectSnapshot>
                oldParents =
                    new Dictionary<ObjectSnapshot, ObjectSnapshot>();

            public Dictionary<ObjectSnapshot, ObjectSnapshot>
                newParents =
                    new Dictionary<ObjectSnapshot, ObjectSnapshot>();
        }

        public static PrefabDiffResult Compare(
            PrefabSnapshot oldSnapshot,
            PrefabSnapshot newSnapshot)
        {
            PrefabDiffResult result =
                new PrefabDiffResult();

            result.oldSnapshot =
                oldSnapshot;

            result.newSnapshot =
                newSnapshot;

            MatchResult matches =
                PrefabMatcher.Match(
                    oldSnapshot,
                    newSnapshot);

            MatchContext context =
                BuildMatchContext(
                    matches,
                    oldSnapshot,
                    newSnapshot);

            ObjectSnapshot oldRoot =
                oldSnapshot != null
                    ? oldSnapshot.root
                    : null;

            ObjectSnapshot newRoot =
                newSnapshot != null
                    ? newSnapshot.root
                    : null;

            if (oldRoot == null &&
                newRoot == null)
            {
                return result;
            }

            if (oldRoot == null)
            {
                result.root =
                    CompareObject(
                        null,
                        newRoot,
                        result,
                        context);

                return result;
            }

            if (newRoot == null)
            {
                result.root =
                    CompareObject(
                        oldRoot,
                        null,
                        result,
                        context);

                return result;
            }

            ObjectMatch rootMatch =
                context.oldToMatch.ContainsKey(oldRoot)
                    ? context.oldToMatch[oldRoot]
                    : null;

            if (rootMatch != null &&
                ReferenceEquals(
                    rootMatch.newObject,
                    newRoot))
            {
                result.root =
                    CompareObject(
                        oldRoot,
                        newRoot,
                        result,
                        context);
            }
            else
            {
                result.root =
                    CompareObject(
                        oldRoot,
                        null,
                        result,
                        context);

                ObjectDiff addedRoot =
                    CompareObject(
                        null,
                        newRoot,
                        result,
                        context);

                result.root.children.Add(
                    addedRoot);

                result.root.changeType =
                    ChangeType.Modified;
            }

            return result;
        }

        private static MatchContext BuildMatchContext(
            MatchResult matchResult,
            PrefabSnapshot oldSnapshot,
            PrefabSnapshot newSnapshot)
        {
            MatchContext context =
                new MatchContext();

            context.matchResult =
                matchResult;

            for (int i = 0;
                 i < matchResult.matches.Count;
                 i++)
            {
                ObjectMatch match =
                    matchResult.matches[i];

                context.oldToMatch[
                    match.oldObject] =
                    match;

                context.newToMatch[
                    match.newObject] =
                    match;
            }

            if (oldSnapshot != null &&
                oldSnapshot.root != null)
            {
                BuildParentMap(
                    oldSnapshot.root,
                    null,
                    context.oldParents);
            }

            if (newSnapshot != null &&
                newSnapshot.root != null)
            {
                BuildParentMap(
                    newSnapshot.root,
                    null,
                    context.newParents);
            }

            return context;
        }

        private static void BuildParentMap(
            ObjectSnapshot obj,
            ObjectSnapshot parent,
            Dictionary<ObjectSnapshot, ObjectSnapshot> map)
        {
            if (obj == null)
                return;

            map[obj] =
                parent;

            for (int i = 0;
                 i < obj.children.Count;
                 i++)
            {
                BuildParentMap(
                    obj.children[i],
                    obj,
                    map);
            }
        }

        private static ObjectDiff CompareObject(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject,
            PrefabDiffResult result,
            MatchContext context)
        {
            ObjectDiff diff =
                new ObjectDiff();

            diff.oldObject =
                oldObject;

            diff.newObject =
                newObject;

            if (oldObject == null &&
                newObject != null)
            {
                diff.changeType =
                    ChangeType.Added;

                diff.name =
                    newObject.name;

                diff.hierarchyPath =
                    newObject.hierarchyPath;

                diff.hierarchyKey =
                    newObject.hierarchyKey;

                result.addedObjects++;

                AddAllComponents(
                    null,
                    newObject.components,
                    diff,
                    result);

                for (int i = 0;
                     i < newObject.children.Count;
                     i++)
                {
                    ObjectSnapshot child =
                        newObject.children[i];

                    if (context.newToMatch.ContainsKey(child))
                        continue;

                    diff.children.Add(
                        CompareObject(
                            null,
                            child,
                            result,
                            context));
                }

                return diff;
            }

            if (oldObject != null &&
                newObject == null)
            {
                diff.changeType =
                    ChangeType.Removed;

                diff.name =
                    oldObject.name;

                diff.hierarchyPath =
                    oldObject.hierarchyPath;

                diff.hierarchyKey =
                    oldObject.hierarchyKey;

                result.removedObjects++;

                AddAllComponents(
                    oldObject.components,
                    null,
                    diff,
                    result);

                for (int i = 0;
                     i < oldObject.children.Count;
                     i++)
                {
                    ObjectSnapshot child =
                        oldObject.children[i];

                    if (context.oldToMatch.ContainsKey(child))
                        continue;

                    diff.children.Add(
                        CompareObject(
                            child,
                            null,
                            result,
                            context));
                }

                return diff;
            }

            if (oldObject == null ||
                newObject == null)
            {
                return diff;
            }

            diff.name =
                newObject.name;

            diff.hierarchyPath =
                newObject.hierarchyPath;

            diff.hierarchyKey =
                newObject.hierarchyKey;

            CompareComponents(
                oldObject,
                newObject,
                diff,
                result);

            CompareChildren(
                oldObject,
                newObject,
                diff,
                result,
                context);

            bool renamed =
                !string.Equals(
                    oldObject.name,
                    newObject.name,
                    StringComparison.Ordinal);

            bool activeChanged =
                oldObject.activeSelf !=
                newObject.activeSelf;

            bool moved =
                WasMoved(
                    oldObject,
                    newObject,
                    context);

            bool contentModified =
                renamed ||
                activeChanged ||
                HasChangedComponents(diff) ||
                HasChangedChildren(diff);

            if (moved)
            {
                diff.changeType =
                    ChangeType.Moved;

                result.modifiedObjects++;
            }
            else if (contentModified)
            {
                diff.changeType =
                    ChangeType.Modified;

                result.modifiedObjects++;
            }
            else
            {
                diff.changeType =
                    ChangeType.Unchanged;
            }

            return diff;
        }

        private static bool WasMoved(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject,
            MatchContext context)
        {
            ObjectSnapshot oldParent =
                context.oldParents.ContainsKey(oldObject)
                    ? context.oldParents[oldObject]
                    : null;

            ObjectSnapshot newParent =
                context.newParents.ContainsKey(newObject)
                    ? context.newParents[newObject]
                    : null;

            if (oldParent == null &&
                newParent == null)
            {
                return false;
            }

            if ((oldParent == null) !=
                (newParent == null))
            {
                return true;
            }

            if (oldParent != null &&
                newParent != null)
            {
                ObjectMatch parentMatch;

                if (!context.oldToMatch.TryGetValue(
                        oldParent,
                        out parentMatch))
                {
                    return true;
                }

                if (!ReferenceEquals(
                        parentMatch.newObject,
                        newParent))
                {
                    return true;
                }
            }

            if (oldObject.siblingIndex !=
                newObject.siblingIndex)
            {
                return true;
            }

            return false;
        }

        private static bool HasChangedComponents(
            ObjectDiff diff)
        {
            for (int i = 0;
                 i < diff.components.Count;
                 i++)
            {
                if (diff.components[i].changeType !=
                    ChangeType.Unchanged)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasChangedChildren(
            ObjectDiff diff)
        {
            for (int i = 0;
                 i < diff.children.Count;
                 i++)
            {
                if (diff.children[i].changeType !=
                    ChangeType.Unchanged)
                {
                    return true;
                }
            }

            return false;
        }

        private static void CompareChildren(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject,
            ObjectDiff objectDiff,
            PrefabDiffResult result,
            MatchContext context)
        {
            HashSet<ObjectSnapshot> consumedOld =
                new HashSet<ObjectSnapshot>();

            HashSet<ObjectSnapshot> consumedNew =
                new HashSet<ObjectSnapshot>();

            for (int i = 0;
                 i < newObject.children.Count;
                 i++)
            {
                ObjectSnapshot newChild =
                    newObject.children[i];

                ObjectMatch match;

                if (!context.newToMatch.TryGetValue(
                        newChild,
                        out match))
                {
                    continue;
                }

                ObjectSnapshot oldChild =
                    match.oldObject;

                ObjectSnapshot matchedOldParent =
                    context.oldParents.ContainsKey(oldChild)
                        ? context.oldParents[oldChild]
                        : null;

                ObjectSnapshot matchedNewParent =
                    context.newParents.ContainsKey(newChild)
                        ? context.newParents[newChild]
                        : null;

                if (!ReferenceEquals(
                        matchedNewParent,
                        newObject))
                {
                    continue;
                }

                objectDiff.children.Add(
                    CompareObject(
                        oldChild,
                        newChild,
                        result,
                        context));

                consumedOld.Add(oldChild);
                consumedNew.Add(newChild);
            }

            for (int i = 0;
                 i < newObject.children.Count;
                 i++)
            {
                ObjectSnapshot newChild =
                    newObject.children[i];

                if (consumedNew.Contains(newChild))
                    continue;

                if (context.newToMatch.ContainsKey(newChild))
                {
                    continue;
                }

                objectDiff.children.Add(
                    CompareObject(
                        null,
                        newChild,
                        result,
                        context));

                consumedNew.Add(newChild);
            }

            for (int i = 0;
                 i < oldObject.children.Count;
                 i++)
            {
                ObjectSnapshot oldChild =
                    oldObject.children[i];

                if (consumedOld.Contains(oldChild))
                    continue;

                if (context.oldToMatch.ContainsKey(oldChild))
                {
                    continue;
                }

                objectDiff.children.Add(
                    CompareObject(
                        oldChild,
                        null,
                        result,
                        context));

                consumedOld.Add(oldChild);
            }
        }

        private static void CompareComponents(
            ObjectSnapshot oldObject,
            ObjectSnapshot newObject,
            ObjectDiff objectDiff,
            PrefabDiffResult result)
        {
            Dictionary<string, ComponentSnapshot> oldMap =
                BuildComponentMap(
                    oldObject.components);

            Dictionary<string, ComponentSnapshot> newMap =
                BuildComponentMap(
                    newObject.components);

            List<string> keys =
                new List<string>();

            AddKeys(
                keys,
                oldMap);

            AddKeys(
                keys,
                newMap);

            for (int i = 0;
                 i < keys.Count;
                 i++)
            {
                string key =
                    keys[i];

                ComponentSnapshot oldComponent =
                    oldMap.ContainsKey(key)
                        ? oldMap[key]
                        : null;

                ComponentSnapshot newComponent =
                    newMap.ContainsKey(key)
                        ? newMap[key]
                        : null;

                objectDiff.components.Add(
                    CompareComponent(
                        oldComponent,
                        newComponent,
                        result));
            }
        }

        private static ComponentDiff CompareComponent(
            ComponentSnapshot oldComponent,
            ComponentSnapshot newComponent,
            PrefabDiffResult result)
        {
            ComponentDiff diff =
                new ComponentDiff();

            diff.oldComponent =
                oldComponent;

            diff.newComponent =
                newComponent;

            ComponentSnapshot source =
                newComponent ??
                oldComponent;

            if (source != null)
            {
                diff.typeName =
                    source.typeName;

                diff.displayName =
                    source.displayName;

                diff.typeIndex =
                    source.typeIndex;
            }

            if (oldComponent == null &&
                newComponent != null)
            {
                diff.changeType =
                    ChangeType.Added;

                result.addedComponents++;

                AddProperties(
                    null,
                    newComponent.properties,
                    diff,
                    result);

                return diff;
            }

            if (oldComponent != null &&
                newComponent == null)
            {
                diff.changeType =
                    ChangeType.Removed;

                result.removedComponents++;

                AddProperties(
                    oldComponent.properties,
                    null,
                    diff,
                    result);

                return diff;
            }

            Dictionary<string, PropertySnapshot> oldMap =
                BuildPropertyMap(
                    oldComponent.properties);

            Dictionary<string, PropertySnapshot> newMap =
                BuildPropertyMap(
                    newComponent.properties);

            List<string> keys =
                new List<string>();

            AddKeys(
                keys,
                oldMap);

            AddKeys(
                keys,
                newMap);

            bool modified =
                false;

            for (int i = 0;
                 i < keys.Count;
                 i++)
            {
                string key =
                    keys[i];

                PropertySnapshot oldProperty =
                    oldMap.ContainsKey(key)
                        ? oldMap[key]
                        : null;

                PropertySnapshot newProperty =
                    newMap.ContainsKey(key)
                        ? newMap[key]
                        : null;

                PropertyDiff propertyDiff =
                    CompareProperty(
                        oldProperty,
                        newProperty,
                        result);

                diff.properties.Add(
                    propertyDiff);

                if (propertyDiff.changeType !=
                    ChangeType.Unchanged)
                {
                    modified = true;
                }
            }

            diff.changeType =
                modified
                    ? ChangeType.Modified
                    : ChangeType.Unchanged;

            if (modified)
                result.modifiedComponents++;

            return diff;
        }

        private static PropertyDiff CompareProperty(
            PropertySnapshot oldProperty,
            PropertySnapshot newProperty,
            PrefabDiffResult result)
        {
            PropertyDiff diff =
                new PropertyDiff();

            diff.oldProperty =
                oldProperty;

            diff.newProperty =
                newProperty;

            PropertySnapshot source =
                newProperty ??
                oldProperty;

            if (source != null)
            {
                diff.path =
                    source.path;

                diff.displayName =
                    source.displayName;
            }

            if (oldProperty == null &&
                newProperty != null)
            {
                diff.changeType =
                    ChangeType.Added;

                diff.oldValue =
                    "";

                diff.newValue =
                    newProperty.value;

                result.modifiedProperties++;

                return diff;
            }

            if (oldProperty != null &&
                newProperty == null)
            {
                diff.changeType =
                    ChangeType.Removed;

                diff.oldValue =
                    oldProperty.value;

                diff.newValue =
                    "";

                result.modifiedProperties++;

                return diff;
            }

            diff.oldValue =
                oldProperty.value;

            diff.newValue =
                newProperty.value;

            bool changed =
                oldProperty.value !=
                newProperty.value ||
                oldProperty.typeName !=
                newProperty.typeName;

            diff.changeType =
                changed
                    ? ChangeType.Modified
                    : ChangeType.Unchanged;

            if (changed)
                result.modifiedProperties++;

            return diff;
        }

        private static Dictionary<string, ComponentSnapshot>
            BuildComponentMap(
                List<ComponentSnapshot> components)
        {
            Dictionary<string, ComponentSnapshot> map =
                new Dictionary<string, ComponentSnapshot>();

            for (int i = 0;
                 i < components.Count;
                 i++)
            {
                ComponentSnapshot component =
                    components[i];

                string key =
                    component.typeName +
                    "#" +
                    component.typeIndex;

                map[key] =
                    component;
            }

            return map;
        }

        private static Dictionary<string, PropertySnapshot>
            BuildPropertyMap(
                List<PropertySnapshot> properties)
        {
            Dictionary<string, PropertySnapshot> map =
                new Dictionary<string, PropertySnapshot>();

            for (int i = 0;
                 i < properties.Count;
                 i++)
            {
                PropertySnapshot property =
                    properties[i];

                map[property.path] =
                    property;
            }

            return map;
        }

        private static void AddAllComponents(
            List<ComponentSnapshot> oldComponents,
            List<ComponentSnapshot> newComponents,
            ObjectDiff objectDiff,
            PrefabDiffResult result)
        {
            List<ComponentSnapshot> source =
                newComponents ??
                oldComponents;

            if (source == null)
                return;

            bool added =
                newComponents != null;

            for (int i = 0;
                 i < source.Count;
                 i++)
            {
                ComponentSnapshot component =
                    source[i];

                objectDiff.components.Add(
                    CompareComponent(
                        added
                            ? null
                            : component,
                        added
                            ? component
                            : null,
                        result));
            }
        }

        private static void AddProperties(
            List<PropertySnapshot> oldProperties,
            List<PropertySnapshot> newProperties,
            ComponentDiff componentDiff,
            PrefabDiffResult result)
        {
            List<PropertySnapshot> source =
                newProperties ??
                oldProperties;

            if (source == null)
                return;

            bool added =
                newProperties != null;

            for (int i = 0;
                 i < source.Count;
                 i++)
            {
                PropertySnapshot property =
                    source[i];

                componentDiff.properties.Add(
                    CompareProperty(
                        added
                            ? null
                            : property,
                        added
                            ? property
                            : null,
                        result));
            }
        }

        private static void AddKeys<T>(
            List<string> keys,
            Dictionary<string, T> map)
        {
            foreach (string key in map.Keys)
            {
                if (!keys.Contains(key))
                {
                    keys.Add(key);
                }
            }
        }
    }
}
