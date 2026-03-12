using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using LogMyDay.Shared.Scanning;
using Microsoft.Extensions.Logging;
using Moq;
using Refit;
using System.Net;

namespace LogMyDay.Api.Tests;

public class ScanOrchestratorTests
{
    private static async Task<ApiException> CreateApiException(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode);
        var request = new HttpRequestMessage(HttpMethod.Get, "http://localhost");

        return await ApiException.Create(request, HttpMethod.Get, response, new RefitSettings());
    }

    private static ScanOrchestrator CreateOrchestrator(
        Mock<IActivityApi> activityApiMock,
        Mock<IScanMappingApi> scanMappingApiMock)
    {
        var logger = new Mock<ILogger<ScanOrchestrator>>();

        return new ScanOrchestrator(activityApiMock.Object, scanMappingApiMock.Object, logger.Object);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Process_NullOrWhitespace_ReturnsError(string? input)
    {
        var activityApi = new Mock<IActivityApi>();
        var scanMappingApi = new Mock<IScanMappingApi>();
        var orchestrator = CreateOrchestrator(activityApi, scanMappingApi);

        var result = await orchestrator.Process(input!);

        Assert.Equal(ScanResultType.Error, result.Type);
        activityApi.VerifyNoOtherCalls();
        scanMappingApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_AppFormattedCode_TagFound_ReturnsTagFound()
    {
        var activityApi = new Mock<IActivityApi>();
        var scanMappingApi = new Mock<IScanMappingApi>();
        var tag = new TagResponse { Id = 42, Title = "Magnesium" };
        activityApi.Setup(x => x.GetTagById(42)).ReturnsAsync(tag);

        var orchestrator = CreateOrchestrator(activityApi, scanMappingApi);
        var result = await orchestrator.Process("lmd://tag/42");

        Assert.Equal(ScanResultType.TagFound, result.Type);
        Assert.Equal(42, result.TagId);
        Assert.Equal("Magnesium", result.Tag!.Title);
        scanMappingApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_AppFormattedCode_TagFound_WithValue_ReturnsPrefilledValue()
    {
        var activityApi = new Mock<IActivityApi>();
        var scanMappingApi = new Mock<IScanMappingApi>();
        activityApi.Setup(x => x.GetTagById(10)).ReturnsAsync(new TagResponse { Id = 10, Title = "Dose" });

        var orchestrator = CreateOrchestrator(activityApi, scanMappingApi);
        var result = await orchestrator.Process("lmd://tag/10?v=500");

        Assert.Equal(ScanResultType.TagFound, result.Type);
        Assert.Equal("500", result.PrefilledValue);
    }

    [Fact]
    public async Task Process_AppFormattedCode_TagNotFound_ReturnsTagNotFound()
    {
        var activityApi = new Mock<IActivityApi>();
        var scanMappingApi = new Mock<IScanMappingApi>();
        var notFoundEx = await CreateApiException(HttpStatusCode.NotFound);
        activityApi.Setup(x => x.GetTagById(99)).ThrowsAsync(notFoundEx);

        var orchestrator = CreateOrchestrator(activityApi, scanMappingApi);
        var result = await orchestrator.Process("lmd://tag/99");

        Assert.Equal(ScanResultType.TagNotFound, result.Type);
        Assert.Equal(99, result.TagId);
        scanMappingApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_OpaqueCode_MappingFound_ReturnsTagFound()
    {
        var activityApi = new Mock<IActivityApi>();
        var scanMappingApi = new Mock<IScanMappingApi>();
        var mapping = new ScanMappingResponse { Id = 1, TagId = 5, DisplayName = "Iron", DefaultDescription = "14" };
        var tag = new TagResponse { Id = 5, Title = "Iron" };
        scanMappingApi
            .Setup(x => x.Lookup("1234567890"))
            .ReturnsAsync(new ScanLookupResponse { Found = true, Mapping = mapping, Tag = tag });

        var orchestrator = CreateOrchestrator(activityApi, scanMappingApi);
        var result = await orchestrator.Process("1234567890");

        Assert.Equal(ScanResultType.TagFound, result.Type);
        Assert.Equal(5, result.TagId);
        Assert.Equal("Iron", result.DisplayName);
        Assert.Equal("14", result.PrefilledValue);
        Assert.Equal(mapping, result.Mapping);
        activityApi.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Process_OpaqueCode_NoMapping_ReturnsUnknownCode()
    {
        var activityApi = new Mock<IActivityApi>();
        var scanMappingApi = new Mock<IScanMappingApi>();
        scanMappingApi
            .Setup(x => x.Lookup("unknown-barcode"))
            .ReturnsAsync(new ScanLookupResponse { Found = false });

        var orchestrator = CreateOrchestrator(activityApi, scanMappingApi);
        var result = await orchestrator.Process("unknown-barcode");

        Assert.Equal(ScanResultType.UnknownCode, result.Type);
        Assert.Equal("unknown-barcode", result.ScannedValue);
        activityApi.VerifyNoOtherCalls();
    }
}
