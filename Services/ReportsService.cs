using System.Security.Claims;

using LiquorPOS.Api.Data;
using LiquorPOS.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Services;

public class ReportsService
{
    private readonly LiquorPosDbContext _db;
    private readonly PermissionService _permissionService;

    public ReportsService(
        LiquorPosDbContext db,
        PermissionService permissionService)
    {
        _db = db;
        _permissionService = permissionService;
    }

    // ============================================================
    // SALES REPORT
    // ============================================================

    public async Task<ReportResult> GetSalesReport(
        ClaimsPrincipal user,
        DateTime? fromDate,
        DateTime? toDate)
    {
        // --------------------------------------------------------
        // USER
        // --------------------------------------------------------

        var userIdClaim =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return ReportResult.Fail(
                "Invalid user authentication.");
        }

        // --------------------------------------------------------
        // ROLE
        // --------------------------------------------------------

        var roleName =
            user.FindFirst(ClaimTypes.Role)?.Value
            ?? user.FindFirst("role")?.Value;

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ReportResult.Fail(
                "Role information is missing from the token.");
        }

        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.RoleName == roleName);

        if (role == null)
        {
            return ReportResult.Fail(
                "User role could not be found.");
        }

        // --------------------------------------------------------
        // PERMISSION
        // --------------------------------------------------------

        var hasPermission =
            await _permissionService.HasPermission(
                role.RoleId,
                "VIEW_REPORTS");

        if (!hasPermission)
        {
            return ReportResult.Fail(
                "You do not have permission to view reports.");
        }

        // --------------------------------------------------------
        // BUSINESS
        // --------------------------------------------------------

        var businessIdClaim =
            user.FindFirst("BusinessId")?.Value;

        if (!int.TryParse(
                businessIdClaim,
                out var businessId))
        {
            return ReportResult.Fail(
                "Business information is missing from the token.");
        }

        // --------------------------------------------------------
        // BRANCH
        // --------------------------------------------------------

        var branchIdClaim =
            user.FindFirst("BranchId")?.Value;

        if (!int.TryParse(
                branchIdClaim,
                out var branchId))
        {
            return ReportResult.Fail(
                "Branch information is missing from the token.");
        }

        // --------------------------------------------------------
        // DATE VALIDATION
        // --------------------------------------------------------

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return ReportResult.Fail(
                "From date cannot be greater than to date.");
        }

        var startDate =
            fromDate?.Date ?? DateTime.MinValue.Date;

        var endDate =
            toDate?.Date.AddDays(1) ?? DateTime.MaxValue;

        // ========================================================
        // COMPLETED SALES
        // ========================================================

        var sales = await _db.Sales
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.BranchId == branchId &&
                x.Status.ToUpper() == "COMPLETED" &&
                x.SaleDate >= startDate &&
                x.SaleDate < endDate)
            .ToListAsync();

        // ========================================================
        // SALES SUMMARY
        // ========================================================

        var totalTransactions = sales.Count;

        var subtotal = sales.Sum(x => x.SubTotal);

        var discounts = sales.Sum(x => x.DiscountAmount);

        var tax = sales.Sum(x => x.TaxAmount);

        var netSales = sales.Sum(x => x.TotalAmount);

        // ========================================================
        // SALE LINES
        // ========================================================

        var saleIds = sales
            .Select(x => x.SaleId)
            .ToList();

        var saleLines = saleIds.Count == 0
            ? new List<SaleLine>()
            : await _db.SaleLines
                .AsNoTracking()
                .Where(x => saleIds.Contains(x.SaleId))
                .ToListAsync();

        var itemsSold =
            saleLines.Sum(x => x.Quantity);

        // ========================================================
        // PAYMENT SUMMARY
        // ========================================================

        var payments = saleIds.Count == 0
            ? new List<SalePayment>()
            : await _db.SalePayments
                .AsNoTracking()
                .Where(x =>
                    saleIds.Contains(x.SaleId) &&
                    x.PaymentStatus.ToUpper() == "PAID")
                .ToListAsync();

        var cashAmount = payments
            .Where(x =>
                x.PaymentMethod.Trim().ToUpper() == "CASH")
            .Sum(x => x.Amount);

        var mpesaAmount = payments
            .Where(x =>
                x.PaymentMethod.Trim().ToUpper() == "MPESA")
            .Sum(x => x.Amount);

        var cardAmount = payments
            .Where(x =>
                x.PaymentMethod.Trim().ToUpper() == "CARD")
            .Sum(x => x.Amount);

        var otherAmount = payments
            .Where(x =>
                x.PaymentMethod.Trim().ToUpper() != "CASH" &&
                x.PaymentMethod.Trim().ToUpper() != "MPESA" &&
                x.PaymentMethod.Trim().ToUpper() != "CARD")
            .Sum(x => x.Amount);

        // ========================================================
        // TOP PRODUCTS
        // ========================================================

        var productIds = saleLines
            .Select(x => x.ProductId)
            .Distinct()
            .ToList();

        var products = productIds.Count == 0
            ? new List<Product>()
            : await _db.Products
                .AsNoTracking()
                .Where(x =>
                    productIds.Contains(x.ProductId))
                .ToListAsync();

        var productLookup = products
            .ToDictionary(x => x.ProductId);

        var topProducts = saleLines
            .GroupBy(x => x.ProductId)
            .Select(group =>
            {
                var productName =
                    productLookup.TryGetValue(
                        group.Key,
                        out var product)
                        ? product.ProductName
                        : $"Product #{group.Key}";

                return new TopProductReport
                {
                    ProductId = group.Key,
                    ProductName = productName,
                    Quantity = group.Sum(x => x.Quantity),
                    SalesAmount = group.Sum(x => x.LineTotal)
                };
            })
            .OrderByDescending(x => x.SalesAmount)
            .Take(10)
            .ToList();

        // ========================================================
        // CASHIER PERFORMANCE
        // ========================================================

        var cashierIds = sales
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var users = cashierIds.Count == 0
            ? new List<User>()
            : await _db.Users
                .AsNoTracking()
                .Where(x =>
                    cashierIds.Contains(x.UserId))
                .ToListAsync();

        var userLookup = users
            .ToDictionary(x => x.UserId);

        var cashierPerformance = sales
            .GroupBy(x => x.UserId)
            .Select(group =>
            {
                var cashierName =
                    userLookup.TryGetValue(
                        group.Key,
                        out var cashier)
                        ? cashier.FullName
                        : $"User #{group.Key}";

                return new CashierReport
                {
                    UserId = group.Key,
                    CashierName = cashierName,
                    Transactions = group.Count(),
                    SalesAmount = group.Sum(x => x.TotalAmount)
                };
            })
            .OrderByDescending(x => x.SalesAmount)
            .ToList();

        // ========================================================
        // RESPONSE
        // ========================================================

        var report = new SalesReportResponse
        {
            FromDate = fromDate?.Date,
            ToDate = toDate?.Date,

            Summary = new SalesSummaryReport
            {
                TotalTransactions = totalTransactions,
                ItemsSold = itemsSold,
                Subtotal = subtotal,
                Discounts = discounts,
                Tax = tax,
                NetSales = netSales
            },

            Payments = new PaymentSummaryReport
            {
                Cash = cashAmount,
                Mpesa = mpesaAmount,
                Card = cardAmount,
                Other = otherAmount,
                TotalPaid = payments.Sum(x => x.Amount)
            },

            TopProducts = topProducts,

            Cashiers = cashierPerformance
        };

        return ReportResult.SuccessResult(report);
    }


    // ============================================================
    // POS SESSION REPORT
    // ============================================================

    public async Task<ReportResult> GetPOSSessionsReport(
        ClaimsPrincipal user,
        DateTime? fromDate,
        DateTime? toDate)
    {
        // --------------------------------------------------------
        // USER
        // --------------------------------------------------------

        var userIdClaim =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!int.TryParse(userIdClaim, out var userId))
        {
            return ReportResult.Fail(
                "Invalid user authentication.");
        }

        // --------------------------------------------------------
        // ROLE
        // --------------------------------------------------------

        var roleName =
            user.FindFirst(ClaimTypes.Role)?.Value
            ?? user.FindFirst("role")?.Value;

        if (string.IsNullOrWhiteSpace(roleName))
        {
            return ReportResult.Fail(
                "Role information is missing from the token.");
        }

        var role = await _db.Roles
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.RoleName == roleName);

        if (role == null)
        {
            return ReportResult.Fail(
                "User role could not be found.");
        }

        // --------------------------------------------------------
        // PERMISSION
        // --------------------------------------------------------

        var hasPermission =
            await _permissionService.HasPermission(
                role.RoleId,
                "VIEW_REPORTS");

        if (!hasPermission)
        {
            return ReportResult.Fail(
                "You do not have permission to view reports.");
        }

        // --------------------------------------------------------
        // BUSINESS
        // --------------------------------------------------------

        var businessIdClaim =
            user.FindFirst("BusinessId")?.Value;

        if (!int.TryParse(
                businessIdClaim,
                out var businessId))
        {
            return ReportResult.Fail(
                "Business information is missing from the token.");
        }

        // --------------------------------------------------------
        // BRANCH
        // --------------------------------------------------------

        var branchIdClaim =
            user.FindFirst("BranchId")?.Value;

        if (!int.TryParse(
                branchIdClaim,
                out var branchId))
        {
            return ReportResult.Fail(
                "Branch information is missing from the token.");
        }

        // --------------------------------------------------------
        // DATE VALIDATION
        // --------------------------------------------------------

        if (fromDate.HasValue &&
            toDate.HasValue &&
            fromDate.Value.Date > toDate.Value.Date)
        {
            return ReportResult.Fail(
                "From date cannot be greater than to date.");
        }

        var startDate =
            fromDate?.Date ?? DateTime.MinValue.Date;

        var endDate =
            toDate?.Date.AddDays(1) ?? DateTime.MaxValue;

        // ========================================================
        // GET POS SESSIONS
        // ========================================================

        var sessions = await _db.POSSessions
            .AsNoTracking()
            .Join(
                _db.POSTerminals.AsNoTracking(),
                session => session.POSTerminalId,
                terminal => terminal.POSTerminalId,
                (session, terminal) => new
                {
                    Session = session,
                    Terminal = terminal
                })
            .Where(x =>
                x.Terminal.BusinessId == businessId &&
                x.Terminal.BranchId == branchId &&
                x.Session.OpenedAt >= startDate &&
                x.Session.OpenedAt < endDate)
            .Select(x => x.Session)
            .OrderByDescending(x => x.OpenedAt)
            .ToListAsync();

        // ========================================================
        // GET USERS
        // ========================================================

        var sessionUserIds = sessions
            .Select(x => x.UserId)
            .Distinct()
            .ToList();

        var sessionUsers = sessionUserIds.Count == 0
            ? new List<User>()
            : await _db.Users
                .AsNoTracking()
                .Where(x =>
                    sessionUserIds.Contains(x.UserId))
                .ToListAsync();

        var sessionUserLookup =
            sessionUsers.ToDictionary(x => x.UserId);

        // ========================================================
        // GET SALES FOR SESSIONS
        // ========================================================

        var sessionIds = sessions
            .Select(x => x.POSSessionId)
            .ToList();

        var sessionSales = sessionIds.Count == 0
            ? new List<Sale>()
            : await _db.Sales
                .AsNoTracking()
                .Where(x =>
                    sessionIds.Contains(x.POSSessionId) &&
                    x.BusinessId == businessId &&
                    x.BranchId == branchId &&
                    x.Status.ToUpper() == "COMPLETED")
                .ToListAsync();

        // ========================================================
        // GET PAYMENTS
        // ========================================================

        var sessionSaleIds = sessionSales
            .Select(x => x.SaleId)
            .ToList();

        var sessionPayments = sessionSaleIds.Count == 0
            ? new List<SalePayment>()
            : await _db.SalePayments
                .AsNoTracking()
                .Where(x =>
                    sessionSaleIds.Contains(x.SaleId) &&
                    x.PaymentStatus.ToUpper() == "PAID")
                .ToListAsync();

        // ========================================================
        // BUILD SESSION REPORT
        // ========================================================

        var sessionReports =
            sessions.Select(session =>
            {
                var currentSessionSales =
                    sessionSales
                        .Where(x =>
                            x.POSSessionId ==
                            session.POSSessionId)
                        .ToList();

                var currentSaleIds =
                    currentSessionSales
                        .Select(x => x.SaleId)
                        .ToHashSet();

                var currentPayments =
                    sessionPayments
                        .Where(x =>
                            currentSaleIds.Contains(
                                x.SaleId))
                        .ToList();

                var cashSales =
                    currentPayments
                        .Where(x =>
                            x.PaymentMethod
                                .Trim()
                                .ToUpper() == "CASH")
                        .Sum(x => x.Amount);

                var mpesaSales =
                    currentPayments
                        .Where(x =>
                            x.PaymentMethod
                                .Trim()
                                .ToUpper() == "MPESA")
                        .Sum(x => x.Amount);

                var otherPayments =
                    currentPayments
                        .Where(x =>
                            x.PaymentMethod
                                .Trim()
                                .ToUpper() != "CASH"
                            &&
                            x.PaymentMethod
                                .Trim()
                                .ToUpper() != "MPESA")
                        .Sum(x => x.Amount);

                var cashierName =
                    sessionUserLookup.TryGetValue(
                        session.UserId,
                        out var cashier)
                        ? cashier.FullName
                        : $"User #{session.UserId}";

                // ------------------------------------------------
                // IMPORTANT:
                // Use the stored ExpectedCash and
                // CashDifference for closed sessions.
                //
                // For an open session, calculate the current
                // expected cash from opening cash + cash sales.
                // ------------------------------------------------

                var expectedCash =
                    session.ExpectedCash
                    ?? (
                        session.OpeningCash +
                        cashSales
                    );

                var cashDifference =
                    session.CashDifference;

                // ------------------------------------------------
                // If the session has been closed but for some
                // reason CashDifference was not stored, calculate
                // it from ActualCash - ExpectedCash.
                // ------------------------------------------------

                if (!cashDifference.HasValue &&
                    session.ActualCash.HasValue)
                {
                    cashDifference =
                        session.ActualCash.Value -
                        expectedCash;
                }

                return new POSSessionReportItem
                {
                    POSSessionId =
                        session.POSSessionId,

                    POSTerminalId =
                        session.POSTerminalId,

                    UserId =
                        session.UserId,

                    CashierName =
                        cashierName,

                    OpenedAt =
                        session.OpenedAt,

                    ClosedAt =
                        session.ClosedAt,

                    OpeningCash =
                        session.OpeningCash,

                    CashSales =
                        cashSales,

                    MpesaSales =
                        mpesaSales,

                    OtherPayments =
                        otherPayments,

                    TotalSales =
                        currentSessionSales
                            .Sum(x => x.TotalAmount),

                    ExpectedCash =
                        expectedCash,

                    ActualCash =
                        session.ActualCash,

                    CashDifference =
                        cashDifference,

                    Status =
                        session.Status,

                    Notes =
                        session.Notes,

                    NumberOfSales =
                        currentSessionSales.Count
                };
            })
            .ToList();

        // ========================================================
        // SESSION SUMMARY
        // ========================================================

        var sessionSummary =
            new POSSessionReportSummary
            {
                TotalSessions =
                    sessionReports.Count,

                OpenSessions =
                    sessionReports.Count(x =>
                        x.Status.Trim().ToUpper() == "OPEN"),

                ClosedSessions =
                    sessionReports.Count(x =>
                        x.Status.Trim().ToUpper() == "CLOSED"),

                TotalOpeningCash =
                    sessionReports.Sum(
                        x => x.OpeningCash),

                TotalCashSales =
                    sessionReports.Sum(
                        x => x.CashSales),

                TotalExpectedCash =
                    sessionReports.Sum(
                        x => x.ExpectedCash),

                TotalActualCash =
                    sessionReports
                        .Where(x =>
                            x.ActualCash.HasValue)
                        .Sum(x =>
                            x.ActualCash!.Value),

                TotalCashDifference =
                    sessionReports
                        .Where(x =>
                            x.CashDifference.HasValue)
                        .Sum(x =>
                            x.CashDifference!.Value)
            };

        // ========================================================
        // RESPONSE
        // ========================================================

        var report =
            new POSSessionReportResponse
            {
                FromDate =
                    fromDate?.Date,

                ToDate =
                    toDate?.Date,

                Summary =
                    sessionSummary,

                Sessions =
                    sessionReports
            };

        return ReportResult.SuccessResult(report);
    }
}


