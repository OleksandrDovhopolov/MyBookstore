using Newtonsoft.Json.Linq;

namespace Game.Configs.Models
{
    /// <summary>
    /// A single leaf predicate: apply <see cref="Operator"/> to the book field named by <see cref="Type"/>
    /// against <see cref="Value"/>.
    ///
    /// <para><see cref="Type"/> and <see cref="Operator"/> are kept as strings (not enums) on purpose: a new
    /// condition type or operator can be added by registering a handler, without touching this model
    /// (open/closed). The authoritative vocabulary lives in docs/INPROGRESS/ACTIVE_REQUEST_CONDITIONS.md.</para>
    ///
    /// <para><see cref="Value"/> is polymorphic (Newtonsoft <see cref="JToken"/>); its shape depends on the operator:</para>
    /// <list type="bullet">
    /// <item><description>scalar string / number — equal, notEqual, greater, greaterOrEqual, less, lessOrEqual, contains, notContains</description></item>
    /// <item><description>array of strings — containsAny, containsAll, containsNone</description></item>
    /// <item><description>object <c>{ "min": n, "max": n }</c> — between</description></item>
    /// </list>
    /// Handlers own the parsing of <see cref="Value"/> (they know the shape their operator expects).
    /// </summary>
    public sealed class RequestCondition
    {
        /// <summary>Book field the condition inspects: genres, tags, publicationYear, pages, ...</summary>
        public string Type { get; set; }

        /// <summary>Comparison operator, e.g. equal, between, contains, containsAll.</summary>
        public string Operator { get; set; }

        /// <summary>Operator argument. Shape depends on <see cref="Operator"/> — see the type summary.</summary>
        public JToken Value { get; set; }
    }
}
