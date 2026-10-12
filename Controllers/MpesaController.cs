using System.Globalization;
using System.Text.Json;

using LiquorPOS.Api.Services;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiquorPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MpesaController : ControllerBase
{
    private readonly MpesaService _mpesaService;
    private readonly SalesService _salesService;
    private readonly ILogger<MpesaController> _logger;

    public MpesaController(
        MpesaService mpesaService,
        SalesService salesService,
        ILogger<MpesaController> logger)
    {
        _mpesaService = mpesaService;
        _salesService = salesService;
        _logger = logger;
    }

    // ============================================================
    // TEST M-PESA AUTHENTICATION
    // ============================================================

    [HttpGet("test-auth")]
    public async Task<IActionResult> TestAuthentication()
    {
        try
        {
            await _mpesaService.GetAccessTokenAsync();

            return Ok(new
            {
                message =
                    "M-Pesa authentication successful."
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // STK PUSH
    // ============================================================

    [HttpPost("stk-push")]
    public async Task<IActionResult> StkPush(
        [FromBody] StkPushRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(
                    request.PhoneNumber))
            {
                return BadRequest(new
                {
                    message =
                        "Phone number is required."
                });
            }

            if (request.Amount <= 0)
            {
                return BadRequest(new
                {
                    message =
                        "Amount must be greater than zero."
                });
            }

            var accountReference =
                string.IsNullOrWhiteSpace(
                    request.AccountReference)
                    ? "LiquorPOS"
                    : request.AccountReference;

            var transactionDescription =
                string.IsNullOrWhiteSpace(
                    request.TransactionDescription)
                    ? "Liquor POS Payment"
                    : request.TransactionDescription;

            var result =
                await _mpesaService.InitiateStkPushAsync(
                    request.PhoneNumber,
                    request.Amount,
                    accountReference,
                    transactionDescription);

            return Ok(new
            {
                message =
                    "STK Push initiated successfully.",

                response =
                    result
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = ex.Message
            });
        }
    }

    // ============================================================
    // M-PESA CALLBACK
    // ============================================================

    // Safaricom calls this endpoint.
    // It MUST NOT require JWT authentication.
    [AllowAnonymous]
    [HttpPost("callback")]
    public async Task<IActionResult> MpesaCallback(
        [FromBody] JsonElement callback)
    {
        try
        {
            _logger.LogInformation(
                "M-Pesa callback received: {Callback}",
                callback.ToString());

            // ----------------------------------------------------
            // Get Body
            // ----------------------------------------------------

            if (!callback.TryGetProperty(
                    "Body",
                    out var body))
            {
                _logger.LogWarning(
                    "M-Pesa callback did not contain Body.");

                return Ok(new
                {
                    ResultCode = 0,
                    ResultDesc =
                        "Callback received."
                });
            }

            // ----------------------------------------------------
            // Get stkCallback
            // ----------------------------------------------------

            if (!body.TryGetProperty(
                    "stkCallback",
                    out var stkCallback))
            {
                _logger.LogWarning(
                    "M-Pesa callback did not contain stkCallback.");

                return Ok(new
                {
                    ResultCode = 0,
                    ResultDesc =
                        "Callback received."
                });
            }

            // ----------------------------------------------------
            // CheckoutRequestID
            // ----------------------------------------------------

            string? checkoutRequestId = null;

            if (stkCallback.TryGetProperty(
                    "CheckoutRequestID",
                    out var checkoutRequestElement))
            {
                checkoutRequestId =
                    checkoutRequestElement.GetString();
            }

            if (string.IsNullOrWhiteSpace(
                    checkoutRequestId))
            {
                _logger.LogWarning(
                    "M-Pesa callback did not contain CheckoutRequestID.");

                return Ok(new
                {
                    ResultCode = 0,
                    ResultDesc =
                        "Callback received."
                });
            }

            // ----------------------------------------------------
            // ResultCode
            // ----------------------------------------------------

            var resultCode = -1;

            if (stkCallback.TryGetProperty(
                    "ResultCode",
                    out var resultCodeElement))
            {
                resultCode =
                    resultCodeElement.GetInt32();
            }

            // ----------------------------------------------------
            // ResultDesc
            // ----------------------------------------------------

            string? resultDescription = null;

            if (stkCallback.TryGetProperty(
                    "ResultDesc",
                    out var resultDescriptionElement))
            {
                resultDescription =
                    resultDescriptionElement.GetString();
            }

            // ----------------------------------------------------
            // Callback metadata
            // ----------------------------------------------------

            string? receiptNumber = null;
            string? phoneNumber = null;
            DateTime? transactionDate = null;

            if (stkCallback.TryGetProperty(
                    "CallbackMetadata",
                    out var callbackMetadata))
            {
                if (callbackMetadata.TryGetProperty(
                        "Item",
                        out var items) &&
                    items.ValueKind ==
                        JsonValueKind.Array)
                {
                    foreach (var item in items.EnumerateArray())
                    {
                        if (!item.TryGetProperty(
                                "Name",
                                out var nameElement))
                        {
                            continue;
                        }

                        var name =
                            nameElement.GetString();

                        if (string.IsNullOrWhiteSpace(name))
                        {
                            continue;
                        }

                        // ----------------------------------------
                        // M-Pesa Receipt Number
                        // ----------------------------------------

                        if (name ==
                            "MpesaReceiptNumber")
                        {
                            if (item.TryGetProperty(
                                    "Value",
                                    out var valueElement))
                            {
                                receiptNumber =
                                    valueElement.ToString();
                            }
                        }

                        // ----------------------------------------
                        // Phone Number
                        // ----------------------------------------

                        else if (name ==
                                 "PhoneNumber")
                        {
                            if (item.TryGetProperty(
                                    "Value",
                                    out var valueElement))
                            {
                                phoneNumber =
                                    valueElement.ToString();
                            }
                        }

                        // ----------------------------------------
                        // Transaction Date
                        // ----------------------------------------

                        else if (name ==
                                 "TransactionDate")
                        {
                            if (item.TryGetProperty(
                                    "Value",
                                    out var valueElement))
                            {
                                var transactionDateValue =
                                    valueElement.ToString();

                                if (DateTime.TryParseExact(
                                        transactionDateValue,
                                        "yyyyMMddHHmmss",
                                        CultureInfo.InvariantCulture,
                                        DateTimeStyles.None,
                                        out var parsedDate))
                                {
                                    transactionDate =
                                        parsedDate;
                                }
                            }
                        }
                    }
                }
            }

            // ----------------------------------------------------
            // Process callback in SalesService
            // ----------------------------------------------------

            await _salesService.ProcessMpesaCallbackAsync(
                checkoutRequestId,
                resultCode,
                resultDescription,
                receiptNumber,
                transactionDate,
                phoneNumber);

            _logger.LogInformation(
                "M-Pesa callback processed successfully. " +
                "CheckoutRequestID: {CheckoutRequestId}, " +
                "ResultCode: {ResultCode}",
                checkoutRequestId,
                resultCode);

            // ----------------------------------------------------
            // Always acknowledge callback to Safaricom
            // ----------------------------------------------------

            return Ok(new
            {
                ResultCode = 0,
                ResultDesc =
                    "Callback received successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error processing M-Pesa callback.");

            // Safaricom should still receive an acknowledgement.
            return Ok(new
            {
                ResultCode = 0,
                ResultDesc =
                    "Callback received"
            });
        }
    }
}

// ================================================================
// STK PUSH REQUEST
// ================================================================

public class StkPushRequest
{
    public string PhoneNumber { get; set; } =
        string.Empty;

    public decimal Amount { get; set; }

    public string? AccountReference { get; set; }

    public string? TransactionDescription { get; set; }
}