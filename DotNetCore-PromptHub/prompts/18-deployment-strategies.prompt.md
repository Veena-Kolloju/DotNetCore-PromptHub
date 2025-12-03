---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Deployment Strategies Framework

Implement comprehensive deployment strategies for .NET Core applications including blue-green, canary, and rolling deployments with CI/CD pipelines.

## Requirements

### 1. Deployment Patterns
- Blue-green deployment strategy
- Canary deployment with gradual rollout
- Rolling deployment with zero downtime
- Feature flag integration

### 2. CI/CD Pipeline
- Automated build and test execution
- Environment-specific deployments
- Rollback mechanisms
- Deployment monitoring

## Example Implementation

### Blue-Green Deployment
```yaml
# azure-pipelines-blue-green.yml
trigger:
  branches:
    include:
    - main
    - develop

variables:
  buildConfiguration: 'Release'
  azureSubscription: 'Production-ServiceConnection'
  resourceGroupName: 'rg-customerapi-prod'
  appServiceName: 'app-customerapi'

stages:
- stage: Build
  displayName: 'Build and Test'
  jobs:
  - job: BuildJob
    displayName: 'Build Job'
    pool:
      vmImage: 'ubuntu-latest'
    
    steps:
    - task: UseDotNet@2
      displayName: 'Use .NET 8 SDK'
      inputs:
        packageType: 'sdk'
        version: '8.0.x'
    
    - task: DotNetCoreCLI@2
      displayName: 'Restore packages'
      inputs:
        command: 'restore'
        projects: '**/*.csproj'
    
    - task: DotNetCoreCLI@2
      displayName: 'Build application'
      inputs:
        command: 'build'
        projects: '**/*.csproj'
        arguments: '--configuration $(buildConfiguration) --no-restore'
    
    - task: DotNetCoreCLI@2
      displayName: 'Run unit tests'
      inputs:
        command: 'test'
        projects: '**/*Tests.csproj'
        arguments: '--configuration $(buildConfiguration) --collect:"XPlat Code Coverage"'
    
    - task: PublishCodeCoverageResults@1
      displayName: 'Publish code coverage'
      inputs:
        codeCoverageTool: 'Cobertura'
        summaryFileLocation: '$(Agent.TempDirectory)/**/coverage.cobertura.xml'
    
    - task: DotNetCoreCLI@2
      displayName: 'Publish application'
      inputs:
        command: 'publish'
        projects: '**/CustomerAPI.csproj'
        arguments: '--configuration $(buildConfiguration) --output $(Build.ArtifactStagingDirectory)'
        zipAfterPublish: true
    
    - task: PublishBuildArtifacts@1
      displayName: 'Publish artifacts'
      inputs:
        pathToPublish: '$(Build.ArtifactStagingDirectory)'
        artifactName: 'drop'

- stage: DeployToStaging
  displayName: 'Deploy to Staging (Green)'
  dependsOn: Build
  condition: succeeded()
  jobs:
  - deployment: DeployStaging
    displayName: 'Deploy to Staging Environment'
    pool:
      vmImage: 'ubuntu-latest'
    environment: 'staging'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureWebApp@1
            displayName: 'Deploy to Green Slot'
            inputs:
              azureSubscription: '$(azureSubscription)'
              appType: 'webApp'
              appName: '$(appServiceName)'
              slotName: 'green'
              package: '$(Pipeline.Workspace)/drop/*.zip'
              appSettings: |
                -ASPNETCORE_ENVIRONMENT Staging
                -ConnectionStrings__DefaultConnection "$(StagingConnectionString)"
                -ApplicationInsights__InstrumentationKey "$(StagingAppInsightsKey)"
          
          - task: AzureAppServiceManage@0
            displayName: 'Start Green Slot'
            inputs:
              azureSubscription: '$(azureSubscription)'
              action: 'Start Azure App Service'
              webAppName: '$(appServiceName)'
              specifySlotOrASE: true
              resourceGroupName: '$(resourceGroupName)'
              slotName: 'green'

- stage: RunSmokeTests
  displayName: 'Run Smoke Tests'
  dependsOn: DeployToStaging
  jobs:
  - job: SmokeTests
    displayName: 'Execute Smoke Tests'
    pool:
      vmImage: 'ubuntu-latest'
    
    steps:
    - task: DotNetCoreCLI@2
      displayName: 'Run smoke tests'
      inputs:
        command: 'test'
        projects: '**/*SmokeTests.csproj'
        arguments: '--configuration $(buildConfiguration) --logger trx --collect:"XPlat Code Coverage"'
      env:
        TEST_BASE_URL: 'https://$(appServiceName)-green.azurewebsites.net'

- stage: SwapSlots
  displayName: 'Swap Blue-Green Slots'
  dependsOn: RunSmokeTests
  condition: succeeded()
  jobs:
  - deployment: SwapProduction
    displayName: 'Swap to Production'
    pool:
      vmImage: 'ubuntu-latest'
    environment: 'production'
    strategy:
      runOnce:
        deploy:
          steps:
          - task: AzureAppServiceManage@0
            displayName: 'Swap Green to Production'
            inputs:
              azureSubscription: '$(azureSubscription)'
              action: 'Swap Slots'
              webAppName: '$(appServiceName)'
              resourceGroupName: '$(resourceGroupName)'
              sourceSlot: 'green'
              targetSlot: 'production'
          
          - task: PowerShell@2
            displayName: 'Verify Production Health'
            inputs:
              targetType: 'inline'
              script: |
                $healthUrl = "https://$(appServiceName).azurewebsites.net/health"
                $maxAttempts = 10
                $attempt = 0
                
                do {
                  $attempt++
                  Write-Host "Health check attempt $attempt of $maxAttempts"
                  
                  try {
                    $response = Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 30
                    if ($response.status -eq "Healthy") {
                      Write-Host "Production deployment successful - health check passed"
                      exit 0
                    }
                  }
                  catch {
                    Write-Host "Health check failed: $($_.Exception.Message)"
                  }
                  
                  if ($attempt -lt $maxAttempts) {
                    Start-Sleep -Seconds 30
                  }
                } while ($attempt -lt $maxAttempts)
                
                Write-Host "Production health check failed after $maxAttempts attempts"
                exit 1
```

