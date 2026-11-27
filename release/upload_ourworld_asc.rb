#!/usr/bin/env ruby
# frozen_string_literal: true

# Uploads complete App Store Connect metadata (tr + en-US), Categories, Review Detail,
# iPhone 6.7" & iPad 12.9"/13" screenshots, and optionally attaches a processed build
# for Our World (com.dogusipeksac.ourworld, APP_ID=6838324963).

require "jwt"
require "net/http"
require "json"
require "openssl"
require "digest"

APP_ID = ENV.fetch("ASC_APP_ID", "6838324963")
KEY_ID = ENV.fetch("ASC_KEY_ID")
ISSUER_ID = ENV.fetch("ASC_ISSUER_ID")
KEY_PATH = ENV.fetch("ASC_KEY_PATH")
VERSION_STR = ENV.fetch("IOS_MARKETING_VERSION", "1.0.11")
BUILD_NUM = ENV["IOS_BUILD_NUMBER"]
ATTACH_BUILD = ARGV.include?("--attach-build")
SKIP_SCREENSHOTS = ARGV.include?("--skip-screenshots")
SUBMIT_REVIEW = ARGV.include?("--submit-review")

PROJECT_ROOT = File.expand_path("..", __dir__)
SHOTS_ROOT = File.join(PROJECT_ROOT, "release", "store_screenshots")

KEY = OpenSSL::PKey::EC.new(File.read(KEY_PATH))

def make_token
  now = Time.now.to_i
  JWT.encode(
    { iss: ISSUER_ID, iat: now, exp: now + 600, aud: "appstoreconnect-v1" },
    KEY,
    "ES256",
    { kid: KEY_ID }
  )
end

def api_req(method, url, body = nil)
  uri = URI(url)
  cls = {
    "GET" => Net::HTTP::Get,
    "POST" => Net::HTTP::Post,
    "PATCH" => Net::HTTP::Patch,
    "DELETE" => Net::HTTP::Delete
  }[method]
  req = cls.new(uri)
  req["Authorization"] = "Bearer #{make_token}"
  req["Content-Type"] = "application/json" if body
  req.body = JSON.generate(body) if body
  res = Net::HTTP.start(uri.hostname, uri.port, use_ssl: true) { |h| h.request(req) }
  parsed = res.body && !res.body.empty? ? JSON.parse(res.body) : {}
  [res.code.to_i, parsed]
end

def check!(label, code, resp)
  if code >= 300
    warn "[ERR] #{label} (HTTP #{code}): #{JSON.pretty_generate(resp)}"
    false
  else
    puts "[OK] #{label} (HTTP #{code})"
    true
  end
end

# 1. Fetch or CREATE PREPARE_FOR_SUBMISSION App Store Version (e.g. 1.1.0 when 1.0.0 is READY_FOR_SALE)
code, vers = api_req("GET", "https://api.appstoreconnect.apple.com/v1/apps/#{APP_ID}/appStoreVersions?limit=5")
abort("Failed to list versions: #{vers}") if code >= 300
ver = (vers["data"] || []).find { |v| %w[PREPARE_FOR_SUBMISSION DEVELOPER_REJECTED REJECTED].include?(v.dig("attributes", "appStoreState")) }
if ver.nil?
  puts "No editable App Store version found (latest=#{vers.dig('data', 0, 'attributes', 'versionString')} #{vers.dig('data', 0, 'attributes', 'appStoreState')}). Creating new iOS version #{VERSION_STR}..."
  code, created = api_req("POST", "https://api.appstoreconnect.apple.com/v1/appStoreVersions", {
    data: {
      type: "appStoreVersions",
      attributes: {
        platform: "IOS",
        versionString: VERSION_STR,
        copyright: "2026 Doğuş İpeksaç",
        releaseType: "AFTER_APPROVAL"
      },
      relationships: {
        app: { data: { type: "apps", id: APP_ID } }
      }
    }
  })
  abort("Failed to create App Store version #{VERSION_STR}: #{JSON.pretty_generate(created)}") unless check!("Create AppStoreVersion #{VERSION_STR}", code, created)
  ver = created["data"]
end
ver_id = ver["id"]
puts "Target Version ID=#{ver_id} current=#{ver.dig('attributes', 'versionString')} state=#{ver.dig('attributes', 'appStoreState')}"

