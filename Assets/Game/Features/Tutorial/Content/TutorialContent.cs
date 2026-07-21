namespace Game.Tutorial.Content
{
    public static class TutorialContent
    {
        public static class Dialogues
        {
            public const string HubIntro = "tutorial_hub_intro";
        }

        public static class Placements
        {
            public const string Bottom = "bottom";
        }

        public static class SalesPhases
        {
            public const string Browsing = "Browsing";
            public const string Done = "Done";
        }

        public static class Characters
        {
            public const string Eddi = "eddi";
        }

        public static class Analytics
        {
            public const string EddiIntroStage = "eddi_intro";
            public const string SaleChanceStage = "sale_chance";
            public const string StateStart = "start";
            public const string StateEnd = "end";
        }
    }

    public static class TutorialTexts
    {
        public const string EddiSearch =
            "The client selects books from genres of interest to him.";
        public const string EddiSold =
            "If the customer finds the book he needs, he continues shopping.";
        public const string EddiFailed =
            "If not, then he leaves the store.";
        public const string DayOneLessonBooksDoNotGuaranteeSales =
            "The presence of books in the genres themselves does not guarantee sales.";
        public const string DayOneLessonMoreBooksRaiseChance =
            "The more books you have on your shelves in a particular genre, the higher your chance of selling them.";
        public const string DayOnePromptInspectSaleChance =
            "Click on a book to find out its chance of sale.";
        public const string SaleChanceHighlight =
            "Tap a genre to inspect sale chance.";
        public const string DayOneLessonSaleChance =
            "This is the chance a book of that genre will sell. Stock more of a genre to raise it.";
        public const string DayOneWrapUp =
            "Day complete - nice work! From tomorrow you'll stock the shelf and choose where to trade yourself.";
        public const string JournalHighlight =
            "Open the journal.";
        public const string ShopDecorStubLog =
            "Decor purchase detected (stub).";
    }
}
