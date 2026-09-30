# Bütçe Hava Durumu

Harcamalarını takip et, bütçenin durumunu **hava durumu** gibi gör.
Bugün güneşli mi, fırtına mı geliyor? Grafik okumak yok, bir bakışta anlaşılır.

<p>
  <img src="docs/screenshots/mobile.png" alt="Mobil görünüm" width="260">
  <img src="docs/screenshots/desktop.png" alt="Masaüstü görünüm" width="560">
</p>

- **Bugünün havası:** Bugünkü harcaman, günlük payınla karşılaştırılır.
- **Gönül rahatlığıyla harcanabilir:** Hava bozulmadan bugün daha ne kadar harcayabilirsin.
- **Son 7 günün havası** ve **ay sonu tahmini.**
- Web ve mobil uyumlu PWA: telefonda "Ana ekrana ekle" ile uygulama gibi çalışır.

Kurallar ve kapsam: [`docs/prd.md`](docs/prd.md). Mimari standart ve review listesi: [`docs/standards.md`](docs/standards.md).

## Çalıştırma

Gereken: Docker.

```bash
docker compose up --build
```

- Uygulama: http://localhost:8080
- API: http://localhost:5080 (sağlık: `/health`)

### Geliştirme

Gereken: .NET 9 SDK, Node 22, Docker (Postgres ve entegrasyon testleri için).

```bash
docker compose up -d postgres
dotnet run --project src/ExpenseTracker.Api          # http://localhost:5080, migration'ları uygular
cd web && npm install && npm run dev                 # http://localhost:5173, /api isteklerini API'ye yönlendirir
```

### Test

```bash
dotnet build -warnaserror
dotnet test                       # Domain + validator + gerçek Postgres üzerinde API testleri
cd web && npm run lint && npm run build
```

Docker Hub limitine takılırsan entegrasyon testleri için farklı bir imaj verebilirsin:
`TEST_POSTGRES_IMAGE=mirror.gcr.io/library/postgres:17-alpine dotnet test`.

## Mimari

```
src/
  ExpenseTracker.Domain          Entity'ler, hava durumu hesabı (saf, paketsiz)
  ExpenseTracker.Application     CQRS (MediatR), FluentValidation, use case klasörleri
  ExpenseTracker.Infrastructure  EF Core + PostgreSQL, migration'lar, saat
  ExpenseTracker.Api             Controller'lar, kullanıcı middleware'i, ProblemDetails
tests/                           Domain, Application ve API entegrasyon testleri
web/                             React + Vite + TypeScript PWA
```

MVP'de giriş yok: tarayıcı rastgele bir kullanıcı kimliği üretir ve `X-User-Id` header'ıyla gönderir.
Bu bir parola değildir; gerçek kullanıcılar için kimlik doğrulama sonraki aşamada eklenecek.

## Claude ile çalışma

`CLAUDE.md` proje kurallarını ve "bitti" tanımını içerir. `.claude/agents/` altında
`architect`, `implementer` ve `reviewer` alt ajanları, `.claude/commands/review.md` altında
`/review` komutu var.
