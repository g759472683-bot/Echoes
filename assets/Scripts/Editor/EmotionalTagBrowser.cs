#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Editor window for browsing and managing emotional tags (GDD emotional-tag-system §3 Rule 7).
///
/// Features:
///   - Tags grouped by EmotionCategory with reference counts
///   - Search/filter by TagId or DisplayName
///   - Rename tag — propagates TagId changes to all MemoryFragment assets
///   - Orphan detection — tags with 0 references marked "[UNUSED]"
///   - Safe delete — shows reference count, batch-removes from fragments, then deletes
///
/// Open via: Window > 回响 > Emotional Tag Browser
/// </summary>
public class EmotionalTagBrowser : EditorWindow
{
    private const string CATALOG_SEARCH_FILTER = "t:EmotionalTagCatalog";
    private const string FRAGMENT_SEARCH_FILTER = "t:MemoryFragment";

    // State
    private EmotionalTagCatalog _catalog;
    private Dictionary<string, int> _refCounts;
    private List<TagRowData> _allRows;
    private string _searchFilter = "";

    // UI references
    private ToolbarSearchField _searchField;
    private ScrollView _scrollView;
    private Label _statusLabel;

    // =========================================================================
    // Unity EditorWindow API
    // =========================================================================

    [MenuItem("回响/Emotional Tag Browser")]
    public static void ShowWindow()
    {
        var window = GetWindow<EmotionalTagBrowser>("Emotional Tags");
        window.minSize = new Vector2(420, 500);
        window.Show();
    }

    private void CreateGUI()
    {
        _catalog = FindCatalog();
        _refCounts = BuildReferenceCounts();

        var root = rootVisualElement;
        root.style.flexDirection = FlexDirection.Column;
        root.style.paddingTop = 4;
        root.style.paddingBottom = 4;
        root.style.paddingLeft = 6;
        root.style.paddingRight = 6;

        // --- Toolbar ---
        var toolbar = new Toolbar();
        _searchField = new ToolbarSearchField();
        _searchField.style.flexGrow = 1;
        _searchField.RegisterValueChangedCallback(evt =>
        {
            _searchFilter = evt.newValue ?? "";
            RebuildList();
        });
        toolbar.Add(_searchField);

        var refreshBtn = new ToolbarButton(() => RefreshData())
        {
            text = "Refresh"
        };
        toolbar.Add(refreshBtn);
        root.Add(toolbar);

        // --- Scrollable tag list ---
        _scrollView = new ScrollView();
        _scrollView.style.flexGrow = 1;
        root.Add(_scrollView);

        // --- Status bar ---
        _statusLabel = new Label();
        _statusLabel.style.paddingTop = 4;
        _statusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
        root.Add(_statusLabel);

        BuildRowData();
        RebuildList();
        UpdateStatus();
    }

    private void OnFocus()
    {
        RefreshData();
    }

    // =========================================================================
    // Data Refresh
    // =========================================================================

    private void RefreshData()
    {
        _catalog = FindCatalog();
        _refCounts = BuildReferenceCounts();
        BuildRowData();
        RebuildList();
        UpdateStatus();
    }

    private static EmotionalTagCatalog FindCatalog()
    {
        var guids = AssetDatabase.FindAssets(CATALOG_SEARCH_FILTER);
        if (guids.Length == 0) return null;
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<EmotionalTagCatalog>(path);
    }

    private static Dictionary<string, int> BuildReferenceCounts()
    {
        var counts = new Dictionary<string, int>();
        var fragGuids = AssetDatabase.FindAssets(FRAGMENT_SEARCH_FILTER);
        foreach (var guid in fragGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var frag = AssetDatabase.LoadAssetAtPath<MemoryFragment>(path);
            if (frag?.EmotionalTags == null) continue;
            foreach (var tag in frag.EmotionalTags)
            {
                if (string.IsNullOrEmpty(tag.TagId)) continue;
                counts.TryGetValue(tag.TagId, out int c);
                counts[tag.TagId] = c + 1;
            }
        }
        return counts;
    }

    // =========================================================================
    // Row Data
    // =========================================================================

