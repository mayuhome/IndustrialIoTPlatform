using Application.Abstractions;

namespace Application.Devices.Projections;

public sealed class RebuildDeviceReadModelHandler(
    IEventStore eventStore,
    IDeviceEventProjector projector)
{
    private readonly IEventStore _eventStore = eventStore;
    private readonly IDeviceEventProjector _projector = projector;

    public async Task<int> Handle(CancellationToken cancellationToken)
    {
        var allEvents = await _eventStore.LoadAllAsync(cancellationToken);

        await _projector.ResetAsync(cancellationToken);
        await _projector.ProjectAsync(allEvents, cancellationToken);

        return allEvents.Count;
    }
}
