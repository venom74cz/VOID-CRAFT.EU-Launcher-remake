using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.ComponentModel;
using System.Threading.Tasks;

namespace VoidCraftLauncher.Views
{
    public partial class MainWindow : Window
    {
        private const double CompactThreshold = 1100;
        private INotifyPropertyChanged? _observedViewModel;
        private bool _skipNextMainViewAnimation = true;
        private bool _isAnimatingMainContent;

        public MainWindow()
        {
            InitializeComponent();
            PropertyChanged += OnWindowPropertyChanged;
            DataContextChanged += OnWindowDataContextChanged;
        }

        private void OnWindowDataContextChanged(object? sender, System.EventArgs e)
        {
            if (_observedViewModel != null)
            {
                _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            }

            _observedViewModel = DataContext as INotifyPropertyChanged;
            if (_observedViewModel != null)
            {
                _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            }
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "CurrentMainView")
            {
                AnimateMainContentTransition();
            }
        }

        private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property != BoundsProperty) return;

            var width = Bounds.Width;
            var col2 = RootLayout.ColumnDefinitions[2];
            var shouldCollapse = width < CompactThreshold;

            if (shouldCollapse && col2.Width != new GridLength(0))
            {
                col2.Width = new GridLength(0);
                ContextDockPanel.IsVisible = false;
            }
            else if (!shouldCollapse && col2.Width == new GridLength(0))
            {
                col2.Width = new GridLength(280);
                ContextDockPanel.IsVisible = true;
            }
        }

        private async void AnimateMainContentTransition()
        {
            if (_skipNextMainViewAnimation)
            {
                _skipNextMainViewAnimation = false;
                return;
            }

            if (_isAnimatingMainContent || Classes.Contains("reduced-motion"))
            {
                return;
            }

            _isAnimatingMainContent = true;
            try
            {
                MainContentHost.Opacity = 0.9;
                MainContentHost.RenderTransform = new TranslateTransform(0, 10);

                await Task.Delay(18);

                MainContentHost.Opacity = 1;
                MainContentHost.RenderTransform = new TranslateTransform(0, 0);

                await Task.Delay(200);
            }
            finally
            {
                _isAnimatingMainContent = false;
            }
        }
    }
}
