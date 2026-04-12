using Alba;
using CqrsMinimalApi;
using Marten;
using Shouldly;

namespace CqrsMinimalApi.Tests;

public class StudentEndpointTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        await _host.CleanAllMartenDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _host.DisposeAsync();
    }

    [Fact]
    public async Task Can_create_a_student()
    {
        var result = await _host.Scenario(s =>
        {
            s.Post.Json(new CreateStudentRequest("Alice Test", "100 Test Blvd", "alice@test.com", new DateTime(2000, 1, 1))).ToUrl("/student/create");
            s.StatusCodeShouldBeOk();
        });

        var student = result.ReadAsJson<Student>();
        student.ShouldNotBeNull();
        student!.Name.ShouldBe("Alice Test");
        (student.Id > 0).ShouldBeTrue();
    }

    [Fact]
    public async Task Can_get_all_students()
    {
        // Arrange: seed two students
        using var session = _host.DocumentStore().LightweightSession();
        session.Store(new Student { Name = "Zara First" }, new Student { Name = "Amy Second" });
        await session.SaveChangesAsync();

        // Act
        var result = await _host.Scenario(s =>
        {
            s.Get.Url("/student/get-all");
            s.StatusCodeShouldBeOk();
        });

        var students = result.ReadAsJson<Student[]>();
        students.ShouldNotBeNull();
        (students!.Length >= 2).ShouldBeTrue();
        // Should be ordered by name
        students[0].Name.ShouldBe("Amy Second");
    }

    [Fact]
    public async Task Get_by_id_returns_404_for_missing()
    {
        await _host.Scenario(s =>
        {
            s.Get.Url("/student/get-by-id?id=999999");
            s.StatusCodeShouldBe(404);
        });
    }

    [Fact]
    public async Task Can_update_a_student()
    {
        // Arrange: create via the API so HiLo ID is assigned correctly
        var createResult = await _host.Scenario(s =>
        {
            s.Post.Json(new CreateStudentRequest("Original Name", "123 St", "orig@test.com", null)).ToUrl("/student/create");
            s.StatusCodeShouldBeOk();
        });
        var student = createResult.ReadAsJson<Student>();

        // Act
        var result = await _host.Scenario(s =>
        {
            s.Put.Json(new UpdateStudentRequest("Updated Name", null, "updated@test.com", null)).ToUrl($"/student/update/{student!.Id}");
            s.StatusCodeShouldBeOk();
        });

        var updated = result.ReadAsJson<Student>();
        updated!.Name.ShouldBe("Updated Name");
        updated.Email.ShouldBe("updated@test.com");
    }

    [Fact]
    public async Task Can_delete_a_student()
    {
        // Arrange: create via the API
        var createResult = await _host.Scenario(s =>
        {
            s.Post.Json(new CreateStudentRequest("To Delete", null, "del@test.com", null)).ToUrl("/student/create");
            s.StatusCodeShouldBeOk();
        });
        var student = createResult.ReadAsJson<Student>();

        // Act
        await _host.Scenario(s =>
        {
            s.Delete.Url($"/student/delete?id={student!.Id}");
            s.StatusCodeShouldBe(204);
        });

        // Verify deleted
        await _host.Scenario(s =>
        {
            s.Get.Url($"/student/get-by-id?id={student.Id}");
            s.StatusCodeShouldBe(404);
        });
    }
}
