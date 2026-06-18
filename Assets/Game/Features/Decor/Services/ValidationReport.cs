using System.Collections.Generic;
using System.Text;

namespace Game.Decor.Services
{
    public sealed class ValidationReport
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
            {
                sb.AppendLine($"  - {Errors[i]}");
            }
            return sb.ToString();
        }
    }
}
