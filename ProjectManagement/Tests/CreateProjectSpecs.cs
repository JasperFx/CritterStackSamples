using Bobcat;
using Bobcat.Xunit;
using ProjectManagement.Api;

namespace Tests;

/// <summary>
/// The original create_new_project test, rewritten as a Bobcat specification. The original only checked
/// the 201 and printed the body, with a comment to "check the data that was appended"; this asserts it.
/// </summary>
[BobcatFeature("Create a new project")]
public class CreateProjectSpecs(ProjectManagementHost app) : ProjectManagementSpec(app)
{
    [BobcatSpec(typeof(CreateProject))]
    public async Task create_a_new_project()
    {
        await WhenPosted(new CreateProject("Clean the house", "jeremy@jasperfx.net", ["tom@jasperfx.net", "bill@jasperfx.net"]),
            "/api/project/create");

        ThenTheResponseStatusIs(201);
        ThenEvents(
            new ProjectCreated("Clean the house"),
            new AdminAssigned("jeremy@jasperfx.net"),
            new TeamMemberAssigned("tom@jasperfx.net"),
            new TeamMemberAssigned("bill@jasperfx.net"));
        ThenTheResponseIsTheNewProjectsId(await TheResponse<Guid>());
    }

    [Then("the response is the id of the new Project stream")]
    public void ThenTheResponseIsTheNewProjectsId(Guid returned)
        => SpecAssert.Check("project id", returned, TheStartedStream<Project>());
}
