using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Bloom.Services;

namespace Bloom.Views;

internal static class BloomAnimator
{
    internal static void SetBloomTransitions(Border item, bool opening, int transformMs, int opacityMs)
    {
        item.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(opacityMs),
                Easing = opening ? new CubicEaseOut() : (Easing)new LinearEasing()
            },
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(transformMs),
                Easing = opening ? new CubicEaseOut() : (Easing)new CubicEaseIn()
            }
        };
    }

    internal static void SetInteractiveTransitions(Border item)
    {
        item.Transitions = new Transitions
        {
            new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(150),
                Easing = new CubicEaseOut()
            }
        };
    }

    internal static void UpdateRepelTransforms(BloomContext ctx, double mouseX, double mouseY)
    {
        for (int i = 0; i < ctx.PetalItems.Count; i++)
        {
            var (px, py) = ctx.PetalPositions[i];
            bool isHovered = (i == ctx.HoveredIndex);

            double offsetX = 0, offsetY = 0;

            if (!isHovered)
            {
                double dx = px - mouseX;
                double dy = py - mouseY;
                double dist = Math.Sqrt(dx * dx + dy * dy);

                if (dist < PetalLayoutEngine.RepelRadius && dist > 0.1)
                {
                    double strength = (1.0 - dist / PetalLayoutEngine.RepelRadius) * PetalLayoutEngine.RepelStrength;
                    double angle = Math.Atan2(dy, dx);
                    offsetX = Math.Cos(angle) * strength;
                    offsetY = Math.Sin(angle) * strength;
                }
            }

            double scale = isHovered ? 1.1 : 1.0;
            // Divide translate by scale to compensate: Avalonia applies scale around the
            // original RenderTransformOrigin (element center at canvas center), so without
            // compensation a petal at px=200 with scale 1.1 lands at 220 instead of 200.
            double tx = (px + offsetX) / scale;
            double ty = (py + offsetY) / scale;
            ctx.PetalItems[i].RenderTransform =
                TransformOperations.Parse(
                    $"translate({PetalFactory.Fmt(tx)}px,{PetalFactory.Fmt(ty)}px) scale({scale.ToString("F2", CultureInfo.InvariantCulture)})");
        }
    }

    internal static void ResetInteractiveTransforms(BloomContext ctx)
    {
        ctx.HoveredIndex = -1;
        for (int i = 0; i < ctx.PetalItems.Count; i++)
        {
            var (px, py) = ctx.PetalPositions[i];
            ctx.PetalItems[i].RenderTransform =
                TransformOperations.Parse($"translate({PetalFactory.Fmt(px)}px,{PetalFactory.Fmt(py)}px) scale(1)");
            ctx.PetalItems[i].ZIndex = ctx.PetalBaseZ[i];
        }
    }

    /// <summary>
    /// Cancel any running close animation and snap to fully closed state.
    /// Called when the user starts dragging the bloom button while petals are closing.
    /// </summary>
    internal static void SnapToClosed(BloomContext ctx, Action<BloomContext> hidePetalWindow)
    {
        ctx.AnimCts?.Cancel();

        for (int i = 0; i < ctx.PetalItems.Count; i++)
        {
            ctx.PetalItems[i].Transitions = null;
            ctx.PetalItems[i].Opacity = 0;
            ctx.PetalItems[i].IsHitTestVisible = false;
            ctx.PetalItems[i].RenderTransform =
                TransformOperations.Parse("translate(0px,0px) scale(0)");
        }

        hidePetalWindow(ctx);
        ctx.ResetTransientState();
    }

    /// <summary>
    /// Cancel any running animation and snap all petals to their final open positions.
    /// Called when the user starts dragging the bloom button mid-animation.
    /// </summary>
    internal static void SnapToExpanded(BloomContext ctx)
    {
        ctx.AnimCts?.Cancel();

        for (int i = 0; i < ctx.PetalItems.Count; i++)
        {
            var (px, py) = ctx.PetalPositions[i];
            ctx.PetalItems[i].Transitions = null;
            ctx.PetalItems[i].Opacity = 1;
            ctx.PetalItems[i].IsHitTestVisible = true;
            ctx.PetalItems[i].RenderTransform =
                TransformOperations.Parse(
                    $"translate({PetalFactory.Fmt(px)}px,{PetalFactory.Fmt(py)}px) scale(1)");
        }

        ctx.IsExpanded = true;

        for (int i = 0; i < ctx.PetalItems.Count; i++)
            SetInteractiveTransitions(ctx.PetalItems[i]);
    }

    internal static async Task AnimateBloomAsync(
        BloomContext ctx,
        bool open,
        Func<BloomContext, (double biasAngleDeg, double spreadDeg)> computeEdgeAwareness,
        Action<BloomContext> showPetalWindow,
        Action<BloomContext> hidePetalWindow)
    {
        ctx.AnimCts?.Cancel();
        var cts = new System.Threading.CancellationTokenSource();
        ctx.AnimCts = cts;
        var token = cts.Token;

        ctx.IsExpanded = false;

        try
        {
            if (open)
            {
                if (ctx.PetalItems.Count == 0) return;

                var (biasAngle, spread) = computeEdgeAwareness(ctx);
                ctx.LastBias = biasAngle;
                ctx.LastSpread = spread;

                showPetalWindow(ctx);
                PetalLayoutEngine.LayoutPetals(ctx, biasAngle, spread);

                int count = ctx.PetalItems.Count;

                // Smooth scaling: fewer items → polished, many items → snappy.
                // Stagger scales independently so every count gets a visible ripple.
                int transitionMs = Math.Max(100, 280 - count * 8);
                int opacityMs = (int)(transitionMs * 0.65);
                int staggerMs = count <= 1 ? 0 : 120 / count;

                for (int i = 0; i < count; i++)
                {
                    ctx.PetalItems[i].Transitions = null;
                    ctx.PetalItems[i].Opacity = 0;
                    ctx.PetalItems[i].RenderTransform =
                        TransformOperations.Parse("translate(0px,0px) scale(0)");
                }

                for (int i = 0; i < count; i++)
                {
                    token.ThrowIfCancellationRequested();
                    var (dx, dy) = ctx.PetalPositions[i];
                    SetBloomTransitions(ctx.PetalItems[i], true, transitionMs, opacityMs);
                    ctx.PetalItems[i].IsHitTestVisible = true;
                    ctx.PetalItems[i].Opacity = 1;
                    ctx.PetalItems[i].RenderTransform =
                        TransformOperations.Parse(
                            $"translate({PetalFactory.Fmt(dx)}px,{PetalFactory.Fmt(dy)}px) scale(1)");

                    if (staggerMs > 0 && i < count - 1)
                        await Task.Delay(staggerMs, token);
                }

                await Task.Delay(transitionMs + 20, token);
                ctx.IsExpanded = true;
                for (int i = 0; i < count; i++)
                    SetInteractiveTransitions(ctx.PetalItems[i]);
            }
            else
            {
                ctx.HoveredIndex = -1;
                int count = ctx.PetalItems.Count;

                // Smooth scaling: matches opening curve.
                int closeTransitionMs = Math.Max(100, 300 - count * 8);
                int closeOpacityMs = (int)(closeTransitionMs * 0.85);
                int closeStaggerMs = count <= 1 ? 0 : 120 / count;

                for (int i = 0; i < count; i++)
                {
                    ctx.PetalItems[i].IsHitTestVisible = false;
                    SetBloomTransitions(ctx.PetalItems[i], false, closeTransitionMs, closeOpacityMs);
                }

                for (int i = count - 1; i >= 0; i--)
                {
                    token.ThrowIfCancellationRequested();
                    ctx.PetalItems[i].ZIndex = ctx.PetalBaseZ[i];
                    ctx.PetalItems[i].Opacity = 0;
                    ctx.PetalItems[i].RenderTransform =
                        TransformOperations.Parse("translate(0px,0px) scale(0.15)");

                    if (closeStaggerMs > 0 && i > 0)
                        await Task.Delay(closeStaggerMs, token);
                }

                await Task.Delay(closeTransitionMs + 20, token);

                hidePetalWindow(ctx);
                ctx.ResetTransientState();
            }
        }
        catch (OperationCanceledException) { }
    }
}
