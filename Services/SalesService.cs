using System.Security.Claims;
using System.Text.Json;

using LiquorPOS.Api.Data;
using LiquorPOS.Api.DTOs;
using LiquorPOS.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Services;

public class SalesService
{
    private readonly LiquorPosDbContext _db;
    private readonly PermissionService _permissionService;
    private readonly MpesaService _mpesaService;

    public SalesService(
        LiquorPosDbContext db,
        PermissionService permissionService,
        MpesaService mpesaService)
    {
        _db = db;
        _permissionService = permissionService;
        _mpesaService = mpesaService;
    }

    // ============================================================
    // CHECKOUT
    // ============================================================

    public async Task<(bool Success, string Message, long? SaleId)> Checkout(
        CheckoutRequest request,
        ClaimsPrincipal user)
    {
        // --------------------------------------------------------
        // 1. Get authenticated user
        // --------------------------------------------------------

        var userIdValue =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            userIdValue =
                user.FindFirst("sub")?.Value;
        }

        if (!int.TryParse(userIdValue, out var userId))
        {
            return (
                false,
                "Authenticated user could not be identified.",
                null);
        }

        // --------------------------------------------------------
        // 2. Get user
        // --------------------------------------------------------

        var currentUser =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (currentUser == null)
        {
            return (
                false,
                "User was not found.",
                null);
        }

        // --------------------------------------------------------
        // 3. Business and Branch
        // --------------------------------------------------------

        var businessIdValue =
            user.FindFirst("BusinessId")?.Value;

        var branchIdValue =
            user.FindFirst("BranchId")?.Value;

        if (!int.TryParse(
                businessIdValue,
                out var businessId))
        {
            return (
                false,
                "Business could not be identified.",
                null);
        }

        if (!int.TryParse(
                branchIdValue,
                out var branchId))
        {
            return (
                false,
                "Branch could not be identified.",
                null);
        }

        // --------------------------------------------------------
        // 4. Permission
        // --------------------------------------------------------

        var roleId =
            currentUser.RoleId;

        if (roleId <= 0)
        {
            return (
                false,
                "User role could not be identified.",
                null);
        }

        var hasPermission =
            await _permissionService.HasPermission(
                roleId,
                "CREATE_SALE");

        if (!hasPermission)
        {
            return (
                false,
                "You do not have permission to create sales.",
                null);
        }

        // --------------------------------------------------------
        // 5. Basic validation
        // --------------------------------------------------------

        if (request.POSSessionId <= 0)
        {
            return (
                false,
                "POS session is required.",
                null);
        }

        if (request.PriceGroupId <= 0)
        {
            return (
                false,
                "Price group is required.",
                null);
        }

        if (request.Lines == null ||
            request.Lines.Count == 0)
        {
            return (
                false,
                "At least one sale line is required.",
                null);
        }

        if (request.Payments == null ||
            request.Payments.Count == 0)
        {
            return (
                false,
                "At least one payment is required.",
                null);
        }

        // --------------------------------------------------------
        // 6. Validate POS session
        // --------------------------------------------------------

        var session =
            await _db.POSSessions
                .FirstOrDefaultAsync(x =>
                    x.POSSessionId == request.POSSessionId &&
                    x.UserId == userId &&
                    x.ClosedAt == null &&
                    x.Status.ToUpper() == "OPEN");

        if (session == null)
        {
            return (
                false,
                "The POS session does not exist, is closed, or does not belong to the logged-in user.",
                null);
        }

        // --------------------------------------------------------
        // 7. Validate terminal
        // --------------------------------------------------------

        var terminal =
            await _db.POSTerminals
                .FirstOrDefaultAsync(x =>
                    x.POSTerminalId == session.POSTerminalId &&
                    x.BusinessId == businessId &&
                    x.BranchId == branchId &&
                    x.IsActive);

        if (terminal == null)
        {
            return (
                false,
                "The POS terminal is invalid, inactive, or does not belong to this branch.",
                null);
        }

        // --------------------------------------------------------
        // 8. Validate price group
        // --------------------------------------------------------

        var priceGroupExists =
            await _db.PriceGroups
                .AnyAsync(x =>
                    x.PriceGroupId == request.PriceGroupId &&
                    x.BusinessId == businessId);

        if (!priceGroupExists)
        {
            return (
                false,
                "The selected price group is invalid.",
                null);
        }

        // --------------------------------------------------------
        // 9. M-Pesa validation
        // --------------------------------------------------------

        var mpesaPayments =
            request.Payments
                .Where(x =>
                    !string.IsNullOrWhiteSpace(x.PaymentMethod) &&
                    x.PaymentMethod.Trim().ToUpper() == "MPESA")
                .ToList();

        if (mpesaPayments.Count > 1)
        {
            return (
                false,
                "Only one M-Pesa payment is allowed per sale.",
                null);
        }

        // --------------------------------------------------------
        // 10. Database transaction
        // --------------------------------------------------------

        await using var transaction =
            await _db.Database.BeginTransactionAsync();

