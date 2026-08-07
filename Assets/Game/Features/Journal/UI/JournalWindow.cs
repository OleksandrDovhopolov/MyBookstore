using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Characters.API;
using Game.Configs;
using Game.Configs.Models;
using Game.Decor;
using Game.Decor.UI;
using Game.LocationUnlock.API;
using Game.Quest.API;
using Game.Quest.UI;
using Game.UI;
using Game.UI.ContentWidget;
using SpriteService;
using UnityEngine;
using VContainer;

namespace Game.Journal.UI
{
    /// <summary>
    /// Journal window with Memories, Places, Objects, People, and Quests tabs.
    /// Opened via <c>uiManager.ShowAsync&lt;JournalWindow&gt;()</c>; needs a prefab at address "JournalWindow".
    /// </summary>
    [Window("JournalWindow", WindowType.Page, true)]
    public sealed class JournalWindow : WindowController<JournalWindowView>
    {
        private const string TodoDescription = "TODO: reward description";

        private readonly JournalCharactersViewModelBuilder _peopleBuilder = new();
        private readonly JournalMemoriesViewModelBuilder _memoriesBuilder = new();
        private readonly JournalPlacesViewModelBuilder _placesBuilder = new();
        private readonly JournalObjectsViewModelBuilder _objectsBuilder = new();

        private JournalTab _activeTab = JournalTab.People;
        private QuestViewModelBuilder _questsBuilder;
        private QuestClaimFlow _questClaimFlow;
        private ICharactersService _characters;
        private IConfigsService _configs;
        private ILocationUnlockService _locations;
        private IDecorPlacementService _decorPlacement;
        private IDecorTotalEffectsProvider _decorEffects;
        private IQuestsService _quests;
        private IQuestRewardGranter _questGranter;
        private IUiSpriteProvider _sprites;
        private Action<string> _onQuestClaim;
        private Action<QuestRewardItemModel, RectTransform> _onRewardInfo;

        // The widget is tracked by instance, not by type: UIManager.HideAsync<T> filters on
        // IWindowController.IsShown, which is only set after the show animation finishes, so a hide
        // issued while the show is still running would be dropped silently.
        private IWindowController _rewardInfoWidget;
        private int _pendingWidgetShows;
        private bool _hideRequestedWhileShowing;

        [Inject]
        public void InjectServices(
            ICharactersService characters = null,
            IConfigsService configs = null,
            ILocationUnlockService locations = null,
            IDecorPlacementService decorPlacement = null,
            IDecorTotalEffectsProvider decorEffects = null,
            IQuestsService quests = null,
            IQuestRewardGranter questGranter = null,
            IUiSpriteProvider sprites = null)
        {
            _characters = characters;
            _configs = configs;
            _locations = locations;
            _decorPlacement = decorPlacement;
            _decorEffects = decorEffects;
            _quests = quests;
            _questGranter = questGranter;
            _sprites = sprites;
        }

        protected override void OnInit()
        {
            _questsBuilder = new QuestViewModelBuilder(_configs);
            _questClaimFlow = new QuestClaimFlow(_quests, _questGranter, UIManager, RenderQuests);
            _onQuestClaim = questId => _questClaimFlow?.Claim(questId);
            _onRewardInfo = (reward, anchor) => ShowRewardInfoWidgetAsync(reward, anchor).Forget();
        }

        protected override void OnShowStart()
        {
            ApplyWindowArgs();
            View.TabSelected += OnTabSelected;
            View.QuestsScrolled += HideRewardInfoWidget;

            if (_characters != null)
            {
                _characters.CharacterDiscovered += OnCharacterDiscovered;
                _characters.MemoryUnlocked += OnMemoryUnlocked;
            }

            if (_locations != null)
            {
                _locations.Unlocked += OnLocationChanged;
                _locations.StatusChanged += OnLocationChanged;
            }

            if (_decorPlacement != null)
                _decorPlacement.PlacementChanged += OnDecorPlacementChanged;

            if (_quests != null)
            {
                _quests.QuestStarted += OnQuestChanged;
                _quests.QuestCompleted += OnQuestChanged;
                _quests.QuestAwarded += OnQuestChanged;
                _quests.QuestFailed += OnQuestChanged;
                _quests.TaskCompleted += OnQuestTaskChanged;
                _quests.TaskProgressChanged += OnQuestTaskChanged;
            }

            RenderAll();
            View.SelectTab(_activeTab);
            MarkMemoriesSeenIfActive();
        }

