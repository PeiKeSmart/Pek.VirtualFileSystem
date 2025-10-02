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
            if (fileInfo.Exists)
            {
                if (Options.LogFileHits) XTrace.WriteLine("[WebVFS] HIT file(extra). Request={0} Path={1} Size={2}", subpath, subpath, fileInfo.Length);
                return fileInfo;
            }
        }
        var combined = _rootPath + subpath;
        var fallback = _fileProvider.GetFileInfo(combined);
        if (fallback.Exists)
        {
            if (Options.LogFileHits) XTrace.WriteLine("[WebVFS] HIT file(fallback). Request={0} Mapped={1} Size={2}", subpath, combined, fallback.Length);
        }
        else if (Options.LogFileMisses)
        {
            XTrace.WriteLine("[WebVFS] MISS file. Request={0} Tried={1}", subpath, combined);
            TryHeuristicFileDiagnostics(subpath);
            TryDumpAllVirtualFilesOnce();
        }
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
            if (directory.Exists)
            {
                if (Options.LogFileHits) XTrace.WriteLine("[WebVFS] HIT dir(extra). RequestDir={0}", subpath);
                return directory;
            }
        }
        var combined = _rootPath + subpath;
        var fallback = _fileProvider.GetDirectoryContents(combined);
        if (fallback.Exists)
        {
            if (Options.LogFileHits) XTrace.WriteLine("[WebVFS] HIT dir(fallback). RequestDir={0} MappedDir={1}", subpath, combined);
        }
        else if (Options.LogFileMisses)
        {
            XTrace.WriteLine("[WebVFS] MISS dir. RequestDir={0} Tried={1}", subpath, combined);
        }
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

    private void TryHeuristicFileDiagnostics(string requestPath)
    {
        try
        {
            // 仅针对文件 miss 做一些常见历史转换尝试，帮助定位真实存储的虚拟键。
            var candidates = new List<string>();

            // 1. 旧拆分逻辑：additional-methods.min.js -> additional_methods/min.js
            if (requestPath.Contains('.'))
            {
                var lastSlash = requestPath.LastIndexOf('/');
                var dir = lastSlash >= 0 ? requestPath[..lastSlash] : string.Empty;
                var file = lastSlash >= 0 ? requestPath[(lastSlash + 1)..] : requestPath;
                // 拆分多点
                var parts = file.Split('.');
                if (parts.Length > 2)
                {
                    // 旧逻辑会把除最后一段（扩展名前一段）外中间段转成目录并替换 '-' 为 '_'
                    // original: additional-methods.min.js -> additional_methods/min.js
                    var ext = parts[^1];
                    var middle = parts[^2];
                    var left = string.Join('.', parts.Take(parts.Length - 2));
                    if (!string.IsNullOrEmpty(left))
                    {
                        var legacyDirName = left.Replace('-', '_');
                        var legacyPath = $"{dir}/{legacyDirName}/{middle}.{ext}";
                        candidates.Add($"/wwwroot{legacyPath}");
                        candidates.Add(legacyPath);
                    }
                }
            }

            // 2. 原始嵌入命名空间形式：Pek.DsMallUI.wwwroot.static.plugins.additional-methods.min.js
            //   请求里去掉 / 前缀与 wwwroot 前缀后尝试还原可能的嵌入全名。
            var trimmed = requestPath.TrimStart('/');
            if (trimmed.StartsWith("static/", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add($"Pek.DsMallUI.wwwroot.{trimmed.Replace('/', '.')}" );
            }

            // 3. 加 /wwwroot/ 前缀（已尝试过 combined），也尝试不含 wwwroot 的直接形式
            candidates.Add(requestPath);

            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in candidates.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                if (!reported.Add(c)) continue;
                var fi = _fileProvider.GetFileInfo(c);
                if (fi.Exists)
                {
                    XTrace.WriteLine("[WebVFS][Diag] Candidate MATCH path={0} Size={1}", c, fi.Length);
                }
                else
                {
                    XTrace.WriteLine("[WebVFS][Diag] Candidate miss path={0}", c);
                }
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("[WebVFS][Diag] Heuristic diagnostics error: {0}", ex.Message);
        }
    }

    private static bool _dumpedAllVirtualFiles; // 仅一次
    private void TryDumpAllVirtualFilesOnce()
    {
        if (!Options.DumpAllVirtualFilesOnFirstMiss)
        {
            XTrace.WriteLine("[WebVFS][Dump] 跳过：DumpAllVirtualFilesOnFirstMiss=false");
            return;
        }
        if (_dumpedAllVirtualFiles)
        {
            XTrace.WriteLine("[WebVFS][Dump] 跳过：已执行过初次转储");
            return;
        }
        _dumpedAllVirtualFiles = true;
        XTrace.WriteLine("[WebVFS][Dump] 开始转储虚拟文件键，DumpAllVirtualFilesOnFirstMiss=true");
        try
        {
            // 通过反射深入 _virtualFileProvider -> VirtualFileProvider.InternalVirtualFileProvider -> _files Lazy 字典
            var vpType = _virtualFileProvider.GetType();
            // 可能是 VirtualFileProvider 或一个包装，尝试获取 _hybridFileProvider 内部字段
            // 我们目标是 InternalVirtualFileProvider，其是 CompositeFileProvider 的子层之一

            // 方案：如果是 VirtualFileProvider，先取其私有字段 _hybridFileProvider，然后枚举其 providers
            var hybridField = vpType.GetField("_hybridFileProvider", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var hybrid = hybridField?.GetValue(_virtualFileProvider);
            var candidates = new List<IFileProvider>();
            if (hybrid is CompositeFileProvider composite)
            {
                // CompositeFileProvider 有私有字段 _fileProviders
                var innerField = typeof(CompositeFileProvider).GetField("_fileProviders", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (innerField?.GetValue(composite) is IEnumerable<IFileProvider> list)
                    candidates.AddRange(list);
            }

            var total = 0;
            if (candidates.Count == 0)
            {
                XTrace.WriteLine("[WebVFS][Dump] 未获取到内部 provider 列表，类型={0}", vpType.FullName);
            }
            else
            {
                var idx = 0;
                foreach (var p in candidates)
                {
                    XTrace.WriteLine("[WebVFS][Dump] Provider[{0}] Type={1}", idx++, p.GetType().FullName);
                }
            }
            foreach (var provider in candidates)
            {
                var pType = provider.GetType();
                if (!pType.Name.Contains("InternalVirtualFileProvider")) continue;
                // 找 _files (Lazy<Dictionary<string,IFileInfo>>)
                var filesField = pType.GetField("_files", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (filesField?.GetValue(provider) is Lazy<Dictionary<string, IFileInfo>> lazyDict)
                {
                    Dictionary<string, IFileInfo> dict = null;
                    try { dict = lazyDict.Value; } catch { }
                    if (dict != null)
                    {
                        XTrace.WriteLine("[WebVFS][Dump] Virtual file keys count={0}", dict.Count);
                        foreach (var kv in dict.OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            total++;
                            var size = kv.Value.Length;
                            var phys = kv.Value.GetVirtualOrPhysicalPathOrNull();
                            XTrace.WriteLine("[WebVFS][Dump] Key={0} Size={1} Path={2}", kv.Key, size, phys);
                        }
                    }
                }
            }
            if (total == 0)
            {
                XTrace.WriteLine("[WebVFS][Dump] 未发现可转储的虚拟文件键（可能 InternalVirtualFileProvider 未加入或字典为空）");
            }
        }
        catch (Exception ex)
        {
            XTrace.WriteLine("[WebVFS][Dump] 枚举虚拟文件失败: {0}", ex.Message);
        }
    }
}