# CLAUDE.md

Bütçe Hava Durumu: harcama takibi yapan, bütçenin durumunu hava durumu gibi
anlatan bir uygulama. Gereksinimler: `docs/prd.md`, yapay zekâ özellikleri: `docs/prd-ai.md`, hesaplar/geçmiş aylar/ortak hane:
`docs/prd-accounts.md`, görevler ve bildirimler: `docs/prd-tasks.md`, ajanlar (ay değerlendirmesi, birikim planlayıcı):
`docs/prd-insights.md`. Mimari standart ve review
listesi: `docs/standards.md`. Kod İngilizce, dokümanlar Türkçe.

## Yapı

```
src/
  ExpenseTracker.Domain/          Entity'ler, enum'lar, hava hesabı (saf, paketsiz)
  ExpenseTracker.Application/     Features/<Aggregate>/<Commands|Queries>/<UseCase>/, arayüzler, validator'lar
  ExpenseTracker.Infrastructure/  EF Core (Npgsql), konfigürasyonlar, migration'lar
  ExpenseTracker.Api/             Controller'lar, middleware, ProblemDetails, composition root
tests/
  ExpenseTracker.Domain.Tests/        Hava hesabı ve entity kuralları
  ExpenseTracker.Application.Tests/   Validator'lar
  ExpenseTracker.Api.IntegrationTests/ Gerçek Postgres (Testcontainers) üzerinde endpoint testleri
web/                              React + Vite + TypeScript PWA
```

Bağımlılık yönü: `Api → Infrastructure → Application → Domain`. Domain hiçbir pakete bağlı değil.

## Komutlar

```bash
dotnet build -warnaserror                 # uyarı = hata
dotnet test                               # entegrasyon testleri Docker + pgvector imajı ister
cd web && npm ci && npm run build         # tip kontrolü + prod build
cd web && npm run lint
docker compose up --build                 # http://localhost:8080
dotnet ef migrations add <Ad> -p src/ExpenseTracker.Infrastructure -s src/ExpenseTracker.Api
```

## Kurallar

- Her use case kendi klasöründe: `XCommand` (record) + `XCommandHandler` + `XCommandValidator`.
- Controller'da iş mantığı yok: `mediator.Send` + HTTP status.
- Durum değişiklikleri entity metodlarıyla (`Expense.Create`, `Budget.ChangeAmount`).
- Her sorgu aktif deftere göre filtrelenir: global query filter + sorguda açık `LedgerId` koşulu. `LedgerId` hanedeyse
  hanenin, değilse kullanıcının kimliğidir (`IUserContext.LedgerId`); `UserId` sadece kaydı kimin girdiğini gösterir.
- Her uç oturum ister (fallback policy). `[AllowAnonymous]` sadece kayıt, giriş ve health için.
- Yetki sunucuda kontrol edilir: başkasının kaydına işlem `ForbiddenException` (403), yok `NotFoundException` (404),
  çakışma `ConflictException` (409).
- Bildirimler kullanıcıya göre filtrelenir (`UserId == IUserContext.UserId`). Arka plan işleri (indexleyici, hatırlatıcı, ay değerlendirmesi)
  oturumsuz çalıştığı için `IgnoreQueryFilters` kullanır; başka yerde kullanımı gerekçe yorumu ister.
- "Bugün" `TimeProvider` + yapılandırılmış saat diliminden gelir, `DateTime.Now` kullanılmaz.
- Sadece unique ihlali yakalanır (`UniqueConstraintViolationException`), diğer DB hataları yukarı fırlar.
- Log mesajları `LoggerMessage` kaynak üreticisiyle, sabit `EventId` ile.
- Yorumlar "neden"i anlatır. Ölü kod, TODO, kararsız yorum bırakılmaz.
- Para `decimal`, tarih `DateOnly`.

## Yapay zekâ kuralları

