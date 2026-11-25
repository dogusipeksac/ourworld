#!/usr/bin/env python3
"""
Populates Google Play Console store listing metadata (tr-TR & en-US),
contact details, 512x512 app icon, 1024x500 feature graphics, and
Phone / 7" Tablet / 10" Tablet screenshots for Our World (com.dogusipeksac.ourworld)
via the official Google Play Developer API v3.
"""

import base64
import io
import json
import os
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path
from PIL import Image

PACKAGE_NAME = "com.dogusipeksac.ourworld"
PROJECT_ROOT = Path(__file__).resolve().parent.parent
SHOTS_ROOT = PROJECT_ROOT / "release" / "store_screenshots"
APP_ICON_SRC = PROJECT_ROOT / "Assets" / "GRIDROAM" / "Art" / "Store" / "AppIcon_1024.png"

ENV_FILE = Path.home() / ".config" / "mydiary" / "release.env"


def load_service_account() -> dict:
    sa_path = os.environ.get("PLAY_JSON_KEY")
    if not sa_path and ENV_FILE.exists():
        for line in ENV_FILE.read_text(encoding="utf-8").splitlines():
            if line.startswith("PLAY_JSON_KEY="):
                sa_path = os.path.expanduser(line.split("=", 1)[1].strip().strip('"').strip("'"))
                break
    if not sa_path or not Path(sa_path).exists():
        raise SystemExit(f"PLAY_JSON_KEY not found (checked env & {ENV_FILE})")
    return json.loads(Path(sa_path).read_text(encoding="utf-8"))


def mint_access_token(sa: dict) -> str:
    now = int(time.time())
    hdr = base64.urlsafe_b64encode(json.dumps({"alg": "RS256", "typ": "JWT"}).encode()).rstrip(b"=")
    clm = base64.urlsafe_b64encode(json.dumps({
        "iss": sa["client_email"],
        "scope": "https://www.googleapis.com/auth/androidpublisher",
        "aud": "https://oauth2.googleapis.com/token",
        "iat": now,
        "exp": now + 600,
    }).encode()).rstrip(b"=")
    signing_input = hdr + b"." + clm
    with tempfile.NamedTemporaryFile("w", delete=False) as kf:
        kf.write(sa["private_key"])
        kpath = kf.name
    try:
        sig = subprocess.check_output(["openssl", "dgst", "-sha256", "-sign", kpath], input=signing_input)
    finally:
        os.unlink(kpath)
    jwt_tok = (signing_input + b"." + base64.urlsafe_b64encode(sig).rstrip(b"=")).decode()
    data = urllib.parse.urlencode({
        "grant_type": "urn:ietf:params:oauth:grant-type:jwt-bearer",
        "assertion": jwt_tok,
    }).encode()
    req = urllib.request.Request("https://oauth2.googleapis.com/token", data=data)
    with urllib.request.urlopen(req, timeout=20) as r:
        return json.loads(r.read().decode())["access_token"]


def api_json(method: str, url: str, token: str, payload=None):
    headers = {"Authorization": f"Bearer {token}"}
    body = None
    if payload is not None:
        headers["Content-Type"] = "application/json"
        body = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=body, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=30) as r:
            raw = r.read().decode("utf-8")
            return r.status, (json.loads(raw) if raw else {})
    except urllib.error.HTTPError as e:
        err_raw = e.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {e.code} on {method} {url}: {err_raw}") from e


def api_upload_png(url: str, token: str, png_bytes: bytes):
    headers = {
        "Authorization": f"Bearer {token}",
        "Content-Type": "image/png",
    }
    req = urllib.request.Request(url, data=png_bytes, headers=headers, method="POST")
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            raw = r.read().decode("utf-8")
            return r.status, (json.loads(raw) if raw else {})
    except urllib.error.HTTPError as e:
        err_raw = e.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"HTTP {e.code} uploading image to {url}: {err_raw}") from e


