using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ResearchBenchETACompactFix
{
    [StaticConstructorOnStartup]
    public static class ResearchBenchETACompactFixStartup
    {
        static ResearchBenchETACompactFixStartup()
        {
            new Harmony("vorsel.storedresearch.inlineeta").PatchAll();
        }
    }

    // Patch the final renderer instead of Building_ResearchBench.GetInspectString.
    // This runs after every mod has finished composing the inspect string and is
    // therefore independent of postfix registration and execution order.
    [HarmonyPatch(typeof(InspectPaneFiller), "DrawInspectString")]
    public static class InspectPaneFillerDrawInspectStringPatch
    {
        // Compatibility-only marker for the standalone ETA row produced by the
        // older companion mod; this is not the label used for our inline UI.
        private const string LegacyStandaloneEtaPrefix = "<b>ETA</b>:";

        [HarmonyPrefix]
        public static void Prefix(ref string __0)
        {
            if (string.IsNullOrEmpty(__0))
            {
                return;
            }

            string progressPrefix = "ResearchProgress".Translate().ToString() + ":";
            string[] sourceLines = __0.Split('\n');
            int preferredProgressIndex = -1;

            for (int i = 0; i < sourceLines.Length; i++)
            {
                if (sourceLines[i].StartsWith(progressPrefix, StringComparison.Ordinal))
                {
                    preferredProgressIndex = i;
                }
            }

            if (preferredProgressIndex < 0)
            {
                return;
            }

            string eta = ResearchEtaCalculator.GetFormattedEta();
            var resultLines = new List<string>(sourceLines.Length);
            string etaLabel = "StoredResearchInlineETA.EtaLabel".Translate().ToString();

            for (int i = 0; i < sourceLines.Length; i++)
            {
                string line = sourceLines[i];

                if (line.StartsWith(LegacyStandaloneEtaPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith(progressPrefix, StringComparison.Ordinal))
                {
                    // Stored Research's rewritten row is the final progress row.
                    // Remove every earlier vanilla or duplicate progress row.
                    if (i != preferredProgressIndex)
                    {
                        continue;
                    }

                    if (!string.IsNullOrEmpty(eta))
                    {
                        line += "  " + etaLabel + ": " + eta;
                    }
                }

                resultLines.Add(line);
            }

            __0 = string.Join("\n", resultLines);
        }
    }

    internal static class ResearchEtaCalculator
    {
        internal const int CacheDurationTicks = 30;

        // RimWorld 1.6 ResearchManager.ResearchPerformed applies this factor to
        // the raw work amount supplied by JobDriver_Research.
        private const double VanillaResearchPointsPerWorkTick = 0.00825d;

        private static ResearchProjectDef cachedProject;
        private static int cachedAtTick;
        private static bool cacheInitialized;
        private static string cachedEta;

        internal static string GetFormattedEta()
        {
            ResearchManager manager = Find.ResearchManager;
            ResearchProjectDef project = manager?.GetProject(null);
            if (project == null)
            {
                ClearCache();
                return null;
            }

            TickManager tickManager = Find.TickManager;
            int now = tickManager != null ? tickManager.TicksGame : 0;
            int elapsed = now - cachedAtTick;
            if (cacheInitialized && ReferenceEquals(project, cachedProject) &&
                elapsed >= 0 && elapsed < CacheDurationTicks)
            {
                return cachedEta;
            }

            cachedProject = project;
            cachedAtTick = now;
            cacheInitialized = true;
            cachedEta = CalculateUncached(project);
            return cachedEta;
        }

        private static string CalculateUncached(ResearchProjectDef project)
        {
            double progressPerTick = MeasureActiveResearchProgressPerTick(project);
            if (!IsFinite(progressPerTick) || progressPerTick <= 0d)
            {
                return null;
            }

            double remaining;
            if (!TryGetRemainingCostInCurrentUnits(project, out remaining))
            {
                return null;
            }

            double exactTicks = Math.Max(0d, remaining) / progressPerTick;
            if (!IsFinite(exactTicks) || exactTicks < 0d)
            {
                return null;
            }

            int ticks = exactTicks >= int.MaxValue ? int.MaxValue : (int)exactTicks;
            return GenDate.ToStringTicksToPeriod(
                ticks,
                true,
                false,
                true,
                true,
                false);
        }

        private static bool TryGetRemainingCostInCurrentUnits(
            ResearchProjectDef project,
            out double remaining)
        {
            remaining = 0d;

            double costAfterStoredPoints;
            if (!StoredResearchIntegration.TryGetCostAfterStoredPoints(
                project,
                out costAfterStoredPoints))
            {
                double cost = project.Cost;
                double progress = project.ProgressReal;
                if (!IsFinite(cost) || !IsFinite(progress))
                {
                    return false;
                }

                remaining = Math.Max(0d, cost - progress);
                return true;
            }

            double apparentCost = project.CostApparent;
            double apparentProgress = project.ProgressApparent;
            double projectCost = project.Cost;

            if (!IsFinite(apparentCost) || !IsFinite(apparentProgress) ||
                !IsFinite(costAfterStoredPoints) || !IsFinite(projectCost))
            {
                return false;
            }

            double apparentRemaining =
                Math.Max(0d, costAfterStoredPoints - apparentProgress);
            if (apparentRemaining == 0d)
            {
                remaining = 0d;
                return true;
            }

            if (apparentCost <= 0d || projectCost < 0d)
            {
                return false;
            }

            // Convert Stored Research's apparent-cost units back to the real
            // progress units updated by ResearchManager.ResearchPerformed.
            remaining = apparentRemaining * projectCost / apparentCost;
            return IsFinite(remaining);
        }

        private static double MeasureActiveResearchProgressPerTick(
            ResearchProjectDef project)
        {
            double total = 0d;
            List<Map> maps = Find.Maps;
            if (maps == null)
            {
                return total;
            }

            for (int mapIndex = 0; mapIndex < maps.Count; mapIndex++)
            {
                Map map = maps[mapIndex];
                if (map == null || map.listerThings == null || map.thingGrid == null)
                {
                    continue;
                }

                List<Thing> researchBenches =
                    map.listerThings.ThingsInGroup(ThingRequestGroup.ResearchBench);
                if (researchBenches == null)
                {
                    continue;
                }

                for (int benchIndex = 0; benchIndex < researchBenches.Count; benchIndex++)
                {
                    Building_ResearchBench bench =
                        researchBenches[benchIndex] as Building_ResearchBench;
                    if (bench == null || !bench.Spawned || bench.Map != map)
                    {
                        continue;
                    }

                    IntVec3 interactionCell = bench.InteractionCell;
                    List<Thing> thingsAtInteractionCell =
                        map.thingGrid.ThingsListAtFast(interactionCell);
                    for (int thingIndex = 0;
                        thingIndex < thingsAtInteractionCell.Count;
                        thingIndex++)
                    {
                        Pawn pawn = thingsAtInteractionCell[thingIndex] as Pawn;
                        if (pawn == null || !pawn.Spawned ||
                            pawn.CurJobDef != JobDefOf.Research ||
                            pawn.CurJob == null ||
                            pawn.CurJob.targetA.Thing != bench ||
                            pawn.Position != interactionCell)
                        {
                            continue;
                        }

                        // RimWorld 1.6 JobDriver_Research forms raw work from these stats.
                        double pawnSpeed = pawn.GetStatValue(StatDefOf.ResearchSpeed);
                        double benchFactor =
                            bench.GetStatValue(StatDefOf.ResearchSpeedFactor);
                        double contribution = pawnSpeed * benchFactor;
                        if (!IsFinite(contribution))
                        {
                            continue;
                        }

                        // RimWorld 1.6 ResearchManager.ResearchPerformed divides each
                        // researcher's contribution by their faction's tech cost factor.
                        Faction faction = pawn.Faction;
                        if (faction != null)
                        {
                            double techCostFactor =
                                project.CostFactor(faction.def.techLevel);
                            if (!IsFinite(techCostFactor) || techCostFactor <= 0d)
                            {
                                continue;
                            }

                            contribution /= techCostFactor;
                        }

                        total += contribution;
                    }
                }
            }

            Storyteller storyteller = Find.Storyteller;
            if (storyteller == null || storyteller.difficulty == null)
            {
                return 0d;
            }

            // RimWorld 1.6 ResearchManager.ResearchPerformed applies both the
            // vanilla per-work-tick factor and the storyteller difficulty factor.
            double difficultyFactor = storyteller.difficulty.researchSpeedFactor;
            if (!IsFinite(difficultyFactor))
            {
                return 0d;
            }

            return total * VanillaResearchPointsPerWorkTick * difficultyFactor;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void ClearCache()
        {
            cachedProject = null;
            cachedAtTick = 0;
            cacheInitialized = false;
            cachedEta = null;
        }
    }

    internal static class StoredResearchIntegration
    {
        private const string PackageId = "Leoltron.StoredResearch";
        private const string AssemblyName = "Leoltron.StoredResearch";
        private const string ComponentTypeName =
            "Leoltron.StoredResearch.StoredResearchWorldComponent";

        private static bool resolutionAttempted;
        private static MethodInfo findComponentMethod;
        private static MethodInfo remainingCostMethod;

        internal static bool TryGetCostAfterStoredPoints(
            ResearchProjectDef project,
            out double cost)
        {
            cost = 0d;
            if (project == null || !ModsConfig.IsActive(PackageId) || !TryResolve())
            {
                return false;
            }

            try
            {
                object component = findComponentMethod.Invoke(null, null);
                if (component == null)
                {
                    return false;
                }

                object value = remainingCostMethod.Invoke(
                    component,
                    new object[] { project });
                if (value == null)
                {
                    return false;
                }

                cost = Convert.ToDouble(value);
                return !double.IsNaN(cost) && !double.IsInfinity(cost);
            }
            catch (Exception)
            {
                // Optional integration fails closed to vanilla ETA behavior.
                return false;
            }
        }

        private static bool TryResolve()
        {
            if (resolutionAttempted)
            {
                return findComponentMethod != null && remainingCostMethod != null;
            }

            resolutionAttempted = true;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            Assembly storedResearchAssembly = null;
            for (int i = 0; i < assemblies.Length; i++)
            {
                Assembly assembly = assemblies[i];
                if (string.Equals(
                    assembly.GetName().Name,
                    AssemblyName,
                    StringComparison.Ordinal))
                {
                    storedResearchAssembly = assembly;
                    break;
                }
            }

            Type componentType = storedResearchAssembly?.GetType(
                ComponentTypeName,
                false,
                false);
            if (componentType == null)
            {
                return false;
            }

            findComponentMethod = componentType.GetMethod(
                "Find",
                BindingFlags.Public | BindingFlags.Static,
                null,
                Type.EmptyTypes,
                null);
            remainingCostMethod = componentType.GetMethod(
                "ResearchCostAfterStoredPointsDeducted",
                BindingFlags.Public | BindingFlags.Instance,
                null,
                new[] { typeof(ResearchProjectDef) },
                null);

            return findComponentMethod != null && remainingCostMethod != null;
        }
    }
}
