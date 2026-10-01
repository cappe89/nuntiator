namespace Nuntiator.Analyzers.Tests;

public class InvalidMiddlewareTypeAnalyzerTests
{
    private readonly InvalidMiddlewareTypeAnalyzer _analyzer = new();

    [Fact]
    public async Task ValidMiddleware_AddMiddleware_ProducesNoDiagnostics()
    {
        const string source = """
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Nuntiator;

        public class ValidMiddleware<TCommand, TResponse> : ICommandMiddleware<TCommand, TResponse>
            where TCommand : ICommand<TResponse>
        {
            public Task<TResponse> HandleAsync(TCommand command, CommandHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
            {
                return next();
            }
        }

        public class Setup
        {
            public void Configure(IServiceCollection services)
            {
                services.AddNuntiator(cfg =>
                {
                    cfg.AddOpenMiddleware(typeof(ValidMiddleware<,>));
                });
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ValidGenericMiddleware_AddMiddlewareGeneric_ProducesNoDiagnostics()
    {
        const string source = """
        using System.Threading;
        using System.Threading.Tasks;
        using Microsoft.Extensions.DependencyInjection;
        using Nuntiator;

        public record MyCommand : ICommand<int>;

        public class SpecificMiddleware : ICommandMiddleware<MyCommand, int>
        {
            public Task<int> HandleAsync(MyCommand command, CommandHandlerDelegate<int> next, CancellationToken cancellationToken)
            {
                return next();
            }
        }

        public class Setup
        {
            public void Configure(IServiceCollection services)
            {
                services.AddNuntiator(cfg =>
                {
                    cfg.AddMiddleware<SpecificMiddleware>();
                });
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task InvalidType_AddMiddlewareTypeOf_ReportsDiagnosticNUNT002()
    {
        const string source = """
        using Microsoft.Extensions.DependencyInjection;
        using Nuntiator;

        public class NotAMiddleware
        {
        }

        public class Setup
        {
            public void Configure(IServiceCollection services)
            {
                services.AddNuntiator(cfg =>
                {
                    cfg.AddMiddleware(typeof(NotAMiddleware));
                });
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.InvalidMiddlewareType, diagnostic.Id);
        Assert.Contains("NotAMiddleware", diagnostic.GetMessage());
        Assert.Contains("AddMiddleware", diagnostic.GetMessage());
    }

    [Fact]
    public async Task InvalidType_AddMiddlewareGeneric_ReportsDiagnosticNUNT002()
    {
        const string source = """
        using Microsoft.Extensions.DependencyInjection;
        using Nuntiator;

        public class NotAMiddleware
        {
        }

        public class Setup
        {
            public void Configure(IServiceCollection services)
            {
                services.AddNuntiator(cfg =>
                {
                    cfg.AddMiddleware<NotAMiddleware>();
                });
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.InvalidMiddlewareType, diagnostic.Id);
        Assert.Contains("NotAMiddleware", diagnostic.GetMessage());
    }

    [Fact]
    public async Task InvalidType_AddOpenMiddleware_ReportsDiagnosticNUNT002()
    {
        const string source = """
        using System.Collections.Generic;
        using Microsoft.Extensions.DependencyInjection;
        using Nuntiator;

        public class Setup
        {
            public void Configure(IServiceCollection services)
            {
                services.AddNuntiator(cfg =>
                {
                    cfg.AddOpenMiddleware(typeof(List<>));
                });
            }
        }
        """;

        var diagnostics = await AnalyzerTestHelper.GetDiagnosticsAsync(source, _analyzer);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(DiagnosticIds.InvalidMiddlewareType, diagnostic.Id);
        Assert.Contains("List", diagnostic.GetMessage());
    }
}
