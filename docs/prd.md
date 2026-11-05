# PRD — Bütçe Hava Durumu (MVP)

## Fikir

Klasik harcama uygulamaları grafik ve tablo gösterir; kullanıcı bakmayı bırakır.
Bu uygulama bütçenin durumunu **hava durumu** gibi anlatır: bugün güneşli mi,
fırtına mı geliyor? Bir bakışta anlaşılır, her gün açmak için bir sebep verir.

- **Bugünün havası:** Bugün ne kadar harcadığına ve günlük payına göre.
- **Son 7 günün havası:** Geçmiş günlerin hava şeridi.
- **Ay sonu tahmini:** Bu hızla gidersen ay sonunda hava nasıl olacak.
- **Gönül rahatlığıyla harcanabilir:** Bugün hava bozulmadan daha ne kadar harcayabilirsin.

## Kapsam (MVP)

- Aylık bütçe belirleme (ay başına bir tutar, TRY).
- Harcama ekleme (tutar, kategori, not, tarih), listeleme, silme.
- Hava durumu ekranı (yukarıdaki 4 bilgi).
- Web + mobil uyumlu PWA (telefona uygulama gibi eklenebilir).

Kapsam dışı (sonraki sürümler): çoklu para birimi, tekrarlayan harcamalar, bildirimler.
Kimlik doğrulama, geçmiş aylar ve paylaşılan (hane) bütçesi eklendi: [`prd-accounts.md`](prd-accounts.md).

## Kullanıcı kimliği

MVP'deki `X-User-Id` header'ı kaldırıldı. Kullanıcı e-posta ve şifreyle giriş yapar, kimlik cookie oturumundan gelir.
Veriler kullanıcının aktif defterine göre izole edilir. Ayrıntılar: [`prd-accounts.md`](prd-accounts.md).

## Hava durumu kuralları

Tanımlar (gün `d`, ayın bütçesi `B`):

- `S0` = ayın başından `d`'den önceki güne kadar harcanan toplam.
- `R` = `d` dahil ayın kalan gün sayısı.
- Günlük pay `A = max(0, (B − S0) / R)`.
- `T` = `d` günü harcanan toplam.
- Oran `r = T / A`.

| Hava | Koşul |
| --- | --- |
| Güneşli (`Sunny`) | `r ≤ 0.5` |
| Parçalı bulutlu (`PartlyCloudy`) | `0.5 < r ≤ 0.9` |
| Bulutlu (`Cloudy`) | `0.9 < r ≤ 1.0` |
| Yağmurlu (`Rainy`) | `1.0 < r ≤ 1.5` |
| Fırtına (`Stormy`) | `r > 1.5` ya da kalan bütçe (`B − S0`) ≤ 0 iken harcama yapılmışsa |

Özel durumlar:

- O ay için bütçe yoksa hava `null` döner; arayüz bütçe girmeye yönlendirir.
- Kalan bütçe ≤ 0 ve o gün harcama yoksa: `Cloudy` (bütçe bitti ama bugün dayanıyorsun).

**Gönül rahatlığıyla harcanabilir** = `max(0, A − T)`.

**Ay sonu tahmini:** `ortalama = (ay içi toplam, d dahil) / d.Day`,
`tahmin = ortalama × ayın gün sayısı`. Tahmin havası `tahmin / B` oranına aynı
eşiklerle uygulanır. Fark `B − tahmin` (pozitifse artan, negatifse aşım).

"Bugün" istemcinin yerel tarihidir; istemci `date=YYYY-MM-DD` gönderir.
Gönderilmezse sunucu yapılandırılmış saat dilimini (`Europe/Istanbul`) kullanır.

## Kategoriler

`Market`, `Food` (Yemek), `Transport` (Ulaşım), `Bills` (Fatura),
`Entertainment` (Eğlence), `Health` (Sağlık), `Clothing` (Giyim), `Other` (Diğer).

## Endpoint'ler

Tüm `/api` istekleri oturum ister; yoksa `401`. Hesap ve hane uçları: [`prd-accounts.md`](prd-accounts.md).

| Metot | Yol | Açıklama | Başarılı |
| --- | --- | --- | --- |
| `GET` | `/api/expenses?month=2026-09` | Ayın harcamaları, yeniden eskiye | `200` |
| `POST` | `/api/expenses` | Harcama ekle. `Idempotency-Key` opsiyonel | `201` (tekrar: `200`) |
| `DELETE` | `/api/expenses/{id}` | Harcama sil | `204` / `404` |
| `GET` | `/api/budgets/{month}` | Ayın bütçesi | `200` / `404` |
| `PUT` | `/api/budgets/{month}` | Ayın bütçesini belirle (upsert) | `200` |
| `GET` | `/api/weather?date=2026-09-30` | Hava durumu ekranı | `200` |
| `GET` | `/health` | Sağlık kontrolü (header istemez) | `200` |

Doğrulama hataları `400` + RFC 7807 `ProblemDetails` (alan bazlı `errors`).

### Doğrulama kuralları

- Tutar: `0 < tutar ≤ 1.000.000`, en fazla 2 ondalık.
- Not: en fazla 200 karakter.
- Tarih: gelecekte olamaz (istemci tarihine 1 gün tolerans), 2000'den önce olamaz.
- Bütçe: `0 < bütçe ≤ 10.000.000`. Ay formatı `yyyy-MM`.
- `Idempotency-Key`: en fazla 100 karakter.

### Idempotency

Aynı kullanıcı aynı `Idempotency-Key` ile tekrar gönderirse yeni kayıt açılmaz,
ilk kayıt `200` ile döner. `(UserId, IdempotencyKey)` unique index ile korunur;
yarışta unique ihlali yakalanıp kazanan kayıt döner. Mobilde çift dokunmayı önler.

## Loglama olayları

`ExpenseCreated`, `ExpenseDuplicateRequest`, `ExpenseDeleted`, `BudgetSet`,
`WeatherCalculated`. Structured (Serilog), sabit `EventId`. Hesap, görev ve ajan olayları
`ApplicationLog.cs`'te.

## Kabul kriterleri

1. `docker compose up` ile Postgres, API ve web ayağa kalkar; web `http://localhost:8080`.
2. Bütçe girilip harcama eklenince hava durumu kurallara göre değişir.
3. Kullanıcılar birbirinin verisini göremez.
4. Aynı `Idempotency-Key` ile iki istek tek kayıt üretir.
5. Geçersiz girdi alan bazlı `400` döner.
6. Arayüz 360 px genişlikte yatay kaydırma olmadan çalışır, telefona eklenebilir (PWA manifest).
7. Hava hesabı unit testlerle, endpoint'ler gerçek Postgres üzerinde entegrasyon testleriyle kanıtlanır.
