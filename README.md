# Nuntiator 🕊️

[![NuGet](https://img.shields.io/nuget/v/Nuntiator.svg)](https://www.nuget.org/packages/Nuntiator)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

**Nuntiator** è una libreria leggera, moderna e ad alte prestazioni per **.NET 10** che replica e semplifica il pattern mediatore di **MediatR**.

Offre il disaccoppiamento tra mittente e ricevitore mediante command e command handler, registrazione automatica tramite Dependency Injection, pipeline di middleware personalizzabili ed estensibili, e **Roslyn Analyzers** per la validazione a tempo di compilazione.

---

## 🚀 Caratteristiche Principali

- 🎯 **Command con e senza risposta**: Interfacce `ICommand<TResponse>` e `ICommand` (void/Unit).
- ⚙️ **Command Handler dedicati**: `ICommandHandler<TCommand, TResponse>` e `ICommandHandler<TCommand>` asincroni con supporto a `CancellationToken`.
- 🔌 **Pipeline & Middleware**: Interfaccia `ICommandMiddleware<TCommand, TResponse>` (e alias `IPipelineBehavior<,>` per compatibilità con MediatR) con supporto a middleware open-generic e closed-generic, short-circuit ed exception handling.
- 📦 **Dependency Injection Automatica**: Metodo di estensione `services.AddNuntiator(...)` con scansione automatica degli assembly e gestione dei cicli di vita (`Transient`, `Scoped`, `Singleton`).
- ⚡ **Alte Prestazioni**: Cache concorrente degli invoker generici (`PipelineInvokerCache`), eliminando overhead di reflection a runtime durante il dispatch.
- 🔍 **Roslyn Analyzers a Compile-Time**: Validazione statica in tempo reale nell'IDE e durante il build per prevenire errori di configurazione:
  - `NUNT002`: Verifica che i tipi passati a `AddMiddleware` implementino effettivamente `ICommandMiddleware<,>` o `IPipelineBehavior<,>`.
  - `NUNT003`: Segnala handler duplicati registrati per lo stesso comando nello stesso progetto.
- 🧪 **Test Unitari Completi**: Suite di 44 test xUnit che copre tutti i casi d'uso (handler, middleware, DI, pipeline order, short-circuit, eccezioni, struct `Unit` e tutti gli analyzer Roslyn).

---

## 📦 Installazione

Puoi installare Nuntiator tramite la .NET CLI o il Package Manager:

**.NET CLI:**
```bash
dotnet add package Nuntiator
```

**Package Manager Console:**
```powershell
Install-Package Nuntiator
```

---

## 📁 Struttura del Progetto

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
│       ├── InvalidMiddlewareTypeAnalyzer.cs      <- Diagnostica NUNT002
│       ├── DuplicateCommandHandlerAnalyzer.cs    <- Diagnostica NUNT003
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

## 🛠️ Guida all'Uso

### 1. Definizione dei Command

#### Command con risposta:
```csharp
using Nuntiator;

public record CreateUserCommand(string Username, string Email) : ICommand<int>;
```

#### Command senza risposta (void):
```csharp
using Nuntiator;

public record SendWelcomeEmailCommand(string Email) : ICommand;
```

---

### 2. Creazione dei Command Handler

#### Handler con risposta:
```csharp
using Nuntiator;

public class CreateUserCommandHandler : ICommandHandler<CreateUserCommand, int>
{
    public async Task<int> HandleAsync(CreateUserCommand command, CancellationToken cancellationToken = default)
    {
        await Task.Delay(10, cancellationToken);
        return 42; // Id utente creato
    }
}
```

#### Handler senza risposta:
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

### 3. Creazione e Configurazione dei Middleware

I middleware consentono di intercettare l'esecuzione dei comandi prima e dopo l'handler, misurare i tempi di esecuzione, validare input, gestire transazioni o effettuare short-circuit.

#### Esempio: Logging Middleware (Open-Generic)
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
        Console.WriteLine($"[START] Esecuzione di {typeof(TCommand).Name}");

        var response = await next();

        Console.WriteLine($"[FINISH] Completato {typeof(TCommand).Name}");
        return response;
    }
}
```

---

### 4. Registrazione in Dependency Injection

Nel file `Program.cs` o nella configurazione dei servizi:

```csharp
// 1. Scansione automatica dell'assembly corrente con configurazione middleware
builder.Services.AddNuntiator(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(Program).Assembly);
    cfg.AddOpenMiddleware(typeof(LoggingMiddleware<,>));
    // cfg.Lifetime = ServiceLifetime.Scoped; // Default: Transient
});

// Oppure registrazione rapida passando uno o più assembly o marker types:
builder.Services.AddNuntiator(typeof(Program));
```

---

### 5. Invocazione con `INuntiator`

Nei controller, endpoint Minimal API o worker services:

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

> 💡 **Compatibilità MediatR**: È possibile invocare sia `await nuntiator.SendAsync(...)` che `await nuntiator.Send(...)`.

---

## 🔍 Roslyn Analyzers

La libreria include analizzatori statici a tempo di compilazione:

| ID | Severità | Descrizione |
|---|---|---|
| **`NUNT002`** | `Error` | Segnala immediatamente se un tipo passato a `AddMiddleware` o `AddOpenMiddleware` non implementa l'interfaccia middleware corretta. |
| **`NUNT003`** | `Warning` | Rileva se esistono più classi handler per lo stesso comando nello stesso progetto, prevenendo comportamenti non deterministici a runtime. |

Gli analyzer sono compilati con supporto multi-target (`net10.0` e `netstandard2.0`) per garantire piena compatibilità sia con il compilatore di .NET 10 sia con gli host IDE (Visual Studio, VS Code, JetBrains Rider). Quando il pacchetto NuGet `Nuntiator` viene installato, gli analyzer operano automaticamente come dipendenza silenziosa (`analyzers/dotnet/cs`).

---

## 🧪 Esecuzione dei Test

Tutti i 44 test unitari possono essere eseguiti con il comando .NET CLI:

```bash
dotnet test
```

Risultato:
```text
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35 - Nuntiator.Tests.dll (net10.0)
Passed!  - Failed: 0, Passed:  9, Skipped: 0, Total:  9 - Nuntiator.Analyzers.Tests.dll (net10.0)
```
