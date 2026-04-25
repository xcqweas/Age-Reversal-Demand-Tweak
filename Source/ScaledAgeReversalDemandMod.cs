using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ScaledAgeReversalDemand
{
    [StaticConstructorOnStartup]
    public static class ScaledAgeReversalDemandMod
    {
        static ScaledAgeReversalDemandMod()
        {
            new Harmony("xcqweas.AgeReversalDemandTweak").PatchAll();
            Log.Message("[Age Reversal Demand Tweak] Loaded.");
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker_AgeReversalDemanded), "ShouldHaveThought")]
    public static class ThoughtWorker_AgeReversalDemanded_Patch
    {
        public static void Postfix(ref ThoughtState __result, Pawn p)
        {
            if (p == null)
            {
                return;
            }

            if (HasAgelessLikeGene(p))
            {
                __result = ThoughtState.Inactive;
                return;
            }

            float lifeExpectancy = p.RaceProps?.lifeExpectancy ?? 80f;

            if (lifeExpectancy <= 0f)
            {
                lifeExpectancy = 80f;
            }

            // Vanilla human: 80 year life expectancy -> demand starts at 25.
            float scaledDemandAge = lifeExpectancy * 25f / 80f;

            if (p.ageTracker.AgeBiologicalYearsFloat < scaledDemandAge)
            {
                // Stage 3 is the invisible dummy stage in AgeReversalDemanded.
                __result = ThoughtState.ActiveAtStage(3);
            }
        }

        private static bool HasAgelessLikeGene(Pawn p)
        {
            if (p.genes == null)
            {
                return false;
            }

            return p.genes.GenesListForReading.Any(g =>
            {
                string defName = g.def.defName.ToLowerInvariant();

                return defName.Contains("ageless")
                    || defName.Contains("non_senescent")
                    || defName.Contains("nonsenescent")
                    || defName.Contains("unaging")
                    || defName.Contains("immortal");
            });
        }
    }
}