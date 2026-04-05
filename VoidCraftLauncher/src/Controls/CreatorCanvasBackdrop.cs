using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VoidCraftLauncher.Models.CreatorStudio;

namespace VoidCraftLauncher.Controls;

public sealed class CreatorCanvasBackdrop : Control
{
    private static readonly SolidColorBrush MinorGridBrush = new(Color.Parse("#1B2130"));
    private static readonly SolidColorBrush MajorGridBrush = new(Color.Parse("#263246"));
    private static readonly SolidColorBrush DefaultConnectionBrush = new(Color.Parse("#52789F"));
    private static readonly SolidColorBrush SelectedConnectionBrush = new(Color.Parse("#63C6FF"));

    private CreatorCanvasGraph? _observedGraph;

    public static readonly StyledProperty<CreatorCanvasGraph?> GraphProperty =
        AvaloniaProperty.Register<CreatorCanvasBackdrop, CreatorCanvasGraph?>(nameof(Graph));

    public static readonly StyledProperty<string?> SelectedNodeIdProperty =
        AvaloniaProperty.Register<CreatorCanvasBackdrop, string?>(nameof(SelectedNodeId));

    public static readonly StyledProperty<double> PanOffsetXProperty =
        AvaloniaProperty.Register<CreatorCanvasBackdrop, double>(nameof(PanOffsetX));

    public static readonly StyledProperty<double> PanOffsetYProperty =
        AvaloniaProperty.Register<CreatorCanvasBackdrop, double>(nameof(PanOffsetY));

    public CreatorCanvasGraph? Graph
    {
        get => GetValue(GraphProperty);
        set => SetValue(GraphProperty, value);
    }

    public string? SelectedNodeId
    {
        get => GetValue(SelectedNodeIdProperty);
        set => SetValue(SelectedNodeIdProperty, value);
    }

    public double PanOffsetX
    {
        get => GetValue(PanOffsetXProperty);
        set => SetValue(PanOffsetXProperty, value);
    }

    public double PanOffsetY
    {
        get => GetValue(PanOffsetYProperty);
        set => SetValue(PanOffsetYProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == GraphProperty)
        {
            AttachGraph(Graph);
            InvalidateVisual();
            return;
        }

        if (change.Property == SelectedNodeIdProperty ||
            change.Property == PanOffsetXProperty ||
            change.Property == PanOffsetYProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        DrawGrid(context);

        if (Graph?.Nodes == null || Graph.Nodes.Count == 0)
            return;

        var nodeIndex = Graph.Nodes.ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        foreach (var source in Graph.Nodes)
        {
            foreach (var targetId in source.ConnectedNodeIds)
            {
                if (!nodeIndex.TryGetValue(targetId, out var target))
                    continue;

                DrawConnection(context, source, target);
            }
        }
    }

    private void DrawGrid(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        DrawGridLines(context, 32, MinorGridBrush, 1);
        DrawGridLines(context, 128, MajorGridBrush, 1);
    }

    private void DrawGridLines(DrawingContext context, double spacing, IBrush brush, double thickness)
    {
        var bounds = Bounds;
        var pen = new Pen(brush, thickness);
        var offsetX = PositiveModulo(PanOffsetX, spacing);
        var offsetY = PositiveModulo(PanOffsetY, spacing);

        for (var x = offsetX; x <= bounds.Width; x += spacing)
        {
            context.DrawLine(pen, new Point(x, 0), new Point(x, bounds.Height));
        }

        for (var y = offsetY; y <= bounds.Height; y += spacing)
        {
            context.DrawLine(pen, new Point(0, y), new Point(bounds.Width, y));
        }
    }

    private void DrawConnection(DrawingContext context, CreatorCanvasNode source, CreatorCanvasNode target)
    {
        var start = ResolveNodeCenter(source);
        var end = ResolveNodeCenter(target);

        var dx = Math.Abs(end.X - start.X);
        var controlOffset = Math.Max(48, dx * 0.35);
        var control1 = new Point(start.X + controlOffset, start.Y);
        var control2 = new Point(end.X - controlOffset, end.Y);
        var isSelected = string.Equals(SelectedNodeId, source.Id, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(SelectedNodeId, target.Id, StringComparison.OrdinalIgnoreCase);

        var brush = isSelected ? SelectedConnectionBrush : DefaultConnectionBrush;
        var pen = new Pen(brush, isSelected ? 2.5 : 1.6);
        var geometry = new StreamGeometry();

        using (var geometryContext = geometry.Open())
        {
            geometryContext.BeginFigure(start, false);
            geometryContext.CubicBezierTo(control1, control2, end);
        }

        context.DrawGeometry(null, pen, geometry);
        DrawArrowHead(context, brush, end, control2);
    }

    private Point ResolveNodeCenter(CreatorCanvasNode node)
    {
        return new Point(
            node.X + (node.Width * 0.5) + PanOffsetX,
            node.Y + (node.Height * 0.5) + PanOffsetY);
    }

    private static void DrawArrowHead(DrawingContext context, IBrush brush, Point end, Point tangentSource)
    {
        var angle = Math.Atan2(end.Y - tangentSource.Y, end.X - tangentSource.X);
        const double arrowLength = 10;
        const double arrowSpread = 0.55;

        var left = new Point(
            end.X - Math.Cos(angle - arrowSpread) * arrowLength,
            end.Y - Math.Sin(angle - arrowSpread) * arrowLength);
        var right = new Point(
            end.X - Math.Cos(angle + arrowSpread) * arrowLength,
            end.Y - Math.Sin(angle + arrowSpread) * arrowLength);

        var pen = new Pen(brush, 2);
        context.DrawLine(pen, end, left);
        context.DrawLine(pen, end, right);
    }

    private static double PositiveModulo(double value, double modulo)
    {
        var result = value % modulo;
        return result < 0 ? result + modulo : result;
    }

    private void AttachGraph(CreatorCanvasGraph? graph)
    {
        DetachGraph();
        _observedGraph = graph;

        if (_observedGraph == null)
            return;

        _observedGraph.PropertyChanged += OnObservedGraphPropertyChanged;
        _observedGraph.Nodes.CollectionChanged += OnObservedNodesCollectionChanged;

        foreach (var node in _observedGraph.Nodes)
        {
            node.PropertyChanged += OnObservedNodePropertyChanged;
        }
    }

    private void DetachGraph()
    {
        if (_observedGraph == null)
            return;

        _observedGraph.PropertyChanged -= OnObservedGraphPropertyChanged;
        _observedGraph.Nodes.CollectionChanged -= OnObservedNodesCollectionChanged;

        foreach (var node in _observedGraph.Nodes)
        {
            node.PropertyChanged -= OnObservedNodePropertyChanged;
        }

        _observedGraph = null;
    }

    private void OnObservedGraphPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }

    private void OnObservedNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
        {
            foreach (var node in e.OldItems.OfType<CreatorCanvasNode>())
            {
                node.PropertyChanged -= OnObservedNodePropertyChanged;
            }
        }

        if (e.NewItems != null)
        {
            foreach (var node in e.NewItems.OfType<CreatorCanvasNode>())
            {
                node.PropertyChanged += OnObservedNodePropertyChanged;
            }
        }

        InvalidateVisual();
    }

    private void OnObservedNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        InvalidateVisual();
    }
}