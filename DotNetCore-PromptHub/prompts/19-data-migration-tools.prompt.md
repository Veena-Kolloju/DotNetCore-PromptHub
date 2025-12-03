---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Data Migration Tools Framework

Create comprehensive data migration tools for .NET Core applications including ETL processes, data validation, and migration monitoring.

## Requirements

### 1. Migration Framework
- Source and target data mapping
- Batch processing for large datasets
- Data transformation and validation
- Error handling and recovery

### 2. ETL Pipeline
- Extract data from multiple sources
- Transform data according to business rules
- Load data with integrity checks
- Progress tracking and logging

## Example Implementation

### Migration Framework Base
```csharp
public interface IDataMigration
{
    string Name { get; }
    string Description { get; }
    Task<MigrationResult> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken = default);
    Task<bool> CanExecuteAsync(MigrationContext context);
    Task RollbackAsync(MigrationContext context, CancellationToken cancellationToken = default);
}

public abstract class DataMigrationBase : IDataMigration
{
    protected readonly ILogger Logger;
    protected readonly IMetricsCollector Metrics;

    protected DataMigrationBase(ILogger logger, IMetricsCollector metrics)
    {
        Logger = logger;
        Metrics = metrics;
    }

    public abstract string Name { get; }
    public abstract string Description { get; }

    public async Task<MigrationResult> ExecuteAsync(MigrationContext context, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = new MigrationResult { MigrationName = Name };

        try
        {
            Logger.LogInformation("Starting migration: {MigrationName}", Name);
            
            if (!await CanExecuteAsync(context))
            {
                result.Success = false;
                result.ErrorMessage = "Migration prerequisites not met";
                return result;
            }

            await ExecuteMigrationAsync(context, result, cancellationToken);
            
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = true;

            Logger.LogInformation("Migration {MigrationName} completed successfully in {Duration}ms", 
                Name, stopwatch.ElapsedMilliseconds);

            Metrics.RecordTime($"migration_{Name.ToLower()}_duration", stopwatch.Elapsed);
            Metrics.IncrementCounter("migrations_completed", new Dictionary<string, string>
            {
                ["migration_name"] = Name,
                ["status"] = "success"
            });

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Exception = ex;

            Logger.LogError(ex, "Migration {MigrationName} failed after {Duration}ms", 
                Name, stopwatch.ElapsedMilliseconds);

            Metrics.IncrementCounter("migrations_completed", new Dictionary<string, string>
            {
                ["migration_name"] = Name,
                ["status"] = "failed"
            });

            return result;
        }
    }

    protected abstract Task ExecuteMigrationAsync(MigrationContext context, MigrationResult result, CancellationToken cancellationToken);
    
    public virtual Task<bool> CanExecuteAsync(MigrationContext context)
    {
        return Task.FromResult(true);
    }

    public virtual Task RollbackAsync(MigrationContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogWarning("Rollback not implemented for migration: {MigrationName}", Name);
        return Task.CompletedTask;
    }
}
```

