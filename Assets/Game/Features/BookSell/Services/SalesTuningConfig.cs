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
        [SerializeField, Range(0f, 1f)] private float _passiveSaleCommentChance = 0.35f;
        [Tooltip("Минимум пассивных попыток покупки за визит.")]
        [SerializeField] private int _minPassiveAttempts = 1;
        [Tooltip("Максимум пассивных попыток покупки за визит. Не зависит от числа книг на полке.")]
        [SerializeField] private int _maxPassiveAttempts = 6;
        [Tooltip("Доля пассивных запросов по жанрам из LocationConfig.DemandGenres для локаций с широким спросом.")]
        [SerializeField] private double _passiveDemandRequestShare = 0.70d;
        [Tooltip("Доля пассивных запросов по жанрам из LocationConfig.DemandGenres для локаций с узким спросом.")]
        [SerializeField] private double _passiveDemandRequestShareNarrow = 0.50d;
        [Tooltip("Минимальное число demand-жанров в локации, после которого используется широкая доля.")]
        [SerializeField] private int _narrowDemandGenreThreshold = 3;
        [SerializeField] private float _commentDuration = 1.2f;
        [SerializeField] private float _completePurchaseDuration = 1.5f;

        [Header("Leave")]
        [SerializeField] private float _leaveDuration = 3f;
        [SerializeField] private float _minLeaveDuration = 3f;
        [SerializeField] private float _maxLeaveDuration = 6f;

        [Header("Spawning")]
        [SerializeField] private float _spawnInterval = 5.0f;
        [SerializeField] private int _maxConcurrentCustomers = 9;
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
            PassiveSaleCommentChance = _passiveSaleCommentChance,
            MinPassiveAttempts = _minPassiveAttempts,
            MaxPassiveAttempts = _maxPassiveAttempts,
            PassiveDemandRequestShare = _passiveDemandRequestShare,
            PassiveDemandRequestShareNarrow = _passiveDemandRequestShareNarrow,
            NarrowDemandGenreThreshold = _narrowDemandGenreThreshold,
            CommentDuration = _commentDuration,
            CompletePurchaseDuration = _completePurchaseDuration,
            LeaveDuration = _leaveDuration,
            MinLeaveDuration = _minLeaveDuration,
            MaxLeaveDuration = _maxLeaveDuration,
            SpawnInterval = _spawnInterval,
            MaxConcurrentCustomers = _maxConcurrentCustomers,
            PassiveRequestGenreCount = _passiveRequestGenreCount
        };
    }
}
