using Newtonsoft.Json.Linq;

namespace Game.Conditions.API
{
    /// <summary>
    /// Builds one leaf <see cref="ICondition"/> from its data-driven JSON node. This is the
    /// extensibility seam: a new feature (e.g. fishing) ships its own factory + data reader and
    /// registers it in DI — the engine and the location-unlock service stay untouched. The factory
    /// holds the injected read-only provider its conditions evaluate against.
    /// </summary>
    public interface IConditionFactory
    {
        /// <summary>Discriminator matched against the node's <c>"type"</c> field (case-insensitive).</summary>
        string Type { get; }

        /// <summary>Creates the condition from its node. May throw on malformed input; the parser catches it.</summary>
        ICondition Create(JObject node);
    }
}
