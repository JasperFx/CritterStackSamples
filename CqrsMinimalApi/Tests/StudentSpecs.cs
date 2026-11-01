using Bobcat;
using Bobcat.Xunit;

namespace CqrsMinimalApi.Tests;

/// <summary>
/// Four of the original tests, rewritten as Bobcat specifications. Each one arranges the stored
/// Student documents directly instead of through earlier requests, sends ONE request, and asserts
/// the response and what is now in the store. The 404 check is still in
/// <see cref="StudentEndpointTests" />, unchanged apart from the move to xUnit v3.
/// </summary>
[BobcatFeature("Student endpoints")]
public class StudentSpecs(StudentHost app) : StudentSpec(app)
{
    [BobcatSpec(typeof(CreateStudentRequest))]
    public async Task create_a_student()
    {
        await WhenPosted(new CreateStudentRequest("Alice Test", "100 Test Blvd", "alice@test.com", new DateTime(2000, 1, 1)),
            "/student/create");

        ThenTheResponseStatusIs(200);
        var created = await TheResponse<Student>();
        TheStudentHasAnId(created);
        await ThenDocument<Student>(created.Id,
            Specify<Student>().With(x => x.Name, "Alice Test").With(x => x.Email, "alice@test.com").With(x => x.Active, true));
    }

    [BobcatSpec]
    public async Task students_are_listed_by_name()
    {
        await GivenReadModel(new Student { Name = "Zara First" });
        await GivenReadModel(new Student { Name = "Amy Second" });

        await WhenReading("/student/get-all");

        ThenTheResponseStatusIs(200);
        TheStudentsAre(await TheResponse<Student[]>(), "Amy Second", "Zara First");
    }

    [BobcatSpec(typeof(UpdateStudentRequest))]
    public async Task update_a_student()
    {
        // Marten assigns the HiLo id when the document is arranged
        var student = new Student { Name = "Original Name", Address = "123 St", Email = "orig@test.com" };
        await GivenReadModel(student);

        await WhenPut(new UpdateStudentRequest("Updated Name", null, "updated@test.com", null), $"/student/update/{student.Id}");

        ThenTheResponseStatusIs(200);
        await ThenDocument<Student>(student.Id,
            Specify<Student>().With(x => x.Name, "Updated Name").With(x => x.Email, "updated@test.com"));
    }

    [BobcatSpec]
    public async Task delete_a_student()
    {
        var student = new Student { Name = "To Delete", Email = "del@test.com" };
        await GivenReadModel(student);

        await WhenDeleted($"/student/delete?id={student.Id}");

        ThenTheResponseStatusIs(204);
        await ThenNoReadModel<Student>(student.Id);
    }

    [Then("the student was given an id")]
    public void TheStudentHasAnId(Student student)
        => SpecAssert.Fact(student.Id > 0, "the HiLo id is assigned");

    [Then("the students are {names}")]
    public void TheStudentsAre(Student[] students, params string[] names)
        => SpecAssert.Check("names", string.Join(", ", students.Select(x => x.Name)), string.Join(", ", names));
}
