using UnityEngine;

namespace Game.WorldHud
{
    // Immutable settings for an attached world-space HUD instance.
    // To change offset or billboard mode mid-life: Detach + Attach a new instance.
    public sealed class WorldHudArgs
    {
        public Vector3 Offset { get; }
        public bool Billboard { get; }
        public float AutoCloseSeconds { get; }

        public WorldHudArgs(Vector3? offset = null, bool billboard = true, float autoCloseSeconds = 0f)
        {
            Offset = offset ?? new Vector3(0f, 2f, 0f);
            Billboard = billboard;
            AutoCloseSeconds = autoCloseSeconds;
        }

        public static readonly WorldHudArgs Default = new();
    }
}
