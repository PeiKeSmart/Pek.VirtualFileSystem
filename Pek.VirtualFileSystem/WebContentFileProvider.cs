using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NewLife.Log;

using NewLife;

namespace Pek.VirtualFileSystem;

public class WebContentFileProvider : IWebContentFileProvider
{
    private readonly IVirtualFileProvider _virtualFileProvider;
    private readonly IFileProvider _fileProvider;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private string _rootPath = "/wwwroot"; //TODO: How to handle wwwroot naming?
    // 使用 XTrace 输出调试日志

    protected DHAspNetCoreContentOptions Options { get; }

    public WebContentFileProvider(
        IVirtualFileProvider virtualFileProvider,
        IWebHostEnvironment hostingEnvironment,
        IOptions<DHAspNetCoreContentOptions> options)
    {
        _virtualFileProvider = virtualFileProvider;
        _hostingEnvironment = hostingEnvironment;
        Options = options.Value;

        _fileProvider = CreateFileProvider();
    }

    /// <summary>
    /// 在给定的路径查找某个文件
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public virtual IFileInfo GetFileInfo(string subpath)
    {
        if (subpath.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(subpath));

        if (PathUtils.PathNavigatesAboveRoot(subpath))
        {
            return new NotFoundFileInfo(subpath);
        }

        if (ExtraAllowedFolder(subpath) && ExtraAllowedExtension(subpath))
        {
            var fileInfo = _fileProvider.GetFileInfo(subpath);
            if (fileInfo.Exists) return fileInfo;
        }
        var combined = _rootPath + subpath;
        var fallback = _fileProvider.GetFileInfo(combined);
        if (!fallback.Exists && Options.LogFileMisses)
            XTrace.WriteLine("[WebVFS] MISS file. Request={0} Tried={1}", subpath, combined);
        return fallback;
    }

    /// <summary>
    /// 枚举位于给定路径的目录（如果有）
    /// </summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public virtual IDirectoryContents GetDirectoryContents(string subpath)
    {
        if (subpath.IsNullOrWhiteSpace()) throw new ArgumentNullException(nameof(subpath));

        if (PathUtils.PathNavigatesAboveRoot(subpath))
        {
            return NotFoundDirectoryContents.Singleton;
        }

        if (ExtraAllowedFolder(subpath))
        {
            var directory = _fileProvider.GetDirectoryContents(subpath);
            if (directory.Exists) return directory;
        }
        var combined = _rootPath + subpath;
        var fallback = _fileProvider.GetDirectoryContents(combined);
        if (!fallback.Exists && Options.LogFileMisses)
            XTrace.WriteLine("[WebVFS] MISS dir. RequestDir={0} Tried={1}", subpath, combined);
        return fallback;
    }

    /// <summary>
    /// 为指定的 IChangeToken 创建 filter
    /// </summary>
    /// <param name="filter"></param>
    /// <returns></returns>
    public virtual IChangeToken Watch(string filter)
    {
        if (!ExtraAllowedFolder(filter))
        {
            var token = _fileProvider.Watch(_rootPath + filter);
            return token;
        }
        var token1 = _fileProvider.Watch(_rootPath + filter);
        var token2 = _fileProvider.Watch(filter);
        return new CompositeChangeToken([token1, token2]);
    }

    protected virtual IFileProvider CreateFileProvider()
    {
        return new CompositeFileProvider(
            new PhysicalFileProvider(_hostingEnvironment.ContentRootPath),
            _virtualFileProvider
        );
    }

    protected virtual bool ExtraAllowedFolder(string path)
    {
        return Options.AllowedExtraWebContentFolders.Any(s => path.StartsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual bool ExtraAllowedExtension(string path)
    {
        return Options.AllowedExtraWebContentFileExtensions.Any(e => path.EndsWith(e, StringComparison.OrdinalIgnoreCase));
    }
}