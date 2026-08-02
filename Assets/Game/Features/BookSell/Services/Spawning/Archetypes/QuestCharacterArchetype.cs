using System;
using System.Collections.Generic;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;
using Dialogue;

namespace Book.Sell.Services
{
    /// <summary>
    /// Quest character: a scripted dialogue up front, then an optional sales archetype.
    /// The passive-count constructor stays for older call sites; production can pass the post-dialogue
    /// archetype explicitly. <see cref="DialogStep"/> holds the interaction lock until presentation calls
    /// <c>CompleteDialogue</c>, then the sales steps continue.
    /// </summary>
    public sealed class QuestCharacterArchetype : ICustomerArchetype
    {
        private readonly DialoguePayload _payload;
        private readonly ICustomerArchetype _afterDialogue;

        public QuestCharacterArchetype(DialoguePayload payload, int passiveCount = 1)
            : this(payload, new PassiveAttemptsArchetype(Math.Max(0, passiveCount), Math.Max(0, passiveCount)))
        {
        }

        public QuestCharacterArchetype(DialoguePayload payload, ICustomerArchetype afterDialogue)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
            _afterDialogue = afterDialogue;
        }

        public string Id => "quest_character";

        public IEnumerable<ICustomerStep> BuildMiddle(SalesSessionSetup setup, SalesTuning tuning, ISalesRandom random)
        {
            var steps = new List<ICustomerStep> { new DialogStep(_payload) };
            var after = _afterDialogue?.BuildMiddle(setup, tuning, random);
            if (after != null)
                steps.AddRange(after);
            return steps;
        }
    }
}
