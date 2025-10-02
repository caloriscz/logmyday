using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using LogMyDay.Shared.Preferences;

namespace LogMyDay.App.Components.Shared;

public abstract class SearchableSelectBase : InputBase<string>, IAsyncDisposable
{
    private const int MaximumResults = 200;

    private readonly List<SelectOption> filtered = new();
    private DotNetObjectReference<SearchableSelectBase>? selfReference;
    private IJSObjectReference? module;
    private string? menuElementId;

    protected bool isOpen;
    protected string searchText = string.Empty;
    protected ElementReference searchInputRef;
    protected ElementReference menuRef;

    [Inject]
    protected IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter]
    public IEnumerable<SelectOption> Options { get; set; } = Array.Empty<SelectOption>();

    [Parameter]
    public string Placeholder { get; set; } = "Select an option";

    [Parameter]
    public string SearchPlaceholder { get; set; } = "Type to filter";

    [Parameter]
    public string? Id { get; set; }
        = $"searchable-select-{Guid.NewGuid():N}";

    [Parameter]
    public bool Disabled { get; set; }
        = false;

    [Parameter]
    public bool ShowValueInList { get; set; } = true;

    protected IReadOnlyList<SelectOption> FilteredOptions => filtered;

    protected string FieldClass => CssClass ?? string.Empty;

    protected string SelectedLabel => ResolveLabel(CurrentValueAsString);

    protected override void OnInitialized()
    {
        base.OnInitialized();
        selfReference = DotNetObjectReference.Create(this);
        UpdateFilter();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();
        UpdateFilter();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        module ??= await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./js/searchableSelect.js");

        if (isOpen)
        {
            menuElementId ??= await module.InvokeAsync<string>("searchableSelect.ensureElementId", menuRef);

            if (module is not null && menuElementId is not null)
            {
                await module.InvokeVoidAsync("searchableSelect.registerOutsideClick", selfReference, menuElementId);
            }

            await searchInputRef.FocusAsync();
        }
    }

    protected override bool TryParseValueFromString(string? value, out string result, out string validationErrorMessage)
    {
        result = value ?? string.Empty;
        validationErrorMessage = string.Empty;
        return true;
    }

    protected void ToggleDropdown()
    {
        if (Disabled)
        {
            return;
        }

        isOpen = !isOpen;

        if (isOpen)
        {
            searchText = string.Empty;
            UpdateFilter();
        }
        else
        {
            _ = UnregisterOutsideClickAsync();
        }

        StateHasChanged();
    }

    protected void HandleToggleKeyDown(KeyboardEventArgs args)
    {
        if (args.Key is "ArrowDown" or "Enter" or " ")
        {
            ToggleDropdown();
        }
        else if (args.Key is "Escape")
        {
            _ = CloseDropdownAsync();
        }
    }

    protected void HandleSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key is "Escape")
        {
            _ = CloseDropdownAsync();
        }
    }

    protected void HandleFocusOut(FocusEventArgs args)
    {
        _ = CloseDropdownAsync();
    }

    protected void OnSearchChanged()
    {
        UpdateFilter();
        StateHasChanged();
    }

    [JSInvokable]
    public Task CloseDropdownAsync() => CloseDropdownInternalAsync();

    protected async Task CloseDropdownInternalAsync()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;
        await UnregisterOutsideClickAsync();
        StateHasChanged();
    }

    protected void SelectOption(SelectOption option)
    {
        CurrentValue = option.Value;
        _ = CloseDropdownAsync();
    }

    protected void UpdateFilter()
    {
        filtered.Clear();

        var query = searchText?.Trim() ?? string.Empty;

        IEnumerable<SelectOption> source = Options;

        if (!string.IsNullOrWhiteSpace(query))
        {
            source = source.Where(option => option.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                || option.Value.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        filtered.AddRange(source.Take(MaximumResults));
    }

    protected string ResolveLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Placeholder;
        }

        var match = Options.FirstOrDefault(option => option.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

        return match?.Label ?? string.Create(CultureInfo.InvariantCulture, $"Unknown ({value})");
    }

    protected ValueTask UnregisterOutsideClickAsync()
    {
        if (module is null || menuElementId is null)
        {
            return ValueTask.CompletedTask;
        }

        return module.InvokeVoidAsync("searchableSelect.unregisterOutsideClick", menuElementId);
    }

    public async ValueTask DisposeAsync()
    {
        await UnregisterOutsideClickAsync();

        if (module is not null)
        {
            await module.DisposeAsync();
        }

        selfReference?.Dispose();
    }
}