### Canary Deployment Strategy
```csharp
public class CanaryDeploymentService
{
    private readonly IFeatureManager _featureManager;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CanaryDeploymentService> _logger;

    public CanaryDeploymentService(
        IFeatureManager featureManager,
        IConfiguration configuration,
        ILogger<CanaryDeploymentService> logger)
    {
        _featureManager = featureManager;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> ShouldUseCanaryVersionAsync(HttpContext context)
    {
        // Check if user is in canary group
        if (await IsUserInCanaryGroupAsync(context))
        {
            return true;
        }

        // Check percentage rollout
        var canaryPercentage = _configuration.GetValue<int>("Deployment:CanaryPercentage", 0);
        if (canaryPercentage > 0)
        {
            var userId = GetUserId(context);
            var hash = ComputeHash(userId);
            var userPercentile = hash % 100;
            
            return userPercentile < canaryPercentage;
        }

        return false;
    }

    private async Task<bool> IsUserInCanaryGroupAsync(HttpContext context)
    {
        var userId = GetUserId(context);
        if (string.IsNullOrEmpty(userId))
            return false;

        // Check if user is explicitly in canary group
        return await _featureManager.IsEnabledAsync("CanaryDeployment", new TargetingContext
        {
            UserId = userId
        });
    }

    private string GetUserId(HttpContext context)
    {
        return context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
               ?? context.Request.Headers["X-User-Id"].FirstOrDefault()
               ?? context.Connection.RemoteIpAddress?.ToString();
    }

    private int ComputeHash(string input)
    {
        if (string.IsNullOrEmpty(input))
            return 0;

        return Math.Abs(input.GetHashCode());
    }
}

// Canary Middleware
public class CanaryMiddleware
{
    private readonly RequestDelegate _next;
    private readonly CanaryDeploymentService _canaryService;
    private readonly ILogger<CanaryMiddleware> _logger;

    public CanaryMiddleware(
        RequestDelegate next,
        CanaryDeploymentService canaryService,
        ILogger<CanaryMiddleware> logger)
    {
        _next = next;
        _canaryService = canaryService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var useCanary = await _canaryService.ShouldUseCanaryVersionAsync(context);
        
        if (useCanary)
        {
            context.Items["UseCanaryVersion"] = true;
            context.Response.Headers.Add("X-Canary-Version", "true");
            _logger.LogDebug("Request routed to canary version for user {UserId}", 
                context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        }

        await _next(context);
    }
}
```

