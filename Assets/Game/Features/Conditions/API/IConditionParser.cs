using Newtonsoft.Json.Linq;

namespace Game.Conditions.API
{
    /// <summary>
    /// Turns a data-driven condition node into a typed <see cref="ICondition"/> tree. This is the
    /// boundary that keeps raw <see cref="JObject"/> out of the domain: callers parse once and then
    /// work only with the typed tree. Structural keys (<c>all</c> / <c>any</c> / <c>not</c>) are
    /// handled by the engine; leaves are dispatched to the registered <see cref="IConditionFactory"/>.
    /// </summary>
    public interface IConditionParser
    {
        /// <summary>
        /// Parses <paramref name="node"/>. A null/empty node means "no requirements" and yields an
        /// always-met condition. Unknown leaf types or malformed nodes yield a never-met condition
        /// (fail-closed) with an error logged, so a config typo locks content rather than crashing.
        /// </summary>
        ICondition Parse(JObject node);
    }
}