    private struct TagRowData
    {
        public bool IsCategoryHeader;
        public EmotionCategory Category;
        public string TagId;
        public string DisplayName;
        public int RefCount;
        public int CatalogIndex; // index into _catalog.Tags (valid only for tag rows)
    }

    private void BuildRowData()
    {
        _allRows = new List<TagRowData>();

        if (_catalog == null || _catalog.Tags == null) return;

        var sortedCategories = _catalog.Tags
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        for (int i = 0; i < _catalog.Tags.Count; i++)
        {
            var tag = _catalog.Tags[i];
            if (string.IsNullOrEmpty(tag.TagId)) continue;

            _refCounts.TryGetValue(tag.TagId, out int refCount);
            _allRows.Add(new TagRowData
            {
                IsCategoryHeader = false,
                TagId = tag.TagId,
                DisplayName = tag.DisplayName,
                Category = tag.Category,
                RefCount = refCount,
                CatalogIndex = i
            });
        }
    }

    private IEnumerable<TagRowData> GetFilteredRows()
    {
        if (string.IsNullOrWhiteSpace(_searchFilter))
            return _allRows.OrderBy(r => r.Category).ThenBy(r => r.TagId);

        var filter = _searchFilter.ToLowerInvariant();
        return _allRows
            .Where(r =>
                r.TagId.ToLowerInvariant().Contains(filter) ||
                (r.DisplayName != null && r.DisplayName.ToLowerInvariant().Contains(filter)))
            .OrderBy(r => r.Category)
            .ThenBy(r => r.TagId);
    }

    // =========================================================================
    // UI Rebuild
    // =========================================================================

    private void RebuildList()
    {
        _scrollView.Clear();

        if (_catalog == null)
        {
            _scrollView.Add(new Label("No EmotionalTagCatalog found in the project.")
            {
                style = { paddingTop = 12, unityTextAlign = TextAnchor.MiddleCenter }
            });
            return;
        }

        var filtered = GetFilteredRows().ToList();

        if (filtered.Count == 0)
        {
            _scrollView.Add(new Label(
                string.IsNullOrWhiteSpace(_searchFilter)
                    ? "No tags defined in catalog."
                    : $"No tags matching '{_searchFilter}'.")
            {
                style = { paddingTop = 12, unityTextAlign = TextAnchor.MiddleCenter }
            });
            return;
        }

        // Group by category
        var groups = filtered.GroupBy(r => r.Category).OrderBy(g => g.Key);

        foreach (var group in groups)
        {
            // Category header foldout
            var foldout = new Foldout { text = $"{CategoryDisplayName(group.Key)} ({group.Count()})", value = true };
            foldout.style.paddingLeft = 4;
            foldout.style.paddingTop = 2;
            foldout.style.paddingBottom = 2;

            foreach (var row in group)
            {
                foldout.Add(CreateTagRow(row));
            }

            _scrollView.Add(foldout);
        }
    }

    private VisualElement CreateTagRow(TagRowData row)
    {
        var rowEl = new VisualElement();
        rowEl.style.flexDirection = FlexDirection.Row;
        rowEl.style.paddingLeft = 20;
        rowEl.style.paddingTop = 2;
        rowEl.style.paddingBottom = 2;
        rowEl.style.alignItems = Align.Center;

        // TagId
        var tagIdLabel = new Label(row.TagId);
        tagIdLabel.style.width = 140;
        tagIdLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        rowEl.Add(tagIdLabel);

        // DisplayName
        var displayLabel = new Label(row.DisplayName ?? "");
        displayLabel.style.width = 80;
        displayLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
        rowEl.Add(displayLabel);

        // Ref count
        var refLabel = new Label(row.RefCount > 0 ? row.RefCount.ToString() : "[UNUSED]");
        refLabel.style.width = 80;
        if (row.RefCount == 0)
        {
            refLabel.style.color = new Color(0.9f, 0.6f, 0.2f); // amber warning
        }
        rowEl.Add(refLabel);

        // Context menu
        rowEl.AddManipulator(new ContextualMenuManipulator(evt =>
        {
            evt.menu.AppendAction("Rename...", _ => StartRename(row));
            evt.menu.AppendSeparator();
            if (row.RefCount > 0)
            {
                evt.menu.AppendAction(
                    $"Delete (referenced by {row.RefCount} fragments)...",
                    _ => SafeDelete(row));
            }
            else
            {
                evt.menu.AppendAction("Delete (unused)", _ => DeleteUnusedTag(row));
            }
        }));

        return rowEl;
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null) return;

