---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Microservices Architecture Framework

Design and implement microservices architecture for .NET Core applications with service discovery, API gateway, and inter-service communication.

## Requirements

### 1. Service Design
- Domain-driven service boundaries
- Independent deployment and scaling
- Database per service pattern
- Service contracts and versioning

### 2. Communication Patterns
- Synchronous HTTP communication
- Asynchronous message-based communication
- Event-driven architecture
- Saga pattern for distributed transactions

## Example Implementation

### Service Base Structure
```csharp
// Shared Contracts
public interface ICustomerService
{
    Task<CustomerDto> GetCustomerAsync(int id);
    Task<CustomerDto> CreateCustomerAsync(CreateCustomerRequest request);
}

public class CustomerDto
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public CustomerType Type { get; set; }
}

// Service Implementation
[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(int id)
    {
        var customer = await _customerService.GetCustomerAsync(id);
        return customer != null ? Ok(customer) : NotFound();
    }

    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(CreateCustomerRequest request)
    {
        var customer = await _customerService.CreateCustomerAsync(request);
        return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
    }
}
```

### Service Discovery
```csharp
public class ServiceDiscovery
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public ServiceDiscovery(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<string> GetServiceUrlAsync(string serviceName)
    {
        // Consul service discovery
        var consulUrl = _configuration["Consul:Url"];
        var response = await _httpClient.GetAsync($"{consulUrl}/v1/catalog/service/{serviceName}");
        
        if (response.IsSuccessStatusCode)
        {
            var services = await response.Content.ReadFromJsonAsync<ServiceInstance[]>();
            var service = services?.FirstOrDefault();
            return service != null ? $"http://{service.ServiceAddress}:{service.ServicePort}" : null;
        }

        return null;
    }
}

public class ServiceInstance
{
    public string ServiceName { get; set; }
    public string ServiceAddress { get; set; }
    public int ServicePort { get; set; }
}
```

### Inter-Service Communication
```csharp
public class OrderService
{
    private readonly HttpClient _httpClient;
    private readonly ServiceDiscovery _serviceDiscovery;
    private readonly IEventBus _eventBus;

    public OrderService(HttpClient httpClient, ServiceDiscovery serviceDiscovery, IEventBus eventBus)
    {
        _httpClient = httpClient;
        _serviceDiscovery = serviceDiscovery;
        _eventBus = eventBus;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        // Synchronous call to Customer Service
        var customerServiceUrl = await _serviceDiscovery.GetServiceUrlAsync("customer-service");
        var customerResponse = await _httpClient.GetAsync($"{customerServiceUrl}/api/customers/{request.CustomerId}");
        
        if (!customerResponse.IsSuccessStatusCode)
        {
            throw new CustomerNotFoundException($"Customer {request.CustomerId} not found");
        }

        var customer = await customerResponse.Content.ReadFromJsonAsync<CustomerDto>();

        // Create order
        var order = new Order(request.CustomerId, request.Items);
        
        // Publish event for other services
        await _eventBus.PublishAsync(new OrderCreatedEvent
        {
            OrderId = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Items = order.Items.Select(i => new OrderItemDto
            {
                ProductId = i.ProductId,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice
            }).ToList()
        });

        return new OrderDto
        {
            Id = order.Id,
            CustomerId = order.CustomerId,
            TotalAmount = order.TotalAmount,
            Status = order.Status.ToString()
        };
    }
}
```

### API Gateway
```csharp
public class ApiGateway
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ApiGateway> _logger;

    public ApiGateway(IServiceProvider serviceProvider, ILogger<ApiGateway> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IResult> RouteRequest(HttpContext context, string serviceName, string path)
    {
        try
        {
            var serviceDiscovery = _serviceProvider.GetRequiredService<ServiceDiscovery>();
            var httpClient = _serviceProvider.GetRequiredService<HttpClient>();

            var serviceUrl = await serviceDiscovery.GetServiceUrlAsync(serviceName);
            if (string.IsNullOrEmpty(serviceUrl))
            {
                return Results.NotFound($"Service {serviceName} not available");
            }

            var targetUrl = $"{serviceUrl}{path}{context.Request.QueryString}";
            var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), targetUrl);

            // Copy headers
            foreach (var header in context.Request.Headers)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }

            // Copy body for POST/PUT requests
            if (context.Request.ContentLength > 0)
            {
                request.Content = new StreamContent(context.Request.Body);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(context.Request.ContentType);
            }

            var response = await httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            context.Response.StatusCode = (int)response.StatusCode;
            await context.Response.WriteAsync(content);

            return Results.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error routing request to {ServiceName}", serviceName);
            return Results.Problem("Internal server error");
        }
    }
}
```

