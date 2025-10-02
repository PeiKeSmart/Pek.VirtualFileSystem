namespace Pek.VirtualFileSystem;

public class DHAspNetCoreContentOptions
{
    public List<string> AllowedExtraWebContentFolders { get; }
    public List<string> AllowedExtraWebContentFileExtensions { get; }
    /// <summary>记录未命中（文件/目录/监视）日志，默认开启。仅在访问失败时输出一条。</summary>
    public bool LogFileMisses { get; set; } = true;
    /// <summary>记录命中（文件/目录）日志，默认关闭。开启后可与未命中对比。</summary>
    public bool LogFileHits { get; set; } = true;
    /// <summary>首次文件 MISS 时转储所有虚拟文件键（只执行一次），用于诊断某个特定资源是否已注册。</summary>
    // 默认开启一次性转储，方便初期排查；使用方可在生产环境关闭以减少日志。
    public bool DumpAllVirtualFilesOnFirstMiss { get; set; } = true;

    public DHAspNetCoreContentOptions()
    {
        AllowedExtraWebContentFolders = new List<string>
            {
                "/Pages",
                "/Views",
                "/Themes"
            };

        AllowedExtraWebContentFileExtensions = new List<string>
            {
                ".js",
                ".css",
                ".png",
                ".jpg",
                ".jpeg",
                ".woff",
                ".woff2",
                ".tff",
                ".otf"
            };

        LogFileMisses = true;
        LogFileHits = true;
    }
}