        if (_catalog == null)
        {
            _statusLabel.text = "Status: No catalog found.";
            return;
        }

        int total = _catalog.Tags?.Count ?? 0;
        int unused = 0;
        if (_catalog.Tags != null)
        {
            foreach (var tag in _catalog.Tags)
            {
                if (string.IsNullOrEmpty(tag.TagId)) continue;
                _refCounts.TryGetValue(tag.TagId, out int c);
                if (c == 0) unused++;
            }
        }

        _statusLabel.text = $"Total: {total} tags | Orphaned: {unused} | Fragments scanned: {CountFragments()}";
    }

    private int CountFragments()
    {
        return AssetDatabase.FindAssets(FRAGMENT_SEARCH_FILTER).Length;
    }

    // =========================================================================
    // Rename (AC-1)
    // =========================================================================

    private void StartRename(TagRowData row)
    {
        var dialog = EditorWindow.CreateInstance<RenameTagDialog>();
        dialog.OldTagId = row.TagId;
        dialog.OnConfirmed = (newTagId) => ExecuteRename(row, newTagId);
        dialog.ShowModal();
    }

    private void ExecuteRename(TagRowData row, string newTagId)
    {
        if (string.IsNullOrEmpty(newTagId) || newTagId == row.TagId) return;

        // 1. Update the tag in catalog
        var tagData = _catalog.Tags[row.CatalogIndex];
        tagData.TagId = newTagId;
        _catalog.Tags[row.CatalogIndex] = tagData;
        EditorUtility.SetDirty(_catalog);

        // 2. Scan all MemoryFragment assets and replace TagId
        int updatedFragments = 0;
        var fragGuids = AssetDatabase.FindAssets(FRAGMENT_SEARCH_FILTER);
        foreach (var guid in fragGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var frag = AssetDatabase.LoadAssetAtPath<MemoryFragment>(path);
            if (frag?.EmotionalTags == null) continue;

            bool fragmentChanged = false;
            for (int i = 0; i < frag.EmotionalTags.Count; i++)
            {
                if (frag.EmotionalTags[i].TagId == row.TagId)
                {
                    frag.EmotionalTags[i] = new EmotionalTag(
                        newTagId, frag.EmotionalTags[i].BaseWeight, frag.EmotionalTags[i].IsPrimary);
                    fragmentChanged = true;
                }
            }

            if (fragmentChanged)
            {
                EditorUtility.SetDirty(frag);
                updatedFragments++;
            }
        }

        // 3. Save all changes
        AssetDatabase.SaveAssets();
        RefreshData();

        Debug.Log($"[EmotionalTagBrowser] Renamed '{row.TagId}' → '{newTagId}'. " +
                  $"Updated {updatedFragments} fragment(s).");
    }

    // =========================================================================
    // Delete (AC-4)
    // =========================================================================

    private void SafeDelete(TagRowData row)
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Delete Emotional Tag",
            $"Tag '{row.TagId}' is referenced by {row.RefCount} fragment(s).\n\n" +
            "Deleting will:\n" +
            $"• Remove '{row.TagId}' from all {row.RefCount} fragment(s)\n" +
            "• Delete the tag entry from the catalog\n\n" +
            "This action cannot be undone. Continue?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        ExecuteDelete(row);
    }

    private void DeleteUnusedTag(TagRowData row)
    {
        bool confirmed = EditorUtility.DisplayDialog(
            "Delete Unused Tag",
            $"Tag '{row.TagId}' has no references.\n\nDelete it from the catalog?",
            "Delete",
            "Cancel");

        if (!confirmed) return;

        ExecuteDelete(row);
    }

    private void ExecuteDelete(TagRowData row)
    {
        // 1. Remove from all fragments
        int affectedFragments = 0;
        var fragGuids = AssetDatabase.FindAssets(FRAGMENT_SEARCH_FILTER);
        foreach (var guid in fragGuids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var frag = AssetDatabase.LoadAssetAtPath<MemoryFragment>(path);
            if (frag?.EmotionalTags == null) continue;

            int removed = frag.EmotionalTags.RemoveAll(t => t.TagId == row.TagId);
            if (removed > 0)
            {
                EditorUtility.SetDirty(frag);
                affectedFragments++;
            }
        }

        // 2. Remove from catalog
        _catalog.Tags.RemoveAt(row.CatalogIndex);
        EditorUtility.SetDirty(_catalog);

        // 3. Save
        AssetDatabase.SaveAssets();
        RefreshData();

        Debug.Log($"[EmotionalTagBrowser] Deleted tag '{row.TagId}'. " +
                  $"Removed from {affectedFragments} fragment(s).");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static string CategoryDisplayName(EmotionCategory category)
    {
        return category switch
        {
            EmotionCategory.Joy => "Joy (喜悦)",
            EmotionCategory.Sadness => "Sadness (悲伤)",
            EmotionCategory.Love => "Love (爱)",
            EmotionCategory.Fear => "Fear (恐惧)",
            EmotionCategory.Anger => "Anger (愤怒)",
            EmotionCategory.Wonder => "Wonder (惊奇)",
            EmotionCategory.Melancholy => "Melancholy (忧郁)",
            EmotionCategory.Peace => "Peace (平静)",
            _ => category.ToString()
        };
    }
}