        protected override void OnHideStart(bool isClosed)
        {
            View.TabSelected -= OnTabSelected;
            View.QuestsScrolled -= HideRewardInfoWidget;
            HideRewardInfoWidget();

            if (_characters != null)
            {
                _characters.CharacterDiscovered -= OnCharacterDiscovered;
                _characters.MemoryUnlocked -= OnMemoryUnlocked;
            }

            if (_locations != null)
            {
                _locations.Unlocked -= OnLocationChanged;
                _locations.StatusChanged -= OnLocationChanged;
            }

            if (_decorPlacement != null)
                _decorPlacement.PlacementChanged -= OnDecorPlacementChanged;

            if (_quests != null)
            {
                _quests.QuestStarted -= OnQuestChanged;
                _quests.QuestCompleted -= OnQuestChanged;
                _quests.QuestAwarded -= OnQuestChanged;
                _quests.QuestFailed -= OnQuestChanged;
                _quests.TaskCompleted -= OnQuestTaskChanged;
                _quests.TaskProgressChanged -= OnQuestTaskChanged;
            }
        }

        protected override void OnDispose()
        {
            // The widget itself is force-closed by the UIManager parent cascade (ContentWidgetArgs
            // carries this controller as ParentWindow); here we only drop our subscription.
            UntrackRewardInfoWidget();
            _questClaimFlow?.Dispose();
            _questClaimFlow = null;
            View.Clear();
        }

        private void OnTabSelected(JournalTab tab)
        {
            _activeTab = tab;
            HideRewardInfoWidget();
            MarkMemoriesSeenIfActive();
        }

        private void OnCharacterDiscovered(ICharacter _)
        {
            RenderPeople();
            RenderMemories();
        }

        private void OnMemoryUnlocked(ICharacterMemory _)
        {
            RenderPeople();
            RenderMemories();
        }

        private void OnLocationChanged(string _) => RenderPlaces();

        private void OnDecorPlacementChanged() => RenderObjects();

        private void OnQuestChanged(IQuest _) => RenderQuests();

        private void OnQuestTaskChanged(IQuestTask _) => RenderQuests();

        private void RenderAll()
        {
            RenderPeople();
            RenderMemories();
            RenderPlaces();
            RenderObjects();
            RenderQuests();
        }

        private void RenderPeople()
        {
            if (_characters == null)
            {
                View.RenderPeople(Array.Empty<JournalCharacterItemModel>(), _sprites);
                return;
            }

            var models = _peopleBuilder.Build(_characters.GetAllCharacters(), _characters.GetJournalEntry);
            View.RenderPeople(models, _sprites);
        }

        private void RenderMemories()
        {
            if (_characters == null)
            {
                View.RenderMemories(Array.Empty<JournalMemoryItemModel>(), _sprites);
                return;
            }

            var models = _memoriesBuilder.Build(_characters.GetAllCharacters(), _characters.GetJournalEntry);
            View.RenderMemories(models, _sprites);
        }

        private void RenderPlaces()
        {
            var locations = _configs != null
                ? _configs.GetAll<LocationConfig>()
                : Array.Empty<LocationConfig>();
            var models = _placesBuilder.Build(locations, IsLocationUnlocked);
            View.RenderPlaces(models, _sprites);
        }

        private void RenderObjects()
        {
            var activeDecorIds = _decorPlacement?.GetActiveDecorIds() ?? Array.Empty<string>();
            var models = _objectsBuilder.Build(activeDecorIds, _configs, _decorEffects);
            View.RenderObjects(models, _sprites, OnDecorInfoClicked);
        }

