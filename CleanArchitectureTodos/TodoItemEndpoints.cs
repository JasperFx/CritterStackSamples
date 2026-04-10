using FluentValidation;
using Marten;
using Microsoft.AspNetCore.Mvc;
using Wolverine.Http;

namespace CleanArchitectureTodos;

// --- Requests with inner validators ---

public record CreateTodoItemRequest(int ListId, string Title)
{
    public class Validator : AbstractValidator<CreateTodoItemRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
            RuleFor(x => x.ListId).GreaterThan(0);
        }
    }
}

public record UpdateTodoItemRequest(string Title, bool Done)
{
    public class Validator : AbstractValidator<UpdateTodoItemRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        }
    }
}

public record UpdateTodoItemDetailRequest(int ListId, PriorityLevel Priority, string? Note);

public static class TodoItemEndpoints
{
    [WolverinePost("/api/todoitems")]
    public static async Task<IResult> Create(CreateTodoItemRequest request, IDocumentSession session)
    {
        var list = await session.LoadAsync<TodoList>(request.ListId);
        if (list is null)
            return Results.NotFound();

        var item = new TodoItem { Title = request.Title };
        list.Items.Add(item);
        list.LastModified = DateTimeOffset.UtcNow;

        session.Store(list);
        await session.SaveChangesAsync();

        return Results.Created($"/api/todoitems/{item.Id}", item.Id);
    }

    [WolverinePut("/api/todoitems/{id}")]
    public static async Task<IResult> Update(Guid id, UpdateTodoItemRequest request, IDocumentSession session)
    {
        var lists = await session.Query<TodoList>()
            .Where(l => l.Items.Any(i => i.Id == id))
            .ToListAsync();

        var list = lists.FirstOrDefault();
        if (list is null) return Results.NotFound();

        var item = list.Items.First(i => i.Id == id);
        var wasDone = item.Done;
        item.Title = request.Title;
        item.Done = request.Done;
        list.LastModified = DateTimeOffset.UtcNow;

        session.Store(list);
        await session.SaveChangesAsync();

        if (!wasDone && item.Done)
        {
            Console.WriteLine($"[TodoItemCompleted] Item '{item.Title}' in list '{list.Title}' was marked as done.");
        }

        return Results.NoContent();
    }

    [WolverinePatch("/api/todoitems/detail/{id}")]
    public static async Task<IResult> UpdateDetail(Guid id, UpdateTodoItemDetailRequest request, IDocumentSession session)
    {
        var currentLists = await session.Query<TodoList>()
            .Where(l => l.Items.Any(i => i.Id == id))
            .ToListAsync();

        var currentList = currentLists.FirstOrDefault();
        if (currentList is null) return Results.NotFound();

        var item = currentList.Items.First(i => i.Id == id);
        item.Priority = request.Priority;
        item.Note = request.Note;

        if (request.ListId != currentList.Id)
        {
            var targetList = await session.LoadAsync<TodoList>(request.ListId);
            if (targetList is null) return Results.NotFound();

            currentList.Items.Remove(item);
            targetList.Items.Add(item);
            currentList.LastModified = DateTimeOffset.UtcNow;
            targetList.LastModified = DateTimeOffset.UtcNow;

            session.Store(currentList);
            session.Store(targetList);
        }
        else
        {
            currentList.LastModified = DateTimeOffset.UtcNow;
            session.Store(currentList);
        }

        await session.SaveChangesAsync();
        return Results.NoContent();
    }

    [WolverineDelete("/api/todoitems/{id}")]
    public static async Task<IResult> Delete(Guid id, IDocumentSession session)
    {
        var lists = await session.Query<TodoList>()
            .Where(l => l.Items.Any(i => i.Id == id))
            .ToListAsync();

        var list = lists.FirstOrDefault();
        if (list is null) return Results.NotFound();

        list.Items.Remove(list.Items.First(i => i.Id == id));
        list.LastModified = DateTimeOffset.UtcNow;

        session.Store(list);
        await session.SaveChangesAsync();

        return Results.NoContent();
    }
}
