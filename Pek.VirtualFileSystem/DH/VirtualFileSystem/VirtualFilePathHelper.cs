namespace Pek.VirtualFileSystem;

internal static class VirtualFilePathHelper
{
    /// <summary>规范化路径：仅统一分隔符、去重斜杠、补前导斜杠、去除末尾多余斜杠；不再拆分 '.' 或替换 '-'</summary>
    public static string NormalizePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return fullPath;

        // 统一分隔符
        var path = fullPath.Replace('\\', '/');

        // 去掉查询或片段（防御性，正常上游不应传进来）
        var q = path.IndexOfAny(new[] { '?', '#' });
        if (q >= 0) path = path.Substring(0, q);

        // 合并重复斜杠
        while (path.Contains("//")) path = path.Replace("//", "/");

        // 补前导斜杠
        if (!path.StartsWith('/')) path = "/" + path;

        // 去掉末尾斜杠（根除外）
        if (path.Length > 1 && path.EndsWith('/')) path = path.TrimEnd('/');

        return path;
    }
}