code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/appStoreVersions/#{ver_id}", {
  data: {
    type: "appStoreVersions",
    id: ver_id,
    attributes: {
      versionString: VERSION_STR,
      copyright: "2026 Doğuş İpeksaç",
      releaseType: "AFTER_APPROVAL"
    }
  }
})
check!("Update AppStoreVersion (#{VERSION_STR}, copyright)", code, resp)

# 2. Update AppInfo categories & localizations (tr + en-US)
_, infos = api_req("GET", "https://api.appstoreconnect.apple.com/v1/apps/#{APP_ID}/appInfos?limit=5")
app_info = (infos["data"] || []).first
if app_info
  info_id = app_info["id"]
  code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/appInfos/#{info_id}", {
    data: {
      type: "appInfos",
      id: info_id,
      relationships: {
        primaryCategory: { data: { type: "appCategories", id: "GAMES" } },
        primarySubcategoryOne: { data: { type: "appCategories", id: "GAMES_SIMULATION" } },
        primarySubcategoryTwo: { data: { type: "appCategories", id: "GAMES_ADVENTURE" } }
      }
    }
  })
  check!("Set App Categories (Games -> Simulation / Adventure)", code, resp)

  _, ilocs = api_req("GET", "https://api.appstoreconnect.apple.com/v1/appInfos/#{info_id}/appInfoLocalizations")
  iloc_map = {}
  (ilocs["data"] || []).each { |l| iloc_map[l.dig("attributes", "locale")] = l["id"] }

  app_info_locs = {
    "tr" => {
      name: "Bizim Dünyamız: Açık Dünya",
      subtitle: "Çiftlik, Araba, Spor & Poligon",
      privacyPolicyUrl: "https://dogusipeksac.com/privacy-policy/ourworld/"
    },
    "en-US" => {
      name: "Our World: 3D Open World Life",
      subtitle: "Farming, Driving, Sports & Sim",
      privacyPolicyUrl: "https://dogusipeksac.com/privacy-policy/ourworld/"
    },
    "en-GB" => {
      name: "Our World: 3D Sandbox City",
      subtitle: "Farm, Garage, Drift & 5 Sports",
      privacyPolicyUrl: "https://dogusipeksac.com/privacy-policy/ourworld/"
    }
  }

  app_info_locs.each do |loc, attrs|
    if iloc_map[loc]
      code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/appInfoLocalizations/#{iloc_map[loc]}", {
        data: { type: "appInfoLocalizations", id: iloc_map[loc], attributes: attrs }
      })
      check!("Update AppInfoLocalization (#{loc})", code, resp)
    else
      code, resp = api_req("POST", "https://api.appstoreconnect.apple.com/v1/appInfoLocalizations", {
        data: {
          type: "appInfoLocalizations",
          attributes: attrs.merge(locale: loc),
          relationships: { appInfo: { data: { type: "appInfos", id: info_id } } }
        }
      })
      check!("Create AppInfoLocalization (#{loc})", code, resp)
    end
  end

  # 2b. Sync Age Rating Declaration accurately (target-only shooting range + stylized zero-blood melee)
  _, ard_get = api_req("GET", "https://api.appstoreconnect.apple.com/v1/appInfos/#{info_id}/ageRatingDeclaration")
  ard_id = ard_get.dig("data", "id")
  if ard_id
    code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/ageRatingDeclarations/#{ard_id}", {
      data: {
        type: "ageRatingDeclarations",
        id: ard_id,
        attributes: {
          advertising: false,
          ageAssurance: false,
          alcoholTobaccoOrDrugUseOrReferences: "NONE",
          contests: "NONE",
          datingAndRelationships: false,
          gambling: false,
          gamblingSimulated: "NONE",
          gunsOrOtherWeapons: "INFREQUENT_OR_MILD",
          healthOrWellnessTopics: false,
          horrorOrFearThemes: "NONE",
          lootBox: false,
          matureOrSuggestiveThemes: "NONE",
          medicalOrTreatmentInformation: "NONE",
          messagingAndChat: false,
          parentalControls: false,
          profanityOrCrudeHumor: "NONE",
          sexualContentGraphicAndNudity: "NONE",
          sexualContentOrNudity: "NONE",
          sharedLocation: false,
          socialMedia: false,
          unrestrictedWebAccess: false,
          userGeneratedContent: false,
          violenceCartoonOrFantasy: "INFREQUENT_OR_MILD",
          violenceRealistic: "NONE",
          violenceRealisticProlongedGraphicOrSadistic: "NONE",
          ageRatingOverrideV2: "NONE",
          koreaAgeRatingOverride: "NONE"
        }
      }
    })
    check!("Update AgeRatingDeclaration (Mild Target Weapons & Cartoon Action)", code, resp)
  end
