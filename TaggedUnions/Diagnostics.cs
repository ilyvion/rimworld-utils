using Microsoft.CodeAnalysis;

namespace TaggedUnions;

internal static class Diagnostics
{
    public static readonly DiagnosticDescriptor UnionMustBeAbstract = new(
        id: "TU0001",
        title: "Tagged union base must be abstract",
        messageFormat: "Type '{0}' is marked [TaggedUnion] but is not abstract",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnionMustBePartial = new(
        id: "TU0002",
        title: "Tagged union base must be partial",
        messageFormat: "Type '{0}' is marked [TaggedUnion] but is not declared partial",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CaseMustBeSealed = new(
        id: "TU0003",
        title: "Tagged union case must be sealed",
        messageFormat: "Case '{0}' of tagged union '{1}' must be declared sealed",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor CaseAmbiguousConstructor = new(
        id: "TU0004",
        title: "Tagged union case has an ambiguous constructor",
        messageFormat: "Case '{0}' of tagged union '{1}' must declare exactly one public constructor (found {2})",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor OperatorSignatureCollision = new(
        id: "TU0005",
        title: "Tagged union cases produce colliding implicit operators",
        messageFormat: "Cases '{0}' and '{1}' of tagged union '{2}' would both generate an implicit operator from '{3}'",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor UnionMustBeClass = new(
        id: "TU0006",
        title: "Tagged union base must be a class",
        messageFormat: "Type '{0}' is marked [TaggedUnion] but is not a class",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor NoCasesFound = new(
        id: "TU0007",
        title: "Tagged union has no cases",
        messageFormat: "Type '{0}' is marked [TaggedUnion] but has no nested classes deriving directly from it",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public static readonly DiagnosticDescriptor ImplicitOperatorFromInterface = new(
        id: "TU0008",
        title: "Cannot generate an implicit operator from an interface type",
        messageFormat: "Case '{0}' of tagged union '{1}' wraps interface type '{2}'; C# does not allow user-defined conversions to or from an interface, so no implicit operator was generated for this case. Add [TaggedUnions.NoImplicitOperator] to suppress this warning.",
        category: "TaggedUnions",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );
}
