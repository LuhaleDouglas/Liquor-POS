using System.Security.Claims;

using LiquorPOS.Api.Data;
using LiquorPOS.Api.DTOs;
using LiquorPOS.Api.Models;

using Microsoft.EntityFrameworkCore;

namespace LiquorPOS.Api.Services;

public class POSSessionService
{
    private readonly LiquorPosDbContext _db;

    public POSSessionService(LiquorPosDbContext db)
    {
        _db = db;
    }


    // ============================================================
    // OPEN POS SESSION
    // ============================================================

    public async Task<(bool Success, string Message, long? SessionId)>
        OpenSession(
            OpenPOSSessionRequest request,
            ClaimsPrincipal user)
    {
        // --------------------------------------------------------
        // 1. Get authenticated UserId
        // --------------------------------------------------------

        var userIdValue =
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        if (!int.TryParse(
                userIdValue,
                out var userId))
        {
            return (
                false,
                "User could not be identified.",
                null);
        }


        // --------------------------------------------------------
        // 2. Get BusinessId
        // --------------------------------------------------------

        var businessIdValue =
            user.FindFirst("BusinessId")?.Value;

        if (!int.TryParse(
                businessIdValue,
                out var businessId))
        {
            return (
                false,
                "Business could not be identified.",
                null);
        }


        // --------------------------------------------------------
        // 3. Get BranchId
        // --------------------------------------------------------

        var branchIdValue =
            user.FindFirst("BranchId")?.Value;

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
        // 4. Validate opening cash
        // --------------------------------------------------------

        if (request.OpeningCash < 0)
        {
            return (
                false,
                "Opening cash cannot be negative.",
                null);
        }


        // --------------------------------------------------------
        // 5. Validate terminal
        // --------------------------------------------------------

        var terminal = await _db.POSTerminals
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.POSTerminalId ==
                    request.POSTerminalId
                &&
                x.BusinessId == businessId
                &&
                x.BranchId == branchId
                &&
                x.IsActive);

        if (terminal == null)
        {
            return (
                false,
                "POS terminal was not found or is inactive.",
                null);
        }


        // --------------------------------------------------------
        // 6. Check if user already has an open session
        // --------------------------------------------------------

