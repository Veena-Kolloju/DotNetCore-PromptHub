---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Event-Driven Architecture Framework

Implement comprehensive event-driven architecture with domain events, event sourcing, and CQRS patterns for .NET Core applications.

## Requirements

### 1. Domain Events
- Event definition and publishing
- Event handlers and subscribers
- Event store implementation
- Event replay capabilities

### 2. Event Sourcing
- Aggregate root with event sourcing
- Event stream management
- Snapshot creation and restoration
- Projection building

## Example Implementation

### Domain Events
```csharp
public interface IDomainEvent
{
    Guid Id { get; }
    DateTime OccurredAt { get; }
    string EventType { get; }
    int Version { get; }
}

public abstract class DomainEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
    public string EventType => GetType().Name;
    public int Version { get; set; } = 1;
}

public class CustomerCreatedEvent : DomainEvent
{
    public int CustomerId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public CustomerType Type { get; set; }
}

public class CustomerPromotedEvent : DomainEvent
{
    public int CustomerId { get; set; }
    public CustomerType FromType { get; set; }
    public CustomerType ToType { get; set; }
    public string Reason { get; set; }
}
```

### Event Sourced Aggregate
```csharp
public abstract class EventSourcedAggregateRoot
{
    private readonly List<IDomainEvent> _uncommittedEvents = new();
    
    public int Id { get; protected set; }
    public int Version { get; protected set; }

    public IReadOnlyList<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    protected void RaiseEvent(IDomainEvent @event)
    {
        @event.Version = Version + 1;
        _uncommittedEvents.Add(@event);
        ApplyEvent(@event);
        Version++;
    }

    public void LoadFromHistory(IEnumerable<IDomainEvent> events)
    {
        foreach (var @event in events.OrderBy(e => e.Version))
        {
            ApplyEvent(@event);
            Version = @event.Version;
        }
    }

    public void MarkEventsAsCommitted()
    {
        _uncommittedEvents.Clear();
    }

    protected abstract void ApplyEvent(IDomainEvent @event);
}

public class Customer : EventSourcedAggregateRoot
{
    public string Name { get; private set; }
    public Email Email { get; private set; }
    public CustomerType Type { get; private set; }
    public CustomerStatus Status { get; private set; }

    private Customer() { } // For event sourcing

    public Customer(string name, Email email)
    {
        RaiseEvent(new CustomerCreatedEvent
        {
            CustomerId = Id,
            Name = name,
            Email = email.Value,
            Type = CustomerType.Regular
        });
    }

    public void PromoteToVip(string reason)
    {
        if (Type == CustomerType.Vip)
            throw new DomainException("Customer is already VIP");

        RaiseEvent(new CustomerPromotedEvent
        {
            CustomerId = Id,
            FromType = Type,
            ToType = CustomerType.Vip,
            Reason = reason
        });
    }

    protected override void ApplyEvent(IDomainEvent @event)
    {
        switch (@event)
        {
            case CustomerCreatedEvent created:
                Id = created.CustomerId;
                Name = created.Name;
                Email = new Email(created.Email);
                Type = created.Type;
                Status = CustomerStatus.Active;
                break;

            case CustomerPromotedEvent promoted:
                Type = promoted.ToType;
                break;
        }
    }
}
```

### Event Store
```csharp
public interface IEventStore
{
    Task<IEnumerable<IDomainEvent>> GetEventsAsync(int aggregateId, int fromVersion = 0);
    Task SaveEventsAsync(int aggregateId, IEnumerable<IDomainEvent> events, int expectedVersion);
    Task<T> GetAggregateAsync<T>(int aggregateId) where T : EventSourcedAggregateRoot, new();
    Task SaveAggregateAsync<T>(T aggregate) where T : EventSourcedAggregateRoot;
}

public class EventStore : IEventStore
{
    private readonly ApplicationDbContext _context;
    private readonly IEventSerializer _serializer;

    public EventStore(ApplicationDbContext context, IEventSerializer serializer)
    {
        _context = context;
        _serializer = serializer;
    }

    public async Task<IEnumerable<IDomainEvent>> GetEventsAsync(int aggregateId, int fromVersion = 0)
    {
        var eventRecords = await _context.EventStore
            .Where(e => e.AggregateId == aggregateId && e.Version > fromVersion)
            .OrderBy(e => e.Version)
            .ToListAsync();

        return eventRecords.Select(r => _serializer.Deserialize(r.EventData, r.EventType));
    }

    public async Task SaveEventsAsync(int aggregateId, IEnumerable<IDomainEvent> events, int expectedVersion)
    {
        var currentVersion = await GetCurrentVersionAsync(aggregateId);
        
        if (currentVersion != expectedVersion)
        {
            throw new ConcurrencyException($"Expected version {expectedVersion}, but current version is {currentVersion}");
        }

        var eventRecords = events.Select(e => new EventRecord
        {
            AggregateId = aggregateId,
            EventId = e.Id,
            EventType = e.EventType,
            EventData = _serializer.Serialize(e),
            Version = e.Version,
            OccurredAt = e.OccurredAt
        });

        _context.EventStore.AddRange(eventRecords);
        await _context.SaveChangesAsync();
    }

    public async Task<T> GetAggregateAsync<T>(int aggregateId) where T : EventSourcedAggregateRoot, new()
    {
        var events = await GetEventsAsync(aggregateId);
        
        if (!events.Any())
            return null;

        var aggregate = new T();
        aggregate.LoadFromHistory(events);
        return aggregate;
    }

    public async Task SaveAggregateAsync<T>(T aggregate) where T : EventSourcedAggregateRoot
    {
        if (!aggregate.UncommittedEvents.Any())
            return;

        await SaveEventsAsync(aggregate.Id, aggregate.UncommittedEvents, aggregate.Version - aggregate.UncommittedEvents.Count);
        aggregate.MarkEventsAsCommitted();
    }

    private async Task<int> GetCurrentVersionAsync(int aggregateId)
    {
        return await _context.EventStore
            .Where(e => e.AggregateId == aggregateId)
            .MaxAsync(e => (int?)e.Version) ?? 0;
    }
}
```

