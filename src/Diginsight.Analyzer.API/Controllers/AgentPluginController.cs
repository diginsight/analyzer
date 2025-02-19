using Diginsight.Analyzer.API.Attributes;
using Diginsight.Analyzer.Business;
using Diginsight.Analyzer.Business.Models;
using Microsoft.AspNetCore.Mvc;
using System.Runtime.CompilerServices;
using StreamOpener = System.Func<System.Threading.CancellationToken, System.Threading.Tasks.ValueTask<System.IO.Stream>>;

namespace Diginsight.Analyzer.API.Controllers;

[Flavor(Flavor.AgentOnly)]
[Route("plugins")]
public sealed class AgentPluginController : ControllerBase
{
    private readonly IPluginService pluginService;
    private readonly IPermissionService permissionService;
    private readonly IHttpClientFactory httpClientFactory;

    public AgentPluginController(
        IPluginService pluginService,
        IPermissionService permissionService,
        IHttpClientFactory httpClientFactory
    )
    {
        this.pluginService = pluginService;
        this.permissionService = permissionService;
        this.httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    public IActionResult GetAll()
    {
        return Ok(pluginService.GetAll());
    }

    [HttpPost]
    public async Task<IActionResult> Register()
    {
        CancellationToken cancellationToken = HttpContext.RequestAborted;

        await permissionService.CheckCanManagePluginsAsync(cancellationToken);

        ICollection<IAsyncDisposable> disposables = new List<IAsyncDisposable>();
        try
        {
            IAsyncEnumerable<StreamOpener> streamOpeners;
            if (Request.HasFormContentType)
            {
                streamOpeners = (await Request.ReadFormFilesAsync(cancellationToken))
                    .ToAsyncEnumerable()
                    .Where(static ff => ff.Name.StartsWith("assembly:", StringComparison.OrdinalIgnoreCase))
                    .SelectAwaitWithCancellation(async (ff, ct) => await FollowLocationAsync(ff, ct));
            }
            else
            {
                streamOpeners = new[] { await FollowLocationAsync(null, cancellationToken) }.ToAsyncEnumerable();
            }

            IReadOnlyCollection<Stream> streams = await streamOpeners
                .SelectAwaitWithCancellation(
                    async (streamOpener, ct) =>
                    {
                        Stream finalStream = new MemoryStream();
                        await using (Stream fileStream = await streamOpener(ct))
                        {
                            await fileStream.CopyToAsync(finalStream, ct);
                        }

                        finalStream.Position = 0;
                        disposables.Add(finalStream);
                        return finalStream;
                    }
                )
                .ToArrayAsync(cancellationToken);

            Plugin plugin = pluginService.Register(streams);
            return Ok(plugin);
        }
        finally
        {
            foreach (IAsyncDisposable disposable in disposables)
            {
                await disposable.DisposeAsync();
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task<StreamOpener> FollowLocationAsync(IFormFile? ff, CancellationToken cancellationToken)
    {
        return (await ApiUtils.FollowLocationAsync(
                httpClientFactory.CreateClient(typeof(AgentPluginController).FullName!), Request, ff, true, cancellationToken
            ))
            .OpenStreamAsync;
    }

    [HttpDelete("{pluginId:guid}")]
    public async Task<IActionResult> Unregister(Guid pluginId)
    {
        await permissionService.CheckCanManagePluginsAsync(CancellationToken.None);

        pluginService.Unregister(pluginId);
        return NoContent();
    }
}
