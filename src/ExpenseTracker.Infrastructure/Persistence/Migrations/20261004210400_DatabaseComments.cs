using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Pgvector;

#nullable disable

namespace ExpenseTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DatabaseComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF owns this table, so it is outside the model; its comment is written by hand.
            migrationBuilder.Sql("""COMMENT ON TABLE "__EFMigrationsHistory" IS 'Uygulanmış veritabanı migration''larının listesi (EF Core). Uygulama açılırken eksik olanları sırayla uygular.';""");
            migrationBuilder.Sql("""COMMENT ON COLUMN "__EFMigrationsHistory".migration_id IS 'Migration adı: zaman damgası + açıklama, örn. 20261004145148_TasksAndNotifications.';""");
            migrationBuilder.Sql("""COMMENT ON COLUMN "__EFMigrationsHistory".product_version IS 'Migration''ı uygulayan EF Core sürümü.';""");

            migrationBuilder.AlterTable(
                name: "tasks",
                comment: "Görevler (örn. \"Elektrik faturası → Yaren\"). Harcamalar gibi bir deftere aittir; aylık tekrarlayabilir.");

            migrationBuilder.AlterTable(
                name: "notifications",
                comment: "Uygulama içi bildirimler (zil ikonu). Kullanıcı ve görev başına her türden en fazla bir tane (unique: user_id, task_id, kind).");

            migrationBuilder.AlterTable(
                name: "households",
                comment: "Ortak haneler (evli çiftler, aileler). Hanenin kimliği, hane defterinin kimliğidir (ledger_id).");

            migrationBuilder.AlterTable(
                name: "household_member",
                comment: "Hane üyelikleri. Bir kullanıcı aynı anda tek hanede olabilir (unique: user_id); hanede en fazla 10 üye.");

            migrationBuilder.AlterTable(
                name: "expenses",
                comment: "Harcamalar. Her satır bir harcama; bir deftere (kişisel ya da hane) aittir.");

            migrationBuilder.AlterTable(
                name: "expense_embeddings",
                comment: "Anlamsal arama (RAG) için harcama vektörleri. Arka plan işi doldurur; harcama kaydı modele bağlı değildir.");

            migrationBuilder.AlterTable(
                name: "budgets",
                comment: "Aylık bütçeler. Bir defterin her ayı için en fazla bir bütçe (unique: ledger_id, year, month).");

            migrationBuilder.AlterTable(
                name: "AspNetUserTokens",
                comment: "Kullanıcı token'ları, örn. şifre sıfırlama (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetUsers",
                comment: "Kullanıcı hesapları (ASP.NET Core Identity). Şifre düz metin değil, PBKDF2 hash olarak saklanır.");

            migrationBuilder.AlterTable(
                name: "AspNetUserRoles",
                comment: "Kullanıcı-rol eşleşmeleri (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetUserLogins",
                comment: "Dış sağlayıcı girişleri, örn. Google (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetUserClaims",
                comment: "Kullanıcıya ait ek bilgiler/claim'ler (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetRoles",
                comment: "Roller (Identity; uygulama henüz rol kullanmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetRoleClaims",
                comment: "Role ait claim'ler (Identity; kullanılmıyor).");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Son değişiklik anı (UTC); hiç değişmediyse boş.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "tasks",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                comment: "Görevin adı (en fazla 80 karakter). Ödenince harcamanın notu olur.",
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80);

            migrationBuilder.AlterColumn<Guid>(
                name: "series_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                comment: "Aylık görevin tüm aylarını bağlar. (series_id, due_on) unique: aynı ay iki kez açılmaz.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "recurrence",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "None (tek seferlik) ya da Monthly (tamamlanınca gelecek ay açılır).",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<decimal>(
                name: "paid_amount",
                table: "tasks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                comment: "Ödenen tutar, TRY; tutarsız tamamlandıysa boş.",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ledger_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                comment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "expense_id",
                table: "tasks",
                type: "uuid",
                nullable: true,
                comment: "Ödendi derken tutar girildiyse oluşturulan harcama (expenses.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "due_on",
                table: "tasks",
                type: "date",
                nullable: false,
                comment: "Son gün. Hatırlatma 3 gün kala ve geçince bir kez gider.",
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                comment: "Görevi veren kullanıcı. Görevi silebilir ve tamamlayabilir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "completed_by_id",
                table: "tasks",
                type: "uuid",
                nullable: true,
                comment: "Görevi tamamlayan kullanıcı.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "completed_at",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Tamamlanma anı; boşsa görev açık.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "Görev ödenince eklenecek harcamanın kategorisi (varsayılan Bills).",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<Guid>(
                name: "assignee_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                comment: "Görevi yapacak kullanıcı. Üye haneden ayrılırsa kurucuya geçer.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "anchor_day",
                table: "tasks",
                type: "integer",
                nullable: false,
                comment: "Serinin ayın kaçında olduğu. 31'i kısa ayda ayın son gününe çekilir, sonra geri döner.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                comment: "Bildirimi alacak kullanıcı. Herkes sadece kendi bildirimlerini görür.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Son değişiklik anı (UTC); hiç değişmediyse boş.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "task_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                comment: "İlgili görev (tasks.id). Görev silinse de bildirim kalır.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "read_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Okunma anı; boşsa okunmamış (zilde sayılır).",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "message",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                comment: "Olay anında yazılan Türkçe metin, örn. \"Yaren sana bir görev verdi: ...\".",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "TaskAssigned, TaskDueSoon, TaskOverdue ya da TaskCompleted.",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "households",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Son değişiklik anı (UTC); hiç değişmediyse boş.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "households",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                comment: "Hanenin adı, örn. \"Doğan ailesi\" (en fazla 60 karakter).",
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60);

            migrationBuilder.AlterColumn<string>(
                name: "invite_code",
                table: "households",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                comment: "8 karakterlik davet kodu (karışan 0/O, 1/I/L harfleri yok). Kurucu yenileyebilir.",
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "households",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "households",
                type: "uuid",
                nullable: false,
                comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir. Aynı zamanda hane defterinin kimliği.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "household_member",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                comment: "Owner (kurucu: üye çıkarır, kodu yeniler) ya da Member.",
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "joined_at",
                table: "household_member",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Haneye katılma anı. Kurucu ayrılırsa kuruculuk en eski üyeye geçer.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "household_member",
                type: "uuid",
                nullable: false,
                comment: "Üye kullanıcı (AspNetUsers.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "household_id",
                table: "household_member",
                type: "uuid",
                nullable: false,
                comment: "Üye olunan hane (households.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "expenses",
                type: "uuid",
                nullable: false,
                comment: "Harcamayı giren kullanıcı (AspNetUsers.id). Sadece bu kullanıcı silebilir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Son değişiklik anı (UTC); hiç değişmediyse boş.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "spent_on",
                table: "expenses",
                type: "date",
                nullable: false,
                comment: "Harcamanın yapıldığı gün (Europe/Istanbul takvimine göre).",
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "expenses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                comment: "Serbest açıklama (en fazla 200 karakter). Anlamsal aramada kullanılır.",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ledger_id",
                table: "expenses",
                type: "uuid",
                nullable: false,
                comment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "expenses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                comment: "İstemcinin gönderdiği Idempotency-Key. Aynı anahtarla tekrar gelen istek yeni kayıt açmaz.",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "expenses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "Kategori: Market, Food, Transport, Bills, Entertainment, Health, Clothing, Other.",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "expenses",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                comment: "Tutar, TRY. 0'dan büyük, en fazla 1.000.000, iki ondalık.",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "expenses",
                type: "uuid",
                nullable: false,
                comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "model",
                table: "expense_embeddings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                comment: "Vektörü üreten model. Model değişince vektör yeniden üretilir.",
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "indexed_at",
                table: "expense_embeddings",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Vektörün üretildiği an.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "expense_embeddings",
                type: "vector(1024)",
                nullable: false,
                comment: "\"<Kategori>: <not>\" metninin 1024 boyutlu vektörü. HNSW index, cosine mesafesi.",
                oldClrType: typeof(Vector),
                oldType: "vector(1024)");

            migrationBuilder.AlterColumn<Guid>(
                name: "expense_id",
                table: "expense_embeddings",
                type: "uuid",
                nullable: false,
                comment: "Vektörün ait olduğu harcama (expenses.id); silinince vektör de silinir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "year",
                table: "budgets",
                type: "integer",
                nullable: false,
                comment: "Bütçenin yılı.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "budgets",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Son değişiklik anı (UTC); hiç değişmediyse boş.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "month",
                table: "budgets",
                type: "integer",
                nullable: false,
                comment: "Bütçenin ayı (1-12).",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "ledger_id",
                table: "budgets",
                type: "uuid",
                nullable: false,
                comment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "budgets",
                type: "timestamp with time zone",
                nullable: false,
                comment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "budgets",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                comment: "O ay harcanması planlanan toplam tutar, TRY. Günlük pay buna göre hesaplanır.",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "budgets",
                type: "uuid",
                nullable: false,
                comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                table: "AspNetUserTokens",
                type: "text",
                nullable: true,
                comment: "Token değeri.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "AspNetUserTokens",
                type: "text",
                nullable: false,
                comment: "Token adı.",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "login_provider",
                table: "AspNetUserTokens",
                type: "text",
                nullable: false,
                comment: "Token'ı üreten sağlayıcı.",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserTokens",
                type: "uuid",
                nullable: false,
                comment: "Kullanıcı (AspNetUsers.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "user_name",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "Kullanıcı adı; uygulamada e-posta ile aynıdır.",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "two_factor_enabled",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                comment: "İki adımlı doğrulama açık mı (kullanılmıyor).",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "security_stamp",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                comment: "Şifre ya da güvenlik bilgisi değişince yenilenir; eski oturumları geçersiz kılar.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "phone_number_confirmed",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                comment: "Telefon doğrulandı mı (kullanılmıyor).",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                comment: "Telefon (kullanılmıyor).",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                comment: "Şifrenin PBKDF2 hash'i. Şifrenin kendisi hiçbir yerde saklanmaz.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_user_name",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "Büyük harfe çevrilmiş kullanıcı adı; harf duyarsız arama için (unique).",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "Büyük harfe çevrilmiş e-posta; harf duyarsız arama için.",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "lockout_end",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true,
                comment: "Hesap kilidinin biteceği an. 5 hatalı şifrede 5 dakika kilitlenir.",
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "lockout_enabled",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                comment: "Bu hesap için kilitleme açık mı.",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "email_confirmed",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                comment: "E-posta doğrulandı mı (henüz kullanılmıyor).",
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "E-posta adresi; girişte kullanılır.",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "display_name",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                comment: "Hane üyelerinin gördüğü ad (en fazla 40 karakter).",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "concurrency_stamp",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                comment: "Eşzamanlı güncellemeleri yakalamak için sürüm damgası.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "access_failed_count",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                comment: "Art arda hatalı giriş sayısı; başarılı girişte sıfırlanır.",
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "AspNetUsers",
                type: "uuid",
                nullable: false,
                comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: "AspNetUserRoles",
                type: "uuid",
                nullable: false,
                comment: "Rol (AspNetRoles.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserRoles",
                type: "uuid",
                nullable: false,
                comment: "Kullanıcı (AspNetUsers.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserLogins",
                type: "uuid",
                nullable: false,
                comment: "Kullanıcı (AspNetUsers.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "provider_display_name",
                table: "AspNetUserLogins",
                type: "text",
                nullable: true,
                comment: "Sağlayıcının görünen adı.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "provider_key",
                table: "AspNetUserLogins",
                type: "text",
                nullable: false,
                comment: "Sağlayıcıdaki kullanıcı kimliği.",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "login_provider",
                table: "AspNetUserLogins",
                type: "text",
                nullable: false,
                comment: "Sağlayıcı adı.",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserClaims",
                type: "uuid",
                nullable: false,
                comment: "Kullanıcı (AspNetUsers.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "claim_value",
                table: "AspNetUserClaims",
                type: "text",
                nullable: true,
                comment: "Claim değeri.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "claim_type",
                table: "AspNetUserClaims",
                type: "text",
                nullable: true,
                comment: "Claim türü.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "AspNetUserClaims",
                type: "integer",
                nullable: false,
                comment: "Birincil anahtar.",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_name",
                table: "AspNetRoles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "Büyük harfe çevrilmiş rol adı (unique).",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "AspNetRoles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                comment: "Rol adı.",
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "concurrency_stamp",
                table: "AspNetRoles",
                type: "text",
                nullable: true,
                comment: "Eşzamanlı güncellemeleri yakalamak için sürüm damgası.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "AspNetRoles",
                type: "uuid",
                nullable: false,
                comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: "AspNetRoleClaims",
                type: "uuid",
                nullable: false,
                comment: "Rol (AspNetRoles.id).",
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<string>(
                name: "claim_value",
                table: "AspNetRoleClaims",
                type: "text",
                nullable: true,
                comment: "Claim değeri.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "claim_type",
                table: "AspNetRoleClaims",
                type: "text",
                nullable: true,
                comment: "Claim türü.",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "AspNetRoleClaims",
                type: "integer",
                nullable: false,
                comment: "Birincil anahtar.",
                oldClrType: typeof(int),
                oldType: "integer")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""COMMENT ON TABLE "__EFMigrationsHistory" IS NULL;""");
            migrationBuilder.Sql("""COMMENT ON COLUMN "__EFMigrationsHistory".migration_id IS NULL;""");
            migrationBuilder.Sql("""COMMENT ON COLUMN "__EFMigrationsHistory".product_version IS NULL;""");

            migrationBuilder.AlterTable(
                name: "tasks",
                oldComment: "Görevler (örn. \"Elektrik faturası → Yaren\"). Harcamalar gibi bir deftere aittir; aylık tekrarlayabilir.");

            migrationBuilder.AlterTable(
                name: "notifications",
                oldComment: "Uygulama içi bildirimler (zil ikonu). Kullanıcı ve görev başına her türden en fazla bir tane (unique: user_id, task_id, kind).");

            migrationBuilder.AlterTable(
                name: "households",
                oldComment: "Ortak haneler (evli çiftler, aileler). Hanenin kimliği, hane defterinin kimliğidir (ledger_id).");

            migrationBuilder.AlterTable(
                name: "household_member",
                oldComment: "Hane üyelikleri. Bir kullanıcı aynı anda tek hanede olabilir (unique: user_id); hanede en fazla 10 üye.");

            migrationBuilder.AlterTable(
                name: "expenses",
                oldComment: "Harcamalar. Her satır bir harcama; bir deftere (kişisel ya da hane) aittir.");

            migrationBuilder.AlterTable(
                name: "expense_embeddings",
                oldComment: "Anlamsal arama (RAG) için harcama vektörleri. Arka plan işi doldurur; harcama kaydı modele bağlı değildir.");

            migrationBuilder.AlterTable(
                name: "budgets",
                oldComment: "Aylık bütçeler. Bir defterin her ayı için en fazla bir bütçe (unique: ledger_id, year, month).");

            migrationBuilder.AlterTable(
                name: "AspNetUserTokens",
                oldComment: "Kullanıcı token'ları, örn. şifre sıfırlama (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetUsers",
                oldComment: "Kullanıcı hesapları (ASP.NET Core Identity). Şifre düz metin değil, PBKDF2 hash olarak saklanır.");

            migrationBuilder.AlterTable(
                name: "AspNetUserRoles",
                oldComment: "Kullanıcı-rol eşleşmeleri (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetUserLogins",
                oldComment: "Dış sağlayıcı girişleri, örn. Google (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetUserClaims",
                oldComment: "Kullanıcıya ait ek bilgiler/claim'ler (Identity; kullanılmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetRoles",
                oldComment: "Roller (Identity; uygulama henüz rol kullanmıyor).");

            migrationBuilder.AlterTable(
                name: "AspNetRoleClaims",
                oldComment: "Role ait claim'ler (Identity; kullanılmıyor).");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Son değişiklik anı (UTC); hiç değişmediyse boş.");

            migrationBuilder.AlterColumn<string>(
                name: "title",
                table: "tasks",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(80)",
                oldMaxLength: 80,
                oldComment: "Görevin adı (en fazla 80 karakter). Ödenince harcamanın notu olur.");

            migrationBuilder.AlterColumn<Guid>(
                name: "series_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Aylık görevin tüm aylarını bağlar. (series_id, due_on) unique: aynı ay iki kez açılmaz.");

            migrationBuilder.AlterColumn<string>(
                name: "recurrence",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "None (tek seferlik) ya da Monthly (tamamlanınca gelecek ay açılır).");

            migrationBuilder.AlterColumn<decimal>(
                name: "paid_amount",
                table: "tasks",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldNullable: true,
                oldComment: "Ödenen tutar, TRY; tutarsız tamamlandıysa boş.");

            migrationBuilder.AlterColumn<Guid>(
                name: "ledger_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir.");

            migrationBuilder.AlterColumn<Guid>(
                name: "expense_id",
                table: "tasks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "Ödendi derken tutar girildiyse oluşturulan harcama (expenses.id).");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "due_on",
                table: "tasks",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldComment: "Son gün. Hatırlatma 3 gün kala ve geçince bir kez gider.");

            migrationBuilder.AlterColumn<Guid>(
                name: "created_by_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Görevi veren kullanıcı. Görevi silebilir ve tamamlayabilir.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).");

            migrationBuilder.AlterColumn<Guid>(
                name: "completed_by_id",
                table: "tasks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true,
                oldComment: "Görevi tamamlayan kullanıcı.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "completed_at",
                table: "tasks",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Tamamlanma anı; boşsa görev açık.");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "tasks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "Görev ödenince eklenecek harcamanın kategorisi (varsayılan Bills).");

            migrationBuilder.AlterColumn<Guid>(
                name: "assignee_id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Görevi yapacak kullanıcı. Üye haneden ayrılırsa kurucuya geçer.");

            migrationBuilder.AlterColumn<int>(
                name: "anchor_day",
                table: "tasks",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Serinin ayın kaçında olduğu. 31'i kısa ayda ayın son gününe çekilir, sonra geri döner.");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "tasks",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Bildirimi alacak kullanıcı. Herkes sadece kendi bildirimlerini görür.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Son değişiklik anı (UTC); hiç değişmediyse boş.");

            migrationBuilder.AlterColumn<Guid>(
                name: "task_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "İlgili görev (tasks.id). Görev silinse de bildirim kalır.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "read_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Okunma anı; boşsa okunmamış (zilde sayılır).");

            migrationBuilder.AlterColumn<string>(
                name: "message",
                table: "notifications",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldComment: "Olay anında yazılan Türkçe metin, örn. \"Yaren sana bir görev verdi: ...\".");

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "TaskAssigned, TaskDueSoon, TaskOverdue ya da TaskCompleted.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "households",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Son değişiklik anı (UTC); hiç değişmediyse boş.");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "households",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(60)",
                oldMaxLength: 60,
                oldComment: "Hanenin adı, örn. \"Doğan ailesi\" (en fazla 60 karakter).");

            migrationBuilder.AlterColumn<string>(
                name: "invite_code",
                table: "households",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(8)",
                oldMaxLength: 8,
                oldComment: "8 karakterlik davet kodu (karışan 0/O, 1/I/L harfleri yok). Kurucu yenileyebilir.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "households",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "households",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir. Aynı zamanda hane defterinin kimliği.");

            migrationBuilder.AlterColumn<string>(
                name: "role",
                table: "household_member",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldComment: "Owner (kurucu: üye çıkarır, kodu yeniler) ya da Member.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "joined_at",
                table: "household_member",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Haneye katılma anı. Kurucu ayrılırsa kuruculuk en eski üyeye geçer.");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "household_member",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Üye kullanıcı (AspNetUsers.id).");

            migrationBuilder.AlterColumn<Guid>(
                name: "household_id",
                table: "household_member",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Üye olunan hane (households.id).");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "expenses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Harcamayı giren kullanıcı (AspNetUsers.id). Sadece bu kullanıcı silebilir.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Son değişiklik anı (UTC); hiç değişmediyse boş.");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "spent_on",
                table: "expenses",
                type: "date",
                nullable: false,
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldComment: "Harcamanın yapıldığı gün (Europe/Istanbul takvimine göre).");

            migrationBuilder.AlterColumn<string>(
                name: "note",
                table: "expenses",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true,
                oldComment: "Serbest açıklama (en fazla 200 karakter). Anlamsal aramada kullanılır.");

            migrationBuilder.AlterColumn<Guid>(
                name: "ledger_id",
                table: "expenses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir.");

            migrationBuilder.AlterColumn<string>(
                name: "idempotency_key",
                table: "expenses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true,
                oldComment: "İstemcinin gönderdiği Idempotency-Key. Aynı anahtarla tekrar gelen istek yeni kayıt açmaz.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "expenses",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).");

            migrationBuilder.AlterColumn<string>(
                name: "category",
                table: "expenses",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "Kategori: Market, Food, Transport, Bills, Entertainment, Health, Clothing, Other.");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "expenses",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldComment: "Tutar, TRY. 0'dan büyük, en fazla 1.000.000, iki ondalık.");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "expenses",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.");

            migrationBuilder.AlterColumn<string>(
                name: "model",
                table: "expense_embeddings",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldComment: "Vektörü üreten model. Model değişince vektör yeniden üretilir.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "indexed_at",
                table: "expense_embeddings",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Vektörün üretildiği an.");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "expense_embeddings",
                type: "vector(1024)",
                nullable: false,
                oldClrType: typeof(Vector),
                oldType: "vector(1024)",
                oldComment: "\"<Kategori>: <not>\" metninin 1024 boyutlu vektörü. HNSW index, cosine mesafesi.");

            migrationBuilder.AlterColumn<Guid>(
                name: "expense_id",
                table: "expense_embeddings",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Vektörün ait olduğu harcama (expenses.id); silinince vektör de silinir.");

            migrationBuilder.AlterColumn<int>(
                name: "year",
                table: "budgets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Bütçenin yılı.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "budgets",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Son değişiklik anı (UTC); hiç değişmediyse boş.");

            migrationBuilder.AlterColumn<int>(
                name: "month",
                table: "budgets",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Bütçenin ayı (1-12).");

            migrationBuilder.AlterColumn<Guid>(
                name: "ledger_id",
                table: "budgets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "created_at",
                table: "budgets",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldComment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle).");

            migrationBuilder.AlterColumn<decimal>(
                name: "amount",
                table: "budgets",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,2)",
                oldPrecision: 12,
                oldScale: 2,
                oldComment: "O ay harcanması planlanan toplam tutar, TRY. Günlük pay buna göre hesaplanır.");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "budgets",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.");

            migrationBuilder.AlterColumn<string>(
                name: "value",
                table: "AspNetUserTokens",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Token değeri.");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "AspNetUserTokens",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Token adı.");

            migrationBuilder.AlterColumn<string>(
                name: "login_provider",
                table: "AspNetUserTokens",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Token'ı üreten sağlayıcı.");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserTokens",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Kullanıcı (AspNetUsers.id).");

            migrationBuilder.AlterColumn<string>(
                name: "user_name",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "Kullanıcı adı; uygulamada e-posta ile aynıdır.");

            migrationBuilder.AlterColumn<bool>(
                name: "two_factor_enabled",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "İki adımlı doğrulama açık mı (kullanılmıyor).");

            migrationBuilder.AlterColumn<string>(
                name: "security_stamp",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Şifre ya da güvenlik bilgisi değişince yenilenir; eski oturumları geçersiz kılar.");

            migrationBuilder.AlterColumn<bool>(
                name: "phone_number_confirmed",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "Telefon doğrulandı mı (kullanılmıyor).");

            migrationBuilder.AlterColumn<string>(
                name: "phone_number",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Telefon (kullanılmıyor).");

            migrationBuilder.AlterColumn<string>(
                name: "password_hash",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Şifrenin PBKDF2 hash'i. Şifrenin kendisi hiçbir yerde saklanmaz.");

            migrationBuilder.AlterColumn<string>(
                name: "normalized_user_name",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "Büyük harfe çevrilmiş kullanıcı adı; harf duyarsız arama için (unique).");

            migrationBuilder.AlterColumn<string>(
                name: "normalized_email",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "Büyük harfe çevrilmiş e-posta; harf duyarsız arama için.");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "lockout_end",
                table: "AspNetUsers",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true,
                oldComment: "Hesap kilidinin biteceği an. 5 hatalı şifrede 5 dakika kilitlenir.");

            migrationBuilder.AlterColumn<bool>(
                name: "lockout_enabled",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "Bu hesap için kilitleme açık mı.");

            migrationBuilder.AlterColumn<bool>(
                name: "email_confirmed",
                table: "AspNetUsers",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComment: "E-posta doğrulandı mı (henüz kullanılmıyor).");

            migrationBuilder.AlterColumn<string>(
                name: "email",
                table: "AspNetUsers",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "E-posta adresi; girişte kullanılır.");

            migrationBuilder.AlterColumn<string>(
                name: "display_name",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Hane üyelerinin gördüğü ad (en fazla 40 karakter).");

            migrationBuilder.AlterColumn<string>(
                name: "concurrency_stamp",
                table: "AspNetUsers",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Eşzamanlı güncellemeleri yakalamak için sürüm damgası.");

            migrationBuilder.AlterColumn<int>(
                name: "access_failed_count",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Art arda hatalı giriş sayısı; başarılı girişte sıfırlanır.");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "AspNetUsers",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: "AspNetUserRoles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Rol (AspNetRoles.id).");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserRoles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Kullanıcı (AspNetUsers.id).");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserLogins",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Kullanıcı (AspNetUsers.id).");

            migrationBuilder.AlterColumn<string>(
                name: "provider_display_name",
                table: "AspNetUserLogins",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Sağlayıcının görünen adı.");

            migrationBuilder.AlterColumn<string>(
                name: "provider_key",
                table: "AspNetUserLogins",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Sağlayıcıdaki kullanıcı kimliği.");

            migrationBuilder.AlterColumn<string>(
                name: "login_provider",
                table: "AspNetUserLogins",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldComment: "Sağlayıcı adı.");

            migrationBuilder.AlterColumn<Guid>(
                name: "user_id",
                table: "AspNetUserClaims",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Kullanıcı (AspNetUsers.id).");

            migrationBuilder.AlterColumn<string>(
                name: "claim_value",
                table: "AspNetUserClaims",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Claim değeri.");

            migrationBuilder.AlterColumn<string>(
                name: "claim_type",
                table: "AspNetUserClaims",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Claim türü.");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "AspNetUserClaims",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Birincil anahtar.")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AlterColumn<string>(
                name: "normalized_name",
                table: "AspNetRoles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "Büyük harfe çevrilmiş rol adı (unique).");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "AspNetRoles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(256)",
                oldMaxLength: 256,
                oldNullable: true,
                oldComment: "Rol adı.");

            migrationBuilder.AlterColumn<string>(
                name: "concurrency_stamp",
                table: "AspNetRoles",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Eşzamanlı güncellemeleri yakalamak için sürüm damgası.");

            migrationBuilder.AlterColumn<Guid>(
                name: "id",
                table: "AspNetRoles",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir.");

            migrationBuilder.AlterColumn<Guid>(
                name: "role_id",
                table: "AspNetRoleClaims",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Rol (AspNetRoles.id).");

            migrationBuilder.AlterColumn<string>(
                name: "claim_value",
                table: "AspNetRoleClaims",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Claim değeri.");

            migrationBuilder.AlterColumn<string>(
                name: "claim_type",
                table: "AspNetRoleClaims",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true,
                oldComment: "Claim türü.");

            migrationBuilder.AlterColumn<int>(
                name: "id",
                table: "AspNetRoleClaims",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldComment: "Birincil anahtar.")
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)
                .OldAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);
        }
    }
}
