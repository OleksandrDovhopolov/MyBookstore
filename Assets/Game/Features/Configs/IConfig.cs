namespace Game.Configs
{
    /// <summary>
    /// Базовый контракт data-driven конфига. Каждый конфиг — POCO с уникальным Id
    /// внутри своего типа. Файл конфига = JSON-массив объектов одного типа,
    /// индексируется в <see cref="ConfigCache{TBase}"/> по этому Id.
    /// </summary>
    public interface IConfig
    {
        string Id { get; }
    }
}
