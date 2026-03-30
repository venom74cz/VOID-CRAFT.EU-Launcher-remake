using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;

namespace VoidCraftLauncher.Controls
{
    public partial class AchievementsView : UserControl
    {
        private TextBlock? _teamTextBlock;
        private ScrollViewer? _teamScrollViewer;
        private DispatcherTimer? _marqueeTimer;
        private double _marqueeOffset;
        private const double MarqueeSpeed = 30.0; // pixels per second
        private readonly TimeSpan _marqueeInterval = TimeSpan.FromMilliseconds(30);

        public AchievementsView()
        {
            InitializeComponent();
            this.LayoutUpdated += AchievementsView_LayoutUpdated;
        }


        private void AchievementsView_LayoutUpdated(object? sender, EventArgs e)
        {
            // Find controls lazily once layout is available
            if (_teamTextBlock == null)
                _teamTextBlock = this.FindControl<TextBlock>("TeamNameTextBlock");
            if (_teamScrollViewer == null)
                _teamScrollViewer = this.FindControl<ScrollViewer>("TeamScrollViewer");

            StartOrUpdateMarquee();
        }

        private void StartOrUpdateMarquee()
        {
            if (_teamTextBlock == null || _teamScrollViewer == null)
                return;

            var textWidth = _teamTextBlock.Bounds.Width;
            var viewWidth = _teamScrollViewer.Bounds.Width;

            if (double.IsNaN(textWidth) || double.IsNaN(viewWidth) || textWidth <= 0 || viewWidth <= 0)
                return;

            if (textWidth > viewWidth + 1)
            {
                if (_marqueeTimer == null)
                {
                    _marqueeOffset = 0;
                    _marqueeTimer = new DispatcherTimer(_marqueeInterval, DispatcherPriority.Background, (s, ev) =>
                    {
                        _marqueeOffset += MarqueeSpeed * _marqueeInterval.TotalSeconds;
                        var max = Math.Max(0, textWidth - viewWidth);
                        if (_marqueeOffset > max)
                        {
                            _marqueeOffset = 0;
                        }
                        _teamTextBlock.RenderTransform = new TranslateTransform(-_marqueeOffset, 0);
                    });
                    _marqueeTimer.Start();
                }
            }
            else
            {
                StopMarquee();
            }
        }

        private void StopMarquee()
        {
            if (_marqueeTimer != null)
            {
                _marqueeTimer.Stop();
                _marqueeTimer = null;
            }

            if (_teamTextBlock != null)
            {
                _teamTextBlock.RenderTransform = new TranslateTransform(0, 0);
            }
        }
    }
}
