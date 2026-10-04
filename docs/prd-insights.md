# PRD — Ajanlar: ay değerlendirmesi ve birikim planlayıcı

Ana PRD: [`prd.md`](prd.md), yapay zekâ ilkeleri: [`prd-ai.md`](prd-ai.md), geliştirme ajanları: bu dokümanın sonu.

İki iş ajanı eklenir. İkisi de bütçe asistanıyla aynı ilkeye dayanır: **model karar verir, kod hesaplar.** Kullanıcının
okuduğu her sayı kodda hesaplanır; model hangi bilginin önemli olduğunu seçer ve rakamsız cümleler yazar. Model yoksa ya da
cevabı kullanılamazsa kural tabanlı sürüm üretilir; özellik hiçbir zaman "yapay zekâ yok" diye durmaz.

## 1. Ay değerlendirmesi ajanı

Ay bitince ajan o ayı inceler ve kısa bir değerlendirme yazar: bir başlık, öne çıkan 2-4 gerçek ve önümüzdeki ay için
bir tavsiye. Hane üyelerine bildirim gider.

### Akış

1. **Kod gerçekleri hesaplar** (`MonthFacts`): toplam ve bütçe sonucu, önceki aya göre toplam değişim, kategori bazında
   büyük artış/azalış/yeni kategori (en az ₺250 ve %25), en büyük kalem, tek seferdeki en büyük harcama, hanede kimin ne
   kadar girdiği, hiç harcama olmayan gün sayısı. Her gerçeğin bir kimliği (`total`, `cat-up:Transport`…) ve hazır Türkçe
   cümlesi vardır. Önceki ay verisi yoksa kategori karşılaştırması yapılmaz.
2. **Ajan döngüsü** (en fazla 4 model çağrısı): model her adımda JSON şemasıyla kısıtlı bir adım döner:
   - `DrillDown(Category)`: o kategorinin bu ay ve önceki ay hangi harcamalardan oluştuğunu kod hesaplar, sonuç yeni bir
     gerçek olarak (`drill:Transport`) modele geri verilir. En fazla 2 kez.
   - `Finish`: başlık, seçilen gerçeklerin kimlikleri ve tavsiye.
3. **Doğrulama:** bilinmeyen kimlikler atılır; başlık ya da tavsiye rakam ya da bağlantı/e-posta içeriyorsa kural tabanlı
   metin kullanılır. Hiç geçerli seçim yoksa en yüksek puanlı 3 gerçek alınır.

### Ne zaman

- **Otomatik:** arka plan işi (`MonthReviewScheduler`, saatte bir, `Insights:ReviewIntervalMinutes`) geçen ay harcaması
  olup değerlendirmesi olmayan defterleri bulur, her çalışmada en fazla `Insights:ReviewBatchSize` (20) değerlendirme yazar
  ve üyelere "Eylül 2026 değerlendirmen hazır: …" bildirimi gönderir. Hesaplardan önceki cihaz defterleri atlanır ve
  partiden pay yemez.
- **İstekle:** geçmiş bir ay açıkken "Değerlendir" düğmesi; "Yenile" mevcut değerlendirmeyi yeniden yazar (kimliği aynı
  kalır, bildirimler geçerli kalır). Sadece bitmiş aylar (`400`), harcaması olan aylar (`404`).

### Kurallar

| Kural | Neden |
| --- | --- |
| Defter ve ay başına tek değerlendirme (unique index) | Arka plan işi ve kullanıcı aynı anda yazarsa ikinci `409` olur, tekrar oluşmaz |
| Arka plan işi defteri kendisi verir, modelden almaz | Oturumsuz çalışır; başka defterin verisine erişim yolu yok |
| Model metninde rakam ve bağlantı yasak | Rakamlar uydurulabilir; harcama notlarıyla prompt injection bir bağlantıyı tüm üyelere taşıyabilir |
| Kural tabanlı yedek | Model yok, kota dolu ya da hata: değerlendirme yine yazılır (`generatedBy = "kurallar"`) |

## 2. Birikim planlayıcı ajanı

Kullanıcı bir hedef verir: "6 ayda ₺20.000, yaz tatili için". Ajan son ayların harcamasına bakıp hangi kategorilerden
ne kadar kısılacağını ve aylık bütçeyi önerir. **Kullanıcı onaylamadan hiçbir şey değişmez.**

### Akış

1. **Taban:** planın yapıldığı aydan önceki son 3 tam ayın harcaması, kategori bazında, harcaması olan ay sayısına bölünür.
   Hiç tam ay yoksa `409` ("en az bir tam ayın harcaması gerekiyor").