        var existingSession =
            await _db.POSSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.UserId == userId
                    &&
                    x.POSTerminalId ==
                        request.POSTerminalId
                    &&
                    x.Status == "OPEN");

        if (existingSession != null)
        {
            return (
                false,
                "You already have an open POS session on this terminal.",
                existingSession.POSSessionId);
        }


        // --------------------------------------------------------
        // 7. Create session
        // --------------------------------------------------------

        var session = new POSSession
        {
            POSTerminalId =
                request.POSTerminalId,

            UserId =
                userId,

            OpenedAt =
                DateTime.Now,

            OpeningCash =
                request.OpeningCash,

            ClosedAt =
                null,

            ExpectedCash =
                request.OpeningCash,

            ActualCash =
                null,

            Status =
                "OPEN",

            Notes =
                request.Notes
        };


        _db.POSSessions.Add(session);

        await _db.SaveChangesAsync();


        return (
            true,
            "POS session opened successfully.",
            session.POSSessionId);
    }
    public async Task<(bool Success, string Message, POSSessionSummaryDto? Data)>
    GetSessionSummary(
        long sessionId,
        ClaimsPrincipal user)
{
    // ------------------------------------------------------------
    // 1. Get BusinessId
    // ------------------------------------------------------------

    var businessIdValue =
        user.FindFirst("BusinessId")?.Value;

    if (!int.TryParse(
            businessIdValue,
            out var businessId))
    {
        return (
            false,
            "Business could not be identified.",
            null);
    }


    // ------------------------------------------------------------
    // 2. Get BranchId
    // ------------------------------------------------------------

    var branchIdValue =
        user.FindFirst("BranchId")?.Value;

    if (!int.TryParse(
            branchIdValue,
            out var branchId))
    {
        return (
            false,
            "Branch could not be identified.",
            null);
    }


    // ------------------------------------------------------------
    // 3. Find session
    // ------------------------------------------------------------

    var session = await _db.POSSessions
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
            x.Session.POSSessionId == sessionId
            &&
            x.Terminal.BusinessId == businessId
            &&
            x.Terminal.BranchId == branchId)
        .Select(x => x.Session)
        .FirstOrDefaultAsync();

    if (session == null)
    {
        return (
            false,
            "POS session not found.",
            null);
    }


    // ------------------------------------------------------------
    // 4. Get sales for this session
    // ------------------------------------------------------------

    var sales = await _db.Sales
        .AsNoTracking()
        .Where(x =>
            x.POSSessionId == sessionId
            &&
            x.BusinessId == businessId
            &&
            x.BranchId == branchId
            &&
            x.Status == "COMPLETED")
        .ToListAsync();


    // ------------------------------------------------------------
    // 5. Calculate sales totals
    // ------------------------------------------------------------

    var numberOfSales =
        sales.Count;

    var totalSales =
        sales.Sum(x => x.TotalAmount);


    // ------------------------------------------------------------
    // 6. Get payments
    // ------------------------------------------------------------

    var payments = await _db.SalePayments
        .AsNoTracking()
        .Where(payment =>
            sales.Select(s => s.SaleId)
                .Contains(payment.SaleId)
            &&
            payment.PaymentStatus == "PAID")
        .ToListAsync();


    // ------------------------------------------------------------
    // 7. Calculate payment totals
    // ------------------------------------------------------------

    var cashSales =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() == "CASH")
            .Sum(x => x.Amount);

    var mpesaSales =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() == "MPESA")
            .Sum(x => x.Amount);

    var otherPayments =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() != "CASH"
                &&
                x.PaymentMethod.ToUpper() != "MPESA")
            .Sum(x => x.Amount);


    // ------------------------------------------------------------
    // 8. Calculate expected cash
    // ------------------------------------------------------------

    var expectedCash =
        session.OpeningCash
        +
        cashSales
        -
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() == "CASH")
            .Sum(x => x.ChangeAmount ?? 0);


    // ------------------------------------------------------------
    // 9. Build summary
    // ------------------------------------------------------------

    var summary = new POSSessionSummaryDto
    {
        POSSessionId =
            session.POSSessionId,

        POSTerminalId =
            session.POSTerminalId,

        UserId =
            session.UserId,

        OpenedAt =
            session.OpenedAt,

        OpeningCash =
            session.OpeningCash,

        ClosedAt =
            session.ClosedAt,

        ExpectedCash =
            expectedCash,

        ActualCash =
            session.ActualCash,

        CashDifference =
            session.ActualCash.HasValue
                ? session.ActualCash.Value - expectedCash
                : null,

        Status =
            session.Status,

        Notes =
            session.Notes,

        NumberOfSales =
            numberOfSales,

        TotalSales =
            totalSales,

        CashSales =
            cashSales,

        MpesaSales =
            mpesaSales,

        OtherPayments =
            otherPayments
    };


    return (
        true,
        "POS session summary retrieved successfully.",
        summary);
}
// ============================================================
// CLOSE POS SESSION
// ============================================================

