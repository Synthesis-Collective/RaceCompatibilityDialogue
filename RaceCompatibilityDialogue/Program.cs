using System;
using System.Collections.Generic;
using System.Linq;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using System.Threading.Tasks;
using Mutagen.Bethesda.FormKeys.SkyrimSE;

namespace RaceCompatibilityDialogue
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .SetTypicalOpen(GameRelease.SkyrimSE, "RaceCompatibilityDialogue.esp")
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args);
        }

        public static readonly Dictionary<IFormLinkGetter<IRaceGetter>, IFormLinkGetter<IKeywordGetter>> vanillaRaceToActorProxyKeywords = new (){
            { Skyrim.Race.ArgonianRace, RaceCompatibility.Keyword.ActorProxyArgonian },
            { Skyrim.Race.BretonRace, RaceCompatibility.Keyword.ActorProxyBreton },
            { Skyrim.Race.DarkElfRace, RaceCompatibility.Keyword.ActorProxyDarkElf },
            { Skyrim.Race.HighElfRace, RaceCompatibility.Keyword.ActorProxyHighElf },
            { Skyrim.Race.ImperialRace, RaceCompatibility.Keyword.ActorProxyImperial },
            { Skyrim.Race.KhajiitRace, RaceCompatibility.Keyword.ActorProxyKhajiit },
            { Skyrim.Race.NordRace, RaceCompatibility.Keyword.ActorProxyNord },
            { Skyrim.Race.OrcRace, RaceCompatibility.Keyword.ActorProxyOrc },
            { Skyrim.Race.RedguardRace, RaceCompatibility.Keyword.ActorProxyRedguard },
            { Skyrim.Race.WoodElfRace, RaceCompatibility.Keyword.ActorProxyWoodElf },
        };

        public static readonly HashSet<IFormLinkGetter<IKeywordGetter>> actorProxyKeywords = new HashSet<IFormLinkGetter<IKeywordGetter>>(vanillaRaceToActorProxyKeywords.Values);

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var functionsOfInterest = new HashSet<ConditionData.Function>() { ConditionData.Function.GetIsRace, ConditionData.Function.GetPCIsRace };

            bool isConditionOnPlayerRace(IFunctionConditionDataGetter x)
            {
                return functionsOfInterest.Contains((ConditionData.Function)x.Function)
                    && vanillaRaceToActorProxyKeywords.ContainsKey(x.ParameterOneRecord.Cast<IRaceGetter>());
            }

            bool isConditionOnPlayerRaceProxyKeyword(IFunctionConditionDataGetter x)
            {
                return x.Function == (int)ConditionData.Function.HasKeyword
                    && actorProxyKeywords.Contains(x.ParameterOneRecord);
            }

            bool isVictim(IDialogResponsesGetter x)
            {
                bool ok = false;
                foreach (var data in x.Conditions
                    .OfType<IConditionFloatGetter>()
                    .Select(x => x.Data)
                    .OfType<IFunctionConditionDataGetter>())
                {
                    if (!ok && isConditionOnPlayerRace(data))
                        ok = true;
                    if (isConditionOnPlayerRaceProxyKeyword(data))
                        return false;
                }
                return ok;
            }

            int responseCounter = 0;
            var dialogueSet = new HashSet<FormKey>();

            foreach (var item in state.LoadOrder.PriorityOrder.DialogResponses().WinningContextOverrides(state.LinkCache))
            {
                if (!isVictim(item.Record)) continue;

                var response = item.GetOrAddAsOverride(state.PatchMod);

                //Console.WriteLine(response.FormKey);

                responseCounter++;

                if (item.Parent?.Record is IDialogTopicGetter getter) dialogueSet.Add(getter.FormKey);

                for (var i = response.Conditions.Count - 1; i >= 0; i--)
                {
                    if (response.Conditions[i] is not ConditionFloat condition) continue;

                    if (condition.Data is not FunctionConditionData data) continue;

                    if (!isConditionOnPlayerRace(data)) continue;

                    var newCondition = new ConditionFloat();
                    newCondition.DeepCopyIn(condition, new Condition.TranslationMask(defaultOn: true)
                    {
                        Unknown1 = false
                    });

                    switch (condition.CompareOperator)
                    {
                        case CompareOperator.EqualTo:
                            if (condition.ComparisonValue == 0)
                                newCondition.Flags = condition.Flags | Condition.Flag.OR;
                            break;
                        case CompareOperator.NotEqualTo:
                            if (condition.ComparisonValue == 1)
                                newCondition.Flags = condition.Flags | Condition.Flag.OR;
                            break;
                        case CompareOperator.GreaterThan:
                        case CompareOperator.LessThan:
                        case CompareOperator.GreaterThanOrEqualTo:
                        case CompareOperator.LessThanOrEqualTo:
                            Console.WriteLine($"TODO not sure how to handle condition in {item.Record.FormKey}");
                            continue;
                    }

                    var newData = new FunctionConditionData
                    {
                        Function = (ushort)ConditionData.Function.HasKeyword,
                        ParameterOneRecord = vanillaRaceToActorProxyKeywords[data.ParameterOneRecord.Cast<IRaceGetter>()].AsSetter()
                    };

                    newData.DeepCopyIn(data, new FunctionConditionData.TranslationMask(defaultOn: true)
                    {
                        Function = false,
                        ParameterOneRecord = false
                    });

                    if ((ConditionData.Function)data.Function is ConditionData.Function.GetPCIsRace)
                    {
                        newData.Unknown3 = (int)Condition.RunOnType.Reference;
                        newData.Reference.SetTo(Constants.Player);
                    }

                    newCondition.Data = newData;

                    response.Conditions.Insert(i, newCondition);
                }
            }

            int dialogueCounter = dialogueSet.Count;

            Console.WriteLine($"Modified {responseCounter} responses to {dialogueCounter} dialogue topics.");
        }
    }
}
