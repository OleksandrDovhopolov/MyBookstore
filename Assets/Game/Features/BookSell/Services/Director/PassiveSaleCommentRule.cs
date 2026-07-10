using System;
using Book.Sell.API;
using Book.Sell.Domain;
using Book.Sell.Domain.Steps;

namespace Book.Sell.Services.Director
{
    public sealed class PassiveSaleCommentRule : IPassiveSaleRule
    {
        public void OnPassiveSale(Customer customer, PassiveSaleEvent sale, CustomerContext ctx)
        {
            if (customer == null) throw new ArgumentNullException(nameof(customer));
            if (sale == null) throw new ArgumentNullException(nameof(sale));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            var chance = Clamp01(ctx.Tuning.PassiveSaleCommentChance);
            if (chance <= 0f) return;
            if (chance < 1f && ctx.Random.NextDouble() >= chance) return;

            var payload = new CustomerCommentPayload(
                sale.BookId,
                sale.MatchedGenres.Count > 0 ? sale.MatchedGenres[0] : string.Empty);

            customer.InsertNext(new CommentStep(payload));
        }

        private static float Clamp01(float value)
        {
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }
    }
}
