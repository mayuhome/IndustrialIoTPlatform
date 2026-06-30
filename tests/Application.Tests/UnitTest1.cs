using Domain.Events;
using Application.Abstractions;
using Application.Devices.Commands;
using Application.Devices.Models;
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
        var registerHandler = new RegisterDeviceCommandHandler(eventStore, readRepository);
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
        var registerHandler = new RegisterDeviceCommandHandler(eventStore, readRepository);
        var startHandler = new StartDeviceCommandHandler(eventStore, readRepository);

        var id = await registerHandler.Handle(new RegisterDeviceCommand("D-101", 100), CancellationToken.None);
        await startHandler.Handle(new StartDeviceCommand(id, 40), CancellationToken.None);

        var history = await eventStore.LoadAsync(id, CancellationToken.None);
        Assert.Contains(history, e => e is DeviceStarted);
    }

    private sealed class FakeEventStore : IEventStore
    {
        private readonly Dictionary<Guid, List<IDomainEvent>> _streams = new();

        public Task<IReadOnlyList<IDomainEvent>> LoadAsync(Guid streamId, CancellationToken cancellationToken)
        {
            if (_streams.TryGetValue(streamId, out var stream))
            {
                return Task.FromResult<IReadOnlyList<IDomainEvent>>(stream.ToList());
            }

            return Task.FromResult<IReadOnlyList<IDomainEvent>>(Array.Empty<IDomainEvent>());
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
                stream = new List<IDomainEvent>();
                _streams[streamId] = stream;
            }

            var actualVersion = stream.Count - 1;
            if (actualVersion != expectedVersion)
            {
                throw new InvalidOperationException("Version mismatch.");
            }

            stream.AddRange(events);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeReadRepository : IDeviceStatusReadRepository
    {
        private readonly Dictionary<Guid, DeviceStatusView> _views = new();

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
