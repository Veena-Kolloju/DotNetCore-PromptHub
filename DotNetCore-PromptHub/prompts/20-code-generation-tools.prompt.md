---
context: ../design-system/architecture-patterns.md
context: ../instructions/general-coding.instructions.md
---

# Code Generation Tools Framework

Create comprehensive code generation tools for .NET Core applications including scaffolding, templates, and automated boilerplate generation.

## Requirements

### 1. Template Engine
- Handlebars-based template system
- Dynamic code generation
- Customizable templates
- Multi-file generation support

### 2. Scaffolding Tools
- Entity-based code generation
- CRUD operation scaffolding
- API endpoint generation
- Test file creation

## Example Implementation

### Code Generator Core
```csharp
public interface ICodeGenerator
{
    Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default);
    Task<List<Template>> GetAvailableTemplatesAsync();
    Task<bool> ValidateTemplateAsync(string templateContent);
}

public class CodeGenerator : ICodeGenerator
{
    private readonly ITemplateEngine _templateEngine;
    private readonly IFileSystem _fileSystem;
    private readonly ILogger<CodeGenerator> _logger;

    public CodeGenerator(ITemplateEngine templateEngine, IFileSystem fileSystem, ILogger<CodeGenerator> logger)
    {
        _templateEngine = templateEngine;
        _fileSystem = fileSystem;
        _logger = logger;
    }

    public async Task<GenerationResult> GenerateAsync(GenerationRequest request, CancellationToken cancellationToken = default)
    {
        var result = new GenerationResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Starting code generation for {EntityName}", request.EntityName);

            // Validate request
            var validationResult = ValidateRequest(request);
            if (!validationResult.IsValid)
            {
                result.Success = false;
                result.Errors = validationResult.Errors;
                return result;
            }

            // Prepare generation context
            var context = CreateGenerationContext(request);

            // Generate files based on templates
            foreach (var templateConfig in request.Templates)
            {
                var generatedFile = await GenerateFileAsync(templateConfig, context, cancellationToken);
                if (generatedFile != null)
                {
                    result.GeneratedFiles.Add(generatedFile);
                }
            }

            stopwatch.Stop();
            result.Success = true;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation("Code generation completed for {EntityName} in {Duration}ms. Generated {FileCount} files",
                request.EntityName, stopwatch.ElapsedMilliseconds, result.GeneratedFiles.Count);

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.Success = false;
            result.ErrorMessage = ex.Message;

            _logger.LogError(ex, "Code generation failed for {EntityName} after {Duration}ms",
                request.EntityName, stopwatch.ElapsedMilliseconds);

            return result;
        }
    }

    private async Task<GeneratedFile> GenerateFileAsync(TemplateConfiguration templateConfig, GenerationContext context, CancellationToken cancellationToken)
    {
        try
        {
            var template = await _templateEngine.LoadTemplateAsync(templateConfig.TemplateName);
            var content = await _templateEngine.RenderAsync(template, context);
            
            var fileName = _templateEngine.RenderFileName(templateConfig.OutputPath, context);
            var fullPath = Path.Combine(context.OutputDirectory, fileName);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await _fileSystem.WriteAllTextAsync(fullPath, content, cancellationToken);

            return new GeneratedFile
            {
                FileName = fileName,
                FullPath = fullPath,
                Content = content,
                TemplateName = templateConfig.TemplateName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate file from template {TemplateName}", templateConfig.TemplateName);
            return null;
        }
    }

    private GenerationContext CreateGenerationContext(GenerationRequest request)
    {
        return new GenerationContext
        {
            EntityName = request.EntityName,
            EntityNamePlural = Pluralize(request.EntityName),
            EntityNameCamel = ToCamelCase(request.EntityName),
            EntityNameLower = request.EntityName.ToLowerInvariant(),
            Properties = request.Properties,
            Namespace = request.Namespace,
            OutputDirectory = request.OutputDirectory,
            ProjectName = request.ProjectName,
            GeneratedAt = DateTime.UtcNow,
            GeneratedBy = Environment.UserName,
            Relationships = request.Relationships,
            BusinessMethods = request.BusinessMethods,
            ValidationRules = request.ValidationRules
        };
    }

    private ValidationResult ValidateRequest(GenerationRequest request)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(request.EntityName))
            result.Errors.Add("Entity name is required");

        if (string.IsNullOrWhiteSpace(request.Namespace))
            result.Errors.Add("Namespace is required");

        if (string.IsNullOrWhiteSpace(request.OutputDirectory))
            result.Errors.Add("Output directory is required");

        if (!request.Templates.Any())
            result.Errors.Add("At least one template must be specified");

        result.IsValid = !result.Errors.Any();
        return result;
    }

    private string Pluralize(string word)
    {
        if (word.EndsWith("y"))
            return word.Substring(0, word.Length - 1) + "ies";
        if (word.EndsWith("s") || word.EndsWith("sh") || word.EndsWith("ch"))
            return word + "es";
        return word + "s";
    }

    private string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }
}
```

