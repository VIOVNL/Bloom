using System;
using System.Collections.Generic;
using Bloom.Models;
using Bloom.Views;

namespace Bloom.Services;

internal static class PetalLayoutEngine
{
    internal static double ScaleFactor { get; set; } = 1.0;

    internal static double ButtonSize => 52 * ScaleFactor;
    internal static double PetalSize => 42 * ScaleFactor;
    internal static double RepelRadius => 60 * ScaleFactor;
    internal static double RepelStrength => 6 * ScaleFactor;
    internal static double IconSize => 20 * ScaleFactor;
    internal static double FlowerIconSize => 36 * ScaleFactor;

    internal static double GetScaleFactor(AppScale scale) => scale switch
    {
        AppScale.Small => 0.85,
        AppScale.Medium => 1.0,
        AppScale.Large => 1.25,
        AppScale.ExtraLarge => 1.5,
        _ => 1.0
    };

    // ────────────────────────────────────────────────────
    // Capacity-based layout
    // ────────────────────────────────────────────────────

    // Minimum center-to-center distance between petals (allows ~15% overlap)
    private static double MinSpacing => PetalSize * 0.85;
    private static double BaseRadius => ButtonSize / 2.0 + PetalSize * 0.3;
    private static double LayerGap => PetalSize * 0.65;

    /// <summary>
    /// Compute capacity-based layer distribution.
    /// Each layer only holds as many petals as geometrically fit at that radius and spread angle.
    /// </summary>
    internal static (int[] counts, double[] radii) ComputeLayout(int totalItems, double spreadDeg)
    {
        if (totalItems <= 0)
            return (new[] { 1 }, new[] { BaseRadius });

        double minSp = MinSpacing;
        double spreadRad = spreadDeg * Math.PI / 180.0;
        double baseR = BaseRadius;
        double gap = LayerGap;

        var counts = new List<int>();
        var radii = new List<double>();
        int remaining = totalItems;
        int layer = 0;

        while (remaining > 0 && layer <= 50)
        {
            double r = baseR + layer * gap;

            int capacity;
            if (spreadDeg >= 360)
                capacity = Math.Max(1, (int)Math.Floor(2 * Math.PI * r / minSp));
            else
                capacity = Math.Max(1, (int)Math.Floor(spreadRad * r / minSp) + 1);

            int assigned = Math.Min(remaining, capacity);
            counts.Add(assigned);
            radii.Add(r);
            remaining -= assigned;
            layer++;
        }

        return (counts.ToArray(), radii.ToArray());
    }

    /// <summary>
    /// Maximum layout radius across all spread angles (for canvas sizing).
    /// Worst case is the minimum spread (90°) which produces the most layers.
    /// </summary>
    internal static double ComputeMaxLayoutRadius(int totalItems, double spreadDeg = 360)
    {
        if (totalItems <= 0) totalItems = 1;
        return ComputeMaxRadiusForSpread(totalItems, spreadDeg);
    }

    /// <summary>
    /// Outermost layer radius for a given item count and spread.
    /// Lightweight – no list allocation.
    /// </summary>
    private static double ComputeMaxRadiusForSpread(int totalItems, double spreadDeg)
    {
        double minSp = MinSpacing;
        double spreadRad = spreadDeg * Math.PI / 180.0;
        double baseR = BaseRadius;
        double gap = LayerGap;

        int remaining = totalItems;
        double maxR = baseR;
        int layer = 0;

        while (remaining > 0 && layer <= 50)
        {
            double r = baseR + layer * gap;
            maxR = r;

            int capacity;
            if (spreadDeg >= 360)
                capacity = Math.Max(1, (int)Math.Floor(2 * Math.PI * r / minSp));
            else
                capacity = Math.Max(1, (int)Math.Floor(spreadRad * r / minSp) + 1);

            remaining -= Math.Min(remaining, capacity);
            layer++;
        }

        return maxR;
    }

    // ────────────────────────────────────────────────────
    // Position computation
    // ────────────────────────────────────────────────────

