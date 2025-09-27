using System.Collections;
using System.Reflection;
using System.Text;

using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.FileProviders.Embedded;
using Microsoft.Extensions.Primitives;

namespace Pek.VirtualFileSystem;

/// <summary>魔方嵌入文件提供者。优化某些特殊文件名的支持</summary>
public class CubeEmbeddedFileProvider : IFileProvider
{
    private static readonly Char[] _invalidFileNameChars = (from c in Path.GetInvalidFileNameChars()
                                                            where c != '/' && c != '\\'
                                                            select c).ToArray();

    private readonly Assembly _assembly;

    private readonly String _baseNamespace;

    private readonly DateTimeOffset _lastModified;

    /// <summary>实例化</summary>
    /// <param name="assembly"></param>
    /// <param name="baseNamespace"></param>
    public CubeEmbeddedFileProvider(Assembly assembly, String baseNamespace)
    {
        if (assembly == null) throw new ArgumentNullException(nameof(assembly));

        _baseNamespace = (String.IsNullOrEmpty(baseNamespace) ? String.Empty : (baseNamespace + "."));
        _assembly = assembly;
        _lastModified = DateTimeOffset.UtcNow;
        if (!String.IsNullOrEmpty(_assembly.Location))
        {
            try
            {
                _lastModified = File.GetLastWriteTimeUtc(_assembly.Location);
            }
            catch (PathTooLongException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>获取文件信息</summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IFileInfo GetFileInfo(String subpath)
    {
        if (String.IsNullOrEmpty(subpath)) return new NotFoundFileInfo(subpath);

        var sb = new StringBuilder(_baseNamespace.Length + subpath.Length);
        sb.Append(_baseNamespace);
        if (subpath.StartsWith("/", StringComparison.Ordinal))
            sb.Append(subpath, 1, subpath.Length - 1);
        else
            sb.Append(subpath);

        for (var i = _baseNamespace.Length; i < sb.Length; i++)
        {
            if (sb[i] == '/' || sb[i] == '\\') sb[i] = '.';
        }

        var text = sb.ToString();
        if (HasInvalidPathChars(text)) return new NotFoundFileInfo(text);

        var fileName = Path.GetFileName(subpath);
        if (_assembly.GetManifestResourceInfo(text) != null)
            return new EmbeddedResourceFileInfo(_assembly, text, fileName, _lastModified);

        // 新规范：不再对 '-' 或 '@' 做下划线替换回退，路径需与资源名精确匹配
        return new NotFoundFileInfo(fileName);
    }

    /// <summary>获取目录内容</summary>
    /// <param name="subpath"></param>
    /// <returns></returns>
    public IDirectoryContents GetDirectoryContents(String subpath)
    {
        if (subpath == null) return NotFoundDirectoryContents.Singleton;

        if (subpath.Length != 0 && !String.Equals(subpath, "/", StringComparison.Ordinal)) return NotFoundDirectoryContents.Singleton;

        var list = new List<IFileInfo>();
        var manifestResourceNames = _assembly.GetManifestResourceNames();
        foreach (var text in manifestResourceNames)
        {
            if (text.StartsWith(_baseNamespace, StringComparison.Ordinal))
            {
                list.Add(new EmbeddedResourceFileInfo(_assembly, text, text.Substring(_baseNamespace.Length), _lastModified));
            }
        }
        return new EnumerableDirectoryContents(list);
    }

    /// <summary>监视</summary>
    /// <param name="pattern"></param>
    /// <returns></returns>
    public IChangeToken Watch(String pattern) => NullChangeToken.Singleton;

    private static Boolean HasInvalidPathChars(String path) => path.IndexOfAny(_invalidFileNameChars) != -1;

    internal class EnumerableDirectoryContents : IDirectoryContents, IEnumerable<IFileInfo>, IEnumerable
    {
        private readonly IEnumerable<IFileInfo> _entries;

        public Boolean Exists => true;

        //[System.Runtime.CompilerServices.NullableContext(1)]
        public EnumerableDirectoryContents(IEnumerable<IFileInfo> entries) => _entries = entries ?? throw new ArgumentNullException(nameof(_entries));

        public IEnumerator<IFileInfo> GetEnumerator() => _entries.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => _entries.GetEnumerator();
    }
}