### Template Engine Implementation
```csharp
public interface ITemplateEngine
{
    Task<string> LoadTemplateAsync(string templateName);
    Task<string> RenderAsync(string template, object model);
    string RenderFileName(string fileNameTemplate, object model);
}

public class HandlebarsTemplateEngine : ITemplateEngine
{
    private readonly IHandlebars _handlebars;
    private readonly ILogger<HandlebarsTemplateEngine> _logger;
    private readonly Dictionary<string, string> _templateCache = new();

    public HandlebarsTemplateEngine(ILogger<HandlebarsTemplateEngine> logger)
    {
        _logger = logger;
        _handlebars = Handlebars.Create();
        RegisterHelpers();
    }

    public async Task<string> LoadTemplateAsync(string templateName)
    {
        if (_templateCache.TryGetValue(templateName, out var cachedTemplate))
        {
            return cachedTemplate;
        }

        var templatePath = Path.Combine("Templates", $"{templateName}.hbs");
        
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templatePath}");
        }

        var template = await File.ReadAllTextAsync(templatePath);
        _templateCache[templateName] = template;
        
        return template;
    }

    public async Task<string> RenderAsync(string template, object model)
    {
        try
        {
            var compiledTemplate = _handlebars.Compile(template);
            return compiledTemplate(model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render template");
            throw;
        }
    }

    public string RenderFileName(string fileNameTemplate, object model)
    {
        var compiledTemplate = _handlebars.Compile(fileNameTemplate);
        return compiledTemplate(model);
    }

    private void RegisterHelpers()
    {
        // PascalCase helper
        _handlebars.RegisterHelper("pascalCase", (writer, context, parameters) =>
        {
            if (parameters.Length > 0 && parameters[0] != null)
            {
                var value = parameters[0].ToString();
                writer.WriteSafeString(ToPascalCase(value));
            }
        });

        // camelCase helper
        _handlebars.RegisterHelper("camelCase", (writer, context, parameters) =>
        {
            if (parameters.Length > 0 && parameters[0] != null)
            {
                var value = parameters[0].ToString();
                writer.WriteSafeString(ToCamelCase(value));
            }
        });

        // pluralize helper
        _handlebars.RegisterHelper("pluralize", (writer, context, parameters) =>
        {
            if (parameters.Length > 0 && parameters[0] != null)
            {
                var value = parameters[0].ToString();
                writer.WriteSafeString(Pluralize(value));
            }
        });

        // lowercase helper
        _handlebars.RegisterHelper("lowercase", (writer, context, parameters) =>
        {
            if (parameters.Length > 0 && parameters[0] != null)
            {
                writer.WriteSafeString(parameters[0].ToString().ToLowerInvariant());
            }
        });

        // uppercase helper
        _handlebars.RegisterHelper("uppercase", (writer, context, parameters) =>
        {
            if (parameters.Length > 0 && parameters[0] != null)
            {
                writer.WriteSafeString(parameters[0].ToString().ToUpperInvariant());
            }
        });

        // eq helper for comparisons
        _handlebars.RegisterHelper("eq", (writer, options, context, parameters) =>
        {
            if (parameters.Length >= 2)
            {
                var left = parameters[0]?.ToString();
                var right = parameters[1]?.ToString();
                
                if (left == right)
                {
                    options.Template(writer, context);
                }
                else
                {
                    options.Inverse(writer, context);
                }
            }
        });
    }

    private string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return char.ToUpperInvariant(input[0]) + input.Substring(1);
    }

    private string ToCamelCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return char.ToLowerInvariant(input[0]) + input.Substring(1);
    }

    private string Pluralize(string word)
    {
        if (word.EndsWith("y"))
            return word.Substring(0, word.Length - 1) + "ies";
        if (word.EndsWith("s") || word.EndsWith("sh") || word.EndsWith("ch"))
            return word + "es";
        return word + "s";
    }
}
```

