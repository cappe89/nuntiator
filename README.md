# Nuntiator 🕊️

[![NuGet](https://img.shields.io/nuget/v/Nuntiator.svg)](https://www.nuget.org/packages/Nuntiator)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

**Nuntiator** is a lightweight, modern, and high-performance library for **.NET 10** that replicates and simplifies the mediator pattern of **MediatR**.

It provides decoupling between sender and receiver using commands and command handlers, automatic registration via Dependency Injection, customizable and extensible middleware pipelines, and **Roslyn Analyzers** for compile-time validation.

---

## 🚀 Key Features

- 🎯 **Commands with and without response**: `ICommand<TResponse>` and `ICommand` (void/Unit) interfaces.
- ⚙️ **Dedicated Command Handlers**: Async `ICommandHandler<TCommand, TResponse>` and `ICommandHandler<TCommand>` with `CancellationToken` support.
- 🔌 **Pipeline & Middleware**: `ICommandMiddleware<TCommand, TResponse>` interface (and `IPipelineBehavior<,>` alias for MediatR compatibility) with support for open-generic and closed-generic middlewares, short-circuiting, and exception handling.
- 📦 **Automatic Dependency Injection**: `services.AddNuntiator(...)` extension method with automatic assembly scanning and lifetime management (`Transient`, `Scoped`, `Singleton`).
- ⚡ **High Performance**: Concurrent generic invoker caching (`PipelineInvokerCache`), eliminating runtime reflection overhead during command dispatch.
- 🔍 **Compile-Time Roslyn Analyzers**: Real-time static analysis in the IDE and during build to prevent configuration errors:
  - `NUNT002`: Verifies that types passed to `AddMiddleware` implement `ICommandMiddleware<,>` or `IPipelineBehavior<,>`.
  - `NUNT003`: Reports duplicate handler classes registered for the same command within the same project.
- 🧪 **Comprehensive Unit Tests**: Suite of 44 xUnit tests covering all use cases (handlers, middlewares, DI, pipeline execution order, short-circuiting, exceptions, `Unit` struct, and all Roslyn analyzers).

---

## 📦 Installation

You can install Nuntiator via the .NET CLI or Package Manager:

**.NET CLI:**
```bash
dotnet add package Nuntiator
```

**Package Manager Console:**
```powershell
Install-Package Nuntiator
```

---

## 📁 Project Structure

```text
├── Nuntiator.slnx
├── src/
│   ├── Nuntiator/
│   │   ├── Abstractions/
│   │   │   ├── ICommand.cs
│   │   │   ├── ICommandHandler.cs
│   │   │   ├── ICommandMiddleware.cs
│   │   │   ├── IPipelineBehavior.cs
│   │   │   ├── INuntiator.cs
│   │   │   └── Unit.cs
│   │   ├── DependencyInjection/
│   │   │   ├── NuntiatorConfiguration.cs
│   │   │   └── ServiceCollectionExtensions.cs
│   │   ├── Exceptions/
│   │   │   ├── NuntiatorException.cs
│   │   │   └── HandlerNotFoundException.cs
│   │   ├── Internal/
│   │   │   ├── PipelineInvoker.cs
│   │   │   └── VoidCommandHandlerAdapter.cs
│   │   ├── Nuntiator.cs
│   │   └── Nuntiator.csproj
│   └── Nuntiator.Analyzers/
│       ├── DiagnosticIds.cs
│       ├── InvalidMiddlewareTypeAnalyzer.cs      <- Diagnostic NUNT002
│       ├── DuplicateCommandHandlerAnalyzer.cs    <- Diagnostic NUNT003
│       └── Nuntiator.Analyzers.csproj
└── tests/
    ├── Nuntiator.Tests/
    │   ├── CommandsTests.cs
    │   ├── DependencyInjectionTests.cs
    │   ├── MiddlewareTests.cs
    │   ├── UnitTests.cs
    │   ├── TestFixtures.cs
    │   └── Nuntiator.Tests.csproj
    └── Nuntiator.Analyzers.Tests/
        ├── InvalidMiddlewareTypeAnalyzerTests.cs
        ├── DuplicateCommandHandlerAnalyzerTests.cs
        ├── AnalyzerTestHelper.cs
        └── Nuntiator.Analyzers.Tests.csproj
```

---

## 🛠️ Usage Guide

### 1. Defining Commands

#### Command with response:
```csharp
using Nuntiator;

public record CreateUserCommand(string Username, string Email) : ICommand<int>;
```

#### Command without response (void):
```csharp
using Nuntiator;

public record SendWelcomeEmailCommand(string Email) : ICommand;
```

---

### 2. Creating Command Handlers

#### Handler with response:
```csharp
using Nuntiator;

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, int>
{
    public async Task<int> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        return 42; // Created user ID
    }
}
```

#### Handler without response:
```csharp
using Nuntiator;

public class SendWelcomeEmailCommandHandler : ICommandHandler<SendWelcomeEmailCommand>
{
    public async Task HandleAsync(SendWelcomeEmailCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
    }
}
```

---

### 3. Creating and Configuring Middlewares

Middlewares allow you to intercept command execution before and after the handler, measure execution time, validate inputs, manage transactions, or perform short-circuiting.

#### Example: Logging Middleware (Open-Generic)
```csharp
using Nuntiator;

public class LoggingMiddleware<TCommand, TResponse> : ICommandMiddleware<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TCommand command,
        CommandHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[START] Executing {typeof(TCommand).Name}");

        var response = await next();

        Console.WriteLine($"[FINISH] Completed {typeof(TCommand).Name}");
        return response;
    }
}
```

---

### 4. Dependency Injection Registration

In your `Program.cs` or service configuration:

```csharp
// 1. Automatic scanning of the current assembly with middleware configuration
builder.Services.AddNuntiator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenMiddleware(typeof(LoggingMiddleware<,>));
    // cfg.Lifetime = ServiceLifetime.Scoped; // Default: Transient
});

// Or quick registration passing one or more assemblies or marker types:
builder.Services.AddNuntiator(typeof(Program));
```

---

### 5. Dispatching with `INuntiator`

In controllers, Minimal API endpoints, or worker services:

```csharp
app.MapPost("/users", async (CreateUserCommand cmd, INuntiator nuntiator) =>
{
    int userId = await nuntiator.SendAsync(cmd);
    return Results.Ok(new { Id = userId });
});

app.MapPost("/welcome", async (SendWelcomeEmailCommand cmd, INuntiator nuntiator) =>
{
    await nuntiator.SendAsync(cmd);
    return Results.NoContent();
});
```

> 💡 **MediatR Compatibility**: You can invoke both `await nuntiator.SendAsync(...)` and `await nuntiator.Send(...)`.

---

### 6. Compile-Time Roslyn Analyzers

The library includes static analyzers to catch configuration issues during build time:

| ID | Severity | Description |
|---|---|---|
| **`NUNT002`** | `Error` | Flags if a type passed to `AddMiddleware` or `AddOpenMiddleware` does not implement the correct middleware interface. |
| **`NUNT003`** | `Warning` | Detects if multiple handler classes exist for the same command within the same project, preventing non-deterministic runtime behavior. |

The analyzers are multi-targeted (`net10.0` and `netstandard2.0`) to ensure full compatibility with both the .NET 10 compiler and IDE hosts (Visual Studio, VS Code, JetBrains Rider). When the `Nuntiator` NuGet package is installed, the analyzers operate automatically as a silent analyzer dependency (`analyzers/dotnet/cs`).

---

## 🧪 Running Tests

All 44 unit tests can be executed using the .NET CLI:

```bash
dotnet test
```

Output:
```text
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35 - Nuntiator.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed:  9, Skipped: 0, Total:  9 - Nuntiator.Analyzers.Tests.dll (net10.0)
```
