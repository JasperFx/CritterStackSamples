using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
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
    // Sad-path validation pulled out of the main handler.
    // Wolverine calls this before Post() — if it returns a populated ProblemDetails,
    // the Post() method is never invoked. See: Railway Programming skill.
    public static async Task<ProblemDetails> ValidateAsync(
        CreateTodoListRequest request,
        IQuerySession session)
    {
        var exists = await session.Query<TodoList>().AnyAsync(l => l.Title == request.Title);
        if (exists)
            return new ProblemDetails
            {
                Detail = $"'{request.Title}' already exists.",
                Status = 400,
            };

        return WolverineContinue.NoProblems;
    }

    // The main handler is synchronous and focused purely on the happy path.
    // FluentValidation middleware handles structural validation (empty/length).
    // ValidateAsync above handles business rule validation (uniqueness).
    [WolverinePost("/api/todolists")]
    public static TodoList Post(CreateTodoListRequest request, IDocumentSession session)
    {
        var list = new TodoList
        {
            Title = request.Title,
            Colour = request.Colour ?? "#808080",
            Created = DateTimeOffset.UtcNow,
        };

        session.Store(list);

        return list;
    }
}
