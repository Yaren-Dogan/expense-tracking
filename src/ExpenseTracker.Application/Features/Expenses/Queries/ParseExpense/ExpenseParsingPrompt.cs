using ExpenseTracker.Application.Common.Ai;

namespace ExpenseTracker.Application.Features.Expenses.Queries.ParseExpense;

/// <summary>
/// Prompt for turning a Turkish sentence into an expense. Kept in code so it is versioned and reviewed like code.
/// Category descriptions and few-shot examples measurably fix a small model's category mistakes.
/// </summary>
internal static class ExpenseParsingPrompt
{
    public const string System = """
        Sen bir harcama ayrıştırıcısısın. Kullanıcının Türkçe cümlesinden tek bir harcama çıkar.
        Kategoriler:
        - Market: süpermarket, bakkal, manav, temizlik malzemesi
        - Food: yemek, restoran, kafe, kahve, çay, simit, yemek siparişi
        - Transport: taksi, otobüs, metro, akbil, benzin, otopark
        - Bills: elektrik, su, doğalgaz, internet, telefon faturası, kira, abonelik
        - Entertainment: sinema, konser, oyun, Netflix, bar
        - Health: eczane, ilaç, doktor, hastane, spor salonu
        - Clothing: kıyafet, ayakkabı, tişört, mont
        - Other: hiçbirine uymayan
        daysAgo: bugün=0, dün=1, evvelsi gün=2; "geçen pazartesi" gibi ifadelerde bugünün gününe göre hesapla. Tarih yoksa 0.
        amount: sadece sayı. "1.250,50" = 1250.5, "2 bin" = 2000.
        note: harcamanın kısa adı, 2-4 kelime, örn. "Kahve", "Taksi", "Elektrik faturası".
        Örnekler:
        "dün akşam kahveye 85 lira verdim" -> {"amount":85,"category":"Food","daysAgo":1,"note":"Kahve"}
        "migrosta 1.250,50 tl alışveriş" -> {"amount":1250.5,"category":"Market","daysAgo":0,"note":"Migros alışverişi"}
        "evvelsi gün taksi 230" -> {"amount":230,"category":"Transport","daysAgo":2,"note":"Taksi"}
        """;

    public static string Today(DateOnly today) =>
        $"Bugün: {today:yyyy-MM-dd} {today.ToString("dddd", TurkishCulture.Instance)}.";
}
