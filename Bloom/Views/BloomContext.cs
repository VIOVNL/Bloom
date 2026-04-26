using System;
using System.Collections.Generic;
using System.Threading;
using Avalonia.Controls;
using Bloom.Models;

namespace Bloom.Views;

internal sealed class BloomContext
{
    public readonly PetalWindow Window;
    public readonly Canvas Canvas;

    public readonly List<Border> PetalItems = new();
    public readonly List<(double dx, double dy)> PetalPositions = new();
    public readonly List<int> PetalBaseZ = new();

    public double CanvasSize;
    public int[] LayerCounts = Array.Empty<int>();
    public double[] LayerRadii = Array.Empty<double>();

    public CancellationTokenSource? AnimCts;
    public bool IsExpanded;
    public int HoveredIndex = -1;

    public double LastBias = double.NaN;
    public double LastSpread = double.NaN;

    public PetalItem[] SourceItems = Array.Empty<PetalItem>();

    public BloomContext(PetalWindow window, Canvas canvas)
    {
        Window = window;
        Canvas = canvas;
    }

    public void ResetTransientState()
    {
        IsExpanded = false;
        HoveredIndex = -1;
        LastBias = double.NaN;
        LastSpread = double.NaN;
    }
}
