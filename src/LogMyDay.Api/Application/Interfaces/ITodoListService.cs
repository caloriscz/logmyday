using LogMyDay.Shared.DTOs;

namespace LogMyDay.Api.Application.Interfaces;

public interface ITodoListService
{
    Task<IList<TodoListResponse>> GetAll(Guid userId, DateOnly? date = null);
    Task<TodoListResponse> GetById(int id, Guid userId);
    Task<TodoListResponse> Create(TodoListRequest request, Guid userId);
    Task Update(int id, TodoListRequest request, Guid userId);
    Task Delete(int id, Guid userId);
}
