using Alba;
using CqrsMinimalApi;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsMinimalApi.Tests;

public class StudentEndpointTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>(builder =>
        {
            // Use a test-specific database to avoid colliding with dev data
            builder.ConfigureServices(services =>
            {
                services.Configure<StoreOptions>(opts =>
                {
                    opts.Connection("Host=localhost;Database=cqrs_minimal_api_tests;Username=postgres;Password=postgres");
                });
            });
        });

        // Clean slate for each test run
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
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
        Assert.NotNull(student);
        Assert.Equal("Alice Test", student!.Name);
        Assert.True(student.Id > 0);
    }

    [Fact]
    public async Task Can_get_all_students()
    {
        // Arrange: seed two students
        await using var session = _host.Services.GetRequiredService<IDocumentSession>();
        session.Store(new Student { Name = "Zara First" }, new Student { Name = "Amy Second" });
        await session.SaveChangesAsync();

        // Act
        var result = await _host.Scenario(s =>
        {
            s.Get.Url("/student/get-all");
            s.StatusCodeShouldBeOk();
        });

        var students = result.ReadAsJson<Student[]>();
        Assert.NotNull(students);
        Assert.True(students!.Length >= 2);
        // Should be ordered by name
        Assert.Equal("Amy Second", students[0].Name);
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
        // Arrange
        await using var session = _host.Services.GetRequiredService<IDocumentSession>();
        var student = new Student { Name = "Original Name", Email = "orig@test.com" };
        session.Store(student);
        await session.SaveChangesAsync();

        // Act
        var result = await _host.Scenario(s =>
        {
            s.Put.Json(new UpdateStudentRequest("Updated Name", null, "updated@test.com", null)).ToUrl($"/student/update?id={student.Id}");
            s.StatusCodeShouldBeOk();
        });

        var updated = result.ReadAsJson<Student>();
        Assert.Equal("Updated Name", updated!.Name);
        Assert.Equal("updated@test.com", updated.Email);
    }

    [Fact]
    public async Task Can_delete_a_student()
    {
        // Arrange
        await using var session = _host.Services.GetRequiredService<IDocumentSession>();
        var student = new Student { Name = "To Delete" };
        session.Store(student);
        await session.SaveChangesAsync();

        // Act
        await _host.Scenario(s =>
        {
            s.Delete.Url($"/student/delete?id={student.Id}");
            s.StatusCodeShouldBeOk();
        });

        // Verify deleted
        await _host.Scenario(s =>
        {
            s.Get.Url($"/student/get-by-id?id={student.Id}");
            s.StatusCodeShouldBe(404);
        });
    }
}