end

# 2c. Sync App Store Accessibility Nutrition Labels (iPhone & iPad — Dark Interface by default)
_, acc_get = api_req("GET", "https://api.appstoreconnect.apple.com/v1/apps/#{APP_ID}/accessibilityDeclarations")
acc_map = {}
(acc_get["data"] || []).each { |a| acc_map[a.dig("attributes", "deviceFamily")] = a["id"] }
acc_attrs = {
  supportsVoiceover: false,
  supportsVoiceControl: false,
  supportsLargerText: false,
  supportsSufficientContrast: false,
  supportsDarkInterface: true,
  supportsDifferentiateWithoutColorAlone: false,
  supportsReducedMotion: false,
  supportsCaptions: false,
  supportsAudioDescriptions: false
}
%w[IPHONE IPAD].each do |family|
  acc_id = acc_map[family]
  if acc_id
    code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/accessibilityDeclarations/#{acc_id}", {
      data: { type: "accessibilityDeclarations", id: acc_id, attributes: acc_attrs }
    })
    check!("Update AccessibilityDeclaration (#{family})", code, resp)
  else
    code, resp = api_req("POST", "https://api.appstoreconnect.apple.com/v1/accessibilityDeclarations", {
      data: {
        type: "accessibilityDeclarations",
        attributes: acc_attrs.merge(deviceFamily: family),
        relationships: { app: { data: { type: "apps", id: APP_ID } } }
      }
    })
    check!("Create AccessibilityDeclaration (#{family})", code, resp)
  end
end

# 3. Update Version Localizations (tr + en-US)
_, vlocs = api_req("GET", "https://api.appstoreconnect.apple.com/v1/appStoreVersions/#{ver_id}/appStoreVersionLocalizations")
vloc_map = {}
(vlocs["data"] || []).each { |l| vloc_map[l.dig("attributes", "locale")] = l["id"] }

desc_tr = <<~TEXT.strip
  Bizim Dünyamız (Our World), oyuncuların fikirleriyle birlikte büyüyen, capcanlı bir 3D açık dünya yaşam, çiftlik, oto tamir, sürüş, spor ve atış poligonu oyunudur!

  Hamidiye Mahallesi'nin samimi sokaklarından Aydos Yeşil Vadi Çiftliği'ne, Sultanbeyli Göleti'ne ve Aydos Atış Poligonu'na uzanan özgür dünyaya adım at. Yaya olarak mahalleni gez, tarlanı ekip biç, hayvanlarını besle, oto tamirhanende araçları lifte kaldırıp tamir et, gölette balık tut, evini dekore et ve 5 farklı sporda maça çık!

  ÖNE ÇIKAN ÖZELLİKLER:
  • Geniş Yaşayan 3D Açık Dünya: Selman Sokağı, Fatih Bulvarı Saat Kulesi, Aydos Çiftliği & Bostanı, Sultanbeyli Göleti, Hz. Osman Camii, Çay Bahçesi, Açık Yüzme Havuzu ve 8 ana cadde.
  • Tarım, Hayvancılık & Köy Pazarı: 16 bostan tarlasında ayçiçeği, domates, biber ve çilek yetiştir; inek sağ, koyun kırp, kümesten yumurta topla ve Köy Pazarı'nda sat.
  • No:73 Oto Tamir, Modifiye & Oto Yıkama: Müşteri araçlarını hidrolik lifte kaldırıp lastik, motor ve kaporta tamiratı yap; kendi aracına metalik boya, jant, GT rüzgarlık, tavan bagajı, turbo ve nitro yükseltmeleri tak.
  • Gölet Balıkçılığı & Ev Dekorasyonu: Sultanbeyli Göleti iskelesinden 5 farklı balık türü yakala; No:48 evini koltuk takımı, akvaryum ve bahçe mangalıyla yenile.
  • 6 Bölümlük 32 Hikâye Görevi + 5 Oynanabilir Spor + Atış Poligonu: 1v1 Tenis, 3v3 Halı Saha Futbolu, 2v2 Basketbol, 2v2 Voleybol, 50m Yüzme & Tramplen Dalışı ve 4 silahlı / 4 modlu Aydos Atış Poligonu; Taksi, Pizza, Temizlik ve Ev Boyama yan işleriyle kazan.
  • 10 Türkçe & 6 İngilizce Seslendirme: Yaşa ve cinsiyete uygun konuşan komşular, gerçekçi animasyonlar, araba/bisiklet/motosiklet sürüşü ve akşam 18:30'dan itibaren yanan sokak lambaları.
  • Reklamsız & Çevrimdışı Oynanabilir: İnternet bağlantısı gerekmeden dilediğin yerde oyna; oyun içi Öneri sekmesinden fikrini yaz, sıradaki güncellemeleri birlikte şekillendirelim!
