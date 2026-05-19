using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// In-game HUD MonoBehaviour implementing <see cref="IHUD"/>.
///
/// Renders a UI Toolkit VisualElement tree on the Game scene's UIDocument:
///   - #fragment-text-overlay — fragment text with 4s auto-fade
///   - #choice-panel — choice panel positioned near anchor object
///   - #association-paths — top-5 association candidates as ink trails
///   - #chapter-progress — horizontal dot progress bar
///   - #interaction-hint — hover interaction hint
///
/// Visibility is governed by the rules table (ADR-0006):
///   GameplayInputActive && panel stack empty && !transitioning => fully visible.
///   Choice panel open => only #choice-panel visible.
///   Transitioning => fully hidden (saves _preTransitionVisibility).
///   Panel stack non-empty => fully hidden.
///
/// MVVM data binding is throttled to 10 Hz via <see cref="HudBindingThrottle"/>.
/// All event subscriptions follow ADR-0001 (method group, no lambdas).
/// </summary>
public class InGameHUD : MonoBehaviour, IHUD
{
    // =========================================================================
    // Serialized Fields
    // =========================================================================

    [SerializeField] private UIDocument _uiDocument;
    [SerializeField] private VisualTreeAsset _choiceOptionTemplate;

    // =========================================================================
    // Cached VisualElement References
    // =========================================================================

    private VisualElement _rootElement;
    private VisualElement _fragmentTextOverlay;
    private Label _fragmentTextLabel;
    private VisualElement _choicePanel;
    private VisualElement _choiceOptions;
    private Label _choicePrompt;
    private VisualElement _associationPaths;
    private VisualElement _chapterProgress;
    private Label _chapterNameLabel;
    private VisualElement _fragmentCount;
    private VisualElement _interactionHint;
    private Label _hintText;

    // =========================================================================
    // Internal State
    // =========================================================================

    private ChoiceGroup _currentGroup;
    private float _textOverlayTimer;
    private bool _fadeOutRequested;
    private float _hoverTimer;
    private string _hoverObjectId;
    private bool _isTransitioning;
    private bool _preTransitionVisibility;
    private bool _choicePanelVisible;
    private bool _gameplayInputActive;
    private TaskCompletionSource<string> _choiceTcs;
    private HudBindingThrottle _throttle;
    private Keyboard _keyboard;

    // =========================================================================
    // Public Data Sources (for MVVM binding)
    // =========================================================================

    /// <summary>Data source for association paths MVVM binding.</summary>
    public readonly AssociationPathsDataSource AssociationPathsData = new AssociationPathsDataSource();

    /// <summary>Data source for chapter progress MVVM binding.</summary>
    public readonly ChapterProgressDataSource ChapterProgressData = new ChapterProgressDataSource();

    // =========================================================================
    // ChapterManager Reference (set by BootBootstrap)
    // =========================================================================

    /// <summary>
    /// Reference to the ChapterManager instance. Set by BootBootstrap during startup.
    /// Used by association path click handlers to trigger fragment transitions.
    /// </summary>
    public static ChapterManager ChapterManagerRef { get; set; }

    // =========================================================================
    // Unity Lifecycle
    // =========================================================================

