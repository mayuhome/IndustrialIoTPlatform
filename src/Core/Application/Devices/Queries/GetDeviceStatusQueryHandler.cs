using Application.Abstractions;
using Application.Devices.Models;

namespace Application.Devices.Queries;

public sealed class GetDeviceStatusQueryHandler
{
    private readonly IDeviceStatusReadRepository _readRepository;

    public GetDeviceStatusQueryHandler(IDeviceStatusReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public Task<DeviceStatusView?> Handle(GetDeviceStatusQuery query, CancellationToken cancellationToken)
    {
        return _readRepository.GetAsync(query.DeviceId, cancellationToken);
    }
}
