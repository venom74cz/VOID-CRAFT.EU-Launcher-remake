using Avalonia.Controls;
using System.Collections.Specialized;
using VoidCraftLauncher.ViewModels;
using System;

namespace VoidCraftLauncher.Controls;

public partial class CreatorArchitektDesk : UserControl
{
    private MainViewModel? _viewModel;

    public CreatorArchitektDesk()
    {
        InitializeComponent();
        
        // Auto-scroll when new messages are added
        DataContextChanged += (s, e) =>
        {
            if (_viewModel != null)
            {
                _viewModel.ArchitektHistory.CollectionChanged -= ArchitektHistory_CollectionChanged;
            }

            if (DataContext is MainViewModel vm)
            {
                _viewModel = vm;
                _viewModel.ArchitektHistory.CollectionChanged += ArchitektHistory_CollectionChanged;
                _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }
        };

        // Also scroll when layout is updated (useful for streaming text)
        ChatScrollViewer.LayoutUpdated += (s, e) => 
        {
            // Only force scroll if the user is near the bottom, so we don't steal scroll
            var viewer = ChatScrollViewer;
            if (viewer.Extent.Height - viewer.Viewport.Height - viewer.Offset.Y < 120)
            {
                viewer.ScrollToEnd();
            }
        };
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "ScrollArchitektRequested")
        {
            ChatScrollViewer.ScrollToEnd();
        }
    }

    private void ArchitektHistory_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ChatScrollViewer.ScrollToEnd();
        });
    }
}