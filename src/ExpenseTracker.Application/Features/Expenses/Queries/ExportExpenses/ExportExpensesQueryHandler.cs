using System.Globalization;
using System.Text;
using ExpenseTracker.Application.Common.Ai;
using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Domain.ValueObjects;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ExportExpenses;

/// <summary>
/// Writes the CSV the way Turkish Excel reads it without an import wizard: UTF-8 with a byte order mark (or "ğ, ş" come
/// out garbled), ";" between fields and "," as the decimal separator. Oldest first, like a statement.
/// </summary>
public sealed class ExportExpensesQueryHandler(IAppDbContext db, IUserContext user, IUserDirectory users)
    : IRequestHandler<ExportExpensesQuery, ExpenseExport>
{
    private const char Separator = ';';

    public async Task<ExpenseExport> Handle(ExportExpensesQuery request, CancellationToken cancellationToken)
    {
        BudgetMonth.TryParse(request.Month, out var month);
        var (from, to, ledgerId) = (month.FirstDay, month.LastDay, user.LedgerId);

        var expenses = await db.Expenses
            .AsNoTracking()
            .Where(e => e.LedgerId == ledgerId && e.SpentOn >= from && e.SpentOn <= to)
            .OrderBy(e => e.SpentOn)
            .ThenBy(e => e.CreatedAt)
            .Select(e => new { e.SpentOn, e.Category, e.Amount, e.Note, e.UserId })
            .ToListAsync(cancellationToken);
        var names = await users.GetDisplayNamesAsync(expenses.Select(e => e.UserId).Distinct(), cancellationToken);

        var csv = new StringBuilder();
        AppendRow(csv, "Tarih", "Kategori", "Tutar (TL)", "Not", "Giren");
        foreach (var e in expenses)
            AppendRow(csv,
                e.SpentOn.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
                CategoryNames.Turkish(e.Category),
                Amount(e.Amount),
                e.Note ?? "",
                names.GetValueOrDefault(e.UserId) ?? "Eski üye");

        var content = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return new ExpenseExport($"harcamalar-{month}.csv", content);
    }

    /// <summary>Formatted by hand: the API container may have no Turkish culture data.</summary>
    private static string Amount(decimal amount) => amount.ToString("0.00", CultureInfo.InvariantCulture).Replace('.', ',');

    private static void AppendRow(StringBuilder csv, params string[] fields)
    {
        csv.AppendJoin(Separator, fields.Select(Field));
        csv.Append("\r\n");
    }

    /// <summary>
    /// Notes are typed by household members and the file is opened in a spreadsheet, so a note starting with "=", "+",
    /// "-" or "@" would run as a formula there (CSV injection). Such a field gets a leading apostrophe; fields with a
    /// separator, quote or line break are quoted.
    /// </summary>
    internal static string Field(string value)
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0]))
            value = "'" + value;
        return value.IndexOfAny([Separator, '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
    }
}
