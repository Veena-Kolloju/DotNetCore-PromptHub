---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Background Services Framework

Implement robust background services and scheduled tasks for .NET Core applications using hosted services, Hangfire, and Quartz.NET.

## Requirements

### 1. Hosted Services
- Long-running background tasks
- Scoped service execution
- Graceful shutdown handling
- Health monitoring

### 2. Scheduled Jobs
- Cron-based scheduling
- Recurring job execution
- Job persistence and recovery
- Distributed job processing

## Example Implementation

### Base Background Service
```csharp
public abstract class BackgroundServiceBase : BackgroundService
{
    protected readonly ILogger Logger;
    protected readonly IServiceProvider ServiceProvider;
    private readonly string _serviceName;

    protected BackgroundServiceBase(ILogger logger, IServiceProvider serviceProvider, string serviceName)
    {
        Logger = logger;
        ServiceProvider = serviceProvider;
        _serviceName = serviceName;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("{ServiceName} background service started", _serviceName);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = ServiceProvider.CreateScope();
                    await ExecuteWorkAsync(scope.ServiceProvider, stoppingToken);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error occurred in {ServiceName}", _serviceName);
                }

                await Task.Delay(GetDelayInterval(), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("{ServiceName} background service is stopping", _serviceName);
        }
        finally
        {
            Logger.LogInformation("{ServiceName} background service stopped", _serviceName);
        }
    }

    protected abstract Task ExecuteWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken);
    protected abstract TimeSpan GetDelayInterval();
}
```

### Email Processing Service
```csharp
public class EmailProcessingService : BackgroundServiceBase
{
    public EmailProcessingService(ILogger<EmailProcessingService> logger, IServiceProvider serviceProvider)
        : base(logger, serviceProvider, nameof(EmailProcessingService)) { }

    protected override async Task ExecuteWorkAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var emailQueue = serviceProvider.GetRequiredService<IEmailQueue>();
        var emailService = serviceProvider.GetRequiredService<IEmailService>();

        var pendingEmails = await emailQueue.GetPendingEmailsAsync(10, cancellationToken);

        foreach (var email in pendingEmails)
        {
            try
            {
                await emailService.SendEmailAsync(email.To, email.Subject, email.Body, cancellationToken);
                await emailQueue.MarkAsProcessedAsync(email.Id, cancellationToken);
                
                Logger.LogInformation("Email {EmailId} sent successfully to {Recipient}", email.Id, email.To);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to send email {EmailId}", email.Id);
                await emailQueue.MarkAsFailedAsync(email.Id, ex.Message, cancellationToken);
            }
        }
    }

    protected override TimeSpan GetDelayInterval() => TimeSpan.FromSeconds(30);
}

public interface IEmailQueue
{
    Task<IEnumerable<QueuedEmail>> GetPendingEmailsAsync(int batchSize, CancellationToken cancellationToken = default);
    Task MarkAsProcessedAsync(int emailId, CancellationToken cancellationToken = default);
    Task MarkAsFailedAsync(int emailId, string errorMessage, CancellationToken cancellationToken = default);
}

public class QueuedEmail
{
    public int Id { get; set; }
    public string To { get; set; }
    public string Subject { get; set; }
    public string Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public int RetryCount { get; set; }
    public string Status { get; set; }
}
```

### Hangfire Job Processing
```csharp
public class HangfireJobService
{
    private readonly ILogger<HangfireJobService> _logger;

    public HangfireJobService(ILogger<HangfireJobService> logger)
    {
        _logger = logger;
    }

    [Queue("default")]
    public async Task ProcessOrderAsync(int orderId)
    {
        _logger.LogInformation("Processing order {OrderId}", orderId);
        
        // Simulate order processing
        await Task.Delay(TimeSpan.FromSeconds(5));
        
        _logger.LogInformation("Order {OrderId} processed successfully", orderId);
    }

    [Queue("emails")]
    public async Task SendBulkEmailsAsync(List<string> recipients, string subject, string body)
    {
        _logger.LogInformation("Sending bulk emails to {RecipientCount} recipients", recipients.Count);

        foreach (var recipient in recipients)
        {
            try
            {
                // Send email logic here
                await Task.Delay(100); // Simulate email sending
                _logger.LogDebug("Email sent to {Recipient}", recipient);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send email to {Recipient}", recipient);
            }
        }

        _logger.LogInformation("Bulk email job completed");
    }

    [Queue("reports")]
    [AutomaticRetry(Attempts = 3)]
    public async Task GenerateMonthlyReportAsync(int year, int month)
    {
        _logger.LogInformation("Generating monthly report for {Year}-{Month}", year, month);

        try
        {
            // Report generation logic
            await Task.Delay(TimeSpan.FromMinutes(2)); // Simulate report generation
            
            _logger.LogInformation("Monthly report generated successfully for {Year}-{Month}", year, month);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate monthly report for {Year}-{Month}", year, month);
            throw;
        }
    }
}
```

