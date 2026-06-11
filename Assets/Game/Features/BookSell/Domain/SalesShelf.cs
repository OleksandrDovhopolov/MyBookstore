using System.Collections.Generic;

namespace Book.Sell.Domain
{
    /// <summary>
    /// The daily shelf with reserve-on-target semantics. Wraps the list of <see cref="ShelfBook"/>
    /// and a set of transient reservations: once a customer targets a book it is reserved (soft-lock)
    /// and disappears from <see cref="AvailableForSelection"/> — both from the active minigame shelf
    /// and from other passive customers — until it is sold or the reservation is released.
    /// </summary>
    public sealed class SalesShelf
    {
        private readonly List<ShelfBook> _books = new();
        private readonly HashSet<string> _reserved = new();

        public IReadOnlyList<ShelfBook> Books => _books;

        public void Add(ShelfBook book) => _books.Add(book);

        public ShelfBook Find(string bookId)
        {
            for (var i = 0; i < _books.Count; i++)
                if (_books[i].BookId == bookId) return _books[i];
            return null;
        }

        public bool IsReserved(string bookId) => _reserved.Contains(bookId);

        /// <summary>Books that can be picked right now: Available and not reserved.</summary>
        public List<ShelfBook> AvailableForSelection()
        {
            var result = new List<ShelfBook>(_books.Count);
            for (var i = 0; i < _books.Count; i++)
            {
                var book = _books[i];
                if (book.State == ShelfBookState.Available && !_reserved.Contains(book.BookId))
                    result.Add(book);
            }
            return result;
        }

        /// <summary>Reserve a book for an in-flight purchase. Returns false if not reservable.</summary>
        public bool Reserve(string bookId)
        {
            var book = Find(bookId);
            if (book == null || book.State != ShelfBookState.Available || _reserved.Contains(bookId))
                return false;
            _reserved.Add(bookId);
            return true;
        }

        /// <summary>Release a reservation without selling (e.g. customer aborted mid-purchase).</summary>
        public void ReleaseReserve(string bookId) => _reserved.Remove(bookId);

        /// <summary>Commit the sale: mark sold out and drop any reservation.</summary>
        public void CommitSale(string bookId)
        {
            var book = Find(bookId);
            if (book != null) book.State = ShelfBookState.SoldOut;
            _reserved.Remove(bookId);
        }

        /// <summary>True when no book is available for sale anymore (all sold out).</summary>
        public bool AllSoldOut()
        {
            for (var i = 0; i < _books.Count; i++)
                if (_books[i].State == ShelfBookState.Available) return false;
            return _books.Count > 0;
        }
    }
}
