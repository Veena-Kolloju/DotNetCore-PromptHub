# Clean Architecture Demo

A .NET Core solution implementing Clean Architecture with CQRS, MediatR, and Entity Framework Core.

## Architecture

- **Domain**: Core business entities and interfaces
- **Application**: Use cases, commands, queries, and handlers with CQRS
- **Infrastructure**: Data access and external service implementations
- **API**: Controllers, middleware, and configuration

## Features

- ✅ Clean Architecture layers properly separated
- ✅ CQRS pattern implemented with MediatR
- ✅ Dependency injection configured for all layers
- ✅ Validation pipeline integrated with FluentValidation
- ✅ Exception handling middleware implemented
- ✅ Logging configured with Serilog
- ✅ Health checks endpoints available
- ✅ Configuration management setup
- ✅ Unit test projects created
- ✅ Architecture tests implemented

## Getting Started

1. Update connection string in `appsettings.json`
2. Run `dotnet restore` to restore packages
3. Run `dotnet build` to build the solution
4. Run `dotnet run --project src/CleanArchitectureDemo.API` to start the API

## API Endpoints

- `GET /api/customers/{id}` - Get customer by ID
- `POST /api/customers` - Create new customer
- `GET /health` - Health check endpoint

## Testing

- Run `dotnet test` to execute all tests
- Unit tests validate business logic
- Architecture tests ensure Clean Architecture principles