### Quartz.NET Scheduled Jobs
```csharp
public class DataCleanupJob : IJob
{
    private readonly ILogger<DataCleanupJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DataCleanupJob(ILogger<DataCleanupJob> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting data cleanup job");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Clean up old audit logs (older than 90 days)
            var cutoffDate = DateTime.UtcNow.AddDays(-90);
            var oldLogs = await dbContext.AuditLogs
                .Where(log => log.CreatedAt < cutoffDate)
                .ToListAsync();

            if (oldLogs.Any())
            {
                dbContext.AuditLogs.RemoveRange(oldLogs);
                await dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Cleaned up {Count} old audit log entries", oldLogs.Count);
            }

            // Clean up soft-deleted records (older than 30 days)
            var softDeleteCutoff = DateTime.UtcNow.AddDays(-30);
            var deletedCustomers = await dbContext.Customers
                .Where(c => c.IsDeleted && c.UpdatedAt < softDeleteCutoff)
                .ToListAsync();

            if (deletedCustomers.Any())
            {
                dbContext.Customers.RemoveRange(deletedCustomers);
                await dbContext.SaveChangesAsync();
                
                _logger.LogInformation("Permanently deleted {Count} soft-deleted customers", deletedCustomers.Count);
            }

            _logger.LogInformation("Data cleanup job completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during data cleanup job");
            throw;
        }
    }
}

public class ReportGenerationJob : IJob
{
    private readonly ILogger<ReportGenerationJob> _logger;
    private readonly IServiceProvider _serviceProvider;

    public ReportGenerationJob(ILogger<ReportGenerationJob> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation("Starting daily report generation");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();

            var reportDate = DateTime.UtcNow.Date.AddDays(-1); // Previous day
            await reportService.GenerateDailyReportAsync(reportDate);

            _logger.LogInformation("Daily report generated successfully for {ReportDate}", reportDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during report generation");
            throw;
        }
    }
}
```

### Job Scheduling Configuration
```csharp
public static class BackgroundJobConfiguration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        // Hosted Services
        services.AddHostedService<EmailProcessingService>();
        services.AddHostedService<OrderProcessingService>();

        // Hangfire
        services.AddHangfire(config =>
        {
            config.UseSqlServerStorage(configuration.GetConnectionString("DefaultConnection"));
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();
        });

        services.AddHangfireServer(options =>
        {
            options.Queues = new[] { "default", "emails", "reports" };
            options.WorkerCount = Environment.ProcessorCount * 2;
        });

        // Quartz.NET
        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjection();

            // Data cleanup job - runs daily at 2 AM
            var dataCleanupJobKey = new JobKey("DataCleanupJob");
            q.AddJob<DataCleanupJob>(opts => opts.WithIdentity(dataCleanupJobKey));
            q.AddTrigger(opts => opts
                .ForJob(dataCleanupJobKey)
                .WithIdentity("DataCleanupJob-trigger")
                .WithCronSchedule("0 0 2 * * ?"));

            // Report generation job - runs daily at 6 AM
            var reportJobKey = new JobKey("ReportGenerationJob");
            q.AddJob<ReportGenerationJob>(opts => opts.WithIdentity(reportJobKey));
            q.AddTrigger(opts => opts
                .ForJob(reportJobKey)
                .WithIdentity("ReportGenerationJob-trigger")
                .WithCronSchedule("0 0 6 * * ?"));
        });

        services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

        return services;
    }

    public static void ConfigureRecurringJobs()
    {
        // Hangfire recurring jobs
        RecurringJob.AddOrUpdate<HangfireJobService>(
            "monthly-report",
            service => service.GenerateMonthlyReportAsync(DateTime.Now.Year, DateTime.Now.Month),
            "0 0 1 * *"); // First day of every month at midnight

        RecurringJob.AddOrUpdate<HangfireJobService>(
            "weekly-cleanup",
            service => service.ProcessOrderAsync(0), // Placeholder for cleanup logic
            Cron.Weekly(DayOfWeek.Sunday, 3)); // Every Sunday at 3 AM
    }
}
```

### Health Checks for Background Services
```csharp
public class BackgroundServiceHealthCheck : IHealthCheck
{
    private readonly ILogger<BackgroundServiceHealthCheck> _logger;
    private readonly IServiceProvider _serviceProvider;

    public BackgroundServiceHealthCheck(ILogger<BackgroundServiceHealthCheck> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var hostedServices = _serviceProvider.GetServices<IHostedService>();
            var healthyServices = new List<string>();
            var unhealthyServices = new List<string>();

            foreach (var service in hostedServices)
            {
                var serviceName = service.GetType().Name;
                
                // Check if service is running (this is a simplified check)
                if (service is BackgroundServiceBase backgroundService)
                {
                    // You would implement actual health checking logic here
                    healthyServices.Add(serviceName);
                }
            }

            var data = new Dictionary<string, object>
            {
                ["healthy_services"] = healthyServices,
                ["unhealthy_services"] = unhealthyServices,
                ["total_services"] = hostedServices.Count()
            };

            return unhealthyServices.Any()
                ? HealthCheckResult.Degraded("Some background services are unhealthy", data: data)
                : HealthCheckResult.Healthy("All background services are healthy", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check background services health", ex);
        }
    }
}
```

## Deliverables

1. **Hosted Services**: Long-running background tasks
2. **Scheduled Jobs**: Cron-based job scheduling
3. **Job Queues**: Reliable job processing with queues
4. **Error Handling**: Retry logic and failure management
5. **Health Monitoring**: Background service health checks
6. **Job Persistence**: Database-backed job storage
7. **Distributed Processing**: Multi-instance job coordination
8. **Performance Monitoring**: Job execution metrics
9. **Configuration Management**: Job scheduling configuration
10. **Graceful Shutdown**: Proper service lifecycle management

## Validation Checklist

- [ ] Background services run continuously and reliably
- [ ] Scheduled jobs execute at correct intervals
- [ ] Job queues handle high throughput efficiently
- [ ] Error handling includes retry logic and dead letter queues
- [ ] Health checks monitor service status
- [ ] Job persistence survives application restarts
- [ ] Distributed processing prevents duplicate execution
- [ ] Performance metrics track job execution times
- [ ] Configuration allows easy job schedule changes
- [ ] Graceful shutdown prevents data loss