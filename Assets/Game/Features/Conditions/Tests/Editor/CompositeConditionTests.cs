using Game.Conditions.API;
using Game.Conditions.Services;
using Game.Conditions.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Conditions.Tests.Editor
{
    public sealed class CompositeConditionTests
    {
        private static ICondition Met() => new StubCondition(5, 5);
        private static ICondition Unmet() => new StubCondition(1, 5);

        [Test]
        public void Leaf_MetWhenCurrentReachesTarget()
        {
            Assert.IsTrue(new StubCondition(30, 30).Evaluate().IsMet);
            Assert.IsTrue(new StubCondition(31, 30).Evaluate().IsMet);
            Assert.IsFalse(new StubCondition(29, 30).Evaluate().IsMet);
        }

        [Test]
        public void AllOf_MetOnlyWhenEveryChildMet()
        {
            Assert.IsTrue(new AllOfCondition(new[] { Met(), Met() }).Evaluate().IsMet);
            Assert.IsFalse(new AllOfCondition(new[] { Met(), Unmet() }).Evaluate().IsMet);
        }

        [Test]
        public void AllOf_ReportsMetCountAsProgress_AndExposesChildren()
        {
            var result = new AllOfCondition(new[] { Met(), Unmet(), Met() }).Evaluate();

            Assert.AreEqual(2, result.Current);
            Assert.AreEqual(3, result.Target);
            Assert.AreEqual(3, result.Children.Count);
            Assert.AreEqual(AllOfCondition.ReasonKey, result.ReasonKey);
        }

        [Test]
        public void AllOf_Empty_IsVacuouslyMet()
        {
            Assert.IsTrue(new AllOfCondition(new ICondition[0]).Evaluate().IsMet);
        }

        [Test]
        public void AnyOf_MetWhenAtLeastOneChildMet()
        {
            Assert.IsTrue(new AnyOfCondition(new[] { Unmet(), Met() }).Evaluate().IsMet);
            Assert.IsFalse(new AnyOfCondition(new[] { Unmet(), Unmet() }).Evaluate().IsMet);
        }

        [Test]
        public void AnyOf_Empty_IsNotMet()
        {
            Assert.IsFalse(new AnyOfCondition(new ICondition[0]).Evaluate().IsMet);
        }

        [Test]
        public void Not_InvertsChild()
        {
            Assert.IsFalse(new NotCondition(Met()).Evaluate().IsMet);
            Assert.IsTrue(new NotCondition(Unmet()).Evaluate().IsMet);
        }

        [Test]
        public void Nested_AllOfWithAnyOf()
        {
            // all[ met, any[ unmet, met ] ] => met
            var condition = new AllOfCondition(new ICondition[]
            {
                Met(),
                new AnyOfCondition(new[] { Unmet(), Met() })
            });

            Assert.IsTrue(condition.Evaluate().IsMet);
        }
    }
}
