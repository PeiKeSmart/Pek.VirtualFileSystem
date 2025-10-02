namespace Pek.VirtualFileSystem;

public class DHAspNetCoreContentOptions
{
    public List<string> AllowedExtraWebContentFolders { get; }
    public List<string> AllowedExtraWebContentFileExtensions { get; }
    /// <summary>记录未命中（文件/目录/监视）日志，默认开启。仅在访问失败时输出一条。</summary>
    public bool LogFileMisses { get; set; } = true;
    /// <summary>记录命中（文件/目录）日志，默认关闭。开启后可与未命中对比。</summary>
    public bool LogFileHits { get; set; } = true;

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