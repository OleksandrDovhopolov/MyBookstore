using UnityEngine;

namespace Game.Cheat
{
    public sealed class FpsOverlay : MonoBehaviour
    {
        private const float RefreshInterval = 0.25f;
        private const float SmoothFactor = 0.1f;
        private const float Margin = 35f;
        private const float Width = 300f;
        private const float Height = 108f;

        private static FpsOverlay Instance;

        private bool _visible;
        private bool _hasSample;
        private float _smoothDeltaTime;
        private float _timeUntilRefresh;
        private string _displayText = "FPS: --\n-- ms";
        private GUIStyle _boxStyle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static void ToggleVisible()
        {
            var overlay = EnsureInstance();
            overlay.SetVisible(!overlay._visible);
        }

        private static FpsOverlay EnsureInstance()
        {
            if (Instance != null)
                return Instance;

            var go = new GameObject(nameof(FpsOverlay))
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            Instance = go.AddComponent<FpsOverlay>();
            Instance.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            return Instance;
        }

        private void Update()
        {
            if (!_visible)
                return;

            var deltaTime = Time.unscaledDeltaTime;
            if (!_hasSample)
            {
                _smoothDeltaTime = deltaTime;
                _hasSample = true;
            }
            else
            {
                _smoothDeltaTime = Mathf.Lerp(_smoothDeltaTime, deltaTime, SmoothFactor);
            }

            _timeUntilRefresh -= deltaTime;
            if (_timeUntilRefresh > 0f)
                return;

            _timeUntilRefresh = RefreshInterval;

            var fps = _smoothDeltaTime > 0f ? 1f / _smoothDeltaTime : 0f;
            var ms = _smoothDeltaTime * 1000f;
            _displayText = $"FPS: {fps:0}\n{ms:0.0} ms";
        }

        private void OnGUI()
        {
            if (!_visible)
                return;

            EnsureStyle();
            GUI.Box(new Rect(Margin, Margin, Width, Height), _displayText, _boxStyle);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            if (!visible)
                return;

            _hasSample = false;
            _timeUntilRefresh = 0f;
        }

        private void EnsureStyle()
        {
            if (_boxStyle != null)
                return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.alignment = TextAnchor.MiddleLeft;
            _boxStyle.fontSize = 30;
            _boxStyle.normal.textColor = Color.white;
            _boxStyle.padding = new RectOffset(12, 12, 6, 6);
        }
    }
}
