using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace CleanArchitectureTodos;

// --- Requests with inner validators ---

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

// --- Response DTOs ---
public record TodoListDto(int Id, string Title, string Colour, IReadOnlyList<TodoItemDto> Items);
public record TodoItemDto(Guid Id, string Title, string? Note, int Priority, bool Done);
public record TodosVm(IReadOnlyList<LookupDto> PriorityLevels, IReadOnlyList<ColourDto> Colours, IReadOnlyList<TodoListDto> Lists);
public record LookupDto(int Id, string Title);
public record ColourDto(string Code, string Name);

public static class TodoListEndpoints
{
    [WolverineGet("/api/todolists")]
    public static async Task<TodosVm> GetAll(IQuerySession session)
    {
        var lists = await session.Query<TodoList>()
            .OrderBy(l => l.Title)
            .ToListAsync();

        var priorityLevels = Enum.GetValues<PriorityLevel>()
            .Select(p => new LookupDto((int)p, p.ToString()))
            .ToList();

        var colours = TodoColours.Supported
            .Select(c => new ColourDto(c.Key, c.Value))
            .ToList();

        var listDtos = lists.Select(l => new TodoListDto(
            l.Id,
            l.Title,
            l.Colour,
            l.Items.Select(i => new TodoItemDto(i.Id, i.Title, i.Note, (int)i.Priority, i.Done)).ToList()
        )).ToList();

        return new TodosVm(priorityLevels, colours, listDtos);
    }

    // Wolverine's FluentValidation middleware runs the inner Validator automatically
    // and returns ProblemDetails if validation fails — no pipeline behavior needed
    [WolverinePost("/api/todolists")]
    public static async Task<IResult> Create(CreateTodoListRequest request, IDocumentSession session)
    {
        // Uniqueness check (async rules not handled by middleware)
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
        await session.SaveChangesAsync();

        return Results.Created($"/api/todolists/{list.Id}", list.Id);
    }

    [WolverinePut("/api/todolists/{id}")]
    public static async Task<IResult> Update(int id, UpdateTodoListRequest request, IDocumentSession session)
    {
        var list = await session.LoadAsync<TodoList>(id);
        if (list is null) return Results.NotFound();

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
        await session.SaveChangesAsync();

        return Results.NoContent();
    }

    [WolverineDelete("/api/todolists/{id}")]
    public static async Task<IResult> Delete(int id, IDocumentSession session)
    {
        var list = await session.LoadAsync<TodoList>(id);
        if (list is null) return Results.NotFound();

        session.Delete(list);
        await session.SaveChangesAsync();

        return Results.NoContent();
    }
}