### Rolling Deployment with Kubernetes
```yaml
# k8s-rolling-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: customerapi-deployment
  namespace: production
  labels:
    app: customerapi
    version: v1
spec:
  replicas: 6
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxUnavailable: 1
      maxSurge: 1
  selector:
    matchLabels:
      app: customerapi
  template:
    metadata:
      labels:
        app: customerapi
        version: v1
    spec:
      containers:
      - name: customerapi
        image: myregistry/customerapi:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: customerapi-secrets
              key: connection-string
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 30
          timeoutSeconds: 10
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        startupProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 30

---
apiVersion: v1
kind: Service
metadata:
  name: customerapi-service
  namespace: production
spec:
  selector:
    app: customerapi
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: ClusterIP

---
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: customerapi-ingress
  namespace: production
  annotations:
    kubernetes.io/ingress.class: nginx
    cert-manager.io/cluster-issuer: letsencrypt-prod
    nginx.ingress.kubernetes.io/rate-limit: "100"
spec:
  tls:
  - hosts:
    - api.company.com
    secretName: customerapi-tls
  rules:
  - host: api.company.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: customerapi-service
            port:
              number: 80
```

### Deployment Health Monitoring
```csharp
public class DeploymentHealthService
{
    private readonly IMetricsCollector _metrics;
    private readonly ILogger<DeploymentHealthService> _logger;
    private readonly HttpClient _httpClient;

    public DeploymentHealthService(
        IMetricsCollector metrics,
        ILogger<DeploymentHealthService> logger,
        HttpClient httpClient)
    {
        _metrics = metrics;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<DeploymentHealthResult> CheckDeploymentHealthAsync(string version, CancellationToken cancellationToken = default)
    {
        var result = new DeploymentHealthResult { Version = version };
        
        try
        {
            // Check application health
            var healthResponse = await _httpClient.GetAsync("/health", cancellationToken);
            result.ApplicationHealthy = healthResponse.IsSuccessStatusCode;

            // Check error rates
            var errorRate = await GetErrorRateAsync(version, TimeSpan.FromMinutes(5));
            result.ErrorRate = errorRate;
            result.ErrorRateAcceptable = errorRate < 0.01; // Less than 1%

            // Check response times
            var avgResponseTime = await GetAverageResponseTimeAsync(version, TimeSpan.FromMinutes(5));
            result.AverageResponseTime = avgResponseTime;
            result.ResponseTimeAcceptable = avgResponseTime < TimeSpan.FromMilliseconds(500);

            // Check resource usage
            var resourceUsage = await GetResourceUsageAsync(version);
            result.CpuUsage = resourceUsage.CpuUsage;
            result.MemoryUsage = resourceUsage.MemoryUsage;
            result.ResourceUsageAcceptable = resourceUsage.CpuUsage < 80 && resourceUsage.MemoryUsage < 80;

            result.OverallHealthy = result.ApplicationHealthy && 
                                   result.ErrorRateAcceptable && 
                                   result.ResponseTimeAcceptable && 
                                   result.ResourceUsageAcceptable;

            _logger.LogInformation("Deployment health check for version {Version}: {Status}", 
                version, result.OverallHealthy ? "Healthy" : "Unhealthy");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check deployment health for version {Version}", version);
            result.OverallHealthy = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private async Task<double> GetErrorRateAsync(string version, TimeSpan timeWindow)
    {
        // Implementation would query metrics store (Application Insights, Prometheus, etc.)
        // This is a simplified example
        await Task.Delay(100);
        return 0.005; // 0.5% error rate
    }

    private async Task<TimeSpan> GetAverageResponseTimeAsync(string version, TimeSpan timeWindow)
    {
        // Implementation would query metrics store
        await Task.Delay(100);
        return TimeSpan.FromMilliseconds(250);
    }

    private async Task<ResourceUsage> GetResourceUsageAsync(string version)
    {
        // Implementation would query container metrics
        await Task.Delay(100);
        return new ResourceUsage
        {
            CpuUsage = 45.5,
            MemoryUsage = 62.3
        };
    }
}

public class DeploymentHealthResult
{
    public string Version { get; set; }
    public bool ApplicationHealthy { get; set; }
    public double ErrorRate { get; set; }
    public bool ErrorRateAcceptable { get; set; }
    public TimeSpan AverageResponseTime { get; set; }
    public bool ResponseTimeAcceptable { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public bool ResourceUsageAcceptable { get; set; }
    public bool OverallHealthy { get; set; }
    public string ErrorMessage { get; set; }
}

public class ResourceUsage
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
}
```

