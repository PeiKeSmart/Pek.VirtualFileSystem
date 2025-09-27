using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;
using NewLife.Log;

namespace Pek.VirtualFileSystem;

public abstract class DictionaryBasedFileProvider : IFileProvider
{
    protected abstract IDictionary<string, IFileInfo> Files { get; }

    private static Int32 _missSamplePrinted;

    public virtual IFileInfo GetFileInfo(string subpath)
    {
        if (string.IsNullOrEmpty(subpath))
        {
            return new NotFoundFileInfo(subpath);
        }

        var normalized = NormalizePath(subpath);
        var file = Files.GetOrDefault(normalized);

        // 前导斜杠回退：如果未命中，尝试在有/无前导斜杠之间切换再查一次
        if (file == null)
        {
            string alt;
            if (normalized.StartsWith('/'))
                alt = normalized.TrimStart('/');
            else
                alt = "/" + normalized;

            if (!String.Equals(alt, normalized, StringComparison.Ordinal))
            {
                file = Files.GetOrDefault(alt);
                if (file != null) return file;
            }
        }

        if (file == null)
        {
            XTrace.WriteLine("[VirtualFileMiss] requested={0} normalized={1}", subpath, normalized);

            // 首次 miss 额外输出样本，帮助确认字典中键的真实形态
            if (System.Threading.Interlocked.CompareExchange(ref _missSamplePrinted, 1, 0) == 0)
            {
                try
                {
                    var total = Files.Count;
                    var sample = string.Join("; ", Files.Keys.Take(8));
                    XTrace.WriteLine("[VirtualFileKeysSample] total={0} sample=[{1}]", total, sample);
                }
                catch (Exception ex)
                {
                    XTrace.WriteException(ex);
                }
            }

            return new NotFoundFileInfo(subpath);
        }

        return file;
    }

    public virtual IDirectoryContents GetDirectoryContents(string subpath)
    {
        var directory = GetFileInfo(subpath);
        if (!directory.IsDirectory)
        {
            return NotFoundDirectoryContents.Singleton;
        }

        var fileList = new List<IFileInfo>();

        var directoryPath = subpath.EnsureEndsWith('/');
        foreach (var fileInfo in Files.Values)
        {
            var fullPath = fileInfo.GetVirtualOrPhysicalPathOrNull();
            if (!fullPath.StartsWith(directoryPath))
            {
                continue;
            }

            var relativePath = fullPath.Substring(directoryPath.Length);
            if (relativePath.Contains("/"))
            {
                continue;
            }

            fileList.Add(fileInfo);
        }

        return new EnumerableDirectoryContents(fileList);
    }

    public virtual IChangeToken Watch(string filter)
    {
        return NullChangeToken.Singleton;
    }

    protected virtual string NormalizePath(string subpath)
    {
        return subpath;
    }
}
