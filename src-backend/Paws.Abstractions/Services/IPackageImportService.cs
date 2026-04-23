namespace Paws.Abstractions.Services;

public interface IPackageImportService
{
    Task<bool> ImportPackageAsync(string archiveFilePath);
}
