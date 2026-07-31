using Book.Sell.API;
using Game.Conditions.API;

namespace Book.Sell.Conditions
{
    /// <summary>
    /// Leaf condition: dialogue <c>dialogueId</c> has already been delivered to the player.
    /// </summary>
    public sealed class DialogueDeliveredCondition : ICondition
    {
        private readonly IDeliveredDialoguesService _delivered;
        private readonly string _dialogueId;

        public DialogueDeliveredCondition(IDeliveredDialoguesService delivered, string dialogueId)
        {
            _delivered = delivered;
            _dialogueId = dialogueId;
        }

        public ConditionResult Evaluate()
            => ConditionResult.Boolean(_delivered.IsDelivered(_dialogueId), $"dialogueDelivered.{_dialogueId}");
    }
}
