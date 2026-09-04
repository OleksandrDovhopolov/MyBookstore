using Game.UI.ContentWidget;

namespace Game.Location.UI
{
    public sealed class LocationRequirementInfoWidgetData : ContentWidgetDataBase
    {
        public LocationRequirementInfoWidgetData(string title, string hintText)
        {
            Title = title ?? string.Empty;
            HintText = hintText ?? string.Empty;
        }

        public string Title { get; }
        public string HintText { get; }
    }
}
