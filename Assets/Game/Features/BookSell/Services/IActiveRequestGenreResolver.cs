using System.Collections.Generic;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Policy for resolving which genres an active request serves. The current implementation infers
    /// genres from condition payloads; callers only depend on the flattened genre list.
    /// </summary>
    public interface IActiveRequestGenreResolver
    {
        IReadOnlyList<string> Resolve(RequestDefinitionConfig request);
    }
}