TEXT

desc_en = <<~TEXT.strip
  Our World is a vibrant, living 3D open-world life, farming, auto-repair, driving, sports, and shooting range game built to grow together with its community!

  Explore a sprawling handcrafted neighborhood stretching from 8 bustling avenues to Aydos Green Valley Farm, Sultanbeyli Lake, and Aydos Shooting Range. Plant & harvest crops, raise cows, sheep, and chickens, repair & tune cars on a hydraulic garage lift, fish off the lakeside pier, decorate your home, and jump into 5 playable sports anytime!

  KEY FEATURES:
  • Living 3D Open World: Roam 8 connected avenues, Aydos Farm Valley, Sultanbeyli Lake & Pine Grove, Clock Tower Plaza, swimmable pool, tea garden, and enterable interiors.
  • Farming, Livestock & Village Market: Grow sunflowers, tomatoes, peppers, and strawberries across 16 beds; milk cows, shear sheep, collect eggs, and sell fresh harvest at the village stand.
  • No:73 Auto Garage, Tuning & Car Wash: Raise customer cars on a hydraulic lift for 3-step repairs, customize your car with metallic paints, rims, GT spoiler, roof rack, turbo & nitro upgrades, and visit the drive-through car wash.
  • Lake Fishing & Home Decor: Cast off Sultanbeyli Lake Pier to catch 5 fish species, upgrade your No:48 home with luxury furniture & aquarium, and grill on your backyard mangal.
  • 6-Chapter Story (32 Missions) + 5 Playable Sports + Shooting Range: Jump anytime into 1v1 Clay Tennis, 3v3 Football, 2v2 Basketball, 2v2 Volleyball, 50m Swimming & Acrobatic Diving, and 4-weapon / 4-mode Aydos Shooting Range, plus Taxi, Pizza, Cleanup & Painting jobs.
  • Expressive Multi-Voice Audio (EN & TR): 6 English and 10 Turkish voices for talking neighbors & range callouts, lifelike animations, multi-vehicle driving, and glowing streetlamps & headlights after dusk.
  • Ad-Free & Offline Playable: Enjoy the entire neighborhood offline with zero ads or paywalls, and share feature ideas via the in-game Suggestions tab!
TEXT

whats_new_tr = <<~WN.strip
  • YENİ: Baştan Aşağı 3D Görsel & Gerçekçilik Güncellemesi! Tüm karakterler doğal insan oranları, detaylı yüzler, parmaklı eller ve katmanlı kıyafetlerle yenilendi; 4 yeni mahalle sakini arketipi (esnaf, yaşlı amca/teyze, sportif genç) eklendi.
  • YENİ: Tüm Hayvanlar & Araçlar Yenilendi! İnek, koyun, keçi, köpek, kedi, tavuk, ördek, kuğu, güvercin ve balıklar doğal anatomi ve PBR tüy/yün/pul dokularıyla; bisiklet, motosiklet, traktör ve otomobiller detaylı 3D gövde ve tekerleklerle güncellendi.
  • YENİ: Hamidiye Binaları, Şehir Hatları Vapuru, İstiklal Tramvayı, 6 Sultanbeyli Mahallesi, Aydos Kalesi, M5 Metro, yeni çalı/kaya doğa detayları, URP yumuşak gölgeler ve 1. Şahıs kokpit kamerası!
  • İyileştirme: Yürürken oluşan fazla rüzgar/ayak sesi azaltıldı, daha doğal ve keyifli bir atmosfer sağlandı.
WN

