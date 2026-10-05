# PRD — Hesaplar, geçmiş aylar ve ortak hane

Ana PRD: [`prd.md`](prd.md). Bu doküman MVP'deki "kimlik doğrulama yok" sınırını kaldırır ve iki özellik ekler.

## 1. Kayıt ve giriş

- E-posta + şifre + görünen ad ile kayıt; e-posta + şifre ile giriş; çıkış.
- **ASP.NET Core Identity:** Şifreler PBKDF2 ile hash'lenir, düz metin saklanmaz. 5 hatalı denemede hesap 5 dakika kilitlenir.
- **Oturum, HttpOnly cookie ile tutulur.** JavaScript cookie'yi okuyamaz (XSS'te token çalınamaz). `SameSite=Strict` ve
  JSON-only API, başka siteden istek atılmasını (CSRF) engeller. Web ve API aynı origin'den (nginx) sunulur.
- Şifre: en az 8 karakter, en az bir rakam.
- Kayıt/giriş uçlarında IP başına dakikada 10 istek sınırı (kaba kuvvet denemelerine karşı).
- Tüm `/api` uçları giriş ister (`/api/account/register`, `/api/account/login` ve `/health` hariç). Giriş yoksa `401`.
- `X-User-Id` header'ı kaldırıldı; kullanıcı kimliği oturumdan gelir.

**Cihazdaki eski veriyi taşıma:** Hesaplardan önce girilen veriler tarayıcıdaki rastgele bir kimliğe bağlıydı. Kayıt ya da ilk
girişte tarayıcı bu kimliği gönderir; o kimliğe ait harcama ve bütçeler kullanıcının kişisel defterine taşınır, tarayıcı
kimliği siler. Kayıtlı bir kullanıcının ya da hanenin kimliği gönderilirse istek reddedilir (başkasının verisini sahiplenme engeli).

## 2. Geçmiş aylar

- Ay seçici ile geçmiş aylara gidilir (ileri ay seçilemez). Liste, bütçe ve hava durumu seçilen aya göre gösterilir.
- Geçmiş bir ayın "havası" ayın kapanışıdır: toplam harcama / bütçe oranı (`prd.md` eşikleri).
- Harcama eklerken tarih seçicide geçmiş herhangi bir gün seçilebilir; geçmiş ay görüntülenirken varsayılan tarih o ayın son günüdür.
- Geçmiş ayların bütçesi de girilebilir/değiştirilebilir.

## 3. Ortak hane

Evli çiftler ve aileler ortak bir **defter** tutar: aynı bütçe, aynı harcama listesi, aynı hava durumu.

### Veri modeli: defter (ledger)

Her harcama ve bütçe bir deftere aittir (`LedgerId`). Kişisel defterin kimliği kullanıcının kimliği, hane defterinin kimliği
hanenin kimliğidir. Kullanıcının **aktif defteri**: bir hanedeyse hane defteri, değilse kişisel defteri. Tüm okuma ve yazmalar
aktif deftere göre filtrelenir; `UserId` sadece harcamayı kimin girdiğini gösterir.

**Defter seçici:** Hanedeki bir kullanıcı haneden ayrılmadan kişisel defterine geçebilir. Üst çubukta
"Kişisel | Hane adı" seçicisi vardır; seçim cihaz başına hatırlanır (telefon kişisel, bilgisayar hane defterinde kalabilir).
İstemci kişisel defteri `X-Ledger: personal` header'ıyla ister. Kişisel defterin kimliği kullanıcının kendi kimliği
olduğu için bu header başkasının verisine erişim yolu açmaz; hane defteri her zaman üyelikten belirlenir. Bilinmeyen değer
yok sayılır. `GET /api/account/me` aktif defteri `ledger: "household" | "personal"` olarak döner. Kişisel defterde görevler
sadece kişinin kendisine verilebilir; hane işlemleri (ayrılma, üye çıkarma) hangi defter açık olursa olsun haneye uygulanır.

Bu model sayesinde:

- Haneye katılınca eski kişisel harcamalar **kişisel kalır**; hane defteri sıfırdan başlar.
- Haneden ayrılınca kişisel deftere dönülür; hane harcamaları **hanede kalır**.
- Ayrı tablolara ya da kopyalamaya gerek yoktur.

### Akış

1. Bir kullanıcı haneyi kurar (ad verir), **kurucu** olur ve 8 karakterlik bir **davet kodu** alır.
2. Kodu eşine/ailesine iletir.
3. Katılacak kişi kodu girer. Uygulama önce önizleme gösterir: **"Aynı haneden misiniz?"** — hanenin adı ve üyeleri.
   Onaylarsa katılır. Katılmadan önce bilgilendirilir: "Bundan sonra girdiğin harcamalar hane üyeleriyle paylaşılır;
   önceki kişisel harcamaların sende kalır."

### Kurallar

| Kural | Neden |
| --- | --- |
| Bir kullanıcı aynı anda tek hanede olabilir | Aktif defter belirsiz kalmasın |
| Hanede en fazla 10 üye | Kötüye kullanım sınırı |
| Herkes tüm hane harcamalarını görür, **sadece kendi girdiğini siler** (başkasınınki `403`) | Kazara/kasıtlı silmeye karşı |
| Bütçeyi her üye değiştirebilir | Ortak karar |
| Kurucu: üye çıkarabilir, davet kodunu yenileyebilir | Kod sızarsa iptal edilebilsin |
| Kurucu ayrılırsa kuruculuk en eski üyeye geçer | Hane sahipsiz kalmasın |
| Davet kodu önizleme/katılma uçlarında kullanıcı başına dakikada 10 istek | Kod tahminine karşı |
| Davet kodu: karışan karakterler (0/O, 1/I/L) olmadan 8 karakter, ~10¹² olasılık | Okunabilir ve tahmin edilemez |

Liste her harcamada kimin girdiğini gösterir. Asistan ve anlamsal arama da aktif deftere göre çalışır.

## Endpoint'ler

| Metot | Yol | Açıklama |
| --- | --- | --- |
| `POST` | `/api/account/register` | Kayıt + giriş. `deviceUserId` opsiyonel |
| `POST` | `/api/account/login` | Giriş. `deviceUserId` opsiyonel |
| `POST` | `/api/account/logout` | Çıkış |
| `GET` | `/api/account/me` | Oturumdaki kullanıcı ve hanesi |
| `GET` | `/api/households/current` | Hanem (yoksa `204`) |
| `POST` | `/api/households` | Hane kur |
| `GET` | `/api/households/invites/{code}` | Davet önizleme ("Aynı haneden misiniz?") |
| `POST` | `/api/households/join` | Koda göre katıl |
| `POST` | `/api/households/leave` | Haneden ayrıl |
| `DELETE` | `/api/households/members/{userId}` | Üye çıkar (kurucu) |
| `POST` | `/api/households/invite-code` | Davet kodunu yenile (kurucu) |

Hata kodları: yok `404`, yetkisiz işlem `403`, zaten hanede `409`.

## Kabul kriterleri

1. Giriş yapmadan veri okunamaz ve yazılamaz.
2. İki kullanıcı birbirinin kişisel verisini göremez.
3. Aynı hanedeki iki kullanıcı ortak harcamaları ve bütçeyi görür; hane dışındaki biri göremez.
4. Üye başkasının harcamasını silemez.
5. Haneye katılınca eski kişisel harcamalar hanede görünmez; ayrılınca hane harcamaları hanede kalır.
6. Başkasının kimliğiyle cihaz verisi sahiplenilemez.
7. Geçmiş bir ay seçilip veri girilebilir ve görüntülenebilir.
