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
    /// NOT wired into any production spawner yet (GAME-6 §Этап 4 boundary): <see cref="DialogStep"/> holds
    /// the interaction lock until presentation calls <c>CompleteDialogue</c> (§Этап 5); spawning it before
    /// that completer exists would hang the day. Used from tests only until §Этап 5.
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
