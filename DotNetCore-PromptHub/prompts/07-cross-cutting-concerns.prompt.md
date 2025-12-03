---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Cross-Cutting Concerns & Best Practices

Implement comprehensive cross-cutting concerns including logging, security, configuration management, and observability for .NET Core applications.

## Requirements

### 1. Structured Logging
- Serilog configuration with multiple sinks
- Correlation ID tracking across requests
- Performance logging and metrics
- Security event logging

### 2. Security Implementation
- JWT authentication and authorization
- Role and claim-based access control
- API key authentication for services
- Security headers and CORS

### 3. Configuration Management
- Strongly typed configuration classes
- Environment-specific settings
- Azure Key Vault integration
- Configuration validation

### 4. Observability
- Health checks for dependencies
- Application metrics and monitoring
- Distributed tracing
- Performance counters

## Example Implementation

### Structured Logging with Serilog
```csharp
public static class LoggingConfiguration
{
    public static IServiceCollection AddLoggingServices(this IServiceCollection services, IConfiguration configuration)
    {
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithCorrelationId()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.File("logs/app-.log", 
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {CorrelationId} {Message:lj} {Properties:j}{NewLine}{Exception}")
            .WriteTo.Seq(configuration.GetConnectionString("Seq"))
            .CreateLogger();

        services.AddSerilog();
        services.AddScoped<ICorrelationIdGenerator, CorrelationIdGenerator>();
        
        return services;
    }
}

// Correlation ID Middleware
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);
        
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            context.Response.Headers.Add("X-Correlation-ID", correlationId);
            
            _logger.LogInformation("Request started for {Method} {Path} with correlation ID {CorrelationId}",
                context.Request.Method, context.Request.Path, correlationId);

            await _next(context);

            _logger.LogInformation("Request completed for {Method} {Path} with status {StatusCode}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode);
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        const string correlationIdHeaderName = "X-Correlation-ID";
        
        if (context.Request.Headers.TryGetValue(correlationIdHeaderName, out var correlationId))
        {
            return correlationId.FirstOrDefault() ?? Guid.NewGuid().ToString();
        }

        return Guid.NewGuid().ToString();
    }
}

public interface ICorrelationIdGenerator
{
    string Generate();
    string Get();
}

public class CorrelationIdGenerator : ICorrelationIdGenerator
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdGenerator(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string Generate() => Guid.NewGuid().ToString();

    public string Get()
    {
        return _httpContextAccessor.HttpContext?.Response.Headers["X-Correlation-ID"].FirstOrDefault() 
               ?? Generate();
    }
}
```

### Security Configuration
```csharp
public static class SecurityConfiguration
{
    public static IServiceCollection AddSecurityServices(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>();
        
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                RequireExpirationTime = true
            };

            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Authentication failed: {Exception}", context.Exception.Message);
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogDebug("Token validated for user {UserId}", context.Principal.Identity.Name);
                    return Task.CompletedTask;
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
            options.AddPolicy("ManagerOrAdmin", policy => policy.RequireRole("Manager", "Admin"));
            options.AddPolicy("CustomerManagement", policy => policy.RequireClaim("permission", "customers:manage"));
            options.AddPolicy("OrderManagement", policy => policy.RequireClaim("permission", "orders:manage"));
        });

        services.AddScoped<IAuthorizationHandler, ResourceOwnerAuthorizationHandler>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        
        return services;
    }
}

// Custom Authorization Handler
public class ResourceOwnerAuthorizationHandler : AuthorizationHandler<ResourceOwnerRequirement, int>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ResourceOwnerAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement,
        int resourceUserId)
    {
        var currentUserId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var isAdmin = context.User.IsInRole("Admin");

        if (isAdmin || currentUserId == resourceUserId.ToString())
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public class ResourceOwnerRequirement : IAuthorizationRequirement { }
```

