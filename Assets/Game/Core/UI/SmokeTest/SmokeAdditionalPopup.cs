using UnityEngine;

namespace Game.UI.SmokeTest
{
    [Window("SmokeAdditionalPopup", WindowType.Popup)]
    public sealed class SmokeAdditionalPopup : WindowController<SmokeWindowView>
    {
        protected override void OnShowStart() => Debug.Log("[Smoke] SmokeAdditionalPopup ShowStart");
        protected override void OnShowComplete() => Debug.Log("[Smoke] SmokeAdditionalPopup ShowComplete");
        protected override void OnHideComplete(bool isClosed) => Debug.Log($"[Smoke] SmokeAdditionalPopup HideComplete closed={isClosed}");
    }
}
