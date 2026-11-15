using System.Globalization;

namespace Reconcile.Net.Tests.Support;

public sealed record LedgerRow(
    string Reference,
    string CustomerId,
    decimal Amount,
    DateTime PostedAt,
    string PayoutId = "");

public sealed record StatementRow(
    string Reference,
    string AccountRef,
    decimal Amount,
    DateTime ValueDate);

public static class Rows
{
    private const string DefaultCustomerId = "C1";
    private const string DefaultDate = "2026-07-01";

    public static LedgerRow Ledger(
        string reference,
        decimal amount,
        string customerId = DefaultCustomerId,
        string postedAt = DefaultDate,
        string payoutId = "")
        => new(reference, customerId, amount, ParseDate(postedAt), payoutId);

    public static StatementRow Statement(
        string reference,
        decimal amount,
        string accountRef = DefaultCustomerId,
        string valueDate = DefaultDate)
        => new(reference, accountRef, amount, ParseDate(valueDate));

    public static DateTime ParseDate(string value)
        => DateTime.Parse(value, CultureInfo.InvariantCulture);
}