### Event Handlers
```csharp
public interface IEventHandler<in T> where T : IDomainEvent
{
    Task HandleAsync(T @event, CancellationToken cancellationToken = default);
}

public class CustomerCreatedEventHandler : IEventHandler<CustomerCreatedEvent>
{
    private readonly IEmailService _emailService;
    private readonly ILogger<CustomerCreatedEventHandler> _logger;

    public CustomerCreatedEventHandler(IEmailService emailService, ILogger<CustomerCreatedEventHandler> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    public async Task HandleAsync(CustomerCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            await _emailService.SendWelcomeEmailAsync(@event.Email, @event.Name, cancellationToken);
            _logger.LogInformation("Welcome email sent for customer {CustomerId}", @event.CustomerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email for customer {CustomerId}", @event.CustomerId);
            throw;
        }
    }
}

public class CustomerPromotedEventHandler : IEventHandler<CustomerPromotedEvent>
{
    private readonly INotificationService _notificationService;
    private readonly ILogger<CustomerPromotedEventHandler> _logger;

    public CustomerPromotedEventHandler(INotificationService notificationService, ILogger<CustomerPromotedEventHandler> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task HandleAsync(CustomerPromotedEvent @event, CancellationToken cancellationToken = default)
    {
        await _notificationService.SendPromotionNotificationAsync(@event.CustomerId, @event.ToType, cancellationToken);
        _logger.LogInformation("Promotion notification sent for customer {CustomerId}", @event.CustomerId);
    }
}
```

### Event Bus
```csharp
public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IDomainEvent;
    Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler) where T : IDomainEvent;
}

public class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider serviceProvider, ILogger<InMemoryEventBus> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : IDomainEvent
    {
        var handlers = _serviceProvider.GetServices<IEventHandler<T>>();
        
        var tasks = handlers.Select(async handler =>
        {
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling event {EventType} with handler {HandlerType}", 
                    typeof(T).Name, handler.GetType().Name);
                throw;
            }
        });

        await Task.WhenAll(tasks);
    }

    public Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler) where T : IDomainEvent
    {
        // Implementation for dynamic subscription
        throw new NotImplementedException("Use dependency injection for handler registration");
    }
}
```

### Projections
```csharp
public class CustomerProjection
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Type { get; set; }
    public string Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastPromotedAt { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
}

public class CustomerProjectionHandler : 
    IEventHandler<CustomerCreatedEvent>,
    IEventHandler<CustomerPromotedEvent>,
    IEventHandler<OrderCreatedEvent>
{
    private readonly ApplicationDbContext _context;

    public CustomerProjectionHandler(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task HandleAsync(CustomerCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var projection = new CustomerProjection
        {
            Id = @event.CustomerId,
            Name = @event.Name,
            Email = @event.Email,
            Type = @event.Type.ToString(),
            Status = "Active",
            CreatedAt = @event.OccurredAt,
            TotalOrders = 0,
            TotalSpent = 0
        };

        _context.CustomerProjections.Add(projection);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task HandleAsync(CustomerPromotedEvent @event, CancellationToken cancellationToken = default)
    {
        var projection = await _context.CustomerProjections.FindAsync(@event.CustomerId);
        if (projection != null)
        {
            projection.Type = @event.ToType.ToString();
            projection.LastPromotedAt = @event.OccurredAt;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        var projection = await _context.CustomerProjections.FindAsync(@event.CustomerId);
        if (projection != null)
        {
            projection.TotalOrders++;
            projection.TotalSpent += @event.TotalAmount;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
```

## Deliverables

1. **Domain Events**: Event definitions and base classes
2. **Event Sourced Aggregates**: Aggregate roots with event sourcing
3. **Event Store**: Persistent event storage implementation
4. **Event Handlers**: Event processing and side effects
5. **Event Bus**: Event publishing and subscription
6. **Projections**: Read model generation from events
7. **Snapshots**: Aggregate state snapshots for performance
8. **Event Replay**: Historical event processing
9. **Event Versioning**: Event schema evolution
10. **Monitoring**: Event processing monitoring and metrics

## Validation Checklist

- [ ] Domain events properly defined and versioned
- [ ] Event sourced aggregates maintain consistency
- [ ] Event store persists events reliably
- [ ] Event handlers process events correctly
- [ ] Event bus delivers events to all subscribers
- [ ] Projections stay synchronized with events
- [ ] Snapshots improve aggregate loading performance
- [ ] Event replay functionality works correctly
- [ ] Event versioning handles schema changes
- [ ] Monitoring tracks event processing health