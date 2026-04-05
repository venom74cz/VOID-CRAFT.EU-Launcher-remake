using Avalonia.Threading;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VoidCraftLauncher.Models;
using VoidCraftLauncher.Services;

namespace VoidCraftLauncher.ViewModels;

/// <summary>
/// Handles external protocol launches such as voidcraft://install deeplinks from VOID-CRAFT.EU.
/// </summary>
public partial class MainViewModel
{
    private readonly SemaphoreSlim _protocolInstallLock = new(1, 1);

    private async Task HandlePendingProtocolLaunchAsync()
    {
        var installRequest = Program.TakePendingInstallRequest();
        if (installRequest == null)
        {
            return;
        }

        await ExecuteProtocolInstallAsync(installRequest);
    }

    private async Task PollProtocolInstallRequestsAsync()
    {
        if (!Program.IsPrimaryInstance)
        {
            return;
        }

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));

            var installRequest = ProtocolHandler.ReadInstallRequestFromFile();
            if (installRequest == null)
            {
                continue;
            }

            try
            {
                await ExecuteProtocolInstallAsync(installRequest);
            }
            catch (Exception ex)
            {
                LogService.Error("Protocol install polling failed", ex);
            }
        }
    }

    private async Task ExecuteProtocolInstallAsync(ProtocolInstallRequest request)
    {
        await _protocolInstallLock.WaitAsync();

        try
        {
            if (_instanceExportService == null)
            {
                throw new InvalidOperationException("Import service launcheru zatím není připravená.");
            }

            var displayName = BuildProtocolDisplayName(request);
            var archivePath = BuildProtocolArchivePath(request);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                App.RestoreMainWindow();
                NavigateToView(MainViewType.Library, true);
                ShowToast("Launcher", $"Připravuji instalaci {displayName}...", ToastSeverity.Info, 2800);
            });

            try
            {
                await DownloadProtocolArchiveAsync(request.DownloadUrl, archivePath, displayName);
                await ImportInstance(archivePath);
            }
            finally
            {
                TryDeleteProtocolArchive(archivePath);
            }
        }
        catch (Exception ex)
        {
            LogService.Error("Protocol install failed", ex);
            await Dispatcher.UIThread.InvokeAsync(() =>
                ShowToast("Installace selhala", ex.Message, ToastSeverity.Error, 4600));
        }
        finally
        {
            _protocolInstallLock.Release();
        }
    }

    private async Task DownloadProtocolArchiveAsync(string downloadUrl, string archivePath, string displayName)
    {
        if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out var downloadUri) ||
            (downloadUri.Scheme != Uri.UriSchemeHttps && downloadUri.Scheme != Uri.UriSchemeHttp))
        {
            throw new InvalidOperationException("Deeplink neobsahuje validní URL pro VOIDPACK archiv.");
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
            ShowToast("Launcher", $"Stahuji {displayName} do launcheru...", ToastSeverity.Info, 2600));

        var directory = Path.GetDirectoryName(archivePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await _httpClient.GetAsync(downloadUri, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var inputStream = await response.Content.ReadAsStreamAsync();
        await using var outputStream = File.Create(archivePath);
        await inputStream.CopyToAsync(outputStream);

        LogService.Log($"Protocol install archive downloaded: {displayName} -> {archivePath}");
    }

    private static string BuildProtocolDisplayName(ProtocolInstallRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ProjectName) && !string.IsNullOrWhiteSpace(request.Version))
        {
            return $"{request.ProjectName} {request.Version}";
        }

        if (!string.IsNullOrWhiteSpace(request.ProjectName))
        {
            return request.ProjectName;
        }

        if (!string.IsNullOrWhiteSpace(request.Slug) && !string.IsNullOrWhiteSpace(request.Version))
        {
            return $"{request.Slug} {request.Version}";
        }

        return string.IsNullOrWhiteSpace(request.Slug) ? "VOID-CRAFT modpack" : request.Slug;
    }

    private static string BuildProtocolArchivePath(ProtocolInstallRequest request)
    {
        var baseNameParts = new[] { request.Slug, request.Version }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(SanitizeProtocolFilePart)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        var baseName = baseNameParts.Length > 0
            ? string.Join("-", baseNameParts)
            : SanitizeProtocolFilePart(request.ProjectName);

        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = "voidcraft-install";
        }

        return Path.Combine(Path.GetTempPath(), $"{baseName}-{Guid.NewGuid():N}.voidpack");
    }

    private static string SanitizeProtocolFilePart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value
            .Trim()
            .Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '-' : ch)
            .ToArray());

        while (sanitized.Contains("--", StringComparison.Ordinal))
        {
            sanitized = sanitized.Replace("--", "-", StringComparison.Ordinal);
        }

        return sanitized.Trim('-');
    }

    private static void TryDeleteProtocolArchive(string archivePath)
    {
        try
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }
        }
        catch
        {
        }
    }
}