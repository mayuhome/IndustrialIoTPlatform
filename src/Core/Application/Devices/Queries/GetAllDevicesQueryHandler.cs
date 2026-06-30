using Application.Abstractions;
using Application.Devices.Models;

namespace Application.Devices.Queries;

public sealed class GetAllDevicesQueryHandler(IDeviceStatusReadRepository readRepository)
{
    private readonly IDeviceStatusReadRepository _readRepository = readRepository;

    public Task<IReadOnlyList<DeviceStatusView>> Handle(GetAllDevicesQuery query, CancellationToken cancellationToken)
    {
        return _readRepository.GetAllAsync(cancellationToken);
    }
}
