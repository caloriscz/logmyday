using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using LogMyDay.App.Mobile.Services;
using LogMyDay.Shared.Preferences;

namespace LogMyDay.App.Mobile.Components.Shared;

public abstract class SearchableSelectBase : InputBase<string>, IAsyncDisposable
{
    private const int MaximumResults = 200;

    private readonly List<SelectOption> filtered = new();

    protected bool isOpen;
    protected string searchText = string.Empty;
    protected ElementReference searchInputRef;
    protected ElementReference menuRef;

    // Deterministic id for the dropdown menu element, used as the outside-click bridge token.
    protected string MenuElementId => $"{Id}-menu";

    [Inject]
    protected IWebViewInterop WebView { get; set; } = default!;

    [Inject]
    protected INativeCallbackBridge Bridge { get; set; } = default!;

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

        if (isOpen)
        {
            // Route the outside-click callback through the native bridge (no IJSRuntime).
            Bridge.Register(MenuElementId, (_, _) => InvokeAsync(CloseDropdownInternalAsync));
            await WebView.InvokeVoidAsync("LogMyDaySearchableSelect.registerOutsideClick", MenuElementId);

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
        if (args.Key is "Escape" or "Tab")
        {
            _ = CloseDropdownAsync();
        }
    }

    protected void OnSearchChanged()
    {
        UpdateFilter();
        StateHasChanged();
    }

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

    protected async Task UnregisterOutsideClickAsync()
    {
        await WebView.InvokeVoidAsync("LogMyDaySearchableSelect.unregisterOutsideClick", MenuElementId);
        Bridge.Unregister(MenuElementId);
    }

    public async ValueTask DisposeAsync()
    {
        await UnregisterOutsideClickAsync();
    }
}
