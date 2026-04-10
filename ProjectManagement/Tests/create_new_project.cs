using System.Threading.Tasks;
using Marten;
using ProjectManagement.Api;
using Xunit;
using Xunit.Abstractions;

namespace Tests;

public class create_new_project : IntegrationContext
{
    private readonly ITestOutputHelper _output;

    public create_new_project(AppFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    [Fact]
    public async Task create_a_new_project()
    {
        var command = new CreateProject("Clean the house", "jeremy@jasperfx.net",
            ["tom@jasperfx.net", "bill@jasperfx.net"]);

        var result = await Host.Scenario(x =>
        {
            x.Post.Json(command).ToUrl("/api/project/create");
            x.StatusCodeShouldBe(201);
        });
        
        // I don't remember what the JSON body ends up looking like, so this first:
        _output.WriteLine(result.ReadAsText());

        //using var session = Host.DocumentStore().LightweightSession();
        // check the data that was appended
    }
}