        private void RenderQuests()
        {
            if (_questClaimFlow is { SuppressRender: true }) return;

            if (_quests == null || _questsBuilder == null)
            {
                View.RenderQuests(Array.Empty<QuestItemModel>(), _onQuestClaim, _onRewardInfo, _sprites);
                return;
            }

            var models = _questsBuilder.Build(_quests.GetAllQuests());
            View.RenderQuests(models, _onQuestClaim, _onRewardInfo, _sprites);
        }

        private async UniTaskVoid ShowRewardInfoWidgetAsync(QuestRewardItemModel reward, RectTransform anchor)
        {
            if (reward == null || string.IsNullOrEmpty(reward.Id) || anchor == null
                || UIManager == null || View == null)
                return;

            // A fresh show supersedes a hide that was requested while the previous one was still running.
            _hideRequestedWhileShowing = false;
            _pendingWidgetShows++;

            try
            {
                var description = string.IsNullOrEmpty(reward.DisplayName) ? TodoDescription : reward.DisplayName;
                var args = new ContentWidgetArgs(
                    new QuestRewardWidgetData(reward.Id, description),
                    anchor,
                    this,
                    placementMode: ContentWidgetPlacementMode.HorizontalOnly);
                TrackRewardInfoWidget(
                    await UIManager.ShowAsync<ContentWidgetController>(args, View.destroyCancellationToken));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogError($"[JournalWindow] Failed to show reward widget for '{reward.Id}': {e}");
            }
            finally
            {
                _pendingWidgetShows--;

                if (_pendingWidgetShows == 0 && _hideRequestedWhileShowing)
                {
                    _hideRequestedWhileShowing = false;
                    HideRewardInfoWidget();
                }
            }
        }

        private void TrackRewardInfoWidget(IWindowController widget)
        {
            // null means the show was rejected by the window filter — keep tracking whatever is up.
            if (widget == null || ReferenceEquals(_rewardInfoWidget, widget)) return;

            UntrackRewardInfoWidget();
            _rewardInfoWidget = widget;

            // The widget also closes on its own (auto-close, close button, another page opening),
            // so drop the reference on Closed instead of hiding an already hidden controller later.
            _rewardInfoWidget.Closed += OnRewardInfoWidgetClosed;
        }

        private void UntrackRewardInfoWidget()
        {
            if (_rewardInfoWidget == null) return;

            _rewardInfoWidget.Closed -= OnRewardInfoWidgetClosed;
            _rewardInfoWidget = null;
        }

        private void OnRewardInfoWidgetClosed(IWindowController _) => UntrackRewardInfoWidget();

        private void HideRewardInfoWidget()
        {
            if (UIManager == null) return;

            // ShowAsync is still holding the UIManager gate: the controller is not IsShown yet, so a
            // hide issued now would be dropped. Replay it once the show completes.
            if (_pendingWidgetShows > 0)
            {
                _hideRequestedWhileShowing = true;
                return;
            }

            var widget = _rewardInfoWidget;
            if (widget == null) return;

            UntrackRewardInfoWidget();
            UIManager.HideAsync(widget, forceClose: true, ct: CancellationToken.None).Forget();
        }

        private bool IsLocationUnlocked(string locationId)
            => _locations == null
               || _locations.GetStatus(locationId)?.State == LocationUnlockState.Unlocked;

        private void MarkMemoriesSeenIfActive()
        {
            if (_activeTab != JournalTab.Memories) return;
            _characters?.MarkAllMemoriesSeen();
        }

        private void ApplyWindowArgs()
        {
            if (Arguments is JournalWindowArgs { Tab: { } tab })
                _activeTab = tab;
        }

        private void OnDecorInfoClicked(string decorId)
        {
            if (string.IsNullOrEmpty(decorId)) return;

            UIManager.ShowAsync<DecorInfoPopup>(
                new DecorInfoPopupArgs(decorId),
                View != null ? View.destroyCancellationToken : default).Forget();
        }
    }
}
