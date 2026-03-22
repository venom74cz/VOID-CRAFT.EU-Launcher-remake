using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using VoidCraftLauncher.Models;

namespace VoidCraftLauncher.Services;

public sealed class LocalizationService
{
    public const string SystemLanguageCode = "system";

    private readonly ResourceManager _resourceManager =
        new("VoidCraftLauncher.Resources.Strings", Assembly.GetExecutingAssembly());

    private static readonly IReadOnlyList<SelectionOption> SupportedLanguagesInternal =
    [
        new SelectionOption { Id = SystemLanguageCode, Label = "System" },
        new SelectionOption { Id = "cs-CZ", Label = "Čeština" },
        new SelectionOption { Id = "en", Label = "English" }
    ];

    public event Action? LanguageChanged;

    public IReadOnlyList<SelectionOption> SupportedLanguages => SupportedLanguagesInternal;

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentUICulture;

    public CultureInfo SystemCulture { get; } = CultureInfo.InstalledUICulture;

    public string CurrentLanguageCode { get; private set; } = SystemLanguageCode;

    public string GetString(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            return string.Empty;

        var value = _resourceManager.GetString(key, CurrentCulture);
        return string.IsNullOrWhiteSpace(value) ? key : value;
    }

    public string Format(string key, params object[] args)
    {
        var format = GetString(key);
        return args.Length == 0 ? format : string.Format(CurrentCulture, format, args);
    }

    public void ApplyConfiguredLanguage(string? languageCode)
    {
        var effectiveCulture = ResolveCulture(languageCode, out var normalizedCode);
        CurrentLanguageCode = normalizedCode;
        CurrentCulture = effectiveCulture;

        CultureInfo.DefaultThreadCurrentCulture = effectiveCulture;
        CultureInfo.DefaultThreadCurrentUICulture = effectiveCulture;
        CultureInfo.CurrentCulture = effectiveCulture;
        CultureInfo.CurrentUICulture = effectiveCulture;

        LanguageChanged?.Invoke();
    }

    private CultureInfo ResolveCulture(string? languageCode, out string normalizedCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode) ||
            string.Equals(languageCode, SystemLanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            normalizedCode = SystemLanguageCode;
            return NormalizeSupportedCulture(SystemCulture);
        }

        try
        {
            var culture = NormalizeSupportedCulture(new CultureInfo(languageCode));
            normalizedCode = culture.Name;
            return culture;
        }
        catch
        {
            normalizedCode = SystemLanguageCode;
            return NormalizeSupportedCulture(SystemCulture);
        }
    }

    private static CultureInfo NormalizeSupportedCulture(CultureInfo sourceCulture)
    {
        return sourceCulture.TwoLetterISOLanguageName.Equals("cs", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("cs-CZ")
            : new CultureInfo("en");
    }
}