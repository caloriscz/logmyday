namespace LogMyDay.Domain.Enums;

/// <summary>
/// What an API key may do on behalf of its user. Admin-only operations additionally require the
/// owning user to be an admin; there is no separate admin scope.
/// </summary>
public enum ApiKeyScope
{
    ReadOnly = 0,
    ReadWrite = 1
}
