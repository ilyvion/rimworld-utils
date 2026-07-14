# TaggedUnions — usage guide

A Roslyn incremental source generator that turns a small, declarative class hierarchy into a Rust-style tagged union: implicit conversion operators from each case's "wrapped" value(s), plus `Match`/`Switch` helpers that give call sites compiler-enforced exhaustiveness.

## The net481 / netstandard2.0 mismatch — do you need to do anything?

No. This is already handled; the section below is just so you understand *why* it works, since it can look surprising at first.

RimWorld mods in this family target `net481` (set in `Common.props`). A Roslyn *source generator*, however, doesn't run as part of your mod's runtime — it runs inside the C# compiler process itself (`csc`/VBCSCompiler, or the IDE's live analysis host), which is why generator projects must target `netstandard2.0`: that's the TFM the compiler host can always load, regardless of what TFM the project being compiled targets. `TaggedUnions.csproj` targets `netstandard2.0` for exactly this reason.

Because of that split, a generator is referenced differently from a normal library:

```xml
<ProjectReference Include="...\TaggedUnions\TaggedUnions.csproj"
                   OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
```

- `OutputItemType="Analyzer"` tells MSBuild to load `TaggedUnions.dll` into the compiler as an analyzer/generator, not as a normal reference.
- `ReferenceOutputAssembly="false"` means your mod's assembly does **not** get a runtime dependency on `TaggedUnions.dll` — nothing from `netstandard2.0` ends up shipping inside your `net481` mod. At runtime, all that exists is the plain C# the generator emitted (properties, operators, `Match`/`Switch` methods) as ordinary source, compiled straight into your assembly like any other code.

`Common.props` already sets this up for you (see below) — you never write that `ProjectReference` by hand.

## Opting in

Set `UseTaggedUnions` to `true` **before** importing `Common.props` in your mod's `Directory.Build.props` (same pattern as the existing `UseLaboratory` toggle):

```xml
<Project>
    <PropertyGroup>
        <UseTaggedUnions>true</UseTaggedUnions>
    </PropertyGroup>
    <Import Project="..\rimworld-utils\Common.props" />
</Project>
```

No package reference, no attribute-only assembly to add — the generator injects its own `[TaggedUnions.TaggedUnion]` marker attribute into your compilation automatically. Nothing else to install.

C# 12 primary constructors (used by the shorthand form below) already work under `net481` here because `Common.props` sets `LangVersion=latest` and pulls in `PolySharp`, regardless of the runtime TFM.

## Authoring a union

Mark an **abstract partial class** with `[TaggedUnion]`. Any class nested directly inside it that derives from it is treated as a case — no per-case attribute needed:

```csharp
[TaggedUnions.TaggedUnion]
public abstract partial class DecryptResponse
{
    public DecryptStatus Status { get; }

    private DecryptResponse(DecryptStatus status)
    {
        Status = status;
    }

    // Shorthand case — see below.
    public sealed partial class OK(Stream Stream) : DecryptResponse(DecryptStatus.OK);

    // Hand-authored case — see below.
    public sealed class Failed : DecryptResponse
    {
        public string ErrorMessage { get; }

        public Failed(DecryptStatus status, string errorMessage) : base(status)
        {
            if (status == DecryptStatus.OK)
                throw new ArgumentException(message: "Value cannot be " + nameof(DecryptStatus.OK) + ".");
            ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        }
    }
}
```

The generator adds a second partial part to `DecryptResponse` with:

- `public static implicit operator DecryptResponse(Stream stream) => new OK(stream);`
- `public static implicit operator DecryptResponse((DecryptStatus status, string errorMessage) pair) => new Failed(pair.status, pair.errorMessage);`
- `Match<TResult>(Func<OK,TResult> ok, Func<Failed,TResult> failed)` and `Switch(Action<OK> ok, Action<Failed> failed)`

so you can write:

```csharp
DecryptResponse response = someStream;               // implicit operator
DecryptResponse response = (errorStatus, "message");  // implicit operator, tuple form

var message = response.Match(
    ok: o => "stream: " + o.Stream,
    failed: f => "error: " + f.ErrorMessage);
```

### Two ways to write a case

1. **Shorthand (primary constructor)** — `public sealed partial class OK(Stream Stream) : DecryptResponse(DecryptStatus.OK);`
   The class must be `partial` and `sealed`. The generator adds a public `{ get; }` property for each primary-constructor parameter (using the parameter's name verbatim — name your parameters `PascalCase` so they read naturally as properties), with an automatic `?? throw new ArgumentNullException(...)` for any non-nullable reference-type parameter. Use this when a case is just "a status tag plus some data," with no extra validation.

2. **Hand-authored** — write the class and its constructor yourself, exactly as you would without the generator (see `Failed` above). Use this whenever a case needs custom validation or logic beyond a null check (the "`status` can't be `OK`" rule, in the example). The generator leaves the constructor and any properties you wrote untouched — it only reads the constructor's parameter list to build that case's implicit operator.

Both forms can be mixed freely within the same union, exactly as in the example above.

### Implicit operator inference

- A one-parameter case constructor → an operator converting directly from that parameter's type.
- A multi-parameter case constructor → an operator converting from a matching named `ValueTuple` (element names taken from the constructor's parameter names), so `return (errorStatus, "message");` works without spelling out the tuple type.

If two cases would produce colliding operators (same wrapped type, or same tuple shape), the generator reports `TU0005` instead of silently picking one.

### Disabling the implicit operator for a case

C# does not allow a user-defined conversion to or from an **interface** type (`CS0552`) — so if a case's single constructor parameter is an interface (e.g. `Waiting(IEnumerable<IResumeCondition> Conditions)`), an implicit operator simply can't be generated for it. The generator detects this itself and skips just that case's operator, emitting a `TU0008` warning instead of failing your build — everything else (the case's properties, `Match`, `Switch`, and every other case's operator) is still generated normally.

If you'd rather not see the warning — or you want to opt a case out of implicit-operator generation for some other reason, e.g. you don't want implicit conversion "magic" for that particular case — put `[TaggedUnions.NoImplicitOperator]` on it:

```csharp
[TaggedUnions.NoImplicitOperator]
public sealed partial class Waiting(IEnumerable<IResumeCondition> Conditions) : JobPhaseOutcome;
```

With the attribute present, the case is built with `new Waiting(conditions)` at call sites instead of an implicit conversion; no operator is generated and no `TU0008` warning is reported.

## Diagnostics

| ID | Meaning |
|---|---|
| `TU0001` | `[TaggedUnion]` type isn't `abstract` |
| `TU0002` | `[TaggedUnion]` type isn't `partial` |
| `TU0003` | A case isn't `sealed` |
| `TU0004` | A case has zero or more than one public constructor (operator inference needs exactly one) |
| `TU0005` | Two cases would generate colliding implicit operators |
| `TU0006` | `[TaggedUnion]` was put on something other than a class |
| `TU0007` | (warning) A `[TaggedUnion]` type has no nested cases deriving from it |
| `TU0008` | (warning) A case's implicit operator was skipped because its wrapped type is an interface; add `[TaggedUnions.NoImplicitOperator]` to suppress |

## Known limitations

- A case class must have exactly one public constructor.
- Cases must be nested directly inside the union base (no discovery across files/namespaces).
- Generic union bases are untested — stick to non-generic unions for now.
