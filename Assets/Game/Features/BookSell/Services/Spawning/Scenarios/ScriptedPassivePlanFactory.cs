using System.Collections.Generic;
using Book.Sell.Domain;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    public static class ScriptedPassivePlanFactory
    {
        public static int PassiveCountFor(ScriptedPassivePurchasePlan script)
            => script is { Count: > 0 } ? script.Count : 0;

        public static ScriptedPassivePurchasePlan Build(IReadOnlyList<ScriptedPassivePurchaseConfig> script)
        {
            if (script == null || script.Count == 0)
                return null;

            var attempts = new List<ScriptedPassiveAttempt>(script.Count);
            for (var i = 0; i < script.Count; i++)
            {
                var attempt = script[i];
                if (attempt == null) continue;
                attempts.Add(new ScriptedPassiveAttempt(attempt.Genre, attempt.ForceHit));
            }

            return attempts.Count > 0
                ? new ScriptedPassivePurchasePlan(attempts)
                : null;
        }
    }
}
