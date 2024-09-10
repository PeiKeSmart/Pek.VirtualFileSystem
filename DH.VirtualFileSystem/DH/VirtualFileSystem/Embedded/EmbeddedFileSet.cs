using System.Reflection;

using Microsoft.Extensions.FileProviders;

using NewLife;

using Pek.VirtualFileSystem;

namespace DH.VirtualFileSystem.Embedded
{
    public class EmbeddedFileSet : IVirtualFileSet
    {
        public Assembly Assembly { get; }

        public string BaseNamespace { get; }

        public string BaseFolderInProject { get; }

        public EmbeddedFileSet(
            Assembly assembly,
            string baseNamespace = null,
            string baseFolderInProject = null)
        {

            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            Assembly = assembly;
            BaseNamespace = baseNamespace;
            BaseFolderInProject = baseFolderInProject;
        }

        public void AddFiles(Dictionary<string, IFileInfo> files)
        {
            var lastModificationTime = GetLastModificationTime();

            foreach (var resourcePath in Assembly.GetManifestResourceNames())
            {
                if (!BaseNamespace.IsNullOrEmpty() && !resourcePath.StartsWith(BaseNamespace))
                {
                    continue;
                }

                var fullPath = ConvertToRelativePath(resourcePath).EnsureStartsWith('/');

                if (fullPath.Contains("/"))
                {
                    AddDirectoriesRecursively(files, fullPath.Substring(0, fullPath.LastIndexOf('/')), lastModificationTime);
                }

                files[fullPath] = new EmbeddedResourceFileInfo(
                    Assembly,
                    resourcePath,
                    fullPath,
                    CalculateFileName(fullPath),
                    lastModificationTime
                );
            }
        }

        private static void AddDirectoriesRecursively(Dictionary<string, IFileInfo> files, string directoryPath, DateTimeOffset lastModificationTime)
        {
            if (files.ContainsKey(directoryPath))
            {
                return;
            }

            files[directoryPath] = new VirtualDirectoryFileInfo(
                directoryPath,
                CalculateFileName(directoryPath),
                lastModificationTime
            );

            if (directoryPath.Contains("/"))
            {
                AddDirectoriesRecursively(files, directoryPath.Substring(0, directoryPath.LastIndexOf('/')), lastModificationTime);
            }
        }

        private DateTimeOffset GetLastModificationTime()
        {
            var lastModified = DateTimeOffset.UtcNow;

            if (!string.IsNullOrEmpty(Assembly.Location))
            {
                try
                {
                    lastModified = File.GetLastWriteTimeUtc(Assembly.Location);
                }
                catch (PathTooLongException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return lastModified;
        }

        private string ConvertToRelativePath(string resourceName)
        {
            if (!BaseNamespace.IsNullOrEmpty())
            {
                resourceName = resourceName.Substring(BaseNamespace.Length + 1);
            }

            var pathParts = resourceName.Split('.');
            if (pathParts.Length <= 2)
            {
                return resourceName;
            }

            var folder = pathParts.Take(pathParts.Length - 2).JoinAsString("/");
            var fileName = pathParts[pathParts.Length - 2] + "." + pathParts[pathParts.Length - 1];

            return folder + "/" + fileName;
        }

        private static string CalculateFileName(string filePath)
        {
            if (!filePath.Contains("/"))
            {
                return filePath;
            }

            return filePath.Substring(filePath.LastIndexOf("/", StringComparison.Ordinal) + 1);
        }
    }
}
