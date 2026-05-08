using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITodoItemService
{
    Task<TodoItemResponse> Create(TodoItemRequest request, Guid userId);
    Task Update(int id, TodoItemRequest request, Guid userId);
    Task Delete(int id, Guid userId);
    Task<TodoItemResponse> Complete(int id, TodoItemCompleteRequest request, Guid userId);
    Task<TodoItemResponse> Reopen(int id, Guid userId);
    Task<TodoItemResponse> Skip(int id, Guid userId);
    Task<TodoItemResponse> Unskip(int id, Guid userId);
    Task Reorder(int listId, IList<TodoItemReorderRequest> items, Guid userId);
}
