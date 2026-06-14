using UnityEngine;

namespace Game.UI.SmokeTest
{
    [Window("Prefabs/UI/SmokeTest/SmokeMainPage", WindowType.Page)]
    public sealed class SmokeMainPage : WindowController<SmokeWindowView>
    {
        protected override void OnShowStart() => Debug.Log("[Smoke] SmokeMainPage ShowStart");
        protected override void OnShowComplete() => Debug.Log("[Smoke] SmokeMainPage ShowComplete");
        protected override void OnHideComplete(bool isClosed) => Debug.Log($"[Smoke] SmokeMainPage HideComplete closed={isClosed}");
    }
}
