# General Coding Instructions for .NET Core

## Core Principles
- Use C# 12 and .NET 8 features
- Follow Clean Architecture principles with clear layer separation
- Implement SOLID principles throughout the codebase
- Use dependency injection for all service registrations
- Apply async/await patterns for I/O operations
- Implement proper error handling and logging
- Use strongly typed configurations
- Follow consistent naming conventions (PascalCase for public members, camelCase for private)

## Architecture Guidelines
- Domain layer: Pure business logic, no external dependencies
- Application layer: Use cases, commands, queries, handlers
- Infrastructure layer: Data access, external services, implementations
- API layer: Controllers, middleware, filters, minimal APIs

## Code Quality Standards
- Write self-documenting code with meaningful names
- Keep methods focused and under 20 lines when possible
- Use guard clauses for early returns
- Implement comprehensive unit tests with 80%+ coverage
- Use structured logging with correlation IDs
- Handle exceptions gracefully with proper error responses
- Validate all inputs at API boundaries
- Use cancellation tokens for async operations