whats_new_en = <<~WN.strip
  • NEW: Complete 3D Stylized-Realism Visual Overhaul! Every human character & NPC has been resculpted with natural anatomy, expressive faces, detailed hands & layered garments, plus 4 brand-new neighborhood citizen archetypes.
  • NEW: Upgraded Animals & Vehicles! All 11 animals (cows, sheep, goats, dogs, cats, chickens, ducks, swans, pigeons & fish) feature smooth anatomy and PBR wool/fur/feather/scale materials; bicycles, motorcycles, tractors & cars now sport sculpted 3D silhouettes.
  • NEW: Detailed Hamidiye Avenue buildings, classic Bosphorus Ferry & İstiklal Tram, 6 Sultanbeyli districts, Aydos Castle, M5 Metro, organic shrubs & mossy rocks, URP soft shadows & 1st-Person Cockpit camera!
  • Polish: Tamed walking wind & footstep ambience for a cleaner, more immersive open-world stroll.
WN

version_locs = {
  "tr" => {
    whatsNew: whats_new_tr,
    promotionalText: "YENİ 5 katlı Hamidiye Atlas AVM (18 mağaza, sinema, yemek katı), Havadis & Kadife Sokakları, zeki NPC'ler, 32 hikâye görevi ve 5 spor tek açık dünyada!",
    description: desc_tr,
    keywords: "sürüş,tamir,balık,tenis,futbol,basketbol,voleybol,yüzme,simülasyon,şehir,mahalle,çevrimdışı,offline",
    supportUrl: "https://dogusipeksac.com",
    marketingUrl: "https://dogusipeksac.com"
  },
  "en-US" => {
    whatsNew: whats_new_en,
    promotionalText: "NEW 5-level Atlas Mall (18 stores, cinema, food court), 2 new streets, smart NPCs, 32 missions, Aydos Farm, Garage & 5 live sports — ad-free & offline!",
    description: desc_en,
    keywords: "sandbox,garage,car,fishing,tennis,football,basketball,volleyball,swimming,shooting,town,city,offline",
    supportUrl: "https://dogusipeksac.com",
    marketingUrl: "https://dogusipeksac.com"
  },
  "en-GB" => {
    whatsNew: whats_new_en,
    promotionalText: "NEW 5-level Atlas Mall (18 stores, cinema, food court), 2 new streets, smart NPCs, 32 missions, Aydos Farm, Garage & 5 live sports — ad-free & offline!",
    description: desc_en,
    keywords: "istanbul,motor,bisiklet,taksi,pizza,soccer,diving,range,target,crops,market,decor,story,rpg,quest",
    supportUrl: "https://dogusipeksac.com",
    marketingUrl: "https://dogusipeksac.com"
  }
}

version_locs.each do |loc, attrs|
  if vloc_map[loc]
    code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/appStoreVersionLocalizations/#{vloc_map[loc]}", {
      data: { type: "appStoreVersionLocalizations", id: vloc_map[loc], attributes: attrs }
    })
    check!("Update VersionLocalization (#{loc})", code, resp)
  else
    code, resp = api_req("POST", "https://api.appstoreconnect.apple.com/v1/appStoreVersionLocalizations", {
      data: {
        type: "appStoreVersionLocalizations",
        attributes: attrs.merge(locale: loc),
        relationships: { appStoreVersion: { data: { type: "appStoreVersions", id: ver_id } } }
      }
    })
    if check!("Create VersionLocalization (#{loc})", code, resp)
      vloc_map[loc] = resp.dig("data", "id")
    end
  end
end

# 4. Update App Store Review Detail
review_notes = <<~NOTES.strip
  Our World (Bizim Dünyamız) is an offline-playable, ad-free 3D open-world neighborhood life, farming, auto-repair, driving, and sports game. No account, login, or in-app purchases are required.

  How to review:
  1. Launch the app. The living open-world Main Menu opens over a live 3D camera tour of the neighborhood — tap "Mahalleye Gir / Enter Neighborhood" (or pick any District / Sport directly).
  2. Use the left virtual joystick to walk, drag the right side of the screen to orbit the camera, and tap Sprint/Jump/Interact buttons on the right.
  3. Follow the 32-mission story tracker or explore freely: drive cars/bikes/motorcycles, farm & raise animals at Aydos Farm, repair & tune cars at No:73 Auto Garage, fish at Sultanbeyli Lake, jump anytime into playable Tennis, 3v3 Football, 2v2 Basketball, 2v2 Volleyball, 50m Swimming/Diving, or the 4-mode Aydos Shooting Range (sporting target/clay pigeon shooting only — weapons are strictly scoped to the range with zero NPC or animal targeting).
  4. Open Menu -> Settings & World to switch language (Turkish with 10 voices / English with 6 voices), graphics tier, voiceover toggle, Invert Y, large subtitles, or Day/Evening/Night time.
  5. No ads, no IAP, no user accounts, no direct user-to-user messaging. The optional Phone -> Ideas tab is a moderated developer feedback box. Firebase Crashlytics is used for crash diagnostics only (no Analytics / no IDFA / no tracking).
