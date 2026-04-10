using FluentValidation;
using Marten;
using Wolverine.Http;
using Wolverine.Persistence;

namespace CleanArchitectureTodos;

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

public static class CreateTodoItemEndpoint
{
    // [Entity] loads the TodoList by the "ListId" property on the request
    [WolverinePost("/api/todoitems")]
    public static TodoItem Post(
        CreateTodoItemRequest request,
        [Entity("ListId", Required = true)] TodoList list,
        IDocumentSession session)
    {
        var item = new TodoItem { Title = request.Title };
        list.Items.Add(item);
        list.LastModified = DateTimeOffset.UtcNow;

        session.Store(list);

        return item;
    }
}
