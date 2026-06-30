using Domain.Events;
using Application.Abstractions;
using Application.Devices.Commands;
using Application.Devices.Models;
using Application.Devices.Projections;
using Application.Devices.Queries;
using Domain.Abstractions;

namespace Application.Tests;

public class UnitTest1
{
    [Fact]
    public async Task Register_And_Query_Should_Return_Active_Status()
    {
        var eventStore = new FakeEventStore();
        var readRepository = new FakeReadRepository();
        var projector = new InMemoryDeviceEventProjector(readRepository);
        var registerHandler = new RegisterDeviceCommandHandler(eventStore, projector);
        var queryHandler = new GetDeviceStatusQueryHandler(readRepository);

        var id = await registerHandler.Handle(new RegisterDeviceCommand("D-100", 90), CancellationToken.None);
        var view = await queryHandler.Handle(new GetDeviceStatusQuery(id), CancellationToken.None);

        Assert.NotNull(view);
        Assert.Equal("Active", view!.Status);
    }

    [Fact]
    public async Task Start_Command_Should_Append_DeviceStarted_Event()
    {
        var eventStore = new FakeEventStore();
        var readRepository = new FakeReadRepository();
        var projector = new InMemoryDeviceEventProjector(readRepository);
        var registerHandler = new RegisterDeviceCommandHandler(eventStore, projector);
        var startHandler = new StartDeviceCommandHandler(eventStore, projector);

        var id = await registerHandler.Handle(new RegisterDeviceCommand("D-101", 100), CancellationToken.None);
        await startHandler.Handle(new StartDeviceCommand(id, 40), CancellationToken.None);

        var history = await eventStore.LoadAsync(id, CancellationToken.None);
        Assert.Contains(history, e => e is DeviceStarted);
    }

    private sealed class FakeEventStore : IEventStore
    {
        private readonly Dictionary<Guid, List<StoredEvent>> _streams = new();

        public Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
        {
            if (_streams.TryGetValue(streamId, out var stream))
            {
                return Task.FromResult<IReadOnlyList<IDomainEvent>>(stream.Select(x => x.DomainEvent).ToArray());
            }

            return Task.FromResult<IReadOnlyList<IDomainEvent>>(Array.Empty<IDomainEvent>());
        }

        public Task<IReadOnlyList<StoredEvent>> LoadAllAsync(CancellationToken cancellationToken)
        {
            var all = _streams.Values
                .SelectMany(x => x)
                .OrderBy(x => x.StreamId)
                .ThenBy(x => x.Version)
                .ToArray();

            return Task.FromResult<IReadOnlyList<StoredEvent>>(all);
        }

        public Task AppendAsync(
            Guid streamId,
            int expectedVersion,
            IReadOnlyCollection<IDomainEvent> events,
            CommandMetadata? commandMetadata,
            CancellationToken cancellationToken)
        {
            if (!_streams.TryGetValue(streamId, out var stream))
            {
                stream = new List<StoredEvent>();
                _streams[streamId] = stream;
            }

            var actualVersion = stream.Count - 1;
            if (actualVersion != expectedVersion)
            {
                throw new InvalidOperationException("Version mismatch.");
            }

            var nextVersion = expectedVersion + 1;
            foreach (var domainEvent in events)
            {
                stream.Add(new StoredEvent(streamId, nextVersion, domainEvent, commandMetadata));
                nextVersion++;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryDeviceEventProjector(FakeReadRepository readRepository) : IDeviceEventProjector
    {
        private readonly FakeReadRepository _readRepository = readRepository;

        public async Task ProjectAsync(IReadOnlyList<StoredEvent> events, CancellationToken cancellationToken)
        {
            foreach (var storedEvent in events.OrderBy(x => x.Version))
            {
                var current = await _readRepository.GetAsync(storedEvent.StreamId, cancellationToken);
                var next = storedEvent.DomainEvent switch
                {
                    DeviceRegistered e => new DeviceStatusView(e.DeviceId, e.DeviceCode, "Active", e.OccurredOnUtc, e.MaxTemperatureThreshold),
                    DeviceStarted e when current is not null => current with { Status = "Running", LastHeartbeatUtc = e.StartedOnUtc },
                    DeviceStopped e when current is not null => current with { Status = "Active", LastHeartbeatUtc = e.StoppedOnUtc },
                    DeviceMaintenanceModeChanged e when current is not null => current with
                    {
                        Status = e.IsEnabled ? "Maintenance" : "Active",
                        LastHeartbeatUtc = e.ChangedOnUtc
                    },
                    _ => current
                };

                if (next is not null)
                {
                    await _readRepository.UpsertAsync(next, cancellationToken);
                }
            }
        }

        public Task ResetAsync(CancellationToken cancellationToken)
        {
            _readRepository.Clear();
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReadRepository : IDeviceStatusReadRepository
    {
        private readonly Dictionary<Guid, DeviceStatusView> _views = new();

        public void Clear()
        {
            _views.Clear();
        }

        public Task<IReadOnlyList<DeviceStatusView>> GetAllAsync(CancellationToken cancellationToken)
        {
            var items = _views.Values
                .OrderBy(x => x.DeviceCode)
                .ToArray();

            return Task.FromResult<IReadOnlyList<DeviceStatusView>>(items);
        }

        public Task<DeviceStatusView?> GetAsync(Guid deviceId, CancellationToken cancellationToken)
        {
            _views.TryGetValue(deviceId, out var view);
            return Task.FromResult(view);
        }

        public Task UpsertAsync(DeviceStatusView view, CancellationToken cancellationToken)
        {
            _views[view.DeviceId] = view;
            return Task.CompletedTask;
        }
    }
}
