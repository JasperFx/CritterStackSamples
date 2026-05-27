using Alba;
using Microsoft.AspNetCore.Hosting;

namespace CqrsMinimalApi.Tests;

// GH-2900: proves the production code-generation workflow end to end.
//
// Booting in the Production environment activates the CritterStackDefaults.Production profile
// configured in Program.cs:
//     GeneratedCodeMode = TypeLoadMode.Static  +  AssertAllPreGeneratedTypesExist = true
//
// In that mode Wolverine loads the *pre-generated* code (committed under Internal/Generated,
// produced by `dotnet run -- codegen write`) and performs NO runtime Roslyn compilation. The
// AssertAllPreGeneratedTypesExist guard means startup throws if any expected generated type is
// missing — so simply reaching a successful boot + request proves the pre-generated path works.
//
// The complementary claim — that a Release publish ships WITHOUT WolverineFx.RuntimeCompilation /
// Roslyn at all — is proven by verify-production-build.sh and the Dockerfile (a Release build,
// which this Debug test cannot demonstrate since the package is present in Debug).
public class ProductionStaticCodegenTests
{
    [Fact]
    public async Task boots_in_production_with_static_pregenerated_code_and_serves_requests()
    {
        await using var host = await AlbaHost.For<Program>(builder =>
        {
            builder.UseEnvironment("Production");
        });

        // A generated endpoint actually executes — confirming the pre-generated handler runs.
        await host.Scenario(s =>
        {
            s.Get.Url("/student/get-all");
            s.StatusCodeShouldBeOk();
        });
    }
}