### Configuration Management
```csharp
// Strongly Typed Configuration Classes
public class JwtSettings
{
    public string SecretKey { get; set; }
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public int ExpirationMinutes { get; set; }
    public int RefreshTokenExpirationDays { get; set; }
}

public class DatabaseSettings
{
    public string ConnectionString { get; set; }
    public int CommandTimeout { get; set; }
    public bool EnableSensitiveDataLogging { get; set; }
    public int MaxRetryCount { get; set; }
}

public class EmailSettings
{
    public string SmtpServer { get; set; }
    public int SmtpPort { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string FromEmail { get; set; }
    public string FromName { get; set; }
    public bool EnableSsl { get; set; }
}

public class CacheSettings
{
    public string ConnectionString { get; set; }
    public int DefaultExpirationMinutes { get; set; }
    public string InstanceName { get; set; }
}

// Configuration Validation
public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    public ValidateOptionsResult Validate(string name, JwtSettings options)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.SecretKey))
            errors.Add("JWT SecretKey is required");
        else if (options.SecretKey.Length < 32)
            errors.Add("JWT SecretKey must be at least 32 characters long");

        if (string.IsNullOrWhiteSpace(options.Issuer))
            errors.Add("JWT Issuer is required");

        if (string.IsNullOrWhiteSpace(options.Audience))
            errors.Add("JWT Audience is required");

        if (options.ExpirationMinutes <= 0)
            errors.Add("JWT ExpirationMinutes must be greater than 0");

        if (options.RefreshTokenExpirationDays <= 0)
            errors.Add("JWT RefreshTokenExpirationDays must be greater than 0");

        return errors.Any() 
            ? ValidateOptionsResult.Fail(errors)
            : ValidateOptionsResult.Success;
    }
}

// Configuration Extensions
public static class ConfigurationExtensions
{
    public static IServiceCollection AddConfigurationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register configuration classes
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<DatabaseSettings>(configuration.GetSection("DatabaseSettings"));
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.Configure<CacheSettings>(configuration.GetSection("CacheSettings"));

        // Add validation
        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

        // Add Azure Key Vault if configured
        var keyVaultUrl = configuration["KeyVault:Url"];
        if (!string.IsNullOrEmpty(keyVaultUrl))
        {
            services.AddAzureKeyVault(keyVaultUrl);
        }

        return services;
    }

    private static IServiceCollection AddAzureKeyVault(this IServiceCollection services, string keyVaultUrl)
    {
        services.AddSingleton<ISecretManager>(provider =>
        {
            var credential = new DefaultAzureCredential();
            var client = new SecretClient(new Uri(keyVaultUrl), credential);
            return new AzureKeyVaultSecretManager(client);
        });

        return services;
    }
}
```

### Health Checks Implementation
```csharp
public static class HealthCheckExtensions
{
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy("API is running"))
            .AddSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                name: "database",
                tags: new[] { "database", "sql" })
            .AddRedis(
                configuration.GetConnectionString("Redis"),
                name: "redis",
                tags: new[] { "cache", "redis" })
            .AddUrlGroup(
                new Uri(configuration["ExternalServices:PaymentService:HealthCheckUrl"]),
                name: "payment-service",
                tags: new[] { "external", "payment" })
            .AddCheck<EmailHealthCheck>("email-service", tags: new[] { "external", "email" })
            .AddCheck<DiskSpaceHealthCheck>("disk-space", tags: new[] { "infrastructure" });

        services.AddHealthChecksUI(options =>
        {
            options.SetEvaluationTimeInSeconds(30);
            options.MaximumHistoryEntriesPerEndpoint(50);
            options.AddHealthCheckEndpoint("API Health", "/health");
        }).AddInMemoryStorage();

        return services;
    }
}

// Custom Health Checks
public class EmailHealthCheck : IHealthCheck
{
    private readonly IEmailService _emailService;

    public EmailHealthCheck(IEmailService emailService)
    {
        _emailService = emailService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _emailService.IsHealthyAsync(cancellationToken);
            
            return isHealthy 
                ? HealthCheckResult.Healthy("Email service is responsive")
                : HealthCheckResult.Unhealthy("Email service is not responding");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Email service check failed", ex);
        }
    }
}

public class DiskSpaceHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady);
            var results = new Dictionary<string, object>();

            foreach (var drive in drives)
            {
                var freeSpacePercentage = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
                results.Add($"Drive {drive.Name}", $"{freeSpacePercentage:F1}% free");

                if (freeSpacePercentage < 10)
                {
                    return Task.FromResult(HealthCheckResult.Unhealthy(
                        $"Drive {drive.Name} has less than 10% free space", 
                        data: results));
                }
            }

            return Task.FromResult(HealthCheckResult.Healthy("Sufficient disk space available", results));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Disk space check failed", ex));
        }
    }
}
```

