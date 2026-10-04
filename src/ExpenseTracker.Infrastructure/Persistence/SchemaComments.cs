using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Households;
using ExpenseTracker.Domain.Insights;
using ExpenseTracker.Domain.Notifications;
using ExpenseTracker.Domain.Tasks;
using ExpenseTracker.Infrastructure.Ai;
using ExpenseTracker.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Persistence;

/// <summary>
/// Turkish descriptions written into the database itself (COMMENT ON TABLE / COLUMN), so anyone opening the schema in
/// Adminer or psql sees what each table and column means. The user guide's database chapter is generated from these
/// comments, and an integration test fails if a table or column is left undocumented.
/// </summary>
internal static class SchemaComments
{
    // Shared wording for the audit columns every Entity has.
    private const string Id = "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.";
    private const string CreatedAt = "Kaydın oluşturulma anı (UTC, sunucu saatiyle).";
    private const string UpdatedAt = "Son değişiklik anı (UTC); hiç değişmediyse boş.";
    private const string Ledger = "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. "
                                  + "Tüm sorgular bu kolona göre filtrelenir.";

    public static void Apply(ModelBuilder model)
    {
        Document<Expense>(model,
            "Harcamalar. Her satır bir harcama; bir deftere (kişisel ya da hane) aittir.",
            new()
            {
                [nameof(Expense.Id)] = Id,
                [nameof(Expense.UserId)] = "Harcamayı giren kullanıcı (AspNetUsers.id). Sadece bu kullanıcı silebilir.",
                [nameof(Expense.LedgerId)] = Ledger,
                [nameof(Expense.Amount)] = "Tutar, TRY. 0'dan büyük, en fazla 1.000.000, iki ondalık.",
                [nameof(Expense.Category)] = "Kategori: Market, Food, Transport, Bills, Entertainment, Health, Clothing, Other.",
                [nameof(Expense.Note)] = "Serbest açıklama (en fazla 200 karakter). Anlamsal aramada kullanılır.",
                [nameof(Expense.SpentOn)] = "Harcamanın yapıldığı gün (Europe/Istanbul takvimine göre).",
                [nameof(Expense.IdempotencyKey)] = "İstemcinin gönderdiği Idempotency-Key. Aynı anahtarla tekrar gelen istek yeni kayıt açmaz.",
                [nameof(Expense.CreatedAt)] = CreatedAt,
                [nameof(Expense.UpdatedAt)] = UpdatedAt,
            });

        Document<Budget>(model,
            "Aylık bütçeler. Bir defterin her ayı için en fazla bir bütçe (unique: ledger_id, year, month).",
            new()
            {
                [nameof(Budget.Id)] = Id,
                [nameof(Budget.LedgerId)] = Ledger,
                [nameof(Budget.Year)] = "Bütçenin yılı.",
                [nameof(Budget.Month)] = "Bütçenin ayı (1-12).",
                [nameof(Budget.Amount)] = "O ay harcanması planlanan toplam tutar, TRY. Günlük pay buna göre hesaplanır.",
                [nameof(Budget.CreatedAt)] = CreatedAt,
                [nameof(Budget.UpdatedAt)] = UpdatedAt,
            });

        Document<Household>(model,
            "Ortak haneler (evli çiftler, aileler). Hanenin kimliği, hane defterinin kimliğidir (ledger_id).",
            new()
            {
                [nameof(Household.Id)] = Id + " Aynı zamanda hane defterinin kimliği.",
                [nameof(Household.Name)] = "Hanenin adı, örn. \"Doğan ailesi\" (en fazla 60 karakter).",
                [nameof(Household.InviteCode)] = "8 karakterlik davet kodu (karışan 0/O, 1/I/L harfleri yok). Kurucu yenileyebilir.",
                [nameof(Household.CreatedAt)] = CreatedAt,
                [nameof(Household.UpdatedAt)] = UpdatedAt,
            });

        Document<HouseholdMember>(model,
            "Hane üyelikleri. Bir kullanıcı aynı anda tek hanede olabilir (unique: user_id); hanede en fazla 10 üye.",
            new()
            {
                [nameof(HouseholdMember.HouseholdId)] = "Üye olunan hane (households.id).",
                [nameof(HouseholdMember.UserId)] = "Üye kullanıcı (AspNetUsers.id).",
                [nameof(HouseholdMember.Role)] = "Owner (kurucu: üye çıkarır, kodu yeniler) ya da Member.",
                [nameof(HouseholdMember.JoinedAt)] = "Haneye katılma anı. Kurucu ayrılırsa kuruculuk en eski üyeye geçer.",
            });

        Document<HouseholdTask>(model,
            "Görevler (örn. \"Elektrik faturası → Yaren\"). Harcamalar gibi bir deftere aittir; aylık tekrarlayabilir.",
            new()
            {
                [nameof(HouseholdTask.Id)] = Id,
                [nameof(HouseholdTask.LedgerId)] = Ledger,
                [nameof(HouseholdTask.SeriesId)] = "Aylık görevin tüm aylarını bağlar. (series_id, due_on) unique: aynı ay iki kez açılmaz.",
                [nameof(HouseholdTask.Title)] = "Görevin adı (en fazla 80 karakter). Ödenince harcamanın notu olur.",
                [nameof(HouseholdTask.Category)] = "Görev ödenince eklenecek harcamanın kategorisi (varsayılan Bills).",
                [nameof(HouseholdTask.AssigneeId)] = "Görevi yapacak kullanıcı. Üye haneden ayrılırsa kurucuya geçer.",
                [nameof(HouseholdTask.CreatedById)] = "Görevi veren kullanıcı. Görevi silebilir ve tamamlayabilir.",
                [nameof(HouseholdTask.DueOn)] = "Son gün. Hatırlatma 3 gün kala ve geçince bir kez gider.",
                [nameof(HouseholdTask.Recurrence)] = "None (tek seferlik) ya da Monthly (tamamlanınca gelecek ay açılır).",
                [nameof(HouseholdTask.AnchorDay)] = "Serinin ayın kaçında olduğu. 31'i kısa ayda ayın son gününe çekilir, sonra geri döner.",
                [nameof(HouseholdTask.CompletedAt)] = "Tamamlanma anı; boşsa görev açık.",
                [nameof(HouseholdTask.CompletedById)] = "Görevi tamamlayan kullanıcı.",
                [nameof(HouseholdTask.ExpenseId)] = "Ödendi derken tutar girildiyse oluşturulan harcama (expenses.id).",
                [nameof(HouseholdTask.PaidAmount)] = "Ödenen tutar, TRY; tutarsız tamamlandıysa boş.",
                [nameof(HouseholdTask.CreatedAt)] = CreatedAt,
                [nameof(HouseholdTask.UpdatedAt)] = UpdatedAt,
            });

        Document<Notification>(model,
            "Uygulama içi bildirimler (zil ikonu). Kullanıcı ve konu başına her türden en fazla bir tane (unique: user_id, subject_id, kind).",
            new()
            {
                [nameof(Notification.Id)] = Id,
                [nameof(Notification.UserId)] = "Bildirimi alacak kullanıcı. Herkes sadece kendi bildirimlerini görür.",
                [nameof(Notification.Kind)] = "TaskAssigned, TaskDueSoon, TaskOverdue, TaskCompleted, MonthReviewReady ya da BudgetPlanApplied.",
                [nameof(Notification.SubjectId)] = "Bildirimin konusu: görev bildirimlerinde tasks.id, ay değerlendirmesinde month_reviews.id, planda budget_plans.id. Konu silinse de bildirim kalır.",
                [nameof(Notification.Message)] = "Olay anında yazılan Türkçe metin, örn. \"Yaren sana bir görev verdi: ...\".",
                [nameof(Notification.ReadAt)] = "Okunma anı; boşsa okunmamış (zilde sayılır).",
                [nameof(Notification.CreatedAt)] = CreatedAt,
                [nameof(Notification.UpdatedAt)] = UpdatedAt,
            });

        Document<ExpenseEmbedding>(model,
            "Anlamsal arama (RAG) için harcama vektörleri. Arka plan işi doldurur; harcama kaydı modele bağlı değildir.",
            new()
            {
                [nameof(ExpenseEmbedding.ExpenseId)] = "Vektörün ait olduğu harcama (expenses.id); silinince vektör de silinir.",
                [nameof(ExpenseEmbedding.Embedding)] = "\"<Kategori>: <not>\" metninin 1024 boyutlu vektörü. HNSW index, cosine mesafesi.",
                [nameof(ExpenseEmbedding.Model)] = "Vektörü üreten model. Model değişince vektör yeniden üretilir.",
                [nameof(ExpenseEmbedding.IndexedAt)] = "Vektörün üretildiği an.",
            });

        Document<MonthReview>(model,
            "Ay değerlendirmeleri: ay bitince değerlendirme ajanının yazdığı özet. Defter ve ay başına bir tane (unique: ledger_id, year, month).",
            new()
            {
                [nameof(MonthReview.Id)] = Id,
                [nameof(MonthReview.LedgerId)] = Ledger,
                [nameof(MonthReview.Year)] = "Değerlendirilen ayın yılı.",
                [nameof(MonthReview.Month)] = "Değerlendirilen ay (1-12).",
                [nameof(MonthReview.Headline)] = "Ayı özetleyen tek cümle. Model yazdıysa rakam içermez (rakamlar sadece koddan gelir).",
                ["_highlights"] = "Öne çıkan gerçekler (en fazla 4 cümle). Cümleleri kod hesaplar; model hangilerinin önemli olduğunu seçer.",
                [nameof(MonthReview.Tip)] = "Önümüzdeki ay için tek tavsiye.",
                [nameof(MonthReview.GeneratedBy)] = "Değerlendirmeyi yazan model adı; model yoksa ya da cevabı kullanılamadıysa \"kurallar\".",
                [nameof(MonthReview.CreatedAt)] = CreatedAt,
                [nameof(MonthReview.UpdatedAt)] = "Değerlendirme yenilendiyse son yenilenme anı (UTC).",
            });

        Document<BudgetPlan>(model,
            "Birikim planları: \"N ayda X lira biriktir\" hedefi için planlayıcı ajanın önerisi. Bir üye uygulayana kadar hiçbir şey değişmez.",
            new()
            {
                [nameof(BudgetPlan.Id)] = Id,
                [nameof(BudgetPlan.LedgerId)] = Ledger,
                [nameof(BudgetPlan.CreatedById)] = "Planı isteyen kullanıcı.",
                [nameof(BudgetPlan.Purpose)] = "Hedefin amacı, örn. \"Yaz tatili\" (isteğe bağlı, en fazla 100 karakter).",
                [nameof(BudgetPlan.GoalAmount)] = "Biriktirilecek toplam tutar, TRY.",
                [nameof(BudgetPlan.RequestedMonths)] = "Kullanıcının istediği süre (ay).",
                [nameof(BudgetPlan.Months)] = "Planın gerçek süresi. Hedef istenen sürede mümkün değilse uzatılır.",
                [nameof(BudgetPlan.StartYear)] = "Planın başladığı ayın yılı (planın yapıldığı ayın ertesi ayı).",
                [nameof(BudgetPlan.StartMonth)] = "Planın başladığı ay (1-12).",
                [nameof(BudgetPlan.BaselineMonthly)] = "Son tam ayların aylık ortalama harcaması; plan buna göre hesaplanır.",
                [nameof(BudgetPlan.MonthlyBudget)] = "Planın her ay için önerdiği bütçe = ortalama − aylık birikim.",
                [nameof(BudgetPlan.Cuts)] = "Kısılacak kategoriler (JSON): kategori, seviye, mevcut ortalama, hedef, gerekçe. Tutarları kod hesaplar.",
                [nameof(BudgetPlan.Advice)] = "Hedef için alışkanlık önerisi.",
                [nameof(BudgetPlan.GeneratedBy)] = "Kategorileri seçen model adı ya da \"kurallar\".",
                [nameof(BudgetPlan.AppliedAt)] = "Planın uygulandığı an; boşsa sadece öneri. Uygulanınca planlanan ayların bütçesi yazılır.",
                [nameof(BudgetPlan.AppliedById)] = "Planı uygulayan kullanıcı.",
                [nameof(BudgetPlan.CreatedAt)] = CreatedAt,
                [nameof(BudgetPlan.UpdatedAt)] = UpdatedAt,
            });

        Document<DataProtectionKey>(model,
            "Oturum cookie'lerini şifreleyen anahtarlar (ASP.NET Core Data Protection). Veritabanında durur ki güncelleme sonrası kimsenin oturumu kapanmasın.",
            new()
            {
                [nameof(DataProtectionKey.Id)] = "Birincil anahtar.",
                [nameof(DataProtectionKey.FriendlyName)] = "Anahtarın adı.",
                [nameof(DataProtectionKey.Xml)] = "Anahtarın kendisi (XML). Gizli bilgidir; dışarı verilmemeli.",
            });

        DocumentIdentity(model);
    }

