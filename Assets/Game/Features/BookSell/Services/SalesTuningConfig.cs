using Book.Sell.Domain;
using UnityEngine;

namespace Book.Sell.Services
{
    /// <summary>
    /// Designer-editable asset for sales timing/pacing. Produces a pure-domain <see cref="SalesTuning"/>
    /// at install time, so the domain (and its tests) stay Unity-free while values are tweakable in the
    /// inspector. Assign on <c>GameInstaller</c>; if unassigned the binding falls back to code defaults.
    /// </summary>
    [CreateAssetMenu(menuName = "Book Sell/Sales Tuning", fileName = "SalesTuning")]
    public sealed class SalesTuningConfig : ScriptableObject
    {
        [Header("Approach")]
        [SerializeField] private float _approachDuration = 3f;
        [SerializeField] private float _minApproachDuration = 3f;
        [SerializeField] private float _maxApproachDuration = 6f;

        [Header("Passive purchase")]
        [SerializeField] private float _browseDuration = 3.5f;
        [SerializeField] private float _passiveCommitDelay = 0.6f;
        [SerializeField] private float _passiveFailureFeedbackDuration = 1.0f;
        [SerializeField] private float _passiveSaleFeedbackDuration = 1.0f;
        [SerializeField] private float _completePurchaseDuration = 1.5f;

        [Header("Leave")]
        [SerializeField] private float _leaveDuration = 3f;
        [SerializeField] private float _minLeaveDuration = 3f;
        [SerializeField] private float _maxLeaveDuration = 6f;

        [Header("Spawning")]
        [SerializeField] private float _spawnInterval = 5.0f;
        [SerializeField] private int _baseCustomers = 6;
        [SerializeField] private int _maxConcurrentCustomers = 3;
        [Tooltip("Сколько жанров в пассивном запросе покупателя (requested-genre модель).")]
        [SerializeField] private int _passiveRequestGenreCount = 2;

        public SalesTuning BuildTuning() => new()
        {
            ApproachDuration = _approachDuration,
            MinApproachDuration = _minApproachDuration,
            MaxApproachDuration = _maxApproachDuration,
            BrowseDuration = _browseDuration,
            PassiveCommitDelay = _passiveCommitDelay,
            PassiveFailureFeedbackDuration = _passiveFailureFeedbackDuration,
            PassiveSaleFeedbackDuration = _passiveSaleFeedbackDuration,
            CompletePurchaseDuration = _completePurchaseDuration,
            LeaveDuration = _leaveDuration,
            MinLeaveDuration = _minLeaveDuration,
            MaxLeaveDuration = _maxLeaveDuration,
            SpawnInterval = _spawnInterval,
            BaseCustomers = _baseCustomers,
            MaxConcurrentCustomers = _maxConcurrentCustomers,
            PassiveRequestGenreCount = _passiveRequestGenreCount
        };
    }
}
