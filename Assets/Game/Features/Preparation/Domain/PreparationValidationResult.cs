using System;
using System.Collections.Generic;

namespace Game.Preparation.Domain
{
    /// <summary>
    /// Результат валидации текущего выбора игрока перед ConfirmAsync.
    /// </summary>
    public sealed class PreparationValidationResult
    {
        public bool IsValid { get; }
        public IReadOnlyList<string> Errors { get; }

        private PreparationValidationResult(bool isValid, IReadOnlyList<string> errors)
        {
            IsValid = isValid;
            Errors = errors ?? Array.Empty<string>();
        }

        public static PreparationValidationResult Ok() => new(true, Array.Empty<string>());

        public static PreparationValidationResult Fail(params string[] errors) => new(false, errors ?? Array.Empty<string>());
    }
}