### Entity Scaffolding Tool
```csharp
public class EntityScaffolder
{
    private readonly ICodeGenerator _codeGenerator;
    private readonly ILogger<EntityScaffolder> _logger;

    public EntityScaffolder(ICodeGenerator codeGenerator, ILogger<EntityScaffolder> logger)
    {
        _codeGenerator = codeGenerator;
        _logger = logger;
    }

    public async Task<ScaffoldingResult> ScaffoldEntityAsync(EntityDefinition entityDefinition, ScaffoldingOptions options)
    {
        var result = new ScaffoldingResult();

        try
        {
            _logger.LogInformation("Scaffolding entity {EntityName}", entityDefinition.Name);

            var request = new GenerationRequest
            {
                EntityName = entityDefinition.Name,
                Namespace = options.Namespace,
                ProjectName = options.ProjectName,
                OutputDirectory = options.OutputDirectory,
                Properties = entityDefinition.Properties,
                Relationships = entityDefinition.Relationships,
                BusinessMethods = entityDefinition.BusinessMethods,
                ValidationRules = entityDefinition.ValidationRules,
                Templates = GetTemplatesForScaffolding(options)
            };

            var generationResult = await _codeGenerator.GenerateAsync(request);
            
            result.Success = generationResult.Success;
            result.GeneratedFiles = generationResult.GeneratedFiles;
            result.Errors = generationResult.Errors;
            result.ErrorMessage = generationResult.ErrorMessage;

            if (result.Success)
            {
                _logger.LogInformation("Successfully scaffolded entity {EntityName}. Generated {FileCount} files",
                    entityDefinition.Name, result.GeneratedFiles.Count);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scaffold entity {EntityName}", entityDefinition.Name);
            result.Success = false;
            result.ErrorMessage = ex.Message;
            return result;
        }
    }

    private List<TemplateConfiguration> GetTemplatesForScaffolding(ScaffoldingOptions options)
    {
        var templates = new List<TemplateConfiguration>();

        if (options.GenerateEntity)
        {
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "DomainEntity",
                OutputPath = "Domain/Entities/{{EntityName}}.cs"
            });
        }

        if (options.GenerateRepository)
        {
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "RepositoryInterface",
                OutputPath = "Application/Interfaces/I{{EntityName}}Repository.cs"
            });
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "RepositoryImplementation",
                OutputPath = "Infrastructure/Repositories/{{EntityName}}Repository.cs"
            });
        }

        if (options.GenerateService)
        {
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "ServiceInterface",
                OutputPath = "Application/Interfaces/I{{EntityName}}Service.cs"
            });
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "ServiceImplementation",
                OutputPath = "Application/Services/{{EntityName}}Service.cs"
            });
        }

        if (options.GenerateController)
        {
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "ApiController",
                OutputPath = "API/Controllers/{{EntityName}}Controller.cs"
            });
        }

        if (options.GenerateDTOs)
        {
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "CreateRequestDTO",
                OutputPath = "Application/DTOs/Create{{EntityName}}Request.cs"
            });
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "UpdateRequestDTO",
                OutputPath = "Application/DTOs/Update{{EntityName}}Request.cs"
            });
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "ResponseDTO",
                OutputPath = "Application/DTOs/{{EntityName}}Dto.cs"
            });
        }

        if (options.GenerateTests)
        {
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "EntityTests",
                OutputPath = "Tests/{{EntityName}}Tests.cs"
            });
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "ServiceTests",
                OutputPath = "Tests/{{EntityName}}ServiceTests.cs"
            });
            templates.Add(new TemplateConfiguration
            {
                TemplateName = "ControllerTests",
                OutputPath = "Tests/{{EntityName}}ControllerTests.cs"
            });
        }

        return templates;
    }
}
```