### Customer Data Migration Example
```csharp
public class CustomerDataMigration : DataMigrationBase
{
    private readonly ILegacyCustomerRepository _legacyRepository;
    private readonly ICustomerRepository _modernRepository;
    private readonly IMapper _mapper;

    public CustomerDataMigration(
        ILegacyCustomerRepository legacyRepository,
        ICustomerRepository modernRepository,
        IMapper mapper,
        ILogger<CustomerDataMigration> logger,
        IMetricsCollector metrics) : base(logger, metrics)
    {
        _legacyRepository = legacyRepository;
        _modernRepository = modernRepository;
        _mapper = mapper;
    }

    public override string Name => "CustomerDataMigration";
    public override string Description => "Migrates customer data from legacy system to modern system";

    protected override async Task ExecuteMigrationAsync(MigrationContext context, MigrationResult result, CancellationToken cancellationToken)
    {
        const int batchSize = 1000;
        var totalRecords = await _legacyRepository.GetTotalCountAsync();
        var processedRecords = 0;
        var migratedRecords = 0;
        var skippedRecords = 0;
        var errorRecords = 0;

        Logger.LogInformation("Starting customer migration. Total records: {TotalRecords}", totalRecords);

        for (int offset = 0; offset < totalRecords; offset += batchSize)
        {
            var legacyCustomers = await _legacyRepository.GetBatchAsync(offset, batchSize, cancellationToken);
            
            foreach (var legacyCustomer in legacyCustomers)
            {
                try
                {
                    // Check if customer already exists
                    var existingCustomer = await _modernRepository.GetByLegacyIdAsync(legacyCustomer.Id, cancellationToken);
                    if (existingCustomer != null)
                    {
                        skippedRecords++;
                        continue;
                    }

                    // Transform legacy customer to modern customer
                    var modernCustomer = await TransformCustomerAsync(legacyCustomer);
                    
                    // Validate transformed data
                    var validationResult = await ValidateCustomerAsync(modernCustomer);
                    if (!validationResult.IsValid)
                    {
                        Logger.LogWarning("Customer validation failed for legacy ID {LegacyId}: {Errors}", 
                            legacyCustomer.Id, string.Join(", ", validationResult.Errors));
                        errorRecords++;
                        continue;
                    }

                    // Save to modern system
                    await _modernRepository.AddAsync(modernCustomer, cancellationToken);
                    migratedRecords++;

                    // Update progress
                    processedRecords++;
                    if (processedRecords % 100 == 0)
                    {
                        var progressPercentage = (double)processedRecords / totalRecords * 100;
                        Logger.LogInformation("Migration progress: {ProcessedRecords}/{TotalRecords} ({ProgressPercentage:F1}%)", 
                            processedRecords, totalRecords, progressPercentage);
                        
                        context.ReportProgress(progressPercentage);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Error migrating customer with legacy ID {LegacyId}", legacyCustomer.Id);
                    errorRecords++;
                }
            }

            // Save batch
            await _modernRepository.SaveChangesAsync(cancellationToken);
        }

        result.RecordsProcessed = processedRecords;
        result.RecordsMigrated = migratedRecords;
        result.RecordsSkipped = skippedRecords;
        result.RecordsErrored = errorRecords;

        Logger.LogInformation("Customer migration completed. Processed: {ProcessedRecords}, Migrated: {MigratedRecords}, Skipped: {SkippedRecords}, Errors: {ErrorRecords}",
            processedRecords, migratedRecords, skippedRecords, errorRecords);
    }

    private async Task<Customer> TransformCustomerAsync(LegacyCustomer legacyCustomer)
    {
        var customer = new Customer(
            legacyCustomer.FullName,
            new Email(legacyCustomer.EmailAddress),
            new PhoneNumber(FormatPhoneNumber(legacyCustomer.PhoneNumber)));

        // Map customer type
        customer.Type = MapCustomerType(legacyCustomer.CustomerLevel);

        // Map address if available
        if (!string.IsNullOrEmpty(legacyCustomer.Address))
        {
            var address = ParseAddress(legacyCustomer.Address);
            customer.UpdateAddress(address);
        }

        // Set legacy reference
        customer.LegacyId = legacyCustomer.Id;
        customer.CreatedAt = legacyCustomer.CreatedDate;

        return customer;
    }

    private CustomerType MapCustomerType(string customerLevel)
    {
        return customerLevel?.ToUpper() switch
        {
            "GOLD" => CustomerType.Vip,
            "SILVER" => CustomerType.Premium,
            _ => CustomerType.Regular
        };
    }

    private string FormatPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber))
            return null;

        // Remove all non-digit characters
        var digits = new string(phoneNumber.Where(char.IsDigit).ToArray());
        
        // Format as +1-XXX-XXX-XXXX for US numbers
        if (digits.Length == 10)
            return $"+1-{digits.Substring(0, 3)}-{digits.Substring(3, 3)}-{digits.Substring(6, 4)}";
        
        return phoneNumber; // Return original if can't format
    }

    private Address ParseAddress(string addressString)
    {
        // Simple address parsing - in real implementation, use proper address parsing library
        var parts = addressString.Split(',').Select(p => p.Trim()).ToArray();
        
        return new Address(
            street: parts.Length > 0 ? parts[0] : "",
            city: parts.Length > 1 ? parts[1] : "",
            state: parts.Length > 2 ? parts[2] : "",
            zipCode: parts.Length > 3 ? parts[3] : "",
            country: "United States");
    }

    private async Task<ValidationResult> ValidateCustomerAsync(Customer customer)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(customer.Name))
            result.Errors.Add("Customer name is required");

        if (customer.Email == null || string.IsNullOrWhiteSpace(customer.Email.Value))
            result.Errors.Add("Customer email is required");

        // Check for duplicate email in modern system
        if (customer.Email != null)
        {
            var existingByEmail = await _modernRepository.GetByEmailAsync(customer.Email.Value);
            if (existingByEmail != null)
                result.Errors.Add($"Customer with email {customer.Email.Value} already exists");
        }

        result.IsValid = !result.Errors.Any();
        return result;
    }

    public override async Task<bool> CanExecuteAsync(MigrationContext context)
    {
        // Check if legacy system is accessible
        try
        {
            await _legacyRepository.TestConnectionAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Cannot connect to legacy system");
            return false;
        }
    }

    public override async Task RollbackAsync(MigrationContext context, CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Rolling back customer migration");
        
        // Delete all customers with legacy IDs
        var migratedCustomers = await _modernRepository.GetByLegacySourceAsync("CustomerDataMigration", cancellationToken);
        
        foreach (var customer in migratedCustomers)
        {
            await _modernRepository.DeleteAsync(customer.Id, cancellationToken);
        }

        await _modernRepository.SaveChangesAsync(cancellationToken);
        
        Logger.LogInformation("Customer migration rollback completed");
    }
}
```

