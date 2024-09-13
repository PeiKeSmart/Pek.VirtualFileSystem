using Microsoft.Extensions.FileProviders;

namespace Pek.VirtualFileSystem;

public interface IDynamicFileProvider : IFileProvider
{
    void AddOrUpdate(IFileInfo fileInfo);

    bool Delete(string filePath);
}