public async Task<(bool Success, string Message, POSSessionSummaryDto? Data)>
    CloseSession(
        long sessionId,
        ClosePOSSessionRequest request,
        ClaimsPrincipal user)
{
    // --------------------------------------------------------
    // 1. Get BusinessId
    // --------------------------------------------------------

    var businessIdValue =
        user.FindFirst("BusinessId")?.Value;

    if (!int.TryParse(
            businessIdValue,
            out var businessId))
    {
        return (
            false,
            "Business could not be identified.",
            null);
    }


    // --------------------------------------------------------
    // 2. Get BranchId
    // --------------------------------------------------------

    var branchIdValue =
        user.FindFirst("BranchId")?.Value;

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
    // 3. Validate actual cash
    // --------------------------------------------------------

    if (request.ActualCash < 0)
    {
        return (
            false,
            "Actual cash cannot be negative.",
            null);
    }


    // --------------------------------------------------------
    // 4. Find session
    // --------------------------------------------------------

    var session = await _db.POSSessions
        .Join(
            _db.POSTerminals,
            session => session.POSTerminalId,
            terminal => terminal.POSTerminalId,
            (session, terminal) => new
            {
                Session = session,
                Terminal = terminal
            })
        .Where(x =>
            x.Session.POSSessionId == sessionId
            &&
            x.Terminal.BusinessId == businessId
            &&
            x.Terminal.BranchId == branchId)
        .Select(x => x.Session)
        .FirstOrDefaultAsync();

    if (session == null)
    {
        return (
            false,
            "POS session not found.",
            null);
    }


    // --------------------------------------------------------
    // 5. Make sure session is still open
    // --------------------------------------------------------

    if (session.Status != "OPEN")
    {
        return (
            false,
            "POS session is already closed.",
            null);
    }


    // --------------------------------------------------------
    // 6. Get completed sales
    // --------------------------------------------------------

    var sales = await _db.Sales
        .AsNoTracking()
        .Where(x =>
            x.POSSessionId == sessionId
            &&
            x.BusinessId == businessId
            &&
            x.BranchId == branchId
            &&
            x.Status == "COMPLETED")
        .ToListAsync();


    // --------------------------------------------------------
    // 7. Get payments
    // --------------------------------------------------------

    var saleIds =
        sales.Select(x => x.SaleId).ToList();

    var payments = await _db.SalePayments
        .AsNoTracking()
        .Where(x =>
            saleIds.Contains(x.SaleId)
            &&
            x.PaymentStatus == "PAID")
        .ToListAsync();


    // --------------------------------------------------------
    // 8. Calculate payment totals
    // --------------------------------------------------------

    var cashSales =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() == "CASH")
            .Sum(x => x.Amount);

    var mpesaSales =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() == "MPESA")
            .Sum(x => x.Amount);

    var otherPayments =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() != "CASH"
                &&
                x.PaymentMethod.ToUpper() != "MPESA")
            .Sum(x => x.Amount);


    // --------------------------------------------------------
  // 8. Calculate expected cash
// ------------------------------------------------------------

var expectedCash =
    session.OpeningCash
    +
    cashSales;

   // --------------------------------------------------------
// 10. Calculate difference
// --------------------------------------------------------

var cashDifference =
    request.ActualCash - expectedCash;


    // --------------------------------------------------------
    // 11. Close session
    // --------------------------------------------------------

    session.ExpectedCash =
        expectedCash;

    session.ActualCash =
        request.ActualCash;

    session.CashDifference =
        cashDifference;

    session.ClosedAt =
        DateTime.Now;

    session.Status =
        "CLOSED";

    if (!string.IsNullOrWhiteSpace(request.Notes))
    {
        session.Notes =
            request.Notes;
    }


    await _db.SaveChangesAsync();


    // --------------------------------------------------------
    // 12. Build closing summary
    // --------------------------------------------------------

    var summary = new POSSessionSummaryDto
    {
        POSSessionId =
            session.POSSessionId,

        POSTerminalId =
            session.POSTerminalId,

        UserId =
            session.UserId,

        OpenedAt =
            session.OpenedAt,

        OpeningCash =
            session.OpeningCash,

        ClosedAt =
            session.ClosedAt,

        ExpectedCash =
            expectedCash,

        ActualCash =
            request.ActualCash,

        CashDifference =
            cashDifference,

        Status =
            session.Status,

        Notes =
            session.Notes,

        NumberOfSales =
            sales.Count,

        TotalSales =
            sales.Sum(x => x.TotalAmount),

        CashSales =
            cashSales,

        MpesaSales =
            mpesaSales,

        OtherPayments =
            otherPayments
    };


    return (
        true,
        "POS session closed successfully.",
        summary);
}



// ============================================================
// GET CURRENT OPEN POS SESSION
// ============================================================

