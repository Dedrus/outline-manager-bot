using System.Text.Json;
using Microsoft.Extensions.Options;
using Serilog;
using TDV.OutlineClient;
using TDV.OutlineClient.Models;
using TgBotVPN.Configuration;

namespace TgBotVPN.Services;

public class OutlineApiService
{
    private readonly string _apiUrl;
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;
    private readonly string _certSha256;

    public OutlineApiService(IOptions<OutlineApiSettings> options, HttpClient httpClient)
    {
        var settings = options.Value;
        _httpClient = httpClient;
        _apiUrl = settings.Url ?? throw new InvalidOperationException("Outline API URL not configured");
        _certSha256 = settings.CertSha256 ?? throw new InvalidOperationException("Outline CertSHA256 not configured");
        _logger = Log.ForContext<OutlineApiService>();
    }

    public async Task<AccessKey> CreateKeyAsync(string keyName, int dataLimitGb)
    {
        try
        {
            var client = new OutlineClient(_apiUrl, _certSha256);
            _logger.Information("Creating Outline key: {KeyName} with limit {DataLimit} GB", keyName, dataLimitGb);

            return await client.AccessKeys.New(name: keyName, limit: dataLimitGb * 1_000_000_000L);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception creating key: {KeyName}", keyName);
            throw;
        }
    }

    public async Task<bool> UpdateKeyDataLimitAsync(string keyId, int dataLimitGb)
    {
        try
        {
            var client = new OutlineClient(_apiUrl, _certSha256);
            await client.AccessKeys.SetDataLimit(keyId, dataLimitGb * 1_000_000_000L);
            _logger.Information("Key {KeyId} data limit updated successfully", keyId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception updating key {KeyId}", keyId);
            return false;
        }
    }
    public async Task<bool> DeleteKeyAsync(string keyId)
    {
        try
        {
            var client = new OutlineClient(_apiUrl, _certSha256);
            await client.AccessKeys.Delete(keyId);
            _logger.Information("Key {KeyId} deleted", keyId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Exception updating key {KeyId}", keyId);
            return false;
        }
    }
}
