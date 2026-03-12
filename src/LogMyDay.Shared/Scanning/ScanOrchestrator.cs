using LogMyDay.Shared.DTOs;
using LogMyDay.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Refit;

namespace LogMyDay.Shared.Scanning;

public class ScanOrchestrator : IScanOrchestrator
{
    private readonly IActivityApi _activityApi;
    private readonly IScanMappingApi _scanMappingApi;
    private readonly ILogger<ScanOrchestrator> _logger;

    public ScanOrchestrator(
        IActivityApi activityApi,
        IScanMappingApi scanMappingApi,
        ILogger<ScanOrchestrator> logger)
    {
        _activityApi = activityApi;
        _scanMappingApi = scanMappingApi;
        _logger = logger;
    }

    public async Task<ScanResult> Process(string scannedValue)
    {
        if (string.IsNullOrWhiteSpace(scannedValue))
        {
            return new ScanResult
            {
                Type = ScanResultType.Error,
                ScannedValue = scannedValue
            };
        }

        var parseResult = LmdQrCodeParser.Parse(scannedValue);

        if (parseResult.IsAppFormatted)
        {
            return await ProcessAppFormatted(parseResult, scannedValue);
        }

        return await ProcessOpaqueCode(scannedValue);
    }

    private async Task<ScanResult> ProcessAppFormatted(LmdQrParseResult parseResult, string scannedValue)
    {
        try
        {
            var tag = await _activityApi.GetTagById(parseResult.TagId!.Value);

            return new ScanResult
            {
                Type = ScanResultType.TagFound,
                TagId = tag.Id,
                Tag = tag,
                PrefilledValue = parseResult.Value,
                DisplayName = parseResult.DisplayName,
                ScannedValue = scannedValue
            };
        }
        catch (ApiException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Tag {TagId} from QR code not found", parseResult.TagId);

            return new ScanResult
            {
                Type = ScanResultType.TagNotFound,
                TagId = parseResult.TagId,
                ScannedValue = scannedValue
            };
        }
    }

    private async Task<ScanResult> ProcessOpaqueCode(string scannedValue)
    {
        var lookupResult = await _scanMappingApi.Lookup(scannedValue);

        if (lookupResult.Found && lookupResult.Mapping is not null)
        {
            return new ScanResult
            {
                Type = ScanResultType.TagFound,
                TagId = lookupResult.Mapping.TagId,
                Tag = lookupResult.Tag,
                PrefilledValue = lookupResult.Mapping.DefaultDescription,
                DisplayName = lookupResult.Mapping.DisplayName,
                Mapping = lookupResult.Mapping,
                ScannedValue = scannedValue
            };
        }

        return new ScanResult
        {
            Type = ScanResultType.UnknownCode,
            ScannedValue = scannedValue
        };
    }
}