public async Task<(bool Success, string Message, POSSessionSummaryDto? Data)>
    GetCurrentOpenSession(
        ClaimsPrincipal user,
        int posTerminalId)
{
    // --------------------------------------------------------
    // 1. Get authenticated UserId
    // --------------------------------------------------------

    var userIdValue =
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value;

    if (!int.TryParse(
            userIdValue,
            out var userId))
    {
        return (
            false,
            "User could not be identified.",
            null);
    }


    // --------------------------------------------------------
    // 2. Get BusinessId
    // --------------------------------------------------------

    var businessIdValue =
        user.FindFirst("BusinessId")?.Value;

    if (!int.TryParse(
            businessIdValue,
            out var businessId))
    {
        return (
            false,
            "Business could not be identified.",
            null);
    }


    // --------------------------------------------------------
    // 3. Get BranchId
    // --------------------------------------------------------

    var branchIdValue =
        user.FindFirst("BranchId")?.Value;

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
    // 4. Find current OPEN session
    // --------------------------------------------------------

    var session =
        await _db.POSSessions
            .AsNoTracking()
            .Join(
                _db.POSTerminals.AsNoTracking(),
                session =>
                    session.POSTerminalId,
                terminal =>
                    terminal.POSTerminalId,
                (session, terminal) => new
                {
                    Session = session,
                    Terminal = terminal
                })
            .Where(x =>
                x.Session.UserId == userId
                &&
                x.Session.POSTerminalId == posTerminalId
                &&
                x.Session.Status == "OPEN"
                &&
                x.Terminal.BusinessId == businessId
                &&
                x.Terminal.BranchId == branchId
                &&
                x.Terminal.IsActive)
            .Select(x => x.Session)
            .FirstOrDefaultAsync();

    if (session == null)
    {
        return (
            true,
            "No open POS session found.",
            null);
    }


    // --------------------------------------------------------
    // 5. Get completed sales
    // --------------------------------------------------------

    var sales =
        await _db.Sales
            .AsNoTracking()
            .Where(x =>
                x.POSSessionId ==
                    session.POSSessionId
                &&
                x.BusinessId == businessId
                &&
                x.BranchId == branchId
                &&
                x.Status == "COMPLETED")
            .ToListAsync();


    // --------------------------------------------------------
    // 6. Get paid payments
    // --------------------------------------------------------

    var saleIds =
        sales
            .Select(x => x.SaleId)
            .ToList();

    var payments =
        await _db.SalePayments
            .AsNoTracking()
            .Where(x =>
                saleIds.Contains(x.SaleId)
                &&
                x.PaymentStatus == "PAID")
            .ToListAsync();


    // --------------------------------------------------------
    // 7. Calculate payment totals
    // --------------------------------------------------------

    var cashSales =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() ==
                    "CASH")
            .Sum(x => x.Amount);

    var mpesaSales =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() ==
                    "MPESA")
            .Sum(x => x.Amount);

    var otherPayments =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() !=
                    "CASH"
                &&
                x.PaymentMethod.ToUpper() !=
                    "MPESA")
            .Sum(x => x.Amount);


    // --------------------------------------------------------
    // 8. Calculate expected cash
    // --------------------------------------------------------

    var cashChange =
        payments
            .Where(x =>
                x.PaymentMethod.ToUpper() ==
                    "CASH")
            .Sum(x =>
                x.ChangeAmount ?? 0);

    var expectedCash =
        session.OpeningCash
        +
        cashSales
        -
        cashChange;


    // --------------------------------------------------------
    // 9. Build summary
    // --------------------------------------------------------

    var summary =
        new POSSessionSummaryDto
        {
            POSSessionId =
                session.POSSessionId,

            POSTerminalId =
                session.POSTerminalId,

            UserId =
                session.UserId,

            OpenedAt =
                session.OpenedAt,

            OpeningCash =
                session.OpeningCash,

            ClosedAt =
                session.ClosedAt,

            ExpectedCash =
                expectedCash,

            ActualCash =
                session.ActualCash,

            CashDifference =
                session.ActualCash.HasValue
                    ? session.ActualCash.Value -
                      expectedCash
                    : null,

            Status =
                session.Status,

            Notes =
                session.Notes,

            NumberOfSales =
                sales.Count,

            TotalSales =
                sales.Sum(x => x.TotalAmount),

            CashSales =
                cashSales,

            MpesaSales =
                mpesaSales,

            OtherPayments =
                otherPayments
        };


    return (
        true,
        "Current POS session retrieved successfully.",
        summary);
}

}