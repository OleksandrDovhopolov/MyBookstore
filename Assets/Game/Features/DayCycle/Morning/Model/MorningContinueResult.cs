namespace Game.DayCycle.Morning.Model
{
    /// <summary>Payload returned when Morning advances to Preparation.</summary>
    public sealed class MorningContinueResult
    {
        public int Day { get; set; }
        public string DayId { get; set; }
        public string EventId { get; set; }
        public string WeatherId { get; set; }
    }
}
