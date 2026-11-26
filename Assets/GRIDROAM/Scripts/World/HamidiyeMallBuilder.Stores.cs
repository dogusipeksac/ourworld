using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public sealed partial class HamidiyeMallBuilder
    {
        // =====================================================================
        // FLOOR 0 — GROUND ATRIUM + 5M ATLAS MARKET + STARBEAN + KAHVE DİYARI + GRATIS + OPTİK + ECZANE
        // =====================================================================
        private void BuildFloor0_GroundAtriumAndStores(Transform interior)
        {
            Transform f0 = new GameObject("Floor0_GroundAtriumAndStores").transform;
            f0.SetParent(interior, false);

            // Central Illuminated Marble Fountain
            Vector3 fPos = new Vector3(0f, 0f, -1.2f);
            Cylinder("FountainBasin", f0, fPos + new Vector3(0f, 0.32f, 0f), new Vector3(4.4f, 0.32f, 4.4f), _facadeStone, col: true, shadows: false);
            Cylinder("FountainWater", f0, fPos + new Vector3(0f, 0.56f, 0f), new Vector3(3.9f, 0.08f, 3.9f), _waterMat, col: false, shadows: false);
            GameObject spinner = new GameObject("FountainSpinner");
            spinner.transform.SetParent(f0, false);
            spinner.transform.localPosition = fPos + new Vector3(0f, 0.65f, 0f);
            _fountainSpinner = spinner.transform;
            Cylinder("Spire",     spinner.transform, new Vector3(0f, 0.75f, 0f), new Vector3(0.45f, 0.75f, 0.45f), _goldAccent, col: false, shadows: false);
            Box("WaterArc1",      spinner.transform, new Vector3(0f, 1.35f, 0f), new Vector3(2.1f, 0.14f, 0.14f), _neonTeal, col: false, shadows: false);
            Box("WaterArc2",      spinner.transform, new Vector3(0f, 1.35f, 0f), new Vector3(0.14f, 0.14f, 2.1f), _neonTeal, col: false, shadows: false);

            // South-West Info Desk (clear of South Main Entrance x:-3.8..+3.8)
            GameObject info = Box("InfoDesk", f0, new Vector3(-7.0f, 0.55f, -13.0f), new Vector3(3.6f, 1.1f, 1.35f), _darkFrame, col: true);
            Box("InfoTop", info.transform, new Vector3(0f, 0.58f, 0f), new Vector3(3.8f, 0.08f, 1.55f), _goldAccent, col: false, shadows: false);
            CreateSignText("ATLAS DANIŞMA", f0, new Vector3(-7.0f, 2.35f, -13.0f), Quaternion.identity, 0.068f, Color.white);
            InteractableTrigger infoTrig = info.AddComponent<InteractableTrigger>();
            infoTrig.Configure(InteractableKind.InspectObject, "mall_info_desk", "ui.btn.interact", 3.0f);
            infoTrig.SetCustomPrompt("AVM Kat Rehberi & Danışma", "Atlas Mall Floor Guide & Info");
            SpawnMallNpc(f0, new Vector3(-7.0f, 0.05f, -14.1f), 0f, "Danışma · Selin", "Atlas AVM Danışma", "Atlas Mall Info",
                new Color(0.12f, 0.56f, 0.62f),
                new[] { "Hamidiye Atlas AVM'ye hoş geldin! 0'da 5M Market ve StarBean, 1'de Waikiki ve TeknoCell, 2'de Yemek Katı, 3'te CineAtlas Sinema var!" });

            // South-East Working ATM + Mado & Waffle Stop Kiosk
            GameObject atm = Box("Mall_ATM", f0, new Vector3(9.0f, 1.05f, -14.8f), new Vector3(1.1f, 2.1f, 0.75f), _tealAccent, col: true);
            Box("ATM_Screen", atm.transform, new Vector3(0f, 0.25f, 0.40f), new Vector3(0.55f, 0.52f, 0.06f), _neonTeal, col: false, shadows: false);
            CreateSignText("ATLAS ATM", f0, new Vector3(9.0f, 2.48f, -14.4f), Quaternion.Euler(0f, 180f, 0f), 0.062f, new Color(0.75f, 1f, 0.96f));
            atm.AddComponent<InteractableTrigger>().Configure(InteractableKind.AtmTerminal, "mall_atm_01", "ui.btn.use_atm", 2.8f);

            GameObject waffle = Box("MadoWaffleKiosk", f0, new Vector3(6.2f, 0.54f, -12.6f), new Vector3(2.8f, 1.08f, 1.25f), _brandRose, col: true);
            Box("WaffleTop", waffle.transform, new Vector3(0f, 0.56f, 0f), new Vector3(3.0f, 0.08f, 1.45f), _marbleFloor, col: false, shadows: false);
            CreateSignText("MADO & WAFFLE", f0, new Vector3(6.2f, 2.35f, -12.6f), Quaternion.Euler(0f, 180f, 0f), 0.060f, Color.white);
            SpawnMallPhysicalItem(f0, "Maraş Dondurma", "Maraş Ice Cream", 24, ShopItemEffect.Energy, 30f, ConsumeAnimKind.EatIceCream, ShopPropVisual.IceCreamCone,
                new Vector3(5.52f, 1.19f, -12.35f), requiresCheckout: false, storeName: "Mado & Waffle");
            SpawnMallPhysicalItem(f0, "Çilekli Waffle", "Strawberry Waffle", 38, ShopItemEffect.Energy, 46f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3(6.88f, 1.19f, -12.35f), requiresCheckout: false, storeName: "Mado & Waffle", repXp: 6);

            // 1. WEST-NORTH: 5M ATLAS MARKET (Walk-in Physical Hypermarket with EAS Door Gate!)
            Transform market = BuildWalkInStore(
                f0, "Store_5M_AtlasMarket", new Vector3(-13.4f, 0f, 6.1f), 90f, 11.0f, 6.4f,
                _brandOrange, _marbleFloor, "5M ATLAS MARKET", "mall_shop_5m_market",
                null, null,
                "Kasiyer Mert", new Color(0.92f, 0.46f, 0.12f),
                new[] { "5M Atlas Market'te reyonlardan ürünlerini eline alıp kasaya getir komşu!", "Dikkat et, ödemeden kapıdan çıkarsan alarm çalar!" },
                isPhysicalCheckoutStore: true);
            // Grocery aisles + waist-high stocked display counters inside 5M Atlas Market
            for (int a = -1; a <= 1; a += 2)
            {
                Box($"GroceryAisle_{a}", market, new Vector3(a * 3.55f, 0.95f, 0.2f), new Vector3(0.95f, 1.9f, 3.2f), _storeWallLight, col: true, shadows: false);
                Box($"ProduceColor_{a}", market, new Vector3(a * 3.55f, 1.45f, 0.2f), new Vector3(1.06f, 0.35f, 2.9f), a < 0 ? _brandGreen : _brandOrange, col: false, shadows: false);
                Box($"MarketDisplay_{a}", market, new Vector3(a * 2.45f, 0.45f, 0.2f), new Vector3(0.78f, 0.90f, 3.0f), _woodWarm, col: true, shadows: false);
            }
            Box("CoolerWall", market, new Vector3(0f, 1.25f, -2.95f), new Vector3(8.8f, 2.5f, 0.4f), _glassCurtain, col: false, shadows: false);
            // Left produce & grocery aisle (requiresCheckout: true — packs into Backpack at checkout!)
            SpawnMallPhysicalItem(market, "Meyve Çantası", "Fruit Bag", 30, ShopItemEffect.Energy, 40f, ConsumeAnimKind.EatFood, ShopPropVisual.FreshFruit,
                new Vector3(-2.45f, 0.91f, 1.15f), requiresCheckout: true, storeName: "5M Atlas Market", repXp: 8);
            SpawnMallPhysicalItem(market, "Bostan Tohum Paketi", "Farm Seed Pack", 45, ShopItemEffect.SeedPack, 0f, ConsumeAnimKind.None, ShopPropVisual.FreshFruit,
                new Vector3(-2.45f, 0.91f, 0.20f), requiresCheckout: true, storeName: "5M Atlas Market", repXp: 10);
            SpawnMallPhysicalItem(market, "Piknik Kumanyası", "Family Picnic Box", 55, ShopItemEffect.Energy, 65f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                new Vector3(-2.45f, 0.91f, -0.75f), requiresCheckout: true, storeName: "5M Atlas Market", repXp: 12);
            // Right drinks & bakery aisle (requiresCheckout: true)
            SpawnMallPhysicalItem(market, "Soğuk Maden Suyu", "Sparkling Water", 10, ShopItemEffect.Energy, 16f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                new Vector3( 2.45f, 0.91f, 1.15f), requiresCheckout: true, storeName: "5M Atlas Market");
            SpawnMallPhysicalItem(market, "Enerji İçeceği", "Energy Drink", 26, ShopItemEffect.Energy, 48f, ConsumeAnimKind.DrinkWater, ShopPropVisual.EnergyDrink,
                new Vector3( 2.45f, 0.91f, 0.20f), requiresCheckout: true, storeName: "5M Atlas Market");
            SpawnMallPhysicalItem(market, "Taze Somun Ekmek", "Fresh Bread Loaf", 12, ShopItemEffect.Energy, 24f, ConsumeAnimKind.EatFood, ShopPropVisual.BreadLoaf,
                new Vector3( 2.45f, 0.91f, -0.75f), requiresCheckout: true, storeName: "5M Atlas Market");

            // 2. WEST-SOUTH: STARBEAN COFFEE (Walk-in Coffee House with direct 3D counter drinks & bakery!)
            Transform starbean = BuildWalkInStore(
                f0, "Store_StarBeanCoffee", new Vector3(-13.4f, 0f, -6.1f), 90f, 11.0f, 6.4f,
                _brandGreen, _woodWarm, "STARBEAN COFFEE", "mall_shop_starbean",
                null, null,
                "Barista Ece", new Color(0.06f, 0.42f, 0.26f),
                new[] { "Hoş geldin! Tezgâhtaki taze kahvelerden veya cheesecake'ten doğrudan seçebilirsin!", "Kahveni alıp içerideki koltuklarda oturabilirsin!" },
                disableCounterTrigger: true);
            Box("EspressoMachine", starbean, new Vector3(-1.15f, 1.38f, -1.92f), new Vector3(0.85f, 0.55f, 0.45f), _darkFrame, col: false, shadows: false);
            Box("PastryCase",      starbean, new Vector3( 1.55f, 1.32f, -1.92f), new Vector3(1.05f, 0.42f, 0.45f), _glassRailing, col: false, shadows: false);
            SpawnMallPhysicalItem(starbean, "Caramel Macchiato", "Caramel Macchiato", 28, ShopItemEffect.Energy, 34f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                new Vector3(-1.55f, 1.20f, -1.32f), requiresCheckout: false, storeName: "StarBean Coffee", repXp: 4);
            SpawnMallPhysicalItem(starbean, "Iced White Mocha", "Iced White Mocha", 26, ShopItemEffect.Energy, 30f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                new Vector3(-0.52f, 1.20f, -1.32f), requiresCheckout: false, storeName: "StarBean Coffee", repXp: 4);
            SpawnMallPhysicalItem(starbean, "Taze Filtre Kahve", "Fresh Filter Coffee", 20, ShopItemEffect.Energy, 24f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                new Vector3( 0.52f, 1.20f, -1.32f), requiresCheckout: false, storeName: "StarBean Coffee");
            SpawnMallPhysicalItem(starbean, "Cheesecake", "San Sebastian Cheesecake", 36, ShopItemEffect.Energy, 44f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                new Vector3( 1.55f, 1.20f, -1.32f), requiresCheckout: false, storeName: "StarBean Coffee", repXp: 6);
            BuildDiningSet(starbean, "StarBeanTable_L", new Vector3(-3.5f, 0.04f, 0.9f), _brandGreen);
            BuildDiningSet(starbean, "StarBeanTable_R", new Vector3( 3.5f, 0.04f, 0.9f), _brandGreen);

            // 3. EAST-NORTH: KAHVE DİYARI (Walk-in Turkish Coffee & Chocolate House!)
            Transform kahve = BuildWalkInStore(
                f0, "Store_KahveDiyari", new Vector3(13.4f, 0f, 8.0f), -90f, 9.0f, 6.4f,
                _brandBrown, _woodWarm, "KAHVE DİYARI", "mall_shop_kahve_diyari",
                null, null,
                "Barista Kemal", new Color(0.42f, 0.22f, 0.14f),
                new[] { "Bol köpüklü Türk kahvesi ve fıstıklı çikolata tezgâhta seni bekliyor!", "Buyur otur, kahven taze kavruldu!" },
                disableCounterTrigger: true);
            SpawnMallPhysicalItem(kahve, "Türk Kahvesi & Lokum", "Turkish Coffee & Lokum", 22, ShopItemEffect.Energy, 32f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                new Vector3(-1.42f, 1.20f, -1.32f), requiresCheckout: false, storeName: "Kahve Diyarı", repXp: 6);
            SpawnMallPhysicalItem(kahve, "Fıstıklı Çikolata", "Pistachio Chocolate", 30, ShopItemEffect.Energy, 36f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                new Vector3(-0.48f, 1.20f, -1.32f), requiresCheckout: false, storeName: "Kahve Diyarı", repXp: 8);
            SpawnMallPhysicalItem(kahve, "Sıcak Çikolata", "Rich Hot Chocolate", 25, ShopItemEffect.Energy, 30f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                new Vector3( 0.48f, 1.20f, -1.32f), requiresCheckout: false, storeName: "Kahve Diyarı");
            SpawnMallPhysicalItem(kahve, "Fıstıklı Dondurma", "Pistachio Ice Cream", 20, ShopItemEffect.Energy, 26f, ConsumeAnimKind.EatIceCream, ShopPropVisual.IceCreamCone,
                new Vector3( 1.42f, 1.20f, -1.32f), requiresCheckout: false, storeName: "Kahve Diyarı");
            BuildDiningSet(kahve, "KahveTable_L", new Vector3(-2.8f, 0.04f, 0.9f), _brandBrown);
            BuildDiningSet(kahve, "KahveTable_R", new Vector3( 2.8f, 0.04f, 0.9f), _brandBrown);

            // 4. EAST-SOUTH: GRATIS BEAUTY & KOZMETİK (Physical Perfume Vanity Tables + EAS Door Gate!)
            Transform gratis = BuildWalkInStore(
                f0, "Store_GratisBeauty", new Vector3(13.4f, 0f, -8.0f), -90f, 9.0f, 6.4f,
                _brandLilac, _marbleFloor, "GRATIS BEAUTY", "mall_shop_gratis",
                null, null,
                "Uzman Selin", new Color(0.60f, 0.22f, 0.64f),
                new[] { "Parfüm stantlarından beğendiğini eline alıp kasaya getir komşu!", "İstanbul Esintisi parfümü mahallede çok beğeniliyor!" },
                isPhysicalCheckoutStore: true);
            Box("PerfumeDisplay_L", gratis, new Vector3(-3.1f, 0.95f, 0.3f), new Vector3(0.65f, 1.9f, 2.4f), _brandLilac, col: true, shadows: false);
            Box("PerfumeDisplay_R", gratis, new Vector3( 3.1f, 0.95f, 0.3f), new Vector3(0.65f, 1.9f, 2.4f), _brandRose, col: true, shadows: false);
            Box("PerfumeVanity_L",  gratis, new Vector3(-2.12f, 0.46f, 0.3f), new Vector3(0.76f, 0.92f, 2.2f), _marbleFloor, col: true, shadows: false);
            Box("PerfumeVanity_R",  gratis, new Vector3( 2.12f, 0.46f, 0.3f), new Vector3(0.76f, 0.92f, 2.2f), _marbleFloor, col: true, shadows: false);
            SpawnMallPhysicalItem(gratis, "İstanbul Esintisi Parfüm", "Istanbul Breeze Perfume", 55, ShopItemEffect.ReputationPrestige, 15f, ConsumeAnimKind.None, ShopPropVisual.PerfumeBottle,
                new Vector3(-2.12f, 0.93f, 0.85f), requiresCheckout: true, storeName: "Gratis Beauty", repXp: 25, scale: 1.25f);
            SpawnMallPhysicalItem(gratis, "Ferahlatıcı Bakım Seti", "Fresh Skincare Kit", 38, ShopItemEffect.ReputationPrestige, 20f, ConsumeAnimKind.None, ShopPropVisual.PerfumeBottle,
                new Vector3(-2.12f, 0.93f, -0.25f), requiresCheckout: true, storeName: "Gratis Beauty", repXp: 14, scale: 1.18f);
            SpawnMallPhysicalItem(gratis, "Nane & Limon Kolonya", "Classic Lemon Cologne", 20, ShopItemEffect.ReputationPrestige, 12f, ConsumeAnimKind.None, ShopPropVisual.WaterBottle,
                new Vector3( 2.12f, 0.93f, 0.85f), requiresCheckout: true, storeName: "Gratis Beauty", repXp: 10);
            SpawnMallPhysicalItem(gratis, "Hediye Kozmetik Kutusu", "Deluxe Gift Box", 75, ShopItemEffect.ReputationPrestige, 0f, ConsumeAnimKind.None, ShopPropVisual.PerfumeBottle,
                new Vector3( 2.12f, 0.93f, -0.25f), requiresCheckout: true, storeName: "Gratis Beauty", repXp: 38, scale: 1.30f);

            // 5. NORTH-WEST: ATLAS OPTİK & SAAT (Live 3D Sunglasses Try-On Stand + Watch Showcase + EAS Gate!)
            Transform optik = BuildWalkInStore(
                f0, "Store_AtlasOptik", new Vector3(-5.8f, 0f, 13.0f), 180f, 8.2f, 5.2f,
                _brandBlue, _marbleFloor, "ATLAS OPTİK & SAAT", "mall_shop_optik",
                null, null,
                "Optisyen Burak", new Color(0.16f, 0.42f, 0.72f),
                new[] { "Sol vitrindeki polarize güneş gözlüğünü yüzünde dene, beğenirsen kasada ödeyebilirsin!", "Ödeme yapmadan kapıdan çıkarsan güvenlik sensörleri hemen öter!" },
                isPhysicalCheckoutStore: true);
            Box("OptikShowcase",   optik, new Vector3(-2.35f, 0.48f, 0.45f), new Vector3(1.55f, 0.96f, 0.84f), _darkFrame, col: true, shadows: false);
            Box("OptikVelvetTop",  optik, new Vector3(-2.35f, 0.97f, 0.45f), new Vector3(1.48f, 0.04f, 0.78f), _brandBlue, col: false, shadows: false);
            Box("WatchShowcase",   optik, new Vector3( 2.35f, 0.48f, 0.45f), new Vector3(1.55f, 0.96f, 0.84f), _darkFrame, col: true, shadows: false);
            Box("WatchVelvetTop",  optik, new Vector3( 2.35f, 0.97f, 0.45f), new Vector3(1.48f, 0.04f, 0.78f), _goldAccent, col: false, shadows: false);
            // Try-On Mirror on left interior wall
            Box("OptikMirrorFrame", optik, new Vector3(-3.94f, 1.42f, 0.45f), new Vector3(0.08f, 1.95f, 1.10f), _goldAccent, col: false, shadows: false);
            Box("OptikMirrorGlass", optik, new Vector3(-3.89f, 1.42f, 0.45f), new Vector3(0.03f, 1.80f, 0.96f), _glassCurtain, col: false, shadows: false);
            SpawnMallPhysicalItem(optik, "Polarize Güneş Gözlüğü", "Polarized Sunglasses", 60, ShopItemEffect.Sunglasses, 0f, ConsumeAnimKind.None, ShopPropVisual.Sunglasses,
                new Vector3(-2.35f, 0.99f, 0.45f), requiresCheckout: true, storeName: "Atlas Optik & Saat", repXp: 28, isTryOn: true, scale: 1.45f, radius: 2.35f);
            SpawnMallPhysicalItem(optik, "Klasik Çelik Saat", "Classic Steel Watch", 95, ShopItemEffect.ReputationPrestige, 0f, ConsumeAnimKind.None, ShopPropVisual.Wristwatch,
                new Vector3( 2.35f, 0.99f, 0.45f), requiresCheckout: true, storeName: "Atlas Optik & Saat", repXp: 45, scale: 1.38f, radius: 2.35f);

            // 6. NORTH-EAST: ECZANE ATLAS (Physical Vitamin Shelf + EAS Door Gate!)
            Transform eczane = BuildWalkInStore(
                f0, "Store_EczaneAtlas", new Vector3(5.8f, 0f, 13.0f), 180f, 8.2f, 5.2f,
                _coralRed, _marbleFloor, "ECZANE ATLAS", "mall_shop_eczane",
                null, null,
                "Eczacı Deniz", new Color(0.95f, 0.96f, 0.97f),
                new[] { "Vitamin stantından ihtiyacın olan takviyeyi seçip kasada okutabilirsin!", "Sağlıklı günler komşu!" },
                isPhysicalCheckoutStore: true);
            Box("MedShelf_L",    eczane, new Vector3(-3.25f, 1.1f, -0.4f), new Vector3(0.55f, 2.2f, 2.6f), _storeWallLight, col: true, shadows: false);
            Box("MedDisplay_L",  eczane, new Vector3(-2.22f, 0.46f, 0.35f), new Vector3(0.78f, 0.92f, 2.1f), _storeWallLight, col: true, shadows: false);
            SpawnMallPhysicalItem(eczane, "C Vitamini & Elektrolit", "Vitamin C & Electrolytes", 25, ShopItemEffect.Energy, 55f, ConsumeAnimKind.DrinkWater, ShopPropVisual.MedicineBox,
                new Vector3(-2.22f, 0.93f, 0.85f), requiresCheckout: true, storeName: "Eczane Atlas", scale: 1.25f);
            SpawnMallPhysicalItem(eczane, "Tam Enerji Multivitamin", "Full-Recovery Multivitamin", 42, ShopItemEffect.Energy, 100f, ConsumeAnimKind.DrinkWater, ShopPropVisual.MedicineBox,
                new Vector3(-2.22f, 0.93f, -0.15f), requiresCheckout: true, storeName: "Eczane Atlas", scale: 1.25f);

            // Corridor Benches
            BuildMallBench(f0, "Bench_F0_W", new Vector3(-7.6f, 0f, 0f), 90f);
            BuildMallBench(f0, "Bench_F0_E", new Vector3( 7.6f, 0f, 0f), -90f);
        }

        // =====================================================================
        // FLOOR 1 — ATLAS WAIKIKI, MAVI & KOTONICA, TEKNOCELL, KARACA HOME, D&M KİTAP
        // =====================================================================
        private void BuildFloor1_FashionTechAndHome(Transform interior)
        {
            float y = 1f * FloorH;
            Transform f1 = new GameObject("Floor1_FashionTechAndHome").transform;
            f1.SetParent(interior, false);

            // 7. WEST-NORTH: ATLAS WAIKIKI (Physical 8-Jacket Live 3D Try-On Boutique + EAS Door Gate!)
            Transform waikiki = BuildWalkInStore(
                f1, "Store_AtlasWaikiki", new Vector3(-13.4f, y, 6.1f), 90f, 11.0f, 6.4f,
                _brandBlue, _woodWarm, "ATLAS WAIKIKI · GİYİM", "mall_shop_waikiki",
                null, null,
                "Moda Danışmanı Zeynep", new Color(0.12f, 0.34f, 0.68f),
                new[] { "Atlas Waikiki'ye hoş geldin! Stantlardaki 8 farklı ceketi üzerinde anında deneyip beğenince kasada ödeyebilirsin!", "Ödeme yapmadan ceketle kapıdan çıkarsan alarm çalar!" },
                isPhysicalCheckoutStore: true);
            for (int r = -1; r <= 1; r += 2)
            {
                Box($"ClothRack_{r}", waikiki, new Vector3(r * 3.85f, 0.95f, 0.4f), new Vector3(0.45f, 1.55f, 3.6f), _darkFrame, col: true, shadows: false);
                Box($"Garments_{r}",  waikiki, new Vector3(r * 3.85f, 1.05f, 0.4f), new Vector3(0.62f, 0.95f, 3.1f), r < 0 ? _tealAccent : _brandBlue, col: false, shadows: false);
                Box($"JacketTable_{r}", waikiki, new Vector3(r * 2.35f, 0.45f, 0.4f), new Vector3(0.88f, 0.90f, 3.8f), _marbleFloor, col: true, shadows: false);
            }
            Box("WaikikiMirrorFrame", waikiki, new Vector3(-5.25f, 1.45f, 0.4f), new Vector3(0.08f, 2.05f, 1.25f), _goldAccent, col: false, shadows: false);
            Box("WaikikiMirrorGlass", waikiki, new Vector3(-5.20f, 1.45f, 0.4f), new Vector3(0.03f, 1.90f, 1.10f), _glassCurtain, col: false, shadows: false);
            string[] jacketTr = { "Klasik Lacivert Ceket", "Bordo Deri Ceket", "Zeytin Parka", "Antrasit Sokak Montu", "İstanbul Turkuazı Ceket", "Safran Hardal Ceket", "Krem Keten Ceket", "Mürdüm Kadife Ceket" };
            string[] jacketEn = { "Classic Navy Jacket", "Burgundy Leather", "Olive Parka", "Charcoal Street Jacket", "Istanbul Turquoise", "Saffron Ochre Jacket", "Cream Linen Jacket", "Plum Velvet Jacket" };
            float[] jZ = { 1.75f, 0.85f, -0.05f, -0.95f };
            for (int j = 0; j < 8; j++)
            {
                float jx = j < 4 ? -2.35f : 2.35f;
                float jz = jZ[j % 4];
                SpawnMallPhysicalItem(waikiki, jacketTr[j], jacketEn[j], 35, ShopItemEffect.JacketColor, j, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                    new Vector3(jx, 0.91f, jz), requiresCheckout: true, storeName: "Atlas Waikiki", jacketColorIdx: j, repXp: 10, isTryOn: true, scale: 1.12f, radius: 1.85f);
            }

            // 8. WEST-SOUTH: MAVI DENIM & KOTONICA (Live 3D Street Cap Try-On + Running Shoes + EAS Gate!)
            Transform mavi = BuildWalkInStore(
                f1, "Store_MaviKotonica", new Vector3(-13.4f, y, -6.1f), 90f, 11.0f, 6.4f,
                _darkFrame, _woodWarm, "MAVİ DENİM & KOTONİCA", "mall_shop_mavi_koton",
                null, null,
                "Stilist Emre", new Color(0.18f, 0.24f, 0.38f),
                new[] { "Soldaki masadan 3D şapkayı başında dene, sağdaki stanttan Pro Koşu Ayakkabısını al!", "Seçtiklerini kasada okutmayı unutma!" },
                isPhysicalCheckoutStore: true);
            Box("DenimTable_L", mavi, new Vector3(-2.75f, 0.48f, 0.55f), new Vector3(1.15f, 0.96f, 2.3f), _brandBlue, col: true, shadows: false);
            Box("ShoeWall_R",   mavi, new Vector3( 3.95f, 1.10f, 0.4f), new Vector3(0.65f, 2.2f, 2.6f), _tealAccent, col: true, shadows: false);
            Box("ShoeBench_R",  mavi, new Vector3( 2.65f, 0.46f, 0.45f), new Vector3(0.95f, 0.92f, 2.3f), _marbleFloor, col: true, shadows: false);
            SpawnMallPhysicalItem(mavi, "3D Spor Şapka", "3D Street Cap", 45, ShopItemEffect.StreetCap, 0f, ConsumeAnimKind.None, ShopPropVisual.StreetCap,
                new Vector3(-2.75f, 0.97f, 1.10f), requiresCheckout: true, storeName: "Mavi & Kotonica", repXp: 20, isTryOn: true, scale: 1.28f);
            SpawnMallPhysicalItem(mavi, "İstanbul Denim Kombin", "Istanbul Denim Outfit", 65, ShopItemEffect.ReputationPrestige, 0f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                new Vector3(-2.75f, 0.97f, 0.00f), requiresCheckout: true, storeName: "Mavi & Kotonica", jacketColorIdx: 0, repXp: 32);
            SpawnMallPhysicalItem(mavi, "Pro Koşu Ayakkabısı", "Pro Running Shoes", 120, ShopItemEffect.RunningShoes, 0f, ConsumeAnimKind.None, ShopPropVisual.RunningShoes,
                new Vector3( 2.65f, 0.93f, 1.00f), requiresCheckout: true, storeName: "Mavi & Kotonica", repXp: 30, scale: 1.25f);
            SpawnMallPhysicalItem(mavi, "Kışlık Kaşkol & Eldiven", "Winter Scarf & Gloves", 35, ShopItemEffect.ReputationPrestige, 10f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                new Vector3( 2.65f, 0.93f, -0.10f), requiresCheckout: true, storeName: "Mavi & Kotonica", jacketColorIdx: 1, repXp: 18);

            // 9. EAST-NORTH: TEKNOCELL & ELEKTRONİK (Physical Tech Demo Tables + Turbo ECU Kit + EAS Gate!)
            Transform tekno = BuildWalkInStore(
                f1, "Store_TeknoCell", new Vector3(13.4f, y, 6.1f), -90f, 11.0f, 6.4f,
                _brandYellow, _marbleFloor, "TEKNOCELL · ELEKTRONİK", "mall_shop_teknocell",
                null, null,
                "Teknoloji Uzmanı Can", new Color(0.94f, 0.74f, 0.12f),
                new[] { "Demo masalarından akıllı saat, oyun konsolu veya araç Turbo ECU Kitini eline alıp kasaya getir!", "Kapıda güvenlik sensörü var, ödemeyi unutma!" },
                isPhysicalCheckoutStore: true);
            for (int t = -1; t <= 1; t += 2)
            {
                Box($"TechDemoTable_{t}", tekno, new Vector3(t * 2.85f, 0.50f, 0.65f), new Vector3(1.35f, 1.00f, 2.2f), _darkFrame, col: true, shadows: false);
                Box($"TechDemoScr_{t}",   tekno, new Vector3(t * 2.85f, 1.03f, 0.65f), new Vector3(1.10f, 0.06f, 1.9f), _neonTeal, col: false, shadows: false);
            }
            SpawnMallPhysicalItem(tekno, "Araç Turbo ECU Kiti", "Vehicle Turbo ECU Kit", 180, ShopItemEffect.TurboKit, 0f, ConsumeAnimKind.None, ShopPropVisual.TurboKit,
                new Vector3(-2.85f, 1.07f, 1.18f), requiresCheckout: true, storeName: "TeknoCell", repXp: 35, scale: 1.20f);
            SpawnMallPhysicalItem(tekno, "Kablosuz Pro Kulaklık", "Wireless Pro Earbuds", 70, ShopItemEffect.ReputationPrestige, 15f, ConsumeAnimKind.None, ShopPropVisual.TechGadget,
                new Vector3(-2.85f, 1.07f, 0.12f), requiresCheckout: true, storeName: "TeknoCell", repXp: 28, scale: 1.15f);
            SpawnMallPhysicalItem(tekno, "Akıllı Spor Saat", "Smart Fitness Watch", 85, ShopItemEffect.ReputationPrestige, 20f, ConsumeAnimKind.None, ShopPropVisual.Wristwatch,
                new Vector3( 2.85f, 1.07f, 1.18f), requiresCheckout: true, storeName: "TeknoCell", repXp: 34, scale: 1.28f);
            SpawnMallPhysicalItem(tekno, "Taşınabilir Oyun Konsolu", "Handheld Gaming Console", 125, ShopItemEffect.ReputationPrestige, 25f, ConsumeAnimKind.None, ShopPropVisual.TechGadget,
                new Vector3( 2.85f, 1.07f, 0.12f), requiresCheckout: true, storeName: "TeknoCell", repXp: 48, scale: 1.25f);

            // 10. EAST-SOUTH: KARACA HOME & ENGLISH LIVING (Physical Porcelain & Home Tables + EAS Gate!)
            Transform karaca = BuildWalkInStore(
                f1, "Store_KaracaHome", new Vector3(13.4f, y, -6.1f), -90f, 11.0f, 6.4f,
                _brandRose, _marbleFloor, "KARACA HOME & ENGLISH LIVING", "mall_shop_karaca",
                null, null,
                "Sorumlu Derya", new Color(0.82f, 0.56f, 0.54f),
                new[] { "Masalardaki porselen çay ve kahve takımlarını yakından inceleyip kasada alabilirsin!", "Ev tekstili koleksiyonumuz yeni yenilendi!" },
                isPhysicalCheckoutStore: true);
            Box("PorcelainTable_L", karaca, new Vector3(-2.85f, 0.48f, 0.55f), new Vector3(1.25f, 0.96f, 2.3f), _storeWallLight, col: true, shadows: false);
            Box("PorcelainTable_R", karaca, new Vector3( 2.85f, 0.48f, 0.55f), new Vector3(1.25f, 0.96f, 2.3f), _brandRose, col: true, shadows: false);
            SpawnMallPhysicalItem(karaca, "İnce Belli Çay Takımı", "Turkish Tea Glass Set", 50, ShopItemEffect.ReputationPrestige, 0f, ConsumeAnimKind.None, ShopPropVisual.PorcelainSet,
                new Vector3(-2.85f, 0.97f, 1.10f), requiresCheckout: true, storeName: "Karaca Home", repXp: 24, scale: 1.22f);
            SpawnMallPhysicalItem(karaca, "Porselen Kahve Fincanı", "Porcelain Coffee Set", 65, ShopItemEffect.ReputationPrestige, 0f, ConsumeAnimKind.None, ShopPropVisual.PorcelainSet,
                new Vector3(-2.85f, 0.97f, 0.00f), requiresCheckout: true, storeName: "Karaca Home", repXp: 30, scale: 1.22f);
            SpawnMallPhysicalItem(karaca, "Yumuşak Ev Battaniyesi", "Cozy Woven Throw", 48, ShopItemEffect.ReputationPrestige, 20f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                new Vector3( 2.85f, 0.97f, 1.10f), requiresCheckout: true, storeName: "Karaca Home", jacketColorIdx: 5, repXp: 20);
            SpawnMallPhysicalItem(karaca, "Akıllı Kahve Makinesi", "Turkish Coffee Maker", 110, ShopItemEffect.ReputationPrestige, 25f, ConsumeAnimKind.None, ShopPropVisual.TurkishCoffee,
                new Vector3( 2.85f, 0.97f, 0.00f), requiresCheckout: true, storeName: "Karaca Home", repXp: 45, scale: 1.30f);

            // 11. SOUTH ANCHOR: D&M KİTAP, MÜZİK & KÜLTÜR (Physical Book & Vinyl Display Tables + EAS Gate!)
            Transform dm = BuildWalkInStore(
                f1, "Store_DM_Kitap", new Vector3(0f, y, -13.0f), 0f, 19.6f, 5.2f,
                _brandBlue, _woodWarm, "D&M · KİTAP, MÜZİK & PLAK", "mall_shop_dm_kitap",
                null, null,
                "Kitapçı Hakan", new Color(0.18f, 0.36f, 0.68f),
                new[] { "Kitap masalarından roman, plak veya satranç setini eline alıp kasada ödeyebilirsin!", "Okuma koltuğuna oturup biraz dinlenebilirsin." },
                isPhysicalCheckoutStore: true);
            for (int b = -1; b <= 1; b += 2)
            {
                Box($"BookcaseA_{b}",  dm, new Vector3(b * 6.5f, 1.15f, -1.65f), new Vector3(3.6f, 2.3f, 0.55f), _woodDark, col: true, shadows: false);
                Box($"BookStrip_{b}",  dm, new Vector3(b * 6.5f, 1.55f, -1.32f), new Vector3(3.3f, 0.25f, 0.10f), _goldAccent, col: false, shadows: false);
                Box($"BookTable_{b}",  dm, new Vector3(b * 5.2f, 0.46f,  0.45f), new Vector3(2.8f, 0.92f, 0.96f), _woodWarm, col: true, shadows: false);
            }
            SpawnMallPhysicalItem(dm, "İstanbul Mahalle Tarihi", "History of Istanbul", 35, ShopItemEffect.ReputationPrestige, 15f, ConsumeAnimKind.None, ShopPropVisual.BookStack,
                new Vector3(-5.85f, 0.93f, 0.45f), requiresCheckout: true, storeName: "D&M Kitap", repXp: 22, scale: 1.22f);
            SpawnMallPhysicalItem(dm, "Anadolu Rock Plağı", "Anatolian Rock Vinyl", 65, ShopItemEffect.ReputationPrestige, 20f, ConsumeAnimKind.None, ShopPropVisual.BookStack,
                new Vector3(-4.55f, 0.93f, 0.45f), requiresCheckout: true, storeName: "D&M Kitap", repXp: 35, scale: 1.22f);
            SpawnMallPhysicalItem(dm, "Satranç & Tavla Seti", "Chess & Backgammon Set", 55, ShopItemEffect.ReputationPrestige, 0f, ConsumeAnimKind.None, ShopPropVisual.BookStack,
                new Vector3( 4.55f, 0.93f, 0.45f), requiresCheckout: true, storeName: "D&M Kitap", repXp: 28, scale: 1.22f);
            SpawnMallPhysicalItem(dm, "Çizim & Defter Seti", "Sketchbook & Pen Set", 28, ShopItemEffect.ReputationPrestige, 12f, ConsumeAnimKind.None, ShopPropVisual.BookStack,
                new Vector3( 5.85f, 0.93f, 0.45f), requiresCheckout: true, storeName: "D&M Kitap", repXp: 16, scale: 1.18f);
            BuildMallBench(dm, "DM_ReadingBench", new Vector3(8.0f, 0.04f, 1.3f), 180f);
        }

        // =====================================================================
        // FLOOR 2 — DEV ATLASPARK FOOD COURT (6 RESTAURANTS + 10 DINING TABLES!)
        // =====================================================================
        private void BuildFloor2_AtlasparkFoodCourt(Transform interior)
        {
            float y = 2f * FloorH;
            Transform f2 = new GameObject("Floor2_AtlasparkFoodCourt").transform;
            f2.SetParent(interior, false);

            // 12. WEST-NORTH: KRAL BURGER
            Transform burger = BuildWalkInStore(
                f2, "Food_KralBurger", new Vector3(-13.4f, y, 6.1f), 90f, 11.0f, 6.4f,
                _coralRed, _marbleFloor, "KRAL BURGER", "mall_food_burger",
                null, null,
                "Kasiyer Berkay", new Color(0.84f, 0.20f, 0.16f),
                new[] { "Alevde ızgara Kral Double Menü tezgâhta sıcak sıcak seni bekliyor!" },
                counterAtBack: false, disableCounterTrigger: true);
            SpawnMallPhysicalItem(burger, "Kral Double Menü", "Double King Meal", 48, ShopItemEffect.Energy, 62f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3(-1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Kral Burger", repXp: 6);
            SpawnMallPhysicalItem(burger, "Çıtır Tavuk Menü", "Crispy Chicken Meal", 38, ShopItemEffect.Energy, 48f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3( 0.00f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Kral Burger");
            SpawnMallPhysicalItem(burger, "Çikolatalı Sundae", "Chocolate Sundae", 18, ShopItemEffect.Energy, 24f, ConsumeAnimKind.EatIceCream, ShopPropVisual.IceCreamCone,
                new Vector3( 1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Kral Burger");

            // 13. WEST-SOUTH: TAVUK DİYARI
            Transform tavuk = BuildWalkInStore(
                f2, "Food_TavukDiyari", new Vector3(-13.4f, y, -6.1f), 90f, 11.0f, 6.4f,
                _brandYellow, _woodWarm, "TAVUK DİYARI", "mall_food_tavuk",
                null, null,
                "Şef Kadir", new Color(0.92f, 0.68f, 0.14f),
                new[] { "Kekiklim Şef Tabağı ve limonata tezgâhta hazır!" },
                counterAtBack: false, disableCounterTrigger: true);
            SpawnMallPhysicalItem(tavuk, "Kekiklim Şef Tabağı", "Thyme Chef's Plate", 52, ShopItemEffect.Energy, 66f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3(-1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Tavuk Diyarı", repXp: 8);
            SpawnMallPhysicalItem(tavuk, "Köri Soslu Tavuk", "Curry Chicken Skillet", 48, ShopItemEffect.Energy, 60f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3( 0.00f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Tavuk Diyarı");
            SpawnMallPhysicalItem(tavuk, "Ev Yapımı Limonata", "Mint Lemonade", 16, ShopItemEffect.Energy, 22f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                new Vector3( 1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Tavuk Diyarı");

            // 14. EAST-NORTH: BEREKET DÖNER
            Transform doner = BuildWalkInStore(
                f2, "Food_BereketDoner", new Vector3(13.4f, y, 6.1f), -90f, 11.0f, 6.4f,
                _coralRed, _marbleFloor, "BEREKET DÖNER", "mall_food_doner",
                null, null,
                "Döner Ustası Yusuf", new Color(0.82f, 0.18f, 0.18f),
                new[] { "Tereyağlı İskender, yaprak dürüm ve yayık ayran tezgâhta hazır!" },
                counterAtBack: false, disableCounterTrigger: true);
            SpawnMallPhysicalItem(doner, "Tereyağlı İskender", "Butter İskender", 58, ShopItemEffect.Energy, 72f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3(-1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Bereket Döner", repXp: 10);
            SpawnMallPhysicalItem(doner, "Yaprak Et Dürüm", "Beef Döner Wrap", 42, ShopItemEffect.Energy, 52f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                new Vector3( 0.00f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Bereket Döner");
            SpawnMallPhysicalItem(doner, "Köpüklü Yayık Ayran", "Frothy Cold Ayran", 14, ShopItemEffect.Energy, 20f, ConsumeAnimKind.DrinkWater, ShopPropVisual.AyranCup,
                new Vector3( 1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Bereket Döner");

            // 15. EAST-SOUTH: PİZZA LAZZA
            Transform pizza = BuildWalkInStore(
                f2, "Food_PizzaLazza", new Vector3(13.4f, y, -6.1f), -90f, 11.0f, 6.4f,
                _brandGreen, _woodWarm, "PİZZA LAZZA", "mall_food_pizza",
                null, null,
                "Pizzacı İrem", new Color(0.14f, 0.54f, 0.28f),
                new[] { "Fırından yeni çıkan Karışık Sultan Pizza ve sıcak sufle tezgâhta!" },
                counterAtBack: false, disableCounterTrigger: true);
            SpawnMallPhysicalItem(pizza, "Karışık Sultan Pizza", "Supreme Turkish Pizza", 50, ShopItemEffect.Energy, 64f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3(-1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Pizza Lazza", repXp: 6);
            SpawnMallPhysicalItem(pizza, "Çikolatalı Sufle", "Chocolate Soufflé", 26, ShopItemEffect.Energy, 34f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                new Vector3( 0.00f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Pizza Lazza");
            SpawnMallPhysicalItem(pizza, "Buzlu İçecek", "Chilled Soft Drink", 12, ShopItemEffect.Energy, 16f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                new Vector3( 1.30f, 1.20f, 2.02f), requiresCheckout: false, storeName: "Pizza Lazza");

            // 16. NORTH-WEST: CAJUN CORNER & KOVADA PİLAV
            Transform cajun = BuildWalkInStore(
                f2, "Food_CajunPilav", new Vector3(-5.8f, y, 13.0f), 180f, 8.2f, 5.2f,
                _brandOrange, _marbleFloor, "CAJUN & KOVADA PİLAV", "mall_food_cajun",
                null, null,
                "Usta Sinan", new Color(0.92f, 0.46f, 0.14f),
                new[] { "Çıtır cajun tavuk ya da nohutlu tavuk pilav tezgâhtan seçebilirsin!" },
                counterAtBack: false, disableCounterTrigger: true);
            SpawnMallPhysicalItem(cajun, "Çıtır Cajun Sepeti", "Crispy Cajun Basket", 44, ShopItemEffect.Energy, 56f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3(-0.82f, 1.20f, 1.42f), requiresCheckout: false, storeName: "Cajun & Pilav");
            SpawnMallPhysicalItem(cajun, "Nohutlu Tavuk Pilav", "Chickpea Rice Bowl", 38, ShopItemEffect.Energy, 54f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3( 0.82f, 1.20f, 1.42f), requiresCheckout: false, storeName: "Cajun & Pilav");

            // 17. SOUTH ANCHOR: SÜTLÜCE KADAYIF & ALAÇATI MUHALLEBİ
            Transform sutluce = BuildWalkInStore(
                f2, "Food_SutluceAlacati", new Vector3(0f, y, -13.0f), 0f, 19.6f, 5.2f,
                _tealAccent, _marbleFloor, "SÜTLÜCE KADAYIF & ALAÇATI MUHALLEBİ", "mall_food_baklava",
                null, null,
                "Tatlıcı Fatma Hanım", new Color(0.12f, 0.60f, 0.58f),
                new[] { "Sıcak fıstıklı künefe, sakızlı muhallebi ve demli çay tezgâhta hazır!" },
                counterAtBack: false, disableCounterTrigger: true);
            SpawnMallPhysicalItem(sutluce, "Fıstıklı Künefe", "Hot Pistachio Künefe", 42, ShopItemEffect.Energy, 52f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                new Vector3(-1.30f, 1.20f, 1.42f), requiresCheckout: false, storeName: "Sütlüce & Alaçatı", repXp: 10);
            SpawnMallPhysicalItem(sutluce, "Sakızlı Muhallebi", "Mastic Milk Pudding", 28, ShopItemEffect.Energy, 36f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                new Vector3( 0.00f, 1.20f, 1.42f), requiresCheckout: false, storeName: "Sütlüce & Alaçatı");
            SpawnMallPhysicalItem(sutluce, "İnce Belli Çay", "Fresh Turkish Tea", 10, ShopItemEffect.Energy, 18f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishTea,
                new Vector3( 1.30f, 1.20f, 1.42f), requiresCheckout: false, storeName: "Sütlüce & Alaçatı");

            // Spacious Food Court Seating Gallery (8 sittable dining tables positioned neatly clear of walking lanes!)
            Vector3[] tables =
            {
                new Vector3(-7.0f, y,  3.0f),
                new Vector3(-7.0f, y, -3.0f),
                new Vector3( 7.0f, y,  3.0f),
                new Vector3( 7.0f, y, -3.0f),
                new Vector3( 3.8f, y, 11.8f),
                new Vector3( 7.2f, y, 11.8f),
                new Vector3( 3.8f, y, 14.2f),
                new Vector3( 7.2f, y, 14.2f)
            };
            for (int i = 0; i < tables.Length; i++)
            {
                BuildDiningSet(f2, $"FoodCourtTable_{i}", tables[i], i % 2 == 0 ? _tealAccent : _brandOrange);
            }
        }

        // =====================================================================
        // FLOOR 3 — CİNEATLAS SİNEMA SALONU (ENCLOSED THEATER) & JOYPARK ARCADE
        // =====================================================================
        private void BuildFloor3_CineAtlasAndJoyPark(Transform interior)
        {
            float y = 3f * FloorH;
            Transform f3 = new GameObject("Floor3_CineAtlasAndJoyPark").transform;
            f3.SetParent(interior, false);

            // 18. SOUTH ANCHOR — CİNEATLAS SİNEMA SALONU (Real Darkened Walk-In Cinema Auditorium!)
            Transform cinema = BuildWalkInStore(
                f3, "CineAtlas_TheaterHall", new Vector3(0f, y, -13.0f), 0f, 19.6f, 5.2f,
                _cinemaRed, _cinemaDarkWall, "CİNEATLAS · DOLBY ATMOS SİNEMA", null, null, null, null, Color.clear, null, darkInterior: true);

            // Giant Glowing Cinema Screen along South back wall (local z = -2.35)
            Box("CinemaScreenFrame", cinema, new Vector3(0f, 2.15f, -2.42f), new Vector3(13.2f, 3.35f, 0.16f), _darkFrame, col: false, shadows: false);
            Box("CinemaBigScreen",   cinema, new Vector3(0f, 2.15f, -2.30f), new Vector3(12.4f, 2.95f, 0.10f), _screenGlow, col: false, shadows: false);

            // 8 Red Plush Cinema Seats in 2 tiered rows facing South screen!
            for (int row = 0; row < 2; row++)
            {
                for (int col = -2; col <= 1; col++)
                {
                    Vector3 sp = new Vector3((col + 0.5f) * 2.4f, 0.04f, -0.35f + row * 1.45f);
                    BuildCinemaSeat(cinema, $"CineAtlasSeat_{row}_{col}", sp);
                }
            }
            // Store exact top-of-cushion world pos of hero seat (row=1, col=0) so watching a film sits Deniz cleanly IN the plush seat!
            _cinemaSeatWorldPos = cinema.TransformPoint(new Vector3(1.2f, 0.58f, 1.06f));

            // EAST-SOUTH: CİNEATLAS BİLET GİŞESİ & PATLAMIŞ MISIR BARI
            Transform boxOffice = BuildWalkInStore(
                f3, "CineAtlas_TicketLobby", new Vector3(13.4f, y, -6.1f), -90f, 11.0f, 6.4f,
                _cinemaRed, _cinemaDarkWall, "CİNEATLAS GİŞE & MISIR", "mall_cinema_watch",
                "CineAtlas Sinema — Film Seç & İzle (35 ₺)", "CineAtlas Cinema — Pick & Watch a Movie (35 ₺)",
                "Gişe Görevlisi Arda", new Color(0.62f, 0.10f, 0.14f),
                new[] { "CineAtlas'ta 3 film vizyonda: İstanbul Geceleri, Aydos'ta Hız Tutkusu ve Boğaz'ın Gizemi!", "Patlamış mısır bilete dahil!" }, darkInterior: true);
            Box("PopcornWarmer", boxOffice, new Vector3(2.2f, 1.35f, -1.6f), new Vector3(0.95f, 0.72f, 0.72f), _neonAmber, col: false, shadows: false);

            // WEST WING (North & South combined entertainment zone): JOYPARK EĞLENCE & OYUN MERKEZİ
            Transform joyNorth = BuildWalkInStore(
                f3, "JoyPark_ArcadeNorth", new Vector3(-13.4f, y, 6.1f), 90f, 11.0f, 6.4f,
                _brandLilac, _cinemaDarkWall, "JOYPARK · ARCADE & EĞLENCE", null, null, null, null, Color.clear, null, darkInterior: true);
            Transform joySouth = BuildWalkInStore(
                f3, "JoyPark_ArcadeSouth", new Vector3(-13.4f, y, -6.1f), 90f, 11.0f, 6.4f,
                _tealAccent, _cinemaDarkWall, "JOYPARK · OYUN SALONU", null, null, null, null, Color.clear, null, darkInterior: true);

            SpawnMallNpc(joyNorth, new Vector3(0f, 0.05f, -1.6f), 0f, "JoyPark Görevlisi Oğuz", "JoyPark Oyun Merkezi", "JoyPark Game Center",
                new Color(0.60f, 0.22f, 0.64f),
                new[] { "JoyPark'ta 6 farklı arcade makinesi var! 10 TL jetonla rekor kır, ödül bileti kazan!" });

            // 6 Playable Neon Arcade Cabinets (3 in JoyPark North, 3 in JoyPark South)
            string[] trGames = { "İstanbul Drift", "Boğaz Uzay Savaşı", "Retro Futbol", "Kule Zıplama", "Süper Basket", "Ödül Pençesi" };
            string[] enGames = { "Istanbul Drift", "Bosphorus Starfighter", "Retro Football", "Tower Hopper", "Super Hoops", "Claw Machine" };
            for (int i = 0; i < 6; i++)
            {
                Transform host = i < 3 ? joyNorth : joySouth;
                int localIdx = i % 3;
                float cx = -3.2f + localIdx * 3.2f;
                GameObject cab = Box($"JoyArcade_{i}", host, new Vector3(cx, 1.05f, -2.1f), new Vector3(1.15f, 2.1f, 1.05f), _darkFrame, col: true);
                Box($"Screen_{i}", cab.transform, new Vector3(0f, 0.25f, 0.54f), new Vector3(0.84f, 0.65f, 0.06f), i % 2 == 0 ? _neonTeal : _neonAmber, col: false, shadows: false);
                Box($"Stick_{i}",  cab.transform, new Vector3(0f, -0.18f, 0.62f), new Vector3(0.92f, 0.14f, 0.32f), _coralRed, col: false, shadows: false);

                InteractableTrigger trig = cab.AddComponent<InteractableTrigger>();
                trig.Configure(InteractableKind.InspectObject, $"mall_arcade_{i}", "ui.btn.interact", 2.6f);
                trig.SetCustomPrompt($"JoyPark · {trGames[i]} Oyna (10 ₺)", $"JoyPark · Play {enGames[i]} (10 ₺)");
            }

            // Neon Air-Hockey decor table inside JoyPark South
            Box("AirHockeyTable", joySouth, new Vector3(0f, 0.45f, 0.8f), new Vector3(2.8f, 0.88f, 1.35f), _darkFrame, col: true, shadows: false);
            Box("AirHockeyTop",   joySouth, new Vector3(0f, 0.91f, 0.8f), new Vector3(2.5f, 0.05f, 1.12f), _neonTeal, col: false, shadows: false);
        }

        // =====================================================================
        // FLOOR 4 — ENNJOY LOUNGE TERAS KAFE & PANORAMİK SEYİR DÜRBÜNÜ
        // =====================================================================
        private void BuildFloor4_EnnjoyRooftopTerrace(Transform interior)
        {
            float y = 4f * FloorH;
            Transform f4 = new GameObject("Floor4_EnnjoyRooftopTerrace").transform;
            f4.SetParent(interior, false);

            // Ennjoy Lounge Open-Air Cafe Bar (West Terrace)
            Transform lounge = new GameObject("Ennjoy_TerraceBar").transform;
            lounge.SetParent(f4, false);
            lounge.localPosition = new Vector3(-13.2f, y, 0f);
            lounge.localRotation = Quaternion.Euler(0f, 90f, 0f);

            GameObject bar = Box("LoungeCounter", lounge, new Vector3(0f, 0.56f, 0f), new Vector3(7.4f, 1.12f, 1.35f), _woodDark, col: true);
            Box("LoungeTop",   bar.transform, new Vector3(0f, 0.58f, 0f), new Vector3(7.6f, 0.08f, 1.55f), _marbleFloor, col: false, shadows: false);
            Box("LoungeHeader",lounge, new Vector3(0f, 3.25f, -0.55f), new Vector3(8.0f, 0.72f, 0.28f), _darkFrame, col: false, shadows: false);
            Box("LoungeGlow",  lounge, new Vector3(0f, 2.84f, -0.40f), new Vector3(7.8f, 0.09f, 0.09f), _neonAmber, col: false, shadows: false);
            CreateSignText("ENNJOY LOUNGE · TERAS KAFE", lounge, new Vector3(0f, 3.25f, -0.36f), Quaternion.Euler(0f, 180f, 0f), 0.068f, new Color(1f, 0.90f, 0.52f));

            SpawnMallPhysicalItem(lounge, "Teras Signature Latte", "Rooftop Signature Latte", 30, ShopItemEffect.Energy, 38f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                new Vector3(-2.35f, 1.26f, 0.28f), requiresCheckout: false, storeName: "Ennjoy Terrace", repXp: 8);
            SpawnMallPhysicalItem(lounge, "Aydos Serpme Kahvaltı", "Aydos Rooftop Brunch", 68, ShopItemEffect.Energy, 85f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                new Vector3(-0.78f, 1.26f, 0.28f), requiresCheckout: false, storeName: "Ennjoy Terrace", repXp: 18);
            SpawnMallPhysicalItem(lounge, "Çilekli Limonata", "Strawberry Lemonade", 24, ShopItemEffect.Energy, 30f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                new Vector3( 0.78f, 1.26f, 0.28f), requiresCheckout: false, storeName: "Ennjoy Terrace");
            SpawnMallPhysicalItem(lounge, "Teras Meyve & Waffle", "Terrace Fruit & Waffle", 44, ShopItemEffect.Energy, 52f, ConsumeAnimKind.EatFood, ShopPropVisual.MealTray,
                new Vector3( 2.35f, 1.26f, 0.28f), requiresCheckout: false, storeName: "Ennjoy Terrace", repXp: 10);

            SpawnMallNpc(f4, new Vector3(-14.6f, y + 0.05f, 0.2f), 90f, "Teras Barista Tarık", "Ennjoy Teras Kafe", "Ennjoy Rooftop Cafe",
                new Color(0.54f, 0.36f, 0.22f),
                new[] { "5. kat terastan gün batımında bütün Sultanbeyli ışıl ışıl görünür!", "Teras Latte ve Aydos Serpme Kahvaltı favorimiz!" });

            // Wooden Shade Pergola over East Terrace Lounge
            Vector3 pg = new Vector3(11.5f, y, 0f);
            foreach (float px in new[] { -3.2f, 3.2f })
            {
                foreach (float pz in new[] { -5.0f, 5.0f })
                {
                    Box($"PergPost_{px}_{pz}", f4, pg + new Vector3(px, 1.55f, pz), new Vector3(0.24f, 3.1f, 0.24f), _woodWarm, col: true, shadows: false);
                }
            }
            for (int slat = -5; slat <= 5; slat++)
            {
                Box($"PergSlat_{slat}", f4, pg + new Vector3(0f, 3.12f, slat * 0.96f), new Vector3(7.0f, 0.14f, 0.24f), _woodWarm, col: false, shadows: false);
            }

            // Rooftop Sittable Cafe Tables & Benches
            BuildDiningSet(f4, "TerraceTable_1", new Vector3( 11.5f, y, -2.5f), _goldAccent);
            BuildDiningSet(f4, "TerraceTable_2", new Vector3( 11.5f, y,  2.5f), _goldAccent);
            BuildDiningSet(f4, "TerraceTable_3", new Vector3( -9.5f, y, -3.0f), _tealAccent);
            BuildDiningSet(f4, "TerraceTable_4", new Vector3( -9.5f, y,  3.0f), _tealAccent);
            BuildMallBench(f4, "TerraceBench_S", new Vector3( -8.5f, y, -12.5f), 0f);
            BuildMallBench(f4, "TerraceBench_N", new Vector3(  8.5f, y,  12.5f), 180f);

            // Panoramic Rooftop Binoculars
            GameObject tele = Cylinder("RooftopBinoculars", f4, new Vector3(-12.5f, y + 0.72f, -14.2f), new Vector3(0.22f, 0.72f, 0.22f), _darkFrame, col: true, shadows: false);
            Box("BinocHead", tele.transform, new Vector3(0f, 0.82f, 0f), new Vector3(0.46f, 0.22f, 0.58f), _goldAccent, col: false, shadows: false);
            InteractableTrigger tTrig = tele.AddComponent<InteractableTrigger>();
            tTrig.Configure(InteractableKind.InspectObject, "mall_telescope", "ui.btn.interact", 2.8f);
            tTrig.SetCustomPrompt("Panoramik Seyir Dürbününe Bak", "Look Through Rooftop Binoculars");

            Vector3[] shrubs =
            {
                new Vector3(-15.2f, y, -14.2f),
                new Vector3( 15.2f, y, -14.2f),
                new Vector3(-15.2f, y,  14.2f),
                new Vector3( 15.2f, y,  14.2f)
            };
            for (int i = 0; i < shrubs.Length; i++)
            {
                Box($"RoofPlanter_{i}", f4, shrubs[i] + new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 1.6f), _facadeStone, col: true, shadows: false);
                Sphere($"RoofShrub_{i}", f4, shrubs[i] + new Vector3(0f, 1.05f, 0f), new Vector3(1.4f, 1.1f, 1.4f), _foliageMat, shadows: false);
            }
        }

        // =====================================================================
        // 12 STROLLING CORRIDOR SHOPPERS ACROSS ALL 5 FLOORS (AUTO-CULLED!)
        // =====================================================================
        private void SpawnStrollingMallShoppers(Transform interior)
        {
            Transform root = new GameObject("AVM_StrollingShoppers").transform;
            root.SetParent(interior, false);

            (int floor, string name, Color shirt, Vector3[] route, string[] lines)[] shoppers =
            {
                (0, "Merve",  new Color(0.76f, 0.24f, 0.34f), new[] { new Vector3(-8.2f, 0.06f, -8.2f), new Vector3(-8.2f, 0.06f, 8.2f), new Vector3(8.2f, 0.06f, 8.2f), new Vector3(8.2f, 0.06f, -8.2f) },
                    new[] { "StarBean'den kahvemi aldım, şimdi 5M Atlas Market'e uğrayacağım!", "Zemin kattaki süs havuzu çok şık olmuş!" }),
                (0, "Ahmet",  new Color(0.22f, 0.42f, 0.64f), new[] { new Vector3(8.0f, 0.06f, 7.8f), new Vector3(-8.0f, 0.06f, 7.8f), new Vector3(-8.0f, 0.06f, -7.8f) },
                    new[] { "Kahve Diyarı'nın Türk kahvesi ve lokumu harika!", "Akşam sinemaya da çıkacağım." }),
                (0, "Elif",   new Color(0.58f, 0.28f, 0.62f), new[] { new Vector3(8.4f, 0.06f, -9.0f), new Vector3(8.4f, 0.06f, 4.0f) },
                    new[] { "Gratis Beauty'de yeni parfümler çıkmış, harika kokuyor!" }),

                (1, "Ceren",  new Color(0.14f, 0.58f, 0.62f), new[] { new Vector3(-8.2f, 4.66f, 8.0f), new Vector3(8.2f, 4.66f, 8.0f), new Vector3(8.2f, 4.66f, -8.0f), new Vector3(-8.2f, 4.66f, -8.0f) },
                    new[] { "Atlas Waikiki'de yeni ceket renklerine bayıldım!", "Mavi & Kotonica'da koşu ayakkabısı da varmış." }),
                (1, "Okan",   new Color(0.86f, 0.56f, 0.16f), new[] { new Vector3(8.0f, 4.66f, -7.8f), new Vector3(8.0f, 4.66f, 7.8f), new Vector3(-8.0f, 4.66f, 7.8f) },
                    new[] { "TeknoCell'deki araç Turbo ECU kiti harika bir yükseltme!", "D&M'den nostalji plağı da aldım." }),
                (1, "Sude",   new Color(0.82f, 0.48f, 0.52f), new[] { new Vector3(-7.6f, 4.66f, -8.4f), new Vector3(7.6f, 4.66f, -8.4f) },
                    new[] { "Karaca Home'un porselen fincan takımları çok zarif!" }),

                (2, "Burak",  new Color(0.84f, 0.34f, 0.18f), new[] { new Vector3(-9.0f, 9.26f, -8.0f), new Vector3(-9.0f, 9.26f, 8.0f), new Vector3(9.0f, 9.26f, 8.0f) },
                    new[] { "Kral Burger mi yesem Bereket Döner mi karar veremiyorum!", "Yemek katı mis gibi kokuyor!" }),
                (2, "Gizem",  new Color(0.24f, 0.56f, 0.34f), new[] { new Vector3(9.0f, 9.26f, 8.0f), new Vector3(9.0f, 9.26f, -8.0f), new Vector3(-9.0f, 9.26f, -8.0f) },
                    new[] { "Tavuk Diyarı'ndan sonra Sütlüce'de sıcak künefe yemeden dönülmez!" }),

                (3, "Yusuf",  new Color(0.26f, 0.32f, 0.58f), new[] { new Vector3(-8.2f, 13.86f, 7.6f), new Vector3(-8.2f, 13.86f, -7.6f), new Vector3(8.2f, 13.86f, -7.6f) },
                    new[] { "JoyPark'ta İstanbul Drift makinesinde 8500 puan yaptım!", "Şimdi CineAtlas'ta aksiyon filmine giriyorum!" }),
                (3, "Damla",  new Color(0.72f, 0.18f, 0.26f), new[] { new Vector3(8.2f, 13.86f, -7.8f), new Vector3(8.2f, 13.86f, 7.8f) },
                    new[] { "CineAtlas salonunun ses sistemi ve koltukları süper rahat!" }),

                (4, "Ömer",   new Color(0.18f, 0.56f, 0.52f), new[] { new Vector3(-8.2f, 18.46f, -8.2f), new Vector3(8.2f, 18.46f, -8.2f), new Vector3(8.2f, 18.46f, 8.2f), new Vector3(-8.2f, 18.46f, 8.2f) },
                    new[] { "Ennjoy Teras Kafe'den bütün Sultanbeyli manzarası görünüyor!", "Dürbünle Aydos Tepeleri'ne baktın mı?" }),
                (4, "Nisa",   new Color(0.90f, 0.68f, 0.24f), new[] { new Vector3(8.0f, 18.46f, 8.0f), new Vector3(-8.0f, 18.46f, 8.0f), new Vector3(-8.0f, 18.46f, -8.0f) },
                    new[] { "Akşam serinliğinde terasta latte içmek paha biçilmez!" })
            };

            for (int i = 0; i < shoppers.Length; i++)
            {
                var sh = shoppers[i];
                ProceduralHumanoid hum = SpawnMallNpc(
                    root, sh.route[0], 0f, sh.name,
                    $"Atlas AVM · {sh.floor}. Kat Ziyaretçisi",
                    $"Atlas Mall · Floor {sh.floor} Shopper",
                    sh.shirt, sh.lines);
                if (hum == null || _handler == null) continue;

                _handler.Strollers.Add(new MallStrollingShopper
                {
                    DisplayName = sh.name,
                    Root = hum.transform,
                    Humanoid = hum,
                    LocalWaypoints = sh.route,
                    TargetIdx = 1 % sh.route.Length,
                    Speed = Random.Range(1.35f, 1.85f),
                    PauseTimer = Random.Range(0.2f, 2.0f),
                    IsFighter = (i % 2 == 0)
                });
            }
        }
    }
}
