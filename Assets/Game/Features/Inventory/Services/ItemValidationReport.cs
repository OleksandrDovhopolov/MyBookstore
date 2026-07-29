using System.Collections.Generic;
using System.Text;

namespace Game.Inventory.Services
{
    /// <summary>
    /// Result of <see cref="ItemReferenceValidator.Validate"/>. Mirrors the shape of the decor
    /// validator's report; kept local to Inventory so the two features stay independent.
    /// </summary>
    public sealed class ItemValidationReport
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();

        public bool HasErrors => Errors.Count > 0;
        public bool HasWarnings => Warnings.Count > 0;

        public string FormatErrors()
        {
            if (!HasErrors) return string.Empty;

            var sb = new StringBuilder();
            for (var i = 0; i < Errors.Count; i++)
                sb.AppendLine($"  - {Errors[i]}");
            return sb.ToString();
        }
    }
}