NOTES

_, rev_get = api_req("GET", "https://api.appstoreconnect.apple.com/v1/appStoreVersions/#{ver_id}/appStoreReviewDetail")
rev_id = rev_get.dig("data", "id")
rev_attrs = {
  contactFirstName: ENV.fetch("ASC_REVIEW_FIRST_NAME", "Doğuş"),
  contactLastName: ENV.fetch("ASC_REVIEW_LAST_NAME", "İpeksaç"),
  contactEmail: ENV.fetch("ASC_REVIEW_EMAIL", "contact@dogusipeksac.com"),
  demoAccountRequired: false,
  notes: review_notes
}
rev_attrs[:contactPhone] = ENV["ASC_REVIEW_PHONE"] if ENV["ASC_REVIEW_PHONE"] && !ENV["ASC_REVIEW_PHONE"].empty?
if rev_id
  code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/appStoreReviewDetails/#{rev_id}", {
    data: { type: "appStoreReviewDetails", id: rev_id, attributes: rev_attrs }
  })
  check!("Update AppStoreReviewDetail", code, resp)
else
  code, resp = api_req("POST", "https://api.appstoreconnect.apple.com/v1/appStoreReviewDetails", {
    data: {
      type: "appStoreReviewDetails",
      attributes: rev_attrs,
      relationships: { appStoreVersion: { data: { type: "appStoreVersions", id: ver_id } } }
    }
  })
  check!("Create AppStoreReviewDetail", code, resp)
end

# 4b. Fill TestFlight Beta App Review Detail & Beta Localizations (tr + en-US)
_, beta_rev = api_req("GET", "https://api.appstoreconnect.apple.com/v1/apps/#{APP_ID}/betaAppReviewDetail")
beta_rev_id = beta_rev.dig("data", "id")
if beta_rev_id
  code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/betaAppReviewDetails/#{beta_rev_id}", {
    data: { type: "betaAppReviewDetails", id: beta_rev_id, attributes: rev_attrs }
  })
  check!("Update BetaAppReviewDetail", code, resp)
end

_, blocs = api_req("GET", "https://api.appstoreconnect.apple.com/v1/apps/#{APP_ID}/betaAppLocalizations")
bloc_map = {}
(blocs["data"] || []).each { |l| bloc_map[l.dig("attributes", "locale")] = l["id"] }
beta_email = ENV.fetch("ASC_REVIEW_EMAIL", "contact@dogusipeksac.com")
beta_locs = {
  "tr" => {
    description: "Bizim Dünyamız (Our World) v1.0.0: 32 hikâye görevi, 10 Türkçe & 6 İngilizce seslendirme, Aydos Çiftliği, No:73 Oto Tamir, Gölet Balıkçılığı, 5 canlı spor, 4 modlu Aydos Atış Poligonu ve yenilenmiş arayüz.",
    feedbackEmail: beta_email,
    marketingUrl: "https://dogusipeksac.com",
    privacyPolicyUrl: "https://dogusipeksac.com/privacy-policy/ourworld/"
  },
  "en-US" => {
    description: "Our World v1.0.0: 32 story missions, 6 English & 10 Turkish voices, Aydos Farm, No:73 Auto Garage, Lake Fishing, 5 live sports, 4-mode Aydos Shooting Range, and redesigned modal UI.",
    feedbackEmail: beta_email,
    marketingUrl: "https://dogusipeksac.com",
    privacyPolicyUrl: "https://dogusipeksac.com/privacy-policy/ourworld/"
  }
}
beta_locs.each do |loc, attrs|
  if bloc_map[loc]
    code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/betaAppLocalizations/#{bloc_map[loc]}", {
      data: { type: "betaAppLocalizations", id: bloc_map[loc], attributes: attrs }
    })
    check!("Update BetaAppLocalization (#{loc})", code, resp)
  else
    code, resp = api_req("POST", "https://api.appstoreconnect.apple.com/v1/betaAppLocalizations", {
      data: {
        type: "betaAppLocalizations",
        attributes: attrs.merge(locale: loc),
        relationships: { app: { data: { type: "apps", id: APP_ID } } }
      }
    })
    check!("Create BetaAppLocalization (#{loc})", code, resp)
  end