    void OnEnable()
    {
        _keyboard = Keyboard.current;

        if (_uiDocument == null)
        {
            Debug.LogError("[InGameHUD] UIDocument is null — HUD will not render.");
            return;
        }

        var root = _uiDocument.rootVisualElement;
        _rootElement = root;

        // Cache all Q() references
        _fragmentTextOverlay = root.Q("fragment-text-overlay");
        _fragmentTextLabel = root.Q<Label>("fragment-text");
        _choicePanel = root.Q("choice-panel");
        _choiceOptions = root.Q("choice-options");
        _choicePrompt = root.Q<Label>("choice-prompt");
        _associationPaths = root.Q("association-paths");
        _chapterProgress = root.Q("chapter-progress");
        _chapterNameLabel = root.Q<Label>("chapter-name");
        _fragmentCount = root.Q("fragment-count");
        _interactionHint = root.Q("interaction-hint");
        _hintText = root.Q<Label>("hint-text");

        // Initial visibility
        if (_choicePanel != null)
            _choicePanel.visible = false;
        if (_interactionHint != null)
            _interactionHint.visible = false;
        if (_fragmentTextOverlay != null)
            _fragmentTextOverlay.visible = false;

        // Initialize throttle
        _throttle = new HudBindingThrottle(root);
        _throttle.OnRefresh += RefreshAllBindings;

        // Wire data source propertyChanged → throttle MarkDirty
        AssociationPathsData.propertyChanged += (_, _) => _throttle.MarkDirty();
        ChapterProgressData.propertyChanged += (_, _) => _throttle.MarkDirty();

        // Subscribe to static events (ADR-0001 pattern)
        GameSceneManager.OnFragmentTransitionStarted += HandleTransitionStarted;
        GameSceneManager.OnFragmentTransitioned += HandleTransitionEnded;
        ChapterManager.OnFragmentChanged += HandleFragmentChanged;
        UIPanelStackCore.OnStackChanged += HandleStackChanged;
        InputManager.OnGameplayInputActiveChanged += HandleGameplayInputActiveChanged;
        InteractionManager.OnHoverEnter += HandleHoverEnter;
        InteractionManager.OnHoverExit += HandleHoverExit;

        // Dismiss text overlay on root click (for early dismiss)
        root.RegisterCallback<ClickEvent>(OnRootClick);

        // Initial visibility evaluation
        EvaluateVisibility();
    }

    void OnDisable()
    {
        _throttle?.Stop();

        // Unsubscribe all static events (ADR-0001 Rule 7)
        GameSceneManager.OnFragmentTransitionStarted -= HandleTransitionStarted;
        GameSceneManager.OnFragmentTransitioned -= HandleTransitionEnded;
        ChapterManager.OnFragmentChanged -= HandleFragmentChanged;
        UIPanelStackCore.OnStackChanged -= HandleStackChanged;
        InputManager.OnGameplayInputActiveChanged -= HandleGameplayInputActiveChanged;
        InteractionManager.OnHoverEnter -= HandleHoverEnter;
        InteractionManager.OnHoverExit -= HandleHoverExit;

        if (_rootElement != null)
            _rootElement.UnregisterCallback<ClickEvent>(OnRootClick);

        AssociationPathsData.propertyChanged = null;
        ChapterProgressData.propertyChanged = null;
    }

    void Update()
    {
        HandleTextOverlayTimer();
        HandleHoverTimer();
    }

    void OnDestroy()
    {
        // ADR-0001 Rule 7: Null static events if we're the last instance
        ChapterManagerRef = null;
    }

    // =========================================================================
    // IHUD Implementation — ShowChoicePanel
    // =========================================================================

    /// <inheritdoc/>
    public Task<string> ShowChoicePanel(ChoiceGroup choiceGroup)
    {
        return ShowChoicePanel(choiceGroup, new Vector2(Screen.width / 2f, Screen.height / 2f));
    }