        try
        {
            var saleDate =
                DateTime.Now;

            // ----------------------------------------------------
            // 11. Prepare sale lines
            // ----------------------------------------------------

            var saleLines =
                new List<SaleLine>();

            decimal subTotal = 0;
            decimal discountTotal = 0;
            decimal taxTotal = 0;

            foreach (var requestLine in request.Lines)
            {
                if (requestLine.Quantity <= 0)
                {
                    return (
                        false,
                        "Quantity must be greater than zero.",
                        null);
                }

                // ------------------------------------------------
                // Product
                // ------------------------------------------------

                var product =
                    await _db.Products
                        .FirstOrDefaultAsync(x =>
                            x.ProductId == requestLine.ProductId &&
                            x.BusinessId == businessId);

                if (product == null)
                {
                    return (
                        false,
                        $"Product {requestLine.ProductId} was not found.",
                        null);
                }

                // ------------------------------------------------
                // Product Unit
                // ------------------------------------------------

                var productUnit =
                    await _db.ProductUnits
                        .FirstOrDefaultAsync(x =>
                            x.ProductUnitId == requestLine.ProductUnitId &&
                            x.ProductId == requestLine.ProductId);

                if (productUnit == null)
                {
                    return (
                        false,
                        $"The selected unit is invalid for product {product.ProductName}.",
                        null);
                }

                if (productUnit.ConversionFactor <= 0)
                {
                    return (
                        false,
                        $"Invalid conversion factor for {product.ProductName}.",
                        null);
                }

                // ------------------------------------------------
                // Price
                // ------------------------------------------------

                var productPrice =
                    await _db.ProductPrices
                        .Where(x =>
                            x.ProductId == requestLine.ProductId &&
                            x.ProductUnitId == requestLine.ProductUnitId &&
                            x.PriceGroupId == request.PriceGroupId &&
                            x.IsActive &&
                            x.StartDate <= saleDate &&
                            (x.EndDate == null ||
                             x.EndDate >= saleDate))
                        .OrderByDescending(x => x.StartDate)
                        .FirstOrDefaultAsync();

                if (productPrice == null)
                {
                    return (
                        false,
                        $"No active price was found for {product.ProductName}.",
                        null);
                }

                // ------------------------------------------------
                // Discount
                // ------------------------------------------------

                if (requestLine.DiscountAmount < 0)
                {
                    return (
                        false,
                        $"Invalid discount for {product.ProductName}.",
                        null);
                }

                var lineGross =
                    requestLine.Quantity *
                    productPrice.UnitPrice;

                var lineDiscount =
                    requestLine.DiscountAmount;

                if (lineDiscount > lineGross)
                {
                    return (
                        false,
                        $"Discount cannot exceed the line amount for {product.ProductName}.",
                        null);
                }

                // ------------------------------------------------
                // Tax
                // ------------------------------------------------

                decimal lineTax = 0;

                // ------------------------------------------------
                // Line total
                // ------------------------------------------------

                var lineTotal =
                    lineGross -
                    lineDiscount +
                    lineTax;

                // ------------------------------------------------
                // Stock
                // ------------------------------------------------

                var currentStock =
                    await GetAvailableStockAsync(
                        businessId,
                        branchId,
                        requestLine.ProductId);

                var requiredStock =
                    requestLine.Quantity *
                    productUnit.ConversionFactor;

                if (requiredStock > currentStock)
                {
                    return (
                        false,
                        $"Insufficient stock for {product.ProductName}. " +
                        $"Available: {currentStock}, " +
                        $"Required: {requiredStock}.",
                        null);
                }

                // ------------------------------------------------
                // Sale line
                // ------------------------------------------------

                saleLines.Add(
                    new SaleLine
                    {
                        ProductId =
                            requestLine.ProductId,

                        ProductUnitId =
                            requestLine.ProductUnitId,

                        Quantity =
                            requestLine.Quantity,

                        UnitPrice =
                            productPrice.UnitPrice,

                        DiscountAmount =
                            lineDiscount,

                        TaxAmount =
                            lineTax,

                        LineTotal =
                            lineTotal
                    });

                subTotal +=
                    lineGross;

                discountTotal +=
                    lineDiscount;

                taxTotal +=
                    lineTax;
            }

            // ----------------------------------------------------
            // 12. Sale total
            // ----------------------------------------------------

            var totalAmount =
                subTotal -
                discountTotal +
                taxTotal;

            if (totalAmount <= 0)
            {
                return (
                    false,
                    "Sale total must be greater than zero.",
                    null);
            }

            // ----------------------------------------------------
            // 13. Sale number
            // ----------------------------------------------------

            var saleNumber =
                $"SALE-{DateTime.Now:yyyyMMddHHmmssfff}";

            var hasMpesa =
                mpesaPayments.Count > 0;

            // ----------------------------------------------------
            // 14. Create sale
            // ----------------------------------------------------

            var sale =
                new Sale
                {
                    BusinessId =
                        businessId,

                    BranchId =
                        branchId,

                    POSSessionId =
                        request.POSSessionId,

                    UserId =
                        userId,

                    CustomerId =
                        request.CustomerId,

                    SaleNumber =
                        saleNumber,

                    SaleDate =
                        saleDate,

                    PriceGroupId =
                        request.PriceGroupId,

                    SubTotal =
                        subTotal,

                    DiscountAmount =
                        discountTotal,

                    TaxAmount =
                        taxTotal,

                    TotalAmount =
                        totalAmount,

                    Status =
                        hasMpesa
                            ? "PENDING"
                            : "COMPLETED",

                    Notes =
                        request.Notes,

                    CreatedAt =
                        saleDate
                };

            _db.Sales.Add(sale);

            await _db.SaveChangesAsync();

            // ----------------------------------------------------
            // 15. Create sale lines
            // ----------------------------------------------------

            foreach (var saleLine in saleLines)
            {
                saleLine.SaleId =
                    sale.SaleId;
            }

            _db.SaleLines.AddRange(
                saleLines);

            await _db.SaveChangesAsync();

            // ----------------------------------------------------
            // 16. Payments
            // ----------------------------------------------------

            decimal paymentTotal = 0;
            decimal cashAmountTendered = 0;

            PaymentRequest? cashPaymentRequest = null;

            foreach (var payment in request.Payments)
            {
                if (string.IsNullOrWhiteSpace(
                        payment.PaymentMethod))
                {
                    return (
                        false,
                        "Payment method is required.",
                        null);
                }

                var paymentMethod =
                    payment.PaymentMethod
                        .Trim()
                        .ToUpper();

                if (payment.AmountTendered <= 0)
                {
                    return (
                        false,
                        $"Invalid amount for {paymentMethod}.",
                        null);
                }

                // ------------------------------------------------
                // CASH
                // ------------------------------------------------

                if (paymentMethod == "CASH")
                {
                    cashAmountTendered +=
                        payment.AmountTendered;

                    cashPaymentRequest =
                        payment;

                    continue;
                }

                // ------------------------------------------------
                // MPESA
                // ------------------------------------------------

                if (paymentMethod == "MPESA")
                {
                    var outstanding =
                        totalAmount -
                        paymentTotal;

                    if (outstanding <= 0)
                    {
                        return (
                            false,
                            "No outstanding amount remains for M-Pesa.",
                            null);
                    }

                    if (payment.AmountTendered >
                        outstanding)
                    {
                        return (
                            false,
                            "M-Pesa amount cannot exceed the outstanding sale amount.",
                            null);
                    }

                    _db.SalePayments.Add(
                        new SalePayment
                        {
                            SaleId =
                                sale.SaleId,

                            PaymentMethod =
                                "MPESA",

                            Amount =
                                payment.AmountTendered,

                            ReferenceNumber =
                                payment.ReferenceNumber,

                            PaymentStatus =
                                "PENDING",

                            TransactionDate =
                                saleDate,

                            Notes =
                                payment.Notes,

                            AmountTendered =
                                payment.AmountTendered,

                            ChangeAmount =
                                0,

                            MpesaPhoneNumber =
                                payment.PhoneNumber
                        });

                    paymentTotal +=
                        payment.AmountTendered;

                    continue;
                }

                // ------------------------------------------------
                // OTHER PAYMENT METHODS
                // ------------------------------------------------

                var remainingAmount =
                    totalAmount -
                    paymentTotal;

                if (remainingAmount <= 0)
                {
                    return (
                        false,
                        "No outstanding amount remains.",
                        null);
                }

                if (payment.AmountTendered >
                    remainingAmount)
                {
                    return (
                        false,
                        $"{paymentMethod} amount cannot exceed the outstanding sale amount.",
                        null);
                }

                _db.SalePayments.Add(
                    new SalePayment
                    {
                        SaleId =
                            sale.SaleId,

                        PaymentMethod =
                            paymentMethod,

                        Amount =
                            payment.AmountTendered,

                        ReferenceNumber =
                            payment.ReferenceNumber,

                        PaymentStatus =
                            "PAID",

                        TransactionDate =
                            saleDate,

                        Notes =
                            payment.Notes,

                        AmountTendered =
                            payment.AmountTendered,

                        ChangeAmount =
                            0
                    });

                paymentTotal +=
                    payment.AmountTendered;
            }

            // ----------------------------------------------------
            // 17. Cash
            // ----------------------------------------------------

            if (cashAmountTendered > 0)
            {
                var remainingAmount =
                    totalAmount -
                    paymentTotal;

                if (remainingAmount < 0)
                {
                    return (
                        false,
                        "Payment exceeds the sale total.",
                        null);
                }

                var cashPaymentAmount =
                    Math.Min(
                        cashAmountTendered,
                        remainingAmount);

                var cashChange =
                    cashAmountTendered -
                    cashPaymentAmount;

                paymentTotal +=
                    cashPaymentAmount;

                _db.SalePayments.Add(
                    new SalePayment
                    {
                        SaleId =
                            sale.SaleId,

                        PaymentMethod =
                            "CASH",

                        Amount =
                            cashPaymentAmount,

                        ReferenceNumber =
                            null,

                        PaymentStatus =
                            "PAID",

                        TransactionDate =
                            saleDate,

                        Notes =
                            cashPaymentRequest?.Notes,

                        AmountTendered =
                            cashAmountTendered,

                        ChangeAmount =
                            cashChange
                    });
            }

            // ----------------------------------------------------
            // 18. Validate payment total
            // ----------------------------------------------------

            if (paymentTotal < totalAmount)
            {
                return (
                    false,
                    $"Payment is insufficient. " +
                    $"Required: {totalAmount}, " +
                    $"Received: {paymentTotal}.",
                    null);
            }

            if (paymentTotal > totalAmount)
            {
                return (
                    false,
                    "Payment exceeds the sale total.",
                    null);
            }

            // ----------------------------------------------------
            // 19. Save payments
            // ----------------------------------------------------

            await _db.SaveChangesAsync();

            // ----------------------------------------------------
            // 20. Complete normal sale
            // ----------------------------------------------------

            if (!hasMpesa)
            {
                await CreateStockTransactionsAsync(
                    sale.SaleId,
                    userId);

                sale.Status =
                    "COMPLETED";

                await _db.SaveChangesAsync();
            }

            // ----------------------------------------------------
            // 21. Commit database transaction
            // ----------------------------------------------------

            await transaction.CommitAsync();

            // ----------------------------------------------------
            // 22. M-Pesa STK Push
            // ----------------------------------------------------

            if (hasMpesa)
            {
                var mpesaRequest =
                    mpesaPayments[0];

                var mpesaPayment =
                    await _db.SalePayments
                        .FirstOrDefaultAsync(x =>
                            x.SaleId == sale.SaleId &&
                            x.PaymentMethod == "MPESA" &&
                            x.PaymentStatus == "PENDING");

                if (mpesaPayment == null)
                {
                    return (
                        false,
                        "Pending M-Pesa payment was not found.",
                        sale.SaleId);
                }

                if (string.IsNullOrWhiteSpace(
                        mpesaRequest.PhoneNumber))
                {
                    mpesaPayment.PaymentStatus =
                        "FAILED";

                    mpesaPayment.MpesaResultDescription =
                        "M-Pesa phone number is required.";

                    sale.Status =
                        "FAILED";

                    await _db.SaveChangesAsync();

                    return (
                        false,
                        "M-Pesa phone number is required.",
                        sale.SaleId);
                }

                try
                {
                    // ------------------------------------------------
                    // Initiate STK Push
                    // ------------------------------------------------

                    var stkResponseJson =
                        await _mpesaService.InitiateStkPushAsync(
                            mpesaRequest.PhoneNumber,
                            mpesaPayment.Amount,
                            $"SALE-{sale.SaleId}",
                            "LiquorPOS Sale");

                    if (string.IsNullOrWhiteSpace(
                            stkResponseJson))
                    {
                        mpesaPayment.PaymentStatus =
                            "FAILED";

                        mpesaPayment.MpesaResultDescription =
                            "No response was received from M-Pesa.";

                        sale.Status =
                            "FAILED";

                        await _db.SaveChangesAsync();

                        return (
                            false,
                            "M-Pesa STK Push failed.",
                            sale.SaleId);
                    }

                    // ------------------------------------------------
                    // Parse M-Pesa response
                    // ------------------------------------------------

                    using var stkJson =
                        JsonDocument.Parse(
                            stkResponseJson);

                    var root =
                        stkJson.RootElement;

                    string? merchantRequestId = null;
                    string? checkoutRequestId = null;
                    string? responseDescription = null;
                    string? responseCode = null;

                    if (root.TryGetProperty(
                            "MerchantRequestID",
                            out var merchantRequestElement))
                    {
                        merchantRequestId =
                            merchantRequestElement.GetString();
                    }

                    if (root.TryGetProperty(
                            "CheckoutRequestID",
                            out var checkoutRequestElement))
                    {
                        checkoutRequestId =
                            checkoutRequestElement.GetString();
                    }

                    if (root.TryGetProperty(
                            "ResponseDescription",
                            out var responseDescriptionElement))
                    {
                        responseDescription =
                            responseDescriptionElement.GetString();
                    }

                    if (root.TryGetProperty(
                            "ResponseCode",
                            out var responseCodeElement))
                    {
                        responseCode =
                            responseCodeElement.GetString();
                    }

                    // ------------------------------------------------
                    // Save M-Pesa identifiers
                    // ------------------------------------------------

                    mpesaPayment.MerchantRequestId =
                        merchantRequestId;

                    mpesaPayment.CheckoutRequestId =
                        checkoutRequestId;

                    mpesaPayment.MpesaResultDescription =
                        responseDescription;

                    // ------------------------------------------------
                    // STK accepted
                    // ------------------------------------------------

                    if (responseCode == "0")
                    {
                        mpesaPayment.PaymentStatus =
                            "PENDING";

                        await _db.SaveChangesAsync();

                        return (
                            true,
                            "Sale created successfully. " +
                            "M-Pesa STK Push initiated. " +
                            "Waiting for payment confirmation.",
                            sale.SaleId);
                    }

                    // ------------------------------------------------
                    // STK rejected
                    // ------------------------------------------------

                    mpesaPayment.PaymentStatus =
                        "FAILED";

                    mpesaPayment.MpesaResultDescription =
                        responseDescription ??
                        "M-Pesa STK Push request was rejected.";

                    sale.Status =
                        "FAILED";

                    await _db.SaveChangesAsync();

                    return (
                        false,
                        responseDescription ??
                        "M-Pesa STK Push was rejected.",
                        sale.SaleId);
                }
                catch (JsonException)
                {
                    mpesaPayment.PaymentStatus =
                        "FAILED";

                    mpesaPayment.MpesaResultDescription =
                        "Invalid response received from M-Pesa.";

                    sale.Status =
                        "FAILED";

                    await _db.SaveChangesAsync();

                    return (
                        false,
                        "M-Pesa returned an invalid response.",
                        sale.SaleId);
                }
                catch (Exception ex)
                {
                    mpesaPayment.PaymentStatus =
                        "FAILED";

                    mpesaPayment.MpesaResultDescription =
                        ex.Message;

                    sale.Status =
                        "FAILED";

                    await _db.SaveChangesAsync();

                    return (
                        false,
                        "M-Pesa STK Push failed.",
                        sale.SaleId);
                }
            }

            // ----------------------------------------------------
            // 23. Completed normal sale
            // ----------------------------------------------------

            return (
                true,
                "Sale completed successfully.",
                sale.SaleId);
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    // ============================================================
    // GET AVAILABLE STOCK
    // ============================================================

    private async Task<decimal> GetAvailableStockAsync(
        int businessId,
        int branchId,
        int productId)
    {
        var stock =
            await _db.StockTransactions
                .Where(x =>
                    x.BusinessId == businessId &&
                    x.BranchId == branchId &&
                    x.ProductId == productId)
                .SumAsync(
                    x => (decimal?)x.Quantity);

        return stock ?? 0;
    }

    // ============================================================
    // CREATE STOCK TRANSACTIONS
    // ============================================================

    private async Task CreateStockTransactionsAsync(
        long saleId,
        int userId)
    {
        var sale =
            await _db.Sales
                .FirstOrDefaultAsync(x =>
                    x.SaleId == saleId);

        if (sale == null)
        {
            throw new InvalidOperationException(
                "Sale not found.");
        }

        // --------------------------------------------------------
        // Prevent duplicate stock deduction
        // --------------------------------------------------------

        var alreadyDeducted =
            await _db.StockTransactions
                .AnyAsync(x =>
                    x.ReferenceType == "SALE" &&
                    x.ReferenceId == saleId &&
                    x.TransactionType == "SALE");

        if (alreadyDeducted)
        {
            return;
        }

        // --------------------------------------------------------
        // Get sale lines
        // --------------------------------------------------------

        var saleLines =
            await _db.SaleLines
                .Where(x =>
                    x.SaleId == saleId)
                .ToListAsync();

        foreach (var saleLine in saleLines)
        {
            var productUnit =
                await _db.ProductUnits
                    .FirstOrDefaultAsync(x =>
                        x.ProductUnitId ==
                            saleLine.ProductUnitId &&
                        x.ProductId ==
                            saleLine.ProductId);

            if (productUnit == null)
            {
                throw new InvalidOperationException(
                    $"Product unit {saleLine.ProductUnitId} was not found.");
            }

            if (productUnit.ConversionFactor <= 0)
            {
                throw new InvalidOperationException(
                    $"Invalid conversion factor for product {saleLine.ProductId}.");
            }

            // ----------------------------------------------------
            // Convert to base units
            // ----------------------------------------------------

            var stockQuantity =
                saleLine.Quantity *
                productUnit.ConversionFactor;

            // ----------------------------------------------------
            // Find latest positive stock cost
            // ----------------------------------------------------

            var costTransaction =
                await _db.StockTransactions
                    .Where(x =>
                        x.BusinessId == sale.BusinessId &&
                        x.BranchId == sale.BranchId &&
                        x.ProductId == saleLine.ProductId &&
                        x.Quantity > 0 &&
                        x.UnitCost > 0)
                    .OrderByDescending(
                        x => x.TransactionDate)
                    .ThenByDescending(
                        x => x.StockTransactionId)
                    .FirstOrDefaultAsync();

            var unitCost =
                costTransaction?.UnitCost ?? 0;

            // ----------------------------------------------------
            // Create stock deduction
            // ----------------------------------------------------

            _db.StockTransactions.Add(
                new StockTransaction
                {
                    BusinessId =
                        sale.BusinessId,

                    BranchId =
                        sale.BranchId,

                    ProductId =
                        saleLine.ProductId,

                    TransactionType =
                        "SALE",

                    Quantity =
                        -stockQuantity,

                    UnitCost =
                        unitCost,

                    ReferenceType =
                        "SALE",

                    ReferenceId =
                        sale.SaleId,

                    TransactionDate =
                        DateTime.Now,

                    UserId =
                        userId,

                    Notes =
                        $"Sale {sale.SaleNumber}"
                });
        }

        await _db.SaveChangesAsync();
    }

    // ============================================================
    // PROCESS M-PESA CALLBACK
    // ============================================================

    public async Task ProcessMpesaCallbackAsync(
        string checkoutRequestId,
        int resultCode,
        string? resultDescription,
        string? receiptNumber,
        DateTime? transactionDate,
        string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(
                checkoutRequestId))
        {
            return;
        }

        // --------------------------------------------------------
        // Find payment
        // --------------------------------------------------------

        var payment =
            await _db.SalePayments
                .FirstOrDefaultAsync(x =>
                    x.CheckoutRequestId ==
                        checkoutRequestId);

        if (payment == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Find sale
        // --------------------------------------------------------

        var sale =
            await _db.Sales
                .FirstOrDefaultAsync(x =>
                    x.SaleId ==
                        payment.SaleId);

        if (sale == null)
        {
            return;
        }

        // --------------------------------------------------------
        // Idempotency
        // --------------------------------------------------------

        if (payment.PaymentStatus == "PAID")
        {
            return;
        }

        // --------------------------------------------------------
        // Failed payment
        // --------------------------------------------------------

        if (resultCode != 0)
        {
            payment.PaymentStatus =
                "FAILED";

            payment.MpesaResultCode =
                resultCode;

            payment.MpesaResultDescription =
                resultDescription;

            payment.MpesaTransactionDate =
                transactionDate;

            if (!string.IsNullOrWhiteSpace(
                    phoneNumber))
            {
                payment.MpesaPhoneNumber =
                    phoneNumber;
            }

            sale.Status =
                "FAILED";

            await _db.SaveChangesAsync();

            return;
        }

        // --------------------------------------------------------
        // Successful payment
        // --------------------------------------------------------

        payment.PaymentStatus =
            "PAID";

        payment.MpesaResultCode =
            resultCode;

        payment.MpesaResultDescription =
            resultDescription;

        payment.MpesaReceiptNumber =
            receiptNumber;

        payment.MpesaTransactionDate =
            transactionDate;

        if (!string.IsNullOrWhiteSpace(
                phoneNumber))
        {
            payment.MpesaPhoneNumber =
                phoneNumber;
        }

        await _db.SaveChangesAsync();

        // --------------------------------------------------------
        // Get all payments for sale
        // --------------------------------------------------------

        var payments =
            await _db.SalePayments
                .Where(x =>
                    x.SaleId ==
                        sale.SaleId)
                .ToListAsync();

        var paidAmount =
            payments
                .Where(x =>
                    x.PaymentStatus == "PAID")
                .Sum(x => x.Amount);

        // --------------------------------------------------------
        // Fully paid
        // --------------------------------------------------------

        if (paidAmount >= sale.TotalAmount)
        {
            sale.Status =
                "COMPLETED";

            await _db.SaveChangesAsync();

            await CreateStockTransactionsAsync(
                sale.SaleId,
                sale.UserId);
        }
        else
        {
            sale.Status =
                "PENDING";

            await _db.SaveChangesAsync();
        }
    }

    // ============================================================
    // GET SALE
    // ============================================================

    public async Task<(bool Success, string Message, object? Data)> GetSale(
        long saleId,
        ClaimsPrincipal user)
    {
        // --------------------------------------------------------
        // 1. Get authenticated user
        // --------------------------------------------------------

        var userIdValue =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            userIdValue =
                user.FindFirst("sub")?.Value;
        }

        if (!int.TryParse(userIdValue, out var userId))
        {
            return (
                false,
                "Authenticated user could not be identified.",
                null);
        }

        // --------------------------------------------------------
        // 2. Get current user
        // --------------------------------------------------------

        var currentUser =
            await _db.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (currentUser == null)
        {
            return (
                false,
                "User was not found.",
                null);
        }

        // --------------------------------------------------------
        // 3. Business and Branch from claims
        // --------------------------------------------------------

        var businessIdValue =
            user.FindFirst("BusinessId")?.Value;

        var branchIdValue =
            user.FindFirst("BranchId")?.Value;

        if (!int.TryParse(
                businessIdValue,
                out var businessId))
        {
            return (
                false,
                "Business could not be identified.",
                null);
        }

        if (!int.TryParse(
                branchIdValue,
                out var branchId))
        {
            return (
                false,
                "Branch could not be identified.",
                null);
        }

        // --------------------------------------------------------
        // 4. Permission
        // --------------------------------------------------------

        var roleId =
            currentUser.RoleId;

        if (roleId <= 0)
        {
            return (
                false,
                "User role could not be identified.",
                null);
        }

        var hasPermission =
            await _permissionService.HasPermission(
                roleId,
                "VIEW_SALE");

        if (!hasPermission)
        {
            return (
                false,
                "You do not have permission to view sales.",
                null);
        }

        // --------------------------------------------------------
        // 5. Get sale
        // --------------------------------------------------------

        var sale =
            await _db.Sales
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.SaleId == saleId &&
                    x.BusinessId == businessId &&
                    x.BranchId == branchId);

