#!/usr/bin/env python3
"""Mechanical RimTest Redux -> DevTools rewrite of C# test sources.

Usage: convert_rimtest_asserts.py <file-or-dir>... (dirs are searched for *.cs)

Rewrites in place (BOMs preserved):
- `using RimTestRedux;`                       -> `using DevTools.Testing;`
- `[TestSuite] [internal|public] static class` -> `[TestFixture(TestType.MainMenu)] ... sealed class`
- `[BeforeEach]` / `[AfterEach]`               -> `[SetUp]` / `[TearDown]`
- Assert.That / Assert.ThatCollection chains (multi-line safe, operands swapped where DevTools
  takes `expected` first) -> Expect.* (see devtools.md's translation table).

Anything it cannot translate (ThatFunc, Between*, ShouldThrow, Null, SameReferenceAs, ...) is
left untouched and listed on stderr with file:line for manual conversion; exit status is 1 then.
Run csharpier afterwards, since rewritten statements can exceed the line width.
"""
import re
import sys
from pathlib import Path


def balanced(s, i):
    """s[i] == '('; return the index just past the matching ')'."""
    depth, j, quote = 0, i, None
    while j < len(s):
        c = s[j]
        if quote:
            if c == "\\" and quote == '"':
                j += 1
            elif c == quote:
                quote = None
        elif c in "\"'":
            quote = c
        elif c == "(":
            depth += 1
        elif c == ")":
            depth -= 1
            if depth == 0:
                return j + 1
        j += 1
    raise ValueError("unbalanced parentheses")


START = re.compile(r"Assert\s*\.\s*(That|ThatCollection)\s*\(")
GRAMMAR = re.compile(r"\s*\.\s*(Is|Not|Has|Does)\b")
METHOD = re.compile(r"\s*\.\s*(\w+)\s*\(")


def build(kind, name, neg, subj, arg):
    if kind == "That":
        table = {
            ("True", False): f"Expect.IsTrue({subj})",
            ("False", False): f"Expect.IsFalse({subj})",
            ("True", True): f"Expect.IsFalse({subj})",
            ("False", True): f"Expect.IsTrue({subj})",
            ("EqualTo", False): f"Expect.AreEqual({arg}, {subj})",
            ("EqualTo", True): f"Expect.AreNotEqual({arg}, {subj})",
            ("GreaterThan", False): f"Expect.GreaterThan({subj}, {arg})",
            ("GreaterThan", True): f"Expect.LessThanOrEqualTo({subj}, {arg})",
            ("LessThan", False): f"Expect.LessThan({subj}, {arg})",
            ("LessThan", True): f"Expect.GreaterThanOrEqualTo({subj}, {arg})",
        }
    else:
        table = {
            # `.Count()` needs System.Linq; prefer `.Count` for lists (CA1829), fix per build warning.
            ("Count", False): f"Expect.AreEqual({arg}, {subj}.Count())",
            ("Empty", False): f"Expect.IsEmpty({subj})",
            ("Empty", True): f"Expect.IsNotEmpty({subj})",
            ("Contain", False): f"Expect.IsTrue({subj}.Contains({arg}))",
            ("Contain", True): f"Expect.IsFalse({subj}.Contains({arg}))",
        }
    return table.get((name, neg))


def convert(src, fname, problems):
    out, pos = [], 0
    while True:
        m = START.search(src, pos)
        if not m:
            out.append(src[pos:])
            return "".join(out)
        out.append(src[pos : m.start()])
        line_no = src.count("\n", 0, m.start()) + 1
        line_start = src.rfind("\n", 0, m.start()) + 1
        if "//" in src[line_start : m.start()]:  # mention inside a comment
            out.append(src[m.start() : m.end()])
            pos = m.end()
            continue
        subject_end = balanced(src, m.end() - 1)
        subject = src[m.end() : subject_end - 1].strip()
        k, neg = subject_end, False
        while g := GRAMMAR.match(src, k):
            neg ^= g.group(1) == "Not"
            k = g.end()
        mm = METHOD.match(src, k)
        replacement = None
        if mm:
            arg_end = balanced(src, mm.end() - 1)
            arg = src[mm.end() : arg_end - 1].strip()
            replacement = build(m.group(1), mm.group(1), neg, subject, arg)
        if replacement is None:
            problems.append(f"{fname}:{line_no}: unsupported {src[m.start():m.start() + 80]!r}")
            out.append(src[m.start() : m.end()])
            pos = m.end()
            continue
        out.append(replacement)
        pos = arg_end


def main():
    files = []
    for a in sys.argv[1:]:
        p = Path(a)
        files += sorted(p.rglob("*.cs")) if p.is_dir() else [p]
    problems = []
    for f in files:
        raw = f.read_bytes()
        bom = raw.startswith(b"\xef\xbb\xbf")
        s = raw.decode("utf-8-sig")
        t = convert(s, f, problems)
        t = t.replace("using RimTestRedux;", "using DevTools.Testing;")
        t = re.sub(
            r"\[TestSuite\]\s*\n(\s*)((?:public |internal )?)static class",
            lambda m: f"[TestFixture(TestType.MainMenu)]\n{m.group(1)}{m.group(2)}sealed class",
            t,
        )
        t = t.replace("[BeforeEach]", "[SetUp]").replace("[AfterEach]", "[TearDown]")
        for attr in ("ShouldThrow", "ThatFunc", "BetweenInclusive", "BetweenExclusive"):
            for hit in re.finditer(attr, t):
                problems.append(f"{f}:{t.count(chr(10), 0, hit.start()) + 1}: manual: {attr}")
        if t != s:
            f.write_bytes((b"\xef\xbb\xbf" if bom else b"") + t.encode("utf-8"))
    for p in problems:
        print(p, file=sys.stderr)
    sys.exit(1 if problems else 0)


main()
