namespace Game.Configs.Models
{
    /// <summary>
    /// Condition-based active-purchase request — the sole active-request model. A request is a boolean
    /// predicate over a book: the book either satisfies it (Excellent) or it does not (Failed).
    ///
    /// <para>See docs/ACTIVE_REQUEST_CONDITIONS.md for the model spec and the ADR to update.</para>
    ///
    /// <para>File: sample_requests.json (JSON array). This is the sole active-request content file.</para>
    /// </summary>
    [ConfigFile("sample_requests")]
    public sealed class RequestDefinitionConfig : IConfig
    {
        public string Id { get; set; }

        /// <summary>Localization key for the customer-facing request text shown in the active recommendation UI.</summary>
        public string DescriptionKey { get; set; }

        /// <summary>Authoring metadata: the primary genre the request was designed around. Not used by evaluation.</summary>
        public string Genre { get; set; }

        /// <summary>Authoring metadata: the reference book the request was tuned against. Not used by evaluation.</summary>
        public string BookTitle { get; set; }

        /// <summary>When false the request is ignored by spawning / selection.</summary>
        public bool Enabled { get; set; }

        /// <summary>Boolean condition tree (flat all/any/none groups). See <see cref="RequestConditionGroup"/>.</summary>
        public RequestConditionGroup Conditions { get; set; }
    }
}
