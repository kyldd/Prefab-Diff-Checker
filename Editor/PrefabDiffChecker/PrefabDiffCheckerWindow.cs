using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PrefabDiffChecker
{
    public class PrefabDiffCheckerWindow : EditorWindow
    {
        private const string LastRefKey = "PrefabDiffChecker.LastRef";

        private const float RowHeight = 24f;
        private const float DiffHeaderHeight = 30f;
        private const float DividerWidth = 1f;
        private const float GutterWidth = 3f;
        private const float MinPaneWidth = 260f;

        private GameObject localPrefab;
        private GitPrefabInfo gitInfo;
        private string gitRef;

        private PrefabDiffResult diff;
        private List<DiffRow> rootRows = new List<DiffRow>();
        private readonly List<DiffRow> visibleRows = new List<DiffRow>();
        private readonly List<int> changedRows = new List<int>();

        private Vector2 scroll;
        private int selectedRow = -1;
        private int navigationIndex = -1;

        private bool showAdded = true;
        private bool showRemoved = true;
        private bool showModified = true;
        private bool showMoved = true;

        private string message;
        private MessageType messageType = MessageType.None;

        private GUIStyle titleStyle;
        private GUIStyle sectionLabelStyle;
        private GUIStyle headerStyle;
        private GUIStyle objectStyle;
        private GUIStyle componentStyle;
        private GUIStyle propertyStyle;
        private GUIStyle valueStyle;
        private GUIStyle secondaryStyle;
        private GUIStyle countStyle;

        private readonly Color addedColor = new Color(0.30f, 0.72f, 0.40f);
        private readonly Color removedColor = new Color(0.82f, 0.36f, 0.36f);
        private readonly Color modifiedColor = new Color(0.88f, 0.66f, 0.25f);
        private readonly Color movedColor = new Color(0.38f, 0.58f, 0.85f);
        private readonly Color readyColor = new Color(0.30f, 0.75f, 0.40f);

        [MenuItem("Tools/Prefab Diff Checker")]
        public static void Open()
        {
            var window = GetWindow<PrefabDiffCheckerWindow>();
            window.titleContent = new GUIContent("Prefab Diff Checker");
            window.minSize = new Vector2(900f, 520f);
            window.Show();
        }

        private void OnEnable()
        {
            gitRef = EditorPrefs.GetString(LastRefKey, "origin/develop");
        }

        private void OnDisable()
        {
            TemporaryPrefabLoader.CleanupAll();
        }

        private void OnGUI()
        {
            SetupStyles();

            DrawHeader();

            GUILayout.Space(10f);

            DrawSources();

            if (!string.IsNullOrEmpty(message))
            {
                GUILayout.Space(6f);
                DrawMessage();
            }

            GUILayout.Space(10f);

            DrawToolbar();

            if (diff == null)
            {
                DrawEmptyState();
                return;
            }

            BuildVisibleRows();

            GUILayout.Space(4f);

            DrawSummary();
            DrawDiffHeader();
            DrawDiff();
        }

        private void DrawHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(false, 34f);

            GUI.Label(
                new Rect(rect.x, rect.y + 5f, 240f, 24f),
                "Prefab Diff Checker",
                titleStyle);

            string status = gitInfo != null ? "Git: Ready" : "Git: Not Ready";
            Color statusColor = gitInfo != null ? readyColor : Color.gray;

            Vector2 textSize = secondaryStyle.CalcSize(new GUIContent(status));
            float width = textSize.x + 22f;

            Rect statusRect = new Rect(
                rect.xMax - width,
                rect.y + 8f,
                width,
                20f);

            Rect dotRect = new Rect(
                statusRect.x,
                statusRect.y + 6f,
                8f,
                8f);

            EditorGUI.DrawRect(dotRect, statusColor);

            GUI.Label(
                new Rect(
                    statusRect.x + 14f,
                    statusRect.y,
                    statusRect.width - 14f,
                    statusRect.height),
                status,
                secondaryStyle);

            Rect line = new Rect(
                rect.x,
                rect.yMax - 1f,
                rect.width,
                1f);

            EditorGUI.DrawRect(line, BorderColor());
        }

        private void DrawSources()
        {
            EditorGUILayout.BeginHorizontal();

            DrawLocalSource();

            GUILayout.Space(24f);

            DrawGitSource();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(10f);

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            bool oldEnabled = GUI.enabled;

            GUI.enabled =
                localPrefab != null &&
                gitInfo != null &&
                !string.IsNullOrWhiteSpace(gitRef);

            if (GUILayout.Button(
                    "Compare",
                    GUILayout.Width(150f),
                    GUILayout.Height(30f)))
            {
                Compare();
            }

            GUI.enabled = oldEnabled;

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawLocalSource()
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(360f));

            GUILayout.Label("LOCAL PREFAB", sectionLabelStyle);

            GameObject newPrefab = (GameObject)EditorGUILayout.ObjectField(
                localPrefab,
                typeof(GameObject),
                false,
                GUILayout.Height(20f));

            if (newPrefab != localPrefab)
            {
                localPrefab = newPrefab;
                ResolveRepository();
            }

            if (localPrefab == null)
            {
                GUILayout.Label(
                    "Select a prefab asset.",
                    secondaryStyle);

                EditorGUILayout.EndVertical();
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(localPrefab);

            GUILayout.Label(
                assetPath,
                secondaryStyle);

            if (gitInfo != null &&
                !string.IsNullOrEmpty(gitInfo.currentBranch))
            {
                GUILayout.Label(
                    "Current branch: " + gitInfo.currentBranch,
                    secondaryStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawGitSource()
        {
            EditorGUILayout.BeginVertical(GUILayout.MinWidth(360f));

            GUILayout.Label("COMPARE AGAINST", sectionLabelStyle);

            EditorGUI.BeginChangeCheck();

            gitRef = EditorGUILayout.TextField(
                gitRef ?? string.Empty,
                GUILayout.Height(20f));

            if (EditorGUI.EndChangeCheck())
            {
                ClearMessage();
            }

            GUILayout.Label(
                "Example: origin/develop, HEAD, HEAD~1, commit SHA",
                secondaryStyle);

            if (gitInfo != null)
            {
                GUILayout.Label(
                    gitInfo.repositoryRelativePath,
                    secondaryStyle);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawMessage()
        {
            if (messageType == MessageType.None)
            {
                GUILayout.Label(message, secondaryStyle);
                return;
            }

            EditorGUILayout.HelpBox(message, messageType);
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            showAdded = GUILayout.Toggle(
                showAdded,
                "Added",
                EditorStyles.toolbarButton);

            showRemoved = GUILayout.Toggle(
                showRemoved,
                "Removed",
                EditorStyles.toolbarButton);

            showModified = GUILayout.Toggle(
                showModified,
                "Modified",
                EditorStyles.toolbarButton);

            showMoved = GUILayout.Toggle(
                showMoved,
                "Moved",
                EditorStyles.toolbarButton);

            GUILayout.FlexibleSpace();

            bool oldEnabled = GUI.enabled;
            GUI.enabled = changedRows.Count > 0;

            if (GUILayout.Button(
                    "<",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(28f)))
            {
                Navigate(-1);
            }

            string navigationText = changedRows.Count == 0
                ? "0 / 0"
                : (navigationIndex >= 0 ? navigationIndex + 1 : 0) +
                  " / " +
                  changedRows.Count;

            GUILayout.Label(
                navigationText,
                secondaryStyle,
                GUILayout.Width(55f));

            if (GUILayout.Button(
                    ">",
                    EditorStyles.toolbarButton,
                    GUILayout.Width(28f)))
            {
                Navigate(1);
            }

            GUI.enabled = oldEnabled;

            GUILayout.Space(8f);

            if (GUILayout.Button(
                    "Expand All",
                    EditorStyles.toolbarButton))
            {
                SetAllExpanded(true);
            }

            if (GUILayout.Button(
                    "Collapse All",
                    EditorStyles.toolbarButton))
            {
                SetAllExpanded(false);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawSummary()
        {
            int added = CountRows(ChangeType.Added);
            int removed = CountRows(ChangeType.Removed);
            int modified = CountRows(ChangeType.Modified);
            int moved = CountRows(ChangeType.Moved);

            int total = added + removed + modified + moved;

            EditorGUILayout.BeginHorizontal();

            GUILayout.Label(
                total + (total == 1 ? " change" : " changes"),
                EditorStyles.boldLabel);

            GUILayout.Space(14f);

            DrawSummaryValue(added, "added", addedColor);
            DrawSummaryValue(removed, "removed", removedColor);
            DrawSummaryValue(modified, "modified", modifiedColor);

            if (moved > 0)
            {
                DrawSummaryValue(moved, "moved", movedColor);
            }

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();

            GUILayout.Space(3f);
        }

        private void DrawSummaryValue(int count, string label, Color color)
        {
            Color oldColor = GUI.contentColor;

            GUI.contentColor = color;

            GUILayout.Label(
                count.ToString(),
                EditorStyles.boldLabel);

            GUI.contentColor = oldColor;

            GUILayout.Label(
                label,
                secondaryStyle);

            GUILayout.Space(10f);
        }

        private void DrawDiffHeader()
        {
            Rect rect = EditorGUILayout.GetControlRect(
                false,
                DiffHeaderHeight);

            float divider = GetDivider(rect);

            Rect left = new Rect(
                rect.x,
                rect.y,
                divider - rect.x,
                rect.height);

            Rect right = new Rect(
                divider + DividerWidth,
                rect.y,
                rect.xMax - divider - DividerWidth,
                rect.height);

            EditorGUI.DrawRect(left, HeaderBackgroundColor());
            EditorGUI.DrawRect(right, HeaderBackgroundColor());

            EditorGUI.DrawRect(
                new Rect(
                    divider,
                    rect.y,
                    DividerWidth,
                    rect.height),
                BorderColor());

            string leftName = string.IsNullOrWhiteSpace(gitRef)
                ? "GIT"
                : gitRef.Trim();

            string rightName = localPrefab != null
                ? localPrefab.name
                : "LOCAL";

            GUI.Label(
                new Rect(
                    left.x + 10f,
                    left.y + 5f,
                    left.width - 20f,
                    20f),
                "GIT  " + leftName,
                headerStyle);

            GUI.Label(
                new Rect(
                    right.x + 10f,
                    right.y + 5f,
                    right.width - 20f,
                    20f),
                "LOCAL  " + rightName,
                headerStyle);
        }

        private void DrawDiff()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < visibleRows.Count; i++)
            {
                DrawRow(visibleRows[i], i);
            }

            GUILayout.Space(8f);

            EditorGUILayout.EndScrollView();
        }

        private void DrawRow(DiffRow row, int index)
        {
            Rect rect = EditorGUILayout.GetControlRect(
                false,
                RowHeight);

            float divider = GetDivider(rect);

            Rect left = new Rect(
                rect.x,
                rect.y,
                divider - rect.x,
                rect.height);

            Rect right = new Rect(
                divider + DividerWidth,
                rect.y,
                rect.xMax - divider - DividerWidth,
                rect.height);

            DrawRowBackground(left, row, true);
            DrawRowBackground(right, row, false);

            if (selectedRow == index)
            {
                EditorGUI.DrawRect(
                    rect,
                    new Color(0.30f, 0.48f, 0.72f, 0.18f));
            }

            EditorGUI.DrawRect(
                new Rect(
                    divider,
                    rect.y,
                    DividerWidth,
                    rect.height),
                BorderColor());

            DrawRowSide(left, row, true);
            DrawRowSide(right, row, false);

            HandleRowInput(rect, left, row, index);
        }

        private void DrawRowSide(
            Rect rect,
            DiffRow row,
            bool leftSide)
        {
            bool exists = leftSide
                ? row.hasLeft
                : row.hasRight;

            if (!exists)
                return;

            DrawChangeGutter(rect, row, leftSide);

            float x = rect.x + 9f + row.depth * 16f;

            if (row.expandable)
            {
                Rect foldoutRect = new Rect(
                    x,
                    rect.y + 4f,
                    14f,
                    16f);

                bool expanded = EditorGUI.Foldout(
                    foldoutRect,
                    row.expanded,
                    GUIContent.none);

                if (expanded != row.expanded)
                {
                    SetExpanded(row, expanded);
                }
            }

            x += 18f;

            string name = leftSide
                ? row.leftName
                : row.rightName;

            string value = leftSide
                ? row.leftValue
                : row.rightValue;

            if (row.rowType == DiffRowType.Property)
            {
                DrawProperty(
                    rect,
                    x,
                    name,
                    value,
                    row,
                    leftSide);

                return;
            }

            GUIStyle style = row.rowType == DiffRowType.Object
                ? objectStyle
                : componentStyle;

            float countSpace = row.changeCount > 0 ? 58f : 10f;

            Rect labelRect = new Rect(
                x,
                rect.y + 2f,
                Mathf.Max(20f, rect.xMax - x - countSpace),
                20f);

            GUI.Label(
                labelRect,
                name ?? string.Empty,
                style);

            if (row.changeCount > 0)
            {
                GUI.Label(
                    new Rect(
                        rect.xMax - 52f,
                        rect.y + 3f,
                        42f,
                        18f),
                    row.changeCount.ToString(),
                    countStyle);
            }
        }

        private void DrawProperty(
            Rect rect,
            float x,
            string name,
            string value,
            DiffRow row,
            bool leftSide)
        {
            float width = rect.xMax - x - 10f;

            if (width <= 20f)
                return;

            float nameWidth = Mathf.Clamp(
                width * 0.40f,
                100f,
                220f);

            nameWidth = Mathf.Min(
                nameWidth,
                Mathf.Max(20f, width - 30f));

            Rect nameRect = new Rect(
                x,
                rect.y + 2f,
                nameWidth,
                20f);

            Rect valueRect = new Rect(
                nameRect.xMax + 6f,
                rect.y + 2f,
                Mathf.Max(
                    20f,
                    rect.xMax - nameRect.xMax - 14f),
                20f);

            GUI.Label(
                nameRect,
                name ?? string.Empty,
                propertyStyle);

            PropertySnapshot property = leftSide
                ? row.leftProperty
                : row.rightProperty;

            if (property != null &&
                property.valueKind == ValueKind.Color)
            {
                DrawColorProperty(
                    valueRect,
                    property,
                    value);

                return;
            }

            if (property != null &&
                property.valueKind == ValueKind.ObjectReference)
            {
                DrawReferenceProperty(
                    valueRect,
                    property,
                    value,
                    leftSide);

                return;
            }

            GUI.Label(
                valueRect,
                value ?? string.Empty,
                valueStyle);
        }

        private void DrawColorProperty(
            Rect rect,
            PropertySnapshot property,
            string value)
        {
            Color color;

            if (!string.IsNullOrEmpty(property.value) &&
                ColorUtility.TryParseHtmlString(property.value, out color))
            {
                Rect swatch = new Rect(
                    rect.x,
                    rect.y + 4f,
                    12f,
                    12f);

                EditorGUI.DrawRect(swatch, color);

                GUI.Label(
                    new Rect(
                        rect.x + 18f,
                        rect.y,
                        rect.width - 18f,
                        rect.height),
                    value ?? string.Empty,
                    valueStyle);

                return;
            }

            GUI.Label(
                rect,
                value ?? string.Empty,
                valueStyle);
        }

        private void DrawReferenceProperty(
            Rect rect,
            PropertySnapshot property,
            string value,
            bool leftSide)
        {
            if (property.objectReference == null)
            {
                GUI.Label(rect, "None", valueStyle);
                return;
            }

            bool canPing = !leftSide;

            float buttonWidth =
                canPing && rect.width > 120f
                    ? 40f
                    : 0f;

            Rect valueRect = rect;

            if (buttonWidth > 0f)
            {
                valueRect.width -= buttonWidth + 4f;
            }

            GUI.Label(
                valueRect,
                new GUIContent(
                    value ?? string.Empty,
                    property.objectReferencePath),
                valueStyle);

            if (buttonWidth <= 0f)
                return;

            Rect buttonRect = new Rect(
                valueRect.xMax + 4f,
                rect.y + 2f,
                buttonWidth,
                17f);

            if (GUI.Button(
                    buttonRect,
                    "Ping",
                    EditorStyles.miniButton))
            {
                Selection.activeObject = property.objectReference;
                EditorGUIUtility.PingObject(property.objectReference);
            }
        }

        private void DrawRowBackground(
            Rect rect,
            DiffRow row,
            bool leftSide)
        {
            bool exists = leftSide
                ? row.hasLeft
                : row.hasRight;

            if (!exists)
            {
                EditorGUI.DrawRect(
                    rect,
                    MissingSideColor());

                return;
            }

            Color color = BaseRowColor(row.rowType);

            switch (row.changeType)
            {
                case ChangeType.Added:
                    if (!leftSide)
                        color = Tint(addedColor, 0.13f);
                    break;

                case ChangeType.Removed:
                    if (leftSide)
                        color = Tint(removedColor, 0.13f);
                    break;

                case ChangeType.Modified:
                    if (row.rowType == DiffRowType.Property)
                    {
                        color = leftSide
                            ? Tint(removedColor, 0.11f)
                            : Tint(addedColor, 0.11f);
                    }
                    else
                    {
                        color = Tint(modifiedColor, 0.08f);
                    }
                    break;

                case ChangeType.Moved:
                    color = Tint(movedColor, 0.10f);
                    break;
            }

            EditorGUI.DrawRect(rect, color);
        }

        private void DrawChangeGutter(
            Rect rect,
            DiffRow row,
            bool leftSide)
        {
            Color color;

            switch (row.changeType)
            {
                case ChangeType.Added:
                    if (leftSide)
                        return;
                    color = addedColor;
                    break;

                case ChangeType.Removed:
                    if (!leftSide)
                        return;
                    color = removedColor;
                    break;

                case ChangeType.Modified:
                    color = row.rowType == DiffRowType.Property
                        ? (leftSide ? removedColor : addedColor)
                        : modifiedColor;
                    break;

                case ChangeType.Moved:
                    color = movedColor;
                    break;

                default:
                    return;
            }

            EditorGUI.DrawRect(
                new Rect(
                    rect.x,
                    rect.y,
                    GutterWidth,
                    rect.height),
                color);
        }

        private void HandleRowInput(
            Rect rect,
            Rect left,
            DiffRow row,
            int index)
        {
            Event evt = Event.current;

            if (evt.type != EventType.MouseDown)
                return;

            if (!rect.Contains(evt.mousePosition))
                return;

            selectedRow = index;

            if (evt.button == 0 && evt.clickCount == 2)
            {
                if (!left.Contains(evt.mousePosition))
                {
                    PingLocalObject(row);
                }

                evt.Use();
            }

            Repaint();
        }

        private void ResolveRepository()
        {
            gitInfo = null;
            ClearDiff();
            ClearMessage();

            if (localPrefab == null)
                return;

            string assetPath = AssetDatabase.GetAssetPath(localPrefab);

            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.EndsWith(
                    ".prefab",
                    StringComparison.OrdinalIgnoreCase))
            {
                SetMessage(
                    "Select a prefab asset from the Project window.",
                    MessageType.Warning);

                return;
            }

            string error;

            if (!GitPrefabSource.TryResolve(
                    localPrefab,
                    out gitInfo,
                    out error))
            {
                SetMessage(
                    error,
                    MessageType.Warning);

                return;
            }

            ClearMessage();
        }

        private void Compare()
        {
            if (localPrefab == null || gitInfo == null)
                return;

            string revision = (gitRef ?? string.Empty).Trim();

            if (revision.Length == 0)
                return;

            try
            {
                ClearMessage();

                if (!GitRunner.RevisionExists(
                        gitInfo.repositoryRoot,
                        revision))
                {
                    SetMessage(
                        "Git ref not found locally. For a remote branch, use its remote-tracking name, for example origin/develop.",
                        MessageType.Error);

                    return;
                }

                PrefabSnapshot localSnapshot =
                    PrefabSnapshotBuilder.Build(localPrefab);

                if (localSnapshot == null)
                {
                    SetMessage(
                        "Could not read the local prefab.",
                        MessageType.Error);

                    return;
                }

                PrefabSnapshot gitSnapshot;
                string error;

                if (!TemporaryPrefabLoader.TryBuildSnapshot(
                        gitInfo,
                        revision,
                        out gitSnapshot,
                        out error))
                {
                    SetMessage(
                        error,
                        MessageType.Error);

                    return;
                }

                diff = PrefabDiffEngine.Compare(
                    gitSnapshot,
                    localSnapshot);

                rootRows = DiffRowBuilder.Build(diff);

                selectedRow = -1;
                navigationIndex = -1;
                scroll = Vector2.zero;

                EditorPrefs.SetString(
                    LastRefKey,
                    revision);

                gitRef = revision;

                BuildVisibleRows();
            }
            catch (Exception exception)
            {
                ClearDiff();

                SetMessage(
                    "Comparison failed. Check the Console for details.",
                    MessageType.Error);

                Debug.LogError(
                    "[Prefab Diff Checker] Comparison failed.\n" +
                    exception);
            }
        }

        private void BuildVisibleRows()
        {
            visibleRows.Clear();
            changedRows.Clear();

            for (int i = 0; i < rootRows.Count; i++)
            {
                AddVisibleRow(rootRows[i]);
            }

            for (int i = 0; i < visibleRows.Count; i++)
            {
                if (visibleRows[i].IsChanged &&
                    PassesFilter(visibleRows[i]))
                {
                    changedRows.Add(i);
                }
            }

            if (selectedRow >= visibleRows.Count)
                selectedRow = -1;

            if (navigationIndex >= changedRows.Count)
                navigationIndex = -1;
        }

        private void AddVisibleRow(DiffRow row)
        {
            if (row == null)
                return;

            if (!HasVisibleChange(row))
                return;

            visibleRows.Add(row);

            if (!row.expanded)
                return;

            for (int i = 0; i < row.children.Count; i++)
            {
                AddVisibleRow(row.children[i]);
            }
        }

        private bool HasVisibleChange(DiffRow row)
        {
            if (row.IsChanged && PassesFilter(row))
                return true;

            for (int i = 0; i < row.children.Count; i++)
            {
                if (HasVisibleChange(row.children[i]))
                    return true;
            }

            return false;
        }

        private bool PassesFilter(DiffRow row)
        {
            switch (row.changeType)
            {
                case ChangeType.Added:
                    return showAdded;

                case ChangeType.Removed:
                    return showRemoved;

                case ChangeType.Modified:
                    return showModified;

                case ChangeType.Moved:
                    return showMoved;

                default:
                    return false;
            }
        }

        private int CountRows(ChangeType type)
        {
            int count = 0;

            for (int i = 0; i < visibleRows.Count; i++)
            {
                if (visibleRows[i].changeType == type)
                    count++;
            }

            return count;
        }

        private void Navigate(int direction)
        {
            BuildVisibleRows();

            if (changedRows.Count == 0)
                return;

            if (navigationIndex < 0)
            {
                navigationIndex = direction > 0
                    ? 0
                    : changedRows.Count - 1;
            }
            else
            {
                navigationIndex += direction;

                if (navigationIndex < 0)
                    navigationIndex = changedRows.Count - 1;

                if (navigationIndex >= changedRows.Count)
                    navigationIndex = 0;
            }

            selectedRow = changedRows[navigationIndex];

            scroll.y = Mathf.Max(
                0f,
                selectedRow * RowHeight -
                position.height * 0.35f);

            Repaint();
        }

        private void SetExpanded(
            DiffRow row,
            bool expanded)
        {
            row.expanded = expanded;

            if (row.objectDiff != null)
                row.objectDiff.expanded = expanded;

            if (row.componentDiff != null)
                row.componentDiff.expanded = expanded;

            Repaint();
        }

        private void SetAllExpanded(bool expanded)
        {
            for (int i = 0; i < rootRows.Count; i++)
            {
                SetExpandedRecursive(
                    rootRows[i],
                    expanded);
            }

            Repaint();
        }

        private void SetExpandedRecursive(
            DiffRow row,
            bool expanded)
        {
            row.expanded = expanded;

            if (row.objectDiff != null)
                row.objectDiff.expanded = expanded;

            if (row.componentDiff != null)
                row.componentDiff.expanded = expanded;

            for (int i = 0; i < row.children.Count; i++)
            {
                SetExpandedRecursive(
                    row.children[i],
                    expanded);
            }
        }

        private void PingLocalObject(DiffRow row)
        {
            if (localPrefab == null)
                return;

            DiffRow objectRow = FindObjectRow(row);

            if (objectRow == null ||
                objectRow.objectDiff == null ||
                objectRow.objectDiff.newObject == null)
            {
                return;
            }

            Transform target = FindTransform(
                localPrefab.transform,
                objectRow.objectDiff.newObject.hierarchyKey);

            if (target == null)
                return;

            Selection.activeGameObject = target.gameObject;
            EditorGUIUtility.PingObject(target.gameObject);
        }

        private static DiffRow FindObjectRow(DiffRow row)
        {
            DiffRow current = row;

            while (current != null)
            {
                if (current.rowType == DiffRowType.Object)
                    return current;

                current = current.parent;
            }

            return null;
        }

        private static Transform FindTransform(
            Transform root,
            string hierarchyKey)
        {
            if (root == null ||
                string.IsNullOrEmpty(hierarchyKey))
            {
                return null;
            }

            string[] parts = hierarchyKey.Split('/');
            Transform current = root;

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i];

                int open = part.LastIndexOf('[');
                int close = part.LastIndexOf(']');

                if (open < 0 || close <= open)
                    return null;

                string objectName = part.Substring(0, open);
                string indexText = part.Substring(
                    open + 1,
                    close - open - 1);

                int siblingIndex;

                if (!int.TryParse(indexText, out siblingIndex))
                    return null;

                Transform found = null;

                for (int childIndex = 0;
                     childIndex < current.childCount;
                     childIndex++)
                {
                    Transform child = current.GetChild(childIndex);

                    if (child.name == objectName &&
                        child.GetSiblingIndex() == siblingIndex)
                    {
                        found = child;
                        break;
                    }
                }

                if (found == null)
                    return null;

                current = found;
            }

            return current;
        }

        private float GetDivider(Rect rect)
        {
            float divider = rect.center.x;

            float min = rect.x + Mathf.Min(
                MinPaneWidth,
                rect.width * 0.45f);

            float max = rect.xMax - Mathf.Min(
                MinPaneWidth,
                rect.width * 0.45f);

            if (max < min)
                return divider;

            return Mathf.Clamp(divider, min, max);
        }

        private void DrawEmptyState()
        {
            GUILayout.Space(60f);

            EditorGUILayout.BeginHorizontal();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical(
                GUILayout.Width(400f));

            GUIStyle centered = new GUIStyle(
                EditorStyles.boldLabel);

            centered.alignment = TextAnchor.MiddleCenter;
            centered.fontSize = 13;

            GUILayout.Label(
                localPrefab == null
                    ? "Select a prefab to begin"
                    : "Enter a Git ref and compare",
                centered);

            GUILayout.Space(5f);

            GUIStyle detail = new GUIStyle(
                secondaryStyle);

            detail.alignment = TextAnchor.MiddleCenter;

            GUILayout.Label(
                localPrefab == null
                    ? "The tool compares the working prefab against the same file in Git."
                    : "Only semantic prefab changes will be shown.",
                detail);

            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
        }

        private void SetMessage(
            string text,
            MessageType type)
        {
            message = text;
            messageType = type;
            Repaint();
        }

        private void ClearMessage()
        {
            message = null;
            messageType = MessageType.None;
        }

        private void ClearDiff()
        {
            diff = null;
            rootRows.Clear();
            visibleRows.Clear();
            changedRows.Clear();

            selectedRow = -1;
            navigationIndex = -1;
            scroll = Vector2.zero;
        }

        private void SetupStyles()
        {
            if (titleStyle != null)
                return;

            titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 15;

            sectionLabelStyle = new GUIStyle(EditorStyles.miniLabel);
            sectionLabelStyle.fontStyle = FontStyle.Bold;

            headerStyle = new GUIStyle(EditorStyles.boldLabel);
            headerStyle.alignment = TextAnchor.MiddleLeft;

            objectStyle = new GUIStyle(EditorStyles.boldLabel);
            objectStyle.alignment = TextAnchor.MiddleLeft;

            componentStyle = new GUIStyle(EditorStyles.label);
            componentStyle.fontStyle = FontStyle.Bold;
            componentStyle.alignment = TextAnchor.MiddleLeft;

            propertyStyle = new GUIStyle(EditorStyles.label);
            propertyStyle.alignment = TextAnchor.MiddleLeft;
            propertyStyle.clipping = TextClipping.Clip;

            valueStyle = new GUIStyle(EditorStyles.label);
            valueStyle.alignment = TextAnchor.MiddleLeft;
            valueStyle.clipping = TextClipping.Clip;

            secondaryStyle = new GUIStyle(EditorStyles.miniLabel);
            secondaryStyle.alignment = TextAnchor.MiddleLeft;

            countStyle = new GUIStyle(EditorStyles.miniLabel);
            countStyle.alignment = TextAnchor.MiddleRight;
            countStyle.normal.textColor = modifiedColor;
        }

        private Color BaseRowColor(DiffRowType type)
        {
            if (EditorGUIUtility.isProSkin)
            {
                switch (type)
                {
                    case DiffRowType.Object:
                        return new Color(0.18f, 0.18f, 0.18f);

                    case DiffRowType.Component:
                        return new Color(0.155f, 0.155f, 0.155f);

                    default:
                        return new Color(0.135f, 0.135f, 0.135f);
                }
            }

            switch (type)
            {
                case DiffRowType.Object:
                    return new Color(0.82f, 0.82f, 0.82f);

                case DiffRowType.Component:
                    return new Color(0.87f, 0.87f, 0.87f);

                default:
                    return new Color(0.91f, 0.91f, 0.91f);
            }
        }

        private Color HeaderBackgroundColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.12f, 0.12f, 0.12f)
                : new Color(0.76f, 0.76f, 0.76f);
        }

        private Color MissingSideColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.09f, 0.09f, 0.09f)
                : new Color(0.84f, 0.84f, 0.84f);
        }

        private Color BorderColor()
        {
            return EditorGUIUtility.isProSkin
                ? new Color(0.07f, 0.07f, 0.07f)
                : new Color(0.55f, 0.55f, 0.55f);
        }

        private Color Tint(Color color, float strength)
        {
            Color baseColor = EditorGUIUtility.isProSkin
                ? new Color(0.14f, 0.14f, 0.14f)
                : new Color(0.90f, 0.90f, 0.90f);

            return Color.Lerp(
                baseColor,
                color,
                strength);
        }
    }
}
