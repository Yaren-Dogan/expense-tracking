using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InsightsAgents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_task_id_kind",
                table: "notifications");

            // Renamed, not dropped and re-added: existing task notifications keep pointing at their tasks.
            migrationBuilder.RenameColumn(
                name: "task_id",
                table: "notifications",
                newName: "subject_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "subject_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                comment: "Bildirimin konusu: görev bildirimlerinde tasks.id, ay değerlendirmesinde month_reviews.id, planda budget_plans.id. Konu silinse de bildirim kalır.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "İlgili görev (tasks.id). Görev silinse de bildirim kalır.");

            migrationBuilder.AlterTable(
                name: "notifications",
                comment: "Uygulama içi bildirimler (zil ikonu). Kullanıcı ve konu başına her türden en fazla bir tane (unique: user_id, subject_id, kind).",
                oldComment: "Uygulama içi bildirimler (zil ikonu). Kullanıcı ve görev başına her türden en fazla bir tane (unique: user_id, task_id, kind).");

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "TaskAssigned, TaskDueSoon, TaskOverdue, TaskCompleted, MonthReviewReady ya da BudgetPlanApplied.",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "TaskAssigned, TaskDueSoon, TaskOverdue ya da TaskCompleted.");


            migrationBuilder.CreateTable(
                name: "budget_plans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir."),
                    ledger_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir."),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Planı isteyen kullanıcı."),
                    purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Hedefin amacı, örn. \"Yaz tatili\" (isteğe bağlı, en fazla 100 karakter)."),
                    goal_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "Biriktirilecek toplam tutar, TRY."),
                    requested_months = table.Column<int>(type: "integer", nullable: false, comment: "Kullanıcının istediği süre (ay)."),
                    months = table.Column<int>(type: "integer", nullable: false, comment: "Planın gerçek süresi. Hedef istenen sürede mümkün değilse uzatılır."),
                    start_year = table.Column<int>(type: "integer", nullable: false, comment: "Planın başladığı ayın yılı (planın yapıldığı ayın ertesi ayı)."),
                    start_month = table.Column<int>(type: "integer", nullable: false, comment: "Planın başladığı ay (1-12)."),
                    baseline_monthly = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "Son tam ayların aylık ortalama harcaması; plan buna göre hesaplanır."),
                    monthly_budget = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false, comment: "Planın her ay için önerdiği bütçe = ortalama − aylık birikim."),
                    cuts = table.Column<string>(type: "jsonb", nullable: false, comment: "Kısılacak kategoriler (JSON): kategori, seviye, mevcut ortalama, hedef, gerekçe. Tutarları kod hesaplar."),
                    advice = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false, comment: "Hedef için alışkanlık önerisi."),
                    generated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Kategorileri seçen model adı ya da \"kurallar\"."),
                    applied_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Planın uygulandığı an; boşsa sadece öneri. Uygulanınca planlanan ayların bütçesi yazılır."),
                    applied_by_id = table.Column<Guid>(type: "uuid", nullable: true, comment: "Planı uygulayan kullanıcı."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Son değişiklik anı (UTC); hiç değişmediyse boş.")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_budget_plans", x => x.id);
                },
                comment: "Birikim planları: \"N ayda X lira biriktir\" hedefi için planlayıcı ajanın önerisi. Bir üye uygulayana kadar hiçbir şey değişmez.");

            migrationBuilder.CreateTable(
                name: "month_reviews",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Birincil anahtar. UUID v7: zamana göre sıralı, index'e hep sona eklenir."),
                    ledger_id = table.Column<Guid>(type: "uuid", nullable: false, comment: "Kaydın ait olduğu defter: kişisel defterde kullanıcının, hane defterinde hanenin kimliği. Tüm sorgular bu kolona göre filtrelenir."),
                    year = table.Column<int>(type: "integer", nullable: false, comment: "Değerlendirilen ayın yılı."),
                    month = table.Column<int>(type: "integer", nullable: false, comment: "Değerlendirilen ay (1-12)."),
                    headline = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false, comment: "Ayı özetleyen tek cümle. Model yazdıysa rakam içermez (rakamlar sadece koddan gelir)."),
                    tip = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false, comment: "Önümüzdeki ay için tek tavsiye."),
                    generated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, comment: "Değerlendirmeyi yazan model adı; model yoksa ya da cevabı kullanılamadıysa \"kurallar\"."),
                    highlights = table.Column<string[]>(type: "text[]", nullable: false, comment: "Öne çıkan gerçekler (en fazla 4 cümle). Cümleleri kod hesaplar; model hangilerinin önemli olduğunu seçer."),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, comment: "Kaydın oluşturulma anı (UTC, sunucu saatiyle)."),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true, comment: "Değerlendirme yenilendiyse son yenilenme anı (UTC).")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_month_reviews", x => x.id);
                },
                comment: "Ay değerlendirmeleri: ay bitince değerlendirme ajanının yazdığı özet. Defter ve ay başına bir tane (unique: ledger_id, year, month).");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_subject_id_kind",
                table: "notifications",
                columns: new[] { "user_id", "subject_id", "kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_budget_plans_ledger_id_created_at",
                table: "budget_plans",
                columns: new[] { "ledger_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_month_reviews_ledger_id_year_month",
                table: "month_reviews",
                columns: new[] { "ledger_id", "year", "month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "budget_plans");

            migrationBuilder.DropTable(
                name: "month_reviews");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_subject_id_kind",
                table: "notifications");

            migrationBuilder.RenameColumn(
                name: "subject_id",
                table: "notifications",
                newName: "task_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "task_id",
                table: "notifications",
                type: "uuid",
                nullable: false,
                comment: "İlgili görev (tasks.id). Görev silinse de bildirim kalır.",
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldComment: "Bildirimin konusu: görev bildirimlerinde tasks.id, ay değerlendirmesinde month_reviews.id, planda budget_plans.id. Konu silinse de bildirim kalır.");

            migrationBuilder.AlterTable(
                name: "notifications",
                comment: "Uygulama içi bildirimler (zil ikonu). Kullanıcı ve görev başına her türden en fazla bir tane (unique: user_id, task_id, kind).",
                oldComment: "Uygulama içi bildirimler (zil ikonu). Kullanıcı ve konu başına her türden en fazla bir tane (unique: user_id, subject_id, kind).");

            migrationBuilder.AlterColumn<string>(
                name: "kind",
                table: "notifications",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                comment: "TaskAssigned, TaskDueSoon, TaskOverdue ya da TaskCompleted.",
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20,
                oldComment: "TaskAssigned, TaskDueSoon, TaskOverdue, TaskCompleted, MonthReviewReady ya da BudgetPlanApplied.");


            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_task_id_kind",
                table: "notifications",
                columns: new[] { "user_id", "task_id", "kind" },
                unique: true);
        }
    }
}
