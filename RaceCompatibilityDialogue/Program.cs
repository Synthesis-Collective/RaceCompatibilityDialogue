using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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

        public static readonly Dictionary<IFormLinkGetter<IRaceGetter>, IFormLinkGetter<IKeywordGetter>> vanillaRaceToActorProxyKeywords = new()
        {
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

        public static readonly HashSet<IFormLinkGetter<IKeywordGetter>> actorProxyKeywords = new(vanillaRaceToActorProxyKeywords.Values);

        public static readonly HashSet<Condition.Function> functionsOfInterest = new()
        {
            Condition.Function.GetIsRace,
            Condition.Function.GetPCIsRace
        };

        protected readonly LoadOrder<IModListing<ISkyrimModGetter>> LoadOrder;
        protected readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache;
        protected readonly ISkyrimMod PatchMod;

        public Program(LoadOrder<IModListing<ISkyrimModGetter>> loadOrder, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, ISkyrimMod patchMod)
        {
            LoadOrder = loadOrder;
            LinkCache = linkCache;
            PatchMod = patchMod;
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            var program = new Program(state.LoadOrder, state.LinkCache, state.PatchMod);

            program.RunPatch();
        }

        public void RunPatch()
        {
            int responseCounter = 0;
            var dialogueSet = new HashSet<IFormLink<IDialogTopicGetter>>();

            foreach (var item in LoadOrder.PriorityOrder.DialogResponses().WinningContextOverrides(LinkCache))
            {
                if (!IsVictim(item.Record)) continue;

                var response = item.GetOrAddAsOverride(PatchMod);

                responseCounter++;

                if (item.Parent?.Record is IDialogTopicGetter getter) dialogueSet.Add(getter.AsLink());

                AdjustResponses(item.Record.FormKey, response);
            }

            int dialogueCounter = dialogueSet.Count;

            Console.WriteLine($"Modified {responseCounter} responses to {dialogueCounter} dialogue topics.");
        }

        public static void AdjustResponses(FormKey formKey2, IDialogResponses response)
        {
            for (var i = response.Conditions.Count - 1; i >= 0; i--)
            {
                if (response.Conditions[i] is not ConditionFloat condition) continue;

                if (!IsBoolean(condition)) continue;

                if (condition.Data is not FunctionConditionData data) continue;

                if (!IsConditionOnPlayerRace(data)) continue;

                // TODO Support is-x, is-vampire-x in addition to is-x-or-vampire-x (the existing keyword)
                //
                // current behaviour:
                //  * is race X => is race X or actorProxyX
                //
                // potential solution:
                //  * is race X or is race vampireX -> actorProxyX
                //  * is race X -> actorProxyX and not Vampire
                //  * is race vampireX -> actorProxyX and Vampire
                //  
                // var vampireKeyword = Skyrim.Keyword.Vampire
                // labels: enhancement

                var newCondition = new ConditionFloat();
                newCondition.DeepCopyIn(condition, new Condition.TranslationMask(defaultOn: true)
                {
                    Unknown1 = false
                });

                if (MaybeOr(condition) == true)
                    newCondition.Flags = condition.Flags | Condition.Flag.OR;

                var newData = new FunctionConditionData
                {
                    Function = Condition.Function.HasKeyword,
                    ParameterOneRecord = vanillaRaceToActorProxyKeywords[data.ParameterOneRecord.Cast<IRaceGetter>()].AsSetter()
                };

                newData.DeepCopyIn(data, new FunctionConditionData.TranslationMask(defaultOn: true)
                {
                    Function = false,
                    ParameterOneRecord = false
                });

                if (data.Function is Condition.Function.GetPCIsRace)
                {
                    newData.RunOnType = Condition.RunOnType.Reference;
                    newData.Reference.SetTo(Constants.Player);
                }

                newCondition.Data = newData;

                response.Conditions.Insert(i, newCondition);
            }
        }

        public static bool? MaybeOr(IConditionFloatGetter condition) => (condition.CompareOperator, condition.ComparisonValue) switch
        {
            (CompareOperator.EqualTo, 0) => true,
            (CompareOperator.LessThanOrEqualTo, 0) => true,
            (CompareOperator.NotEqualTo, 1) => true,
            (CompareOperator.LessThan, 1) => true,
            (CompareOperator.EqualTo, 1) => false,
            (CompareOperator.GreaterThanOrEqualTo, 1) => false,
            (CompareOperator.NotEqualTo, 0) => false,
            (CompareOperator.GreaterThan, 0) => false,
            (_, _) => null
        };

        public static bool IsBoolean(IConditionFloatGetter condition) => Enum.IsDefined(condition.CompareOperator) && (condition.ComparisonValue) switch { 0 or 1 => true, _ => false };

        public static bool IsConditionOnPlayerRace(IFunctionConditionDataGetter x) => functionsOfInterest.Contains(x.Function)
                && vanillaRaceToActorProxyKeywords.ContainsKey(x.ParameterOneRecord.Cast<IRaceGetter>());

        public static bool IsConditionOnPlayerRaceProxyKeyword(IFunctionConditionDataGetter x) => x.Function == Condition.Function.HasKeyword
                && actorProxyKeywords.Contains(x.ParameterOneRecord);

        public static bool IsVictim(IDialogResponsesGetter x)
        {
            bool ok = false;
            foreach (var data in x.Conditions
                .OfType<IConditionFloatGetter>()
                .Where(x => IsBoolean(x))
                .Select(x => x.Data)
                .OfType<IFunctionConditionDataGetter>())
            {
                if (!ok && IsConditionOnPlayerRace(data))
                    ok = true;
                if (IsConditionOnPlayerRaceProxyKeyword(data))
                    return false;
            }
            return ok;
        }

    }
}
