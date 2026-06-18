using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Weather.Core.Abstractions;
using Weather.Core.Models;

namespace Weather.Infrastructure.Services;

/// <summary>
/// A bounded, in-process work queue (singleton). Request handlers drop the
/// user's origin cell in via <see cref="RequestWarming"/>; the background
/// service reads it and fans out to the surrounding cells. The queue is
/// bounded and drops the oldest entry under pressure, so a traffic spike can
/// never grow memory without limit or block the request path.
/// </summary>
internal sealed class NeighborhoodWarmer : INeighborhoodWarmer
{
    private readonly Channel<GridPoint> _channel;
    private readonly ILogger<NeighborhoodWarmer> _logger;

    public NeighborhoodWarmer(ILogger<NeighborhoodWarmer> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<GridPoint>(new BoundedChannelOptions(capacity: 256)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });
    }

    public ChannelReader<GridPoint> Reader => _channel.Reader;

    public void RequestWarming(GridPoint origin)
    {
        if (!_channel.Writer.TryWrite(origin) && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("Neighbourhood warm queue closed; dropped origin {Origin}.", origin);
        }
    }
}
