---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Monitoring & Observability Framework

Implement comprehensive monitoring, logging, and observability for .NET Core applications using Application Insights, Serilog, and custom metrics.

## Requirements

### 1. Application Monitoring
- Performance metrics collection
- Error tracking and alerting
- Dependency monitoring
- Custom telemetry data

### 2. Distributed Tracing
- Request correlation across services
- Performance bottleneck identification
- Service dependency mapping
- End-to-end transaction tracking

## Example Implementation

### Application Insights Configuration
```csharp
public static class MonitoringConfiguration
{
    public static IServiceCollection AddMonitoring(this IServiceCollection services, IConfiguration configuration)
    {
        // Application Insights
        services.AddApplicationInsightsTelemetry(configuration);
        
        // Custom telemetry initializers
        services.AddSingleton<ITelemetryInitializer, CustomTelemetryInitializer>();
        services.AddSingleton<ITelemetryProcessor, CustomTelemetryProcessor>();
        
        // Metrics collection
        services.AddSingleton<IMetricsCollector, MetricsCollector>();
        
        // Health checks with detailed reporting
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database")
            .AddCheck<ExternalServiceHealthCheck>("external-services")
            .AddApplicationInsightsPublisher();

        return services;
    }
}

public class CustomTelemetryInitializer : ITelemetryInitializer
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CustomTelemetryInitializer(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize(ITelemetry telemetry)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            telemetry.Context.GlobalProperties["UserId"] = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            telemetry.Context.GlobalProperties["UserEmail"] = context.User?.FindFirst(ClaimTypes.Email)?.Value;
            telemetry.Context.GlobalProperties["CorrelationId"] = context.TraceIdentifier;
        }

        telemetry.Context.GlobalProperties["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        telemetry.Context.GlobalProperties["MachineName"] = Environment.MachineName;
    }
}
```

### Custom Metrics Collection
```csharp
public interface IMetricsCollector
{
    void IncrementCounter(string name, Dictionary<string, string> tags = null);
    void RecordValue(string name, double value, Dictionary<string, string> tags = null);
    void RecordTime(string name, TimeSpan duration, Dictionary<string, string> tags = null);
    void TrackBusinessMetric(string eventName, Dictionary<string, object> properties = null);
}

public class MetricsCollector : IMetricsCollector
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<MetricsCollector> _logger;

    public MetricsCollector(TelemetryClient telemetryClient, ILogger<MetricsCollector> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void IncrementCounter(string name, Dictionary<string, string> tags = null)
    {
        _telemetryClient.TrackMetric(name, 1, tags);
        _logger.LogDebug("Counter {MetricName} incremented", name);
    }

    public void RecordValue(string name, double value, Dictionary<string, string> tags = null)
    {
        _telemetryClient.TrackMetric(name, value, tags);
        _logger.LogDebug("Metric {MetricName} recorded value {Value}", name, value);
    }

    public void RecordTime(string name, TimeSpan duration, Dictionary<string, string> tags = null)
    {
        _telemetryClient.TrackMetric($"{name}_duration_ms", duration.TotalMilliseconds, tags);
        _logger.LogDebug("Timer {MetricName} recorded {Duration}ms", name, duration.TotalMilliseconds);
    }

    public void TrackBusinessMetric(string eventName, Dictionary<string, object> properties = null)
    {
        var stringProperties = properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString());
        _telemetryClient.TrackEvent(eventName, stringProperties);
        _logger.LogInformation("Business event {EventName} tracked", eventName);
    }
}
```

### Performance Monitoring Middleware
```csharp
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    private readonly IMetricsCollector _metrics;
    private readonly TelemetryClient _telemetryClient;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next,
        ILogger<PerformanceMonitoringMiddleware> logger,
        IMetricsCollector metrics,
        TelemetryClient telemetryClient)
    {
        _next = next;
        _logger = logger;
        _metrics = metrics;
        _telemetryClient = telemetryClient;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var path = context.Request.Path.Value;
        var method = context.Request.Method;
        var correlationId = context.TraceIdentifier;

        using var operation = _telemetryClient.StartOperation<RequestTelemetry>($"{method} {path}");
        operation.Telemetry.Properties["CorrelationId"] = correlationId;

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _telemetryClient.TrackException(ex, new Dictionary<string, string>
            {
                ["Path"] = path,
                ["Method"] = method,
                ["CorrelationId"] = correlationId
            });

            _metrics.IncrementCounter("http_requests_errors", new Dictionary<string, string>
            {
                ["method"] = method,
                ["path"] = path,
                ["exception_type"] = ex.GetType().Name
            });

            throw;
        }
        finally
        {
            stopwatch.Stop();
            var elapsed = stopwatch.ElapsedMilliseconds;
            var statusCode = context.Response.StatusCode;

            // Record metrics
            _metrics.RecordTime("http_request_duration", stopwatch.Elapsed, new Dictionary<string, string>
            {
                ["method"] = method,
                ["path"] = path,
                ["status_code"] = statusCode.ToString()
            });

            _metrics.IncrementCounter("http_requests_total", new Dictionary<string, string>
            {
                ["method"] = method,
                ["path"] = path,
                ["status_code"] = statusCode.ToString()
            });

            // Log performance data
            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                method, path, statusCode, elapsed, correlationId);

            // Track slow requests
            if (elapsed > 1000)
            {
                _logger.LogWarning(
                    "Slow request detected: {Method} {Path} took {ElapsedMs}ms [CorrelationId: {CorrelationId}]",
                    method, path, elapsed, correlationId);

                _telemetryClient.TrackEvent("SlowRequest", new Dictionary<string, string>
                {
                    ["Method"] = method,
                    ["Path"] = path,
                    ["Duration"] = elapsed.ToString(),
                    ["CorrelationId"] = correlationId
                });
            }

            operation.Telemetry.ResponseCode = statusCode.ToString();
            operation.Telemetry.Success = statusCode < 400;
        }
    }
}
```

