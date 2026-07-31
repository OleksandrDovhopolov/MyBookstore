using System;
using Book.Sell.API;
using Game.Conditions.API;
using Newtonsoft.Json.Linq;

namespace Book.Sell.Conditions
{
    /// <summary>
    /// Builds <see cref="DialogueDeliveredCondition"/> from
    /// <c>{ "type": "dialogueDelivered", "dialogueId": "eddy1" }</c>.
    /// </summary>
    public sealed class DialogueDeliveredConditionFactory : IConditionFactory
    {
        public const string TypeId = "dialogueDelivered";

        private readonly IDeliveredDialoguesService _delivered;

        public DialogueDeliveredConditionFactory(IDeliveredDialoguesService delivered)
            => _delivered = delivered ?? throw new ArgumentNullException(nameof(delivered));

        public string Type => TypeId;

        public ICondition Create(JObject node)
        {
            var dialogueId = node.Value<string>("dialogueId");
            if (string.IsNullOrWhiteSpace(dialogueId))
                throw new ArgumentException("missing 'dialogueId'");

            return new DialogueDeliveredCondition(_delivered, dialogueId);
        }
    }
}
