using Bobcat;
using Bobcat.Xunit;

namespace ContributorApi.Tests;

/// <summary>
/// Five of the original tests, rewritten as Bobcat specifications. Each one arranges the stored
/// Contributor documents directly instead of through earlier requests, sends ONE request, and asserts
/// the response and what is now in the store. The rest of the original tests are still in
/// <see cref="ContributorTests" />, unchanged apart from the move to xUnit v3.
/// </summary>
[BobcatFeature("Contributors")]
public class ContributorSpecs(ContributorHost app) : ContributorSpec(app)
{
    [BobcatSpec(typeof(CreateContributor))]
    public async Task create_a_contributor_with_a_phone_number()
    {
        await WhenPosted(new CreateContributor("Alice Smith", "1", "5551234567", null), "/api/contributors");

        ThenTheResponseStatusIs(200);
        var created = await TheResponse<Contributor>();
        var stored = await ThenDocument<Contributor>(created.Id, Specify<Contributor>().With(x => x.Name, "Alice Smith"));
        ThePhoneNumberIs(stored, "+1 5551234567");
    }

    [BobcatSpec(typeof(CreateContributor))]
    public async Task a_duplicate_name_is_refused()
    {
        await GivenReadModel(new Contributor { Name = "Duplicate" });

        await WhenPosted(new CreateContributor("Duplicate", null, null, null), "/api/contributors");

        await ThenTheRequestIsRefusedWith(409, "Contributor 'Duplicate' already exists");
        await ThenSingleDocument<Contributor>(Specify<Contributor>().With(x => x.Name, "Duplicate"));
    }

    [BobcatSpec(typeof(UpdateContributor))]
    public async Task rename_a_contributor()
    {
        // Marten assigns the HiLo id when the document is arranged
        var original = new Contributor { Name = "Original Name" };
        await GivenReadModel(original);

        await WhenPut(new UpdateContributor(original.Id, "Updated Name"), $"/api/contributors/{original.Id}");

        ThenTheResponseStatusIs(200);
        var stored = await ThenDocument<Contributor>(original.Id, Specify<Contributor>().With(x => x.Name, "Updated Name"));
        TheUpdateIsStamped(stored);
    }

    [BobcatSpec]
    public async Task delete_a_contributor()
    {
        var contributor = new Contributor { Name = "To Delete" };
        await GivenReadModel(contributor);

        await WhenDeleted($"/api/contributors/{contributor.Id}");

        ThenTheResponseStatusIs(204);
        await ThenNoReadModel<Contributor>(contributor.Id);
    }

    [BobcatSpec]
    public async Task contributors_are_listed_by_name()
    {
        await GivenReadModel(new Contributor { Name = "Zara First" });
        await GivenReadModel(new Contributor { Name = "Amy Second" });

        await WhenReading("/api/contributors");

        ThenTheResponseStatusIs(200);
        TheContributorsAre(await TheResponse<Contributor[]>(), "Amy Second", "Zara First");
    }

    [Then("the phone number is {phone}")]
    public void ThePhoneNumberIs(Contributor contributor, string phone)
        => SpecAssert.Check("phone", contributor.PhoneNumber?.ToString(), phone);

    [Then("the update is time-stamped")]
    public void TheUpdateIsStamped(Contributor contributor)
        => SpecAssert.Fact(contributor.UpdatedAt is not null, "UpdatedAt is set");

    [Then("the contributors are {names}")]
    public void TheContributorsAre(Contributor[] contributors, params string[] names)
        => SpecAssert.Check("names", string.Join(", ", contributors.Select(x => x.Name)), string.Join(", ", names));
}
