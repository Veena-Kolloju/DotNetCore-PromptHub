---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Service Communication & Integration

Create robust service communication patterns with HTTP clients, resilience policies, and event-driven integration using Polly and message queues.

## Requirements

### 1. HTTP Client Services
- Typed HTTP clients with dependency injection
- Resilience patterns (retry, circuit breaker, timeout)
- Request/response logging and monitoring
- Authentication and authorization handling

### 2. Message Queue Integration
- Event publishing and consuming
- Message serialization and deserialization
- Dead letter queue handling
- Idempotency and duplicate detection

### 3. Resilience Patterns
- Retry policies with exponential backoff
- Circuit breaker implementation
- Timeout and bulkhead patterns
- Fallback mechanisms

## Example Implementation

### HTTP Client Service
```csharp
public interface IPaymentService
{
    Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentStatus> GetPaymentStatusAsync(string paymentId, CancellationToken cancellationToken = default);
    Task<bool> RefundPaymentAsync(string paymentId, decimal amount, CancellationToken cancellationToken = default);
}

public class PaymentService : IPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentService> _logger;
    private readonly PaymentServiceOptions _options;

    public PaymentService(
        HttpClient httpClient,
        ILogger<PaymentService> logger,
        IOptions<PaymentServiceOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing payment for amount {Amount} and customer {CustomerId}", 
                request.Amount, request.CustomerId);

            var requestContent = JsonContent.Create(request);
            var response = await _httpClient.PostAsync("/api/payments", requestContent, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<PaymentResult>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                _logger.LogInformation("Payment processed successfully with ID {PaymentId}", result.PaymentId);
                return result;
            }

            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Payment processing failed with status {StatusCode}: {Error}", 
                response.StatusCode, errorContent);

            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = $"Payment failed with status {response.StatusCode}"
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error occurred while processing payment");
            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = "Payment service is currently unavailable"
            };
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout occurred while processing payment");
            return new PaymentResult
            {
                IsSuccess = false,
                ErrorMessage = "Payment request timed out"
            };
        }
    }

    public async Task<PaymentStatus> GetPaymentStatusAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/payments/{paymentId}/status", cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonSerializer.Deserialize<PaymentStatus>(content, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new PaymentStatus { Status = "NotFound" };
            }

            throw new HttpRequestException($"Failed to get payment status: {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment status for {PaymentId}", paymentId);
            throw;
        }
    }
}
```

### Resilience Policies with Polly
```csharp
public static class ResiliencePolicies
{
    public static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    logger.LogWarning("Retry {RetryCount} for {OperationKey} in {Delay}ms",
                        retryCount, context.OperationKey, timespan.TotalMilliseconds);
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(ILogger logger)
    {
        return Policy
            .HandleResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30),
                onBreak: (result, timespan) =>
                {
                    logger.LogWarning("Circuit breaker opened for {Duration}s", timespan.TotalSeconds);
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset");
                });
    }

    public static IAsyncPolicy<HttpResponseMessage> GetTimeoutPolicy()
    {
        return Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(10));
    }

    public static IAsyncPolicy<HttpResponseMessage> GetCombinedPolicy(ILogger logger)
    {
        return Policy.WrapAsync(
            GetRetryPolicy(logger),
            GetCircuitBreakerPolicy(logger),
            GetTimeoutPolicy());
    }
}

// HTTP Client Configuration
public static class HttpClientServiceExtensions
{
    public static IServiceCollection AddHttpClientServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PaymentServiceOptions>(configuration.GetSection("PaymentService"));

        services.AddHttpClient<IPaymentService, PaymentService>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<PaymentServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {options.ApiKey}");
            client.DefaultRequestHeaders.Add("User-Agent", "CustomerManagement/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<PaymentService>>();
            return ResiliencePolicies.GetCombinedPolicy(logger);
        })
        .AddHttpMessageHandler<LoggingHandler>();

        services.AddTransient<LoggingHandler>();

        return services;
    }
}
```

### HTTP Message Handler for Logging
```csharp
public class LoggingHandler : DelegatingHandler
{
    private readonly ILogger<LoggingHandler> _logger;

    public LoggingHandler(ILogger<LoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestId = Guid.NewGuid();
        
        _logger.LogInformation("HTTP Request {RequestId}: {Method} {Uri}",
            requestId, request.Method, request.RequestUri);

        if (request.Content != null)
        {
            var requestContent = await request.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug("HTTP Request {RequestId} Content: {Content}", requestId, requestContent);
        }

        var stopwatch = Stopwatch.StartNew();
        var response = await base.SendAsync(request, cancellationToken);
        stopwatch.Stop();

        _logger.LogInformation("HTTP Response {RequestId}: {StatusCode} in {ElapsedMs}ms",
            requestId, response.StatusCode, stopwatch.ElapsedMilliseconds);

        if (!response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("HTTP Response {RequestId} Error Content: {Content}", requestId, responseContent);
        }

        return response;
    }
}
```