### Performance Monitoring
```csharp
public class PerformanceMonitoringMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceMonitoringMiddleware> _logger;
    private readonly DiagnosticSource _diagnosticSource;

    public PerformanceMonitoringMiddleware(
        RequestDelegate next, 
        ILogger<PerformanceMonitoringMiddleware> logger,
        DiagnosticSource diagnosticSource)
    {
        _next = next;
        _logger = logger;
        _diagnosticSource = diagnosticSource;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestPath = context.Request.Path.Value;
        var requestMethod = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            // Log performance metrics
            _logger.LogInformation("Request {Method} {Path} completed in {ElapsedMs}ms with status {StatusCode}",
                requestMethod, requestPath, elapsedMilliseconds, context.Response.StatusCode);

            // Emit diagnostic event
            if (_diagnosticSource.IsEnabled("RequestPerformance"))
            {
                _diagnosticSource.Write("RequestPerformance", new
                {
                    Method = requestMethod,
                    Path = requestPath,
                    ElapsedMilliseconds = elapsedMilliseconds,
                    StatusCode = context.Response.StatusCode
                });
            }

            // Log slow requests
            if (elapsedMilliseconds > 1000)
            {
                _logger.LogWarning("Slow request detected: {Method} {Path} took {ElapsedMs}ms",
                    requestMethod, requestPath, elapsedMilliseconds);
            }
        }
    }
}

// Application Metrics
public interface IMetricsCollector
{
    void IncrementCounter(string name, Dictionary<string, string> tags = null);
    void RecordValue(string name, double value, Dictionary<string, string> tags = null);
    void RecordTime(string name, TimeSpan duration, Dictionary<string, string> tags = null);
}

public class MetricsCollector : IMetricsCollector
{
    private readonly ILogger<MetricsCollector> _logger;

    public MetricsCollector(ILogger<MetricsCollector> logger)
    {
        _logger = logger;
    }

    public void IncrementCounter(string name, Dictionary<string, string> tags = null)
    {
        _logger.LogInformation("Counter {MetricName} incremented {Tags}",
            name, tags != null ? string.Join(", ", tags.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "");
    }

    public void RecordValue(string name, double value, Dictionary<string, string> tags = null)
    {
        _logger.LogInformation("Metric {MetricName} recorded value {Value} {Tags}",
            name, value, tags != null ? string.Join(", ", tags.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "");
    }

    public void RecordTime(string name, TimeSpan duration, Dictionary<string, string> tags = null)
    {
        _logger.LogInformation("Timer {MetricName} recorded {Duration}ms {Tags}",
            name, duration.TotalMilliseconds, tags != null ? string.Join(", ", tags.Select(kvp => $"{kvp.Key}={kvp.Value}")) : "");
    }
}
```

## Deliverables

1. **Structured Logging**: Serilog configuration with correlation IDs
2. **Security Framework**: JWT authentication and authorization
3. **Configuration Management**: Strongly typed settings with validation
4. **Health Checks**: Comprehensive dependency monitoring
5. **Performance Monitoring**: Request timing and metrics collection
6. **Error Handling**: Global exception management
7. **CORS Configuration**: Cross-origin request handling
8. **Security Headers**: HTTP security headers implementation
9. **Rate Limiting**: Request throttling and protection
10. **Observability**: Distributed tracing and monitoring

## Validation Checklist

- [ ] Structured logging implemented with correlation IDs
- [ ] Security properly configured with JWT
- [ ] Configuration classes strongly typed and validated
- [ ] Health checks cover all dependencies
- [ ] Performance monitoring tracks key metrics
- [ ] Error handling comprehensive and consistent
- [ ] CORS configured for security
- [ ] Security headers implemented
- [ ] Rate limiting protects against abuse
- [ ] Observability provides adequate visibility