### Rollback Strategy
```csharp
public class RollbackService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RollbackService> _logger;
    private readonly IMetricsCollector _metrics;

    public RollbackService(
        IConfiguration configuration,
        ILogger<RollbackService> logger,
        IMetricsCollector metrics)
    {
        _configuration = configuration;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<bool> ShouldTriggerRollbackAsync(DeploymentHealthResult healthResult)
    {
        var rollbackThresholds = _configuration.GetSection("Deployment:RollbackThresholds");
        
        var maxErrorRate = rollbackThresholds.GetValue<double>("MaxErrorRate", 0.05); // 5%
        var maxResponseTime = rollbackThresholds.GetValue<int>("MaxResponseTimeMs", 1000);
        var maxCpuUsage = rollbackThresholds.GetValue<double>("MaxCpuUsage", 90);
        var maxMemoryUsage = rollbackThresholds.GetValue<double>("MaxMemoryUsage", 90);

        var shouldRollback = !healthResult.ApplicationHealthy ||
                           healthResult.ErrorRate > maxErrorRate ||
                           healthResult.AverageResponseTime.TotalMilliseconds > maxResponseTime ||
                           healthResult.CpuUsage > maxCpuUsage ||
                           healthResult.MemoryUsage > maxMemoryUsage;

        if (shouldRollback)
        {
            _logger.LogWarning("Rollback triggered for version {Version}. Health check failed: {HealthResult}",
                healthResult.Version, System.Text.Json.JsonSerializer.Serialize(healthResult));

            _metrics.IncrementCounter("deployment_rollbacks_total", new Dictionary<string, string>
            {
                ["version"] = healthResult.Version,
                ["reason"] = GetRollbackReason(healthResult, maxErrorRate, maxResponseTime, maxCpuUsage, maxMemoryUsage)
            });
        }

        return shouldRollback;
    }

    private string GetRollbackReason(DeploymentHealthResult healthResult, double maxErrorRate, int maxResponseTime, double maxCpuUsage, double maxMemoryUsage)
    {
        if (!healthResult.ApplicationHealthy) return "application_unhealthy";
        if (healthResult.ErrorRate > maxErrorRate) return "high_error_rate";
        if (healthResult.AverageResponseTime.TotalMilliseconds > maxResponseTime) return "high_response_time";
        if (healthResult.CpuUsage > maxCpuUsage) return "high_cpu_usage";
        if (healthResult.MemoryUsage > maxMemoryUsage) return "high_memory_usage";
        return "unknown";
    }
}
```

## Deliverables

1. **Blue-Green Deployment**: Zero-downtime deployment strategy
2. **Canary Deployment**: Gradual rollout with feature flags
3. **Rolling Deployment**: Kubernetes rolling update configuration
4. **CI/CD Pipelines**: Automated build, test, and deployment
5. **Health Monitoring**: Deployment health validation
6. **Rollback Mechanisms**: Automated rollback triggers
7. **Feature Flags**: Runtime configuration management
8. **Deployment Metrics**: Deployment success tracking
9. **Environment Management**: Multi-environment deployment
10. **Security Scanning**: Automated security validation

## Validation Checklist

- [ ] Blue-green deployment provides zero downtime
- [ ] Canary deployment allows gradual rollout
- [ ] Rolling deployment maintains service availability
- [ ] CI/CD pipeline automates entire deployment process
- [ ] Health monitoring validates deployment success
- [ ] Rollback mechanisms trigger automatically on failures
- [ ] Feature flags enable runtime configuration
- [ ] Deployment metrics track success rates
- [ ] Environment-specific configurations managed
- [ ] Security scanning integrated into pipeline