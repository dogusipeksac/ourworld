# Oyuncu Önerileri → Firebase → AI — Kurulum

Oyundaki telefonun **Öneri** sekmesi, oyuncunun yazdığı fikri Firebase Cloud Firestore'a gönderir.
Firebase SDK kullanılmaz (REST API), bu yüzden ek Unity paketi gerekmez. İnternet yoksa veya
Firebase ayarlı değilse öneri cihazda bekler ve bağlantı gelince otomatik gönderilir.

## 1. Firebase projesi (bir kez, ~10 dk)

1. https://console.firebase.google.com → **Proje ekle** → ad: `ourworld` (Analytics gerekmez).
2. **Build → Authentication → Başlayın → Sign-in method → Anonymous → Etkinleştir.**
3. **Build → Firestore Database → Veritabanı oluştur** → *Production mode* → konum `eur3 (europe-west)`.
4. Firestore → **Rules** sekmesine [`firestore.rules`](firestore.rules) içeriğini yapıştır → **Yayınla**.
   (Oyuncular yalnızca öneri *oluşturabilir*; kimse istemciden okuyamaz/silemez.)
5. **Proje ayarları (⚙) → Genel → Uygulamalarınız → Web uygulaması ekle (</>)** → takma ad `ourworld-game`.
   Çıkan yapılandırmadan `projectId` ve `apiKey` değerlerini al.
6. `Assets/GRIDROAM/Resources/FirebaseConfig.json` dosyasını doldur:

```json
{
  "projectId": "ourworld-xxxxx",
  "apiKey": "AIza...",
  "collection": "feedback"
}
```

> `apiKey` gizli bir anahtar değildir (her Firebase istemcisinde bulunur); güvenliği Firestore kuralları sağlar.
> İstersen Google Cloud Console → APIs & Services → Credentials'ta anahtarı yalnızca
> *Identity Toolkit API*, *Token Service API* ve *Cloud Firestore API* ile sınırla.

7. Unity'de **GRIDROAM → Build → 0. Store Readiness Pre-Flight Check** çalıştır; "Firebase öneri sistemi" satırı yeşil olmalı.

## 2. Test

Oyunda telefonu aç → **Öneri** → bir fikir yaz → **Gönder**. Firebase Console → Firestore →
`feedback` koleksiyonunda `status: "new"` olan belge görünmelidir.

## 3. Önerileri AI'a vermek

```bash
python3 -m venv .venv && source .venv/bin/activate
pip install -r scripts/feedback/requirements.txt
```

Firebase Console → Proje ayarları → **Hizmet hesapları → Yeni özel anahtar oluştur**. JSON'u repo
**dışına** kaydet (ör. `~/.secrets/ourworld-admin.json`). Asla commit etme.

```bash
GOOGLE_APPLICATION_CREDENTIALS=~/.secrets/ourworld-admin.json python3 scripts/feedback/export_feedback.py
```

Çıktı: `Docs/feedback/inbox_YYYY-MM-DD.md`. Bu dosyayı Claude Code'a verip örneğin şunu söyle:
"Bu önerileri grupla, en çok istenen 3 özelliği seç, plan çıkar ve uygula."
Dışa aktarılan belgeler Firestore'da `status: "exported"` olarak işaretlenir, bir dahaki sefere tekrar gelmez.

İleride bu adım haftalık otomatik bir görev (Claude Code scheduled task / GitHub Action) hâline getirilebilir.

## 4. Store gizlilik beyanları

- Google Play Data Safety ve App Store App Privacy cevapları: `Docs/STORE_LISTING_TR_EN.md` → bölüm 4.
- Gizlilik politikası güncellendi: `Docs/privacy_policy.html` (Firebase ve öneri verileri eklendi).
- iOS: `Assets/Plugins/iOS/PrivacyInfo.xcprivacy` içine *Other User Content* ve *User ID* (bağlantısız, takip yok) eklendi.