def build_512_icon_png() -> bytes:
    with Image.open(APP_ICON_SRC) as im:
        im = im.convert("RGBA").resize((512, 512), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        im.save(buf, format="PNG", optimize=True)
        return buf.getvalue()


LISTINGS = {
    "tr-TR": {
        "title": "Bizim Dünyamız · Açık Dünya",
        "shortDescription": "3D açık dünya: çiftlik, oto tamir, balık, 5 spor, atış poligonu ve 32 görev!",
        "fullDescription": (
            "Bizim Dünyamız (Our World), oyuncuların fikirleriyle birlikte büyüyen, capcanlı bir 3D açık dünya "
            "yaşam, çiftlik, oto tamir, sürüş, spor ve atış poligonu oyunudur!\n\n"
            "Hamidiye Mahallesi'nin samimi sokaklarından Aydos Yeşil Vadi Çiftliği'ne, Sultanbeyli Göleti'ne ve "
            "Aydos Atış Poligonu'na uzanan özgür dünyaya adım at. Yaya olarak mahalleni gez, tarlanı ekip biç, "
            "hayvanlarını besle, oto tamirhanende araçları lifte kaldırıp tamir et, gölette balık tut, evini dekore et "
            "ve 5 farklı sporda maça çık!\n\n"
            "ÖNE ÇIKAN ÖZELLİKLER:\n"
            "• Geniş Yaşayan 3D Açık Dünya: Selman Sokağı, Fatih Bulvarı Saat Kulesi, Aydos Çiftliği & Bostanı, "
            "Sultanbeyli Göleti, Hz. Osman Camii, Çay Bahçesi, Açık Yüzme Havuzu ve 8 ana cadde.\n"
            "• Tarım, Hayvancılık & Köy Pazarı: 16 bostan tarlasında ayçiçeği, domates, biber ve çilek yetiştir; "
            "inek sağ, koyun kırp, kümesten yumurta topla ve Köy Pazarı'nda sat.\n"
            "• No:73 Oto Tamir, Modifiye & Oto Yıkama: Müşteri araçlarını hidrolik lifte kaldırıp tamir et; "
            "kendi aracına metalik boya, jant, GT rüzgarlık, tavan bagajı, turbo ve nitro yükseltmeleri tak.\n"
            "• Gölet Balıkçılığı & Ev Dekorasyonu: Sultanbeyli Göleti iskelesinden 5 farklı balık türü yakala; "
            "No:48 evini koltuk takımı, akvaryum ve bahçe mangalıyla yenile.\n"
            "• 6 Bölümlük 32 Hikâye Görevi + 5 Oynanabilir Spor + Atış Poligonu: 1v1 Tenis, 3v3 Halı Saha Futbolu, "
            "2v2 Basketbol, 2v2 Voleybol, 50m Yüzme & Tramplen Dalışı ve 4 silahlı / 4 modlu Aydos Atış Poligonu.\n"
            "• 10 Türkçe & 6 İngilizce Seslendirme: Yaşa ve cinsiyete uygun konuşan komşular, gerçekçi animasyonlar, "
            "araba/bisiklet/motosiklet sürüşü ve akşam 18:30'dan itibaren yanan sokak lambaları.\n"
            "• Reklamsız & Çevrimdışı Oynanabilir: İnternet bağlantısı gerekmeden dilediğin yerde oyna; "
            "oyun içi Öneri sekmesinden fikrini yaz, sıradaki güncellemeleri birlikte şekillendirelim!"
        ),
    },
    "en-US": {
        "title": "Our World · Living Open World",
        "shortDescription": "3D open world: farming, auto garage, fishing, 5 sports, shooting & 32 missions!",
        "fullDescription": (
            "Our World is a vibrant, living 3D open-world life, farming, auto-repair, driving, sports, and "
            "shooting range game built to grow together with its community!\n\n"
            "Explore a sprawling handcrafted neighborhood stretching from 8 bustling avenues to Aydos Green Valley Farm, "
            "Sultanbeyli Lake, and Aydos Shooting Range. Plant & harvest crops, raise cows, sheep, and chickens, "
            "repair & tune cars on a hydraulic garage lift, fish off the lakeside pier, decorate your home, "
            "and jump into 5 playable sports anytime!\n\n"
            "KEY FEATURES:\n"
            "• Living 3D Open World: Roam 8 connected avenues, Aydos Farm Valley, Sultanbeyli Lake & Pine Grove, "
            "Clock Tower Plaza, swimmable pool, tea garden, and enterable interiors.\n"
            "• Farming, Livestock & Village Market: Grow sunflowers, tomatoes, peppers, and strawberries across "
            "16 beds; milk cows, shear sheep, collect eggs, and sell fresh harvest at the village stand.\n"
            "• No:73 Auto Garage, Tuning & Car Wash: Raise customer cars on a hydraulic lift for 3-step repairs, "
            "customize your car with metallic paints, rims, GT spoiler, roof rack, turbo & nitro upgrades.\n"
            "• Lake Fishing & Home Decor: Cast off Sultanbeyli Lake Pier to catch 5 fish species, upgrade your "
            "No:48 home with luxury furniture & aquarium, and grill on your backyard mangal.\n"
            "• 6-Chapter Story (32 Missions) + 5 Playable Sports + Shooting Range: Play 1v1 Clay Tennis, 3v3 Football, "
            "2v2 Basketball, 2v2 Volleyball, 50m Swimming & Acrobatic Diving, and 4-weapon / 4-mode Aydos Shooting Range.\n"
            "• Expressive Multi-Voice Audio (EN & TR): Lifelike talking neighbors, realistic animations, "
            "car/bike/motorcycle driving, and glowing streetlamps & headlights after dusk.\n"
            "• Ad-Free & Offline Playable: Enjoy the entire neighborhood offline with zero ads or paywalls, "
            "and share feature ideas right from your in-game Phone!"
        ),
    },
}


def main():
    for lang, data in LISTINGS.items():
        assert len(data["title"]) <= 30, f"{lang} title > 30 ({len(data['title'])})"
        assert len(data["shortDescription"]) <= 80, f"{lang} shortDescription > 80 ({len(data['shortDescription'])})"
        assert len(data["fullDescription"]) <= 4000, f"{lang} fullDescription > 4000"

    sa = load_service_account()
    token = mint_access_token(sa)

    base = f"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{PACKAGE_NAME}/edits"
    upload_base = f"https://androidpublisher.googleapis.com/upload/androidpublisher/v3/applications/{PACKAGE_NAME}/edits"

    _, edit = api_json("POST", base, token, {})
    edit_id = edit["id"]
    print(f"[OK] Opened Play editId={edit_id}")

    try:
        # 1. App Contact Details
        api_json("PUT", f"{base}/{edit_id}/details", token, {
            "defaultLanguage": "tr-TR",
            "contactEmail": "contact@dogusipeksac.com",
            "contactWebsite": "https://dogusipeksac.com",
        })
        print("[OK] Updated app contact details (defaultLanguage=tr-TR, email, website)")

        icon_bytes = build_512_icon_png()

        # 2. Listings & Visual Assets for both tr-TR and en-US
        for lang, listing in LISTINGS.items():
            api_json("PUT", f"{base}/{edit_id}/listings/{lang}", token, listing)
            print(f"[OK] Updated listing texts ({lang}): '{listing['title']}'")

            # App icon (512x512)
            api_json("DELETE", f"{base}/{edit_id}/listings/{lang}/icon", token)
            api_upload_png(f"{upload_base}/{edit_id}/listings/{lang}/icon?uploadType=media", token, icon_bytes)
            print(f"[OK] Uploaded 512x512 icon ({lang})")

            # Feature Graphic (1024x500)
            fg_file = SHOTS_ROOT / "google_play_feature" / (
                "feature_graphic_en_1024x500.png" if lang == "en-US" else "feature_graphic_1024x500.png"
            )
            if fg_file.exists():
                api_json("DELETE", f"{base}/{edit_id}/listings/{lang}/featureGraphic", token)
                api_upload_png(
                    f"{upload_base}/{edit_id}/listings/{lang}/featureGraphic?uploadType=media",
                    token,
                    fg_file.read_bytes(),
                )
                print(f"[OK] Uploaded featureGraphic ({lang}): {fg_file.name}")

            # Phone, 7" Tablet, 10" Tablet Screenshots
            shot_base = (SHOTS_ROOT / "en-US") if lang == "en-US" else SHOTS_ROOT
            image_sets = [
                ("phoneScreenshots", shot_base / "google_play_phone"),
                ("sevenInchScreenshots", shot_base / "google_play_tablet_7"),
                ("tenInchScreenshots", shot_base / "google_play_tablet_10"),
            ]
            for img_type, folder in image_sets:
                if not folder.exists():
                    continue
                files = sorted(p for p in folder.iterdir() if p.is_file() and p.suffix.lower() == ".png")
                if not files:
                    continue
                api_json("DELETE", f"{base}/{edit_id}/listings/{lang}/{img_type}", token)
                for f in files:
                    api_upload_png(
                        f"{upload_base}/{edit_id}/listings/{lang}/{img_type}?uploadType=media",
                        token,
                        f.read_bytes(),
                    )
                print(f"[OK] Uploaded {len(files)} {img_type} ({lang})")

        # 3. Validate & Commit Edit
        api_json("POST", f"{base}/{edit_id}:validate", token, {})
        print("[OK] Edit validated cleanly")
        api_json("POST", f"{base}/{edit_id}:commit", token, {})
        print("=== Google Play Store Listing & Visual Assets COMMITTED! ===")
    except Exception:
        try:
            api_json("DELETE", f"{base}/{edit_id}", token)
        except Exception:
            pass
        raise


if __name__ == "__main__":
    main()
