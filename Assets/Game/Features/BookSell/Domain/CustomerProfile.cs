using System;
using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// A customer's desire profile — currently just the genres they are inclined to buy passively.
    /// Pure domain. Foundation for future quest personas and convergence with the active RequestConfig
    /// (kept separate for now).
    /// </summary>
    public sealed class CustomerProfile
    {
        public IReadOnlyList<string> DesiredGenres { get; }

        public CustomerProfile(IReadOnlyList<string> desiredGenres)
        {
            DesiredGenres = desiredGenres ?? Array.Empty<string>();
        }

        public static readonly CustomerProfile Empty = new(Array.Empty<string>());
    }
}
