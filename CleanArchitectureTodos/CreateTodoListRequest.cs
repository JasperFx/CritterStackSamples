using FluentValidation;
using Marten;
using Wolverine.Http;

namespace CleanArchitectureTodos;

public record CreateTodoListRequest(string Title, string? Colour)
{
    public class Validator : AbstractValidator<CreateTodoListRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        }
    }
}

public static class CreateTodoListEndpoint
{
    // IResult is justified here: the uniqueness check creates genuinely
    // variable return logic (Created vs ValidationProblem)
    [WolverinePost("/api/todolists")]
    public static async Task<IResult> Post(CreateTodoListRequest request, IDocumentSession session)
    {
        var exists = await session.Query<TodoList>().AnyAsync(l => l.Title == request.Title);
        if (exists)
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Title"] = [$"'{request.Title}' already exists."]
            });

        var list = new TodoList
        {
            Title = request.Title,
            Colour = request.Colour ?? "#808080",
            Created = DateTimeOffset.UtcNow,
        };

        session.Store(list);

        return Results.Created($"/api/todolists/{list.Id}", list.Id);
    }
}