    /// <inheritdoc/>
    public Task<string> ShowChoicePanel(ChoiceGroup choiceGroup, Vector2 screenPosition)
    {
        if (_choicePanel == null || _choiceOptions == null)
        {
            Debug.LogWarning("[InGameHUD] ShowChoicePanel: choice-panel or choice-options VisualElement not found.");
            _choiceTcs = new TaskCompletionSource<string>();
            _choiceTcs.TrySetResult(null);
            return _choiceTcs.Task;
        }

        _currentGroup = choiceGroup;

        // Filter available choices (condition filtering handled by InteractionManager)
        var availableChoices = choiceGroup.Choices ?? Array.Empty<Choice>();
        if (availableChoices.Length == 0)
        {
            Debug.LogWarning($"[InGameHUD] ShowChoicePanel: ChoiceGroup '{choiceGroup.GroupId}' has 0 choices.");
            _choiceTcs = new TaskCompletionSource<string>();
            _choiceTcs.TrySetResult(null);
            return _choiceTcs.Task;
        }

        // Switch to UI input mode
        InputManager.SwitchToUIMode();
        _choicePanelVisible = true;

        // Position panel
        float panelWidth = 300f;
        float panelHeight = 200f;
        Vector2 panelPos = CalculatePanelPosition(screenPosition, panelWidth, panelHeight);
        _choicePanel.style.left = panelPos.x;
        _choicePanel.style.top = panelPos.y;
        _choicePanel.visible = true;

        // Set prompt text
        if (_choicePrompt != null)
            _choicePrompt.text = choiceGroup.GroupLabel ?? string.Empty;

        // Render choices
        RenderChoiceOptions(availableChoices);

        // Register Escape handler
        if (_keyboard != null)
        {
            // Escape handling via Update polling of Keyboard.current
        }

        // Update visibility (hide paths + progress while choice panel open)
        EvaluateVisibility();

        // Create TCS for async result
        _choiceTcs = new TaskCompletionSource<string>();
        return _choiceTcs.Task;
    }

    /// <inheritdoc/>
    public async Task HideChoicePanel(float fadeDuration)
    {
        if (_choicePanel != null)
        {
            if (fadeDuration > 0f)
            {
                _choicePanel.AddToClassList("fade-out");
                await Task.Delay((int)(fadeDuration * 1000));
                if (this == null) return;
            }

            _choicePanel.visible = false;
            _choicePanel.RemoveFromClassList("fade-out");
            _choiceOptions.Clear();
        }

        _choicePanelVisible = false;
        _currentGroup = null;

        // Restore gameplay input
        InputManager.SwitchToGameplayMode();

        // Restore game elements visibility
        EvaluateVisibility();
    }

    // =========================================================================
    // IHUD Implementation — ShowFragmentText
    // =========================================================================

    /// <inheritdoc/>
    public void ShowFragmentText(TextContent content, Vector2 screenPosition)
    {
        if (_fragmentTextOverlay == null || _fragmentTextLabel == null)
        {
            Debug.LogWarning("[InGameHUD] ShowFragmentText: fragment-text-overlay not found.");
            return;
        }

        _fragmentTextLabel.text = content?.Text ?? string.Empty;
        _fragmentTextLabel.pickingMode = PickingMode.Ignore;

        _fragmentTextOverlay.style.left = screenPosition.x;
        _fragmentTextOverlay.style.top = screenPosition.y;
        _fragmentTextOverlay.style.opacity = 1f;
        _fragmentTextOverlay.visible = true;

        // Set timer: default 4s, use content.Duration if set
        _textOverlayTimer = content?.Duration > 0f ? content.Duration : 4.0f;
        _fadeOutRequested = false;
    }

    // =========================================================================
    // Association Paths Visualization
    // =========================================================================

