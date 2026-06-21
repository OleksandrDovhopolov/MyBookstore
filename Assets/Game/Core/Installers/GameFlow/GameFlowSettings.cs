using UnityEngine;

namespace Game.Bootstrap
{
    // Конфиг игрового цикла (имена сцен для GameFlowService). Отдельный SO, чтобы не смешивать
    // с BootstrapInstaller. Создаётся через Assets → Create → Game → GameFlow Settings и назначается
    // на BootstrapInstaller (_gameFlowSettings).
    [CreateAssetMenu(menuName = "Game/GameFlow Settings", fileName = "GameFlowSettings")]
    public sealed class GameFlowSettings : ScriptableObject
    {
        [Tooltip("Имя сцены хаба (базовая сцена, остаётся загруженной). Должна быть в Build Settings.")]
        [SerializeField] private string _gameplaySceneName = "GameplayScene";

        [Tooltip("Имя сцены локации (грузится additive поверх хаба). Должна быть в Build Settings.")]
        [SerializeField] private string _locationSceneName = "LocationScene";

        public string GameplaySceneName => _gameplaySceneName;
        public string LocationSceneName => _locationSceneName;
    }
}