// =========================================================================
// Rename Dialog (modal EditorWindow)
// =========================================================================

/// <summary>
/// Modal dialog for renaming an emotional tag.
/// Validates: new TagId must not be empty and must not conflict with existing tags.
/// </summary>
internal class RenameTagDialog : EditorWindow
{
    public string OldTagId;
    public System.Action<string> OnConfirmed;

    private TextField _inputField;
    private Label _errorLabel;

    private void CreateGUI()
    {
        titleContent = new GUIContent("Rename Tag");
        minSize = new Vector2(320, 120);
        maxSize = new Vector2(400, 150);

        var root = rootVisualElement;
        root.style.paddingTop = 10;
        root.style.paddingBottom = 10;
        root.style.paddingLeft = 12;
        root.style.paddingRight = 12;

        root.Add(new Label($"Rename '{OldTagId}' to:") { style = { paddingBottom = 6 } });

        _inputField = new TextField();
        _inputField.value = OldTagId;
        _inputField.RegisterValueChangedCallback(_ => ValidateInput());
        _inputField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return && GetError() == null)
                Confirm();
            else if (evt.keyCode == KeyCode.Escape)
                Close();
        });
        root.Add(_inputField);

        _errorLabel = new Label();
        _errorLabel.style.color = new Color(0.9f, 0.3f, 0.3f);
        _errorLabel.style.paddingTop = 4;
        root.Add(_errorLabel);

        var buttonRow = new VisualElement();
        buttonRow.style.flexDirection = FlexDirection.Row;
        buttonRow.style.paddingTop = 8;
        buttonRow.style.justifyContent = Justify.FlexEnd;

        var cancelBtn = new Button(() => Close()) { text = "Cancel" };
        cancelBtn.style.marginRight = 6;
        buttonRow.Add(cancelBtn);

        var confirmBtn = new Button(() => Confirm()) { text = "Rename" };
        buttonRow.Add(confirmBtn);
        root.Add(buttonRow);

        _inputField.Focus();
        _inputField.SelectAll();
    }

    private void ValidateInput()
    {
        var error = GetError();
        _errorLabel.text = error ?? "";
    }

    private string GetError()
    {
        var value = _inputField?.value;
        if (string.IsNullOrWhiteSpace(value))
            return "Tag ID cannot be empty.";
        if (value == OldTagId)
            return "New Tag ID must differ from the current one.";
        if (value.Any(c => char.IsWhiteSpace(c)))
            return "Tag ID must not contain whitespace.";
        if (!value.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-'))
            return "Tag ID must contain only letters, digits, underscores, and hyphens.";
        return null;
    }

    private void Confirm()
    {
        if (GetError() != null) return;
        OnConfirmed?.Invoke(_inputField.value.Trim());
        Close();
    }
}
#endif
