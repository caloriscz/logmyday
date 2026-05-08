using LogMyDay.Shared.DTOs;
using Refit;

namespace LogMyDay.Shared.Interfaces;

public interface ITodoApi
{
    [Get("/api/todo-lists")]
    Task<IList<TodoListResponse>> GetTodoLists();

    [Post("/api/todo-lists")]
    Task<TodoListResponse> CreateTodoList([Body] TodoListRequest request);

    [Put("/api/todo-lists/{id}")]
    Task UpdateTodoList(int id, [Body] TodoListRequest request);

    [Delete("/api/todo-lists/{id}")]
    Task DeleteTodoList(int id);

    [Post("/api/todo-items")]
    Task<TodoItemResponse> CreateTodoItem([Body] TodoItemRequest request);

    [Put("/api/todo-items/{id}")]
    Task UpdateTodoItem(int id, [Body] TodoItemRequest request);

    [Delete("/api/todo-items/{id}")]
    Task DeleteTodoItem(int id);

    [Post("/api/todo-items/{id}/complete")]
    Task<TodoItemResponse> CompleteItem(int id, [Body] TodoItemCompleteRequest request);

    [Patch("/api/todo-lists/{listId}/items/reorder")]
    Task ReorderItems(int listId, [Body] IList<TodoItemReorderRequest> items);

    [Post("/api/todo-items/{id}/reopen")]
    Task<TodoItemResponse> ReopenItem(int id);

    [Post("/api/todo-items/{id}/skip")]
    Task<TodoItemResponse> SkipItem(int id);

    [Post("/api/todo-items/{id}/unskip")]
    Task<TodoItemResponse> UnskipItem(int id);
}