        if (sale == null)
        {
            return (
                false,
                "Sale was not found.",
                null);
        }

        // --------------------------------------------------------
        // 6. Get sale lines
        // --------------------------------------------------------

        var saleLines =
            await _db.SaleLines
                .AsNoTracking()
                .Where(x =>
                    x.SaleId == saleId)
                .ToListAsync();

        // --------------------------------------------------------
        // 7. Get payments
        // --------------------------------------------------------

        var payments =
            await _db.SalePayments
                .AsNoTracking()
                .Where(x =>
                    x.SaleId == saleId)
                .ToListAsync();

        // --------------------------------------------------------
        // 8. Get products
        // --------------------------------------------------------

        var productIds =
            saleLines
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

        var products =
            await _db.Products
                .AsNoTracking()
                .Where(x =>
                    productIds.Contains(x.ProductId))
                .ToListAsync();

        // --------------------------------------------------------
        // 9. Get BUSINESS
        // --------------------------------------------------------
        //
        // IMPORTANT:
        // The sale already contains BusinessId = 1.
        // We use that BusinessId to retrieve the business.
        //
        // KRA_PIN is deliberately NOT selected/returned.
        // --------------------------------------------------------

        var business =
            await _db.Businesses
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == sale.BusinessId &&
                    x.IsActive)
                .Select(x => new
                {
                    BusinessId =
                        x.BusinessId,

                    BusinessName =
                        x.BusinessName,

                    Phone =
                        x.Phone,

                    Email =
                        x.Email,

                    Address =
                        x.Address,
                    PayBill =
                        x.PayBill,

    DevelopedBy =
        x.DevelopedBy
                })
                .FirstOrDefaultAsync();

        // --------------------------------------------------------
        // 10. Build response
        // --------------------------------------------------------
        //
        // Business is deliberately included as a top-level
        // property of the returned sale object.
        // --------------------------------------------------------

        var data =
            new
            {
                SaleId =
                    sale.SaleId,

                BusinessId =
                    sale.BusinessId,

                BranchId =
                    sale.BranchId,

                POSSessionId =
                    sale.POSSessionId,

                UserId =
                    sale.UserId,

                CustomerId =
                    sale.CustomerId,

                SaleNumber =
                    sale.SaleNumber,

                SaleDate =
                    sale.SaleDate,

                PriceGroupId =
                    sale.PriceGroupId,

                SubTotal =
                    sale.SubTotal,

                DiscountAmount =
                    sale.DiscountAmount,

                TaxAmount =
                    sale.TaxAmount,

                TotalAmount =
                    sale.TotalAmount,

                Status =
                    sale.Status,

                Notes =
                    sale.Notes,

                CreatedAt =
                    sale.CreatedAt,

                // ================================================
                // BUSINESS DETAILS
                // ================================================

                Business =
                    business,

                // ================================================
                // SALE LINES
                // ================================================

                Lines =
                    saleLines
                        .Select(line =>
                        {
                            var product =
                                products.FirstOrDefault(
                                    x =>
                                        x.ProductId ==
                                        line.ProductId);

                            return new
                            {
                                SaleLineId =
                                    line.SaleLineId,

                                SaleId =
                                    line.SaleId,

                                ProductId =
                                    line.ProductId,

                                ProductCode =
                                    product?.ProductCode,

                                ProductName =
                                    product?.ProductName,

                                ProductUnitId =
                                    line.ProductUnitId,

                                Quantity =
                                    line.Quantity,

                                UnitPrice =
                                    line.UnitPrice,

                                DiscountAmount =
                                    line.DiscountAmount,

                                TaxAmount =
                                    line.TaxAmount,

                                LineTotal =
                                    line.LineTotal
                            };
                        })
                        .ToList(),

                // ================================================
                // PAYMENTS
                // ================================================

                Payments =
                    payments
                        .Select(payment =>
                            new
                            {
                                SalePaymentId =
                                    payment.SalePaymentId,

                                SaleId =
                                    payment.SaleId,

                                PaymentMethod =
                                    payment.PaymentMethod,

                                Amount =
                                    payment.Amount,

                                ReferenceNumber =
                                    payment.ReferenceNumber,

                                PaymentStatus =
                                    payment.PaymentStatus,

                                TransactionDate =
                                    payment.TransactionDate,

                                Notes =
                                    payment.Notes,

                                AmountTendered =
                                    payment.AmountTendered,

                                ChangeAmount =
                                    payment.ChangeAmount,

                                MerchantRequestId =
                                    payment.MerchantRequestId,

                                CheckoutRequestId =
                                    payment.CheckoutRequestId,

                                MpesaReceiptNumber =
                                    payment.MpesaReceiptNumber,

                                MpesaTransactionDate =
                                    payment.MpesaTransactionDate,

                                MpesaPhoneNumber =
                                    payment.MpesaPhoneNumber,

                                MpesaResultCode =
                                    payment.MpesaResultCode,

                                MpesaResultDescription =
                                    payment.MpesaResultDescription
                            })
                        .ToList()
            };

        return (
            true,
            "Sale retrieved successfully.",
            data);
    }

    // ============================================================
    // GET SALES
    // ============================================================

    public async Task<(bool Success, string Message, object? Data)> GetSales(
        ClaimsPrincipal user)
    {
        // --------------------------------------------------------
        // 1. Get authenticated user
        // --------------------------------------------------------

        var userIdValue =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userIdValue))
        {
            userIdValue =
                user.FindFirst("sub")?.Value;
        }

        if (!int.TryParse(userIdValue, out var userId))
        {
            return (
                false,
                "Authenticated user could not be identified.",
                null);
        }

        // --------------------------------------------------------
        // 2. Get current user
        // --------------------------------------------------------

        var currentUser =
            await _db.Users
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId);

        if (currentUser == null)
        {
            return (
                false,
                "User was not found.",
                null);
        }

        // --------------------------------------------------------
        // 3. Business and Branch
        // --------------------------------------------------------

        var businessIdValue =
            user.FindFirst("BusinessId")?.Value;

        var branchIdValue =
            user.FindFirst("BranchId")?.Value;

        if (!int.TryParse(
                businessIdValue,
                out var businessId))
        {
            return (
                false,
                "Business could not be identified.",
                null);
        }

        if (!int.TryParse(
                branchIdValue,
                out var branchId))
        {
            return (
                false,
                "Branch could not be identified.",
                null);
        }

        // --------------------------------------------------------
        // 4. Permission
        // --------------------------------------------------------

        var roleId =
            currentUser.RoleId;

        if (roleId <= 0)
        {
            return (
                false,
                "User role could not be identified.",
                null);
        }

        var hasPermission =
            await _permissionService.HasPermission(
                roleId,
                "VIEW_SALE");

        if (!hasPermission)
        {
            return (
                false,
                "You do not have permission to view sales.",
                null);
        }

        // --------------------------------------------------------
        // 5. Get sales
        // --------------------------------------------------------

        var sales =
            await _db.Sales
                .AsNoTracking()
                .Where(x =>
                    x.BusinessId == businessId &&
                    x.BranchId == branchId)
                .OrderByDescending(x =>
                    x.SaleDate)
                .Select(x => new
                {
                    x.SaleId,
                    x.SaleNumber,
                    x.SaleDate,
                    x.POSSessionId,
                    x.UserId,
                    x.CustomerId,
                    x.PriceGroupId,
                    x.SubTotal,
                    x.DiscountAmount,
                    x.TaxAmount,
                    x.TotalAmount,
                    x.Status,
                    x.Notes,
                    x.CreatedAt
                })
                .ToListAsync();

        // --------------------------------------------------------
        // 6. Get payment information
        // --------------------------------------------------------

        var saleIds =
            sales
                .Select(x => x.SaleId)
                .ToList();

        var payments =
            await _db.SalePayments
                .AsNoTracking()
                .Where(x =>
                    saleIds.Contains(x.SaleId))
                .Select(x => new
                {
                    x.SalePaymentId,
                    x.SaleId,
                    x.PaymentMethod,
                    x.Amount,
                    x.PaymentStatus,
                    x.ReferenceNumber,
                    x.TransactionDate,
                    x.MpesaReceiptNumber,
                    x.MpesaPhoneNumber
                })
                .ToListAsync();

        // --------------------------------------------------------
        // 7. Build result
        // --------------------------------------------------------

        var result =
            sales.Select(sale =>
            {
                var salePayments =
                    payments
                        .Where(x =>
                            x.SaleId ==
                            sale.SaleId)
                        .ToList();

                var paymentMethods =
                    salePayments
                        .Select(x =>
                            x.PaymentMethod)
                        .Distinct()
                        .ToList();

                var paymentStatuses =
                    salePayments
                        .Select(x =>
                            x.PaymentStatus)
                        .Distinct()
                        .ToList();

                return new
                {
                    sale.SaleId,

                    sale.SaleNumber,

                    sale.SaleDate,

                    sale.POSSessionId,

                    sale.UserId,

                    sale.CustomerId,

                    sale.PriceGroupId,

                    sale.SubTotal,

                    sale.DiscountAmount,

                    sale.TaxAmount,

                    sale.TotalAmount,

                    sale.Status,

                    sale.Notes,

                    sale.CreatedAt,

                    PaymentMethod =
                        string.Join(
                            " + ",
                            paymentMethods),

                    PaymentStatus =
                        string.Join(
                            " / ",
                            paymentStatuses),

                    PaymentAmount =
                        salePayments.Sum(
                            x => x.Amount)
                };
            }).ToList();

        return (
            true,
            "Sales retrieved successfully.",
            result);
    }
}