using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using RaceCompatibilityDialogue;
using System.Linq;
using Xunit;

namespace Tests
{
    public class Tests
    {
        public static readonly ModKey masterModKey = ModKey.FromNameAndExtension("master.esm");
        public static readonly ModKey patchModKey = ModKey.FromNameAndExtension("patch.esp");

        public static readonly FormKey FormKey1 = patchModKey.MakeFormKey(0x123456);

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

        public static readonly TheoryData<CompareOperator, float> BooleanRepresentations = new()
        {
            { CompareOperator.EqualTo, 0f},
            { CompareOperator.LessThanOrEqualTo, 0f},
            { CompareOperator.NotEqualTo, 1f},
            { CompareOperator.LessThan, 1f },
            { CompareOperator.EqualTo, 1f },
            { CompareOperator.GreaterThanOrEqualTo, 1f },
            { CompareOperator.NotEqualTo, 0f },
            { CompareOperator.GreaterThan, 0f },
        };

        [Theory]
        [MemberData(nameof(RaceData))]
        public void IsVictim(FormLink<IRaceGetter> race, bool isPlayerRace)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            {
                var conditionData = new GetIsRaceConditionData();
                conditionData.Race.Link.SetTo(race);

                var condition = new ConditionFloat
                {
                    Data = conditionData
                };

                dialogResponses.Conditions.Add(condition);
            }

            if (!isPlayerRace)
            {
                Assert.False(Program.IsVictim(dialogResponses));
                return;
            }

            Assert.True(Program.IsVictim(dialogResponses));

            {
                var conditionData = new HasKeywordConditionData();
                conditionData.Keyword.Link.SetTo(Program.vanillaRaceToActorProxyKeywords[race]);

                var condition = new ConditionFloat
                {
                    Data = conditionData
                };

                dialogResponses.Conditions.Add(condition);
            }

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
        public void IsAlsoNotVictim(CompareOperator compareOperator, float comparisonValue)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            {
                var conditionData = new GetIsCrimeFactionConditionData()
                {
                    RunOnType = Condition.RunOnType.Subject
                };
                conditionData.Faction.Link.SetTo(Skyrim.Faction.AlduinFaction);

                var condition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = conditionData
                };

                dialogResponses.Conditions.Add(condition);
            }

