using System;
using System.Linq;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VoidCraftLauncher.Models.CreatorStudio;
using VoidCraftLauncher.ViewModels;

namespace VoidCraftLauncher.Controls;

public partial class CreatorCanvasSurface : UserControl
{
    private const int DoubleClickWindowMs = 420;
    private MainViewModel? _viewModel;
    private double _panOffsetX;
    private double _panOffsetY;
    private bool _isPanning;
    private Point _lastViewportPoint;
    private CreatorCanvasNode? _pressedNode;
    private CreatorCanvasNode? _editingNode;
    private Point _dragOriginViewport;
    private Point _dragOriginNode;
    private bool _isDraggingNode;
    private Point _lastContextWorkspacePoint = new(72, 72);
    private string? _lastPressedNodeId;
    private DateTimeOffset _lastNodePressedAt = DateTimeOffset.MinValue;
    private Point _lastNodePressedPoint;

    public CreatorCanvasSurface()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        UpdateSurfaceState();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateSurfaceState();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedCreatorCanvasGraph) ||
            e.PropertyName == nameof(MainViewModel.SelectedCreatorCanvasNode))
        {
            UpdateSurfaceState();
        }
    }

    private void UpdateSurfaceState()
    {
        BackdropLayer.Graph = _viewModel?.SelectedCreatorCanvasGraph;
        BackdropLayer.SelectedNodeId = _viewModel?.SelectedCreatorCanvasNode?.Id;
        BackdropLayer.PanOffsetX = _panOffsetX;
        BackdropLayer.PanOffsetY = _panOffsetY;
        NodeLayer.RenderTransform = new TranslateTransform(_panOffsetX, _panOffsetY);
    }

    private Point ToWorkspacePoint(Point viewportPoint)
    {
        return new Point(viewportPoint.X - _panOffsetX, viewportPoint.Y - _panOffsetY);
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var sourceControl = e.Source as Control;
        var currentPoint = e.GetCurrentPoint(ViewportBorder);
        var viewportPoint = e.GetPosition(ViewportBorder);

        if (currentPoint.Properties.IsMiddleButtonPressed)
        {
            CommitInlineEdit();
            _isPanning = true;
            _lastViewportPoint = viewportPoint;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(ViewportBorder);
            e.Handled = true;
            return;
        }

        if (currentPoint.Properties.IsRightButtonPressed)
        {
            _lastContextWorkspacePoint = ToWorkspacePoint(viewportPoint);
            if (!IsInteractiveEditorSource(sourceControl))
            {
                CommitInlineEdit();
            }
            return;
        }

        if (!currentPoint.Properties.IsLeftButtonPressed)
            return;

        if (IsInteractiveEditorSource(sourceControl))
            return;

        var node = FindNodeFromSource(sourceControl);
        if (node == null)
        {
            CommitInlineEdit();
            return;
        }

        if (node.IsInlineEditing)
            return;

        if (_viewModel == null)
            return;

        if ((e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.Control &&
            _viewModel.SelectedCreatorCanvasNode != null &&
            !string.Equals(_viewModel.SelectedCreatorCanvasNode.Id, node.Id, StringComparison.OrdinalIgnoreCase))
        {
            CommitInlineEdit(node);
            _viewModel.LinkCreatorCanvasNodeCommand.Execute(node);
            BackdropLayer.SelectedNodeId = _viewModel.SelectedCreatorCanvasNode?.Id;
            e.Handled = true;
            return;
        }

        if (IsSameNodeDoubleClick(node, viewportPoint))
        {
            StartInlineEdit(node);
            e.Handled = true;
            return;
        }

        CommitInlineEdit(node);
        _viewModel.SelectCreatorCanvasNodeCommand.Execute(node);
        BackdropLayer.SelectedNodeId = node.Id;

        _pressedNode = node;
        _isDraggingNode = false;
        _dragOriginViewport = viewportPoint;
        _dragOriginNode = new Point(node.X, node.Y);
        _lastPressedNodeId = node.Id;
        _lastNodePressedAt = DateTimeOffset.UtcNow;
        _lastNodePressedPoint = viewportPoint;

        e.Pointer.Capture(ViewportBorder);
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        var current = e.GetPosition(ViewportBorder);

        if (_pressedNode != null)
        {
            var delta = current - _dragOriginViewport;

            if (!_isDraggingNode)
            {
                if (System.Math.Abs(delta.X) < 3 && System.Math.Abs(delta.Y) < 3)
                    return;

                _isDraggingNode = true;
            }

            _pressedNode.X = System.Math.Max(12, _dragOriginNode.X + delta.X);
            _pressedNode.Y = System.Math.Max(12, _dragOriginNode.Y + delta.Y);
            e.Handled = true;
            return;
        }

        if (!_isPanning)
            return;

        var panDelta = current - _lastViewportPoint;
        _lastViewportPoint = current;

        _panOffsetX += panDelta.X;
        _panOffsetY += panDelta.Y;
        UpdateSurfaceState();
        e.Handled = true;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pressedNode != null)
        {
            _pressedNode = null;
            _isDraggingNode = false;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        if (!_isPanning)
            return;

        _isPanning = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnViewportPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _pressedNode = null;
        _isDraggingNode = false;
        _isPanning = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    private void OnInlineEditDoneClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not CreatorCanvasNode node)
            return;

        EndInlineEdit(node);
        e.Handled = true;
    }

    private void OnInlineEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control control || control.DataContext is not CreatorCanvasNode node)
            return;

        if (e.Key == Key.Escape || (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.Control))
        {
            EndInlineEdit(node);
            e.Handled = true;
        }
    }

    private void OnAddTextNodeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CreateNodeFromContext("text");

    private void OnAddImageNodeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CreateNodeFromContext("image");

    private void OnAddFileNodeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CreateNodeFromContext("file");

    private void OnAddLinkNodeClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => CreateNodeFromContext("link");

    private void CreateNodeFromContext(string kind)
    {
        if (_viewModel == null)
            return;

        CommitInlineEdit();

        _viewModel.CreateCreatorCanvasNodeAtCommand.Execute(new CreatorCanvasSpawnRequest
        {
            NodeKind = kind,
            X = _lastContextWorkspacePoint.X,
            Y = _lastContextWorkspacePoint.Y
        });

        BackdropLayer.SelectedNodeId = _viewModel.SelectedCreatorCanvasNode?.Id;
    }

    private void CommitInlineEdit(CreatorCanvasNode? keepNode = null)
    {
        if (_editingNode == null)
            return;

        if (keepNode != null && ReferenceEquals(_editingNode, keepNode))
            return;

        _editingNode.IsInlineEditing = false;
        _editingNode = null;
    }

    private void EndInlineEdit(CreatorCanvasNode node)
    {
        node.IsInlineEditing = false;
        if (ReferenceEquals(_editingNode, node))
        {
            _editingNode = null;
        }
    }

    private void StartInlineEdit(CreatorCanvasNode node)
    {
        if (_viewModel == null)
            return;

        CommitInlineEdit(node);
        _viewModel.SelectCreatorCanvasNodeCommand.Execute(node);
        node.IsInlineEditing = true;
        _editingNode = node;
        BackdropLayer.SelectedNodeId = node.Id;

        var nodeBorder = FindNodeBorder(node);
        if (nodeBorder == null)
            return;

        Dispatcher.UIThread.Post(() => FocusFirstInlineEditor(nodeBorder), DispatcherPriority.Input);
    }

    private static bool IsInteractiveEditorSource(Control? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is TextBox || current is Button)
                return true;

            current = current.GetVisualParent() as Control;
        }

        return false;
    }

    private static CreatorCanvasNode? FindNodeFromSource(Control? source)
    {
        var current = source;
        while (current != null)
        {
            if (current.DataContext is CreatorCanvasNode node)
                return node;

            current = current.GetVisualParent() as Control;
        }

        return null;
    }

    private bool IsSameNodeDoubleClick(CreatorCanvasNode node, Point viewportPoint)
    {
        if (!string.Equals(_lastPressedNodeId, node.Id, StringComparison.OrdinalIgnoreCase))
            return false;

        var elapsed = DateTimeOffset.UtcNow - _lastNodePressedAt;
        if (elapsed.TotalMilliseconds > DoubleClickWindowMs)
            return false;

        var dx = viewportPoint.X - _lastNodePressedPoint.X;
        var dy = viewportPoint.Y - _lastNodePressedPoint.Y;
        return Math.Abs(dx) <= 6 && Math.Abs(dy) <= 6;
    }

    private Border? FindNodeBorder(CreatorCanvasNode node)
    {
        return NodeLayer.GetVisualDescendants()
            .OfType<Border>()
            .FirstOrDefault(border => ReferenceEquals(border.Tag, node));
    }

    private static void FocusFirstInlineEditor(Border border)
    {
        var editor = border.GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault(textBox => textBox.IsVisible);

        if (editor == null)
            return;

        editor.Focus();
        editor.SelectAll();
    }
}