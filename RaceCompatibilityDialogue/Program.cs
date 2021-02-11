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
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .Run(args, new RunPreferences()
                {
                    ActionsForEmptyArgs = new RunDefaultPatcher()
                    {
                        IdentifyingModKey = "RaceCompatibilityDialogue.esp",
                        TargetRelease = GameRelease.SkyrimSE,
                    }
                });
        }

        public static readonly ModKey raceCompatibilityEsm = new ModKey("RaceCompatibility", ModType.Master);

        public static readonly Dictionary<FormKey, FormKey> vanillaRaceToActorProxyKeywords = new Dictionary<FormKey, FormKey>{
            { Skyrim.Race.ArgonianRace, new FormKey(raceCompatibilityEsm, 0x001D8B) }, // Argonian
            { Skyrim.Race.BretonRace, new FormKey(raceCompatibilityEsm, 0x001D8A) }, // Breton
            { Skyrim.Race.DarkElfRace, new FormKey(raceCompatibilityEsm, 0x001D8F) }, // DarkElf
            { Skyrim.Race.HighElfRace, new FormKey(raceCompatibilityEsm, 0x001D8E) }, // HighElf
            { Skyrim.Race.ImperialRace, new FormKey(raceCompatibilityEsm, 0x001D90) }, // Imperial
            { Skyrim.Race.KhajiitRace, new FormKey(raceCompatibilityEsm, 0x001D8C) }, // Khajit
            { Skyrim.Race.NordRace, new FormKey(raceCompatibilityEsm, 0x001D93) }, // Nord
            { Skyrim.Race.OrcRace, new FormKey(raceCompatibilityEsm, 0x001D8D) }, // Orc
            { Skyrim.Race.RedguardRace, new FormKey(raceCompatibilityEsm, 0x001D91) }, // Redguard
            { Skyrim.Race.WoodElfRace, new FormKey(raceCompatibilityEsm, 0x001D92) }, // WoodElf
        };

        public static readonly HashSet<FormKey> actorProxyKeywords = new HashSet<FormKey>(vanillaRaceToActorProxyKeywords.Values);

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            bool isConditionOnPlayerRace(IFunctionConditionDataGetter x) {
                return x.Function == (int)ConditionData.Function.GetIsRace
                        && vanillaRaceToActorProxyKeywords.ContainsKey(x.ParameterOneRecord.FormKey);
            }

            bool isConditionOnPlayerRaceProxyKeyword(IFunctionConditionDataGetter x) {
                return x.Function == (int)ConditionData.Function.HasKeyword
                        && actorProxyKeywords.Contains(x.ParameterOneRecord.FormKey);
            }

            bool isVictim(IDialogResponsesGetter x) {
                bool ok = false;
                foreach (var data in x.Conditions
                    .OfType<IConditionFloatGetter>()
                    .Select(x => x.Data)
                    .OfType<IFunctionConditionDataGetter>()) {
                    if (!ok && isConditionOnPlayerRace(data))
                        ok = true;
                    if (isConditionOnPlayerRaceProxyKeyword(data))
                        return false;
                }
                return ok;
            }

            foreach (var item in state.LoadOrder.PriorityOrder.DialogResponses().WinningContextOverrides(state.LinkCache))
            {
                if (!isVictim(item.Record)) continue;

                var response = item.GetOrAddAsOverride(state.PatchMod);

                for (var i = response.Conditions.Count - 1; i >= 0; i--)
                {
                    if (!(response.Conditions[i] is ConditionFloat)) continue;
                    ConditionFloat condition = (ConditionFloat)response.Conditions[i];

                    if (!(condition.Data is FunctionConditionData)) continue;
                    FunctionConditionData data = (FunctionConditionData)condition.Data;

                    if (!isConditionOnPlayerRace(data)) continue;

                    var newCondition = new ConditionFloat();
                    newCondition.DeepCopyIn(condition, new Condition.TranslationMask(defaultOn: true){
                        Unknown1 = false
                    });

                    condition.Flags |= Condition.Flag.OR;

                    var newData = new FunctionConditionData();

                    newData.Function = (int)ConditionData.Function.HasKeyword;
                    newData.ParameterOneRecord = vanillaRaceToActorProxyKeywords[data.ParameterOneRecord.FormKey];

                    newData.DeepCopyIn(data, new FunctionConditionData.TranslationMask(defaultOn: true){
                        Function = false,
                        ParameterOneRecord = false
                    });

                    newCondition.Data = newData;

                    response.Conditions.Insert(i, newCondition);
                }
            }
        }
    }
}
