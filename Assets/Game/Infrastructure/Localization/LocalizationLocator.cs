namespace Game.Localization
{
    public static class LocalizationLocator
    {
        public static ILocalizationService Service { get; private set; }

        public static void SetService(ILocalizationService service)
            => Service = service;

        public static void Clear(ILocalizationService service)
        {
            if (ReferenceEquals(Service, service))
                Service = null;
        }

        public static string GetOrKey(string key)
            => Service != null ? Service.Get(key) : key ?? string.Empty;

        public static string GetOrKey(string key, params object[] args)
            => Service != null ? Service.Get(key, args) : key ?? string.Empty;
    }
}