### ETL Pipeline Framework
```csharp
public class ETLPipeline<TSource, TTarget>
{
    private readonly IDataExtractor<TSource> _extractor;
    private readonly IDataTransformer<TSource, TTarget> _transformer;
    private readonly IDataLoader<TTarget> _loader;
    private readonly ILogger<ETLPipeline<TSource, TTarget>> _logger;

    public ETLPipeline(
        IDataExtractor<TSource> extractor,
        IDataTransformer<TSource, TTarget> transformer,
        IDataLoader<TTarget> loader,
        ILogger<ETLPipeline<TSource, TTarget>> logger)
    {
        _extractor = extractor;
        _transformer = transformer;
        _loader = loader;
        _logger = logger;
    }

    public async Task<ETLResult> ExecuteAsync(ETLContext context, CancellationToken cancellationToken = default)
    {
        var result = new ETLResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting ETL pipeline execution");

            // Extract
            _logger.LogInformation("Starting data extraction");
            var extractedData = await _extractor.ExtractAsync(context, cancellationToken);
            result.ExtractedRecords = extractedData.Count();
            _logger.LogInformation("Extracted {RecordCount} records", result.ExtractedRecords);

            // Transform
            _logger.LogInformation("Starting data transformation");
            var transformedData = new List<TTarget>();
            var transformErrors = new List<string>();

            await foreach (var sourceItem in extractedData.ToAsyncEnumerable())
            {
                try
                {
                    var transformedItem = await _transformer.TransformAsync(sourceItem, context, cancellationToken);
                    if (transformedItem != null)
                    {
                        transformedData.Add(transformedItem);
                        result.TransformedRecords++;
                    }
                }
                catch (Exception ex)
                {
                    transformErrors.Add($"Transform error for item: {ex.Message}");
                    result.TransformErrors++;
                }
            }

            _logger.LogInformation("Transformed {RecordCount} records with {ErrorCount} errors", 
                result.TransformedRecords, result.TransformErrors);

            // Load
            _logger.LogInformation("Starting data loading");
            var loadResult = await _loader.LoadAsync(transformedData, context, cancellationToken);
            result.LoadedRecords = loadResult.LoadedRecords;
            result.LoadErrors = loadResult.ErrorCount;

            _logger.LogInformation("Loaded {RecordCount} records with {ErrorCount} errors", 
                result.LoadedRecords, result.LoadErrors);

            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = result.LoadErrors == 0 && result.TransformErrors == 0;

            _logger.LogInformation("ETL pipeline completed in {Duration}ms. Success: {Success}", 
                stopwatch.ElapsedMilliseconds, result.Success);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Duration = stopwatch.Elapsed;
            result.Success = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError(ex, "ETL pipeline failed after {Duration}ms", stopwatch.ElapsedMilliseconds);
            return result;
        }
    }
}

public interface IDataExtractor<T>
{
    Task<IEnumerable<T>> ExtractAsync(ETLContext context, CancellationToken cancellationToken = default);
}

public interface IDataTransformer<TSource, TTarget>
{
    Task<TTarget> TransformAsync(TSource source, ETLContext context, CancellationToken cancellationToken = default);
}

public interface IDataLoader<T>
{
    Task<LoadResult> LoadAsync(IEnumerable<T> data, ETLContext context, CancellationToken cancellationToken = default);
}
```

