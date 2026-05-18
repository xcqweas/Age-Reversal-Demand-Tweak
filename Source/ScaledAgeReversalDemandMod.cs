using System.Collections;
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
            if (!AgeReversalDemandUtility.ShouldShowDemandForPawn(p))
            {
                // Stage 3 is the invisible dummy stage in AgeReversalDemanded.
                __result = ThoughtState.ActiveAtStage(3);
            }
        }
    }

    [HarmonyPatch(typeof(ThoughtWorker_AgeReversalDemanded), "CanHaveThought")]
    public static class ThoughtWorker_AgeReversalDemanded_CanHaveThought_Patch
    {
        public static void Postfix(ref bool __result, Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (!AgeReversalDemandUtility.ShouldShowDemandForPawn(pawn))
            {
                __result = false;
                return;
            }

            if (__result)
            {
                return;
            }

            // Vanilla keeps this false below 25 years; re-enable it once our scaled threshold is reached.
            if (pawn.Ideo != null && pawn.Ideo.HasPrecept(PreceptDefOf.AgeReversal_Demanded))
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(Alert_AgeReversalDemandNear), "CalcPawnsNearDeadline")]
    public static class Alert_AgeReversalDemandNear_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Alert_AgeReversalDemandNear __instance)
        {
            FieldRefAccess(__instance)?.RemoveNonDemandingPawns();
        }

        private static IList FieldRefAccess(Alert_AgeReversalDemandNear instance)
        {
            var targetsField = AccessTools.Field(typeof(Alert_AgeReversalDemandNear), "targets");

            if (targetsField == null)
            {
                return null;
            }

            return targetsField.GetValue(instance) as IList;
        }

        private static void RemoveNonDemandingPawns(this IList targets)
        {
            if (targets == null)
            {
                return;
            }

            int before = targets.Count;

            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i] is Pawn pawn && !AgeReversalDemandUtility.ShouldShowDemandForPawn(pawn))
                {
                    targets.RemoveAt(i);
                }
            }

            AgeReversalDemandDebug.LogCalcTargetsFiltered(before, targets.Count);
        }
    }

    [HarmonyPatch(typeof(Alert_AgeReversalDemandNear), "GetReport")]
    public static class Alert_AgeReversalDemandNear_GetReport_Patch
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref AlertReport __result)
        {
            if (!__result.active)
            {
                AgeReversalDemandDebug.LogGetReportInactive();
                return;
            }

            int beforePawns = __result.culpritsPawns?.Count ?? 0;
            int beforeThings = __result.culpritsThings?.Count ?? 0;
            int beforeTargets = __result.culpritsTargets?.Count ?? 0;
            bool beforeSingleTarget = __result.culpritTarget.HasValue;

            __result.culpritsPawns?.RemoveAll(pawn => !AgeReversalDemandUtility.ShouldShowDemandForPawn(pawn));
            __result.culpritsThings?.RemoveAll(thing => thing is Pawn pawn && !AgeReversalDemandUtility.ShouldShowDemandForPawn(pawn));
            __result.culpritsTargets?.RemoveAll(target => target.Thing is Pawn pawn && !AgeReversalDemandUtility.ShouldShowDemandForPawn(pawn));

            if (__result.culpritTarget.HasValue
                && __result.culpritTarget.Value.Thing is Pawn targetPawn
                && !AgeReversalDemandUtility.ShouldShowDemandForPawn(targetPawn))
            {
                __result.culpritTarget = null;
            }

            bool hasAnyCulprits = (__result.culpritsPawns?.Count ?? 0) > 0
                || (__result.culpritsThings?.Count ?? 0) > 0
                || (__result.culpritsTargets?.Count ?? 0) > 0
                || __result.culpritTarget.HasValue;

            AgeReversalDemandDebug.LogGetReportFiltered(
                beforePawns,
                __result.culpritsPawns?.Count ?? 0,
                beforeThings,
                __result.culpritsThings?.Count ?? 0,
                beforeTargets,
                __result.culpritsTargets?.Count ?? 0,
                beforeSingleTarget,
                __result.culpritTarget.HasValue,
                hasAnyCulprits);

            if (!hasAnyCulprits)
            {
                __result = AlertReport.Inactive;
            }
        }
    }

    public static class AgeReversalDemandDebug
    {
        private static int calcLogs;
        private static int reportLogs;
        private const int MaxDebugLogs = 15;

        public static void LogCalcTargetsFiltered(int before, int after)
        {
            if (calcLogs >= MaxDebugLogs)
            {
                return;
            }

            calcLogs++;
            Log.Message($"[Age Reversal Demand Tweak][Debug] CalcPawnsNearDeadline targets: before={before}, after={after}");
        }

        public static void LogGetReportInactive()
        {
            if (reportLogs >= MaxDebugLogs)
            {
                return;
            }

            reportLogs++;
            Log.Message("[Age Reversal Demand Tweak][Debug] GetReport returned inactive before filtering.");
        }

        public static void LogGetReportFiltered(
            int beforePawns,
            int afterPawns,
            int beforeThings,
            int afterThings,
            int beforeTargets,
            int afterTargets,
            bool beforeSingleTarget,
            bool afterSingleTarget,
            bool hasAnyCulprits)
        {
            if (reportLogs >= MaxDebugLogs)
            {
                return;
            }

            reportLogs++;
            Log.Message(
                "[Age Reversal Demand Tweak][Debug] GetReport filtered culprits: "
                + $"pawns {beforePawns}->{afterPawns}, "
                + $"things {beforeThings}->{afterThings}, "
                + $"targets {beforeTargets}->{afterTargets}, "
                + $"singleTarget {beforeSingleTarget}->{afterSingleTarget}, "
                + $"hasAny={hasAnyCulprits}");
        }
    }

    public static class AgeReversalDemandUtility
    {
        public static bool ShouldShowDemandForPawn(Pawn p)
        {
            if (p == null)
            {
                return false;
            }

            if (HasAgelessLikeGene(p))
            {
                return false;
            }

            return p.ageTracker.AgeBiologicalYearsFloat >= GetScaledDemandAge(p);
        }

        public static float GetScaledDemandAge(Pawn p)
        {
            float lifeExpectancy = p.RaceProps?.lifeExpectancy ?? 80f;

            if (lifeExpectancy <= 0f)
            {
                lifeExpectancy = 80f;
            }

            // Vanilla human: 80 year life expectancy -> demand starts at 25.
            return lifeExpectancy * 25f / 80f;
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