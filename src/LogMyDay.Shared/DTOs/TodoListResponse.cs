using LogMyDay.Domain.Enums;

namespace LogMyDay.Shared.DTOs;

public class TodoListResponse
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public int DisplayOrder { get; set; }
    public TodoListType ListType { get; set; }
    public DateTime DateCreated { get; set; }
    public int? CompletionTagId { get; set; }
    public string? CompletionTagName { get; set; }
    public IList<TodoItemResponse> Items { get; set; } = new List<TodoItemResponse>();
}
