# Nuntiator 🕊️

**Nuntiator** è una libreria leggera, moderna e ad alte prestazioni per **.NET 10** che replica e semplifica il pattern mediatore di **MediatR**.

Offre il disaccoppiamento tra mittente e ricevitore mediante command e command handler, registrazione automatica tramite Dependency Injection e supporto completo a pipeline di middleware personalizzabili prima e dopo l'esecuzione degli handler.

---

## 🚀 Caratteristiche Principali

- 🎯 **Command con e senza risposta**: Interfacce `ICommand<TResponse>` e `ICommand` (void/Unit).
- ⚙️ **Command Handler dedicati**: `ICommandHandler<TCommand, TResponse>` e `ICommandHandler<TCommand>` asincroni con supporto a `CancellationToken`.
- 🔌 **Pipeline & Middleware**: Interfaccia `ICommandMiddleware<TCommand, TResponse>` (e alias `IPipelineBehavior<,>` per compatibilità con MediatR) con supporto a middleware open-generic e closed-generic, short-circuit ed exception handling.
- 📦 **Dependency Injection Automatica**: Metodo di estensione `services.AddNuntiator(...)` con scansione automatica degli assembly e gestione dei cicli di vita (`Transient`, `Scoped`, `Singleton`).
- ⚡ **Alte Prestazioni**: Cache concorrente degli invoker generici (`PipelineInvokerCache`), eliminando overhead di reflection a runtime durante il dispatch.
- 🧪 **Test Unitari Completi**: Suite di test xUnit inclusa che copre tutti i casi d'uso (handler, middleware, DI, pipeline order, short-circuit, eccezioni e struct `Unit`).

---

## 📁 Struttura del Progetto

```text
├── Nuntiator.sln
├── src/
│   └── Nuntiator/
│       ├── Abstractions/
│       │   ├── ICommand.cs
│       │   ├── ICommandHandler.cs
│       │   ├── ICommandMiddleware.cs
│       │   ├── IPipelineBehavior.cs
│       │   ├── INuntiator.cs
│       │   └── Unit.cs
│       ├── DependencyInjection/
│       │   ├── NuntiatorConfiguration.cs
│       │   └── ServiceCollectionExtensions.cs
│       ├── Exceptions/
│       │   ├── NuntiatorException.cs
│       │   └── HandlerNotFoundException.cs
│       ├── Internal/
│       │   ├── PipelineInvoker.cs
│       │   └── VoidCommandHandlerAdapter.cs
│       ├── Nuntiator.cs
│       └── Nuntiator.csproj
└── tests/
    └── Nuntiator.Tests/
        ├── CommandsTests.cs
        ├── DependencyInjectionTests.cs
        ├── MiddlewareTests.cs
        ├── UnitTests.cs
        ├── TestFixtures.cs
        └── Nuntiator.Tests.csproj
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
        // Logica di creazione utente
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
        // Logica di invio email
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

#### Esempio: Short-Circuiting Middleware
```csharp
public class ValidationMiddleware : ICommandMiddleware<CreateUserCommand, int>
{
    public async Task<int> HandleAsync(
        CreateUserCommand command,
        CommandHandlerDelegate<int> next,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            throw new ArgumentException("Username non può essere vuoto.");
        }

        return await next();
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
    cfg.AddMiddleware<ValidationMiddleware>();
    // cfg.Lifetime = ServiceLifetime.Scoped; // Default: Transient
});

// Oppure registrazione rapida passando uno o più assembly o marker types:
builder.Services.AddNuntiator(typeof(Program));
```

---

### 5. Invocazione con `INuntiator`

Nei tuoi controller, endpoint Minimal API o servizi di background:

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

## 🧪 Esecuzione dei Test

Tutti i test unitari possono essere eseguiti con il comando .NET CLI:

```bash
dotnet test
```
