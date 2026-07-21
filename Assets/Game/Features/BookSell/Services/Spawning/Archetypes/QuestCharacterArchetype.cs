using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Dialogue;

namespace Book.Sell.Services
{
    /// <summary>
    /// Quest character: a scripted dialogue up front, then N passive purchase attempts — "arrives → talks
    /// → shops". Deterministic: fixed <paramref name="passiveCount"/>, no random consumed (like
    /// <see cref="ActiveRequestArchetype"/>).
    ///
    /// Used by quest-aware production spawners. <see cref="DialogStep"/> holds the interaction lock until
    /// presentation calls <c>CompleteDialogue</c>, then the passive shopping steps continue.
    /// </summary>
    public sealed class QuestCharacterArchetype : ICustomerArchetype
    {
        private readonly DialoguePayload _payload;
        private readonly int _passiveCount;

        public QuestCharacterArchetype(DialoguePayload payload, int passiveCount = 1)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            _passiveCount = Math.Max(0, passiveCount);
        }

        public string Id => "quest_character";

        public IEnumerable<ICustomerStep> BuildMiddle(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var steps = new List<ICustomerStep>(1 + _passiveCount);
            steps.Add(new DialogStep(_payload));
            for (var i = 0; i < _passiveCount; i++)
                steps.Add(new PassivePurchaseStep());
            return steps;
        }
    }
}
