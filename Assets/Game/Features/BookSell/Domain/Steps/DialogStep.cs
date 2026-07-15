using System;
using Book.Sell.API;
using Dialogue;

namespace Book.Sell.Domain.Steps
{
    /// <summary>
    /// A scripted dialogue inserted as a middle step. Mirrors <see cref="ActiveRequestStep"/> but without a
    /// Think dwell: on its first free tick it acquires the shared interaction lock, reports the dialogue via
    /// <see cref="ISalesDaySink.OnDialogueStarted"/>, and holds the lock — staying
    /// <see cref="StepStatus.Running"/> — until the controller resolves it (player closed the dialogue UI)
    /// and force-completes the step. While the lock is held by someone else the step is
    /// <see cref="StepStatus.Blocked"/>. Pure domain — no Unity types; presentation lives behind the sink.
    /// </summary>
    public sealed class DialogStep : ICustomerStep
    {
        private readonly DialoguePayload _payload;
        private bool _acquired;

        public DialogStep(DialoguePayload payload)
        {
            _payload = payload ?? throw new ArgumentNullException(nameof(payload));
        }

        public DialoguePayload Payload => _payload;

        public void Enter(Customer self, CustomerContext ctx)
        {
            _acquired = false;
        }

        public StepStatus Tick(Customer self, CustomerContext ctx, float dt)
        {
            if (_acquired) return StepStatus.Running;   // holding the lock, awaiting dialogue completion

            if (ctx.Lock.TryAcquire(self))
            {
                _acquired = true;
                // Phase is set only on acquire (not in Enter): while the lock is held by someone else the
                // customer waits and must not visually "talk" — same as ActiveRequestStep's InMinigame.
                self.SetPhase(CustomerPhase.InDialogue, ctx);
                ctx.Sink?.OnDialogueStarted(self, _payload);
                return StepStatus.Running;
            }

            // Lock held by someone else; TryAcquire enqueued us FIFO. Wait.
            return StepStatus.Blocked;
        }

        public void Exit(Customer self, CustomerContext ctx)
        {
            // Releases the lock if we held it, or removes us from the FIFO queue if we were only waiting.
            ctx.Lock.Release(self);
            _acquired = false;
        }
    }
}
