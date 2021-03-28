using Mutagen.Bethesda.Skyrim;
using RaceCompatibilityDialogue;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Tests
{
    public class GroupConditions
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TestAnd(int count)
        {
            List<Condition> conditions = new();

            // cond1 & cond2 & cond3
            for (int i = 0; i < count; i++)
                conditions.Add(new ConditionFloat());


            var grouped = Program.GroupConditions(conditions);


            Assert.Equal(count, grouped.Count);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void TestAndOr(int count)
        {
            List<Condition> conditions = new();

            // (cond1 | cond2) & (cond3 | cond4) & ...
            for (int i = 0; i < count; i++)
            {
                conditions.Add(new ConditionFloat()
                {
                    Flags = Condition.Flag.OR
                });
                conditions.Add(new ConditionFloat());
            }


            var grouped = Program.GroupConditions(conditions);


            Assert.Equal(count, grouped.Count);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(1, false)]
        [InlineData(2, true)]
        [InlineData(2, false)]
        [InlineData(3, true)]
        [InlineData(3, false)]
        public void TestOr(int count, bool finalOr)
        {
            List<Condition> conditions = new();

            // cond1 | cond2 | cond3 |
            for (int i = 0; i < count; i++)
            {
                conditions.Add(new ConditionFloat()
                {
                    Flags = Condition.Flag.OR
                });
            }

            // cond3 | -> cond3
            if (!finalOr && conditions.Count > 0)
                conditions[^1].Flags ^= Condition.Flag.OR;


            var grouped = Program.GroupConditions(conditions);


            if (count == 0)
            {
                Assert.Empty(grouped);
                return;
            }

            Assert.Single(grouped);

            Assert.Equal(count, grouped.Single().Count);
        }
    }
}
