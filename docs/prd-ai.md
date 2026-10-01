# PRD — Yapay Zekâ Özellikleri

Ana PRD: [`prd.md`](prd.md). Bu doküman uygulamaya eklenen üç LLM özelliğini ve tasarım kararlarını anlatır.

## Özet

| Özellik | Teknik | Endpoint |
| --- | --- | --- |
| Doğal dille harcama girişi | Structured output (JSON schema) | `POST /api/expenses/parse` |
| Anlamsal harcama araması | RAG retrieval: embedding + pgvector | `GET /api/expenses/search?q=` |
| Bütçe asistanı | AI agent: tool calling + RAG | `POST /api/assistant/chat` |

Model sağlayıcısı yapılandırmayla seçilir (`Ai:Provider`). Kod `Microsoft.Extensions.AI` soyutlamalarına
(`IChatClient`, `IEmbeddingGenerator`) yazıldığı için uygulama mantığı hangi sağlayıcının kullanıldığını bilmez.

| Sağlayıcı | Sohbet / plan | Embedding | Disk | Varsayılan ajan modu |
| --- | --- | --- | --- | --- |
| `OpenAICompatible` + Google Gemini (varsayılan) | `gemini-flash-latest` | `gemini-embedding-001` (1024'e indirilir) | 0 | `Native` |
| `Ollama` (yerel) | `qwen2.5:3b` | `bge-m3` (1024) | ~7 GB | `Planned` |

`OpenAICompatible` aynı kodla OpenAI, Groq ya da Ollama'nın `/v1` ucuyla da çalışır; sadece adres, anahtar ve model adları değişir.

**Embedding boyutu:** Veritabanı kolonu `vector(1024)`. Gemini varsayılan olarak 3072 boyut döner; `FixedDimensionEmbeddingGenerator`
önce sağlayıcıdan 1024 boyut ister, uymayan sağlayıcının vektörünü ilk 1024 boyuta kesip yeniden normalize eder.
Gemini embedding modelleri buna göre eğitilmiştir (Matryoshka representation learning).

**Şemalar nullable alan içermez:** Sağlayıcılar `"type": ["string", "null"]` gibi birleşik tipleri farklı destekler;
kullanılmayan alanlar boş metin döner.

**Anahtar yoksa** uygulama yine açılır; yapay zekâ uçları `503` ve "yapılandırılmamış" mesajı döner.

## 1. Doğal dille harcama girişi

Kullanıcı "dün akşam kahveye 85 lira verdim" yazar; sistem tutar, kategori, not ve tarih önerir.
**Kayıt yapmaz:** öneri forma dolar, kullanıcı kontrol edip "Ekle"ye basar (human-in-the-loop).

Tasarım kararları:

- **LLM çıkarır, kod hesaplar.** Model tarih üretmez, sadece `daysAgo` (bugün=0, dün=1) çıkarır;
  tarihi kod hesaplar. Küçük modeller takvim aritmetiğinde hata yapar, sayı çıkarmada yapmaz.
- **JSON schema ile kısıtlı çıktı.** Kategori bir enum; model listede olmayan değer üretemez.
- **Few-shot prompt.** Kategorilerin Türkçe açıklamaları ve 3 örnek, küçük modelin doğruluğunu belirgin artırır.
- **Temperature 0.** Aynı cümle her seferinde aynı sonucu verir.
- **Çıktı doğrulanır.** Tutar ≤ 0 ya da ayrıştırılamayan cevap `422` döner; `daysAgo` 0–365'e sıkıştırılır.

## 2. Anlamsal arama (RAG retrieval)

"kahve harcamalarım" araması, notunda "kahve" kelimesi geçmeyen "Starbucks latte" ve "Espresso"
kayıtlarını da bulur.

- Her harcama `"<Kategori>: <not>"` metni olarak embedding'e çevrilip `expense_embeddings`
  tablosunda `vector(1024)` kolonunda saklanır. HNSW index, cosine mesafesi.
- **Indexleme arka planda yapılır** (`ExpenseEmbeddingIndexer`, varsayılan 10 sn). Harcama kaydı
  modele bağımlı değildir: Ollama kapalıyken de harcama eklenir, model açılınca indexlenir.
- Embedding modeli değişirse eski vektörler otomatik yeniden üretilir (`model` kolonu karşılaştırılır).
- Sonuçlar kullanıcıya göre filtrelenir ve `MinSimilarity` (0.45) altındakiler elenir.

## 3. Bütçe asistanı (agent)

Kullanıcı "Bu ay en çok nereye harcadım?" ya da "Kahveye ne kadar veriyorum?" diye sorar.
Model, cevabı uydurmak yerine araç çağırır, sonuçları okuyup Türkçe özetler.

| Araç | Ne yapar |
| --- | --- |
| `get_budget_status` | Bugünün havası, kalan bütçe, ay sonu tahmini |
| `get_spending_by_category` | Tarih aralığında kategori toplamları |
| `list_expenses` | Tarih aralığı ve kategoriye göre harcamalar (en fazla 20) |
| `search_expenses` | Anlamsal arama (RAG) |

İki çalışma modu var (`Ai:AgentMode`):

| Mod | Nasıl çalışır | Ne zaman |
| --- | --- | --- |
| `Planned` (varsayılan) | Her adımda model, JSON şemasıyla kısıtlanmış bir "plan" döner (hangi araç, hangi parametre). Aracı uygulama çalıştırır, sonucu modele geri verir. Cevaptaki sayılar araçların kodda hesaplanan `özet` cümlelerinden kurulur. | Küçük yerel modeller (3B) |
| `Native` | Modelin kendi tool calling özelliği ve `FunctionInvokingChatClient` döngüsü; cevap serbest metin. | Güçlü modeller (Claude, GPT, 7B+) |

Neden iki mod: `qwen2.5:3b` ile yapılan ölçümlerde native tool calling 3 sorudan 2'sinde hiç araç çağırmadan cevap uydurdu;
Ollama `tool_choice` (aracı zorunlu kılma) desteklemediği için bu zorlanamadı. Aynı model şemayla kısıtlı JSON'da
güvenilirdi (constrained decoding). Ancak sonuçları düzyazıya çevirirken sayıları yanlış okudu, olmayan yüzdeler uydurdu.
Bu yüzden Planned modda **model karar verir, kod raporlar.**

Tasarım kararları:

- **Araçlar kullanıcıya kapsamlıdır.** Hiçbir araç `userId` parametresi almaz; kimlik sunucudaki
  `IUserContext`'ten gelir. Model başka bir kullanıcının verisini isteyemez.
- **Döngü sınırı.** Bir istekte en fazla 5 araç turu (`MaxToolIterations`); aynı araç aynı parametreyle tekrar istenirse döngü biter.
- **Araç hataları metin döner.** Geçersiz tarih aralığında exception yerine açıklama döner; model düzeltip tekrar dener.
- **Prompt injection savunması.** Sistem prompt'u harcama notlarını talimat değil veri olarak ele almasını söyler;
  araçlar zaten salt okunur ve kullanıcıya kapsamlı olduğu için etki alanı sınırlıdır.
- **Şeffaflık.** Cevapla birlikte kullanılan araçların adları döner; arayüz bunları gösterir.
- **Sunucu durumsuz.** Sohbet geçmişi istemcide tutulur, her istekte en fazla 20 mesaj gönderilir.

## Hata davranışı

| Durum | Cevap |
| --- | --- |
| Model servisine ulaşılamıyor / zaman aşımı | `503` ProblemDetails |
| API anahtarı yok, geçersiz (401/403), kota dolu (429), model adı yanlış (404) | `503`, nedeni Türkçe açıklanır |
| Model çıktısı ayrıştırılamadı | `422` ProblemDetails |
| Girdi geçersiz (boş metin, çok uzun) | `400` ProblemDetails |

## Kabul kriterleri

1. Örnek cümlelerde doğru tutar, kategori ve tarih önerilir; kayıt kullanıcı onayıyla oluşur.
2. Anlamsal arama kelime eşleşmesi olmadan ilgili kayıtları bulur, başka kullanıcının kaydını asla döndürmez.
3. Asistan sayısal soruları araç çağırarak cevaplar; kullanılan araçlar cevapta listelenir.
4. Ollama kapalıyken harcama ekleme ve hava durumu çalışmaya devam eder.
5. Davranışlar sahte (deterministik) model istemcileriyle testlerde kanıtlanır.
