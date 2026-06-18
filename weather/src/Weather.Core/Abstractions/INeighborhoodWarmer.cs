using System.Threading.Channels;
using Weather.Core.Models;

namespace Weather.Core.Abstractions;

/// <summary>
/// A bounded, in-process work queue. Producers (request handlers) drop the
/// user's origin cell in; a background consumer fans it out to neighbours and
/// warms them under a global rate limit.
/// </summary>
public interface INeighborhoodWarmer
{
    /// <summary>Schedule the cells around <paramref name="origin"/> to be warmed. Non-blocking.</summary>
    void RequestWarming(GridPoint origin);

    /// <summary>Consumed by the background service. Not used by request handlers.</summary>
    ChannelReader<GridPoint> Reader { get; }
}
