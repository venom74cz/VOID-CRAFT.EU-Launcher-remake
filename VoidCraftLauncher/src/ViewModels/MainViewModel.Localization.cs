using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

public partial class MainViewModel
{
    private bool _isUpdatingLanguageSelection;

    public ObservableCollection<SelectionOption> LanguageOptions { get; } = new();

    [ObservableProperty]
    private SelectionOption? _selectedLanguageOption;

    public string LocalizationUiCultureName => CultureInfo.CurrentUICulture.Name;

    public string LocalizationUiCultureDisplayName => CultureInfo.CurrentUICulture.DisplayName;

    public string LocalizationSystemCultureName => _localizationService.SystemCulture.Name;

    public string LocalizationSystemCultureDisplayName => _localizationService.SystemCulture.DisplayName;

    public string LocalizationRuntimeStatus => L("Localization.Runtime.Ready");

    public string LocalizationCoverageStatus => L("Localization.Runtime.Coverage");

    public string LocalizationHeaderTitle => L("Localization.Header.Title");

    public string LocalizationHeaderSubtitle => L("Localization.Header.Subtitle");

    public string LocalizationRuntimeCardTitle => L("Localization.Card.Runtime");

    public string LocalizationActiveLanguageTitle => L("Localization.Card.ActiveLanguage");

    public string LocalizationSystemLanguageTitle => L("Localization.Card.SystemLanguage");

    public string LocalizationSelectLanguageTitle => L("Localization.Card.SelectLanguage");

    public string LocalizationSelectLanguageHint => L("Localization.Card.SelectLanguageHint");

    public string LocalizationInfrastructureTitle => L("Localization.Card.Infrastructure");

    public string LocalizationDiagnosticsTitle => L("Localization.Card.Diagnostics");

    public string LocalizationDiagnosticsHint => L("Localization.Card.DiagnosticsHint");

    public string LocalizationInfrastructureItem1 => L("Localization.Card.Infrastructure.Item1");

    public string LocalizationInfrastructureItem2 => L("Localization.Card.Infrastructure.Item2");

    public string LocalizationInfrastructureItem3 => L("Localization.Card.Infrastructure.Item3");

    public string LocalizationInfrastructureItem4 => L("Localization.Card.Infrastructure.Item4");

    public string LocalizationCopyReportLabel => L("Localization.Button.CopyReport");

    private void InitializeLocalizationSurface()
    {
        _localizationService.LanguageChanged += OnLocalizationLanguageChanged;
        RebuildLanguageOptions();
        SyncSelectedLanguageOption();
    }

    partial void OnSelectedLanguageOptionChanged(SelectionOption? value)
    {
        if (_isUpdatingLanguageSelection || value == null)
            return;

        _localizationService.ApplyConfiguredLanguage(value.Id);
        Config.PreferredLanguageCode = value.Id;
        _launcherService.SaveConfig(Config);
        ShowToast(L("Localization.Toast.Title"), LF("Localization.Toast.LanguageChanged", value.Label), ToastSeverity.Success, 2500);
    }

    private void RebuildLanguageOptions()
    {
        var currentSelectionId = SelectedLanguageOption?.Id ?? Config.PreferredLanguageCode;
        LanguageOptions.Clear();
        LanguageOptions.Add(new SelectionOption { Id = LocalizationService.SystemLanguageCode, Label = $"{L("Localization.Card.SystemLanguage")} ({_localizationService.SystemCulture.NativeName})" });
        LanguageOptions.Add(new SelectionOption { Id = "cs-CZ", Label = "Čeština" });
        LanguageOptions.Add(new SelectionOption { Id = "en", Label = "English" });

        _isUpdatingLanguageSelection = true;
        SelectedLanguageOption = LanguageOptions.FirstOrDefault(option => option.Id == currentSelectionId)
            ?? LanguageOptions.FirstOrDefault(option => option.Id == LocalizationService.SystemLanguageCode);
        _isUpdatingLanguageSelection = false;
    }

    private void SyncSelectedLanguageOption()
    {
        _isUpdatingLanguageSelection = true;
        SelectedLanguageOption = LanguageOptions.FirstOrDefault(option => option.Id == Config.PreferredLanguageCode)
            ?? LanguageOptions.FirstOrDefault(option => option.Id == LocalizationService.SystemLanguageCode);
        _isUpdatingLanguageSelection = false;
    }

    private void OnLocalizationLanguageChanged()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            RebuildLanguageOptions();
            RebuildAchievementFilterOptions();
            NotifyLocalizationStateChanged();
            RebuildAchievementSurface();
            OnPropertyChanged(nameof(MainPanelTitle));
        });
    }

    private void NotifyLocalizationStateChanged()
    {
        OnPropertyChanged(nameof(LocalizationUiCultureName));
        OnPropertyChanged(nameof(LocalizationUiCultureDisplayName));
        OnPropertyChanged(nameof(LocalizationSystemCultureName));
        OnPropertyChanged(nameof(LocalizationSystemCultureDisplayName));
        OnPropertyChanged(nameof(LocalizationRuntimeStatus));
        OnPropertyChanged(nameof(LocalizationCoverageStatus));
        OnPropertyChanged(nameof(LocalizationHeaderTitle));
        OnPropertyChanged(nameof(LocalizationHeaderSubtitle));
        OnPropertyChanged(nameof(LocalizationRuntimeCardTitle));
        OnPropertyChanged(nameof(LocalizationActiveLanguageTitle));
        OnPropertyChanged(nameof(LocalizationSystemLanguageTitle));
        OnPropertyChanged(nameof(LocalizationSelectLanguageTitle));
        OnPropertyChanged(nameof(LocalizationSelectLanguageHint));
        OnPropertyChanged(nameof(LocalizationInfrastructureTitle));
        OnPropertyChanged(nameof(LocalizationDiagnosticsTitle));
        OnPropertyChanged(nameof(LocalizationDiagnosticsHint));
        OnPropertyChanged(nameof(LocalizationInfrastructureItem1));
        OnPropertyChanged(nameof(LocalizationInfrastructureItem2));
        OnPropertyChanged(nameof(LocalizationInfrastructureItem3));
        OnPropertyChanged(nameof(LocalizationInfrastructureItem4));
        OnPropertyChanged(nameof(LocalizationCopyReportLabel));
    }

    [RelayCommand]
    private async Task CopyLocalizationReport()
    {
        var topLevel = Avalonia.Controls.TopLevel.GetTopLevel(
            Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null);

        if (topLevel?.Clipboard == null)
        {
            ShowToast(L("Localization.Toast.Title"), L("Localization.Toast.ClipboardUnavailable"), ToastSeverity.Warning);
            return;
        }

        var payload =
            $"{L("Localization.Report.Title")}\n" +
            $"UI culture: {LocalizationUiCultureName} ({LocalizationUiCultureDisplayName})\n" +
            $"System culture: {LocalizationSystemCultureName} ({LocalizationSystemCultureDisplayName})\n" +
            $"Selected language: {SelectedLanguageOption?.Label ?? L("Common.Unknown")}\n" +
            $"{LF("Localization.Report.Status", LocalizationRuntimeStatus)}";

        await topLevel.Clipboard.SetTextAsync(payload);
        ShowToast(L("Localization.Toast.Title"), L("Localization.Toast.ReportCopied"), ToastSeverity.Success, 2500);
    }

    private string L(string key) => _localizationService.GetString(key);

    private string LF(string key, params object[] args) => _localizationService.Format(key, args);
}