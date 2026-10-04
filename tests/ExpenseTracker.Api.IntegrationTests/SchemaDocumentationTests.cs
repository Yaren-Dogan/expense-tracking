using ExpenseTracker.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseTracker.Api.IntegrationTests;

/// <summary>
/// Every table and column carries a Turkish description in the database (see SchemaComments), and the user guide's
/// database chapter is generated from them. A new column without a description fails here.
/// </summary>
[Collection(ApiCollection.Name)]
public class SchemaDocumentationTests(ApiFactory factory)
{
    [Fact]
    public async Task Every_table_and_column_has_a_description()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var undocumented = await db.Database.SqlQueryRaw<string>("""
            SELECT DISTINCT CASE WHEN obj_description(c.oid, 'pg_class') IS NULL THEN c.relname ELSE c.relname || '.' || a.attname END AS "Value"
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace AND n.nspname = 'public'
            LEFT JOIN pg_attribute a ON a.attrelid = c.oid AND a.attnum > 0 AND NOT a.attisdropped
            WHERE c.relkind = 'r'
              AND (obj_description(c.oid, 'pg_class') IS NULL OR col_description(c.oid, a.attnum) IS NULL)
            """).ToListAsync();

        undocumented.Should().BeEmpty("SchemaComments must describe every table and column");
    }
}