end

# 5. Upload Screenshots for both tr and en-US (iPhone 6.7", iPhone 6.5" & iPad 12.9"/13")
def upload_screenshot_set(vloc_id, display_type, files)
  return if files.empty?
  _, sets = api_req("GET", "https://api.appstoreconnect.apple.com/v1/appStoreVersionLocalizations/#{vloc_id}/appScreenshotSets")
  set = (sets["data"] || []).find { |s| s.dig("attributes", "screenshotDisplayType") == display_type }
  if set
    set_id = set["id"]
    # Clear existing screenshots in this set so order & count are exact
    _, existing = api_req("GET", "https://api.appstoreconnect.apple.com/v1/appScreenshotSets/#{set_id}/appScreenshots")
    (existing["data"] || []).each do |sh|
      api_req("DELETE", "https://api.appstoreconnect.apple.com/v1/appScreenshots/#{sh['id']}")
    end
  else
    code, resp = api_req("POST", "https://api.appstoreconnect.apple.com/v1/appScreenshotSets", {
      data: {
        type: "appScreenshotSets",
        attributes: { screenshotDisplayType: display_type },
        relationships: {
          appStoreVersionLocalization: { data: { type: "appStoreVersionLocalizations", id: vloc_id } }
        }
      }
    })
    return unless check!("Create screenshot set #{display_type}", code, resp)
    set_id = resp.dig("data", "id")
  end

  files.each do |path|
    data = File.binread(path)
    md5 = Digest::MD5.hexdigest(data)
    code, resp = api_req("POST", "https://api.appstoreconnect.apple.com/v1/appScreenshots", {
      data: {
        type: "appScreenshots",
        attributes: { fileName: File.basename(path), fileSize: data.bytesize },
        relationships: { appScreenshotSet: { data: { type: "appScreenshotSets", id: set_id } } }
      }
    })
    next unless check!("Reserve #{display_type}/#{File.basename(path)}", code, resp)
    shot_id = resp.dig("data", "id")
    ops = resp.dig("data", "attributes", "uploadOperations") || []
    ops.each do |op|
      uri = URI(op["url"])
      req = Net::HTTP::Put.new(uri)
      (op["requestHeaders"] || []).each { |h| req[h["name"]] = h["value"] }
      req.body = data.byteslice(op["offset"], op["length"])
      res = Net::HTTP.start(uri.hostname, uri.port, use_ssl: true) { |h| h.request(req) }
      abort("Screenshot PUT failed (#{res.code}): #{res.body}") if res.code.to_i >= 300
    end
    code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/appScreenshots/#{shot_id}", {
      data: {
        type: "appScreenshots",
        id: shot_id,
        attributes: { uploaded: true, sourceFileChecksum: md5 }
      }
    })
    check!("Commit #{display_type}/#{File.basename(path)}", code, resp)
  end
end

unless SKIP_SCREENSHOTS
  {
    "tr" => SHOTS_ROOT,
    "en-US" => File.join(SHOTS_ROOT, "en-US"),
    "en-GB" => File.join(SHOTS_ROOT, "en-US")
  }.each do |loc, dir_root|
    vloc_id = vloc_map[loc]
    next unless vloc_id
    iphone67_files = Dir.glob(File.join(dir_root, "app_store_6.7", "*.png")).sort
    iphone65_files = Dir.glob(File.join(dir_root, "app_store_6.5", "*.png")).sort
    ipad_files     = Dir.glob(File.join(dir_root, "app_store_ipad_12.9", "*.png")).sort
    puts "=== Uploading screenshots for #{loc}: iPhone67=#{iphone67_files.length}, iPhone65=#{iphone65_files.length}, iPad=#{ipad_files.length} ==="
    upload_screenshot_set(vloc_id, "APP_IPHONE_67", iphone67_files)
    upload_screenshot_set(vloc_id, "APP_IPHONE_65", iphone65_files)
    upload_screenshot_set(vloc_id, "APP_IPAD_PRO_3GEN_129", ipad_files)
  end
end