    /// <summary>
    /// Renders the top-5 association candidates as ink-trail path elements.
    /// Sorted by Score descending. Each .path-candidate gets visual properties
    /// mapped from Strength grading (opacity + target indicator size).
    /// Click handler triggers ChapterManager.TransitionToFragment.
    /// No .scent-label text (MVP scope).
    /// </summary>
    public void ShowAssociationPaths(AssociationCandidate[] candidates)
    {
        if (_associationPaths == null)
            return;

        _associationPaths.Clear();

        if (candidates == null || candidates.Length == 0)
            return;

        // Sort by Score DESC, take top-5
        var topCandidates = candidates
            .OrderByDescending(c => c.CompositeScore)
            .Take(5)
            .ToArray();

        // Update MVVM data source
        var dataList = new List<PathCandidateData>(topCandidates.Length);
        foreach (var candidate in topCandidates)
        {
            dataList.Add(PathCandidateData.FromCandidate(candidate));
        }
        AssociationPathsData.Candidates = dataList;

        int totalCandidates = topCandidates.Length;

        for (int i = 0; i < totalCandidates; i++)
        {
            var candidate = topCandidates[i];
            int index = i; // capture for closure

            var pathEl = new VisualElement();
            pathEl.AddToClassList("path-candidate");

            // Ink trail
            var inkTrail = new VisualElement();
            inkTrail.AddToClassList("ink-trail");
            ApplyStrengthVisuals(inkTrail, candidate.Grade, isIndicator: false);
            pathEl.Add(inkTrail);

            // Target indicator (vermilion circle at the end of the trail)
            var targetIndicator = new VisualElement();
            targetIndicator.AddToClassList("target-indicator");
            ApplyStrengthVisuals(targetIndicator, candidate.Grade, isIndicator: true);
            pathEl.Add(targetIndicator);

            // No .scent-label text (MVP scope)

            // Click handler: trigger fragment transition
            string targetFragmentId = candidate.FragmentId;
            pathEl.RegisterCallback<ClickEvent>(_ =>
            {
                if (ChapterManagerRef != null)
                {
                    _ = ChapterManagerRef.TransitionToFragment(targetFragmentId);
                }
                else
                {
                    Debug.LogWarning(
                        $"[InGameHUD] Association path clicked but ChapterManagerRef is null. " +
                        $"Target: {targetFragmentId}");
                }
            });

            // Keyboard navigation: set focusable and tab index
            pathEl.focusable = true;
            pathEl.tabIndex = i + 1;

            // Register Enter key handler for keyboard navigation
            pathEl.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (ChapterManagerRef != null)
                    {
                        _ = ChapterManagerRef.TransitionToFragment(targetFragmentId);
                    }
                    evt.StopPropagation();
                }
            });

            _associationPaths.Add(pathEl);
        }
    }

    // =========================================================================
    // Chapter Progress
    // =========================================================================

    /// <summary>
    /// Renders the chapter progress as horizontal dots in #chapter-progress.
    /// Solid vermilion (#C04040) for visited fragments, hollow for unvisited,
    /// "dot-current" class on the last visited dot for L2 pulse animation.
    /// </summary>
    public void UpdateChapterProgress(string chapterKey, int visitedCount, int totalFragments)
    {
        if (_chapterProgress == null || _fragmentCount == null || _chapterNameLabel == null)
            return;

        // Update data source for MVVM
        ChapterProgressData.ChapterName = chapterKey ?? string.Empty;
        ChapterProgressData.VisitedCount = visitedCount;
        ChapterProgressData.TotalCount = totalFragments;

        // Set chapter name
        _chapterNameLabel.text = chapterKey ?? string.Empty;

        // Render dots
        _fragmentCount.Clear();

        for (int i = 0; i < totalFragments; i++)
        {
            var dot = new VisualElement();
            dot.AddToClassList("chapter-dot");

            if (i < visitedCount)
            {
                dot.AddToClassList("dot-visited"); // Solid vermilion #C04040
            }
            else
            {
                dot.AddToClassList("dot-unvisited"); // Hollow ink circle
            }

            _fragmentCount.Add(dot);
        }

        // Mark current fragment dot with pulse class
        if (visitedCount > 0 && visitedCount <= totalFragments)
        {
            var currentDot = _fragmentCount.ElementAt(visitedCount - 1);
            if (currentDot != null)
            {
                // Remove dot-current from all dots first
                for (int i = 0; i < _fragmentCount.childCount; i++)
                {
                    _fragmentCount.ElementAt(i)?.RemoveFromClassList("dot-current");
                }
                currentDot.AddToClassList("dot-current"); // L2 pulse
            }
        }
    }

    // =========================================================================
    // Interaction Hint
    // =========================================================================

    /// <summary>
    /// Shows the interaction hint after a 0.5s delay above the cursor position.
    /// Maps interaction type to appropriate hint text.
    /// </summary>
    public void ShowInteractionHint(string objectName, InteractionType interactionType, Vector2 cursorScreenPos)
    {
        if (_interactionHint == null || _hintText == null)
            return;

        string hintText = interactionType switch
        {
            InteractionType.Touch => objectName,
            InteractionType.Drag => $"拖拽 {objectName}",
            InteractionType.Hover => $"{objectName}...",
            InteractionType.Examine => $"细看 {objectName}",
            _ => objectName
        };

        _hintText.text = hintText;
        _interactionHint.style.left = cursorScreenPos.x;
        _interactionHint.style.top = cursorScreenPos.y - 20f; // 20px above cursor
        _interactionHint.visible = true;
    }

    /// <summary>
    /// Hides the interaction hint immediately.
    /// </summary>
    public void HideInteractionHint()
    {
        if (_interactionHint != null)
            _interactionHint.visible = false;
    }

    // =========================================================================
    // Visibility Rules
    // =========================================================================

    /// <summary>
    /// Evaluates HUD visibility according to the rules table (ADR-0006):
    ///   GameplayInputActive && panel stack empty && !transitioning => fully visible
    ///   Choice panel open => only #choice-panel visible
    ///   Transitioning => fully hidden (saves _preTransitionVisibility)
    ///   Panel stack non-empty => fully hidden
    /// </summary>
    private void EvaluateVisibility()
    {
        int stackDepth = UIPanelStackCore.OnStackChanged != null ? 0 : 0;
        // We can't query StackDepth from static events — we track it via HandleStackChanged

        bool shouldShow = _gameplayInputActive
            && !_isTransitioning
            && _stackDepth == 0;

        if (_rootElement != null)
            _rootElement.visible = shouldShow;

        if (shouldShow && _choicePanelVisible)
        {
            // Choice panel mode: only choice panel visible
            if (_associationPaths != null)
                _associationPaths.visible = false;
            if (_chapterProgress != null)
                _chapterProgress.visible = false;
            if (_choicePanel != null)
                _choicePanel.visible = true;
        }
        else if (shouldShow)
        {
            // Normal gameplay: game elements visible, choice panel hidden
            if (_associationPaths != null)
                _associationPaths.visible = true;
            if (_chapterProgress != null)
                _chapterProgress.visible = true;
            if (_choicePanel != null)
                _choicePanel.visible = false;
        }
    }

    private int _stackDepth;

    private void HandleStackChanged(int depth)
    {
        _stackDepth = depth;
        EvaluateVisibility();
    }

    // =========================================================================
    // Event Handlers (ADR-0001 method group pattern)
    // =========================================================================

    /// <summary>
    /// Handles transition start from GameSceneManager.
    /// Records pre-transition visibility and hides HUD completely.
    /// </summary>
    private void HandleTransitionStarted(string chapterKey, string fragmentId)
    {
        _isTransitioning = true;

        // Save pre-transition visibility
        if (_rootElement != null)
            _preTransitionVisibility = _rootElement.visible;

        // Dismiss text overlay during transition
        DismissTextOverlay();

        // Pause hover timer
        HideInteractionHint();
        _hoverTimer = 0f;

        EvaluateVisibility();
    }

    /// <summary>
    /// Handles transition end from GameSceneManager.
    /// Restores HUD to pre-transition visibility (except text overlay).
    /// </summary>
    private void HandleTransitionEnded(string chapterKey, string fragmentId)
    {
        _isTransitioning = false;
        EvaluateVisibility();
    }

    /// <summary>
    /// Handles fragment change from ChapterManager.
    /// Updates chapter progress and dismisses text overlay.
    /// </summary>
    private void HandleFragmentChanged(string previousFragmentId, string newFragmentId)
    {
        // Dismiss text overlay when fragment changes
        DismissTextOverlay();

        // Chapter progress is updated externally by whoever has the data
        // (ChapterManager or calling code calls UpdateChapterProgress directly)
    }

    /// <summary>
    /// Handles gameplay input active state changes from InputManager.
    /// </summary>
    private void HandleGameplayInputActiveChanged(bool active)
    {
        _gameplayInputActive = active;
        EvaluateVisibility();
    }

    /// <summary>
    /// Handles hover enter from InteractionManager.
    /// Starts 0.5s delay timer before showing interaction hint.
    /// </summary>
    private void HandleHoverEnter(string objectId)
    {
        _hoverObjectId = objectId;
        _hoverTimer = 0.5f; // 0.5s delay before showing hint
    }

    /// <summary>
    /// Handles hover exit from InteractionManager.
    /// Hides interaction hint immediately.
    /// </summary>
    private void HandleHoverExit(string objectId)
    {
        if (_hoverObjectId == objectId || string.IsNullOrEmpty(objectId))
        {
            _hoverObjectId = null;
            _hoverTimer = 0f;
            HideInteractionHint();
        }
    }

    // =========================================================================
    // Root Click Handler (early dismiss for text overlay)
    // =========================================================================

    private void OnRootClick(ClickEvent evt)
    {
        // Dismiss text overlay on any click
        if (_fragmentTextOverlay != null && _fragmentTextOverlay.visible)
        {
            DismissTextOverlay();
        }
    }

    // =========================================================================
    // Update Timers
    // =========================================================================

    private void HandleTextOverlayTimer()
    {
        if (_textOverlayTimer <= 0f)
            return;

        _textOverlayTimer -= Time.deltaTime;

        // Start fade-out 0.5s before timer ends
        if (_textOverlayTimer <= 0.5f && !_fadeOutRequested)
        {
            _fadeOutRequested = true;
            if (_fragmentTextOverlay != null)
                _fragmentTextOverlay.AddToClassList("fade-out");
        }

        // Hide when timer expires
        if (_textOverlayTimer <= 0f)
        {
            DismissTextOverlay();
        }
    }

    private void HandleHoverTimer()
    {
        if (_hoverTimer <= 0f || string.IsNullOrEmpty(_hoverObjectId))
            return;

        _hoverTimer -= Time.deltaTime;

        if (_hoverTimer <= 0f)
        {
            // Show interaction hint
            Vector2 mousePos = Vector2.zero;
            if (_keyboard != null)
            {
                mousePos = Mouse.current?.position.ReadValue() ?? Vector2.zero;
            }

            // Determine interaction type from the object
            // We receive only the objectId from the event; for MVP we show the object name
            ShowInteractionHint(_hoverObjectId, InteractionType.Touch, mousePos);
            _hoverObjectId = null;
            _hoverTimer = 0f;
        }
    }

    // =========================================================================
    // Choice Rendering
    // =========================================================================

    /// <summary>
    /// Renders choice options from the given Choice array using the
    /// _choiceOptionTemplate VisualTreeAsset. Each option gets a click handler
    /// that resolves the choice TCS and triggers HideChoicePanel.
    /// Escape key listener is registered for cancellation.
    /// </summary>
    private void RenderChoiceOptions(Choice[] choices)
    {
        _choiceOptions.Clear();

        for (int i = 0; i < choices.Length; i++)
        {
            var choice = choices[i];
            var optionEl = _choiceOptionTemplate?.CloneTree() ?? CreateDefaultOption();

            // Set text on .choice-text label
            var textLabel = optionEl.Q<Label>("choice-text") ?? optionEl.Q<Label>(className: "choice-text");
            if (textLabel != null)
            {
                textLabel.text = choice.Text ?? choice.ChoiceId;
            }

            // Click handler
            string selectedId = choice.ChoiceId;
            optionEl.RegisterCallback<ClickEvent>(_ =>
            {
                // Apply changes via ChangeTracker
                if (ChangeTracker.Instance != null && choice.ContentChanges != null && choice.ContentChanges.Count > 0)
                {
                    ChangeTracker.Instance.ApplyChanges(
                        GameSceneManager.Instance?.CurrentFragmentId ?? string.Empty,
                        selectedId,
                        choice.ContentChanges.ToArray());
                }

                // Fire choice selected event
                InteractionManager.OnChoiceSelected?.Invoke(selectedId);

                // Resolve TCS
                _choiceTcs?.TrySetResult(selectedId);

                // Hide panel
                _ = HideChoicePanel(0.3f);
            });

            // Keyboard focus: first option gets auto-focus
            optionEl.focusable = true;
            optionEl.tabIndex = i + 1;
            if (i == 0)
            {
                optionEl.schedule.Execute(() => optionEl.Focus());
            }

            // Enter key handler for keyboard navigation
            optionEl.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (ChangeTracker.Instance != null && choice.ContentChanges != null && choice.ContentChanges.Count > 0)
                    {
                        ChangeTracker.Instance.ApplyChanges(
                            GameSceneManager.Instance?.CurrentFragmentId ?? string.Empty,
                            selectedId,
                            choice.ContentChanges.ToArray());
                    }

                    InteractionManager.OnChoiceSelected?.Invoke(selectedId);
                    _choiceTcs?.TrySetResult(selectedId);
                    _ = HideChoicePanel(0.3f);
                    evt.StopPropagation();
                }
            });

            _choiceOptions.Add(optionEl);
        }
    }

    /// <summary>
    /// Fallback option element when _choiceOptionTemplate is not assigned.
    /// Creates a basic VisualElement with a Label.
    /// </summary>
    private VisualElement CreateDefaultOption()
    {
        var el = new VisualElement();
        el.AddToClassList("choice-option");
        var label = new Label();
        label.name = "choice-text";
        label.AddToClassList("choice-text");
        el.Add(label);
        return el;
    }

    // =========================================================================
    // Positioning
    // =========================================================================

    /// <summary>
    /// Calculates the screen-space position for a panel relative to an anchor point.
    /// Priority: right side (+40px) > flip left if overflow > fallback below.
    /// </summary>
    /// <param name="anchorScreenPos">Anchor screen position.</param>
    /// <param name="panelWidth">Width of the panel to position.</param>
    /// <param name="panelHeight">Height of the panel to position.</param>
    /// <returns>Screen-space position for the panel's top-left corner.</returns>
    public static Vector2 CalculatePanelPosition(Vector2 anchorScreenPos, float panelWidth, float panelHeight)
    {
        // Priority 1: right side (+40px horizontal offset)
        Vector2 rightPos = anchorScreenPos + new Vector2(40f, 0f);
        if (rightPos.x + panelWidth <= Screen.width)
            return rightPos;

        // Priority 2: flip to left
        Vector2 leftPos = anchorScreenPos - new Vector2(panelWidth + 40f, 0f);
        if (leftPos.x >= 0f)
            return leftPos;

        // Priority 3: fallback below (+20px vertical offset)
        Vector2 belowPos = anchorScreenPos + new Vector2(0f, 20f);
        if (belowPos.y + panelHeight <= Screen.height)
            return belowPos;

        // Ultimate fallback: center of screen
        return new Vector2(Screen.width / 2f - panelWidth / 2f, Screen.height / 2f - panelHeight / 2f);
    }

    // =========================================================================
    // Visual Helpers
    // =========================================================================

    /// <summary>
    /// Applies Strength-based visual properties to a VisualElement.
    /// Maps Strength to opacity and size for ink trails and target indicators.
    /// </summary>
    private static void ApplyStrengthVisuals(VisualElement element, Strength grade, bool isIndicator)
    {
        switch (grade)
        {
            case Strength.Strong:
                element.style.opacity = 0.9f;
                if (isIndicator)
                {
                    element.style.width = 16f;
                    element.style.height = 16f;
                }
                break;

            case Strength.Medium:
                element.style.opacity = 0.6f;
                if (isIndicator)
                {
                    element.style.width = 12f;
                    element.style.height = 12f;
                }
                break;

            case Strength.Faint:
                element.style.opacity = 0.35f;
                if (isIndicator)
                {
                    element.style.width = 8f;
                    element.style.height = 8f;
                }
                break;

            case Strength.Trace:
                element.style.opacity = 0.15f;
                if (isIndicator)
                {
                    element.style.width = 6f;
                    element.style.height = 6f;
                }
                break;
        }
    }

    /// <summary>
    /// Dismisses the text overlay immediately (hides and resets state).
    /// </summary>
    private void DismissTextOverlay()
    {
        if (_fragmentTextOverlay != null)
        {
            _fragmentTextOverlay.visible = false;
            _fragmentTextOverlay.RemoveFromClassList("fade-out");
        }
        _textOverlayTimer = 0f;
        _fadeOutRequested = false;
    }

    // =========================================================================
    // MVVM Refresh
    // =========================================================================

    /// <summary>
    /// Called by HudBindingThrottle when the dirty flag triggers a refresh.
    /// Re-renders bound UI elements from their data sources.
    /// </summary>
    private void RefreshAllBindings()
    {
        // Association paths re-render from data source
        if (_associationPaths != null && AssociationPathsData.Candidates != null && AssociationPathsData.Candidates.Count > 0)
        {
            RenderPathsFromDataSource();
        }

        // Chapter progress re-render from data source
        if (_chapterProgress != null && ChapterProgressData.TotalCount > 0)
        {
            RenderProgressFromDataSource();
        }
    }

    /// <summary>
    /// Re-renders association paths from the MVVM data source.
    /// Called during throttled refresh.
    /// </summary>
    private void RenderPathsFromDataSource()
    {
        _associationPaths.Clear();

        var candidates = AssociationPathsData.Candidates;
        for (int i = 0; i < candidates.Count; i++)
        {
            var data = candidates[i];
            int index = i;

            var pathEl = new VisualElement();
            pathEl.AddToClassList("path-candidate");

            var inkTrail = new VisualElement();
            inkTrail.AddToClassList("ink-trail");
            ApplyStrengthVisuals(inkTrail, data.Grade, isIndicator: false);
            pathEl.Add(inkTrail);

            var targetIndicator = new VisualElement();
            targetIndicator.AddToClassList("target-indicator");
            ApplyStrengthVisuals(targetIndicator, data.Grade, isIndicator: true);
            pathEl.Add(targetIndicator);

            string targetId = data.TargetFragmentId;
            pathEl.RegisterCallback<ClickEvent>(_ =>
            {
                if (ChapterManagerRef != null)
                {
                    _ = ChapterManagerRef.TransitionToFragment(targetId);
                }
            });

            pathEl.focusable = true;
            pathEl.tabIndex = index + 1;

            _associationPaths.Add(pathEl);
        }
    }

    /// <summary>
    /// Re-renders chapter progress from the MVVM data source.
    /// Called during throttled refresh.
    /// </summary>
    private void RenderProgressFromDataSource()
    {
        if (_fragmentCount == null)
            return;

        _fragmentCount.Clear();

        var data = ChapterProgressData;
        _chapterNameLabel.text = data.ChapterName;

        for (int i = 0; i < data.TotalCount; i++)
        {
            var dot = new VisualElement();
            dot.AddToClassList("chapter-dot");

            if (i < data.VisitedCount)
                dot.AddToClassList("dot-visited");
            else
                dot.AddToClassList("dot-unvisited");

            _fragmentCount.Add(dot);
        }

        if (data.VisitedCount > 0 && data.VisitedCount <= data.TotalCount)
        {
            var currentDot = _fragmentCount.ElementAt(data.VisitedCount - 1);
            if (currentDot != null)
            {
                for (int i = 0; i < _fragmentCount.childCount; i++)
                    _fragmentCount.ElementAt(i)?.RemoveFromClassList("dot-current");
                currentDot.AddToClassList("dot-current");
            }
        }
    }
}