### Business Metrics Tracking
```csharp
public class CustomerService : ICustomerService
{
    private readonly ICustomerRepository _repository;
    private readonly IMetricsCollector _metrics;
    private readonly TelemetryClient _telemetryClient;

    public CustomerService(
        ICustomerRepository repository,
        IMetricsCollector metrics,
        TelemetryClient telemetryClient)
    {
        _repository = repository;
        _metrics = metrics;
        _telemetryClient = telemetryClient;
    }

    public async Task<Result<CustomerDto>> CreateCustomerAsync(CreateCustomerRequest request)
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("CreateCustomer");
        
        try
        {
            var customer = await _repository.CreateAsync(request);

            // Track business metrics
            _metrics.TrackBusinessMetric("CustomerCreated", new Dictionary<string, object>
            {
                ["CustomerId"] = customer.Id,
                ["CustomerType"] = customer.Type.ToString(),
                ["RegistrationSource"] = request.Source ?? "Direct"
            });

            _metrics.IncrementCounter("customers_created_total", new Dictionary<string, string>
            {
                ["type"] = customer.Type.ToString(),
                ["source"] = request.Source ?? "Direct"
            });

            operation.Telemetry.Success = true;
            return Result<CustomerDto>.Success(customer);
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _telemetryClient.TrackException(ex);
            throw;
        }
    }

    public async Task<Result> PromoteCustomerToVipAsync(int customerId)
    {
        using var operation = _telemetryClient.StartOperation<DependencyTelemetry>("PromoteCustomerToVip");
        
        try
        {
            var customer = await _repository.GetByIdAsync(customerId);
            if (customer == null)
            {
                return Result.Failure("Customer not found");
            }

            var previousType = customer.Type;
            customer.PromoteToVip();
            await _repository.UpdateAsync(customer);

            // Track promotion event
            _metrics.TrackBusinessMetric("CustomerPromoted", new Dictionary<string, object>
            {
                ["CustomerId"] = customerId,
                ["FromType"] = previousType.ToString(),
                ["ToType"] = CustomerType.Vip.ToString(),
                ["PromotionDate"] = DateTime.UtcNow
            });

            _metrics.IncrementCounter("customer_promotions_total", new Dictionary<string, string>
            {
                ["from_type"] = previousType.ToString(),
                ["to_type"] = CustomerType.Vip.ToString()
            });

            operation.Telemetry.Success = true;
            return Result.Success();
        }
        catch (Exception ex)
        {
            operation.Telemetry.Success = false;
            _telemetryClient.TrackException(ex);
            throw;
        }
    }
}
```

