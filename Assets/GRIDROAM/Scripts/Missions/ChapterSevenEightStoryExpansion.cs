using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.Player;
using OurWorld.UI;
using OurWorld.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace OurWorld.Missions
{
    /// <summary>
    /// Chapter 7 & Chapter 8 Epic GTA 6 Story Expansion ("Aydos'un Gölgesi · Sultanbeyli'nin Kaderi", Missions 27-32):
    /// 6 long-form, high-stakes multi-district action-thriller missions (42 total playable stages!) spanning
    /// ALL 7 Sultanbeyli districts (Hamidiye, Abdurrahmangazi, Mehmet Akif M5 Metro, Hasanpaşa Millet Parkı,
    /// Battalgazi Semt Pazarı, Mimar Sinan Kültür Parkı, and Historic Aydos Castle):
    /// - Street brawls & Boss showdown against underground syndicate boss "Kuzgun" Bora Karahan & his crews
    ///   (using full HP, Dodge Roll i-frames, Sprint-Slide & 20s Knockout combat!).
    /// - Live 2★, 3★, and 4★ Police Pursuits (EvadePolice steps) with sirens, stealth hiding & Pay'n'Spray!
    /// - Stealth Crouch infiltrations (StealthReach steps), timed cross-district chases & night runs!
    /// - Rich bilingual (TR + EN) cinematic dialogues & a 3-choice emotional Sultanbeyli Legend finale!
    /// </summary>
    public sealed class ChapterSevenEightStoryExpansion : MonoBehaviour
    {
        public static ChapterSevenEightStoryExpansion Instance { get; private set; }

        private sealed class MissionHostile
        {
            public GameObject Root;
            public Rigidbody Rb;
            public ProceduralHumanoid Humanoid;
            public string DisplayName;
            public bool IsBoss;
            public float Health;
            public float MaxHealth;
            public bool Defeated;
            public float HitCooldown;
            public float PunchCooldown;
            public float PendingPunchTimer;
            public Vector3 StumbleVel;
        }

        private struct StoryPropEntry
        {
            public GameObject Go;
            public string AnchorId;
            public string MissionId;
        }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly List<ScriptableObject> _runtimeAssets = new List<ScriptableObject>(48);
        private readonly List<Material> _ownedMats = new List<Material>(16);
        private readonly List<StoryPropEntry> _props = new List<StoryPropEntry>(12);
        private readonly List<MissionHostile> _activeHostiles = new List<MissionHostile>(8);

        private Material _gangRedMat;
        private Material _gangDarkMat;
        private Material _bossGoldMat;
        private Material _propGlowMat;
        private Material _propCaseMat;
        private Material _npcCoatBlueMat;
        private Material _npcApronGreenMat;
        private Material _npcElderGreyMat;

        private string _activeEncounterAnchorId;
        private bool _encounterActive;
        private bool _subscribed;

        // Live Top-Center GTA Boss & Gang Ambush Health Bar HUD
        private GameObject _combatHudPanel;
        private Image _combatHudBg;
        private Image _combatHudBorder;
        private Text _combatHudTitle;
        private Text _combatHudSub;
        private Image _combatHudFill;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            if (FindAnyObjectByType<MissionManager>() == null) return;

            GameObject go = new GameObject("--- CHAPTER7_8_AYDOS_SHADOW_STORY ---");
            go.AddComponent<ChapterSevenEightStoryExpansion>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            RegisterAllBilingualStrings();
        }

        private IEnumerator Start()
        {
            // Wait 1 frame so ChapterSixStoryExpansion & SultanbeyliDistrictsBuilder finish installing
            yield return null;

            BuildMaterials();
            BuildEncounterHud();
            InstallChapterSevenEightMissions();
            SpawnAllAnchorsNpcsAndProps();
            RefreshPropVisibility();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted += HandleIdleStoryNpcTalk;
                PlayerModeController.Instance.RefreshInteractablesCache();
                _subscribed = true;
            }
        }

        private void OnDestroy()
        {
            ClearActiveHostiles();
            if (_subscribed && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.OnInteracted -= HandleIdleStoryNpcTalk;
            }
            for (int i = 0; i < _runtimeAssets.Count; i++)
            {
                if (_runtimeAssets[i] != null) Destroy(_runtimeAssets[i]);
            }
            for (int i = 0; i < _ownedMats.Count; i++)
            {
                if (_ownedMats[i] != null) Destroy(_ownedMats[i]);
            }
            if (Instance == this) Instance = null;
        }

        // =====================================================================
        // 1. BILINGUAL STORY SCRIPT (TR + EN) — 6 LONG-FORM MISSIONS (M27-M32)
        // =====================================================================
        private static void T(string key, string tr, string en) => LocalizationService.RegisterRuntimeText(key, tr, en);

        private void RegisterAllBilingualStrings()
        {
            // Speakers & Failure Notices
            T("npc.emre.name",       "Muhbir Emre",           "Informant Emre");
            T("npc.ismail.name",     "Pazarcı İsmail Amca",   "Bazaar Vendor İsmail");
            T("npc.kemal_dede.name", "Kemal Dede",            "Elder Kemal");
            T("npc.demir.name",      "Demir (Kuzgun'un Sağ Kolu)", "Demir (Kuzgun's Lieutenant)");
            T("npc.haluk_kale.name", "Usta Haluk",            "Master Haluk");
            T("ch78.fail.busted",    "Polise yakalandın! Takibi atlatmak için tekrar dene.", "You were busted by the police! Retry to shake the pursuit.");
            T("ch78.fail.ko",        "Çete çatışmasında bayıldın! Canın yenilendi — tekrar denemek için R'ye bas.", "Knocked out in the ambush! Your health is restored — press Retry to jump back in.");

            // --- MISSION 27: Karanlık Teklif: Kent Meydanı'nda Fırtına (7 Stages) ---
            T("m27.title",   "Bölüm 7 · Karanlık Teklif", "Chapter 7 · Dark Offer");
            T("m27.summary", "Kuzgun çetesi Sultanbeyli'ye sızdı. Kent Meydanı'nda iz sür, pusuyu kır ve çete konvoyundan kaç.", "The Kuzgun syndicate moved into Sultanbeyli. Investigate City Square, survive the ambush, and outrun their getaway crew.");
            T("m27.s1.obj",  "Abdurrahmangazi Kent Meydanı'nda Muhbir Emre ile buluş", "Meet Informant Emre at Abdurrahmangazi City Square");
            T("m27.d1.1",    "Deniz, sonunda geldin! Sultanbeyli'de büyük bir bela var — 'Kuzgun' lakaplı Bora Karahan bütün mahalleleri haraca bağlamak istiyor.", "Deniz, glad you made it! Big trouble in Sultanbeyli — an underground boss named 'Kuzgun' Bora Karahan is extorting local shops.");
            T("m27.d1.2",    "Belediye binasının yan merdivenlerinde çetenin şifreli harita çantasını bıraktıklarını gördüm. Al ama dikkat et, gözcüleri etrafta!", "I spotted their encrypted map briefcase near City Hall steps. Grab it, but watch out — lookouts are watching!");
            T("m27.s2.obj",  "Belediye yanındaki Kuzgun Harita Çantasını incele", "Inspect the Kuzgun Map Briefcase near City Hall");
            T("m27.d2.1",    "Haritada 7 mahallenin tamamı işaretlenmiş... Hey, arkadan ayak sesleri geliyor — pusu kurmuşlar!", "All 7 districts are marked on this map... Wait, footsteps behind me — it's an ambush!");
            T("m27.s3.obj",  "Pusu kuran 2 Kuzgun Gözcüsünü dövüşte bayılt!", "Knock out the 2 Kuzgun Lookouts in the ambush!");
            T("m27.d3.1",    "İkisi de yerde! Telsizden biri Mehmet Akif M5 Metro'ya doğru kaçtıklarını söylüyor — hemen arabaya atlayıp yetişmeliyim!", "Both are down! Their radio says another courier is fleeing to Mehmet Akif M5 Metro — gotta drive there fast!");
            T("m27.s4.obj",  "Süre dolmadan araçla Mehmet Akif M5 Metro'ya yetiş! (75 sn)", "Drive to Mehmet Akif M5 Metro before time runs out! (75s)");
            T("m27.s5.obj",  "M5 Metro girişindeki Korsan Telsizi ele geçir", "Grab the Pirate Radio at the M5 Metro entrance");
            T("m27.d5.1",    "Telsiz elimizde! Kuzgun'un destek minibüsleri bulvara girdi — gaza basıp Kent Meydanı'na yetişmeliyiz!", "Got the radio! Kuzgun's backup vans just hit the boulevard — floor it back to City Square!");
            T("m27.s6.obj",  "Süre dolmadan araçla Kent Meydanı'na kaçış sürüşü yap! (65 sn)", "Race back to Abdurrahmangazi Square by car before time runs out! (65s)");
            T("m27.d6.1",    "Takipçileri geride bıraktık! Şimdi telsizdeki koordinatları Emre'ye çözdürelim.", "Left their crew in the dust! Time to bring these radio coordinates back to Emre.");
            T("m27.s7.obj",  "Abdurrahmangazi Kent Meydanı'nda Emre'ye rapor ver", "Report back to Emre at Abdurrahmangazi Square");
            T("m27.d7.1",    "Helal olsun Deniz! Telsiz kaydına göre sıradaki hedefleri Battalgazi Semt Pazarı. Esnaf tehlikede, hemen oraya gitmelisin!", "Incredible work Deniz! According to the recording, their next hit is Battalgazi Bazaar. The vendors need you!");

            // --- MISSION 28: Bosna Bulvarı Baskını: Pazarın Koruyucusu (7 Stages) ---
            T("m28.title",   "Bölüm 7 · Bosna Bulvarı Baskını", "Chapter 7 · Bosna Blvd Raid");
            T("m28.summary", "Battalgazi Semt Pazarı'nı Kuzgun çetesinden koru, çalınan parayı kurtar ve Hasanpaşa Parkı'na gizlice sız.", "Defend Battalgazi Bazaar from Kuzgun's crew, recover the stolen cashbox, and infiltrate Hasanpaşa Park in stealth.");
            T("m28.s1.obj",  "Battalgazi Semt Pazarı'nda Pazarcı İsmail Amca ile konuş", "Talk to Vendor İsmail at Battalgazi Bazaar");
            T("m28.d1.1",    "Deniz evladım, gözünü seveyim yetiş! Kuzgun'un üç haydutu bütün pazarın haftalık kazancını çalıp futbol sahasının arkasına kaçırdı!", "Deniz, thank goodness you're here! Three of Kuzgun's thugs stole the market's entire earnings and stashed them behind the football pitch!");
            T("m28.d1.2",    "O kasa geri gelmezse 20 aile bu hafta ekmeksiz kalır... Lütfen yardım et!", "Without that cashbox, twenty families won't make rent this week... Please help us!");
            T("m28.s2.obj",  "Battalgazi futbol sahası arkasındaki Pazar Gelir Kutusunu bul", "Find the Stolen Bazaar Cashbox behind Battalgazi pitch");
            T("m28.d2.1",    "Kasa burada! Ama çete etrafımı sardı — kaçış yok, hepsini indirme vakti!", "Found the cashbox! Wait — they surrounded me. Time to take them all down!");
            T("m28.s3.obj",  "3 Kuzgun Çete Üyesini bayılt! (Takla için X / Eğil için C)", "Knock out all 3 Kuzgun Thugs! (Press X to Dodge Roll)");
            T("m28.d3.1",    "Üçü de bayıldı! Çetenin destek aracı gelmeden kasayı güvenli noktaya, Hasanpaşa Millet Parkı'na yetiştireyim!", "All three knocked out! Gotta drive this cashbox to safety at Hasanpaşa Millet Park before backup arrives!");
            T("m28.s4.obj",  "Süre dolmadan araçla Hasanpaşa Millet Parkı'na ulaş! (75 sn)", "Drive swiftly to Hasanpaşa Millet Park! (75s)");
            T("m28.s5.obj",  "Eğilerek (C / Eğil) park pergolasına sessizce sız ve kuryeyi dinle", "Crouch (C) and sneak quietly to the park pergola to eavesdrop");
            T("m28.d5.1",    "Kurye gece 22:00'de M5 Metro hattından kaçak sevkiyat yapılacağını söyledi... Eyvah, fark edildim!", "The courier just leaked a midnight smuggling run on the M5 Metro line... Uh oh, spotted!");
            T("m28.s6.obj",  "Seni fark eden Kurye ve Korumasını bayılt!", "Knock out the Courier and Bodyguard who spotted you!");
            T("m28.s7.obj",  "Battalgazi Semt Pazarı'na dön ve parayı İsmail Amca'ya teslim et", "Return the cashbox to Vendor İsmail at Battalgazi Bazaar");
            T("m28.d7.1",    "Allah senden razı olsun Deniz! Bütün Battalgazi senin arkanda! Bu gece M5 Metro'da dikkatli ol aslanım.", "Bless you Deniz! All of Battalgazi has your back! Be careful tonight at the M5 Metro.");

            // --- MISSION 29: Geceyarısı M5 Hattı: Hızlı Kaçış (7 Stages · Night 22:00!) ---
            T("m29.title",   "Bölüm 7 · Geceyarısı M5 Hattı", "Chapter 7 · Midnight M5 Run");
            T("m29.summary", "Gece yarısı iki M5 Metro istasyonu arasındaki kaçak sevkiyatı sabote et ve zamanla yarışarak Gölet'ten Kent Meydanı'na kaç!", "Sabotage the midnight smuggling drop between both M5 Metro stations and race through the night from the Lake back to City Square!");
            T("m29.s1.obj",  "Gece Mehmet Akif M5 Metro önünde Muhbir Emre ile buluş", "Meet Informant Emre at Mehmet Akif M5 Metro at night");
            T("m29.d1.1",    "Saat 22:00 oldu Deniz, sokaklar ıssız. Kuzgun'un kaçak sandığı az önce Hasanpaşa M5 Metro meydanına indirildi.", "It's 10 PM, streets are quiet. Kuzgun's smuggled crate was just dropped at Hasanpaşa M5 Metro Plaza.");
            T("m29.d1.2",    "Arabana atla, Fatih Bulvarı'ndan son sürat basıp o sandığı dağıt — ama dikkat et, alarm tüm şehri ayağa kaldıracak!", "Hop in your car, blast down Fatih Blvd and sabotage that crate — just know the alarm will wake the whole district!");
            T("m29.s2.obj",  "Gece yarışı için aracına bin", "Get into a vehicle for the midnight run");
            T("m29.s3.obj",  "Süre dolmadan araçla Hasanpaşa M5 Metro'ya yetiş! (60 sn)", "Race to Hasanpaşa M5 Metro before time runs out! (60s)");
            T("m29.s4.obj",  "Hasanpaşa M5 Metro önündeki Kaçak Sandığı sabote et", "Sabotage the Smuggled Crate at Hasanpaşa M5 Metro");
            T("m29.d4.1",    "Sandık patlatıldı, sevkiyat iptal! Alarm çalıyor — Gölet'e doğru son gaz kaç!", "Crate disabled, shipment ruined! Alarm's blaring — floor it toward Sultanbeyli Lake!");
            T("m29.s5.obj",  "Süre dolmadan araçla Sultanbeyli Göleti iskelesine kaç! (60 sn)", "Escape by car to Sultanbeyli Lake Pier! (60s)");
            T("m29.s6.obj",  "Gece tam gaz! Süre dolmadan araçla Abdurrahmangazi Kent Meydanı'na dön! (75 sn)", "Midnight getaway! Race by car back to Abdurrahmangazi Square! (75s)");
            T("m29.d6.1",    "İnanılmaz bir gece sürüşüydü! Kuzgun'un bütün sevkiyat planı suya düştü.", "What an insane midnight getaway! Kuzgun's entire smuggling run is ruined.");
            T("m29.s7.obj",  "Kent Meydanı'nda Emre ile buluşup yeni istihbaratı al", "Meet Emre at Kent Square for the next lead");
            T("m29.d7.1",    "Kuzgun çıldırdı Deniz! Mimar Sinan Kültür Parkı'ndaki çeşmeye son buluşmanın şifresini bırakmışlar. Kemal Dede seni bekliyor.", "Kuzgun is furious! His lieutenant hid the final meeting cipher at Mimar Sinan Park fountain. Elder Kemal is waiting.");

            // --- MISSION 30: Mimar Sinan'ın Şifresi: Gizli Takip (7 Stages · Sunset 18:15!) ---
            T("m30.title",   "Bölüm 8 · Mimar Sinan'ın Şifresi", "Chapter 8 · Mimar Sinan Cipher");
            T("m30.summary", "Mimar Sinan Parkı'ndan şifreli planı sessizce çal, Atlas AVM'de Kuzgun'un sağ kolunu durdur ve kalenin sırrını öğren.", "Stealthily steal the cipher at Mimar Sinan Park, defeat Kuzgun's lieutenant at Atlas Mall, and uncover the castle plot.");
            T("m30.s1.obj",  "Mimar Sinan Kültür Parkı çeşmesinde Kemal Dede ile konuş", "Speak with Elder Kemal at Mimar Sinan Park Fountain");
            T("m30.d1.1",    "Hoş geldin yavrum... Siyah paltolu bir adam az önce çeşmenin arkasına kırmızı mühürlü bir zarf sakladı.", "Welcome my child... A man in a black coat just hid a red-sealed envelope behind the stone fountain.");
            T("m30.d1.2",    "Parkta çocuklar oynuyor, kimseyi korkutma. Eğilerek sessizce çeşmenin arkasına sız ve zarfı al evladım.", "Kids are playing nearby, don't startle anyone. Crouch low, sneak behind the fountain and take the envelope.");
            T("m30.s2.obj",  "Eğilerek (C) çeşme arkasına sessizce sız", "Crouch (C) and sneak behind the stone fountain");
            T("m30.s3.obj",  "Şifreli Aydos Kalesi Plan Zarfını incele", "Inspect the Encrypted Aydos Castle Plan");
            T("m30.d3.1",    "Zarf çözüldü! Kuzgun'un sağ kolu Demir şu an Hamidiye Atlas AVM önünde konvoy topluyor! Hemen keselim!", "Decoded! Kuzgun's lieutenant Demir is gathering a convoy outside Hamidiye Atlas Mall right now!");
            T("m30.s4.obj",  "Süre dolmadan araçla Hamidiye Atlas AVM meydanına yetiş! (70 sn)", "Race to Hamidiye Atlas Mall Plaza before they leave! (70s)");
            T("m30.s5.obj",  "AVM meydanındaki 3 Elit Korumayı bayılt!", "Knock out Demir's 3 Elite Bodyguards outside Atlas Mall!");
            T("m30.s6.obj",  "Teslim olan Demir'i sorgula", "Interrogate Demir after taking out his guards");
            T("m30.d6.1",    "Tamam, vurma! Söylüyorum... Bora bu gece saat 23:00'te Tarihi Aydos Kalesi'nde tüm Sultanbeyli esnafının tapu sandığını yakacak!", "Alright, don't hit me! I'll talk... Tonight at 11 PM Bora is burning all the Sultanbeyli merchants' deeds atop Historic Aydos Castle!");
            T("m30.d6.2",    "Turgut Reis'ten kaleye çıkan taş rampada nöbetçiler var. Sakın tek başına dalma Deniz!", "Guards are posted on the stone ramp from Turgut Reis. Don't charge in blind!");
            T("m30.s7.obj",  "Araçla Turgut Reis · Demokrasi Caddesi buluşma noktasına git", "Drive to the Turgut Reis · Demokrasi Ave rally point");

            // --- MISSION 31: Aydos Kalesi Kuşatması (8 Stages · Night 23:00 · EPIC BOSS BATTLE!) ---
            T("m31.title",   "Bölüm 8 · Aydos Kalesi Kuşatması", "Chapter 8 · Siege of Aydos Castle");
            T("m31.summary", "Gece yarısı Tarihi Aydos Kalesi'ne sız, Boss Kuzgun Bora Karahan'ı indir, esnaf tapularını kurtar ve zaferle meydana dön!", "Infiltrate Historic Aydos Castle at midnight, defeat Boss Kuzgun Bora Karahan, recover the deeds, and race to victory!");
            T("m31.s1.obj",  "Turgut Reis Çam Korusu'nda Usta Haluk ile son planı yap", "Finalize the plan with Master Haluk in Turgut Reis Pine Grove");
            T("m31.d1.1",    "Deniz, bu gece Sultanbeyli'nin kaderi belirleniyor. Tarihi Aydos Kalesi'nin surları Kuzgun'un en sert adamlarıyla dolu.", "Deniz, Sultanbeyli's fate is decided tonight. Historic Aydos Castle is crawling with Kuzgun's toughest crew.");
            T("m31.d1.2",    "Önce taş rampanın dibine eğilerek sız, kapı nöbetçilerini indir, sonra avluda Bora'nın işini bitir. Arkandayız aslanım!", "Sneak to the ramp base first, knock out the gate guards, then finish Bora inside the courtyard. We're behind you!");
            T("m31.s2.obj",  "Eğilerek (C) Aydos Kalesi taş rampasının eteklerine gizlice sız", "Crouch (C) and sneak to the base of Aydos Castle's stone ramp");
            T("m31.s3.obj",  "Kale girişini tutan 2 Kale Nöbetçisini bayılt!", "Knock out the 2 Castle Gate Guards!");
            T("m31.s4.obj",  "Taş rampadan tırmanıp Tarihi Aydos Kalesi Avlusuna gir", "Climb the stone ramp into Historic Aydos Castle Courtyard");
            T("m31.d4.1",    "Kuzgun Bora: 'Demek tüm planlarımı bozan meşhur Deniz sensin! Gel bakalım, Sultanbeyli'nin tek hakimi kimmiş görelim!'", "Kuzgun Bora: 'So you're the famous Deniz ruining my plans! Step up — let's see who really owns Sultanbeyli!'");
            T("m31.s5.obj",  "FİNAL KAVGASI: Boss 'Kuzgun' Bora Karahan ve 3 Elit Adamını bayılt!", "BOSS FIGHT: Knock out 'Kuzgun' Bora Karahan & his 3 Elite Guards!");
            T("m31.d5.1",    "Kuzgun Bora yere serildi! Çete dağıldı! Hemen seyir terasındaki Tapu Sandığını alalım!", "Kuzgun Bora is knocked out cold! The syndicate is finished! Grab the Deeds Chest on the viewing deck!");
            T("m31.s6.obj",  "Aydos Kalesi seyir terasındaki Esnaf Tapu Sandığını kurtar", "Recover the Merchants' Deeds Chest on the castle deck");
            T("m31.d6.1",    "Sandık güvende! Kaleden hızla inip şafak sökmeden sandığı Kent Meydanı'na yetiştir!", "Got the chest! Race down from Aydos Castle and deliver the deeds to Abdurrahmangazi Square!");
            T("m31.s7.obj",  "Süre dolmadan araçla sandığı Abdurrahmangazi Kent Meydanı'na yetiştir! (85 sn)", "Deliver the chest by car to Abdurrahmangazi Square! (85s)");
            T("m31.s8.obj",  "Son zafer koşusu: Araçla Fatih Bulvarı Saat Kulesi Meydanı'na ulaş! (60 sn)", "Final victory dash: Drive to Fatih Blvd Clock Tower Plaza! (60s)");

            // --- MISSION 32: Sultanbeyli'nin Efsanesi (6 Stages · Sunrise 07:30 · 3-Choice Finale!) ---
            T("m32.title",   "Bölüm 8 Final · Sultanbeyli'nin Efsanesi", "Chapter 8 Finale · Legend of Sultanbeyli");
            T("m32.summary", "Şafak vakti tüm Sultanbeyli seni bekliyor! Tapuları teslim et, zafer turu at ve büyük final kararını ver.", "All of Sultanbeyli awaits at sunrise! Return the deeds, take a victory lap, and choose your legend's path.");
            T("m32.s1.obj",  "Şafak vakti Abdurrahmangazi Kent Meydanı'nda Usta Haluk ile buluş", "Meet Master Haluk at Abdurrahmangazi Square at sunrise");
            T("m32.d1.1",    "Başardın Deniz!! Kuzgun çetesi bitti, 7 mahallenin tamamı sabah haberlerinde senin adını konuşuyor!", "You did it Deniz!! The Kuzgun syndicate is done — all 7 districts are chanting your name this morning!");
            T("m32.d1.2",    "Haydi tapu sandığını meydan kürsüsüne bırak da tüm esnaf derin bir nefes alsın!", "Place the deeds chest on the square podium so every shopkeeper can breathe easy!");
            T("m32.s2.obj",  "Esnaf Tapu Sandığını Kent Meydanı kürsüsüne teslim et", "Place the Deeds Chest on the City Square podium");
            T("m32.d2.1",    "Alkışlar bütün meydanı inletti! Şimdi aracınla Fatih Bulvarı Saat Kulesi'nden bir şeref turu atıp evine dön!", "The whole square erupted in cheers! Take a victory lap past Fatih Blvd Clock Tower and head home!");
            T("m32.s3.obj",  "Araçla Fatih Bulvarı Saat Kulesi'nden zafer turu at", "Drive a victory lap past Fatih Blvd Clock Tower");
            T("m32.s4.obj",  "Selman Sokağı No:48 evinin önüne dön", "Return home to No:48 Selman Street");
            T("m32.s5.obj",  "Kent Meydanı'nda Usta Haluk ile büyük final kararını ver", "Make your final destiny choice with Master Haluk at Kent Square");
            T("m32.d5.1",    "Deniz, Hamidiye'den Aydos Kalesi'ne kadar tüm Sultanbeyli'nin efsanesi oldun. Bundan sonra bu şehirde nasıl anılmak istersin?", "Deniz, from Hamidiye to Aydos Castle, you've become a true legend. How will Sultanbeyli remember your name?");
            T("m32.c1.lbl",  "Mahallenin Koruyucusu (Esnafın ve sokakların güvenliği benden sorulur!)", "Guardian of the Streets (Every neighbor & shop stays safe on my watch!)");
            T("m32.c1.f1",   "Usta Haluk: 'İşte gerçek delikanlılık budur Deniz! 7 mahallenin kapısı sana her zaman açık!'", "Master Haluk: 'True heart right there, Deniz! Every door across all 7 districts is always open to you!'");
            T("m32.c2.lbl",  "Bulvarların Hız Kralı (Drift, nitro ve tüm Stunt rampaları benim!)", "King of the Boulevards (Drift, nitro & every Stunt Jump is mine!)");
            T("m32.c2.f1",   "Usta Haluk: 'Egzozun sesi Aydos'tan duyulsun o zaman! Garajım her zaman bedava emrinde!'", "Master Haluk: 'Let that exhaust roar all the way to Aydos! My garage is always yours!'");
            T("m32.c3.lbl",  "Aydos'tan İstanbul'a (Hem ticaretimi hem kendi dünyamı büyüteceğim!)", "From Aydos to Istanbul (I'll grow my empire and my own world!)");
            T("m32.c3.f1",   "Usta Haluk: 'Büyük düşünen büyük kazanır Deniz! Bizim Dünyamız seninle daha da parlayacak!'", "Master Haluk: 'Dream big, win big Deniz! Our World shines brighter because of you!'");
            T("m32.s6.obj",  "Kutlama için Fatih Saat Kulesi Meydanı'na uğra — Özgür Sultanbeyli senin!", "Swing by Fatih Clock Tower Plaza for the celebration — Sultanbeyli is yours!");
        }

        // =====================================================================
        // 2. INSTALL MISSIONS 27-32 INTO MISSION CATALOG (CHAINS AFTER M26!)
        // =====================================================================
        private void InstallChapterSevenEightMissions()
        {
            MissionManager mm = MissionManager.Instance ?? FindAnyObjectByType<MissionManager>();
            if (mm == null || mm.Catalog == null || mm.Catalog.missions == null) return;
            if (mm.Catalog.GetById("m27_karanlik_teklif") != null) return;

            MissionData m26 = mm.Catalog.GetById("m26_hamidiye_yaz_soleni");
            if (m26 != null && string.IsNullOrEmpty(m26.nextMissionId))
            {
                m26.nextMissionId = "m27_karanlik_teklif";
                m26.autoStartNextMission = true;
            }

            // --- MISSION 27 (7 Stages) ---
            MissionData m27 = MakeMission("m27_karanlik_teklif", "m27.title", "m27.summary", 450, "m28_bosna_baskini", "retry_m27_p", "retry_m27_v");
            m27.steps.Add(MakeTalk("m27.s1.obj", "npc_muhbir_emre", MakeDlg(("npc.emre.name", "m27.d1.1"), ("npc.emre.name", "m27.d1.2"))));
            m27.steps.Add(MakeInspect("m27.s2.obj", "m27_kuzgun_briefcase", MakeDlg(("npc.player.name", "m27.d2.1"))));
            m27.steps.Add(MakeFight("m27.s3.obj", "m27_ambush_meydan", 2, MakeDlg(("npc.player.name", "m27.d3.1"))));
            m27.steps.Add(MakeReach("m27.s4.obj", "m27_metro_intercept", radius: 11f, requiresVehicle: true, timeLimit: 75f));
            m27.steps.Add(MakeInspect("m27.s5.obj", "m27_pirate_radio", MakeDlg(("npc.player.name", "m27.d5.1"))));
            m27.steps.Add(MakeReach("m27.s6.obj", "m31_meydan_delivery", radius: 13f, requiresVehicle: true, timeLimit: 65f, MakeDlg(("npc.player.name", "m27.d6.1"))));
            m27.steps.Add(MakeTalk("m27.s7.obj", "npc_muhbir_emre", MakeDlg(("npc.emre.name", "m27.d7.1"))));

            // --- MISSION 28 (7 Stages) ---
            MissionData m28 = MakeMission("m28_bosna_baskini", "m28.title", "m28.summary", 520, "m29_geceyarisi_m5", "retry_m28_p", "retry_m28_v");
            m28.steps.Add(MakeTalk("m28.s1.obj", "npc_pazarci_ismail", MakeDlg(("npc.ismail.name", "m28.d1.1"), ("npc.ismail.name", "m28.d1.2"))));
            m28.steps.Add(MakeInspect("m28.s2.obj", "m28_stolen_cashbox", MakeDlg(("npc.player.name", "m28.d2.1"))));
            m28.steps.Add(MakeFight("m28.s3.obj", "m28_ambush_pazar", 3, MakeDlg(("npc.player.name", "m28.d3.1"))));
            m28.steps.Add(MakeReach("m28.s4.obj", "m28_hasanpasa_dropoff", radius: 11f, requiresVehicle: true, timeLimit: 75f));
            m28.steps.Add(MakeStealth("m28.s5.obj", "m28_stealth_pergola", radius: 5.5f, MakeDlg(("npc.player.name", "m28.d5.1"))));
            m28.steps.Add(MakeFight("m28.s6.obj", "m28_ambush_courier", 2, null));
            m28.steps.Add(MakeTalk("m28.s7.obj", "npc_pazarci_ismail", MakeDlg(("npc.ismail.name", "m28.d7.1"))));

            // --- MISSION 29 (7 Stages · Night 22:00!) ---
            MissionData m29 = MakeMission("m29_geceyarisi_m5", "m29.title", "m29.summary", 600, "m30_mimarsinan_sifresi", "retry_m29_p", "retry_m29_v");
            MissionStepData m29s1 = MakeTalk("m29.s1.obj", "npc_muhbir_emre", MakeDlg(("npc.emre.name", "m29.d1.1"), ("npc.emre.name", "m29.d1.2")));
            m29s1.setTimeOfDayHours = 22.0f;
            m29.steps.Add(m29s1);
            m29.steps.Add(new MissionStepData { stepType = MissionStepType.EnterVehicle, objectiveKey = "m29.s2.obj", targetAnchorId = "m29_hasanpasa_metro" });
            m29.steps.Add(MakeReach("m29.s3.obj", "m29_hasanpasa_metro", radius: 12f, requiresVehicle: true, timeLimit: 60f));
            m29.steps.Add(MakeInspect("m29.s4.obj", "m29_smuggled_crate", MakeDlg(("npc.player.name", "m29.d4.1"))));
            m29.steps.Add(MakeReach("m29.s5.obj", "m29_lake_escape", radius: 12f, requiresVehicle: true, timeLimit: 60f));
            m29.steps.Add(MakeReach("m29.s6.obj", "m31_meydan_delivery", radius: 13f, requiresVehicle: true, timeLimit: 75f, MakeDlg(("npc.player.name", "m29.d6.1"))));
            m29.steps.Add(MakeTalk("m29.s7.obj", "npc_muhbir_emre", MakeDlg(("npc.emre.name", "m29.d7.1"))));

            // --- MISSION 30 (7 Stages · Sunset 18:15!) ---
            MissionData m30 = MakeMission("m30_mimarsinan_sifresi", "m30.title", "m30.summary", 650, "m31_aydos_kusatmasi", "retry_m30_p", "retry_m30_v");
            MissionStepData m30s1 = MakeTalk("m30.s1.obj", "npc_mimarsinan_kemal", MakeDlg(("npc.kemal_dede.name", "m30.d1.1"), ("npc.kemal_dede.name", "m30.d1.2")));
            m30s1.setTimeOfDayHours = 18.25f;
            m30.steps.Add(m30s1);
            m30.steps.Add(MakeStealth("m30.s2.obj", "m30_stealth_fountain", radius: 5.5f, null));
            m30.steps.Add(MakeInspect("m30.s3.obj", "m30_cipher_envelope", MakeDlg(("npc.player.name", "m30.d3.1"))));
            m30.steps.Add(MakeReach("m30.s4.obj", "m30_avm_intercept", radius: 12f, requiresVehicle: true, timeLimit: 70f));
            m30.steps.Add(MakeFight("m30.s5.obj", "m30_ambush_avm", 3, null));
            m30.steps.Add(MakeTalk("m30.s6.obj", "npc_demir_lieutenant", MakeDlg(("npc.demir.name", "m30.d6.1"), ("npc.demir.name", "m30.d6.2"))));
            m30.steps.Add(MakeReach("m30.s7.obj", "m30_turgutreis_rally", radius: 12f, requiresVehicle: true, timeLimit: 0f));

            // --- MISSION 31 (8 Stages · Night 23:00 · Historic Aydos Castle Boss Siege!) ---
            MissionData m31 = MakeMission("m31_aydos_kusatmasi", "m31.title", "m31.summary", 850, "m32_sultanbeyli_efsanesi", "retry_m31_p", "retry_m31_v");
            MissionStepData m31s1 = MakeTalk("m31.s1.obj", "npc_castle_haluk", MakeDlg(("npc.haluk_kale.name", "m31.d1.1"), ("npc.haluk_kale.name", "m31.d1.2")));
            m31s1.setTimeOfDayHours = 23.0f;
            m31.steps.Add(m31s1);
            m31.steps.Add(MakeStealth("m31.s2.obj", "m31_stealth_ramp", radius: 6.0f, null));
            m31.steps.Add(MakeFight("m31.s3.obj", "m31_ambush_gate", 2, null));
            m31.steps.Add(MakeReach("m31.s4.obj", "m31_castle_courtyard", radius: 7.5f, requiresVehicle: false, timeLimit: 0f, MakeDlg(("npc.player.name", "m31.d4.1"))));
            m31.steps.Add(MakeFight("m31.s5.obj", "m31_ambush_kuzgun_boss", 4, MakeDlg(("npc.player.name", "m31.d5.1"))));
            m31.steps.Add(MakeInspect("m31.s6.obj", "m31_deed_chest", MakeDlg(("npc.player.name", "m31.d6.1"))));
            m31.steps.Add(MakeReach("m31.s7.obj", "m31_meydan_delivery", radius: 13f, requiresVehicle: true, timeLimit: 85f));
            m31.steps.Add(MakeReach("m31.s8.obj", "m32_saatkulesi_lap", radius: 14f, requiresVehicle: true, timeLimit: 60f));

            // --- MISSION 32 (6 Stages · Sunrise 07:30 · 3-Choice Legend Finale!) ---
            MissionData m32 = MakeMission("m32_sultanbeyli_efsanesi", "m32.title", "m32.summary", 1200, string.Empty, "retry_m27_p", "retry_m27_v");
            m32.autoStartNextMission = false;
            MissionStepData m32s1 = MakeTalk("m32.s1.obj", "npc_muhbir_emre", MakeDlg(("npc.haluk_kale.name", "m32.d1.1"), ("npc.haluk_kale.name", "m32.d1.2")));
            m32s1.setTimeOfDayHours = 7.5f;
            m32.steps.Add(m32s1);
            m32.steps.Add(MakeInspect("m32.s2.obj", "m32_deed_podium", MakeDlg(("npc.player.name", "m32.d2.1"))));
            m32.steps.Add(MakeReach("m32.s3.obj", "m32_saatkulesi_lap", radius: 14f, requiresVehicle: true, timeLimit: 0f));
            m32.steps.Add(MakeReach("m32.s4.obj", "m32_home_celebration", radius: 10f, requiresVehicle: false, timeLimit: 0f));

            DialogueData finalChoiceDlg = MakeDlg(("npc.haluk_kale.name", "m32.d5.1"));
            finalChoiceDlg.choices.Add(new DialogueChoice { choiceId = "ch8_guardian", labelKey = "m32.c1.lbl", followUpDialogue = MakeDlg(("npc.haluk_kale.name", "m32.c1.f1")) });
            finalChoiceDlg.choices.Add(new DialogueChoice { choiceId = "ch8_speedking", labelKey = "m32.c2.lbl", followUpDialogue = MakeDlg(("npc.haluk_kale.name", "m32.c2.f1")) });
            finalChoiceDlg.choices.Add(new DialogueChoice { choiceId = "ch8_empire", labelKey = "m32.c3.lbl", followUpDialogue = MakeDlg(("npc.haluk_kale.name", "m32.c3.f1")) });
            m32.steps.Add(MakeTalk("m32.s5.obj", "npc_muhbir_emre", finalChoiceDlg));
            m32.steps.Add(MakeReach("m32.s6.obj", "m32_saatkulesi_lap", radius: 14f, requiresVehicle: false, timeLimit: 0f));

            mm.Catalog.missions.Add(m27);
            mm.Catalog.missions.Add(m28);
            mm.Catalog.missions.Add(m29);
            mm.Catalog.missions.Add(m30);
            mm.Catalog.missions.Add(m31);
            mm.Catalog.missions.Add(m32);

            Debug.Log($"[ChapterSevenEightStoryExpansion] Installed Chapters 7-8 (M27-M32 · 42 Action Stages). Total story missions: {mm.Catalog.missions.Count}.");
        }

        private MissionData MakeMission(string id, string titleKey, string summaryKey, int reward, string nextId, string retryPlayerAnchor = null, string retryVehicleAnchor = null)
        {
            MissionData m = ScriptableObject.CreateInstance<MissionData>();
            m.name = id;
            m.missionId = id;
            m.titleKey = titleKey;
            m.summaryKey = summaryKey;
            m.rewardMoney = reward;
            m.nextMissionId = nextId;
            m.autoStartNextMission = !string.IsNullOrEmpty(nextId);
            if (!string.IsNullOrEmpty(retryPlayerAnchor)) m.retryPlayerAnchorId = retryPlayerAnchor;
            if (!string.IsNullOrEmpty(retryVehicleAnchor)) m.retryVehicleAnchorId = retryVehicleAnchor;
            _runtimeAssets.Add(m);
            return m;
        }

        private MissionStepData MakeTalk(string obj, string anchor, DialogueData dlg) => new MissionStepData
        {
            stepType = MissionStepType.TalkToNpc, objectiveKey = obj, targetAnchorId = anchor, dialogueAsset = dlg
        };

        private MissionStepData MakeInspect(string obj, string anchor, DialogueData dlg) => new MissionStepData
        {
            stepType = MissionStepType.InspectTarget, objectiveKey = obj, targetAnchorId = anchor, dialogueAsset = dlg
        };

        private MissionStepData MakeReach(string obj, string anchor, float radius, bool requiresVehicle, float timeLimit, DialogueData dlg = null) => new MissionStepData
        {
            stepType = MissionStepType.ReachLocation, objectiveKey = obj, targetAnchorId = anchor,
            triggerRadius = radius, requiresVehicle = requiresVehicle, timeLimitSeconds = timeLimit, dialogueAsset = dlg
        };

        private MissionStepData MakeStealth(string obj, string anchor, float radius, DialogueData dlg) => new MissionStepData
        {
            stepType = MissionStepType.StealthReach, objectiveKey = obj, targetAnchorId = anchor, triggerRadius = radius, dialogueAsset = dlg
        };

        private MissionStepData MakeFight(string obj, string anchor, int count, DialogueData dlg) => new MissionStepData
        {
            stepType = MissionStepType.DefeatHostiles, objectiveKey = obj, targetAnchorId = anchor, hostileCount = count, dialogueAsset = dlg
        };

        private MissionStepData MakeEvade(string obj, int stars, DialogueData dlg) => new MissionStepData
        {
            stepType = MissionStepType.EvadePolice, objectiveKey = obj, targetAnchorId = string.Empty, wantedStarsToTrigger = stars, dialogueAsset = dlg
        };

        private DialogueData MakeDlg(params (string speakerKey, string textKey)[] lines)
        {
            DialogueData d = ScriptableObject.CreateInstance<DialogueData>();
            for (int i = 0; i < lines.Length; i++)
            {
                d.lines.Add(new DialogueLine { speakerKey = lines[i].speakerKey, textKey = lines[i].textKey });
            }
            _runtimeAssets.Add(d);
            return d;
        }

        // =====================================================================
        // 3. STORY NPCs, WORLD ANCHORS & GLOWING INTERACTIVE PROPS
        // =====================================================================
        private Material MakeMat(Shader sh, Color col, Color? emission = null)
        {
            Material m = new Material(sh);
            if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, col);
            if (m.HasProperty(ColorId)) m.SetColor(ColorId, col);
            if (emission.HasValue && m.HasProperty(EmissionColorId))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColorId, emission.Value);
            }
            _ownedMats.Add(m);
            return m;
        }

        private void BuildMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            if (lit == null) return;
            _gangRedMat = MakeMat(lit, new Color(0.76f, 0.12f, 0.14f));
            _gangDarkMat = MakeMat(lit, new Color(0.11f, 0.12f, 0.15f));
            _bossGoldMat = MakeMat(lit, new Color(0.94f, 0.74f, 0.18f), new Color(0.55f, 0.38f, 0.06f));
            _propGlowMat = MakeMat(lit, new Color(1.0f, 0.78f, 0.20f), new Color(1.0f, 0.72f, 0.16f) * 1.4f);
            _propCaseMat = MakeMat(lit, new Color(0.24f, 0.16f, 0.10f));
            _npcCoatBlueMat = MakeMat(lit, new Color(0.16f, 0.32f, 0.56f));
            _npcApronGreenMat = MakeMat(lit, new Color(0.18f, 0.52f, 0.30f));
            _npcElderGreyMat = MakeMat(lit, new Color(0.58f, 0.55f, 0.50f));
        }

        private void SpawnAllAnchorsNpcsAndProps()
        {
            Transform root = new GameObject("Chapter7_8_WorldActors").transform;
            root.SetParent(transform, false);

            // Key Story NPCs (positioned on verified open plazas/streets across the 6 new districts!)
            SpawnStoryNpc(root, "npc_muhbir_emre",      "Muhbir Emre",        new Vector3( 171f, 0.10f,  185f), 160f, _npcCoatBlueMat);
            SpawnStoryNpc(root, "npc_pazarci_ismail",   "Pazarcı İsmail Amca",new Vector3(-184f, 0.10f, -176f),   0f, _npcApronGreenMat);
            SpawnStoryNpc(root, "npc_mimarsinan_kemal", "Kemal Dede",         new Vector3( -48f, 0.08f, -296f),   0f, _npcElderGreyMat);
            SpawnStoryNpc(root, "npc_demir_lieutenant", "Demir",              new Vector3(-112f, 0.08f,  -54f), 180f, _gangDarkMat);
            SpawnStoryNpc(root, "npc_castle_haluk",     "Usta Haluk",         new Vector3( 231f, 0.08f, -124f),  90f, _npcCoatBlueMat);

            // Glowing Interactive Story Props
            SpawnStoryProp(root, "m27_kuzgun_briefcase", "m27_karanlik_teklif",   new Vector3( 166f, 0.12f,  211f), "Kuzgun Harita Çantasını İncele", "Inspect Kuzgun Map Case");
            SpawnStoryProp(root, "m27_pirate_radio",     "m27_karanlik_teklif",   new Vector3( -14f, 0.12f,  260f), "Korsan Telsizi Ele Geçir",       "Grab Pirate Radio");
            SpawnStoryProp(root, "m28_stolen_cashbox",   "m28_bosna_baskini",     new Vector3(-154f, 0.10f, -206f), "Çalınan Pazar Kutusunu Al",      "Recover Stolen Cashbox");
            SpawnStoryProp(root, "m29_smuggled_crate",   "m29_geceyarisi_m5",     new Vector3(-154f, 0.12f,  174f), "Kaçak Sandığı Sabote Et",        "Sabotage Smuggled Crate");
            SpawnStoryProp(root, "m30_cipher_envelope",  "m30_mimarsinan_sifresi",new Vector3( -55f, 0.10f, -301f), "Şifreli Kale Planını Al",        "Take Encrypted Castle Plan");
            SpawnStoryProp(root, "m31_deed_chest",       "m31_aydos_kusatmasi",   new Vector3( 260f, 3.18f, -122f), "Esnaf Tapu Sandığını Kurtar",    "Recover Merchants' Deeds Chest");
            SpawnStoryProp(root, "m32_deed_podium",      "m32_sultanbeyli_efsanesi",new Vector3(180f, 0.12f, 206f), "Tapu Sandığını Kürsüye Koy",     "Place Deeds Chest on Podium");

            // Location, Stealth & Ambush Anchors
            SpawnAnchor(root, "m27_ambush_meydan",       new Vector3( 176f, 0.12f,  194f), 8f);
            SpawnAnchor(root, "m27_metro_intercept",     new Vector3(  -6f, 0.12f,  266f), 11f);
            SpawnAnchor(root, "m28_ambush_pazar",        new Vector3(-172f, 0.12f, -192f), 8f);
            SpawnAnchor(root, "m28_hasanpasa_dropoff",   new Vector3(-174f, 0.12f,  188f), 11f);
            SpawnAnchor(root, "m28_stealth_pergola",     new Vector3(-185f, 0.10f,  200f), 5.5f);
            SpawnAnchor(root, "m28_ambush_courier",      new Vector3(-185f, 0.10f,  204f), 8f);
            SpawnAnchor(root, "m29_hasanpasa_metro",     new Vector3(-158f, 0.12f,  178f), 12f);
            SpawnAnchor(root, "m29_lake_escape",         new Vector3(-162f, 0.10f,    4f), 12f);
            SpawnAnchor(root, "m30_stealth_fountain",    new Vector3( -56f, 0.10f, -298f), 5.5f);
            SpawnAnchor(root, "m30_avm_intercept",       new Vector3(-116f, 0.12f,  -58f), 12f);
            SpawnAnchor(root, "m30_ambush_avm",          new Vector3(-116f, 0.12f,  -56f), 8f);
            SpawnAnchor(root, "m30_turgutreis_rally",    new Vector3( 222f, 0.12f, -148f), 12f);
            SpawnAnchor(root, "m31_stealth_ramp",        new Vector3( 239f, 0.10f, -118f), 6f);
            SpawnAnchor(root, "m31_ambush_gate",         new Vector3( 256f, 3.18f, -118f), 8f);
            SpawnAnchor(root, "m31_castle_courtyard",    new Vector3( 263f, 3.18f, -118f), 7.5f);
            SpawnAnchor(root, "m31_ambush_kuzgun_boss",  new Vector3( 266f, 3.18f, -118f), 10f);
            SpawnAnchor(root, "m31_meydan_delivery",     new Vector3( 176f, 0.12f,  180f), 13f);
            SpawnAnchor(root, "m32_saatkulesi_lap",      new Vector3(   0f, 0.18f,  134f), 14f);
            SpawnAnchor(root, "m32_home_celebration",    new Vector3( -62f, 0.12f,    4.5f), 10f);

            // Clean open-road Retry Player & Vehicle Anchors for every Chapter 7-8 Mission!
            SpawnAnchor(root, "retry_m27_p", new Vector3( 168f, 0.12f,  178f), 4f);
            SpawnAnchor(root, "retry_m27_v", new Vector3( 164f, 0.28f,  172f), 5f);
            SpawnAnchor(root, "retry_m28_p", new Vector3(-178f, 0.12f, -174f), 4f);
            SpawnAnchor(root, "retry_m28_v", new Vector3(-172f, 0.28f, -170f), 5f);
            SpawnAnchor(root, "retry_m29_p", new Vector3(  -8f, 0.12f,  260f), 4f);
            SpawnAnchor(root, "retry_m29_v", new Vector3(  -4f, 0.28f,  266f), 5f);
            SpawnAnchor(root, "retry_m30_p", new Vector3( -50f, 0.12f, -268f), 4f);
            SpawnAnchor(root, "retry_m30_v", new Vector3( -44f, 0.28f, -262f), 5f);
            SpawnAnchor(root, "retry_m31_p", new Vector3( 228f, 0.12f, -138f), 4f);
            SpawnAnchor(root, "retry_m31_v", new Vector3( 228f, 0.28f, -148f), 5f);
        }

        private void SpawnStoryNpc(Transform parent, string anchorId, string displayName, Vector3 pos, float yaw, Material shirtMat)
        {
            ProceduralHumanoid template = PlayerModeController.Instance?.FootController?.Humanoid;
            if (template == null) return;

            bool isWorkerNpc = anchorId.Contains("ismail") || anchorId.Contains("haluk");
            bool isElderNpc = anchorId.Contains("kemal");
            bool isGangLt = anchorId.Contains("demir");
            Color shirtCol = (shirtMat != null && shirtMat.HasProperty(BaseColorId)) ? shirtMat.GetColor(BaseColorId) : new Color(0.18f, 0.34f, 0.56f);
            Color pantsCol = isGangLt ? new Color(0.10f, 0.11f, 0.14f) : new Color(0.18f, 0.22f, 0.28f);
            Color hairCol = isElderNpc ? RealisticCharacterFactory.SilverElderHair : new Color(0.14f, 0.10f, 0.08f);
            Color skinCol = RealisticCharacterFactory.DiverseSkinPalette[isElderNpc ? 0 : 1];

            ProceduralHumanoid hum = RealisticCharacterFactory.BuildCharacter(
                parent,
                isWorkerNpc ? CharacterArchetype.Worker : CharacterArchetype.CasualMale,
                template, shirtCol, pantsCol, hairCol, skinCol,
                height: 1.78f,
                leatherCoat: isGangLt);
            if (hum == null) hum = Instantiate(template, parent);
            hum.name = $"StoryNPC_Ch78_{anchorId}";
            hum.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));

            Collider[] oldCols = hum.GetComponentsInChildren<Collider>(true);
            for (int c = 0; c < oldCols.Length; c++) Destroy(oldCols[c]);
            CapsuleCollider cap = hum.gameObject.AddComponent<CapsuleCollider>();
            cap.radius = 0.32f;
            cap.height = 1.78f;
            cap.center = new Vector3(0f, 0.89f, 0f);
            WorldPhysicsSolidifier.EnsureKinematicRigidbody(hum.gameObject);
            hum.SetLocomotion(0f, true, 0f);

            WorldAnchor anchor = hum.gameObject.AddComponent<WorldAnchor>();
            anchor.Configure(anchorId, 3.2f, Vector3.zero);
            InteractableTrigger trig = hum.gameObject.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.Npc, anchorId, "ui.btn.talk", 3.3f);
            trig.SetCustomPrompt($"Konuş: {displayName}", $"Talk: {displayName}");
        }

        private void SpawnStoryProp(Transform parent, string anchorId, string missionId, Vector3 pos, string promptTr, string promptEn)
        {
            GameObject go = new GameObject($"StoryProp_{anchorId}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "CaseMesh";
            box.transform.SetParent(go.transform, false);
            box.transform.localPosition = new Vector3(0f, 0.28f, 0f);
            box.transform.localScale = new Vector3(0.68f, 0.44f, 0.50f);
            MeshRenderer mr = box.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = _propCaseMat;

            GameObject glow = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            glow.name = "GlowBeacon";
            glow.transform.SetParent(go.transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            glow.transform.localScale = new Vector3(1.15f, 0.03f, 1.15f);
            Destroy(glow.GetComponent<Collider>());
            MeshRenderer gmr = glow.GetComponent<MeshRenderer>();
            if (gmr != null) { gmr.sharedMaterial = _propGlowMat; gmr.shadowCastingMode = ShadowCastingMode.Off; }

            WorldAnchor anchor = go.AddComponent<WorldAnchor>();
            anchor.Configure(anchorId, 2.8f, Vector3.zero);
            InteractableTrigger trig = go.AddComponent<InteractableTrigger>();
            trig.Configure(InteractableKind.InspectObject, anchorId, "ui.btn.interact", 3.2f);
            trig.SetCustomPrompt(promptTr, promptEn);

            _props.Add(new StoryPropEntry { Go = go, AnchorId = anchorId, MissionId = missionId });
        }

        private static void SpawnAnchor(Transform parent, string id, Vector3 pos, float radius)
        {
            GameObject go = new GameObject($"Anchor_{id}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            WorldAnchor a = go.AddComponent<WorldAnchor>();
            a.Configure(id, radius, Vector3.zero);
        }

        // =====================================================================
        // 4. ACTIVE GANG AMBUSHES & BOSS "KUZGUN" BORA KARAHAN COMBAT ENCOUNTERS
        // =====================================================================
        public void BeginHostileEncounter(string anchorId, int count, string missionId)
        {
            ClearActiveHostiles();
            _activeEncounterAnchorId = anchorId;
            _encounterActive = true;

            WorldAnchor anchor = WorldAnchorRegistry.Get(anchorId);
            Vector3 center = anchor != null ? anchor.transform.position : (PlayerModeController.Instance != null ? PlayerModeController.Instance.ActiveWorldPosition + Vector3.forward * 8f : Vector3.zero);
            ProceduralHumanoid template = PlayerModeController.Instance?.FootController?.Humanoid;
            if (template == null) return;

            bool hasBoss = string.Equals(anchorId, "m31_ambush_kuzgun_boss", StringComparison.OrdinalIgnoreCase);
            bool en = LocalizationService.IsEnglish;

            for (int i = 0; i < count; i++)
            {
                bool isBoss = hasBoss && i == 0;
                float ang = (i / (float)Mathf.Max(1, count)) * Mathf.PI * 2f;
                Vector3 spawnPos = center + new Vector3(Mathf.Cos(ang) * 2.8f, 0f, Mathf.Sin(ang) * 2.8f);

                // Boss "Kuzgun" Bora uses his bespoke sculpted Blender BossKuzgun trenchcoat + beard + gold aviators rig!
                CharacterArchetype enemyArch = isBoss ? CharacterArchetype.BossKuzgun : CharacterArchetype.CasualMale;
                Color coatCol = isBoss ? new Color(0.09f, 0.10f, 0.13f) : new Color(0.56f, 0.11f, 0.14f);
                Color pantsCol = new Color(0.09f, 0.10f, 0.13f);
                Color hairCol = isBoss ? new Color(0.06f, 0.06f, 0.07f) : RealisticCharacterFactory.DiverseHairPalette[i % 3];
                Color skinCol = RealisticCharacterFactory.DiverseSkinPalette[(i + 1) % RealisticCharacterFactory.DiverseSkinPalette.Length];

                ProceduralHumanoid hum = RealisticCharacterFactory.BuildCharacter(
                    transform, enemyArch, template, coatCol, pantsCol, hairCol, skinCol,
                    height: 1.78f,
                    leatherCoat: true,
                    goldAccents: isBoss);
                if (hum == null) hum = Instantiate(template, transform);
                hum.name = $"StoryHostile_{i:00}_{(isBoss ? "KuzgunBoss" : "Gang")}";
                hum.transform.position = spawnPos;
                hum.transform.localScale = Vector3.one * (isBoss ? 1.08f : 1.01f);

                Collider[] cols = hum.GetComponentsInChildren<Collider>(true);
                for (int c = 0; c < cols.Length; c++) Destroy(cols[c]);
                CapsuleCollider cap = hum.gameObject.AddComponent<CapsuleCollider>();
                cap.radius = 0.32f;
                cap.height = 1.78f;
                cap.center = new Vector3(0f, 0.89f, 0f);
                Rigidbody rb = WorldPhysicsSolidifier.EnsureKinematicRigidbody(hum.gameObject);

                string label = isBoss
                    ? (en ? "BOSS: Kuzgun Bora" : "BOSS: Kuzgun Bora")
                    : (en ? $"Kuzgun Thug #{i + 1}" : $"Kuzgun Çetesi #{i + 1}");

                float maxHp = isBoss ? 175f : 95f;
                _activeHostiles.Add(new MissionHostile
                {
                    Root = hum.gameObject,
                    Rb = rb,
                    Humanoid = hum,
                    DisplayName = label,
                    IsBoss = isBoss,
                    Health = maxHp,
                    MaxHealth = maxHp,
                    PunchCooldown = 0.65f + i * 0.35f
                });
            }

            PlayerModeController.Instance?.RaiseStatusMessage(en
                ? $"⚔️ AMBUSH! Knock out all {count} Kuzgun enemies! (Press X to Dodge Roll!)"
                : $"⚔️ PUSU! {count} Kuzgun adamının tamamını bayılt! (Takla için X / Eğil için C!)");
        }

        public void ClearActiveHostiles()
        {
            for (int i = 0; i < _activeHostiles.Count; i++)
            {
                if (_activeHostiles[i]?.Root != null) Destroy(_activeHostiles[i].Root);
            }
            _activeHostiles.Clear();
            _encounterActive = false;
            _activeEncounterAnchorId = null;
            if (_combatHudPanel != null) _combatHudPanel.SetActive(false);
        }

        public void CopyActiveHostileRadarBlips(List<Vector3> outNormalPositions, out Vector3 bossPos, out bool hasAliveBoss)
        {
            outNormalPositions.Clear();
            bossPos = Vector3.zero;
            hasAliveBoss = false;
            if (!_encounterActive || _activeHostiles.Count == 0) return;

            for (int i = 0; i < _activeHostiles.Count; i++)
            {
                MissionHostile h = _activeHostiles[i];
                if (h == null || h.Root == null || h.Defeated) continue;
                if (h.IsBoss)
                {
                    bossPos = h.Root.transform.position;
                    hasAliveBoss = true;
                }
                else
                {
                    outNormalPositions.Add(h.Root.transform.position);
                }
            }
        }

        private void BuildEncounterHud()
        {
            OurWorldUiKit.EnsureInitialized();
            GameObject cGo = new GameObject("Ch78_BossCombatHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            cGo.transform.SetParent(transform, false);
            Canvas c = cGo.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 146;
            CanvasScaler cs = cGo.GetComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1920f, 1080f);
            cs.matchWidthOrHeight = 0.5f;

            _combatHudPanel = new GameObject("BossCombatBarPanel", typeof(RectTransform), typeof(Image));
            _combatHudPanel.transform.SetParent(cGo.transform, false);
            RectTransform rt = (RectTransform)_combatHudPanel.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -108f);
            rt.sizeDelta = new Vector2(448f, 68f);
            _combatHudBorder = _combatHudPanel.GetComponent<Image>();
            _combatHudBorder.color = new Color(0.95f, 0.24f, 0.24f, 0.92f);
            _combatHudBorder.raycastTarget = false;

            GameObject inner = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(_combatHudPanel.transform, false);
            RectTransform irt = (RectTransform)inner.transform;
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = new Vector2(2f, 2f);
            irt.offsetMax = new Vector2(-2f, -2f);
            _combatHudBg = inner.GetComponent<Image>();
            _combatHudBg.color = new Color(0.06f, 0.08f, 0.13f, 0.90f);
            _combatHudBg.raycastTarget = false;

            GameObject titleGo = new GameObject("CombatTitle", typeof(RectTransform), typeof(Text));
            titleGo.transform.SetParent(inner.transform, false);
            RectTransform trt = (RectTransform)titleGo.transform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -4f);
            trt.sizeDelta = new Vector2(-24f, 26f);
            _combatHudTitle = titleGo.GetComponent<Text>();
            _combatHudTitle.font = OurWorldUiKit.DefaultFont;
            _combatHudTitle.fontSize = 18;
            _combatHudTitle.fontStyle = FontStyle.Bold;
            _combatHudTitle.alignment = TextAnchor.MiddleLeft;
            _combatHudTitle.color = Color.white;
            _combatHudTitle.raycastTarget = false;

            GameObject subGo = new GameObject("CombatSub", typeof(RectTransform), typeof(Text));
            subGo.transform.SetParent(inner.transform, false);
            RectTransform srt = (RectTransform)subGo.transform;
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = new Vector2(0f, -4f);
            srt.sizeDelta = new Vector2(-24f, 26f);
            _combatHudSub = subGo.GetComponent<Text>();
            _combatHudSub.font = OurWorldUiKit.DefaultFont;
            _combatHudSub.fontSize = 15;
            _combatHudSub.fontStyle = FontStyle.Bold;
            _combatHudSub.alignment = TextAnchor.MiddleRight;
            _combatHudSub.color = new Color(0.98f, 0.80f, 0.30f, 1f);
            _combatHudSub.raycastTarget = false;

            GameObject barBg = new GameObject("BarTrack", typeof(RectTransform), typeof(Image));
            barBg.transform.SetParent(inner.transform, false);
            RectTransform brt = (RectTransform)barBg.transform;
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f);
            brt.anchoredPosition = new Vector2(0f, 12f);
            brt.sizeDelta = new Vector2(-24f, 14f);
            Image trackImg = barBg.GetComponent<Image>();
            trackImg.color = new Color(1f, 1f, 1f, 0.14f);
            trackImg.raycastTarget = false;

            GameObject fillGo = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            RectTransform frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;
            _combatHudFill = fillGo.GetComponent<Image>();
            _combatHudFill.color = new Color(0.92f, 0.22f, 0.22f, 1f);
            _combatHudFill.raycastTarget = false;

            _combatHudPanel.SetActive(false);
        }

        private void UpdateEncounterHud(PlayerModeController mode)
        {
            if (_combatHudPanel == null) return;

            bool show = _encounterActive && _activeHostiles.Count > 0 &&
                        mode != null && !mode.IsModalLocked && !MainMenuController.IsMenuActive;
            if (!show)
            {
                if (_combatHudPanel.activeSelf) _combatHudPanel.SetActive(false);
                return;
            }

            // Show Boss/Ambush bar as soon as player enters the district fight zone (<= 55m)
            Vector3 pPos = mode.ActiveWorldPosition;
            MissionHostile boss = null;
            float totalHp = 0f;
            float totalMaxHp = 0f;
            int aliveCount = 0;
            int guardsAlive = 0;
            float nearestSq = float.MaxValue;

            for (int i = 0; i < _activeHostiles.Count; i++)
            {
                MissionHostile h = _activeHostiles[i];
                if (h == null || h.Root == null) continue;
                totalMaxHp += h.MaxHealth;
                if (!h.Defeated)
                {
                    aliveCount++;
                    totalHp += h.Health;
                    if (h.IsBoss) boss = h;
                    else guardsAlive++;
                    float sq = (h.Root.transform.position - pPos).sqrMagnitude;
                    if (sq < nearestSq) nearestSq = sq;
                }
            }

            bool inZone = aliveCount > 0 && nearestSq <= 55f * 55f;
            if (_combatHudPanel.activeSelf != inZone) _combatHudPanel.SetActive(inZone);
            if (!inZone) return;

            bool en = LocalizationService.IsEnglish;
            if (boss != null && !boss.Defeated)
            {
                float ratio = Mathf.Clamp01(boss.Health / Mathf.Max(1f, boss.MaxHealth));
                _combatHudBorder.color = new Color(0.98f, 0.78f, 0.22f, 0.96f);
                _combatHudFill.color = new Color(0.94f, 0.18f, 0.22f, 1f);
                _combatHudFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                _combatHudTitle.text = en ? "👑 BOSS · KUZGUN BORA KARAHAN" : "👑 BOSS · KUZGUN BORA KARAHAN";
                _combatHudSub.text = guardsAlive > 0
                    ? (en ? $"{Mathf.CeilToInt(boss.Health)} HP · +{guardsAlive} Guards" : $"{Mathf.CeilToInt(boss.Health)} Can · +{guardsAlive} Koruma")
                    : $"{Mathf.CeilToInt(boss.Health)} / {Mathf.RoundToInt(boss.MaxHealth)} HP";
            }
            else
            {
                float ratio = Mathf.Clamp01(totalHp / Mathf.Max(1f, totalMaxHp));
                int knocked = _activeHostiles.Count - aliveCount;
                _combatHudBorder.color = new Color(0.92f, 0.24f, 0.24f, 0.90f);
                _combatHudFill.color = new Color(0.96f, 0.36f, 0.18f, 1f);
                _combatHudFill.rectTransform.anchorMax = new Vector2(ratio, 1f);
                _combatHudTitle.text = en ? "⚔️ KUZGUN SYNDICATE AMBUSH" : "⚔️ KUZGUN ÇETESİ PUSUSU";
                _combatHudSub.text = en
                    ? $"KO: {knocked}/{_activeHostiles.Count} · X: Roll"
                    : $"Bayıltılan: {knocked}/{_activeHostiles.Count} · X: Takla";
            }
        }

        public bool TryReceivePlayerPunch(Vector3 strikeCenter, float radius, Vector3 attackerPos, Vector3 punchDir, int comboStep)
        {
            if (!_encounterActive || _activeHostiles.Count == 0) return false;

            MissionHostile best = null;
            float maxR = radius + 0.62f;
            float bestSq = maxR * maxR;

            for (int i = 0; i < _activeHostiles.Count; i++)
            {
                MissionHostile h = _activeHostiles[i];
                if (h == null || h.Root == null || h.Defeated || h.HitCooldown > 0f) continue;
                Vector3 d = (h.Root.transform.position + Vector3.up * 1.0f) - strikeCenter;
                if (Mathf.Abs(d.y) > 2.0f) continue;
                float sq = d.sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = h;
                }
            }

            if (best == null) return false;

            Vector3 knockDir = punchDir;
            knockDir.y = 0f;
            if (knockDir.sqrMagnitude < 0.01f) knockDir = best.Root.transform.position - attackerPos;
            knockDir.y = 0f;
            knockDir = knockDir.sqrMagnitude > 0.001f ? knockDir.normalized : Vector3.forward;

            float dmg = comboStep >= 2 ? 52f : (comboStep == 1 ? 36f : 32f);
            best.Health = Mathf.Max(0f, best.Health - dmg);
            bool en = LocalizationService.IsEnglish;

            if (best.Health <= 0f)
            {
                best.Defeated = true;
                best.PendingPunchTimer = 0f;
                best.StumbleVel = knockDir * 7.8f;
                best.Root.transform.rotation = Quaternion.LookRotation(-knockDir, Vector3.up);
                best.Humanoid?.PlayKnockdown(25f);

                VisualEffectsManager.SpawnFloatingText(
                    best.Root.transform.position + Vector3.up * 1.7f,
                    best.IsBoss ? (en ? "🏆 BOSS KNOCKED OUT!" : "🏆 KUZGUN BAYILDI!") : (en ? "💫 KNOCKED OUT!" : "💫 BAYILDI!"),
                    new Color(1.0f, 0.86f, 0.22f), scale: best.IsBoss ? 1.35f : 1.15f, duration: 1.5f);
                GameFeelAudio.SpawnBurst(best.Root.transform.position + Vector3.up * 1.3f, new Color(1.0f, 0.82f, 0.22f), count: 16, spread: 0.7f);
                GameFeelAudio.TriggerHitstop(0.068f, slowScale: 0.18f);

                CheckAllHostilesDefeated();
            }
            else
            {
                best.HitCooldown = 0.22f;
                best.StumbleVel = knockDir * 3.8f;
                best.Humanoid?.PlayHitReact();
                VisualEffectsManager.SpawnFloatingText(
                    best.Root.transform.position + Vector3.up * 1.6f,
                    $"-{Mathf.RoundToInt(dmg)}", new Color(1.0f, 0.48f, 0.20f), scale: 0.92f, duration: 0.65f);
            }

            return true;
        }

        private void CheckAllHostilesDefeated()
        {
            int defeated = 0;
            for (int i = 0; i < _activeHostiles.Count; i++)
            {
                if (_activeHostiles[i].Defeated) defeated++;
            }

            bool en = LocalizationService.IsEnglish;
            if (defeated >= _activeHostiles.Count)
            {
                string finishedAnchor = _activeEncounterAnchorId;
                _encounterActive = false;
                StartCoroutine(CleanupDefeatedHostilesAfterDelay());
                MissionManager.Instance?.NotifyHostilesDefeated(finishedAnchor);
            }
            else
            {
                PlayerModeController.Instance?.RaiseStatusMessage(en
                    ? $"⚔️ Kuzgun Crew knocked out: {defeated}/{_activeHostiles.Count}"
                    : $"⚔️ Kuzgun Çetesi bayıltılan: {defeated}/{_activeHostiles.Count}");
            }
        }

        private IEnumerator CleanupDefeatedHostilesAfterDelay()
        {
            yield return new WaitForSeconds(5.5f);
            if (!_encounterActive) ClearActiveHostiles();
        }

        private void Update()
        {
            if (Time.frameCount % 15 == 0)
            {
                RefreshPropVisibility();
            }

            PlayerModeController mode = PlayerModeController.Instance;
            UpdateEncounterHud(mode);

            if (!_encounterActive || _activeHostiles.Count == 0) return;

            float dt = Time.deltaTime;
            if (mode == null || mode.IsModalLocked || MainMenuController.IsMenuActive) return;

            Vector3 pPos = mode.ActiveWorldPosition;
            bool inCar = mode.IsDriving && mode.CurrentVehicle != null;
            float carSpeed = inCar ? mode.CurrentVehicle.SpeedKmh : 0f;

            for (int i = 0; i < _activeHostiles.Count; i++)
            {
                MissionHostile h = _activeHostiles[i];
                if (h == null || h.Root == null) continue;
                Transform tr = h.Root.transform;

                if (h.StumbleVel.sqrMagnitude > 0.02f)
                {
                    Vector3 dRef = tr.forward;
                    NpcSmartNavigation.MoveActorSafely(tr, h.Rb, h.StumbleVel * dt, ref dRef, false, 0.30f);
                    h.StumbleVel = Vector3.MoveTowards(h.StumbleVel, Vector3.zero, 11f * dt);
                }

                if (h.Defeated)
                {
                    h.Humanoid?.SetLocomotion(0f, true, 0f);
                    continue;
                }

                if (h.HitCooldown > 0f) h.HitCooldown -= dt;
                if (h.PunchCooldown > 0f) h.PunchCooldown -= dt;

                Vector3 toP = pPos - tr.position;
                float vertGap = Mathf.Abs(toP.y);
                toP.y = 0f;
                float dist = toP.magnitude;

                // Running over a mission gang thug at >18 km/h knocks them out cleanly too!
                if (inCar && carSpeed >= 18f && dist < 2.65f && vertGap < 2.0f)
                {
                    h.Health = 0f;
                    h.Defeated = true;
                    h.Humanoid?.PlayKnockdown(25f);
                    GameFeelAudio.SpawnBurst(tr.position + Vector3.up, new Color(1f, 0.8f, 0.2f), count: 12, spread: 0.6f);
                    CheckAllHostilesDefeated();
                    if (!_encounterActive) return;
                    continue;
                }

                if (dist > 34f || vertGap > 4.5f)
                {
                    h.Humanoid?.SetLocomotion(0f, true, 0f);
                    continue;
                }

                Vector3 dir = dist > 0.05f ? toP / dist : tr.forward;
                tr.rotation = Quaternion.Slerp(tr.rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-12f * dt));

                if (h.PendingPunchTimer > 0f)
                {
                    h.PendingPunchTimer -= dt;
                    h.Humanoid?.SetLocomotion(0f, true, 0f);
                    if (h.PendingPunchTimer <= 0f && dist <= 2.05f && !inCar)
                    {
                        PlayerNeedsController.Instance?.TakeDamage(h.IsBoss ? 16f : 11f, tr.position, h.DisplayName, heavyHit: h.IsBoss);
                    }
                    continue;
                }

                if (dist > 1.55f)
                {
                    float chaseSpd = h.IsBoss ? 4.35f : 3.95f;
                    Vector3 steer = NpcSmartNavigation.ComputeSmartSteering(tr, dir, 1.8f, 0.30f, avoidPlayer: false);
                    Vector3 pRef = steer;
                    NpcSmartNavigation.MoveActorSafely(tr, h.Rb, steer * (chaseSpd * dt), ref pRef, false, 0.30f);
                    h.Humanoid?.SetLocomotion(chaseSpd, true, 0f, steer);
                }
                else
                {
                    h.Humanoid?.SetLocomotion(0f, true, 0f);
                    if (h.PunchCooldown <= 0f && !inCar)
                    {
                        h.PunchCooldown = h.IsBoss ? 1.15f : 1.45f;
                        h.PendingPunchTimer = 0.24f;
                        h.Humanoid?.PlayPunch();
                    }
                }
            }
        }

        private void RefreshPropVisibility()
        {
            string activeMissionId = MissionManager.Instance != null && MissionManager.Instance.ActiveMission != null
                ? MissionManager.Instance.ActiveMission.missionId
                : string.Empty;
            string activeAnchor = MissionManager.Instance != null && MissionManager.Instance.CurrentStep != null
                ? MissionManager.Instance.CurrentStep.targetAnchorId
                : string.Empty;

            for (int i = 0; i < _props.Count; i++)
            {
                var p = _props[i];
                if (p.Go == null) continue;
                bool visible = string.Equals(activeMissionId, p.MissionId, StringComparison.OrdinalIgnoreCase) &&
                               string.Equals(activeAnchor, p.AnchorId, StringComparison.OrdinalIgnoreCase);
                if (p.Go.activeSelf != visible) p.Go.SetActive(visible);
            }
        }

        private void HandleIdleStoryNpcTalk(InteractableTrigger trig)
        {
            if (trig == null || string.IsNullOrEmpty(trig.InteractableId)) return;
            // Only speak casual flavor line if this NPC is NOT the active mission target right now
            MissionStepData step = MissionManager.Instance != null ? MissionManager.Instance.CurrentStep : null;
            if (step != null && string.Equals(step.targetAnchorId, trig.InteractableId, StringComparison.OrdinalIgnoreCase)) return;

            bool en = LocalizationService.IsEnglish;
            if (trig.InteractableId == "npc_muhbir_emre")
            {
                NpcSpeechBubbleManager.Say(trig.transform, en ? "Informant Emre" : "Muhbir Emre",
                    en ? "Keep your phone handy, Deniz. Big moves happening across Sultanbeyli!" : "Telefonun elinde olsun Deniz, Sultanbeyli'de büyük işler dönüyor!", 3.0f, NpcBubbleMood.Mission, 2.08f);
            }
            else if (trig.InteractableId == "npc_pazarci_ismail")
            {
                NpcSpeechBubbleManager.Say(trig.transform, en ? "Vendor İsmail" : "Pazarcı İsmail Amca",
                    en ? "Fresh fruit & honest trade — Battalgazi never backs down!" : "Taze meyve, helal kazanç — Battalgazi esnafı kimseye boyun eğmez!", 3.0f, NpcBubbleMood.Friendly, 2.08f);
            }
            else if (trig.InteractableId == "npc_mimarsinan_kemal")
            {
                NpcSpeechBubbleManager.Say(trig.transform, en ? "Elder Kemal" : "Kemal Dede",
                    en ? "Historic Aydos Castle watches over all of us, my child." : "Tarihi Aydos Kalesi hepimizi yukarıdan izler evladım.", 3.0f, NpcBubbleMood.Friendly, 2.08f);
            }
        }
    }
}
