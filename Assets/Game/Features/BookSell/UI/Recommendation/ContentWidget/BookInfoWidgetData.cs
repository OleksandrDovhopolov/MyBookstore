using Game.UI.ContentWidget;

namespace Book.Sell.UI
{
    public sealed class BookInfoWidgetData : ContentWidgetDataBase
    {
        public BookInfoWidgetData(string title, string genres, string qualities)
        {
            Title = title;
            Genres = genres;
            Qualities = qualities;
        }

        public string Title { get; }
        public string Genres { get; }
        public string Qualities { get; }
    }
}
