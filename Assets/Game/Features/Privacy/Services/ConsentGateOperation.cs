using System;
using System.Threading;
using Analytics;
using Cysharp.Threading.Tasks;
using Game.Bootstrap.Loading;
using Game.UI;

namespace Game.Privacy.Services
{
    /// <summary>
    /// REL-5 / UK GDPR. Blocks loading on the first-run privacy notice until the player accepts.
    ///
    /// Placed in phase_technical_init after AddressablesUpdateOperation (the window prefab lives in the
    /// Local "UI" Addressables group, so the catalog has to be up first) and before
    /// RemoteConfigInitOperation, which is the first thing that touches Firebase.
    ///
    /// Implements <see cref="IInteractiveLoadingOperation"/> so the orchestrator's 60 s global budget is
    /// paused while the dialog is open — otherwise a player who reads slowly, or taps the policy link and
    /// leaves for the browser, comes back to a bogus "check your internet connection" retry screen.
    /// </summary>
    public sealed class ConsentGateOperation : LoadingOperationBase, IInteractiveLoadingOperation
    {
        private readonly IUIManager _uiManager;
        private readonly IConsentGateService _gate;

        public ConsentGateOperation(IUIManager uiManager, IConsentGateService gate)
            : base(
                id: "consent_gate",
                description: "Privacy settings",
                isCritical: true,
                weight: 0.05f,
                displayPriority: 94,
                retryPolicy: LoadingRetryPolicy.None,
                // Must stay null: any per-operation timeout would cancel the dialog out from under
                // the player. See IInteractiveLoadingOperation.
                timeout: null)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        }

        protected override async UniTask ExecuteInternalAsync(CancellationToken ct)
        {
            ReportProgress(0.1f);

            if (!_gate.IsDecisionRequired)
            {
                ReportProgress(1f);
                return;
            }

            var controller = await _uiManager.ShowAsync<ConsentWindowController>(new ConsentWindowArgs(), ct);

            // ShowAsync returns null when IUiFilter blocks the window or the prefab fails to load.
            // Failing loudly beats silently walking past a legally required gate.
            if (controller == null)
            {
                throw new InvalidOperationException(
                    "[Consent] ConsentWindow was blocked by IUiFilter or failed to load. " +
                    "Check that the prefab is registered in the 'UI' Addressables group under the address 'ConsentWindow'.");
            }

            await controller.WaitForCloseAsync(ct);

            // Belt and braces: catches a close button being added to the prefab later.
            if (_gate.IsDecisionRequired)
            {
                throw new InvalidOperationException(
                    "[Consent] ConsentWindow closed without recording a decision. Loading cannot continue.");
            }

            ReportProgress(1f);
        }
    }
}
