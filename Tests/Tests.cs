using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using RaceCompatibilityDialogue;
using Xunit;

namespace Tests
{
    public class Tests
    {
        public static readonly ModKey FooEsp = ModKey.FromNameAndExtension("foo.esp");

        public static readonly FormKey FormKey1 = FooEsp.MakeFormKey(0x123456);
        public static readonly FormKey FormKey2 = FooEsp.MakeFormKey(0x234561);

        public static readonly FormLink<IRaceGetter> NordRace = Skyrim.Race.NordRace;

        public static readonly FormLink<IKeywordGetter> NordRaceKeyword = RaceCompatibility.Keyword.ActorProxyNord;

        public static readonly TheoryData<IFormLink<IRaceGetter>, bool> RaceData = new()
        {
            { Skyrim.Race.ArgonianRace, true },
            { Skyrim.Race.BretonRace, true },
            { Skyrim.Race.DarkElfRace, true },
            { Skyrim.Race.HighElfRace, true },
            { Skyrim.Race.ImperialRace, true },
            { Skyrim.Race.KhajiitRace, true },
            { Skyrim.Race.NordRace, true },
            { Skyrim.Race.OrcRace, true },
            { Skyrim.Race.RedguardRace, true },
            { Skyrim.Race.WoodElfRace, true },
            { Skyrim.Race.AlduinRace, false }
        };

        public static readonly TheoryData<CompareOperator, float, bool> BooleanRepresentations = new()
        {
            { CompareOperator.EqualTo, 0f, false },
            { CompareOperator.LessThanOrEqualTo, 0f, false },
            { CompareOperator.NotEqualTo, 1f, false },
            { CompareOperator.LessThan, 1f, false },
            { CompareOperator.EqualTo, 1f, true },
            { CompareOperator.GreaterThanOrEqualTo, 1f, true },
            { CompareOperator.NotEqualTo, 0f, true },
            { CompareOperator.GreaterThan, 0f, true },
        };

        [Theory]
        [MemberData(nameof(RaceData))]
        public void IsVictim(FormLink<IRaceGetter> race, bool isPlayerRace)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            dialogResponses.Conditions.Add(new ConditionFloat()
            {
                Data = new FunctionConditionData()
                {
                    Function = (ushort)ConditionData.Function.GetIsRace,
                    ParameterOneRecord = race
                }
            });

            if (!isPlayerRace)
            {
                Assert.False(Program.IsVictim(dialogResponses));
                return;
            }

            Assert.True(Program.IsVictim(dialogResponses));

            dialogResponses.Conditions.Add(new ConditionFloat()
            {
                Data = new FunctionConditionData()
                {
                    Function = (ushort)ConditionData.Function.HasKeyword,
                    ParameterOneRecord = Program.vanillaRaceToActorProxyKeywords[race].AsSetter()
                }
            });

            Assert.False(Program.IsVictim(dialogResponses));
        }

        [Fact]
        public void IsNotVictim()
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            Assert.False(Program.IsVictim(dialogResponses));
        }

        [Theory]
        [MemberData(nameof(BooleanRepresentations))]
        public void TestTargetIsRace(CompareOperator compareOperator, float comparisonValue, bool isTrue)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            dialogResponses.Conditions.Add(new ConditionFloat()
            {
                CompareOperator = compareOperator,
                ComparisonValue = comparisonValue,
                Data = new FunctionConditionData()
                {
                    Function = (ushort)ConditionData.Function.GetIsRace,
                    ParameterOneRecord = NordRace,
                    RunOnType = Condition.RunOnType.Reference,
                    Reference = Constants.Player.AsSetter()
                }
            });

            Program.AdjustResponses(FormKey2, dialogResponses);

            Assert.Equal(2, dialogResponses.Conditions.Count);

            var newCondition = dialogResponses.Conditions[0];

            // !race -> !keyword && !race
            // race -> (keyword || race)

            if (isTrue)
                Assert.False(newCondition.Flags.HasFlag(Condition.Flag.OR));
            else
                Assert.True(newCondition.Flags.HasFlag(Condition.Flag.OR));

            FunctionConditionData newConditionData = (FunctionConditionData)newCondition.Data;

            Assert.NotNull(newConditionData);

            Assert.Equal(compareOperator, newCondition.CompareOperator);
            Assert.Equal((ushort)ConditionData.Function.HasKeyword, newConditionData.Function);
            Assert.Equal(NordRaceKeyword, newConditionData.ParameterOneRecord);
        }

        [Theory]
        [MemberData(nameof(BooleanRepresentations))]
        public void TestPlayerIsRace(CompareOperator compareOperator, float comparisonValue, bool isTrue)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            dialogResponses.Conditions.Add(new ConditionFloat()
            {
                CompareOperator = compareOperator,
                ComparisonValue = comparisonValue,
                Data = new FunctionConditionData()
                {
                    Function = (ushort)ConditionData.Function.GetPCIsRace,
                    ParameterOneRecord = NordRace
                }
            });

            Program.AdjustResponses(FormKey2, dialogResponses);

            Assert.Equal(2, dialogResponses.Conditions.Count);

            var newCondition = dialogResponses.Conditions[0];

            // !race -> !keyword && !race
            // race -> (keyword || race)

            if (isTrue)
                Assert.False(newCondition.Flags.HasFlag(Condition.Flag.OR));
            else
                Assert.True(newCondition.Flags.HasFlag(Condition.Flag.OR));

            FunctionConditionData newConditionData = (FunctionConditionData)newCondition.Data;

            Assert.NotNull(newConditionData);

            Assert.Equal(compareOperator, newCondition.CompareOperator);
            Assert.Equal((ushort)ConditionData.Function.HasKeyword, newConditionData.Function);
            Assert.Equal(NordRaceKeyword, newConditionData.ParameterOneRecord);
            Assert.Equal(Condition.RunOnType.Reference, newConditionData.RunOnType);
            Assert.Equal(Constants.Player, newConditionData.Reference);
        }

    }
}