- Kod `IChatClient` / `IEmbeddingGenerator` soyutlamalarına yazılır; sağlayıcıya özel tip sadece Infrastructure DI'da.
  Sağlayıcı `Ai:Provider` ile seçilir (`OpenAICompatible` = Gemini varsayılanı, `Ollama` = yerel). API anahtarı sadece `.env`'de.
- Modele gönderilen JSON şemalarında nullable alan olmaz (sağlayıcı uyumluluğu).
- LLM çıkarır, kod hesaplar: tarih aritmetiği, toplamlar, yüzdeler kodda. Modelden sayı üretmesi istenmez.
- Model çıktısı JSON schema ile kısıtlanır ve doğrulanır; kullanılamayan çıktı `AiResponseException` (422).
- Ajan araçları `userId`/`ledgerId` parametresini modelden almaz; defter `IUserContext`'ten (arka plan işinde koddan) gelir.
  Araçlar salt okunur. Veri değiştiren tek ajan sonucu (plan uygulama) insan onayıyla çalışır.
- Ajanlar "model karar verir, kod hesaplar" ilkesiyle çalışır: model gerçek kimliği, kategori, seviye gibi seçimler yapar;
  kullanıcının okuduğu her sayı kodda hesaplanır. Modelin kendi yazdığı metin rakam ya da bağlantı içeriyorsa kullanılmaz
  (`ModelText.IsUsable`). Her ajanın kural tabanlı yedeği vardır; model yokken özellik durmaz (`AiModel.IsConfigured`).
- Kullanıcının serbest metni (not, amaç) talimatlara değil, kullanıcı mesajına etiketli olarak girer.
- Model çağıran uçlar `[EnableRateLimiting(RateLimitPolicies.Ai)]` taşır.
- Prompt'lar kodda sabit olarak durur (`*Prompt.cs`) ve testlerde kontrol edilir.
- Testler gerçek model çağırmaz: `ScriptedChatClient`, `ProgrammableChatClient`, `FakeEmbeddingGenerator`.

## Kılavuz ve veritabanı açıklamaları

- Kullanım ve teknik kılavuz: `docs/Butce-Hava-Durumu-Kilavuz.pdf`, kaynağı `docs/guide/` (nasıl üretildiği `docs/guide/README.md`).
- Her tablo ve kolonun Türkçe açıklaması veritabanına yazılır (`SchemaComments.cs`, `COMMENT ON`). Yeni kolon = açıklama +
  migration; eksikse `SchemaDocumentationTests` kırılır. Kılavuzun veritabanı bölümü bu açıklamalardan üretilir.

## Bitti tanımı

1. `dotnet build -warnaserror` ve `dotnet test` yeşil.
2. `npm run build` ve `npm run lint` yeşil.
3. `docs/standards.md` içindeki review listesi uygulanmış; uygulanmayan madde PR açıklamasında gerekçeli.
4. PRD değiştiyse `docs/prd.md` güncel.
5. Kullanıcıya görünen ya da şemayı değiştiren her değişiklikte kılavuz güncel: `kilavuz.html` + `changelog.json`'a yeni
   sürüm, `cd docs/guide && npm run all`, yeni PDF commit'li.

## Alt ajanlar

`.claude/agents/` altında:

| Ajan | Komut | Görev |
| --- | --- | --- |
| `architect` | | Plan çıkarır, kod yazmaz |
| `implementer` | | Bir aşamayı kod + testle uygular |
| `tester` | `/test-plan` | Uç durum senaryoları, eksik testler |
| `reviewer` | `/review` | PRD ve standarda karşı salt okunur review |
| `security-reviewer` | `/security-review` | İzolasyon, yetki, rate limit, prompt injection |
| `migration-checker` | `/check-migration` | Migration'da veri kaybı ve geri alma |
| `docs-keeper` | `/update-guide` | Kılavuz (PDF), changelog, PRD, veritabanı açıklamaları |

Sıra: architect → implementer → tester → security-reviewer / migration-checker → reviewer → docs-keeper.
