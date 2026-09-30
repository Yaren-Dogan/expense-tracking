# Mimari Standart ve Review Listesi

Bu standart `dms-api` referans projesinin tersine mühendisliğinden çıkarıldı.
Referansın **iskeleti ve desenleri** alındı; referansta bulunan hatalar
"yapılmayacaklar" olarak bölüm 4'te listelendi.

## 1. Yapı

- Proje adı `<Ürün>.<Katman>`: `ExpenseTracker.Domain`, `.Application`, `.Infrastructure`, `.Api`.
- Bağımlılık yönü: `Api → Infrastructure → Application → Domain`. Domain paketsiz.
- Use case başına klasör: `Features/<Aggregate>/<Commands|Queries>/<UseCase>/` → request + handler + validator.
- Portlar (arayüzler) Application'da, adaptörler Infrastructure'da.
- EF ayarları entity başına `IEntityTypeConfiguration<T>`.
- HTTP'ye bağlı servisler (`HttpUserContext`) Api katmanında.
- DI kayıtları katman başına extension: `AddApplication()`, `AddInfrastructure(config)`.
- Ortak ayarlar `Directory.Build.props` (TargetFramework, Nullable, TreatWarningsAsErrors).

## 2. Katman kuralları

**Domain:** Ortak `Entity` tabanı. Durum enum ile. Değişiklik entity metodlarıyla,
invariant ihlali `DomainException`. İş hesapları (hava durumu) saf fonksiyon, test edilebilir.

**Application:** Request'ler `record`. Her command için `AbstractValidator<T>`, MediatR
`ValidationBehavior` pipeline'ı. `IAppDbContext` `DbSet` açar, repository yok.
Zaman `TimeProvider`'dan.

**Infrastructure:** `ApplyConfigurationsFromAssembly`. Kullanıcı izolasyonu global query filter.
Kritik tekillikler unique index ile. Unique ihlali `UniqueConstraintViolationException`'a çevrilir.

**Api:** İnce controller. Yeni kayıt `201`, idempotent tekrar `200`, silme `204`, yok `404`,
doğrulama `400 ProblemDetails`. Kullanıcı header'ı middleware'de zorunlu.

## 3. Desenler

| Desen | Uygulama |
| --- | --- |
| Kullanıcı izolasyonu | `X-User-Id` + middleware + global filter + sorguda açık koşul |
| Idempotency | `Idempotency-Key`, `(UserId, IdempotencyKey)` unique, yarışta kazanan döner |
| Upsert yarışı | Bütçe `(UserId, Year, Month)` unique, ihlalde yeniden okuyup günceller |
| Zaman | `TimeProvider` + yapılandırılmış saat dilimi, istemci tarihi opsiyonel |
| Structured logging | `LoggerMessage` + sabit `EventId`, PRD'deki olay adları |
| Yerel ortam | docker compose + healthcheck + `service_healthy` |

## 4. Referanstan tekrarlanmayacaklar

1. Kayıtlı ama hiç çağrılmayan servis.
2. Kimliği/tenant'ı doğrulamadan kabul etmek.
3. Paketi yükleyip validator yazmamak.
4. Kontrol-sonra-yaz'ı transaction/unique index olmadan yapmak.
5. `catch (DbUpdateException)` ile her hatayı yarış saymak.
6. Config, compose ve kodda farklı veritabanı.
7. Sabit kodlanmış süre ve limitler.
8. Ajan düşünce yorumları, ölü kod, commit'lenmiş bin/obj, düz metin gizli bilgi.

## 5. Review listesi

**Mimari**
- [ ] Bağımlılık yönü korunmuş.
- [ ] Yeni use case kendi klasöründe, request + handler + validator.
- [ ] Controller'da iş mantığı yok.
- [ ] Durum değişikliği entity metodunda.

**Doğruluk ve PRD uyumu**
- [ ] PRD maddesi ↔ kod eşlemesi tam.
- [ ] Kayıtlı her servis kullanılıyor.
- [ ] Config, compose, migration ve kod aynı veritabanını kullanıyor.

**İzolasyon ve güvenlik**
- [ ] `X-User-Id` zorunlu ve GUID.
- [ ] Her sorgu kullanıcıya göre filtreli; `IgnoreQueryFilters` yok ya da gerekçeli.
- [ ] Girdi doğrulanıyor (tutar, not, tarih, ay formatı).
- [ ] Gizli bilgi repoda değil (compose'daki yerel geliştirme şifresi hariç, açıkça belirtilmiş).

**Veri ve eşzamanlılık**
- [ ] Tekillik unique index ile korunuyor.
- [ ] Sadece unique ihlali yakalanıyor.
- [ ] Migration modelle uyumlu.

**Kod kalitesi**
- [ ] `dotnet build -warnaserror` temiz, `npm run lint` temiz.
- [ ] Loglar structured, `EventId`'li.
- [ ] Yorumlar "neden"i anlatıyor.

**Test ve teslim**
- [ ] Hava hesabı unit test, endpoint'ler entegrasyon testiyle kanıtlı.
- [ ] `docker compose up --build` temiz makinede çalışıyor.
