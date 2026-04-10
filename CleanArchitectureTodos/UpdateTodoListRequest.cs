using FluentValidation;
using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace CleanArchitectureTodos;

public record UpdateTodoListRequest(string Title, string? Colour)
{
    public class Validator : AbstractValidator<UpdateTodoListRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        }
    }
}

public static class UpdateTodoListEndpoint
{
    // IResult justified: uniqueness check creates variable return logic
    [WolverinePut("/api/todolists/{id}")]
    public static async Task<IResult> Put(
        int id,
        UpdateTodoListRequest request,
        [Entity(Required = true)] TodoList list,
        IDocumentSession session)
    {
        var duplicate = await session.Query<TodoList>().AnyAsync(l => l.Title == request.Title && l.Id != id);
        if (duplicate)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Title"] = [$"'{request.Title}' already exists."]
            });

        list.Title = request.Title;
        list.Colour = request.Colour ?? list.Colour;
        list.LastModified = DateTimeOffset.UtcNow;

        session.Store(list);

        return Results.NoContent();
    }
}
