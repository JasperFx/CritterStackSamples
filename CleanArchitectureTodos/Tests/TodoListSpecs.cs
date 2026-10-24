using Bobcat;
using Bobcat.Xunit;

namespace CleanArchitectureTodos.Tests;

/// <summary>
/// Five of the original tests, rewritten as Bobcat specifications. Each one arranges the stored
/// TodoList documents directly instead of through earlier requests, sends ONE request, and asserts
/// the response and what is now in the store. The rest of the original tests are still in
/// <see cref="TodoListTests" />, unchanged apart from the move to xUnit v3.
/// </summary>
[BobcatFeature("Todo Lists")]
public class TodoListSpecs(TodosHost app) : TodosSpec(app)
{
    [BobcatSpec(typeof(CreateTodoListRequest))]
    public async Task create_a_todo_list()
    {
        await WhenPosted(new CreateTodoListRequest("Groceries", "#FF0000"), "/api/todolists");

        ThenTheResponseStatusIs(200);
        var created = await TheResponse<TodoList>();
        await ThenDocument<TodoList>(created.Id,
            Specify<TodoList>().With(x => x.Title, "Groceries").With(x => x.Colour, "#FF0000"));
    }

    [BobcatSpec(typeof(CreateTodoListRequest))]
    public async Task a_duplicate_title_is_refused()
    {
        await GivenReadModel(new TodoList { Title = "Duplicate" });

        await WhenPosted(new CreateTodoListRequest("Duplicate", null), "/api/todolists");

        await ThenTheRequestIsRefusedWith("'Duplicate' already exists.");
        await ThenSingleDocument<TodoList>(Specify<TodoList>().With(x => x.Title, "Duplicate"));
    }

    [BobcatSpec(typeof(UpdateTodoListRequest))]
    public async Task rename_and_recolour_a_todo_list()
    {
        // Marten assigns the HiLo id when the document is arranged
        var original = new TodoList { Title = "Original", Colour = "#0000FF" };
        await GivenReadModel(original);

        await WhenPut(new UpdateTodoListRequest("Updated", "#FF0000"), $"/api/todolists/{original.Id}");

        ThenTheResponseStatusIs(200);
        await ThenDocument<TodoList>(original.Id,
            Specify<TodoList>().With(x => x.Title, "Updated").With(x => x.Colour, "#FF0000"));
    }

    [BobcatSpec]
    public async Task delete_a_todo_list()
    {
        var list = new TodoList { Title = "To Delete" };
        await GivenReadModel(list);

        await WhenDeleted($"/api/todolists/{list.Id}");

        ThenTheResponseStatusIs(204);
        await ThenNoReadModel<TodoList>(list.Id);
    }

    [BobcatSpec(typeof(UpdateTodoItemDetailRequest))]
    public async Task set_the_priority_and_note_of_a_todo_item()
    {
        var item = new TodoItem { Title = "Detail Item" };
        var list = new TodoList { Title = "Detail List", Items = [item] };
        await GivenReadModel(list);

        await WhenPatched(new UpdateTodoItemDetailRequest(list.Id, PriorityLevel.High, "Important note"),
            $"/api/todoitems/detail/{item.Id}");

        ThenTheResponseStatusIs(204);
        var stored = await ThenDocument<TodoList>(list.Id);
        TheItemIs(stored, "Detail Item", PriorityLevel.High, "Important note");
    }

    [Then("the list holds only {title}, with priority {priority} and note {note}")]
    public void TheItemIs(TodoList list, string title, PriorityLevel priority, string? note)
    {
        SpecAssert.Check("items", list.Items.Count, 1);
        var item = list.Items.Single();
        SpecAssert.Check("title", item.Title, title);
        SpecAssert.Check("priority", item.Priority, priority);
        SpecAssert.Check("note", item.Note, note);
    }
}
