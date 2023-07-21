using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog;
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

        protected readonly ILoadOrder<IModListing<ISkyrimModGetter>> LoadOrder;
        protected readonly ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache;
        protected readonly ISkyrimMod PatchMod;

        public Program(ILoadOrder<IModListing<ISkyrimModGetter>> loadOrder, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, ISkyrimMod patchMod)
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

                if (item.Parent?.Record is IDialogTopicGetter getter) dialogueSet.Add(getter.ToLink());

                AdjustResponses(response);
            }

            int dialogueCounter = dialogueSet.Count;

            Console.WriteLine($"Modified {responseCounter} responses to {dialogueCounter} dialogue topics.");
        }

        public static void AdjustResponses(IDialogResponses response)
        {
            var andList = GroupConditions(response.Conditions);

            AdjustConditions(andList);

            response.Conditions.Clear();
            foreach (var orList in andList)
                response.Conditions.AddRange(orList);
        }

        public static List<List<Condition>> GroupConditions(IList<Condition> conditions)
        {
            List<List<Condition>> andList = new();
            List<Condition> orList = new();

            foreach (var condition in conditions)
            {
                orList.Add(condition);
                if (!condition.Flags.HasFlag(Condition.Flag.OR))
                {
                    andList.Add(orList);
                    orList = new();
                }
            }

            if (orList.Count > 0)
                andList.Add(orList);

            return andList;
        }

        public static void AdjustConditions(List<List<Condition>> andList)
        {
            foreach (var orList in andList)
            {
                List<Condition>? newConditions = null;

                foreach (var item in orList)
                {
                    if (item is not ConditionFloat condition) continue;
                    if (!IsBoolean(condition)) continue;

                    IFormLinkNullableGetter<IRaceGetter> targetRace;
                    IFormLinkGetter<ISkyrimMajorRecordGetter> character;

                    switch (condition.Data)
                    {
                        case GetPCIsRaceConditionData pcIsRaceConditionData:
                            targetRace = pcIsRaceConditionData.Race.Link;
                            character = Constants.Player;
                            break;
                        case GetIsRaceConditionData isRaceConditionData:
                            targetRace = isRaceConditionData.Race.Link;
                            character = isRaceConditionData.Reference;
                            break;
                        default:
                            continue;
                    }

                    if (!vanillaRaceToActorProxyKeywords.TryGetValue(targetRace, out var targetRaceKeyword))
                        continue;

                    (newConditions ??= new()).Add(MakeNewCondition(condition, character, targetRaceKeyword));
                }

                if (newConditions != null)
                    foreach (var newCondition in newConditions)
                    {
                        newCondition.Flags = newCondition.Flags.SetFlag(Condition.Flag.OR, false);
                        orList[^1].Flags = orList[^1].Flags.SetFlag(Condition.Flag.OR, true);
                        orList.Add(newCondition);
                    }
            }
        }

        public static readonly ConditionFloat.TranslationMask newConditionCopyMask = new(true)
        {
            Data = false
        };

        private static ConditionFloat MakeNewCondition(IConditionFloatGetter condition, IFormLinkGetter<ISkyrimMajorRecordGetter> character, IFormLinkGetter<IKeywordGetter> targetRaceKeyword)
        {
            var newCondition = condition.DeepCopy(newConditionCopyMask);

            var newData = new HasKeywordConditionData();
            newData.Keyword.Link.SetTo(targetRaceKeyword);
            newData.Reference.SetTo(character);
            newData.RunOnType = condition.Data.RunOnType;

            newCondition.Data = newData;

            return newCondition;
        }

        public static bool IsBoolean(IConditionFloatGetter condition) => Enum.IsDefined(condition.CompareOperator) && (condition.ComparisonValue) switch { 0 or 1 => true, _ => false };

        public static bool IsConditionOnPlayerRaceProxyKeyword(IHasKeywordConditionDataGetter x) => actorProxyKeywords.Contains(x.Keyword.Link);

        public static bool IsVictim(IDialogResponsesGetter x)
        {
            bool ok = false;
            foreach (var data in x.Conditions
                .OfType<IConditionFloatGetter>()
                .Where(x => IsBoolean(x))
                .Select(x => x.Data))
            {
                if (!ok)
                {
                    var targetRace = data switch
                    {
                        IGetIsRaceConditionData a => a.Race.Link,
                        IGetPCIsRaceConditionData b => b.Race.Link,
                        _ => null
                    };
                    if (targetRace is not null)
                        if (vanillaRaceToActorProxyKeywords.ContainsKey(targetRace))
                            ok = true;
                }
                if (data is IHasKeywordConditionData bar)
                    if (IsConditionOnPlayerRaceProxyKeyword(bar))
                        return false;
            }
            return ok;
        }

    }
}
