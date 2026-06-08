namespace Game.Configs.Models
{
    /// <summary>
    /// Конфиг заказа/запроса покупателя. Поля иллюстративны — финальный набор
    /// выравнивается с Notion-задачей. Файл: requests.json (JSON-массив).
    /// </summary>
    [ConfigFile("requests")]
    public sealed class RequestConfig : IConfig
    {
        public string Id { get; set; }
        public string BookId { get; set; }
        public int RewardSoft { get; set; }
        public int TimeLimitSeconds { get; set; }
    }
}
