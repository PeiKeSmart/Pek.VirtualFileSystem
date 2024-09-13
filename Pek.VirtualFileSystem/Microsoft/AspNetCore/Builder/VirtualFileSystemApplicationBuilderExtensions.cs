using Microsoft.Extensions.DependencyInjection;

using Pek.VirtualFileSystem;

namespace Microsoft.AspNetCore.Builder;

public static class VirtualFileSystemApplicationBuilderExtensions
{
    public static IApplicationBuilder UseVirtualFiles(this IApplicationBuilder app)
    {
        return app.UseStaticFiles(
            new StaticFileOptions
            {
                FileProvider = app.ApplicationServices.GetRequiredService<IWebContentFileProvider>()
            }
        );
    }
}