### Distributed Transaction (Saga Pattern)
```csharp
public class OrderSaga
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<OrderSaga> _logger;

    public OrderSaga(IEventBus eventBus, ILogger<OrderSaga> logger)
    {
        _eventBus = eventBus;
        _logger = logger;
    }

    public async Task HandleOrderCreatedAsync(OrderCreatedEvent @event)
    {
        try
        {
            // Step 1: Reserve inventory
            await _eventBus.PublishAsync(new ReserveInventoryCommand
            {
                OrderId = @event.OrderId,
                Items = @event.Items
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start order saga for order {OrderId}", @event.OrderId);
            await CompensateOrderAsync(@event.OrderId);
        }
    }

    public async Task HandleInventoryReservedAsync(InventoryReservedEvent @event)
    {
        try
        {
            // Step 2: Process payment
            await _eventBus.PublishAsync(new ProcessPaymentCommand
            {
                OrderId = @event.OrderId,
                Amount = @event.TotalAmount,
                CustomerId = @event.CustomerId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", @event.OrderId);
            await CompensateInventoryAsync(@event.OrderId);
        }
    }

    public async Task HandlePaymentProcessedAsync(PaymentProcessedEvent @event)
    {
        // Step 3: Confirm order
        await _eventBus.PublishAsync(new ConfirmOrderCommand
        {
            OrderId = @event.OrderId
        });
    }

    private async Task CompensateOrderAsync(int orderId)
    {
        await _eventBus.PublishAsync(new CancelOrderCommand { OrderId = orderId });
    }

    private async Task CompensateInventoryAsync(int orderId)
    {
        await _eventBus.PublishAsync(new ReleaseInventoryCommand { OrderId = orderId });
    }
}
```

### Health Checks for Microservices
```csharp
public class MicroserviceHealthCheck : IHealthCheck
{
    private readonly ServiceDiscovery _serviceDiscovery;
    private readonly HttpClient _httpClient;
    private readonly string _serviceName;

    public MicroserviceHealthCheck(ServiceDiscovery serviceDiscovery, HttpClient httpClient, string serviceName)
    {
        _serviceDiscovery = serviceDiscovery;
        _httpClient = httpClient;
        _serviceName = serviceName;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var serviceUrl = await _serviceDiscovery.GetServiceUrlAsync(_serviceName);
            if (string.IsNullOrEmpty(serviceUrl))
            {
                return HealthCheckResult.Unhealthy($"Service {_serviceName} not found in service discovery");
            }

            var response = await _httpClient.GetAsync($"{serviceUrl}/health", cancellationToken);
            
            return response.IsSuccessStatusCode 
                ? HealthCheckResult.Healthy($"Service {_serviceName} is healthy")
                : HealthCheckResult.Unhealthy($"Service {_serviceName} returned {response.StatusCode}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Service {_serviceName} health check failed", ex);
        }
    }
}
```

## Deliverables

1. **Service Templates**: Microservice project templates
2. **Service Discovery**: Consul integration
3. **API Gateway**: Request routing and aggregation
4. **Inter-Service Communication**: HTTP and messaging
5. **Distributed Transactions**: Saga pattern implementation
6. **Event Bus**: Message-based communication
7. **Health Checks**: Service monitoring
8. **Configuration Management**: Distributed configuration
9. **Load Balancing**: Service load distribution
10. **Circuit Breaker**: Fault tolerance patterns

## Validation Checklist

- [ ] Services have clear domain boundaries
- [ ] Service discovery properly configured
- [ ] API gateway routes requests correctly
- [ ] Inter-service communication resilient
- [ ] Distributed transactions handle failures
- [ ] Event-driven architecture implemented
- [ ] Health checks monitor service status
- [ ] Configuration externalized and secure
- [ ] Load balancing distributes traffic
- [ ] Circuit breakers prevent cascade failures