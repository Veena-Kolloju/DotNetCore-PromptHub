# .NET Core Development Guardrails

## Security Guardrails
- Do not hardcode connection strings, API keys, or secrets
- Do not use string concatenation for SQL queries
- Do not expose internal implementation details in API responses
- Do not log sensitive information (passwords, tokens, PII)
- Always validate and sanitize user inputs
- Always use HTTPS in production environments
- Always implement proper authentication and authorization
- Always use parameterized queries or ORM

## Performance Guardrails
- Do not use synchronous I/O operations in async contexts
- Do not create unnecessary database round trips (N+1 problem)
- Do not load entire collections when pagination is needed
- Do not ignore memory management for large datasets
- Always use async/await for I/O operations
- Always implement proper caching strategies
- Always use connection pooling for database connections
- Always dispose of resources properly (using statements)

## Code Quality Guardrails
- Do not use magic numbers or strings
- Do not create god classes or methods
- Do not ignore exception handling
- Do not skip unit tests for business logic
- Always follow SOLID principles
- Always use meaningful variable and method names
- Always implement proper logging
- Always handle null reference scenarios

## Architecture Guardrails
- Do not reference higher layers from lower layers
- Do not put business logic in controllers
- Do not create circular dependencies
- Do not bypass the repository pattern for data access
- Always maintain clear separation of concerns
- Always use dependency injection for service resolution
- Always implement interfaces for testability
- Always follow Clean Architecture principles