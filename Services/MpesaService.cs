
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LiquorPOS.Api.Models;
using Microsoft.Extensions.Options;

namespace LiquorPOS.Api.Services;

public class MpesaService
{
    private readonly HttpClient _httpClient;
    private readonly MpesaSettings _settings;

    public MpesaService(
        HttpClient httpClient,
        IOptions<MpesaSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        var credentials =
            $"{_settings.ConsumerKey}:{_settings.ConsumerSecret}";

        var encodedCredentials =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(credentials));

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_settings.BaseUrl}/oauth/v1/generate?grant_type=client_credentials");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                encodedCredentials);

        var response =
            await _httpClient.SendAsync(request);

        var responseContent =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"M-Pesa authentication failed. " +
                $"HTTP {(int)response.StatusCode}: " +
                $"Reason: {response.ReasonPhrase}. " +
                $"Response: {responseContent}");
        }

        using var json =
            JsonDocument.Parse(responseContent);

        if (!json.RootElement.TryGetProperty(
                "access_token",
                out var accessToken))
        {
            throw new Exception(
                "M-Pesa access token was not returned.");
        }

        return accessToken.GetString()
            ?? throw new Exception(
                "M-Pesa access token was empty.");
    }

    public async Task<string> InitiateStkPushAsync(
        string phoneNumber,
        decimal amount,
        string accountReference,
        string transactionDescription)
    {
        var accessToken =
            await GetAccessTokenAsync();

        phoneNumber =
            NormalizePhoneNumber(phoneNumber);

        if (phoneNumber.Length != 12 ||
            !phoneNumber.StartsWith("254"))
        {
            throw new Exception(
                "Invalid phone number. Use format 2547XXXXXXXX.");
        }

        if (amount <= 0)
        {
            throw new Exception(
                "STK Push amount must be greater than zero.");
        }

        var timestamp =
            DateTime.Now.ToString("yyyyMMddHHmmss");

        var passwordString =
            $"{_settings.ShortCode}" +
            $"{_settings.PassKey}" +
            $"{timestamp}";

        var password =
            Convert.ToBase64String(
                Encoding.UTF8.GetBytes(passwordString));

        var requestBody = new
        {
            BusinessShortCode = _settings.ShortCode,
            Password = password,
            Timestamp = timestamp,
            TransactionType = _settings.TransactionType,
            Amount = Convert.ToInt32(Math.Round(amount)),
            PartyA = phoneNumber,
            PartyB = _settings.ShortCode,
            PhoneNumber = phoneNumber,
            CallBackURL = _settings.CallbackUrl,
            AccountReference = accountReference,
            TransactionDesc = transactionDescription
        };

        var json =
            JsonSerializer.Serialize(requestBody);

        using var request =
            new HttpRequestMessage(
                HttpMethod.Post,
                $"{_settings.BaseUrl}/mpesa/stkpush/v1/processrequest");

        request.Headers.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                accessToken);

        request.Content =
            new StringContent(
                json,
                Encoding.UTF8,
                "application/json");

        var response =
            await _httpClient.SendAsync(request);

        var responseContent =
            await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception(
                $"M-Pesa STK Push failed. " +
                $"HTTP {(int)response.StatusCode}: " +
                $"Reason: {response.ReasonPhrase}. " +
                $"Response: {responseContent}");
        }

        return responseContent;
    }

    private static string NormalizePhoneNumber(
        string phoneNumber)
    {
        phoneNumber =
            phoneNumber.Trim()
                .Replace(" ", "")
                .Replace("-", "");

        if (phoneNumber.StartsWith("07") &&
            phoneNumber.Length == 10)
        {
            return "254" + phoneNumber[1..];
        }

        if (phoneNumber.StartsWith("01") &&
            phoneNumber.Length == 10)
        {
            return "254" + phoneNumber[1..];
        }

        if (phoneNumber.StartsWith("+254"))
        {
            return phoneNumber[1..];
        }

        if (phoneNumber.StartsWith("254"))
        {
            return phoneNumber;
        }

        return phoneNumber;
    }
}