### Health Checks with Detailed Reporting
```csharp
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;
    private readonly IMetricsCollector _metrics;

    public DatabaseHealthCheck(ApplicationDbContext context, IMetricsCollector metrics)
    {
        _context = context;
        _metrics = metrics;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Test database connectivity
            await _context.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            
            // Test query performance
            var customerCount = await _context.Customers.CountAsync(cancellationToken);
            
            stopwatch.Stop();
            
            var data = new Dictionary<string, object>
            {
                ["response_time_ms"] = stopwatch.ElapsedMilliseconds,
                ["customer_count"] = customerCount,
                ["connection_string"] = _context.Database.GetConnectionString()?.Substring(0, 50) + "..."
            };

            _metrics.RecordTime("health_check_database", stopwatch.Elapsed);

            return stopwatch.ElapsedMilliseconds < 1000
                ? HealthCheckResult.Healthy("Database is responsive", data)
                : HealthCheckResult.Degraded("Database is slow", data);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _metrics.IncrementCounter("health_check_failures", new Dictionary<string, string>
            {
                ["check_name"] = "database"
            });

            return HealthCheckResult.Unhealthy("Database connection failed", ex);
        }
    }
}

public class ExternalServiceHealthCheck : IHealthCheck
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly IMetricsCollector _metrics;

    public ExternalServiceHealthCheck(HttpClient httpClient, IConfiguration configuration, IMetricsCollector metrics)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _metrics = metrics;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var services = new[]
        {
            ("PaymentService", _configuration["ExternalServices:PaymentService:HealthUrl"]),
            ("EmailService", _configuration["ExternalServices:EmailService:HealthUrl"]),
            ("NotificationService", _configuration["ExternalServices:NotificationService:HealthUrl"])
        };

        var results = new Dictionary<string, object>();
        var allHealthy = true;

        foreach (var (serviceName, healthUrl) in services)
        {
            if (string.IsNullOrEmpty(healthUrl))
            {
                results[serviceName] = "Not configured";
                continue;
            }

            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var response = await _httpClient.GetAsync(healthUrl, cancellationToken);
                stopwatch.Stop();

                var isHealthy = response.IsSuccessStatusCode;
                results[serviceName] = new
                {
                    status = isHealthy ? "Healthy" : "Unhealthy",
                    response_time_ms = stopwatch.ElapsedMilliseconds,
                    status_code = (int)response.StatusCode
                };

                _metrics.RecordTime($"health_check_{serviceName.ToLower()}", stopwatch.Elapsed);

                if (!isHealthy)
                {
                    allHealthy = false;
                    _metrics.IncrementCounter("health_check_failures", new Dictionary<string, string>
                    {
                        ["check_name"] = serviceName.ToLower()
                    });
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                allHealthy = false;
                results[serviceName] = new
                {
                    status = "Error",
                    error = ex.Message,
                    response_time_ms = stopwatch.ElapsedMilliseconds
                };

                _metrics.IncrementCounter("health_check_failures", new Dictionary<string, string>
                {
                    ["check_name"] = serviceName.ToLower()
                });
            }
        }

        return allHealthy
            ? HealthCheckResult.Healthy("All external services are healthy", results)
            : HealthCheckResult.Degraded("Some external services are unhealthy", results);
    }
}
```

### Alerting Configuration
```csharp
public class AlertingService
{
    private readonly TelemetryClient _telemetryClient;
    private readonly ILogger<AlertingService> _logger;

    public AlertingService(TelemetryClient telemetryClient, ILogger<AlertingService> logger)
    {
        _telemetryClient = telemetryClient;
        _logger = logger;
    }

    public void TriggerAlert(string alertName, string message, AlertSeverity severity, Dictionary<string, string> properties = null)
    {
        var alertProperties = new Dictionary<string, string>(properties ?? new Dictionary<string, string>())
        {
            ["AlertName"] = alertName,
            ["Severity"] = severity.ToString(),
            ["Timestamp"] = DateTime.UtcNow.ToString("O"),
            ["Environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        };

        _telemetryClient.TrackEvent("Alert", alertProperties);
        
        _logger.Log(GetLogLevel(severity), "ALERT: {AlertName} - {Message}", alertName, message);

        // Additional alerting mechanisms (email, Slack, etc.) can be added here
    }

    private LogLevel GetLogLevel(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Critical => LogLevel.Critical,
            AlertSeverity.Error => LogLevel.Error,
            AlertSeverity.Warning => LogLevel.Warning,
            AlertSeverity.Information => LogLevel.Information,
            _ => LogLevel.Information
        };
    }
}

public enum AlertSeverity
{
    Information,
    Warning,
    Error,
    Critical
}
```

## Deliverables

1. **Application Insights Integration**: Complete telemetry setup
2. **Custom Metrics Collection**: Business and technical metrics
3. **Performance Monitoring**: Request timing and bottleneck identification
4. **Error Tracking**: Exception monitoring and alerting
5. **Health Checks**: Comprehensive system health monitoring
6. **Distributed Tracing**: Cross-service request correlation
7. **Business Intelligence**: Custom event tracking
8. **Alerting System**: Automated alert generation
9. **Dashboard Configuration**: Monitoring dashboard setup
10. **Log Aggregation**: Centralized logging with correlation

## Validation Checklist

- [ ] Application Insights properly configured
- [ ] Custom metrics collected for business events
- [ ] Performance monitoring tracks request times
- [ ] Error tracking captures and alerts on exceptions
- [ ] Health checks monitor all critical dependencies
- [ ] Distributed tracing correlates requests across services
- [ ] Business metrics provide actionable insights
- [ ] Alerting system notifies on critical issues
- [ ] Dashboards provide real-time system visibility
- [ ] Log correlation enables effective troubleshooting