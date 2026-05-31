using Microsoft.UI.Xaml;
using Windows.Storage.Pickers;
using WiFiStudio.Core.Models;
using WiFiStudio.Core.Serialization;
using WinRT.Interop;

namespace WiFiStudio.App.Services;

public sealed class ProjectFileService : IProjectFileService
{
    private readonly Window _window;

    public ProjectFileService(Window window)
    {
        _window = window;
    }

    public async Task<string?> SaveAsync(ProjectModel project, string? currentPath, CancellationToken cancellationToken)
    {
        var path = currentPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = SanitizeFileName(project.Name),
                DefaultFileExtension = ".json"
            };
            picker.FileTypeChoices.Add("WiFi Studio Project", new List<string> { ".json" });
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
            var file = await picker.PickSaveFileAsync();
            if (file is null)
            {
                return null;
            }

            path = file.Path;
        }

        await ProjectJsonSerializer.SaveAsync(project, path, cancellationToken);
        return path;
    }

    public async Task<(ProjectModel Project, string Path)?> OpenAsync(CancellationToken cancellationToken)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add(".json");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(_window));
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return null;
        }

        var project = await ProjectJsonSerializer.LoadAsync(file.Path, cancellationToken);
        return (project, file.Path);
    }

    private static string SanitizeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "rf-plan" : sanitized;
    }
}
