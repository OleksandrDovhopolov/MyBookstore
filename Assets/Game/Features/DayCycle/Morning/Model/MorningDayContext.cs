namespace Game.DayCycle.Morning.Model
{
    public sealed class MorningDayContext
    {
        public int Day { get; set; }
        public string DayId { get; set; }
        public string Title { get; set; }
        public string WeatherId { get; set; }
        public string EventId { get; set; }
    }
}
