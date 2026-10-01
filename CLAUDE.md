# CLAUDE.md

Bütçe Hava Durumu: harcama takibi yapan, bütçenin durumunu hava durumu gibi
anlatan bir uygulama. Gereksinimler: `docs/prd.md`, yapay zekâ özellikleri: `docs/prd-ai.md`. Mimari standart ve review
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
- Her sorgu kullanıcıya göre filtrelenir: global query filter + sorguda açık `UserId` koşulu.
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
- Ajan araçları `userId` parametresi almaz; kullanıcı `IUserContext`'ten gelir. Araçlar salt okunur.
- Prompt'lar kodda sabit olarak durur (`*Prompt.cs`) ve testlerde kontrol edilir.
- Testler gerçek model çağırmaz: `ScriptedChatClient`, `ProgrammableChatClient`, `FakeEmbeddingGenerator`.

## Bitti tanımı

1. `dotnet build -warnaserror` ve `dotnet test` yeşil.
2. `npm run build` ve `npm run lint` yeşil.
3. `docs/standards.md` içindeki review listesi uygulanmış; uygulanmayan madde PR açıklamasında gerekçeli.
4. PRD değiştiyse `docs/prd.md` güncel.

## Alt ajanlar

`.claude/agents/` altında: `architect` (plan, kod yazmaz), `implementer` (bir aşamayı
kod + testle uygular), `reviewer` (PRD ve standarda karşı salt okunur review).
`/review` komutu reviewer'ı çalıştırır.
