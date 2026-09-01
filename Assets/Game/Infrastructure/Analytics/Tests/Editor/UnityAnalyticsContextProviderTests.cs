using Analytics;
using NUnit.Framework;

namespace AnalyticsTests.Editor
{
    public sealed class UnityAnalyticsContextProviderTests
    {
        [Test]
        public void GetCommonParameters_WithoutExplicitUserId_StaysWithinFirebaseBudgetContract()
        {
            var provider = new UnityAnalyticsContextProvider(new TestAnalyticsConfig());

            var parameters = provider.GetCommonParameters();

            Assert.That(parameters.Count, Is.LessThanOrEqualTo(6));
            Assert.That(parameters.ContainsKey(AnalyticsParameterNames.UserId), Is.False);
        }
    }
}
