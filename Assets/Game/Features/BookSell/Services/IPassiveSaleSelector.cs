using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Выбор книги для фоновой («пассивной») продажи: подходит ли что-то с полки под спрос локации.
    /// </summary>
    public interface IPassiveSaleSelector
    {
        /// <summary>
        /// Возвращает книгу для пассивной продажи или null, если на полке нет ничего, что матчит спрос локации.
        /// </summary>
        ShelfBook PickPassiveSale(IReadOnlyList<ShelfBook> shelf, LocationConfig location, ISalesRandom random);
    }
}