// ================================================================
// REPORT RESULT
// ================================================================

public class ReportResult
{
    public bool Success { get; set; }

    public string Message { get; set; } = string.Empty;

    public object? Data { get; set; }

    public static ReportResult Fail(string message)
    {
        return new ReportResult
        {
            Success = false,
            Message = message
        };
    }

    public static ReportResult SuccessResult(object data)
    {
        return new ReportResult
        {
            Success = true,
            Message = "Report generated successfully.",
            Data = data
        };
    }
}


// ================================================================
// SALES REPORT RESPONSE
// ================================================================

public class SalesReportResponse
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public SalesSummaryReport Summary { get; set; } = new();

    public PaymentSummaryReport Payments { get; set; } = new();

    public List<TopProductReport> TopProducts { get; set; } = new();

    public List<CashierReport> Cashiers { get; set; } = new();
}


// ================================================================
// SALES SUMMARY
// ================================================================

public class SalesSummaryReport
{
    public int TotalTransactions { get; set; }

    public decimal ItemsSold { get; set; }

    public decimal Subtotal { get; set; }

    public decimal Discounts { get; set; }

    public decimal Tax { get; set; }

    public decimal NetSales { get; set; }
}


// ================================================================
// PAYMENT SUMMARY
// ================================================================

public class PaymentSummaryReport
{
    public decimal Cash { get; set; }