### Migration Orchestrator
```csharp
public class MigrationOrchestrator
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MigrationOrchestrator> _logger;
    private readonly IMigrationRepository _migrationRepository;

    public MigrationOrchestrator(
        IServiceProvider serviceProvider,
        ILogger<MigrationOrchestrator> logger,
        IMigrationRepository migrationRepository)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _migrationRepository = migrationRepository;
    }

    public async Task<MigrationExecutionResult> ExecuteMigrationsAsync(
        List<string> migrationNames = null, 
        CancellationToken cancellationToken = default)
    {
        var executionResult = new MigrationExecutionResult();
        var context = new MigrationContext();

        try
        {
            var migrations = GetMigrationsToExecute(migrationNames);
            _logger.LogInformation("Executing {MigrationCount} migrations", migrations.Count);

            foreach (var migration in migrations)
            {
                var migrationRecord = await _migrationRepository.GetMigrationRecordAsync(migration.Name);
                
                if (migrationRecord?.Status == MigrationStatus.Completed)
                {
                    _logger.LogInformation("Migration {MigrationName} already completed, skipping", migration.Name);
                    continue;
                }

                // Create or update migration record
                migrationRecord ??= new MigrationRecord
                {
                    Name = migration.Name,
                    Description = migration.Description
                };

                migrationRecord.Status = MigrationStatus.Running;
                migrationRecord.StartedAt = DateTime.UtcNow;
                await _migrationRepository.SaveMigrationRecordAsync(migrationRecord);

                // Execute migration
                var result = await migration.ExecuteAsync(context, cancellationToken);
                
                // Update migration record
                migrationRecord.Status = result.Success ? MigrationStatus.Completed : MigrationStatus.Failed;
                migrationRecord.CompletedAt = DateTime.UtcNow;
                migrationRecord.Duration = result.Duration;
                migrationRecord.RecordsProcessed = result.RecordsProcessed;
                migrationRecord.RecordsMigrated = result.RecordsMigrated;
                migrationRecord.ErrorMessage = result.ErrorMessage;

                await _migrationRepository.SaveMigrationRecordAsync(migrationRecord);

                executionResult.MigrationResults.Add(result);

                if (!result.Success)
                {
                    _logger.LogError("Migration {MigrationName} failed: {ErrorMessage}", 
                        migration.Name, result.ErrorMessage);
                    
                    if (context.StopOnError)
                    {
                        break;
                    }
                }
            }

            executionResult.Success = executionResult.MigrationResults.All(r => r.Success);
            return executionResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration orchestration failed");
            executionResult.Success = false;
            executionResult.ErrorMessage = ex.Message;
            return executionResult;
        }
    }

    private List<IDataMigration> GetMigrationsToExecute(List<string> migrationNames)
    {
        var allMigrations = _serviceProvider.GetServices<IDataMigration>().ToList();
        
        if (migrationNames?.Any() == true)
        {
            return allMigrations.Where(m => migrationNames.Contains(m.Name)).ToList();
        }

        return allMigrations.OrderBy(m => m.Name).ToList();
    }
}
```

### Migration Monitoring Dashboard
```csharp
[ApiController]
[Route("api/[controller]")]
public class MigrationController : ControllerBase
{
    private readonly MigrationOrchestrator _orchestrator;
    private readonly IMigrationRepository _repository;

    public MigrationController(MigrationOrchestrator orchestrator, IMigrationRepository repository)
    {
        _orchestrator = orchestrator;
        _repository = repository;
    }

    [HttpGet("status")]
    public async Task<ActionResult<List<MigrationStatusDto>>> GetMigrationStatus()
    {
        var records = await _repository.GetAllMigrationRecordsAsync();
        var statusList = records.Select(r => new MigrationStatusDto
        {
            Name = r.Name,
            Description = r.Description,
            Status = r.Status.ToString(),
            StartedAt = r.StartedAt,
            CompletedAt = r.CompletedAt,
            Duration = r.Duration,
            RecordsProcessed = r.RecordsProcessed,
            RecordsMigrated = r.RecordsMigrated,
            ErrorMessage = r.ErrorMessage
        }).ToList();

        return Ok(statusList);
    }

    [HttpPost("execute")]
    public async Task<ActionResult<MigrationExecutionResult>> ExecuteMigrations([FromBody] ExecuteMigrationsRequest request)
    {
        var result = await _orchestrator.ExecuteMigrationsAsync(request.MigrationNames);
        return Ok(result);
    }

    [HttpPost("rollback/{migrationName}")]
    public async Task<ActionResult> RollbackMigration(string migrationName)
    {
        // Implementation for rollback
        return Ok();
    }
}
```

## Deliverables

1. **Migration Framework**: Base classes and interfaces
2. **ETL Pipeline**: Extract, transform, load implementation
3. **Data Validation**: Comprehensive validation framework
4. **Progress Tracking**: Real-time migration monitoring
5. **Error Handling**: Robust error management and recovery
6. **Rollback Mechanisms**: Migration rollback capabilities
7. **Batch Processing**: Large dataset handling
8. **Migration Orchestrator**: Coordinated migration execution
9. **Monitoring Dashboard**: Web-based migration monitoring
10. **Data Quality Checks**: Post-migration validation

## Validation Checklist

- [ ] Migration framework handles large datasets efficiently
- [ ] ETL pipeline processes data with proper transformations
- [ ] Data validation ensures integrity throughout migration
- [ ] Progress tracking provides real-time status updates
- [ ] Error handling captures and logs all issues
- [ ] Rollback mechanisms can undo migrations safely
- [ ] Batch processing prevents memory issues
- [ ] Migration orchestrator coordinates complex migrations
- [ ] Monitoring dashboard provides visibility
- [ ] Data quality checks validate migration success