using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;
using Wolverine.Persistence;

namespace CqrsMinimalApi;

// --- Request/Response records ---

public record CreateStudentRequest(string Name, string? Address, string? Email, DateTime? DateOfBirth, bool Active = true);
public record UpdateStudentRequest(string Name, string? Address, string? Email, DateTime? DateOfBirth, bool Active = true);

// --- Wolverine HTTP Endpoints ---

public static class StudentEndpoints
{
    // Converted
    // [WolverinePost("/student/create")]
    // public static async Task<IResult> Create(CreateStudentRequest request, IDocumentSession session)
    // {
    //     var student = new Student
    //     {
    //         Name = request.Name,
    //         Address = request.Address,
    //         Email = request.Email,
    //         DateOfBirth = request.DateOfBirth,
    //         Active = request.Active
    //     };
    //
    //     session.Store(student);
    //     await session.SaveChangesAsync();
    //
    //     return Results.Ok(student);
    // }
    
    // Desired. Remove the usage of IResult, that's "mystery meat" and Wolverine can
    // better derive the OpenAPI metadata by a more expressive signature
    // Also, let the transactional middleware kick in so this is synchronous
    [WolverinePost("/student/create")]
    public static Student Create(CreateStudentRequest request, IDocumentSession session)
    {
        var student = new Student
        {
            Name = request.Name,
            Address = request.Address,
            Email = request.Email,
            DateOfBirth = request.DateOfBirth,
            Active = request.Active
        };

        session.Store(student);
        
        return student;
    }

    // Return the strong typed objects instead of IResult, Wolverine.HTTP
    // handles the 200 and 404 mechanics just fine
    [WolverineGet("/student/get-all")]
    public static async Task<IReadOnlyList<Student>> GetAll(IQuerySession session, CancellationToken ct)
    {
        var students = await session.Query<Student>()
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return students;
    }

    // Use [Entity] for this. Wolverine.HTTP can handle the 200 + JSON or 404 mechanics
    // automatically
    [WolverineGet("/student/get-by-id")]
    public static Student? GetById([FromQuery] int id, [Entity(Required = true)] Student? student)
    {
        return student;
    }

    // Just return the value, don't use IResult unless there is a conditional
    // return within the endpoint method
    [WolverineGet("/student/get-by-name")]
    public static async Task<Student?> GetByName([FromQuery] string name, IQuerySession session, CancellationToken ct)
    {
        return await session.Query<Student>()
            .FirstOrDefaultAsync(s => s.Name == name, ct);
    }

    // Try to be synchronous in all cases
    // Use [Entity(Required = true)] to declaratively deal with HTTP status code
    // 200 or 404 here
    [WolverinePut("/student/update/{id}")]
    public static Student Update(
        UpdateStudentRequest request,
        int id,
        [Entity(Required = true)] Student student,
        IDocumentSession session)
    {
        student.Name = request.Name;
        student.Address = request.Address;
        student.Email = request.Email;
        student.DateOfBirth = request.DateOfBirth;
        student.Active = request.Active;

        session.Store(student);

        return student;
    }

    // Use [Entity(Required = true)] to deal with 200 vs 404
    // Try to be synchronous
    // Rely on Wolverine transactional middleware whereever possible
    // you should almost never need to directly call IDocumentSession.SaveChangesAsync() in your
    // handlers or endpoint methods
    [WolverineDelete("/student/delete")]
    public static void Delete([FromQuery] int id, [Entity(Required = true)] Student student,
        IDocumentSession session)
    {
        session.Delete(student);
    }
}