2. **Ajan döngüsü** (en fazla 4 model çağrısı):
   - `InspectCategory(Category)`: kategoride en çok neye harcandığını (notlara göre gruplu) kod hesaplar. En fazla 2 kez.
   - `Finish`: 1-3 kategori, her biri için seviye (`Light`/`Medium`/`Strong`) ve rakamsız gerekçe, bir alışkanlık önerisi.
   - Kullanıcının amaç metni talimatlara değil, kullanıcı mesajına `<amac>` etiketleri arasında girer.
3. **Hesap** (`PlanMath`):
   - Aylık birikim = hedef ÷ ay (yukarı yuvarlanır). Yeni aylık bütçe = taban − birikim (10'a aşağı yuvarlanır).
   - Birikim seçilen kategorilere seviye ağırlığıyla (az %10, orta %20, çok %35 × kategori ortalaması) orantılı dağıtılır.
   - Hiçbir kategori yarısından fazla kısılmaz; yetmezse diğer esnek kategoriler büyükten küçüğe eklenir.
   - Fatura ve Sağlık asla kısılmaz (model seçse bile atılır). Esnek: Market, Yemek, Ulaşım, Eğlence, Giyim, Diğer.
   - Hedef istenen sürede sığmıyorsa (esnek harcamaların yarısını aşıyorsa) süre uzatılır ve kullanıcıya söylenir; 24 ayı
     aşıyorsa `409`.
4. **Uygulama:** "Planı uygula" → onay → planın başladığı aydan (planın yapıldığı ayın ertesi) itibaren her ayın bütçesi
   yazılır (varsa değiştirilir). Bir kez uygulanır (`409`). Diğer hane üyelerine bildirim gider.

## Endpoint'ler

| Metot | Yol | Açıklama |
| --- | --- | --- |
| `GET` | `/api/reviews/{yyyy-MM}` | Ayın değerlendirmesi (yoksa `404`) |
| `POST` | `/api/reviews/{yyyy-MM}` | Varsa döner, yoksa yazar; `?refresh=true` yeniden yazar |
| `GET` | `/api/plans/latest` | Defterin son planı (yoksa `204`) |
| `POST` | `/api/plans` | Plan iste: `goalAmount`, `months` (1-24), `purpose` (opsiyonel, en fazla 100) |
| `POST` | `/api/plans/{id}/apply` | Planı uygula (bütçeleri yazar) |

Model çağıran uçlar (`POST /api/reviews`, `POST /api/plans`, asistan, cümleyle harcama) kullanıcı başına dakikada
`RateLimits:AiPerMinute` (20) istekle sınırlıdır: tek kullanıcı herkesin paylaştığı model kotasını tüketemez.

## Kabul kriterleri

1. Ay bitince her defter için bir değerlendirme yazılır ve üyelere bir kez bildirim gider.
2. Değerlendirmedeki ve plandaki her sayı kodda hesaplanır; modelin yazdığı rakam ya da bağlantı kullanıcıya ulaşmaz.
3. Model yokken iki özellik de kurallarla çalışır.
4. Plan fatura ve sağlığı kısmaz, hiçbir kategoriyi yarısından fazla kısmaz, sığmayan hedefi uzatır.
5. Plan onaylanmadan bütçe değişmez; onaylanınca planlanan ayların bütçesi yazılır.
6. Başka hanenin değerlendirmesi ve planı görülemez ve uygulanamaz.

## Geliştirme ajanları (`.claude/agents/`)

| Ajan | Komut | Ne yapar |
| --- | --- | --- |
| `architect` | | Plan çıkarır, kod yazmaz |
| `implementer` | | Bir aşamayı kod ve testle uygular |
| `tester` | `/test-plan` | Uç durum senaryoları çıkarır, eksik testleri yazar, çalıştırır |
| `reviewer` | `/review` | PRD ve standarda karşı salt okunur inceleme |
| `security-reviewer` | `/security-review` | İzolasyon, yetki, oturum, rate limit, prompt injection incelemesi |
| `migration-checker` | `/check-migration` | Migration'ları veri kaybı ve geri alınabilirlik açısından inceler |
| `docs-keeper` | `/update-guide` | Kılavuzu, değişiklik geçmişini, PRD'leri ve veritabanı açıklamalarını günceller, PDF üretir |

Önerilen sıra: architect → implementer → tester → security-reviewer / migration-checker → reviewer → docs-keeper.
Bu sürümdeki iki iş ajanı `security-reviewer` ile incelendi; bulduğu rate limit, amaç metninin prompt'a girişi, bağlantı
filtresi, gerekçe uzunluğu ve arka plan işinin partileme sorunları düzeltildi.
