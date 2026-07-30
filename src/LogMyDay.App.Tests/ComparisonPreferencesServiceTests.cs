using LogMyDay.App.Services.Insights;
using Microsoft.JSInterop;
using Microsoft.JSInterop.Infrastructure;
using Moq;

namespace LogMyDay.App.Tests;

public class ComparisonPreferencesServiceTests
{
    private static Mock<IJSRuntime> JsReturning(string? storedJson)
    {
        var js = new Mock<IJSRuntime>();

        js.Setup(j => j.InvokeAsync<string?>("localStorage.getItem", It.IsAny<object?[]?>()))
            .ReturnsAsync(storedJson);

        return js;
    }

    /// <summary>Captures what <c>Save</c> writes, so a save can be fed straight back into <c>Load</c>.</summary>
    private static Mock<IJSRuntime> JsCapturingWrites(Action<string> onWrite)
    {
        var js = new Mock<IJSRuntime>();

        js.Setup(j => j.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object?[]?>()))
            .Callback((string _, object?[]? args) => onWrite((string)args![1]!))
            .ReturnsAsync(Mock.Of<IJSVoidResult>());

        return js;
    }

    // --- Nothing stored ---

    [Fact]
    public async Task Load_NoStoredValue_ReturnsDefault()
    {
        var service = new ComparisonPreferencesService(JsReturning(null).Object);

        var preferences = await service.Load([1, 2]);

        Assert.Same(ComparisonPreferences.Default, preferences);
    }

    [Fact]
    public async Task Load_EmptyStoredValue_ReturnsDefault()
    {
        var service = new ComparisonPreferencesService(JsReturning("   ").Object);

        Assert.Same(ComparisonPreferences.Default, await service.Load([1]));
    }

    [Fact]
    public async Task Load_MalformedJson_ReturnsDefault()
    {
        var service = new ComparisonPreferencesService(JsReturning("{not json").Object);

        Assert.Same(ComparisonPreferences.Default, await service.Load([1]));
    }

    [Fact]
    public async Task Load_JsonWithNoRows_ReturnsDefault()
    {
        var service = new ComparisonPreferencesService(JsReturning("""{"ColumnCount":14,"Rows":[]}""").Object);

        Assert.Same(ComparisonPreferences.Default, await service.Load([1]));
    }

    [Fact]
    public async Task Load_InteropThrows_ReturnsDefault()
    {
        // Prerendering genuinely cannot reach localStorage.
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<string?>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .ThrowsAsync(new InvalidOperationException("prerendering"));
        var service = new ComparisonPreferencesService(js.Object);

        Assert.Same(ComparisonPreferences.Default, await service.Load([1]));
    }

    // --- Validation ---

    [Fact]
    public async Task Load_StaleTagId_ResetsThatRowToNoTag()
    {
        // Tag 99 was deleted since the setup was saved.
        var json = """{"ColumnCount":14,"Rows":[{"TagId":1,"SyncMode":0,"OffsetDays":0,"Aggregation":0},{"TagId":99,"SyncMode":1,"OffsetDays":1,"Aggregation":1}]}""";
        var service = new ComparisonPreferencesService(JsReturning(json).Object);

        var preferences = await service.Load([1, 2]);

        Assert.Equal(1, preferences.Rows[0].TagId);
        Assert.Null(preferences.Rows[1].TagId);
        Assert.Equal(RowSyncMode.Offset, preferences.Rows[1].SyncMode);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(15)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Load_ColumnCountNotAPreset_FallsBackToDefault(int columnCount)
    {
        var json = $"{{\"ColumnCount\":{columnCount},\"Rows\":[{{\"TagId\":1,\"SyncMode\":0,\"OffsetDays\":0,\"Aggregation\":0}}]}}";
        var service = new ComparisonPreferencesService(JsReturning(json).Object);

        var preferences = await service.Load([1]);

        Assert.Equal(ComparisonConstants.DefaultColumnCount, preferences.ColumnCount);
    }

    [Theory]
    [InlineData(7)]
    [InlineData(14)]
    [InlineData(30)]
    [InlineData(90)]
    public async Task Load_ColumnCountIsAPreset_IsKept(int columnCount)
    {
        var json = $"{{\"ColumnCount\":{columnCount},\"Rows\":[{{\"TagId\":1,\"SyncMode\":0,\"OffsetDays\":0,\"Aggregation\":0}}]}}";
        var service = new ComparisonPreferencesService(JsReturning(json).Object);

        Assert.Equal(columnCount, (await service.Load([1])).ColumnCount);
    }

    [Fact]
    public async Task Load_FewerRowsThanMinimum_PadsToMinimum()
    {
        var json = """{"ColumnCount":14,"Rows":[{"TagId":1,"SyncMode":0,"OffsetDays":0,"Aggregation":0}]}""";
        var service = new ComparisonPreferencesService(JsReturning(json).Object);

        var preferences = await service.Load([1]);

        Assert.Equal(ComparisonConstants.MinRows, preferences.Rows.Count);
        Assert.Null(preferences.Rows[1].TagId);
    }

    [Fact]
    public async Task Load_MoreRowsThanMaximum_TruncatesToMaximum()
    {
        var rows = string.Join(",", Enumerable.Range(0, 9)
            .Select(_ => """{"TagId":1,"SyncMode":0,"OffsetDays":0,"Aggregation":0}"""));
        var service = new ComparisonPreferencesService(
            JsReturning($"{{\"ColumnCount\":14,\"Rows\":[{rows}]}}").Object);

        var preferences = await service.Load([1]);

        Assert.Equal(ComparisonConstants.MaxRows, preferences.Rows.Count);
    }

    [Fact]
    public async Task Load_AnchorRowStoredAsOffset_IsForcedSynchronizedWithZeroOffset()
    {
        var json = """{"ColumnCount":14,"Rows":[{"TagId":1,"SyncMode":1,"OffsetDays":5,"Aggregation":0},{"TagId":2,"SyncMode":0,"OffsetDays":0,"Aggregation":0}]}""";
        var service = new ComparisonPreferencesService(JsReturning(json).Object);

        var preferences = await service.Load([1, 2]);

        Assert.Equal(RowSyncMode.Synchronized, preferences.Rows[0].SyncMode);
        Assert.Equal(0, preferences.Rows[0].OffsetDays);
    }

    [Theory]
    [InlineData(999999, ComparisonConstants.MaxOffsetDays)]
    [InlineData(-999999, -ComparisonConstants.MaxOffsetDays)]
    public async Task Load_OffsetBeyondLimit_IsClamped(int stored, int expected)
    {
        var json = $"{{\"ColumnCount\":14,\"Rows\":[{{\"TagId\":1,\"SyncMode\":0,\"OffsetDays\":0,\"Aggregation\":0}},{{\"TagId\":2,\"SyncMode\":1,\"OffsetDays\":{stored},\"Aggregation\":0}}]}}";
        var service = new ComparisonPreferencesService(JsReturning(json).Object);

        var preferences = await service.Load([1, 2]);

        Assert.Equal(expected, preferences.Rows[1].OffsetDays);
    }

    [Fact]
    public async Task Load_UndefinedAggregation_FallsBackToFirst()
    {
        var json = """{"ColumnCount":14,"Rows":[{"TagId":1,"SyncMode":0,"OffsetDays":0,"Aggregation":42},{"TagId":2,"SyncMode":0,"OffsetDays":0,"Aggregation":0}]}""";
        var service = new ComparisonPreferencesService(JsReturning(json).Object);

        var preferences = await service.Load([1, 2]);

        Assert.Equal(ComparisonAggregation.First, preferences.Rows[0].Aggregation);
    }

    // --- Round trip ---

    [Fact]
    public async Task Save_ThenLoad_RestoresColumnCountRowsOffsetsAndAggregations()
    {
        string? written = null;
        var saveService = new ComparisonPreferencesService(JsCapturingWrites(json => written = json).Object);

        await saveService.Save(new ComparisonPreferences(30,
        [
            ComparisonRowConfig.Anchor(1) with { Aggregation = ComparisonAggregation.Average },
            ComparisonRowConfig.Comparison(2) with { SyncMode = RowSyncMode.Offset, OffsetDays = -365, Aggregation = ComparisonAggregation.Count }
        ]));

        Assert.NotNull(written);

        var loadService = new ComparisonPreferencesService(JsReturning(written).Object);
        var restored = await loadService.Load([1, 2]);

        Assert.Equal(30, restored.ColumnCount);
        Assert.Equal(2, restored.Rows.Count);
        Assert.Equal(1, restored.Rows[0].TagId);
        Assert.Equal(ComparisonAggregation.Average, restored.Rows[0].Aggregation);
        Assert.Equal(2, restored.Rows[1].TagId);
        Assert.Equal(RowSyncMode.Offset, restored.Rows[1].SyncMode);
        Assert.Equal(-365, restored.Rows[1].OffsetDays);
        Assert.Equal(ComparisonAggregation.Count, restored.Rows[1].Aggregation);
    }

    [Fact]
    public async Task Save_InteropThrows_DoesNotPropagate()
    {
        var js = new Mock<IJSRuntime>();
        js.Setup(j => j.InvokeAsync<IJSVoidResult>(It.IsAny<string>(), It.IsAny<object?[]?>()))
            .ThrowsAsync(new InvalidOperationException("prerendering"));
        var service = new ComparisonPreferencesService(js.Object);

        await service.Save(ComparisonPreferences.Default);
    }

    [Fact]
    public async Task Save_WritesUnderTheComparisonStorageKey()
    {
        var js = new Mock<IJSRuntime>();
        object?[]? captured = null;
        js.Setup(j => j.InvokeAsync<IJSVoidResult>("localStorage.setItem", It.IsAny<object?[]?>()))
            .Callback((string _, object?[]? args) => captured = args)
            .ReturnsAsync(Mock.Of<IJSVoidResult>());
        var service = new ComparisonPreferencesService(js.Object);

        await service.Save(ComparisonPreferences.Default);

        Assert.Equal(ComparisonConstants.StateStorageKey, captured![0]);
    }
}
