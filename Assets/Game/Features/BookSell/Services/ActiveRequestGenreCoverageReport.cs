using System.Collections.Generic;

namespace Book.Sell.Services
{
    public sealed class ActiveRequestGenreCoverageReport
    {
        public ActiveRequestGenreCoverageReport(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            Errors = errors;
            Warnings = warnings;
        }

        public IReadOnlyList<string> Errors { get; }
        public IReadOnlyList<string> Warnings { get; }
    }
}
