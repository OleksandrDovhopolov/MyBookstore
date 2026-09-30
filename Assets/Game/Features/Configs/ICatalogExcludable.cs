namespace Game.Configs
{
    /// <summary>
    /// Opt-in contract for configs that authoring keeps in the json file but the game must not see.
    /// <see cref="ConfigsService"/> drops these entries while deserializing, so every read path
    /// (Get / TryGet / IsExists / GetAll) agrees on one catalogue and no call site needs its own filter.
    ///
    /// <para>Used by book content: the catalogue was seeded from an external sheet where rows marked
    /// "Fake" are in-world inventions of that sheet's setting. They stay in books.json as authoring
    /// history — the file is still validated and diffed as a whole — but they are not this game's books.</para>
    /// </summary>
    public interface ICatalogExcludable
    {
        /// <summary>True when this entry must not enter the runtime catalogue.</summary>
        bool IsExcludedFromCatalog { get; }
    }
}
