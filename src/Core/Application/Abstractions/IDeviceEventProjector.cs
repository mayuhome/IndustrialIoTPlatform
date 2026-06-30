namespace Application.Abstractions;

public interface IDeviceEventProjector
{
    Task ProjectAsync(IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken);

    Task ResetAsync(CancellationToken cancellationToken);
}
