#!/usr/bin/env python3
"""
Oyuncu önerilerini Firestore'dan çekip yapay zekâya verilecek bir Markdown dosyasına yazar.

Kurulum (bir kez):
  python3 -m venv .venv && source .venv/bin/activate
  pip install -r scripts/feedback/requirements.txt
  Firebase Console > Proje Ayarları > Hizmet hesapları > "Yeni özel anahtar oluştur"
  JSON dosyasını repo DIŞINDA bir yere kaydet (ör. ~/.secrets/ourworld-admin.json)

Kullanım:
  GOOGLE_APPLICATION_CREDENTIALS=~/.secrets/ourworld-admin.json \
    python3 scripts/feedback/export_feedback.py            # yeni önerileri dışa aktar ve 'exported' işaretle
  ... export_feedback.py --dry-run                        # sadece göster, işaretleme
  ... export_feedback.py --limit 200

Çıktı: Docs/feedback/inbox_YYYY-MM-DD.md  -> Bu dosyayı Claude'a verip
"Bu önerileri grupla, en çok istenen 3 özelliği seç ve uygulama planı çıkar" diyebilirsin.
"""
import argparse
import collections
import datetime as dt
import os
import sys

try:
    import firebase_admin
    from firebase_admin import credentials, firestore
except ImportError:
    sys.exit("firebase-admin yüklü değil: pip install -r scripts/feedback/requirements.txt")

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Docs", "feedback")
CATEGORY_TR = {"idea": "Fikir", "bug": "Hata", "other": "Diğer"}


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dry-run", action="store_true")
    ap.add_argument("--limit", type=int, default=500)
    args = ap.parse_args()

    if not os.environ.get("GOOGLE_APPLICATION_CREDENTIALS"):
        sys.exit("GOOGLE_APPLICATION_CREDENTIALS ortam değişkenini servis hesabı JSON yoluna ayarla.")

    firebase_admin.initialize_app(credentials.ApplicationDefault())
    db = firestore.client()
    docs = list(
        db.collection("feedback").where("status", "==", "new").limit(args.limit).stream()
    )
    if not docs:
        print("Yeni öneri yok.")
        return

    items = []
    for d in docs:
        v = d.to_dict()
        v["_id"] = d.id
        items.append(v)
    items.sort(key=lambda x: x.get("createdAt") or dt.datetime.min.replace(tzinfo=dt.timezone.utc))

    by_cat = collections.defaultdict(list)
    for it in items:
        by_cat[it.get("category", "other")].append(it)

    today = dt.date.today().isoformat()
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, f"inbox_{today}.md")
    lines = [
        f"# Oyuncu Önerileri — {today}",
        "",
        f"Toplam {len(items)} yeni kayıt. Kategori: "
        + ", ".join(f"{CATEGORY_TR.get(k, k)} {len(v)}" for k, v in by_cat.items()),
        "",
        "> AI için görev: Benzer önerileri grupla, her grup için kaç oyuncunun istediğini yaz,",
        "> hataları önceliklendir ve bir sonraki sürüm için en fazla 3 özelliklik uygulanabilir plan öner.",
        "",
    ]
    for cat, rows in by_cat.items():
        lines.append(f"## {CATEGORY_TR.get(cat, cat)} ({len(rows)})")
        lines.append("")
        for it in rows:
            text = (it.get("text") or "").replace("\n", " ").strip()
            meta = f"v{it.get('appVersion','?')} · {it.get('platform','?')} · {it.get('language','?')} · görev {it.get('completedMissions',0)}/10 · {it.get('playMinutes',0)} dk"
            lines.append(f"- {text}  \n  _{meta}_")
        lines.append("")

    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    print(f"{len(items)} öneri yazıldı: {path}")

    if args.dry_run:
        print("--dry-run: Firestore'da işaretleme yapılmadı.")
        return

    batch = db.batch()
    for n, d in enumerate(docs, 1):
        batch.update(d.reference, {"status": "exported", "exportedAt": firestore.SERVER_TIMESTAMP})
        if n % 400 == 0:
            batch.commit()
            batch = db.batch()
    batch.commit()
    print("Hepsi 'exported' olarak işaretlendi.")


if __name__ == "__main__":
    main()
