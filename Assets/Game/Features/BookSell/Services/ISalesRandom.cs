namespace Book.Sell.Services
{
    /// <summary>
    /// Порт случайности для Sales. Под капотом UnityEngine.Random в проде, фейк-очередь в тестах.
    /// Через порт фиксируем detеrminism и не зависим от глобального состояния Random.
    /// </summary>
    public interface ISalesRandom
    {
        /// <summary>Возвращает int в [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>Возвращает double в [0, 1).</summary>
        double NextDouble();
    }
}
