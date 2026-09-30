using System.Collections.Generic;
using Book.Sell.Domain;
using Book.Sell.Services;

namespace Book.Sell.Tests.Editor.Fakes
{
    /// <summary>
    /// Hands out queued requests in order, recording the shelf it was shown each time. The recorded shelves
    /// are the point: the whole reason the draw moved into the step is that it must see the shelf as it
    /// stands when the customer walks up, not as it was when the day was planned.
    /// </summary>
    public sealed class FakeActiveRequestSelector : IActiveRequestSelector
    {
        private readonly Queue<ActiveRequestRuntime> _queue = new();

        /// <summary>Book ids that were available for selection at each Draw, oldest first.</summary>
        public List<string[]> ShelvesSeen { get; } = new();

        public List<CustomerProfile> ProfilesSeen { get; } = new();

        public int DrawCount => ShelvesSeen.Count;

        public FakeActiveRequestSelector Enqueue(params ActiveRequestRuntime[] requests)
        {
            foreach (var request in requests) _queue.Enqueue(request);
            return this;
        }

        public ActiveRequestRuntime Draw(CustomerProfile profile, IReadOnlyList<ShelfBook> shelf, ISalesRandom random)
        {
            var ids = new List<string>();
            if (shelf != null)
                foreach (var book in shelf)
                    if (book != null) ids.Add(book.BookId);

            ShelvesSeen.Add(ids.ToArray());
            ProfilesSeen.Add(profile);

            return _queue.Count > 0 ? _queue.Dequeue() : null;
        }
    }
}