    internal static void LayoutPetals(BloomContext ctx, double biasAngleDeg, double spreadDeg)
    {
        int totalItems = ctx.PetalPositions.Count;
        if (totalItems == 0) return;

        var (counts, radii) = ComputeLayout(totalItems, spreadDeg);
        ctx.LayerCounts = counts;
        ctx.LayerRadii = radii;

        int totalLayers = counts.Length;
        int itemIndex = 0;

        for (int layer = 0; layer < totalLayers; layer++)
        {
            int count = counts[layer];
            double radius = radii[layer];

            double rotationOffset = 0;
            if (layer > 0 && spreadDeg >= 360)
            {
                double prevSpacing = 360.0 / counts[layer - 1];
                rotationOffset = prevSpacing / 2.0;
            }

            for (int i = 0; i < count; i++)
            {
                if (itemIndex >= ctx.PetalPositions.Count) return;

                double angleDeg;
                if (spreadDeg >= 360)
                    angleDeg = 360.0 * i / count + biasAngleDeg + rotationOffset;
                else if (count == 1)
                    angleDeg = biasAngleDeg;
                else
                {
                    double startAngle = biasAngleDeg - spreadDeg / 2.0;
                    angleDeg = startAngle + spreadDeg * i / (count - 1);
                }

                double angleRad = angleDeg * Math.PI / 180.0;
                double dx = radius * Math.Cos(angleRad);
                double dy = radius * Math.Sin(angleRad);

                ctx.PetalPositions[itemIndex] = (dx, dy);
                itemIndex++;
            }
        }
    }

    // ────────────────────────────────────────────────────
    // Edge awareness
    // ────────────────────────────────────────────────────

    internal static (double biasAngleDeg, double spreadDeg) ComputeEdgeAwareness(
        BloomContext ctx,
        (int X, int Y, int Width, int Height) screenBounds,
        double scaling,
        (int X, int Y) buttonCenter)
    {
        int totalItems = ctx.PetalPositions.Count;
        if (totalItems == 0)
            return (-90, 360);

        double spaceLeft = buttonCenter.X - screenBounds.X;
        double spaceRight = (screenBounds.X + screenBounds.Width) - buttonCenter.X;
        double spaceUp = buttonCenter.Y - screenBounds.Y;
        double spaceDown = (screenBounds.Y + screenBounds.Height) - buttonCenter.Y;

        double petalR = PetalSize * 0.55 + RepelStrength;

        // Check if full-circle layout fits on all sides
        double fullMaxR = ComputeMaxRadiusForSpread(totalItems, 360);
        double extent = (fullMaxR + petalR) * scaling;

        bool leftOk = spaceLeft >= extent;
        bool rightOk = spaceRight >= extent;
        bool upOk = spaceUp >= extent;
        bool downOk = spaceDown >= extent;

        int constrainedCount = (leftOk ? 0 : 1) + (rightOk ? 0 : 1)
                             + (upOk ? 0 : 1) + (downOk ? 0 : 1);

        if (constrainedCount == 0)
            return (-90, 360);

        double biasX = 0, biasY = 0;
        if (!leftOk) biasX += 1;
        if (!rightOk) biasX -= 1;
        if (!upOk) biasY += 1;
        if (!downOk) biasY -= 1;

        double biasMag = Math.Sqrt(biasX * biasX + biasY * biasY);
        if (biasMag < 0.1) return (-90, 360);

        double biasAngleDeg = Math.Atan2(biasY / biasMag, biasX / biasMag) * 180.0 / Math.PI;
        double biasRad = biasAngleDeg * Math.PI / 180.0;

        double availUp = spaceUp / scaling;
        double availDown = spaceDown / scaling;
        double availLeft = spaceLeft / scaling;
        double availRight = spaceRight / scaling;

        double spreadDeg = 350;
        while (spreadDeg > 90)
        {
            // Compute the actual max radius for this spread (more layers when narrower)
            double actualMaxR = ComputeMaxRadiusForSpread(totalItems, spreadDeg);

            double halfRad = spreadDeg / 2.0 * Math.PI / 180.0;
            bool safe = true;

            var angles = new List<double> { biasRad - halfRad, biasRad + halfRad };
            foreach (double card in new[] { 0.0, Math.PI / 2, Math.PI, -Math.PI / 2 })
            {
                double diff = card - biasRad;
                while (diff > Math.PI) diff -= 2 * Math.PI;
                while (diff < -Math.PI) diff += 2 * Math.PI;
                if (Math.Abs(diff) <= halfRad)
                    angles.Add(card);
            }

            foreach (double a in angles)
            {
                double dx = actualMaxR * Math.Cos(a);
                double dy = actualMaxR * Math.Sin(a);
                if (dx + petalR > availRight || -dx + petalR > availLeft ||
                    dy + petalR > availDown || -dy + petalR > availUp)
                {
                    safe = false;
                    break;
                }
            }

            if (safe) break;
            spreadDeg -= 5;
        }

        spreadDeg = Math.Max(spreadDeg, 90);
        return (biasAngleDeg, spreadDeg);
    }
}
