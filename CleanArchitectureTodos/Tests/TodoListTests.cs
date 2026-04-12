using Alba;
using CleanArchitectureTodos;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace CleanArchitectureTodos.Tests;

public class TodoListTests : IAsyncLifetime
{
    private IAlbaHost _host = null!;

    public async Task InitializeAsync()
    {
        _host = await AlbaHost.For<Program>();
        var store = _host.Services.GetRequiredService<IDocumentStore>();
        await store.Advanced.Clean.DeleteAllDocumentsAsync();
    }

    public async Task DisposeAsync() => await _host.DisposeAsync();

    // --- TodoList CRUD ---

    [Fact]
    public async Task create_todo_list()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Groceries", "#FF0000")).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        var list = result.ReadAsJson<TodoList>();
        list.ShouldNotBeNull();
        list!.Title.ShouldBe("Groceries");
        list.Colour.ShouldBe("#FF0000");
    }

    [Fact]
    public async Task create_todo_list_uses_default_colour()
    {
        var result = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Default Colour List", null)).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        var list = result.ReadAsJson<TodoList>();
        list.ShouldNotBeNull();
        list!.Colour.ShouldBe("#808080");
    }

    [Fact]
    public async Task create_todo_list_duplicate_title_returns_400()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Duplicate", null)).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Duplicate", null)).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task update_todo_list()
    {
        var createResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Original", "#0000FF")).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        var created = createResult.ReadAsJson<TodoList>()!;

        var updateResult = await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateTodoListRequest("Updated", "#FF0000")).ToUrl($"/api/todolists/{created.Id}");
            x.StatusCodeShouldBe(200);
        });

        var updated = updateResult.ReadAsJson<TodoList>();
        updated.ShouldNotBeNull();
        updated!.Title.ShouldBe("Updated");
        updated.Colour.ShouldBe("#FF0000");
    }

    [Fact]
    public async Task update_todo_list_duplicate_title_returns_400()
    {
        var first = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("First", null)).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        var second = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Second", null)).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        var secondList = second.ReadAsJson<TodoList>()!;

        await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateTodoListRequest("First", null)).ToUrl($"/api/todolists/{secondList.Id}");
            x.StatusCodeShouldBe(400);
        });
    }

    [Fact]
    public async Task delete_todo_list()
    {
        var createResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("To Delete", null)).ToUrl("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        var created = createResult.ReadAsJson<TodoList>()!;

        await _host.Scenario(x =>
        {
            x.Delete.Url($"/api/todolists/{created.Id}");
            x.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task get_all_todo_lists()
    {
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("List A", null)).ToUrl("/api/todolists");
        });
        await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("List B", null)).ToUrl("/api/todolists");
        });

        var result = await _host.Scenario(x =>
        {
            x.Get.Url("/api/todolists");
            x.StatusCodeShouldBe(200);
        });

        var vm = result.ReadAsJson<TodosVm>();
        vm.ShouldNotBeNull();
        (vm!.Lists.Count >= 2).ShouldBeTrue();
        vm.PriorityLevels.ShouldNotBeEmpty();
        vm.Colours.ShouldNotBeEmpty();
    }

    // --- TodoItem CRUD ---

    [Fact]
    public async Task create_todo_item()
    {
        var listResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Items List", null)).ToUrl("/api/todolists");
        });
        var list = listResult.ReadAsJson<TodoList>()!;

        var itemResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoItemRequest(list.Id, "Buy milk")).ToUrl("/api/todoitems");
            x.StatusCodeShouldBe(200);
        });

        var item = itemResult.ReadAsJson<TodoItem>();
        item.ShouldNotBeNull();
        item!.Title.ShouldBe("Buy milk");
        item.Done.ShouldBeFalse();
    }

    [Fact]
    public async Task update_todo_item()
    {
        var listResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Update Item List", null)).ToUrl("/api/todolists");
        });
        var list = listResult.ReadAsJson<TodoList>()!;

        var itemResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoItemRequest(list.Id, "Original Item")).ToUrl("/api/todoitems");
        });
        var item = itemResult.ReadAsJson<TodoItem>()!;

        await _host.Scenario(x =>
        {
            x.Put.Json(new UpdateTodoItemRequest("Updated Item", true)).ToUrl($"/api/todoitems/{item.Id}");
            x.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task update_todo_item_detail()
    {
        var listResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Detail List", null)).ToUrl("/api/todolists");
        });
        var list = listResult.ReadAsJson<TodoList>()!;

        var itemResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoItemRequest(list.Id, "Detail Item")).ToUrl("/api/todoitems");
        });
        var item = itemResult.ReadAsJson<TodoItem>()!;

        await _host.Scenario(x =>
        {
            x.Patch.Json(new UpdateTodoItemDetailRequest(list.Id, PriorityLevel.High, "Important note"))
                .ToUrl($"/api/todoitems/detail/{item.Id}");
            x.StatusCodeShouldBe(204);
        });
    }

    [Fact]
    public async Task delete_todo_item()
    {
        var listResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoListRequest("Delete Item List", null)).ToUrl("/api/todolists");
        });
        var list = listResult.ReadAsJson<TodoList>()!;

        var itemResult = await _host.Scenario(x =>
        {
            x.Post.Json(new CreateTodoItemRequest(list.Id, "To Remove")).ToUrl("/api/todoitems");
        });
        var item = itemResult.ReadAsJson<TodoItem>()!;

        await _host.Scenario(x =>
        {
            x.Delete.Url($"/api/todoitems/{item.Id}");
            x.StatusCodeShouldBe(204);
        });
    }
}
