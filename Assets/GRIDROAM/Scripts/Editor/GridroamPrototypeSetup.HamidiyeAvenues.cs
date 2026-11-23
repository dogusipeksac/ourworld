using System.Collections.Generic;
using Gridroam.Core;
using Gridroam.Jobs;
using Gridroam.Player;
using Gridroam.World;
using UnityEngine;

namespace Gridroam.Editor
{
    public static partial class GridroamPrototypeSetup
    {
        /// <summary>
        /// Builds the living, fully detailed 8-avenue Hamidiye Mahallesi district surrounding Selman Sokağı:
        ///   1. Fatih Bulvarı (Saat Kulesi Meydanı, Taksi Durağı, Kafe & Pastane, ATM, Otomat, Cam Balkon blokları)
        ///   2. Atatürk Caddesi (TEM O-4 korkuluk, çam koridoru, kaldırım, banklar & park araçları)
        ///   3. Mürşit Caddesi (AYEDAŞ Trafo, Çay & Tost Köşesi, Otomat, balkonlu apartmanlar)
        ///   4. Eyyubi Caddesi (53 Eyyubi Mahalle Büfesi, Otomat, ferforje balkonlu evler & boyanabilir ev)
        ///   5. Seyyid Kutup Caddesi (kaldırım, balkonlu apartmanlar, posta kutuları & sakinler)
        ///   6. Petrol Yolu Caddesi (Akaryakıt Pompası #2, Sokak Temizliği İş Panosu, Büfe, ATM & ticaret)
        ///   7. Aziziye Caddesi (İTO Şehit Er Dursun Sıvaz İlkokulu, Okul Önü Dondurma & Simit, banklar)
        ///   8. Zübeyde Hanım Caddesi (Ev Boyama İş Panosu, çam ağaçları, balkonlu mavi evler & 2 boyanabilir ev)
        /// All added sidewalks, balconies, and windows use ShadowCastingMode.Off + GPU-instanced shared materials
        /// so mobile 30+ FPS performance stays completely protected across the full 390m x 390m map.
        /// </summary>
        private static void BuildHamidiyeEightMainAvenues(
            Transform parent,
            MaterialSet mats,
            PrefabSet prefabs,
            List<SeatInteractable> seats)
        {
            GameObject hamRoot = new GameObject("Hamidiye_8MainAvenues_LivingDistrict");
            hamRoot.transform.SetParent(parent, false);

            Quaternion faceSouth = Quaternion.Euler(0f, 180f, 0f);
            Quaternion faceNorth = Quaternion.identity;
            Quaternion faceEast  = Quaternion.Euler(0f, 90f, 0f);
            Quaternion faceWest  = Quaternion.Euler(0f, -90f, 0f);

            // =========================================================================
            // 1. CONNECTED ROAD NETWORK + SIDEWALKS + CURBS + STREET WEATHERING
            // =========================================================================
            GameObject roads = new GameObject("Hamidiye_AvenueRoads_And_Sidewalks");
            roads.transform.SetParent(hamRoot.transform, false);

            // North-South Arterials extending from Zübeyde Hanım (-150) all the way to Fatih Bulvarı (+148):
            DisableShadowCasting(CreateTiledBox("Road_EyyubiCd", roads.transform, new Vector3(-148f, 0.014f, -2f), new Vector3(9.2f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_SeyyidKutupCd_Ext", roads.transform, new Vector3(-85f, 0.014f, -2f), new Vector3(8.6f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_AbdulkadirUdeh_Ext", roads.transform, new Vector3(99f, 0.014f, -2f), new Vector3(9.0f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_EastRingConnector", roads.transform, new Vector3(152f, 0.014f, -2f), new Vector3(8.6f, 0.028f, 304f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_NutukToMursit", roads.transform, new Vector3(59.2f, 0.015f, 30f), new Vector3(7.0f, 0.030f, 34f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_SelmanWestToEyyubi", roads.transform, new Vector3(-130f, 0.014f, 0f), new Vector3(44f, 0.028f, 7.6f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_SelmanEastToRing", roads.transform, new Vector3(136f, 0.014f, 0f), new Vector3(38f, 0.028f, 7.6f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));

            // East-West Main Avenues:
            DisableShadowCasting(CreateTiledBox("Road_MursitCd", roads.transform, new Vector3(2f, 0.015f, 46f), new Vector3(312f, 0.030f, 8.6f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_AtaturkCd", roads.transform, new Vector3(2f, 0.015f, 96f), new Vector3(316f, 0.030f, 10.6f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_FatihBulvari", roads.transform, new Vector3(2f, 0.016f, 142f), new Vector3(322f, 0.032f, 19.0f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_PetrolYoluCd", roads.transform, new Vector3(2f, 0.015f, -78f), new Vector3(312f, 0.030f, 10.6f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_AziziyeCd", roads.transform, new Vector3(2f, 0.015f, -114f), new Vector3(312f, 0.030f, 9.2f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));
            DisableShadowCasting(CreateTiledBox("Road_ZubeydeHanimCd", roads.transform, new Vector3(2f, 0.015f, -146f), new Vector3(312f, 0.030f, 8.8f), Quaternion.identity, mats.RoadAsphaltMain, 5.8f, collider: false));

            // Sidewalks + Curbs along ALL East-West Avenues (cleanly split at Eyyubi -148, Seyyid Kutup -85 & Abdülkadir Udeh +99!)
            (string tag, float zSouth, float zNorth, bool yellowCurb)[] ewSidewalks =
            {
                ("Fatih",   131.3f, 152.7f, true),
                ("Ataturk",  89.5f, 102.5f, true),
                ("Mursit",   40.6f,  51.4f, false),
                ("Petrol",  -84.5f, -71.5f, true),
                ("Aziziye",-119.8f,-108.2f, false),
                ("Zubeyde",-151.6f,-140.4f, false)
            };
            (float x0, float x1)[] ewSpans = { (-142f, -90.5f), (-79.5f, 93.5f), (104.5f, 146.5f) };
            for (int a = 0; a < ewSidewalks.Length; a++)
            {
                var sw = ewSidewalks[a];
                for (int s = 0; s < ewSpans.Length; s++)
                {
                    float len = ewSpans[s].x1 - ewSpans[s].x0;
                    float cx = (ewSpans[s].x0 + ewSpans[s].x1) * 0.5f;
                    DisableShadowCasting(CreateTiledBox($"SW_{sw.tag}_S_{s}", roads.transform,
                        new Vector3(cx, 0.032f, sw.zSouth), new Vector3(len, 0.064f, 2.2f),
                        Quaternion.identity, mats.KilitParke, 1.05f, collider: false));
                    if (sw.tag != "Ataturk") // Atatürk north edge has TEM highway guardrail + pine embankment
                    {
                        DisableShadowCasting(CreateTiledBox($"SW_{sw.tag}_N_{s}", roads.transform,
                            new Vector3(cx, 0.032f, sw.zNorth), new Vector3(len, 0.064f, 2.2f),
                            Quaternion.identity, mats.KilitParke, 1.05f, collider: false));
                    }
                    // Curb stone line on south sidewalk road edge
                    GameObject curb = CreatePrimitiveChild($"Curb_{sw.tag}_{s}", PrimitiveType.Cube, roads.transform,
                        new Vector3(cx, 0.048f, sw.zSouth + 1.08f), new Vector3(len, 0.096f, 0.18f),
                        sw.yellowCurb ? mats.RoadMarkingYellow : mats.Concrete, removeCollider: true);
                    DisableShadowCasting(curb);
                    MarkStaticEnvironment(curb);
                }
            }

            // Sidewalks along North-South Arterials (Eyyubi -148, Seyyid Kutup -85, Abdülkadir Udeh +99)
            (string tag, float xW, float xE)[] nsSidewalks =
            {
                ("Eyyubi",  -153.7f, -142.3f),
                ("Seyyid",   -90.4f,  -79.6f),
                ("Udeh",      93.4f,  104.6f)
            };
            (float z0, float z1)[] nsSpans = { (-139f, -120f), (-107f, -85f), (-70f, -8f), (8f, 40f), (52f, 89f), (103f, 130f) };
            for (int a = 0; a < nsSidewalks.Length; a++)
            {
                var sw = nsSidewalks[a];
                for (int s = 0; s < nsSpans.Length; s++)
                {
                    float len = nsSpans[s].z1 - nsSpans[s].z0;
                    float cz = (nsSpans[s].z0 + nsSpans[s].z1) * 0.5f;
                    DisableShadowCasting(CreateTiledBox($"SW_{sw.tag}_W_{s}", roads.transform,
                        new Vector3(sw.xW, 0.032f, cz), new Vector3(2.1f, 0.064f, len),
                        Quaternion.identity, mats.KilitParke, 1.05f, collider: false));
                    DisableShadowCasting(CreateTiledBox($"SW_{sw.tag}_E_{s}", roads.transform,
                        new Vector3(sw.xE, 0.032f, cz), new Vector3(2.1f, 0.064f, len),
                        Quaternion.identity, mats.KilitParke, 1.05f, collider: false));
                }
            }

            // Shared lightweight asphalt wear patches & manhole grime at key Hamidiye avenue junctions
            (float x, float z, float w, float h, float yaw)[] avePatches =
            {
                (  0.0f,  136.5f, 5.5f, 2.4f,  1f),
                (  0.0f,  147.5f, 5.2f, 2.2f, -2f),
                (-85.0f,   96.0f, 4.8f, 2.4f,  3f),
                ( 59.2f,   46.0f, 4.6f, 2.2f, -1f),
                (-18.0f,  -78.0f, 5.4f, 2.4f,  2f),
                (-16.0f, -114.0f, 5.0f, 2.2f, -2f),
                (-148.0f, -18.0f, 4.4f, 2.2f, 88f),
                ( 14.0f, -146.0f, 4.8f, 2.1f,  1f)
            };
            for (int i = 0; i < avePatches.Length; i++)
            {
                var p = avePatches[i];
                CreateWeatherDecalQuad($"AveAsphaltPatch_{i}", roads.transform,
                    new Vector3(p.x, 0.033f, p.z), Quaternion.Euler(90f, p.yaw, 0f),
                    new Vector2(p.w, p.h), mats.WeatherAsphaltPatch);
            }

            // =========================================================================
            // 2. AVENUE 1: FATİH BULVARI + SULTANBEYLİ SAAT KULESİ MEYDANI + TAKSİ DURAĞI
            // =========================================================================
            GameObject fatihRoot = new GameObject("Avenue_01_FatihBulvari_SaatKulesi");
            fatihRoot.transform.SetParent(hamRoot.transform, false);

            CreateTiledBox("Fatih_SaatKulesiMedian", fatihRoot.transform, new Vector3(0f, 0.08f, 142f), new Vector3(44f, 0.16f, 5.4f), Quaternion.identity, mats.Concrete, 2.0f, collider: true);
            PlaceSelmanModel("Hamidiye_FatihSaatKulesi.fbx", "Sultanbeyli_SaatKulesi", fatihRoot.transform,
                new Vector3(0f, 0.16f, 142f), faceSouth, Vector3.one,
                new Vector3(3.6f, 9.5f, 3.6f), new Vector3(0f, 4.75f, 0f), mats);
            CreateWorldSignText("SULTANBEYLİ · FATİH BULVARI", fatihRoot.transform,
                new Vector3(0f, 6.2f, 138.2f), Quaternion.identity, new Vector2(4.4f, 0.52f), new Color(0.11f, 0.28f, 0.42f, 1f));

            var (fb1, fb2) = CreateBench("Fatih_MedianBench_W", fatihRoot.transform, new Vector3(-9.5f, 0.16f, 142f), Quaternion.identity, mats);
            seats.Add(fb1); seats.Add(fb2);
            var (fb3, fb4) = CreateBench("Fatih_MedianBench_E", fatihRoot.transform, new Vector3(9.5f, 0.16f, 142f), Quaternion.identity, mats);
            seats.Add(fb3); seats.Add(fb4);
            CreateTownTree("Fatih_MedianTree_W", fatihRoot.transform, new Vector3(-16.5f, 0.16f, 142f), mats, 1.05f, 901);
            CreateTownTree("Fatih_MedianTree_E", fatihRoot.transform, new Vector3(16.5f, 0.16f, 142f), mats, 1.05f, 902);
            CreatePointLamp("Fatih_SaatKulesiGlow", fatihRoot.transform, new Vector3(0f, 7.5f, 137.5f), new Color(1.0f, 0.86f, 0.55f), 26f, 3.8f);

            // Commercial blocks lining North & South sides of Fatih Bulvarı — WITH Blender Cam Balkon & Side Windows!
            float[] fatihXs = { -120f, -56f, -24f, 12f, 48f, 124f };
            for (int i = 0; i < fatihXs.Length; i++)
            {
                GameObject nBlk = PlaceSelmanModel("Hamidiye_FatihCommercialBlock.fbx", $"Fatih_NorthBlock_{i + 1}", fatihRoot.transform,
                    new Vector3(fatihXs[i], 0f, 158.5f), faceSouth, Vector3.one,
                    new Vector3(14.2f, 13.5f, 9.6f), new Vector3(0f, 6.75f, 0f), mats);
                if (nBlk != null && (i == 2 || i == 3))
                {
                    MountBlenderBalcony(nBlk.transform, "Sultanbeyli_Balcony_CamBalkon.fbx", $"FatihN_{i}_BalcF2", new Vector3(7.10f, 3.80f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                    MountBlenderBalcony(nBlk.transform, "Sultanbeyli_Balcony_CamBalkon.fbx", $"FatihN_{i}_BalcF3", new Vector3(7.10f, 6.90f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                    AddSideWallWindowGrid(nBlk.transform, $"FatihN_{i}_WinE", -7.10f, false, new[] { -2.2f, 2.2f }, new[] { 4.2f, 7.3f, 10.4f }, 1.45f, 1.42f, mats);
                }
                if (Mathf.Abs(fatihXs[i]) > 18f)
                {
                    GameObject sBlk = PlaceSelmanModel("Hamidiye_FatihCommercialBlock.fbx", $"Fatih_SouthBlock_{i + 1}", fatihRoot.transform,
                        new Vector3(fatihXs[i], 0f, 125.5f), faceNorth, Vector3.one,
                        new Vector3(14.2f, 13.5f, 9.6f), new Vector3(0f, 6.75f, 0f), mats);
                    if (sBlk != null && (i == 1 || i == 2 || i == 4))
                    {
                        MountBlenderBalcony(sBlk.transform, "Sultanbeyli_Balcony_CamBalkon.fbx", $"FatihS_{i}_BalcF2", new Vector3(-7.10f, 3.80f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                        MountBlenderBalcony(sBlk.transform, "Sultanbeyli_Balcony_ClassicIron.fbx", $"FatihS_{i}_BalcF3", new Vector3(7.10f, 6.90f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                    }
                }
            }
            // Fatih Bulvarı Living Hub: Taxi Stand Job Board, Fatih Kafe & Pastane, Bank ATM, Vending & Simitçi!
            CreateJobStartRing("JobBoard_Taxi", "job_board_taxi", JobType.Taxi, "job.taxi.name", "job.taxi.loc",
                fatihRoot.transform, new Vector3(12.0f, 0.06f, 132.0f), 4.4f, mats.JobRingGlow);
            CreateStreetVendorStall(
                "Vendor_FatihKafe", "shop_coffee", "Selim Usta", "Fatih Kafe & Pastane", "FATİH KAFE & ÇAY",
                fatihRoot.transform, new Vector3(-12.5f, 0.04f, 131.2f), Quaternion.identity,
                mats.TownAwningNavy, BlenderCharacterVariant.CasualMale,
                mats.PedShirtTeal, mats.CharPants, mats.CharSkin, mats.CharHairBrown, mats);
            CreateAvenueAtm("Fatih_SaatKulesi_ATM", "atm_fatih_saat", "Fatih Bulvarı ATM · Saat Kulesi",
                fatihRoot.transform, new Vector3(-22.0f, 0.04f, 131.0f), 0f, mats);
            CreateVendingMachine("Vending_Fatih", "vending_fatih_01", fatihRoot.transform, new Vector3(22.0f, 0.04f, 131.0f), Quaternion.identity, mats);
            PlaceAvenueSimitCart("Fatih_SimitCart", fatihRoot.transform, new Vector3(4.5f, 0.16f, 141.0f), 165f, mats);
            CreateTownTree("Fatih_SidewalkTree_W", fatihRoot.transform, new Vector3(-42f, 0.04f, 131.2f), mats, 1.12f, 903);
            CreateTownTree("Fatih_SidewalkTree_E", fatihRoot.transform, new Vector3(36f, 0.04f, 131.2f), mats, 1.15f, 904);
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_Fatih_E", fatihRoot.transform,
                new Vector3(64f, 0.04f, 131.2f), Quaternion.identity, Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            CreateIstanbulRedStreetSign(fatihRoot.transform, "FATİH BULVARI", new Vector3(-76f, 0f, 132f), 0f, mats);
            CreateIstanbulRedStreetSign(fatihRoot.transform, "FATİH BULVARI", new Vector3(92f, 0f, 132f), 0f, mats);

            // =========================================================================
            // 3. AVENUE 2: ATATÜRK CADDESİ (TEM O-4 Guardrail & Pine Embankment + Balconies!)
            // =========================================================================
            GameObject ataturkRoot = new GameObject("Avenue_02_AtaturkCaddesi");
            ataturkRoot.transform.SetParent(hamRoot.transform, false);

            for (int gx = -132; gx <= 132; gx += 12)
            {
                if (Mathf.Abs(gx + 85) < 9 || Mathf.Abs(gx - 99) < 9) continue;
                PlaceSelmanModel("Hamidiye_AtaturkHighwayGuardrail.fbx", $"Ataturk_Guardrail_{gx}", ataturkRoot.transform,
                    new Vector3(gx, 0.04f, 102.2f), Quaternion.identity, Vector3.one,
                    new Vector3(12f, 1.0f, 0.35f), new Vector3(0f, 0.5f, 0f), mats);
                if (gx % 24 == 0)
                {
                    CreateTownTree($"Ataturk_Pine_{gx}", ataturkRoot.transform, new Vector3(gx, 0.04f, 106.5f), mats, 1.35f, 920 + gx);
                }
            }
            float[] ataturkXs = { -116f, -52f, -12f, 32f, 70f, 126f };
            for (int i = 0; i < ataturkXs.Length; i++)
            {
                bool isBranda = (i % 2 == 0);
                string fbx = isBranda ? "Hamidiye_PetrolYoluBrandaBlock.fbx" : "Hamidiye_SeyyidKutupApt.fbx";
                GameObject blk = PlaceSelmanModel(fbx, $"Ataturk_SouthBldg_{i + 1}", ataturkRoot.transform,
                    new Vector3(ataturkXs[i], 0f, 82.0f), faceNorth, Vector3.one,
                    new Vector3(12f, 11f, 9.2f), new Vector3(0f, 5.5f, 0f), mats);
                if (blk != null && (i == 2 || i == 3))
                {
                    float sideX = isBranda ? 5.60f : 6.20f;
                    MountBlenderBalcony(blk.transform, "Sultanbeyli_Balcony_ClassicIron.fbx", $"Ataturk_{i}_BalcF2", new Vector3(-sideX, 3.65f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                    MountBlenderBalcony(blk.transform, "Sultanbeyli_Balcony_MasonryCorner.fbx", $"Ataturk_{i}_BalcF3", new Vector3(sideX, 6.70f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                    if (i == 3)
                    {
                        AttachSelmanPaintableAddress(blk, "hamidiye_ataturk_30", "Atatürk Caddesi No: 30", new Vector3(ataturkXs[i], 0.12f, 89.2f));
                    }
                }
            }
            var (ab1, ab2) = CreateBench("Ataturk_Bench_1", ataturkRoot.transform, new Vector3(-24f, 0.04f, 89.6f), Quaternion.identity, mats);
            seats.Add(ab1); seats.Add(ab2);
            var (ab3, ab4) = CreateBench("Ataturk_Bench_2", ataturkRoot.transform, new Vector3(46f, 0.04f, 89.6f), Quaternion.identity, mats);
            seats.Add(ab3); seats.Add(ab4);
            CreateMailbox("Ataturk_Mailbox", "mailbox_ataturk", ataturkRoot.transform, new Vector3(10f, 0.04f, 89.4f), Quaternion.identity, mats);
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_Ataturk", ataturkRoot.transform,
                new Vector3(-68f, 0.04f, 89.4f), Quaternion.identity, Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            CreateIstanbulRedStreetSign(ataturkRoot.transform, "ATATÜRK CADDESİ", new Vector3(-78f, 0f, 90f), 0f, mats);
            CreateIstanbulRedStreetSign(ataturkRoot.transform, "ATATÜRK CADDESİ", new Vector3(92f, 0f, 90f), 0f, mats);

            // =========================================================================
            // 4. AVENUE 3: MÜRŞİT CADDESİ (AYEDAŞ Trafo + Çay & Tost Köşesi + Otomat!)
            // =========================================================================
            GameObject mursitRoot = new GameObject("Avenue_03_MursitCaddesi");
            mursitRoot.transform.SetParent(hamRoot.transform, false);

            PlaceSelmanModel("Sultanbeyli_HighVoltagePylon.fbx", "Mursit_HVPylon", mursitRoot.transform,
                new Vector3(-18f, 0f, 56.0f), Quaternion.identity, Vector3.one,
                new Vector3(5.8f, 26f, 5.8f), new Vector3(0f, 13f, 0f), mats);

            float[] mursitXs = { -118f, -52f, 2f, 40f, 78f, 124f };
            for (int i = 0; i < mursitXs.Length; i++)
            {
                GameObject mBlk = PlaceSelmanModel("Hamidiye_MursitTrafoAndGreyApt.fbx", $"Mursit_Block_{i + 1}", mursitRoot.transform,
                    new Vector3(mursitXs[i], 0f, 57.5f), faceSouth, Vector3.one,
                    new Vector3(16f, 9.6f, 8.8f), new Vector3(0f, 4.8f, 0f), mats);
                if (mBlk != null && (i == 2 || i == 3))
                {
                    MountBlenderBalcony(mBlk.transform, "Sultanbeyli_Balcony_CamBalkon.fbx", $"Mursit_{i}_BalcF2", new Vector3(-2.5f, 3.35f, 4.42f), Quaternion.identity, mats);
                    if (i == 2)
                    {
                        AttachSelmanPaintableAddress(mBlk, "hamidiye_mursit_14", "Mürşit Caddesi No: 14", new Vector3(mursitXs[i], 0.12f, 51.4f));
                    }
                }
            }
            CreateStreetVendorStall(
                "Vendor_MursitCayTost", "shop_coffee", "Kemal Abi", "Mürşit Çay & Tost", "MÜRŞİT ÇAY & TOST",
                mursitRoot.transform, new Vector3(14.5f, 0.04f, 51.5f), faceSouth,
                mats.TownAwningGreen, BlenderCharacterVariant.CasualMale,
                mats.PedShirtOlive, mats.CharPantsKhaki, mats.CharSkinWarm, mats.CharHair, mats);
            CreateVendingMachine("Vending_Mursit", "vending_mursit_01", mursitRoot.transform, new Vector3(24.0f, 0.04f, 51.6f), faceSouth, mats);
            var (mb1, mb2) = CreateBench("Mursit_Bench_1", mursitRoot.transform, new Vector3(-34f, 0.04f, 40.6f), Quaternion.identity, mats);
            seats.Add(mb1); seats.Add(mb2);
            CreateTownTree("Mursit_Tree_1", mursitRoot.transform, new Vector3(-42f, 0.04f, 51.6f), mats, 1.08f, 931);
            CreateTownTree("Mursit_Tree_2", mursitRoot.transform, new Vector3(50f, 0.04f, 51.6f), mats, 1.12f, 932);
            CreateIstanbulRedStreetSign(mursitRoot.transform, "MÜRŞİT CADDESİ", new Vector3(53f, 0f, 41f), 0f, mats);
            CreateIstanbulRedStreetSign(mursitRoot.transform, "MÜRŞİT CADDESİ", new Vector3(-78f, 0f, 41f), 0f, mats);

            // =========================================================================
            // 5. AVENUE 4: EYYUBİ CADDESİ (53 Eyyubi Mahalle Büfesi + Balkonlu Evler!)
            // =========================================================================
            GameObject eyyubiRoot = new GameObject("Avenue_04_EyyubiCaddesi");
            eyyubiRoot.transform.SetParent(hamRoot.transform, false);

            float[] eyyubiZs = { -128f, -96f, -46f, -18f, 22f, 70f, 118f };
            for (int i = 0; i < eyyubiZs.Length; i++)
            {
                GameObject wBufe = PlaceSelmanModel("Hamidiye_EyyubiBufeBlock.fbx", $"Eyyubi_WestBufe_{i + 1}", eyyubiRoot.transform,
                    new Vector3(-160.5f, 0f, eyyubiZs[i]), faceEast, Vector3.one,
                    new Vector3(8.0f, 6.4f, 14.0f), new Vector3(0f, 3.2f, 0f), mats);
                if (wBufe != null && (i == 3 || i == 4))
                {
                    MountBlenderBalcony(wBufe.transform, "Sultanbeyli_Balcony_ClassicIron.fbx", $"EyyubiW_{i}_Balc", new Vector3(-1.5f, 3.35f, 3.82f), Quaternion.identity, mats);
                    if (i == 4)
                    {
                        AttachSelmanPaintableAddress(wBufe, "hamidiye_eyyubi_53", "Eyyubi Caddesi No: 53", new Vector3(-153.5f, 0.12f, eyyubiZs[i]));
                    }
                }
                if (Mathf.Abs(eyyubiZs[i]) > 14f)
                {
                    GameObject eApt = PlaceSelmanModel("Hamidiye_SeyyidKutupApt.fbx", $"Eyyubi_EastApt_{i + 1}", eyyubiRoot.transform,
                        new Vector3(-135.5f, 0f, eyyubiZs[i]), faceWest, Vector3.one,
                        new Vector3(9.4f, 10.5f, 12.6f), new Vector3(0f, 5.25f, 0f), mats);
                    if (eApt != null && (i == 2 || i == 3))
                    {
                        MountBlenderBalcony(eApt.transform, "Sultanbeyli_Balcony_MasonryCorner.fbx", $"EyyubiE_{i}_BalcF2", new Vector3(-6.20f, 3.55f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                    }
                }
            }
            // Interactive 53 Eyyubi Mahalle Büfesi (sells cold water, snacks, ayran with full drink/eat animation!)
            CreateStreetVendorStall(
                "Vendor_Eyyubi53Bufe", "shop_bakkal", "Murat Abi", "53 Eyyubi Mahalle Büfesi", "53 EYYUBİ BÜFESİ",
                eyyubiRoot.transform, new Vector3(-153.6f, 0.04f, -18.0f), faceEast,
                mats.TownAwningTerracotta, BlenderCharacterVariant.CasualMale,
                mats.PedShirtRed, mats.CharPantsDenim, mats.CharSkinMedium, mats.CharHair, mats);
            CreateVendingMachine("Vending_Eyyubi", "vending_eyyubi_01", eyyubiRoot.transform, new Vector3(-153.6f, 0.04f, -10.5f), faceEast, mats);
            var (eb1, eb2) = CreateBench("Eyyubi_Bench_1", eyyubiRoot.transform, new Vector3(-142.3f, 0.04f, -24.0f), faceWest, mats);
            seats.Add(eb1); seats.Add(eb2);
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_Eyyubi", eyyubiRoot.transform,
                new Vector3(-153.5f, 0.04f, 12.0f), faceEast, Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            CreateTownTree("Eyyubi_Tree_1", eyyubiRoot.transform, new Vector3(-142.0f, 0.04f, 14.0f), mats, 1.10f, 941);
            CreateTownTree("Eyyubi_Tree_2", eyyubiRoot.transform, new Vector3(-142.0f, 0.04f, -56.0f), mats, 1.15f, 942);
            CreateIstanbulRedStreetSign(eyyubiRoot.transform, "EYYUBİ CADDESİ", new Vector3(-142f, 0f, 8f), 90f, mats);
            CreateIstanbulRedStreetSign(eyyubiRoot.transform, "EYYUBİ CADDESİ", new Vector3(-142f, 0f, -70f), 90f, mats);

            // =========================================================================
            // 6. AVENUE 5: SEYYİD KUTUP CADDESİ (3-Story Tan Apartments + Balconies!)
            // =========================================================================
            GameObject seyyidRoot = new GameObject("Avenue_05_SeyyidKutupCaddesi");
            seyyidRoot.transform.SetParent(hamRoot.transform, false);

            float[] seyyidZs = { -130f, -96f, -52f, 68f, 118f };
            for (int i = 0; i < seyyidZs.Length; i++)
            {
                GameObject wApt = PlaceSelmanModel("Hamidiye_SeyyidKutupApt.fbx", $"SeyyidKutup_WestApt_{i + 1}", seyyidRoot.transform,
                    new Vector3(-97.5f, 0f, seyyidZs[i]), faceEast, Vector3.one,
                    new Vector3(9.4f, 10.5f, 12.6f), new Vector3(0f, 5.25f, 0f), mats);
                GameObject eApt = PlaceSelmanModel("Hamidiye_SeyyidKutupApt.fbx", $"SeyyidKutup_EastApt_{i + 1}", seyyidRoot.transform,
                    new Vector3(-72.5f, 0f, seyyidZs[i]), faceWest, Vector3.one,
                    new Vector3(9.4f, 10.5f, 12.6f), new Vector3(0f, 5.25f, 0f), mats);
                if (wApt != null && (i == 2 || i == 3))
                {
                    MountBlenderBalcony(wApt.transform, "Sultanbeyli_Balcony_ClassicIron.fbx", $"SeyyidW_{i}_BalcF2", new Vector3(6.20f, 3.55f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                    if (i == 3)
                    {
                        AttachSelmanPaintableAddress(wApt, "hamidiye_seyyid_22", "Seyyid Kutup Cd. No: 22", new Vector3(-90.4f, 0.12f, seyyidZs[i]));
                    }
                }
                if (eApt != null && (i == 2 || i == 3))
                {
                    MountBlenderBalcony(eApt.transform, "Sultanbeyli_Balcony_CamBalkon.fbx", $"SeyyidE_{i}_BalcF2", new Vector3(-6.20f, 3.55f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                }
            }
            var (sb1, sb2) = CreateBench("Seyyid_Bench_1", seyyidRoot.transform, new Vector3(-90.4f, 0.04f, 62f), faceEast, mats);
            seats.Add(sb1); seats.Add(sb2);
            CreateMailbox("Seyyid_Mailbox", "mailbox_seyyid", seyyidRoot.transform, new Vector3(-79.6f, 0.04f, -46f), faceWest, mats);
            CreateTownTree("Seyyid_Tree_1", seyyidRoot.transform, new Vector3(-90.2f, 0.04f, -38f), mats, 1.05f, 951);
            CreateTownTree("Seyyid_Tree_2", seyyidRoot.transform, new Vector3(-79.8f, 0.04f, 78f), mats, 1.08f, 952);

            // =========================================================================
            // 7. AVENUE 6: PETROL YOLU CADDESİ (Akaryakıt #2, Sokak Temizliği, Büfe & ATM!)
            // =========================================================================
            GameObject petrolRoot = new GameObject("Avenue_06_PetrolYoluCaddesi");
            petrolRoot.transform.SetParent(hamRoot.transform, false);

            float[] petrolXs = { -120f, -56f, -18f, 18f, 54f, 126f };
            for (int i = 0; i < petrolXs.Length; i++)
            {
                GameObject nBlk = PlaceSelmanModel("Hamidiye_PetrolYoluBrandaBlock.fbx", $"PetrolYolu_NorthBlock_{i + 1}", petrolRoot.transform,
                    new Vector3(petrolXs[i], 0f, -64.5f), faceSouth, Vector3.one,
                    new Vector3(11.4f, 12.8f, 9.4f), new Vector3(0f, 6.4f, 0f), mats);
                GameObject sBlk = PlaceSelmanModel("Hamidiye_PetrolYoluBrandaBlock.fbx", $"PetrolYolu_SouthBlock_{i + 1}", petrolRoot.transform,
                    new Vector3(petrolXs[i], 0f, -91.5f), faceNorth, Vector3.one,
                    new Vector3(11.4f, 12.8f, 9.4f), new Vector3(0f, 6.4f, 0f), mats);
                if (nBlk != null && (i == 2 || i == 3))
                {
                    MountBlenderBalcony(nBlk.transform, "Sultanbeyli_Balcony_CamBalkon.fbx", $"PetrolN_{i}_BalcF2", new Vector3(5.60f, 3.70f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                    MountBlenderBalcony(nBlk.transform, "Sultanbeyli_Balcony_ClassicIron.fbx", $"PetrolN_{i}_BalcF3", new Vector3(-5.60f, 6.70f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                }
                if (sBlk != null && (i == 1 || i == 3))
                {
                    MountBlenderBalcony(sBlk.transform, "Sultanbeyli_Balcony_MasonryCorner.fbx", $"PetrolS_{i}_BalcF2", new Vector3(-5.60f, 3.70f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                }
            }
            CreateWorldSignText("PETROL YOLU CD. · TENTE & TİCARET", petrolRoot.transform,
                new Vector3(-18f, 4.15f, -69.5f), Quaternion.identity, new Vector2(5.2f, 0.45f), new Color(0.84f, 0.34f, 0.10f, 1f));

            // Petrol Yolu Living Hub: Street Cleanup Job Ring, Fuel Pump #2, Petrol Yolu Büfe & Market, ATM, Dumpsters!
            CreateJobStartRing("JobBoard_Cleanup", "job_board_cleanup", JobType.StreetCleanup, "job.cleanup.name", "job.cleanup.loc",
                petrolRoot.transform, new Vector3(-18.0f, 0.06f, -71.8f), 4.0f, mats.JobRingGlow);
            CreateFuelPump("PetrolYolu_FuelPump", "fuel_pump_petrol", petrolRoot.transform, new Vector3(36.0f, 0.04f, -71.5f), mats);
            CreateStreetVendorStall(
                "Vendor_PetrolYoluBufe", "shop_bakkal", "Orhan Usta", "Petrol Yolu Market & Büfe", "PETROL YOLU BÜFE",
                petrolRoot.transform, new Vector3(2.0f, 0.04f, -71.2f), faceSouth,
                mats.TownAwningCream, BlenderCharacterVariant.Worker,
                mats.PedShirtBurgundy, mats.CharPants, mats.CharSkinMedium, mats.CharHairBrown, mats);
            CreateAvenueAtm("PetrolYolu_ATM", "atm_petrol_yolu", "Petrol Yolu Cd. ATM",
                petrolRoot.transform, new Vector3(-34.0f, 0.04f, -71.2f), 180f, mats);
            var (pb1, pb2) = CreateBench("Petrol_Bench_1", petrolRoot.transform, new Vector3(-4.0f, 0.04f, -84.4f), Quaternion.identity, mats);
            seats.Add(pb1); seats.Add(pb2);
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_Petrol_W", petrolRoot.transform,
                new Vector3(-44.0f, 0.04f, -84.4f), Quaternion.identity, Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_Petrol_E", petrolRoot.transform,
                new Vector3(66.0f, 0.04f, -71.2f), faceSouth, Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            CreateTownTree("Petrol_Tree_1", petrolRoot.transform, new Vector3(-68f, 0.04f, -71.5f), mats, 1.05f, 955);
            CreateTownTree("Petrol_Tree_2", petrolRoot.transform, new Vector3(72f, 0.04f, -84.5f), mats, 1.12f, 956);
            CreateIstanbulRedStreetSign(petrolRoot.transform, "PETROL YOLU CD.", new Vector3(-78f, 0f, -72f), 0f, mats);
            CreateIstanbulRedStreetSign(petrolRoot.transform, "PETROL YOLU CD.", new Vector3(92f, 0f, -72f), 0f, mats);

            // =========================================================================
            // 8. AVENUE 7: AZİZİYE CADDESİ (İTO İlkokulu + Okul Önü Dondurma & Simit!)
            // =========================================================================
            GameObject aziziyeRoot = new GameObject("Avenue_07_AziziyeCaddesi");
            aziziyeRoot.transform.SetParent(hamRoot.transform, false);

            float[] aziziyeXs = { -118f, -54f, -16f, 24f, 62f, 126f };
            for (int i = 0; i < aziziyeXs.Length; i++)
            {
                GameObject sShop = PlaceSelmanModel("Hamidiye_AziziyeShopBlock.fbx", $"Aziziye_SouthShop_{i + 1}", aziziyeRoot.transform,
                    new Vector3(aziziyeXs[i], 0f, -126.5f), faceNorth, Vector3.one,
                    new Vector3(13.4f, 12.4f, 9.0f), new Vector3(0f, 6.2f, 0f), mats);
                if (sShop != null && (i == 1 || i == 3))
                {
                    MountBlenderBalcony(sShop.transform, "Sultanbeyli_Balcony_ClassicIron.fbx", $"Aziziye_{i}_BalcF2", new Vector3(-6.70f, 3.60f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                    MountBlenderBalcony(sShop.transform, "Sultanbeyli_Balcony_CamBalkon.fbx", $"Aziziye_{i}_BalcF3", new Vector3(6.70f, 6.60f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                }
            }
            CreateWorldSignText("İTO ŞEHİT ER DURSUN SIVAZ İLKOKULU", aziziyeRoot.transform,
                new Vector3(-16f, 4.5f, -121.6f), Quaternion.Euler(0f, 180f, 0f), new Vector2(6.4f, 0.56f), new Color(0.12f, 0.24f, 0.52f, 1f));

            // School-front Ice Cream & Cold Water Stand + Simit Cart + Benches + Trees!
            CreateStreetVendorStall(
                "Vendor_AziziyeOkulDondurma", "shop_dondurma", "Ayşe Abla", "Okul Önü Dondurma & Su", "DONDURMA & SOĞUK SU",
                aziziyeRoot.transform, new Vector3(-2.5f, 0.04f, -119.8f), Quaternion.identity,
                mats.TownAwningTerracotta, BlenderCharacterVariant.CasualFemale,
                mats.PedShirtMustard, mats.CharPants, mats.CharSkin, mats.CharHairBrown, mats);
            PlaceAvenueSimitCart("Aziziye_SchoolSimitCart", aziziyeRoot.transform, new Vector3(-28.0f, 0.04f, -119.8f), 15f, mats);
            var (azb1, azb2) = CreateBench("Aziziye_SchoolBench_1", aziziyeRoot.transform, new Vector3(-16.0f, 0.04f, -119.6f), Quaternion.identity, mats);
            seats.Add(azb1); seats.Add(azb2);
            var (azb3, azb4) = CreateBench("Aziziye_SchoolBench_2", aziziyeRoot.transform, new Vector3(38.0f, 0.04f, -119.6f), Quaternion.identity, mats);
            seats.Add(azb3); seats.Add(azb4);
            CreateTownTree("Aziziye_SchoolTree_1", aziziyeRoot.transform, new Vector3(-36f, 0.04f, -119.8f), mats, 1.15f, 957);
            CreateTownTree("Aziziye_SchoolTree_2", aziziyeRoot.transform, new Vector3(10f, 0.04f, -119.8f), mats, 1.10f, 958);
            PlaceSelmanModel("Sultanbeyli_Dumpster.fbx", "Dumpster_Aziziye", aziziyeRoot.transform,
                new Vector3(48f, 0.04f, -119.8f), Quaternion.identity, Vector3.one,
                new Vector3(1.45f, 1.25f, 1.05f), new Vector3(0f, 0.62f, 0f), mats);
            CreateIstanbulRedStreetSign(aziziyeRoot.transform, "AZİZİYE CADDESİ", new Vector3(-78f, 0f, -108f), 0f, mats);
            CreateIstanbulRedStreetSign(aziziyeRoot.transform, "AZİZİYE CADDESİ", new Vector3(92f, 0f, -108f), 0f, mats);

            // =========================================================================
            // 9. AVENUE 8: ZÜBEYDE HANIM CADDESİ (Ev Boyama İş Panosu + Balkonlu Mavi Evler!)
            // =========================================================================
            GameObject zubeydeRoot = new GameObject("Avenue_08_ZubeydeHanimCaddesi");
            zubeydeRoot.transform.SetParent(hamRoot.transform, false);

            float[] zubeydeXs = { -122f, -58f, -24f, 14f, 52f, 124f };
            for (int i = 0; i < zubeydeXs.Length; i++)
            {
                GameObject zHouse = PlaceSelmanModel("Hamidiye_ZubeydeBlueHouse.fbx", $"Zubeyde_SouthHouse_{i + 1}", zubeydeRoot.transform,
                    new Vector3(zubeydeXs[i], 0f, -158.0f), faceNorth, Vector3.one,
                    new Vector3(9.6f, 9.2f, 8.8f), new Vector3(0f, 4.6f, 0f), mats);
                CreateTownTree($"Zubeyde_Pine_{i + 1}", zubeydeRoot.transform, new Vector3(zubeydeXs[i] + 7.5f, 0.04f, -152.5f), mats, 1.25f, 960 + i);
                if (zHouse != null)
                {
                    if (i % 2 == 0)
                    {
                        MountBlenderBalcony(zHouse.transform, "Sultanbeyli_Balcony_ClassicIron.fbx", $"Zubeyde_{i}_BalcW", new Vector3(-4.70f, 3.25f, 0f), Quaternion.Euler(0f, -90f, 0f), mats);
                    }
                    else
                    {
                        MountBlenderBalcony(zHouse.transform, "Sultanbeyli_Balcony_MasonryCorner.fbx", $"Zubeyde_{i}_BalcE", new Vector3(4.70f, 3.25f, 0f), Quaternion.Euler(0f, 90f, 0f), mats);
                    }
                    if (i == 2)
                    {
                        AttachSelmanPaintableAddress(zHouse, "hamidiye_zubeyde_71", "Zübeyde Hanım Cd. No: 71", new Vector3(zubeydeXs[i], 0.12f, -151.6f));
                    }
                    else if (i == 3)
                    {
                        AttachSelmanPaintableAddress(zHouse, "hamidiye_zubeyde_75", "Zübeyde Hanım Cd. No: 75", new Vector3(zubeydeXs[i], 0.12f, -151.6f));
                    }
                }
            }
            CreateJobStartRing("JobBoard_HousePainting", "job_board_painting", JobType.HousePainting, "job.paint.name", "job.paint.loc",
                zubeydeRoot.transform, new Vector3(14.0f, 0.06f, -140.4f), 4.0f, mats.JobRingGlow);
            var (zb1, zb2) = CreateBench("Zubeyde_Bench_1", zubeydeRoot.transform, new Vector3(-8f, 0.04f, -151.6f), Quaternion.identity, mats);
            seats.Add(zb1); seats.Add(zb2);
            var (zb3, zb4) = CreateBench("Zubeyde_Bench_2", zubeydeRoot.transform, new Vector3(36f, 0.04f, -151.6f), Quaternion.identity, mats);
            seats.Add(zb3); seats.Add(zb4);
            CreateIstanbulRedStreetSign(zubeydeRoot.transform, "ZÜBEYDE HANIM CD.", new Vector3(-78f, 0f, -140f), 0f, mats);
            CreateIstanbulRedStreetSign(zubeydeRoot.transform, "ZÜBEYDE HANIM CD.", new Vector3(92f, 0f, -140f), 0f, mats);

            // =========================================================================
            // 10. DRIVABLE PARKED CARS DISTRIBUTED ACROSS ALL 8 AVENUES (8 cars!)
            // =========================================================================
            if (prefabs != null)
            {
                GameObject aveCarsRoot = new GameObject("Hamidiye_Avenues_DrivableCars");
                aveCarsRoot.transform.SetParent(hamRoot.transform, false);

                (GameObject prefab, Vector3 pos, float yaw, string label)[] aveCars =
                {
                    (prefabs.TownCarYellowSportsCoupe, new Vector3(-28.0f, 0.25f,  134.8f),  90f, "Car_Fatih_SaatKulesiCoupe"),
                    (prefabs.TownCarGreenSedan,        new Vector3( 28.0f, 0.25f,   92.4f), 270f, "Car_Ataturk_GreenSedan"),
                    (prefabs.TownCarWhitePickup,       new Vector3( 26.0f, 0.25f,   43.4f),  90f, "Car_Mursit_Pickup"),
                    (prefabs.TownCarRedHatchback,      new Vector3(-150.6f,0.25f,  -28.0f),   0f, "Car_Eyyubi_Hatchback"),
                    (prefabs.TownCarCharcoalSUV,       new Vector3(-87.4f, 0.25f,   66.0f), 180f, "Car_SeyyidKutup_SUV"),
                    (prefabs.TownCarWhiteVan,          new Vector3(-30.0f, 0.25f,  -74.6f),  90f, "Car_PetrolYolu_DeliveryVan"),
                    (prefabs.TownCarBlueHatchback,     new Vector3(-10.0f, 0.25f, -110.8f), 270f, "Car_Aziziye_SchoolCar"),
                    (prefabs.TownCarGreenSedan,        new Vector3( 22.0f, 0.25f, -143.0f),  90f, "Car_Zubeyde_Sedan")
                };
                for (int i = 0; i < aveCars.Length; i++)
                {
                    if (aveCars[i].prefab == null) continue;
                    GameObject car = PlacePrefabInstance(aveCars[i].prefab, aveCarsRoot.transform, aveCars[i].pos, Quaternion.Euler(0f, aveCars[i].yaw, 0f));
                    car.name = aveCars[i].label;
                }
            }

            // =========================================================================
            // 11. ROAMING CITIZENS & PETTABLE STREET CATS ACROSS ALL 8 AVENUES!
            // =========================================================================
            BuildHamidiyeAvenueCitizensAndCats(hamRoot.transform, mats, seats);

            // =========================================================================
            // 12. WARM NIGHT STREETLAMPS ALONG THE 8 MAIN AVENUES (Mobile-budgeted 16 lamps!)
            // =========================================================================
            Vector3[] avenueLampCoords =
            {
                new Vector3(-40f, 0.04f, 133f), new Vector3(40f, 0.04f, 133f),
                new Vector3(-40f, 0.04f,  91f), new Vector3(40f, 0.04f,  91f),
                new Vector3(-40f, 0.04f,  41f), new Vector3(40f, 0.04f,  41f),
                new Vector3(-40f, 0.04f, -72f), new Vector3(40f, 0.04f, -72f),
                new Vector3(-40f, 0.04f,-109f), new Vector3(40f, 0.04f,-109f),
                new Vector3(-40f, 0.04f,-141f), new Vector3(40f, 0.04f,-141f),
                new Vector3(-143f,0.04f,  40f), new Vector3(-143f,0.04f,-40f),
                new Vector3(104f, 0.04f,  40f), new Vector3(104f, 0.04f,-40f)
            };
            Color warmSodium = new Color(1.0f, 0.80f, 0.48f);
            for (int i = 0; i < avenueLampCoords.Length; i++)
            {
                GameObject lamp = PlaceSelmanModel("Sultanbeyli_StreetLamp.fbx", $"Hamidiye_AveLamp_{i + 1:00}", hamRoot.transform,
                    avenueLampCoords[i], Quaternion.identity, Vector3.one,
                    new Vector3(0.32f, 5.8f, 0.32f), new Vector3(0f, 2.9f, 0f), mats);
                if (lamp != null)
                {
                    CreatePointLamp($"AveLampLight_{i + 1:00}", lamp.transform, avenueLampCoords[i] + new Vector3(0f, 5.8f, 1.4f), warmSodium, 22f, 3.2f);
                }
            }
        }

        private static void CreateAvenueAtm(string objName, string id, string label, Transform parent, Vector3 pos, float yaw, MaterialSet mats)
        {
            GameObject atmGo = PlaceSelmanModel(
                "Istanbul_ATM.fbx",
                objName,
                parent,
                pos,
                Quaternion.Euler(0f, yaw, 0f),
                Vector3.one,
                new Vector3(1.05f, 2.2f, 0.85f),
                new Vector3(0f, 1.1f, 0f),
                mats);
            if (atmGo == null) return;

            AtmTerminalInteractable atm = atmGo.AddComponent<AtmTerminalInteractable>();
            atm.Configure(label);
            InteractableTrigger atmTrig = atmGo.AddComponent<InteractableTrigger>();
            atmTrig.Configure(InteractableKind.AtmTerminal, id, "ui.btn.use_atm", 2.8f);
        }

        private static void PlaceAvenueSimitCart(string objName, Transform parent, Vector3 pos, float yaw, MaterialSet mats)
        {
            GameObject simitGo = new GameObject(objName);
            simitGo.transform.SetParent(parent, false);
            simitGo.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            InstantiateBlenderModel("Istanbul_SimitCart.fbx", $"{objName}_Mesh", simitGo.transform, mats);
            BoxCollider sc = simitGo.AddComponent<BoxCollider>();
            sc.center = new Vector3(0f, 0.85f, 0f);
            sc.size = new Vector3(1.5f, 1.7f, 1.0f);
            DisableShadowCasting(simitGo);
            MarkStaticEnvironment(simitGo);
        }

        private static void BuildHamidiyeAvenueCitizensAndCats(Transform parent, MaterialSet mats, List<SeatInteractable> seats)
        {
            GameObject lifeRoot = new GameObject("Hamidiye_AvenueCitizens_And_Cats");
            lifeRoot.transform.SetParent(parent, false);

            // 8 Roaming Citizens living across all 8 Hamidiye Avenues!
            (string name, string role, BlenderCharacterVariant variant, Material shirt, Material pants, Material skin, Material hair, float height, Vector3[] route, string[] lines)[] aveCitizens =
            {
                (
                    "Emre", "Fatih Bulvarı · Saat Kulesi Sakini", BlenderCharacterVariant.CasualMale,
                    mats.PedShirtTeal, mats.CharPantsDenim, mats.CharSkin, mats.CharHairBrown, 1.78f,
                    new[] { new Vector3(-28f, 0.06f, 131.3f), new Vector3(26f, 0.06f, 131.3f), new Vector3(0f, 0.06f, 131.3f) },
                    new[]
                    {
                        "Fatih Bulvarı akşamları çok canlı olur, Saat Kulesi Meydanı Sultanbeyli'nin kalbidir!",
                        "Taksi durağı hemen buradan kalkıyor, bütün mahalleyi gezebilirsin."
                    }
                ),
                (
                    "Zeynep Hanım", "Fatih Bulvarı Alışveriş", BlenderCharacterVariant.CasualFemale,
                    mats.PedShirtBurgundy, mats.CharPants, mats.CharSkinWarm, mats.CharHair, 1.67f,
                    new[] { new Vector3(44f, 0.06f, 152.7f), new Vector3(-16f, 0.06f, 152.7f), new Vector3(16f, 0.06f, 152.7f) },
                    new[]
                    {
                        "Yeni cam balkonlu daireler bulvara ayrı bir hava kattı.",
                        "Selim Usta'nın kafesinde demli çay içmeden eve dönmem!"
                    }
                ),
                (
                    "Serkan", "Atatürk Cd. Koşucusu", BlenderCharacterVariant.CasualMale,
                    mats.PedShirtOlive, mats.CharPantsKhaki, mats.CharSkinMedium, mats.CharHair, 1.80f,
                    new[] { new Vector3(-56f, 0.06f, 89.5f), new Vector3(48f, 0.06f, 89.5f), new Vector3(-8f, 0.06f, 89.5f) },
                    new[]
                    {
                        "Atatürk Caddesi'nin çam kokusu sabah yürüyüşü için harika!",
                        "TEM kenarındaki korkulukların arkasında koca çam korusu uzanıyor."
                    }
                ),
                (
                    "Ahmet Abi", "Mürşit Cd. Esnafı", BlenderCharacterVariant.Worker,
                    mats.PedShirtMustard, mats.CharPantsDenim, mats.CharSkinMedium, mats.CharHairBrown, 1.77f,
                    new[] { new Vector3(-36f, 0.06f, 51.4f), new Vector3(38f, 0.06f, 51.4f), new Vector3(4f, 0.06f, 51.4f) },
                    new[]
                    {
                        "Mürşit Caddesi'nden Nutuk Sokağı'na inince doğrudan Selman Sokağı'na çıkarsın.",
                        "Kemal Abi'nin çay ve tost köşesi mahallenin favorisidir."
                    }
                ),
                (
                    "Hakan", "Eyyubi Cd. Sakini", BlenderCharacterVariant.CasualMale,
                    mats.PedShirtRed, mats.CharPants, mats.CharSkin, mats.CharHair, 1.76f,
                    new[] { new Vector3(-153.6f, 0.06f, -52f), new Vector3(-153.6f, 0.06f, 24f), new Vector3(-153.6f, 0.06f, -16f) },
                    new[]
                    {
                        "53 Eyyubi Büfesi'nden soğuk su alıp yürümek yaz günlerinde ilaç gibi geliyor!",
                        "Eyyubi Caddesi'nin ferforje balkonları akşamları hep çiçek doludur."
                    }
                ),
                (
                    "Derya", "Petrol Yolu Cd. Çalışanı", BlenderCharacterVariant.CasualFemale,
                    mats.PedShirtTeal, mats.CharPantsDenim, mats.CharSkinWarm, mats.CharHairBlonde, 1.69f,
                    new[] { new Vector3(-54f, 0.06f, -71.5f), new Vector3(34f, 0.06f, -71.5f), new Vector3(-14f, 0.06f, -71.5f) },
                    new[]
                    {
                        "Petrol Yolu Caddesi'nde hem akaryakıt hem ATM hem de market bir arada.",
                        "Sokak temizliği görevini buradan başlatıp caddeyi pırıl pırıl yapabilirsin!"
                    }
                ),
                (
                    "Merve Öğretmen", "İTO İlkokulu · Aziziye Cd.", BlenderCharacterVariant.CasualFemale,
                    mats.PedShirtMustard, mats.CharPants, mats.CharSkin, mats.CharHairBrown, 1.68f,
                    new[] { new Vector3(-42f, 0.06f, -119.8f), new Vector3(26f, 0.06f, -119.8f), new Vector3(-16f, 0.06f, -119.8f) },
                    new[]
                    {
                        "İTO Şehit Er Dursun Sıvaz İlkokulu zil çalınca bütün Aziziye Caddesi çocuk sesleriyle şenlenir!",
                        "Ayşe Abla'nın okul önü dondurma tezgâhı herkesin gözdesi."
                    }
                ),
                (
                    "Mustafa Amca", "Zübeyde Hanım Cd. Sakini", BlenderCharacterVariant.CasualMale,
                    mats.CharShirt, mats.CharPantsKhaki, mats.CharSkinMedium, mats.CharHair, 1.74f,
                    new[] { new Vector3(-40f, 0.06f, -151.6f), new Vector3(44f, 0.06f, -151.6f), new Vector3(8f, 0.06f, -151.6f) },
                    new[]
                    {
                        "Zübeyde Hanım Caddesi'nin mavi balkonlu evleri ve çam ağaçları mahallenin en huzurlu köşesi.",
                        "Ev boyama iş panosu tam karşımızda, istersen buradaki evleri de boyayabilirsin!"
                    }
                )
            };

            for (int i = 0; i < aveCitizens.Length; i++)
            {
                var c = aveCitizens[i];
                GameObject pedGo = new GameObject($"Hamidiye_AveCitizen_{i + 1:00}_{c.name.Replace(" ", "")}");
                pedGo.transform.SetParent(lifeRoot.transform, false);
                pedGo.transform.position = c.route[0];

                CapsuleCollider col = pedGo.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 0.9f, 0f);
                col.radius = 0.34f;
                col.height = 1.76f;

                ProceduralHumanoid hum = BuildBlenderHumanoid(
                    pedGo.transform, mats, c.variant, c.shirt, c.pants, c.skin, c.hair, c.height);

                TownPedestrian tp = pedGo.AddComponent<TownPedestrian>();
                tp.ConfigureSchedule(c.route, seats.ToArray(), hum, 1.22f + (i % 3) * 0.12f, 0);
                tp.ConfigureCitizen(c.name, c.role, c.lines, brave: false);

                InteractableTrigger trig = pedGo.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.TownCitizen, $"citizen_ave_{i + 1:00}", "ui.btn.chat", 2.9f);
            }

            // 5 Pettable Street Cats across the 8 Hamidiye Avenues!
            (string name, Vector3 pos, Color fur, float radius)[] aveCats =
            {
                ("Paşa",   new Vector3(  -6.5f, 0.17f,  142.0f), new Color(0.88f, 0.54f, 0.22f, 1f), 3.0f), // Fatih Saat Kulesi
                ("Zeytin", new Vector3(  -8.0f, 0.06f,  -71.5f), new Color(0.16f, 0.17f, 0.20f, 1f), 2.8f), // Petrol Yolu Cd.
                ("Boncuk", new Vector3(-153.4f, 0.06f,  -14.0f), new Color(0.94f, 0.92f, 0.88f, 1f), 2.6f), // 53 Eyyubi Büfe
                ("Tarçın", new Vector3( -12.0f, 0.06f, -119.6f), new Color(0.76f, 0.44f, 0.20f, 1f), 2.8f), // Aziziye İTO İlkokulu
                ("Loki",   new Vector3(  18.0f, 0.06f, -151.6f), new Color(0.55f, 0.56f, 0.60f, 1f), 2.8f)  // Zübeyde Hanım Cd.
            };
            for (int i = 0; i < aveCats.Length; i++)
            {
                var c = aveCats[i];
                GameObject catGo = new GameObject($"IstanbulCat_{c.name}");
                catGo.transform.SetParent(lifeRoot.transform, false);
                catGo.transform.SetPositionAndRotation(c.pos, Quaternion.Euler(0f, i * 70f, 0f));

                InstantiateBlenderModel("Istanbul_StreetCat.fbx", $"{c.name}_Mesh", catGo.transform, mats);

                StreetCatController ctrl = catGo.AddComponent<StreetCatController>();
                ctrl.Configure(c.name, c.fur, c.radius);

                InteractableTrigger trig = catGo.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.StreetCat, $"street_cat_{c.name.ToLowerInvariant()}", "ui.btn.pet_cat", 2.5f);
            }
        }
    }
}
