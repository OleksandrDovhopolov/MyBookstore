using Analytics;
using Game.Bootstrap.Loading;
using Game.Ftue.Services;
using Game.Tutorial.Presentation;
using Game.UI;
using Infrastructure.ResourceAnimations;
using SpriteService;
using UnityEngine;
using VContainer;

namespace Game.Bootstrap
{
    // ScriptableObjectInstaller — lives under Script Installers on the GlobalLifetimeScope.prefab.
    // Registers application-wide singletons that survive scene transitions.
    // The starting gameplay scene name now lives as a SerializeField on the Bootstrap MonoBehaviour
    // (in the boot scene), so there is no _gameplaySceneName / LoadingSettings here.
    [CreateAssetMenu(fileName = "BootstrapInstaller", menuName = "Game/Installers/BootstrapInstaller")]
    public class BootstrapInstaller : ScriptableObjectInstaller
    {
        [Header("UI System")]
        [Tooltip("Prefab with the UICanvasRoot component. Instantiated once into DontDestroyOnLoad.")]
        [SerializeField] private UICanvasRoot _uiCanvasRootPrefab;

        [Header("Game Flow")]
        [Tooltip("Scene names for the hub ↔ location loop (see docs/GameFlowLoop.md).")]
        [SerializeField] private GameFlowSettings _gameFlowSettings;

        [Header("UI Sprites")]
        [Tooltip("Addressable addresses of newspaper/rewards UI sprites, preloaded once at bootstrap.")]
        [SerializeField] private UiSpriteCatalog _uiSpriteCatalog;

        [Header("Resource Animations")]
        [Tooltip("Shared settings for flying resource UI animations.")]
        [SerializeField] private ResourceAnimationSettings _resourceAnimationSettings;

        [Header("Tutorial")]
        [Tooltip("Overlay settings for the tutorial engine (blackout/pointer/text panel).")]
        [SerializeField] private TutorialOverlaySettings _tutorialOverlaySettings;

        [Tooltip("Per-sequence tutorial enable/disable settings. Null means every registered sequence is enabled.")]
        [SerializeField] private TutorialSettings _tutorialSettings;

        [Tooltip("When off, the tutorial engine still registers (overlay + 'tutorialCompleted' condition), " +
                 "but sequences never auto-start from triggers or resume on load. Explicit TryStartAsync still works.")]
        [SerializeField] private bool _tutorialAutoStart = true;

        [Header("Analytics")]
        [SerializeField] private AnalyticsConfigSO _analyticsConfig;
        [SerializeField] private AnalyticsRoutingConfigSO _analyticsRoutingConfig;
        [SerializeField] private AnalyticsMappingConfigSO _analyticsMappingConfig;

        [Header("FTUE")]
        [Tooltip("When off, the first-entry WelcomeWindow is not shown. The welcome_completed save flag is left unchanged.")]
        [SerializeField] private bool _startWelcomeWindow = true;

        [Header("First Day Entry")]
        [Tooltip("Day 1 entry path. Hub = classic flow (hub → Start Day → Location Window → Preparation). " +
                 "Location = drop straight into the location with an auto-stocked shelf (see docs/FTUE.md).")]
        [SerializeField] private FirstDayEntryMode _firstDayEntry = FirstDayEntryMode.Location;

        [Header("Privacy (REL-5)")]
        [Tooltip("Public privacy policy URL opened from the first-run consent screen. " +
                 "RELEASE BLOCKER: must be a live https URL — PrivacyLinksBuildCheck fails the build otherwise.")]
        [SerializeField] private string _privacyPolicyUrl = "";

        [Tooltip("Public terms of use URL. Leave empty when one page covers both privacy and terms.")]
        [SerializeField] private string _termsOfUseUrl = "";

#if UNITY_EDITOR
        [Header("Debug Start (Editor only)")]
        [Tooltip("Master switch. When off, the debug flags below are ignored.")]
        [SerializeField] private bool _useDebugFeatures = false;

        [Tooltip("Skip Addressables update and RemoteConfig init. Uses the bundled catalog + base configs.")]
        [SerializeField] private bool _skipFullLoading = false;
#endif

        public override void InstallBindings(IContainerBuilder builder)
        {
            ApplyDebugFlags();

            builder.RegisterMessagePipeBus();
            builder.RegisterMessagePipeSmokeTest(); // TODO: remove after first real message broker is wired up
            builder.RegisterGameLoading();
            builder.RegisterGameFlow(_gameFlowSettings);
            builder.RegisterGameAnalytics(_analyticsConfig, _analyticsRoutingConfig, _analyticsMappingConfig);
            builder.RegisterConsent(_privacyPolicyUrl, _termsOfUseUrl);
            builder.RegisterSave();
            builder.RegisterInfrastructure();
            builder.RegisterConfigs();
            builder.RegisterLocalization();
            builder.RegisterDayCycleServices();
            builder.RegisterUiSystem(_uiCanvasRootPrefab);
            builder.RegisterWorldHud();
            builder.RegisterInventory();
            builder.RegisterDecor();
            builder.RegisterResources();
            builder.RegisterRewards();
            builder.RegisterShop();
            builder.RegisterNewspaper();
            builder.RegisterUiSprites(_uiSpriteCatalog);
            builder.RegisterResourceAnimations(_resourceAnimationSettings);
            builder.RegisterProgression();
            builder.RegisterConditions();          // domain-agnostic condition engine (registry + parser)
            builder.RegisterSalesStats();          // persistent per-genre sold counters + "soldGenre" condition factory
            builder.RegisterLocationVisits();      // persistent per-location visit counts + "visitLocation"/"locationIs" factories
            builder.RegisterLocationPrefabs();     // long-lived Addressables prefab cache for LocationScene visuals/anchors
            builder.RegisterLocationUnlock();      // location unlock states/purchase over the condition engine
            builder.RegisterLocationEntry();       // per-visit entry fee calculator (location base + decor delta)
            builder.RegisterQuest();               // in-memory quest lifecycle over the condition engine (ISaveHook init)
            builder.RegisterTutorial(_tutorialOverlaySettings, _tutorialSettings, _tutorialAutoStart); // forced-step tutorial engine + overlay + "tutorialCompleted" (ISaveHook init)
            builder.RegisterCharacters();          // read-side character/memory projection over quests (ISaveHook init)
            builder.RegisterFtue(_startWelcomeWindow);
            builder.RegisterFirstDayEntry(_firstDayEntry);
            builder.RegisterBookSellSharedState(); // ISalesShelfStateService — общий для хаба и локации
            builder.RegisterPreparation();         // Preparation services (окно PreparationWindow инжектится глобально)

        }

        private void ApplyDebugFlags()
        {
#if UNITY_EDITOR
            DebugStartFlags.UseDebugFeatures = _useDebugFeatures;
            DebugStartFlags.SkipFullLoading = _useDebugFeatures && _skipFullLoading;
            if (DebugStartFlags.UseDebugFeatures)
            {
                Debug.LogWarning(
                    $"[BootstrapInstaller] Debug flags ON. SkipFullLoading={DebugStartFlags.SkipFullLoading}");
            }
#endif
        }
    }
}
