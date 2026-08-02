using System.Collections.Generic;

namespace Game.Tutorial.API
{
    public static class TutorialSequenceIds
    {
        public const string DayOne = "tutorial_day_1";
        public const string Hub = "tutorial_hub";
        public const string ShopDecor = "tutorial_shop_decor";

        public static readonly IReadOnlyList<string> All = new[]
        {
            DayOne,
            Hub,
            ShopDecor
        };
    }
}
