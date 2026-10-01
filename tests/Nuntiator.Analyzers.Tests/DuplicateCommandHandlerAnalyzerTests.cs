namespace Nuntiator.Analyzers.Tests;

public class DuplicateCommandHandlerAnalyzerTests
{
    private readonly DuplicateCommandHandlerAnalyzer _analyzer = new();

    [Fact]
    public async Task SingleHandler_ProducesNoDiagnostics()
    {
        const string source = """
        using System.Threading;
        using System.Threading.Tasks;
        using Nuntiator;

        public record CreateOrderCommand(int OrderId) : ICommand<int>;

        public class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, int>
        {
            public Task<int> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
            {
                return Task.FromResult(command.OrderId);
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task DuplicateHandlers_SameCommand_ReportsDiagnosticNUNT003()
    {
        const string source = """
        using System.Threading;
        using System.Threading.Tasks;
        using Nuntiator;

        public record CreateOrderCommand(int OrderId) : ICommand<int>;

        public class CreateOrderCommandHandlerA : ICommandHandler<CreateOrderCommand, int>
        {
            public Task<int> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
            {
                return Task.FromResult(command.OrderId);
            }
        }

        public class CreateOrderCommandHandlerB : ICommandHandler<CreateOrderCommand, int>
        {
            public Task<int> HandleAsync(CreateOrderCommand command, CancellationToken cancellationToken)
            {
                return Task.FromResult(command.OrderId);
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal(DiagnosticIds.DuplicateCommandHandler, d.Id);
            Assert.Contains("CreateOrderCommand", d.GetMessage());
            Assert.Contains("CreateOrderCommandHandlerA", d.GetMessage());
            Assert.Contains("CreateOrderCommandHandlerB", d.GetMessage());
        });
    }

    [Fact]
    public async Task DuplicateHandlers_VoidCommand_ReportsDiagnosticNUNT003()
    {
        const string source = """
        using System.Threading;
        using System.Threading.Tasks;
        using Nuntiator;

        public record SendEmailCommand(string To) : ICommand;

        public class SendEmailCommandHandler1 : ICommandHandler<SendEmailCommand>
        {
            public Task HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }

        public class SendEmailCommandHandler2 : ICommandHandler<SendEmailCommand>
        {
            public Task HandleAsync(SendEmailCommand command, CancellationToken cancellationToken)
            {
                return Task.CompletedTask;
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);

        Assert.Equal(2, diagnostics.Length);
        Assert.All(diagnostics, d =>
        {
            Assert.Equal(DiagnosticIds.DuplicateCommandHandler, d.Id);
            Assert.Contains("SendEmailCommand", d.GetMessage());
        });
    }

    [Fact]
    public async Task AbstractHandlerWithConcreteHandler_ProducesNoDiagnostics()
    {
        const string source = """
        using System.Threading;
        using System.Threading.Tasks;
        using Nuntiator;

        public record DoSomethingCommand : ICommand<bool>;

        public abstract class BaseDoSomethingHandler : ICommandHandler<DoSomethingCommand, bool>
        {
            public abstract Task<bool> HandleAsync(DoSomethingCommand command, CancellationToken cancellationToken);
        }

        public class ConcreteDoSomethingHandler : BaseDoSomethingHandler
        {
            public override Task<bool> HandleAsync(DoSomethingCommand command, CancellationToken cancellationToken)
            {
                return Task.FromResult(true);
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);
        Assert.Empty(diagnostics);
    }
}
