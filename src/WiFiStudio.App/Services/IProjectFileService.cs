using WiFiStudio.Core.Models;

namespace WiFiStudio.App.Services;

public interface IProjectFileService
{
    Task<string?> SaveAsync(ProjectModel project, string? currentPath, CancellationToken cancellationToken);
    Task<(ProjectModel Project, string Path)?> OpenAsync(CancellationToken cancellationToken);
}
