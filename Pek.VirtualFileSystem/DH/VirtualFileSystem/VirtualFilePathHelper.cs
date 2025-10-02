namespace Pek.VirtualFileSystem;

internal static class VirtualFilePathHelper
{
    // 方案3：新项目直接禁用“点号拆分”与目录段连字符替换；
    // 目标：保证嵌入资源文件名（含多点/连字符/版本号/hash）原样可通过 URL 访问。
    // 旧行为回顾（已移除）：
    //   1) 去除扩展后将中间 '.' 全部替换为 '/' 形成伪目录结构；
    //   2) 对目录段内 '-' 转为 '_'；
    //   该逻辑会把 additional-methods.min.js 拆成 additional_methods/min.js 导致无法匹配。
    // 回退策略：如需恢复旧逻辑，可从版本控制中取回本文件旧实现，或实现一个可配置开关分支。
    public static string NormalizePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath)) return fullPath;

        // 标准化：统一使用正斜杠，去除重复斜杠，确保以 '/' 开头（除非原本是相对路径，这里保持原样以避免破坏调用方假设）。
        var path = fullPath.Replace('\\', '/');

        // 折叠连续 '/'
        while (path.Contains("//")) path = path.Replace("//", "/");

        // 不做任何 '.' 拆分、不做 '-' 替换，直接返回。
        return path;
    }
}
