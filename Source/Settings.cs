﻿using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FactionControl
{
    public class Controller : Mod
    {
        public Controller(ModContentPack content) : base(content)
        {
            base.GetSettings<Settings>();
        }

        public override string SettingsCategory()
        {
            return "RFC.FactionControlName".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.GetSettings<Settings>().DoWindowContents(inRect);
        }
    }

    public class Settings : ModSettings
    {
        const float DEFAULT_MIN_POP = 75f;
        const float DEFAULT_MAX_POP = 85f;

        public float DensityMin = DEFAULT_MIN_POP;
        public float DensityMax = DEFAULT_MAX_POP;
        public static bool DisableFactionLimit = true;
        public static List<FactionDensity> FactionDensities = new List<FactionDensity>();

        private string minBuffer, maxBuffer;
        private bool initialized = false;
        private Vector2 scroll = Vector2.zero;
        private float lastY = 0;

        public void DoWindowContents(Rect rect)
        {
            string sparce = "PlanetPopulation_Low".Translate();
            string dense = "PlanetPopulation_High".Translate();

            if (minBuffer == null || minBuffer == "")
                UpdateBuffers();

            Initialize();

            float y = rect.y + 10f;
            float half = rect.width * 0.5f;
            float width = half - 10f;
            float inner = width - 16f;

            Widgets.Label(new Rect(rect.x, y, 200, 28), "RFC.DisableFactionLimits".Translate());
            Widgets.Checkbox(new Vector2(rect.x + 210, y - 2), ref DisableFactionLimit);
            y += 32;
            Widgets.Label(new Rect(rect.x, y, width, 28), "RFC.PopulationOverride".Translate());
            y += 30;
            float f = DensityMin;
            DrawValueInput(rect.x + 10, ref y, "min".Translate().CapitalizeFirst(), ref DensityMin, ref minBuffer, 0.01f, 250f, DEFAULT_MIN_POP);
            if (f != DensityMin && DensityMin > DensityMax)
            {
                DensityMax = DensityMin;
                maxBuffer = DensityMax.ToString("0.00");
            }
            f = DensityMax;
            DrawValueInput(rect.x + 10, ref y, "max".Translate().CapitalizeFirst(), ref DensityMax, ref maxBuffer, 0.01f, 250f, DEFAULT_MAX_POP);
            if (f != DensityMax && DensityMax < DensityMin)
            {
                DensityMin = DensityMax;
                minBuffer = DensityMin.ToString("0.00");
            }

            y = rect.y + 10f;
            Widgets.Label(new Rect(half, y, width, 28), "RFC.FactionDensity".Translate());
            y += 30;
            Widgets.BeginScrollView(new Rect(half, y, width, rect.height - 40), ref scroll, new Rect(0, 0, inner, lastY));
            lastY = 0;
            int i = 0;
            foreach (var fd in FactionDensities)
            {
                Widgets.Label(new Rect(0, lastY, 150, 28), fd.Faction.LabelCap);
                if (Widgets.ButtonText(new Rect(160, lastY, 75, 28), fd.Enabled ? "On".Translate() : "Off".Translate()))
                    fd.Enabled = !fd.Enabled;

                if (fd.Enabled)
                {
                    lastY += 40;
                    fd.Density = Widgets.HorizontalSlider(new Rect(0, lastY, inner, 28), fd.Density, 15f, 400f, true, ((int)fd.Density).ToString(), dense, sparce);
                }
                lastY += 30;
                ++i;
                if (i < FactionDensities.Count)
                {
                    Widgets.DrawLineHorizontal(0, lastY, inner);
                    lastY += 10;
                }
            }
            Widgets.EndScrollView();
        }

        private void DrawValueInput(float x, ref float y, string label, ref float pop, ref string buffer, float min, float max, float d)
        {
            Widgets.Label(new Rect(x, y, 50, 28), label);
            Widgets.TextFieldNumeric(new Rect(x + 60, y, 100, 28), ref pop, ref buffer, min, max);
            if (Widgets.ButtonText(new Rect(x + 170, y, 100, 28), "RFC.Default".Translate()))
            {
                pop = d;
                buffer = pop.ToString("0.00");
            }
            y += 45;
            var f = Widgets.HorizontalSlider(new Rect(x, y, 270, 28), pop, min, max, false, null, "PlanetPopulation_Low".Translate(), "PlanetPopulation_High".Translate());
            if (Math.Abs(pop - f) > 0.1)
            {
                pop = f;
                buffer = pop.ToString("0.00");
            }
            y += 40;
        }

        private void Initialize()
        {
            if (!initialized || FactionDensities == null)
            {
                if (FactionDensities == null)
                    FactionDensities = new List<FactionDensity>();

                foreach (var f in DefDatabase<FactionDef>.AllDefsListForReading)
                {
                    if (!f.isPlayer && !f.hidden)
                    {
                        if (FactionDensities.Find(fd => fd.FactionDefName == f.defName) == null)
                        {
                            FactionDensities.Add(new FactionDensity()
                            {
                                FactionDefName = f.defName,
                                Density = 400,
                                Enabled = false
                            });
                        }
                    }
                }
                for (int i = FactionDensities.Count - 1; i >= 0; --i)
                {
                    var fd = FactionDensities[i];
                    var f = DefDatabase<FactionDef>.GetNamed(fd.FactionDefName, false);
                    if (f == null)
                        FactionDensities.RemoveAt(i);
                    else
                        fd.Faction = f;
                }
                FactionDensities.Sort((FactionDensity a, FactionDensity b) => a.FactionDefName.CompareTo(b.FactionDefName));
                initialized = true;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref DensityMin, "densityMin", DEFAULT_MIN_POP);
            Scribe_Values.Look(ref DensityMax, "densityMax", DEFAULT_MAX_POP);
            Scribe_Values.Look(ref DisableFactionLimit, "disableFactionLimit", true);
            Scribe_Collections.Look(ref FactionDensities, "factionDensities", LookMode.Deep, new object[0]);
            UpdateSettlementsPer100k();
            UpdateBuffers();
        }

        public void UpdateSettlementsPer100k()
        {
            typeof(FactionGenerator).GetField("SettlementsPer100kTiles", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, new FloatRange(DensityMin, DensityMax));
        }

        public void UpdateBuffers()
        {
            minBuffer = DensityMin.ToString("0.0");
            maxBuffer = DensityMax.ToString("0.0");
        }
    }

    public class MinMax : IExposable
    {
        public int Min, Max;
        public MinMax() { }
        public void ExposeData()
        {
            Scribe_Values.Look(ref Min, "min");
            Scribe_Values.Look(ref Max, "max");
        }
    }

    public class FactionDensity : IExposable
    {
        public FactionDef Faction;
        public string FactionDefName;
        public float Density;
        public bool Enabled;

        public FactionDensity() { }
        public void ExposeData()
        {
            Scribe_Values.Look(ref FactionDefName, "faction");
            Scribe_Values.Look(ref Density, "density", 400);
            Scribe_Values.Look(ref Enabled, "enabled", false);
        }
    }
}