### CLI Tool Implementation
```csharp
public class CodeGeneratorCLI
{
    private readonly IServiceProvider _serviceProvider;

    public CodeGeneratorCLI(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<int> RunAsync(string[] args)
    {
        var app = new CommandLineApplication
        {
            Name = "dotnet-codegen",
            Description = ".NET Core Code Generator CLI"
        };

        app.HelpOption();

        // Generate entity command
        app.Command("entity", cmd =>
        {
            cmd.Description = "Generate code for an entity";

            var nameOption = cmd.Option<string>("--name|-n", "Entity name", CommandOptionType.SingleValue);
            var namespaceOption = cmd.Option<string>("--namespace|-ns", "Namespace", CommandOptionType.SingleValue);
            var outputOption = cmd.Option<string>("--output|-o", "Output directory", CommandOptionType.SingleValue);
            var propertiesOption = cmd.Option<string>("--properties|-p", "Properties (Name:Type:Required)", CommandOptionType.MultipleValue);
            var allOption = cmd.Option("--all|-a", "Generate all components", CommandOptionType.NoValue);

            cmd.OnExecuteAsync(async cancellationToken =>
            {
                if (!nameOption.HasValue())
                {
                    Console.WriteLine("Entity name is required");
                    return 1;
                }

                var entityDefinition = new EntityDefinition
                {
                    Name = nameOption.Value(),
                    Properties = ParseProperties(propertiesOption.Values)
                };

                var options = new ScaffoldingOptions
                {
                    Namespace = namespaceOption.Value() ?? "MyProject",
                    OutputDirectory = outputOption.Value() ?? Directory.GetCurrentDirectory(),
                    GenerateEntity = allOption.HasValue(),
                    GenerateRepository = allOption.HasValue(),
                    GenerateService = allOption.HasValue(),
                    GenerateController = allOption.HasValue(),
                    GenerateDTOs = allOption.HasValue(),
                    GenerateTests = allOption.HasValue()
                };

                var scaffolder = _serviceProvider.GetRequiredService<EntityScaffolder>();
                var result = await scaffolder.ScaffoldEntityAsync(entityDefinition, options);

                if (result.Success)
                {
                    Console.WriteLine($"Successfully generated {result.GeneratedFiles.Count} files:");
                    foreach (var file in result.GeneratedFiles)
                    {
                        Console.WriteLine($"  - {file.FileName}");
                    }
                    return 0;
                }
                else
                {
                    Console.WriteLine($"Generation failed: {result.ErrorMessage}");
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"  - {error}");
                    }
                    return 1;
                }
            });
        });

        // List templates command
        app.Command("templates", cmd =>
        {
            cmd.Description = "List available templates";

            cmd.OnExecuteAsync(async cancellationToken =>
            {
                var codeGenerator = _serviceProvider.GetRequiredService<ICodeGenerator>();
                var templates = await codeGenerator.GetAvailableTemplatesAsync();

                Console.WriteLine("Available templates:");
                foreach (var template in templates)
                {
                    Console.WriteLine($"  - {template.Name}: {template.Description}");
                }

                return 0;
            });
        });

        return await app.ExecuteAsync(args);
    }

    private List<PropertyDefinition> ParseProperties(IEnumerable<string> propertyStrings)
    {
        var properties = new List<PropertyDefinition>();

        foreach (var propertyString in propertyStrings)
        {
            var parts = propertyString.Split(':');
            if (parts.Length >= 2)
            {
                properties.Add(new PropertyDefinition
                {
                    Name = parts[0],
                    Type = parts[1],
                    IsRequired = parts.Length > 2 && bool.Parse(parts[2])
                });
            }
        }

        return properties;
    }
}
```

### Template Examples

#### Domain Entity Template (DomainEntity.hbs)
```handlebars
using System;
using System.Collections.Generic;
using {{Namespace}}.Domain.Common;

namespace {{Namespace}}.Domain.Entities
{
    /// <summary>
    /// {{EntityName}} domain entity
    /// Generated on {{GeneratedAt}} by {{GeneratedBy}}
    /// </summary>
    public class {{EntityName}} : BaseEntity
    {
        {{#each Properties}}
        /// <summary>
        /// {{Description}}
        /// </summary>
        {{#if IsRequired}}[Required]{{/if}}
        {{#if MaxLength}}[MaxLength({{MaxLength}})]{{/if}}
        public {{Type}} {{Name}} { get; {{#if IsReadOnly}}private {{/if}}set; }

        {{/each}}

        {{#each Relationships}}
        /// <summary>
        /// Navigation property for {{Name}}
        /// </summary>
        public virtual {{#if (eq Type "OneToMany")}}ICollection<{{RelatedEntity}}>{{else}}{{RelatedEntity}}{{/if}} {{Name}} { get; set; }{{#if (eq Type "OneToMany")}} = new List<{{RelatedEntity}}>();{{/if}}

        {{/each}}

        {{#each BusinessMethods}}
        /// <summary>
        /// {{Description}}
        /// </summary>
        public {{ReturnType}} {{Name}}({{#each Parameters}}{{Type}} {{Name}}{{#unless @last}}, {{/unless}}{{/each}})
        {
            {{Implementation}}
        }

        {{/each}}
    }
}
```

