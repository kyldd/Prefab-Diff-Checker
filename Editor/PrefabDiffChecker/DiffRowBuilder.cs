using System.Collections.Generic;

namespace PrefabDiffChecker
{
    public enum DiffRowType
    {
        Object,
        Component,
        Property
    }

    public class DiffRow
    {
        public DiffRowType rowType;
        public ChangeType changeType;

        public int depth;

        public string id;

        public string leftName;
        public string rightName;

        public string leftValue;
        public string rightValue;

        public string searchText;

        public bool hasLeft;
        public bool hasRight;

        public bool expandable;
        public bool expanded = true;

        public int changeCount;

        public ObjectDiff objectDiff;
        public ComponentDiff componentDiff;
        public PropertyDiff propertyDiff;

        public PropertySnapshot leftProperty;
        public PropertySnapshot rightProperty;

        public DiffRow parent;

        public readonly List<DiffRow> children =
            new List<DiffRow>();

        public bool IsChanged
        {
            get
            {
                return changeType !=
                       ChangeType.Unchanged;
            }
        }
    }

    public static class DiffRowBuilder
    {
        public static List<DiffRow> Build(
            PrefabDiffResult result)
        {
            List<DiffRow> roots =
                new List<DiffRow>();

            if (result == null ||
                result.root == null)
            {
                return roots;
            }

            DiffRow root =
                BuildObject(
                    result.root,
                    null,
                    0,
                    "root");

            if (root != null)
                roots.Add(root);

            return roots;
        }

        private static DiffRow BuildObject(
            ObjectDiff diff,
            DiffRow parent,
            int depth,
            string id)
        {
            if (diff == null)
                return null;

            DiffRow row =
                new DiffRow();

            row.rowType =
                DiffRowType.Object;

            row.changeType =
                diff.changeType;

            row.depth =
                depth;

            row.id =
                id;

            row.parent =
                parent;

            row.objectDiff =
                diff;

            row.hasLeft =
                diff.oldObject != null;

            row.hasRight =
                diff.newObject != null;

            row.leftName =
                row.hasLeft
                    ? diff.oldObject.name
                    : "";

            row.rightName =
                row.hasRight
                    ? diff.newObject.name
                    : "";

            row.searchText =
                CombineSearchText(
                    row.leftName,
                    row.rightName,
                    diff.hierarchyPath,
                    diff.hierarchyKey);

            if (diff.components != null)
            {
                for (int i = 0;
                     i < diff.components.Count;
                     i++)
                {
                    DiffRow component =
                        BuildComponent(
                            diff.components[i],
                            row,
                            depth + 1,
                            id + "/c" + i);

                    if (component != null)
                        row.children.Add(component);
                }
            }

            if (diff.children != null)
            {
                for (int i = 0;
                     i < diff.children.Count;
                     i++)
                {
                    DiffRow child =
                        BuildObject(
                            diff.children[i],
                            row,
                            depth + 1,
                            id + "/o" + i);

                    if (child != null)
                        row.children.Add(child);
                }
            }

            row.expandable =
                row.children.Count > 0;

            row.expanded =
                diff.expanded;

            row.changeCount =
                CountChanges(row);

            return row;
        }

        private static DiffRow BuildComponent(
            ComponentDiff diff,
            DiffRow parent,
            int depth,
            string id)
        {
            if (diff == null)
                return null;

            DiffRow row =
                new DiffRow();

            row.rowType =
                DiffRowType.Component;

            row.changeType =
                diff.changeType;

            row.depth =
                depth;

            row.id =
                id;

            row.parent =
                parent;

            row.componentDiff =
                diff;

            row.hasLeft =
                diff.oldComponent != null;

            row.hasRight =
                diff.newComponent != null;

            row.leftName =
                GetComponentName(
                    diff.oldComponent);

            row.rightName =
                GetComponentName(
                    diff.newComponent);

            row.searchText =
                CombineSearchText(
                    row.leftName,
                    row.rightName,
                    diff.typeName);

            if (diff.properties != null)
            {
                for (int i = 0;
                     i < diff.properties.Count;
                     i++)
                {
                    DiffRow property =
                        BuildProperty(
                            diff.properties[i],
                            row,
                            depth + 1,
                            id + "/p" + i);

                    if (property != null)
                        row.children.Add(property);
                }
            }

            row.expandable =
                row.children.Count > 0;

            row.expanded =
                diff.expanded;

            row.changeCount =
                CountChanges(row);

            return row;
        }

        private static DiffRow BuildProperty(
            PropertyDiff diff,
            DiffRow parent,
            int depth,
            string id)
        {
            if (diff == null)
                return null;

            DiffRow row =
                new DiffRow();

            row.rowType =
                DiffRowType.Property;

            row.changeType =
                diff.changeType;

            row.depth =
                depth;

            row.id =
                id;

            row.parent =
                parent;

            row.propertyDiff =
                diff;

            row.leftProperty =
                diff.oldProperty;

            row.rightProperty =
                diff.newProperty;

            row.hasLeft =
                diff.oldProperty != null;

            row.hasRight =
                diff.newProperty != null;

            row.leftName =
                row.hasLeft
                    ? diff.oldProperty.displayName
                    : "";

            row.rightName =
                row.hasRight
                    ? diff.newProperty.displayName
                    : "";

            row.leftValue =
                row.hasLeft
                    ? diff.oldProperty.value
                    : "";

            row.rightValue =
                row.hasRight
                    ? diff.newProperty.value
                    : "";

            row.searchText =
                CombineSearchText(
                    diff.displayName,
                    diff.path,
                    diff.oldValue,
                    diff.newValue);

            row.expandable = false;
            row.expanded = false;

            row.changeCount =
                row.IsChanged
                    ? 1
                    : 0;

            return row;
        }

        private static string GetComponentName(
            ComponentSnapshot component)
        {
            if (component == null)
                return "";

            string result =
                string.IsNullOrEmpty(
                    component.displayName)
                    ? component.typeName
                    : component.displayName;

            if (component.typeIndex > 0)
            {
                result +=
                    " [" +
                    component.typeIndex +
                    "]";
            }

            return result;
        }

        private static int CountChanges(
            DiffRow row)
        {
            if (row == null)
                return 0;

            int count =
                row.rowType ==
                DiffRowType.Property &&
                row.IsChanged
                    ? 1
                    : 0;

            for (int i = 0;
                 i < row.children.Count;
                 i++)
            {
                count +=
                    CountChanges(
                        row.children[i]);
            }

            if (count == 0 &&
                row.IsChanged)
            {
                count = 1;
            }

            return count;
        }

        private static string CombineSearchText(
            params string[] values)
        {
            if (values == null)
                return "";

            string result = "";

            for (int i = 0;
                 i < values.Length;
                 i++)
            {
                if (string.IsNullOrEmpty(
                        values[i]))
                {
                    continue;
                }

                if (result.Length > 0)
                    result += " ";

                result +=
                    values[i];
            }

            return result;
        }
    }
}
