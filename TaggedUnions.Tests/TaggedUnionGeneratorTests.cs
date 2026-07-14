using Microsoft.CodeAnalysis;
using Xunit;

namespace TaggedUnions.Tests;

public class TaggedUnionGeneratorTests
{
    [Fact]
    public void ShorthandCaseCompilesAndGeneratesPropertiesAndOperator()
    {
        const string source = """
            namespace Sample;

            [TaggedUnions.TaggedUnion]
            public abstract partial class Response
            {
                private Response() { }

                public sealed partial class OK(string Value) : Response;
            }
            """;

        var (compilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        AssertNoErrors(diagnostics);

        var responseType = compilation.GetTypeByMetadataName("Sample.Response");
        Assert.NotNull(responseType);

        var okType = responseType!.GetTypeMembers("OK").Single();
        var valueProperty = okType.GetMembers("Value").OfType<IPropertySymbol>().SingleOrDefault();
        Assert.NotNull(valueProperty);

        var implicitOperator = responseType
            .GetMembers("op_Implicit")
            .OfType<IMethodSymbol>()
            .SingleOrDefault(m =>
                m.Parameters is [{ Type.SpecialType: SpecialType.System_String }]
            );
        Assert.NotNull(implicitOperator);
    }

    [Fact]
    public void HandAuthoredConstructorIsNotOverwrittenAndStillGetsOperator()
    {
        const string source = """
            namespace Sample;

            [TaggedUnions.TaggedUnion]
            public abstract partial class Response
            {
                public int Status { get; }

                private Response(int status) => Status = status;

                public sealed class Failed : Response
                {
                    public string ErrorMessage { get; }

                    public Failed(int status, string errorMessage) : base(status)
                    {
                        if (status == 0) throw new System.ArgumentException("status cannot be 0");
                        ErrorMessage = errorMessage ?? throw new System.ArgumentNullException(nameof(errorMessage));
                    }
                }
            }
            """;

        var (compilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        AssertNoErrors(diagnostics);

        var responseType = compilation.GetTypeByMetadataName("Sample.Response");
        Assert.NotNull(responseType);

        var implicitOperator = responseType!
            .GetMembers("op_Implicit")
            .OfType<IMethodSymbol>()
            .SingleOrDefault(m => m.Parameters is [{ Type.IsTupleType: true }]);
        Assert.NotNull(implicitOperator);
    }

    [Fact]
    public void AmbiguousConstructorReportsDiagnostic()
    {
        const string source = """
            namespace Sample;

            [TaggedUnions.TaggedUnion]
            public abstract partial class Response
            {
                private Response() { }

                public sealed class Weird : Response
                {
                    public Weird() { }
                    public Weird(int x) { }
                }
            }
            """;

        var (_, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "TU0004");
    }

    [Fact]
    public void CollidingOperatorSignaturesReportDiagnostic()
    {
        const string source = """
            namespace Sample;

            [TaggedUnions.TaggedUnion]
            public abstract partial class Response
            {
                private Response() { }

                public sealed partial class A(string Value) : Response;
                public sealed partial class B(string Value) : Response;
            }
            """;

        var (_, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "TU0005");
    }

    [Fact]
    public void CaseWrappingInterfaceSkipsOperatorAndWarns()
    {
        const string source = """
            using System.Collections.Generic;

            namespace Sample;

            public interface ICondition { }

            [TaggedUnions.TaggedUnion]
            public abstract partial class Outcome
            {
                private Outcome() { }

                public sealed partial class Waiting(IEnumerable<ICondition> Conditions) : Outcome;
            }
            """;

        var (compilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        AssertNoErrors(diagnostics);
        Assert.Contains(
            diagnostics,
            d => d.Id == "TU0008" && d.Severity == DiagnosticSeverity.Warning
        );

        var outcomeType = compilation.GetTypeByMetadataName("Sample.Outcome");
        Assert.NotNull(outcomeType);
        Assert.Empty(outcomeType!.GetMembers("op_Implicit"));

        // The property/null-check shorthand generation still happens even though the operator was skipped.
        var conditionsProperty = outcomeType
            .GetTypeMembers("Waiting")
            .Single()
            .GetMembers("Conditions")
            .OfType<IPropertySymbol>()
            .SingleOrDefault();
        Assert.NotNull(conditionsProperty);
    }

    [Fact]
    public void NoImplicitOperatorAttributeSuppressesWarningAndOperator()
    {
        const string source = """
            using System.Collections.Generic;

            namespace Sample;

            public interface ICondition { }

            [TaggedUnions.TaggedUnion]
            public abstract partial class Outcome
            {
                private Outcome() { }

                [TaggedUnions.NoImplicitOperator]
                public sealed partial class Waiting(IEnumerable<ICondition> Conditions) : Outcome;
            }
            """;

        var (compilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        AssertNoErrors(diagnostics);
        Assert.DoesNotContain(diagnostics, d => d.Id == "TU0008");

        var outcomeType = compilation.GetTypeByMetadataName("Sample.Outcome");
        Assert.NotNull(outcomeType);
        Assert.Empty(outcomeType!.GetMembers("op_Implicit"));
    }

    [Fact]
    public void ParameterlessCaseCompilesWithoutOperatorAndStillMatches()
    {
        const string source = """
            namespace Sample;

            [TaggedUnions.TaggedUnion]
            internal abstract partial class JobPhaseOutcome
            {
                private JobPhaseOutcome() { }

                [TaggedUnions.NoImplicitOperator]
                public sealed partial class Ready(object Value) : JobPhaseOutcome;

                public sealed class NotImplemented : JobPhaseOutcome
                {
                    public NotImplemented() { }
                }
            }
            """;

        var (compilation, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        AssertNoErrors(diagnostics);

        var outcomeType = compilation.GetTypeByMetadataName("Sample.JobPhaseOutcome");
        Assert.NotNull(outcomeType);

        // NotImplemented has no constructor parameters, so there is nothing to convert from -
        // it must not produce a bogus `implicit operator JobPhaseOutcome(())`.
        Assert.Empty(outcomeType!.GetMembers("op_Implicit"));
    }

    [Fact]
    public void NullableGenericArgumentDoesNotProduceNullabilityMismatchWarning()
    {
        const string source = """
            namespace Sample;

            public sealed class AnyBoxed<T>
            {
                public AnyBoxed(T value) { }
            }

            public sealed class PendingJobWork { }

            [TaggedUnions.TaggedUnion]
            public abstract partial class JobDriverPhase
            {
                private JobDriverPhase() { }

                public sealed partial class Ready(AnyBoxed<PendingJobWork?> Value) : JobDriverPhase;
            }
            """;

        var (_, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        AssertNoErrors(diagnostics);
        Assert.DoesNotContain(diagnostics, d => d.Id is "CS8619" or "CS8620");
    }

    [Fact]
    public void NonAbstractUnionReportsDiagnostic()
    {
        const string source = """
            namespace Sample;

            [TaggedUnions.TaggedUnion]
            public partial class Response
            {
                public sealed partial class A(string Value) : Response;
            }
            """;

        var (_, diagnostics) = GeneratorTestHelper.RunGenerator(source);

        Assert.Contains(diagnostics, d => d.Id == "TU0001");
    }

    private static void AssertNoErrors(
        System.Collections.Immutable.ImmutableArray<Diagnostic> diagnostics
    )
    {
        var errors = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.True(errors.Count == 0, string.Join("\n", errors.Select(e => e.ToString())));
    }
}
