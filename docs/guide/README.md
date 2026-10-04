# Kılavuz (PDF) üretimi

`docs/Butce-Hava-Durumu-Kilavuz.pdf` bu klasörden üretilir. Her özellik değişikliğinde güncellenir.

| Dosya | Ne |
| --- | --- |
| `kilavuz.html`, `kilavuz.css` | Kılavuzun metni ve görünümü (elle yazılır) |
| `changelog.json` | Değişiklik geçmişi; ilk kayıt kılavuzun sürümüdür |
| `schema.sql` → `schema.json` | Veritabanındaki tablo/kolon açıklamaları (`COMMENT ON`); veritabanı bölümü bundan üretilir |
| `screenshots.mjs` → `img/` | Çalışan uygulamaya örnek hane kurup ekran görüntüsü alır (gerçek veriye dokunmaz) |
| `build.mjs` | Hepsini birleştirip PDF'i yazar |

```bash
docker compose up -d --build     # uygulama çalışıyor olmalı (schema ve screenshots için)
cd docs/guide
npm install
npm run all                      # schema + screenshots + pdf
npm run pdf                      # sadece metin değiştiyse
```

Bir güncellemede yapılacaklar:

1. Yeni tablo/kolon varsa açıklamasını `src/ExpenseTracker.Infrastructure/Persistence/SchemaComments.cs`'e yaz
   (yazılmazsa `SchemaDocumentationTests` kırılır) ve migration ekle.
2. `kilavuz.html`'de ilgili bölümü güncelle (kullanım + teknik).
3. `changelog.json`'un başına yeni sürüm ekle.
4. `npm run all`, PDF'i kontrol et ve commit'le.