    public decimal Mpesa { get; set; }

    public decimal Card { get; set; }

    public decimal Other { get; set; }

    public decimal TotalPaid { get; set; }
}


// ================================================================
// TOP PRODUCTS
// ================================================================

public class TopProductReport
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    public decimal SalesAmount { get; set; }
}


// ================================================================
// CASHIER REPORT
// ================================================================

public class CashierReport
{
    public int UserId { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public int Transactions { get; set; }

    public decimal SalesAmount { get; set; }
}


// ================================================================
// POS SESSION REPORT RESPONSE
// ================================================================

public class POSSessionReportResponse
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public POSSessionReportSummary Summary { get; set; } = new();

    public List<POSSessionReportItem> Sessions { get; set; } = new();
}


// ================================================================
// POS SESSION REPORT SUMMARY
// ================================================================

public class POSSessionReportSummary
{
    public int TotalSessions { get; set; }

    public int OpenSessions { get; set; }

    public int ClosedSessions { get; set; }

    public decimal TotalOpeningCash { get; set; }

    public decimal TotalCashSales { get; set; }

    public decimal TotalExpectedCash { get; set; }

    public decimal TotalActualCash { get; set; }

    public decimal TotalCashDifference { get; set; }
}


// ================================================================
// POS SESSION REPORT ITEM
// ================================================================

public class POSSessionReportItem
{
    public long POSSessionId { get; set; }

    public int POSTerminalId { get; set; }

    public int UserId { get; set; }

    public string CashierName { get; set; } = string.Empty;

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    public decimal OpeningCash { get; set; }

    public decimal CashSales { get; set; }

    public decimal MpesaSales { get; set; }

    public decimal OtherPayments { get; set; }

    public decimal TotalSales { get; set; }

    public decimal ExpectedCash { get; set; }

    public decimal? ActualCash { get; set; }

    public decimal? CashDifference { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public int NumberOfSales { get; set; }
}