    /// <summary>ASP.NET Core Identity's tables: accounts, plus features the app does not use yet (roles, external logins).</summary>
    private static void DocumentIdentity(ModelBuilder model)
    {
        Document<AppUser>(model,
            "Kullanıcı hesapları (ASP.NET Core Identity). Şifre düz metin değil, PBKDF2 hash olarak saklanır.",
            new()
            {
                ["Id"] = Id,
                [nameof(AppUser.DisplayName)] = "Hane üyelerinin gördüğü ad (en fazla 40 karakter).",
                ["UserName"] = "Kullanıcı adı; uygulamada e-posta ile aynıdır.",
                ["NormalizedUserName"] = "Büyük harfe çevrilmiş kullanıcı adı; harf duyarsız arama için (unique).",
                ["Email"] = "E-posta adresi; girişte kullanılır.",
                ["NormalizedEmail"] = "Büyük harfe çevrilmiş e-posta; harf duyarsız arama için.",
                ["EmailConfirmed"] = "E-posta doğrulandı mı (henüz kullanılmıyor).",
                ["PasswordHash"] = "Şifrenin PBKDF2 hash'i. Şifrenin kendisi hiçbir yerde saklanmaz.",
                ["SecurityStamp"] = "Şifre ya da güvenlik bilgisi değişince yenilenir; eski oturumları geçersiz kılar.",
                ["ConcurrencyStamp"] = "Eşzamanlı güncellemeleri yakalamak için sürüm damgası.",
                ["PhoneNumber"] = "Telefon (kullanılmıyor).",
                ["PhoneNumberConfirmed"] = "Telefon doğrulandı mı (kullanılmıyor).",
                ["TwoFactorEnabled"] = "İki adımlı doğrulama açık mı (kullanılmıyor).",
                ["LockoutEnd"] = "Hesap kilidinin biteceği an. 5 hatalı şifrede 5 dakika kilitlenir.",
                ["LockoutEnabled"] = "Bu hesap için kilitleme açık mı.",
                ["AccessFailedCount"] = "Art arda hatalı giriş sayısı; başarılı girişte sıfırlanır.",
            });

        Document<IdentityRole<Guid>>(model, "Roller (Identity; uygulama henüz rol kullanmıyor).", new()
        {
            ["Id"] = Id,
            ["Name"] = "Rol adı.",
            ["NormalizedName"] = "Büyük harfe çevrilmiş rol adı (unique).",
            ["ConcurrencyStamp"] = "Eşzamanlı güncellemeleri yakalamak için sürüm damgası.",
        });

        Document<IdentityUserRole<Guid>>(model, "Kullanıcı-rol eşleşmeleri (Identity; kullanılmıyor).", new()
        {
            ["UserId"] = "Kullanıcı (AspNetUsers.id).",
            ["RoleId"] = "Rol (AspNetRoles.id).",
        });

        Document<IdentityUserClaim<Guid>>(model, "Kullanıcıya ait ek bilgiler/claim'ler (Identity; kullanılmıyor).", new()
        {
            ["Id"] = "Birincil anahtar.",
            ["UserId"] = "Kullanıcı (AspNetUsers.id).",
            ["ClaimType"] = "Claim türü.",
            ["ClaimValue"] = "Claim değeri.",
        });

        Document<IdentityRoleClaim<Guid>>(model, "Role ait claim'ler (Identity; kullanılmıyor).", new()
        {
            ["Id"] = "Birincil anahtar.",
            ["RoleId"] = "Rol (AspNetRoles.id).",
            ["ClaimType"] = "Claim türü.",
            ["ClaimValue"] = "Claim değeri.",
        });

        Document<IdentityUserLogin<Guid>>(model, "Dış sağlayıcı girişleri, örn. Google (Identity; kullanılmıyor).", new()
        {
            ["LoginProvider"] = "Sağlayıcı adı.",
            ["ProviderKey"] = "Sağlayıcıdaki kullanıcı kimliği.",
            ["ProviderDisplayName"] = "Sağlayıcının görünen adı.",
            ["UserId"] = "Kullanıcı (AspNetUsers.id).",
        });

        Document<IdentityUserToken<Guid>>(model, "Kullanıcı token'ları, örn. şifre sıfırlama (Identity; kullanılmıyor).", new()
        {
            ["UserId"] = "Kullanıcı (AspNetUsers.id).",
            ["LoginProvider"] = "Token'ı üreten sağlayıcı.",
            ["Name"] = "Token adı.",
            ["Value"] = "Token değeri.",
        });
    }

    private static void Document<TEntity>(ModelBuilder model, string table, Dictionary<string, string> columns)
        where TEntity : class
    {
        var entity = model.Entity<TEntity>();
        entity.ToTable(t => t.HasComment(table));
        foreach (var (property, comment) in columns)
            entity.Property(property).HasComment(comment);
    }
}