# 6. Optionally attach processed build to App Store Version
if ATTACH_BUILD
  puts "Polling App Store Connect for processed build (version=#{BUILD_NUM || 'latest'})..."
  build_id = nil
  40.times do |i|
    _, bresp = api_req("GET", "https://api.appstoreconnect.apple.com/v1/builds?filter[app]=#{APP_ID}&sort=-uploadedDate&limit=5")
    b = (bresp["data"] || []).find do |item|
      st = item.dig("attributes", "processingState")
      ver_match = BUILD_NUM.nil? || BUILD_NUM.empty? || item.dig("attributes", "version") == BUILD_NUM
      ver_match && st == "VALID"
    end
    if b
      build_id = b["id"]
      puts "Found VALID build id=#{build_id} version=#{b.dig('attributes', 'version')}"
      break
    end
    first_st = bresp.dig("data", 0, "attributes", "processingState")
    puts "  Wait #{i + 1}/40: latest build state=#{first_st || 'NONE'}"
    sleep 15
  end
  if build_id
    code, resp = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/appStoreVersions/#{ver_id}/relationships/build", {
      data: { type: "builds", id: build_id }
    })
    check!("Attach build #{build_id} to version #{VERSION_STR}", code, resp)

    # Also update TestFlight "What to Test" notes for this build (tr + en-US)
    _, bbl = api_req("GET", "https://api.appstoreconnect.apple.com/v1/builds/#{build_id}/betaBuildLocalizations")
    bbl_map = {}
    (bbl["data"] || []).each { |l| bbl_map[l.dig("attributes", "locale")] = l["id"] }
    what_to_test = {
      "tr" => "v#{VERSION_STR} Build #{BUILD_NUM || '11'}: Komple 3D stilize gerçekçilik yükseltmesi (9 karakter, 11 hayvan, yeni bisiklet/motosiklet/traktör/araba, Hamidiye binaları, Boğaz Vapuru & İstiklal Tramvayı, PBR dokular, URP yumuşak gölgeler, 1. Şahıs kokpit kamerası ve azaltılmış yürüme sesi).",
      "en-US" => "v#{VERSION_STR} Build #{BUILD_NUM || '11'}: Complete 3D stylized-realism visual overhaul (9 characters, 11 animals, sculpted bikes/motorcycles/tractor/cars, articulated Hamidiye avenues, Bosphorus Ferry & İstiklal Tram, PBR textures, URP soft shadows, 1st-Person cockpit camera & cleaner walking audio)."
    }
    what_to_test.each do |loc, text|
      if bbl_map[loc]
        c2, r2 = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/betaBuildLocalizations/#{bbl_map[loc]}", {
          data: { type: "betaBuildLocalizations", id: bbl_map[loc], attributes: { whatsNew: text } }
        })
        check!("Update BetaBuildLocalization (#{loc})", c2, r2)
      else
        c2, r2 = api_req("POST", "https://api.appstoreconnect.apple.com/v1/betaBuildLocalizations", {
          data: {
            type: "betaBuildLocalizations",
            attributes: { locale: loc, whatsNew: text },
            relationships: { build: { data: { type: "builds", id: build_id } } }
          }
        })
        check!("Create BetaBuildLocalization (#{loc})", c2, r2)
      end
    end

    if SUBMIT_REVIEW
      puts "Submitting version #{VERSION_STR} (id=#{ver_id}) to App Store Review (Production)..."
      c_sub, r_sub = api_req("POST", "https://api.appstoreconnect.apple.com/v1/reviewSubmissions", {
        data: {
          type: "reviewSubmissions",
          attributes: { platform: "IOS" },
          relationships: { app: { data: { type: "apps", id: APP_ID } } }
        }
      })
      if check!("Create ReviewSubmission", c_sub, r_sub)
        sub_id = r_sub.dig("data", "id")
        c_item, r_item = api_req("POST", "https://api.appstoreconnect.apple.com/v1/reviewSubmissionItems", {
          data: {
            type: "reviewSubmissionItems",
            relationships: {
              reviewSubmission: { data: { type: "reviewSubmissions", id: sub_id } },
              appStoreVersion: { data: { type: "appStoreVersions", id: ver_id } }
            }
          }
        })
        if check!("Add AppStoreVersion to ReviewSubmission", c_item, r_item)
          c_fin, r_fin = api_req("PATCH", "https://api.appstoreconnect.apple.com/v1/reviewSubmissions/#{sub_id}", {
            data: { type: "reviewSubmissions", id: sub_id, attributes: { submitted: true } }
          })
          check!("Submit ReviewSubmission #{sub_id} to Apple App Review!", c_fin, r_fin)
        end
      end
    end
  else
    warn "Build not VALID yet — it is still processing in App Store Connect; attach once processing completes."
  end
end

puts "=== App Store Connect sync complete! ==="