            Assert.False(Program.IsVictim(dialogResponses));
        }

        [Theory]
        [MemberData(nameof(BooleanRepresentations))]
        public void TestTargetIsRace(CompareOperator compareOperator, float comparisonValue)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            {
                var conditionData = new GetIsRaceConditionData()
                {
                    RunOnType = Condition.RunOnType.Reference
                };
                conditionData.Race.Link.SetTo(NordRace);
                conditionData.Reference.SetTo(Constants.Player);

                var condition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = conditionData
                };

                dialogResponses.Conditions.Add(condition);
            }

            // !race -> !keyword | !race
            //  race ->  keyword | race

            Program.AdjustResponses(dialogResponses);


            Assert.Equal(2, dialogResponses.Conditions.Count);

            var oldCondition = dialogResponses.Conditions[0];

            Assert.True(oldCondition.Flags.HasFlag(Condition.Flag.OR));

            var newCondition = dialogResponses.Conditions[1];

            Assert.False(newCondition.Flags.HasFlag(Condition.Flag.OR));

            HasKeywordConditionData newConditionData = (HasKeywordConditionData)newCondition.Data;

            Assert.NotNull(newConditionData);

            Assert.Equal(compareOperator, newCondition.CompareOperator);
            Assert.Equal(NordRaceKeyword.AsNullable(), newConditionData.Keyword.Link);
        }

        [Theory]
        [MemberData(nameof(BooleanRepresentations))]
        public void TestDoesNotAdjustNonPlayerRace(CompareOperator compareOperator, float comparisonValue)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            {
                var conditionData = new GetIsRaceConditionData()
                {
                    RunOnType = Condition.RunOnType.Reference
                };
                conditionData.Race.Link.SetTo(NordRace);
                conditionData.Reference.SetTo(Constants.Player);

                var condition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = conditionData
                };
                condition.Flags = condition.Flags.SetFlag(Condition.Flag.OR, true);

                dialogResponses.Conditions.Add(condition);
            }

            {
                var conditionData = new GetIsRaceConditionData()
                {
                    RunOnType = Condition.RunOnType.Reference
                };
                conditionData.Race.Link.SetTo(Skyrim.Race.AlduinRace);
                conditionData.Reference.SetTo(Constants.Player);

                var condition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = conditionData
                };

                dialogResponses.Conditions.Add(condition);
            }

            // !race -> !keyword | !race
            //  race ->  keyword | race

            Program.AdjustResponses(dialogResponses);


            Assert.Equal(3, dialogResponses.Conditions.Count);

            var oldCondition1 = dialogResponses.Conditions[0];

            Assert.True(oldCondition1.Flags.HasFlag(Condition.Flag.OR));

            var oldCondition2 = dialogResponses.Conditions[1];

            Assert.True(oldCondition2.Flags.HasFlag(Condition.Flag.OR));

            var newCondition = dialogResponses.Conditions[2];

            Assert.False(newCondition.Flags.HasFlag(Condition.Flag.OR));

            HasKeywordConditionData newConditionData = (HasKeywordConditionData)newCondition.Data;

            Assert.NotNull(newConditionData);

            Assert.Equal(compareOperator, newCondition.CompareOperator);
            Assert.Equal(NordRaceKeyword.AsNullable(), newConditionData.Keyword.Link);
        }

        [Theory]
        [MemberData(nameof(BooleanRepresentations))]
        public void TestDoesNotAdjustOtherBooleanConditions(CompareOperator compareOperator, float comparisonValue)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            {
                var conditionData = new GetIsRaceConditionData()
                {
                    RunOnType = Condition.RunOnType.Reference
                };
                conditionData.Race.Link.SetTo(NordRace);
                conditionData.Reference.SetTo(Constants.Player);

                var condition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = conditionData
                };
                condition.Flags = condition.Flags.SetFlag(Condition.Flag.OR, true);

                dialogResponses.Conditions.Add(condition);
            }

            {
                var conditionData = new GetIsCrimeFactionConditionData()
                {
                    RunOnType = Condition.RunOnType.Subject
                };
                conditionData.Faction.Link.SetTo(Skyrim.Faction.AlduinFaction);

                var condition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = conditionData
                };

                dialogResponses.Conditions.Add(condition);
            }

            // !race -> !keyword | !race
            //  race ->  keyword | race

            Program.AdjustResponses(dialogResponses);


            Assert.Equal(3, dialogResponses.Conditions.Count);

            var oldCondition1 = dialogResponses.Conditions[0];

            Assert.True(oldCondition1.Flags.HasFlag(Condition.Flag.OR));

            var oldCondition2 = dialogResponses.Conditions[1];

            Assert.True(oldCondition2.Flags.HasFlag(Condition.Flag.OR));

            var newCondition = dialogResponses.Conditions[2];

            Assert.False(newCondition.Flags.HasFlag(Condition.Flag.OR));

            HasKeywordConditionData newConditionData = (HasKeywordConditionData)newCondition.Data;

            Assert.NotNull(newConditionData);

            Assert.Equal(compareOperator, newCondition.CompareOperator);
            Assert.Equal(NordRaceKeyword.AsNullable(), newConditionData.Keyword.Link);
        }

        [Theory]
        [MemberData(nameof(BooleanRepresentations))]
        public void TestPlayerIsRace(CompareOperator compareOperator, float comparisonValue)
        {
            var dialogResponses = new DialogResponses(FormKey1, SkyrimRelease.SkyrimSE);

            {
                var conditionData = new GetPCIsRaceConditionData();
                conditionData.Race.Link.SetTo(NordRace);

                var condition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = conditionData
                };

                dialogResponses.Conditions.Add(condition);
            }

            // !race -> !race | !keyword
            //  race ->  race |  keyword

            Program.AdjustResponses(dialogResponses);


            Assert.Equal(2, dialogResponses.Conditions.Count);

            var oldCondition = dialogResponses.Conditions[0];

            Assert.True(oldCondition.Flags.HasFlag(Condition.Flag.OR));

            var newCondition = dialogResponses.Conditions[1];

            HasKeywordConditionData newConditionData = (HasKeywordConditionData)newCondition.Data;

            Assert.NotNull(newConditionData);

            Assert.Equal(compareOperator, newCondition.CompareOperator);
            Assert.Equal(NordRaceKeyword.AsNullable(), newConditionData.Keyword.Link);
            Assert.Equal(Condition.RunOnType.Reference, newConditionData.RunOnType);
            Assert.Equal(Constants.Player, newConditionData.Reference);
        }

        [Theory]
        [MemberData(nameof(BooleanRepresentations))]
        public void TestRunPatchstate(CompareOperator compareOperator, float comparisonValue)
        {
            var masterMod = new SkyrimMod(masterModKey, SkyrimRelease.SkyrimSE);

            var dialogTopics = masterMod.DialogTopics.AddNew();

            var dialogResponses = new DialogResponses(masterMod, "myResponse");
            dialogTopics.Responses.Add(dialogResponses);

            {
                var getPCIsRaceConditionData = new GetPCIsRaceConditionData();
                getPCIsRaceConditionData.Race.Link.SetTo(NordRace);

                var oldCondition = new ConditionFloat()
                {
                    CompareOperator = compareOperator,
                    ComparisonValue = comparisonValue,
                    Data = getPCIsRaceConditionData
                };

                dialogResponses.Conditions.Add(oldCondition);
            }

            var patchMod = new SkyrimMod(patchModKey, SkyrimRelease.SkyrimSE);


            var loadOrder = new LoadOrder<IModListing<ISkyrimModGetter>>()
            {
                new ModListing<ISkyrimModGetter>(masterMod, true),
                new ModListing<ISkyrimModGetter>(patchMod, true)
            };

            var linkCache = loadOrder.ToImmutableLinkCache();

            var program = new Program(loadOrder, linkCache, patchMod);


            // !race -> !race | !keyword
            //  race ->  race |  keyword

            program.RunPatch();


            var newDialogResponses = patchMod.DialogTopics[dialogTopics.FormKey].Responses.Single();


            Assert.Equal(2, newDialogResponses.Conditions.Count);

            var originalCondition = newDialogResponses.Conditions[0];
            Assert.True(originalCondition.Flags.HasFlag(Condition.Flag.OR));

            var newCondition = newDialogResponses.Conditions[1];

            Assert.False(newCondition.Flags.HasFlag(Condition.Flag.OR));

            HasKeywordConditionData newConditionData = (HasKeywordConditionData)newCondition.Data;

            Assert.NotNull(newConditionData);

            Assert.Equal(compareOperator, newCondition.CompareOperator);
            Assert.Equal(NordRaceKeyword.AsNullable(), newConditionData.Keyword.Link);
            Assert.Equal(Condition.RunOnType.Reference, newConditionData.RunOnType);
            Assert.Equal(Constants.Player, newConditionData.Reference);
        }

        [Theory]
        [InlineData((CompareOperator)42, 0)]
        [InlineData((CompareOperator)42, 1)]
        [InlineData(CompareOperator.EqualTo, -1)]
        [InlineData(CompareOperator.EqualTo, 0.5)]
        [InlineData(CompareOperator.EqualTo, 2)]
        public void TestInvalidBooleans(CompareOperator op, float value)
        {
            var condition = new ConditionFloat()
            {
                CompareOperator = op,
                ComparisonValue = value
            };

            Assert.False(Program.IsBoolean(condition));
        }
    }
}
