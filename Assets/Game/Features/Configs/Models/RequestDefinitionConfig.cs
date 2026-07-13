namespace Game.Configs.Models
{
    /// <summary>
    /// New condition-based active-purchase request. A request is a boolean predicate over a book:
    /// the book either satisfies it or it does not (set membership), as opposed to the legacy
    /// weighted-scoring model in <see cref="RequestConfig"/> (requests.json).
    ///
    /// <para>This type COEXISTS with the legacy <see cref="RequestConfig"/> and does NOT replace it yet —
    /// both config files are loaded. See docs/INPROGRESS/ACTIVE_REQUEST_CONDITIONS.md for the migration plan
    /// and the ADR that must be updated when this model is accepted.</para>
    ///
    /// <para>File: sample_requests.json (JSON array).</para>
    /// </summary>
    [ConfigFile("sample_requests")]
    public sealed class RequestDefinitionConfig : IConfig
    {
        public string Id { get; set; }

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