#### API Controller Template (ApiController.hbs)
```handlebars
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using {{Namespace}}.Application.Interfaces;
using {{Namespace}}.Application.DTOs;

namespace {{Namespace}}.API.Controllers
{
    /// <summary>
    /// {{EntityName}} management controller
    /// Generated on {{GeneratedAt}} by {{GeneratedBy}}
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class {{EntityName}}Controller : ControllerBase
    {
        private readonly I{{EntityName}}Service _{{camelCase EntityName}}Service;

        public {{EntityName}}Controller(I{{EntityName}}Service {{camelCase EntityName}}Service)
        {
            _{{camelCase EntityName}}Service = {{camelCase EntityName}}Service;
        }

        /// <summary>
        /// Get all {{pluralize EntityName}}
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResult<{{EntityName}}Dto>>>> GetAll([FromQuery] PaginationRequest request)
        {
            var result = await _{{camelCase EntityName}}Service.GetPagedAsync(request);
            return Ok(new ApiResponse<PagedResult<{{EntityName}}Dto>>
            {
                Success = true,
                Data = result
            });
        }

        /// <summary>
        /// Get {{EntityName}} by ID
        /// </summary>
        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<{{EntityName}}Dto>>> GetById(int id)
        {
            var result = await _{{camelCase EntityName}}Service.GetByIdAsync(id);
            if (result == null)
                return NotFound();

            return Ok(new ApiResponse<{{EntityName}}Dto>
            {
                Success = true,
                Data = result
            });
        }

        /// <summary>
        /// Create new {{EntityName}}
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<ApiResponse<{{EntityName}}Dto>>> Create([FromBody] Create{{EntityName}}Request request)
        {
            var result = await _{{camelCase EntityName}}Service.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<{{EntityName}}Dto>
            {
                Success = true,
                Data = result
            });
        }

        /// <summary>
        /// Update {{EntityName}}
        /// </summary>
        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<{{EntityName}}Dto>>> Update(int id, [FromBody] Update{{EntityName}}Request request)
        {
            var result = await _{{camelCase EntityName}}Service.UpdateAsync(id, request);
            if (result == null)
                return NotFound();

            return Ok(new ApiResponse<{{EntityName}}Dto>
            {
                Success = true,
                Data = result
            });
        }

        /// <summary>
        /// Delete {{EntityName}}
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<ActionResult<ApiResponse>> Delete(int id)
        {
            var success = await _{{camelCase EntityName}}Service.DeleteAsync(id);
            if (!success)
                return NotFound();

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "{{EntityName}} deleted successfully"
            });
        }
    }
}
```

## Deliverables

1. **Code Generator Core**: Template-based code generation engine
2. **Template Engine**: Handlebars-based template processing
3. **Entity Scaffolder**: Complete entity scaffolding tool
4. **CLI Tool**: Command-line interface for code generation
5. **Template Library**: Pre-built templates for common patterns
6. **Validation Framework**: Template and request validation
7. **File System Integration**: File creation and management
8. **Configuration System**: Customizable generation options
9. **Extension Points**: Plugin architecture for custom generators
10. **Documentation Generator**: Automatic documentation creation

## Validation Checklist

- [ ] Code generator produces syntactically correct code
- [ ] Template engine processes all template features correctly
- [ ] Entity scaffolder generates complete CRUD functionality
- [ ] CLI tool provides intuitive command interface
- [ ] Template library covers common development patterns
- [ ] Validation framework prevents invalid generation
- [ ] File system integration handles paths correctly
- [ ] Configuration system allows customization
- [ ] Extension points enable custom generators
- [ ] Generated documentation is accurate and complete