### Event Bus Implementation
```csharp
public interface IEventBus
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent;
    Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : class, IEvent;
}

public class RabbitMQEventBus : IEventBus
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMQEventBus> _logger;
    private readonly string _exchangeName;

    public RabbitMQEventBus(IConnection connection, ILogger<RabbitMQEventBus> logger, IConfiguration configuration)
    {
        _connection = connection;
        _channel = _connection.CreateModel();
        _logger = logger;
        _exchangeName = configuration.GetValue<string>("EventBus:ExchangeName");

        _channel.ExchangeDeclare(_exchangeName, ExchangeType.Topic, durable: true);
    }

    public async Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        var eventName = @event.GetType().Name;
        var message = JsonSerializer.Serialize(@event, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var body = Encoding.UTF8.GetBytes(message);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.MessageId = Guid.NewGuid().ToString();
        properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        properties.Headers = new Dictionary<string, object>
        {
            ["EventType"] = eventName,
            ["Source"] = "CustomerManagement"
        };

        _channel.BasicPublish(
            exchange: _exchangeName,
            routingKey: eventName,
            basicProperties: properties,
            body: body);

        _logger.LogInformation("Published event {EventName} with ID {MessageId}", eventName, properties.MessageId);

        await Task.CompletedTask;
    }

    public async Task SubscribeAsync<T>(Func<T, CancellationToken, Task> handler, CancellationToken cancellationToken = default) where T : class, IEvent
    {
        var eventName = typeof(T).Name;
        var queueName = $"{eventName}_Queue";

        _channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueBind(queueName, _exchangeName, eventName);

        var consumer = new EventingBasicConsumer(_channel);
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);

            try
            {
                var @event = JsonSerializer.Deserialize<T>(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                await handler(@event, cancellationToken);

                _channel.BasicAck(ea.DeliveryTag, false);
                _logger.LogInformation("Processed event {EventName} with delivery tag {DeliveryTag}", eventName, ea.DeliveryTag);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event {EventName}: {Message}", eventName, message);
                _channel.BasicNack(ea.DeliveryTag, false, false); // Send to dead letter queue
            }
        };

        _channel.BasicConsume(queueName, autoAck: false, consumer);

        await Task.CompletedTask;
    }
}
```

### Domain Events
```csharp
public interface IEvent
{
    Guid Id { get; }
    DateTime OccurredAt { get; }
}

public abstract class BaseEvent : IEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public class CustomerCreatedEvent : BaseEvent
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public string Email { get; set; }
    public CustomerType Type { get; set; }
}

public class CustomerPromotedToVipEvent : BaseEvent
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; }
    public DateTime PromotedAt { get; set; }
}

public class OrderCreatedEvent : BaseEvent
{
    public int OrderId { get; set; }
    public int CustomerId { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderItemDto> Items { get; set; }
}
```

### Event Handlers
```csharp
public interface IEventHandler<in T> where T : IEvent
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
            await _emailService.SendWelcomeEmailAsync(@event.Email, @event.CustomerName, cancellationToken);
            _logger.LogInformation("Welcome email sent to customer {CustomerId}", @event.CustomerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send welcome email to customer {CustomerId}", @event.CustomerId);
            throw;
        }
    }
}

public class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly IInventoryService _inventoryService;
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(IInventoryService inventoryService, ILogger<OrderCreatedEventHandler> logger)
    {
        _inventoryService = inventoryService;
        _logger = logger;
    }

    public async Task HandleAsync(OrderCreatedEvent @event, CancellationToken cancellationToken = default)
    {
        try
        {
            foreach (var item in @event.Items)
            {
                await _inventoryService.ReserveInventoryAsync(item.ProductId, item.Quantity, cancellationToken);
            }

            _logger.LogInformation("Inventory reserved for order {OrderId}", @event.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reserve inventory for order {OrderId}", @event.OrderId);
            throw;
        }
    }
}
```

### Service Registration
```csharp
public static class ServiceCommunicationExtensions
{
    public static IServiceCollection AddServiceCommunication(this IServiceCollection services, IConfiguration configuration)
    {
        // HTTP Clients
        services.AddHttpClientServices(configuration);

        // Event Bus
        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var connectionString = configuration.GetConnectionString("RabbitMQ");
            return new ConnectionFactory { Uri = new Uri(connectionString) };
        });

        services.AddSingleton<IConnection>(sp =>
        {
            var factory = sp.GetRequiredService<IConnectionFactory>();
            return factory.CreateConnection();
        });

        services.AddSingleton<IEventBus, RabbitMQEventBus>();

        // Event Handlers
        services.AddScoped<IEventHandler<CustomerCreatedEvent>, CustomerCreatedEventHandler>();
        services.AddScoped<IEventHandler<OrderCreatedEvent>, OrderCreatedEventHandler>();

        return services;
    }
}
```

## Deliverables

1. **HTTP Client Services**: Typed clients with resilience patterns
2. **Resilience Policies**: Retry, circuit breaker, timeout implementations
3. **Message Logging**: Request/response logging handlers
4. **Event Bus**: Message queue integration
5. **Domain Events**: Event definitions and handlers
6. **Error Handling**: Comprehensive exception management
7. **Configuration**: Service communication settings
8. **Health Checks**: External service monitoring
9. **Authentication**: Service-to-service authentication
10. **Monitoring**: Performance and reliability metrics

## Validation Checklist

- [ ] HTTP clients properly configured
- [ ] Resilience patterns implemented
- [ ] Event bus integration working
- [ ] Message serialization handled
- [ ] Error handling comprehensive
- [ ] Logging and monitoring enabled
- [ ] Authentication configured
- [ ] Health checks implemented
- [ ] Performance optimized
- [ ] Dead letter queues configured