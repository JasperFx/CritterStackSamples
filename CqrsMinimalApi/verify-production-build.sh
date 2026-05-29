#!/usr/bin/env bash
# GH-2900: prove that a Release/production publish of CqrsMinimalApi ships WITHOUT Roslyn /
# WolverineFx.RuntimeCompilation. Run from anywhere:  ./verify-production-build.sh
set -euo pipefail

here="$(cd "$(dirname "$0")" && pwd)"
out="$here/bin/verify-publish"
rm -rf "$out"

# Pre-generate code. Debug has the runtime-compiler package; `codegen write` is metadata-only,
# so no database is needed for this step.
dotnet run -c Debug --project "$here/CqrsMinimalApi.csproj" -- codegen write

# Publish Release. WolverineFx.RuntimeCompilation is referenced only in Debug, so it (and Roslyn)
# must NOT appear in the published output.
dotnet publish "$here/CqrsMinimalApi.csproj" -c Release -o "$out"

echo
banned='Wolverine.RuntimeCompilation|Microsoft.CodeAnalysis|JasperFx.RuntimeCompiler'
if ls "$out" | grep -iE "$banned"; then
    echo "FAIL: runtime-compilation / Roslyn assemblies found in the Release publish (see above)."
    exit 1
fi

echo "PASS: Release publish is Roslyn-free — no Wolverine.RuntimeCompilation / Microsoft.CodeAnalysis* / JasperFx.RuntimeCompiler."
