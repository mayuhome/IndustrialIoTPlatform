namespace Domain.Abstractions;

public abstract class EventSourcedAggregateRoot
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; }
    public int Version { get; private set; } = -1;

    public void LoadFromHistory(IEnumerable<IDomainEvent> history)
    {
        foreach (var domainEvent in history)
        {
            Apply(domainEvent);
            Version++;
        }
    }

    public IReadOnlyCollection<IDomainEvent> DequeueUncommittedEvents()
    {
        var copy = _uncommittedEvents.ToArray();
        _uncommittedEvents.Clear();
        return copy;
    }

    protected void Raise(IDomainEvent domainEvent)
    {
        Apply(domainEvent);
        _uncommittedEvents.Add(domainEvent);
    }

    protected abstract void Apply(IDomainEvent domainEvent);
}
