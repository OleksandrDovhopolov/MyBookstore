using UnityEngine;

namespace Game.UI.SmokeTest
{
    // Page-typed but shown via WindowArgs.AsSystem() to verify layer override.
    [Window("SmokeSystemDialog", WindowType.Page)]
    public sealed class SmokeSystemDialog : WindowController<SmokeWindowView>
    {
        protected override void OnShowStart() => Debug.Log("[Smoke] SmokeSystemDialog ShowStart");
        protected override void OnShowComplete() => Debug.Log("[Smoke] SmokeSystemDialog ShowComplete");
        protected override void OnHideComplete(bool isClosed) => Debug.Log($"[Smoke] SmokeSystemDialog HideComplete closed={isClosed}");
    }
}
