# PRD — Görevler ve bildirimler

Ana PRD: [`prd.md`](prd.md), hane modeli: [`prd-accounts.md`](prd-accounts.md).

Hanede faturalar ve ödemeler kişilere dağıtılır: "Elektrik faturası → Yaren", "İnternet faturası → Ahmet".
Görevi alan kişiye bildirim gider, son gün yaklaşınca ve geçince hatırlatılır.

## Kurallar

| Kural | Neden |
| --- | --- |
| Görev aktif deftere aittir (`LedgerId`), harcamalar gibi | Hanede ortak, hanesi olmayan için kişisel hatırlatıcı |
| Görev sadece defterdeki birine verilebilir (hane üyeleri ya da kendin) | Hane dışına iş/veri sızmasın (`400`) |
| Her üye görev verebilir | Ortak ev yönetimi |
| Tamamlayabilen: görevi alan ve görevi veren | Başkası adına yanlışlıkla tamamlanmasın (`403`) |
| Silebilen: görevi veren ve hane kurucusu | Kurucu sahipsiz görevleri temizleyebilsin (`403`) |
| Görev tek seferlik ya da aylık tekrarlı | Faturalar her ay gelir |
| Aylık görev tamamlanınca gelecek ayın görevi aynı gün açılır; 31'i kısa ayda ayın son gününe çekilir, sonraki ay 31'e döner | Sabit ödeme günü |
| Aylık görevin açık olanı silinirse seri biter | Sonraki ay sadece tamamlanınca açılır |
| "Ödendi" derken tutar opsiyonel; girilirse görevin kategorisinde (varsayılan Fatura) hane defterine harcama eklenir, tarih bugün | Bütçe havası güncel kalsın, çift giriş olmasın |
| Aynı görev iki kez tamamlanamaz (`409`); eşzamanlı iki "Ödendi" `(SeriesId, DueOn)` unique index ile engellenir | Çift harcama, çift sonraki ay olmasın |
| Haneden ayrılan ya da çıkarılan üyenin açık görevleri kurucuya geçer | Fatura göremeyen birinde kalmasın |

## Bildirimler (uygulama içi)

Üst çubukta zil ikonu ve okunmamış sayısı. Panel açılınca hepsi okundu sayılır. İstemci dakikada bir ve uygulama öne
gelince yeniler.

| Olay | Kime | Örnek metin |
| --- | --- | --- |
| Görev verildi | Görevi alan (kendine verdiyse gitmez) | Yaren sana bir görev verdi: İnternet faturası · son gün 12 Ekim |
| Son gün yaklaştı (≤ 3 gün, `Tasks:ReminderDaysBefore`) | Görevi alan | İnternet faturası için son gün yarın (12 Ekim) |
| Son gün geçti | Görevi alan | İnternet faturası gecikti: son gün 12 Ekim idi |
| Tamamlandı | Görevi veren ve (başkası yaptıysa) görevi alan | Ahmet tamamladı: İnternet faturası · ₺449,90 ödendi |
| Üye ayrıldı | Kurucu | Ahmet haneden ayrıldı, görevi sana geçti: İnternet faturası · son gün 12 Ekim |

- Hatırlatmaları arka plan işi (`TaskReminderScheduler`) açılışta ve saatte bir (`Tasks:ReminderIntervalMinutes`) üretir.
- Her (kullanıcı, görev, tür) için en fazla bir bildirim: `(user_id, task_id, kind)` unique index. İş tekrar çalışsa da
  aynı hatırlatma ikinci kez gitmez.
- Metin olay anında yazılır; görev sonradan silinse de bildirim okunur kalır. Tarih ve para elle biçimlenir
  (konteynerde kültür verisi olmasa da Türkçe ay adları ve `₺1.234,50`).

## Endpoint'ler

| Metot | Yol | Açıklama |
| --- | --- | --- |
| `GET` | `/api/tasks` | Açık görevler (son güne göre) ve son 20 tamamlanan |
| `POST` | `/api/tasks` | Görev ver: `title`, `assigneeId`, `dueOn`, `recurrence` (`None`/`Monthly`), `category` (varsayılan `Bills`) |
| `POST` | `/api/tasks/{id}/complete` | Tamamla; `amount` opsiyonel. Aylık görevde gelecek ayın görevini de döner |
| `DELETE` | `/api/tasks/{id}` | Sil |
| `GET` | `/api/notifications` | Son 30 bildirim ve okunmamış sayısı |
| `POST` | `/api/notifications/read` | Hepsini okundu yap |

Her görev cevabında `canComplete` ve `canDelete` sunucuda hesaplanır; arayüz sadece izin verilen düğmeleri gösterir.

## Kapsam dışı

Telefon push bildirimi (Web Push) ve e-posta. Altyapı bildirimleri tabloda tuttuğu için ileride aynı kayıtlardan
gönderilebilir.

## Kabul kriterleri

1. Görev verilince alan kişinin zilinde bildirim görünür; hane dışındaki biri görevi göremez ve tamamlayamaz.
2. Tutarla "Ödendi" denince harcama hane listesinde ve bütçe havasında görünür, görevi veren bildirim alır.
3. Aylık görev tamamlanınca gelecek ayın görevi açılır.
4. Son gün yaklaşınca ve geçince birer kez hatırlatma gider.
5. Ayrılan üyenin açık görevleri kurucuya geçer.
