using System.Collections;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Automatically populates every shop shelf, fridge, manav stand, ice-cream chest, tea counter,
    /// Hafsa Tesisat workbench, and street vendor stall across the entire Hamidiye map with real,
    /// tangible 3D purchasable products (with floating world-space price tags) on scene load —
    /// replacing modal UI shop menus with 100% physical in-world shopping!
    /// </summary>
    public class PhysicalShopWorldPopulator : MonoBehaviour
    {
        private bool _populated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (FindFirstObjectByType<PhysicalShopWorldPopulator>() != null) return;
            GameObject host = new GameObject("PhysicalShopWorldPopulator");
            host.AddComponent<PhysicalShopWorldPopulator>();
        }

        private void Start()
        {
            StartCoroutine(PopulateAfterSceneReady());
        }

        private IEnumerator PopulateAfterSceneReady()
        {
            // Wait 1 frame so all scene objects & PlayerModeController are initialized
            yield return null;
            if (_populated) yield break;
            _populated = true;

            PopulateAllShops();

            if (PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RefreshInteractablesCache();
            }
        }

        private void PopulateAllShops()
        {
            // 1. Selman Bakkal & Manav (No:58) — Full real-life market experience:
            //    Pick items off the shelves/coolers into your hand, then pay at Bakkal_KasaBandi!
            Transform bakkalFridge = FindTransformByName("Bakkal_DrinkFridge");
            if (bakkalFridge != null)
            {
                SpawnItem(bakkalFridge, "Soğuk Su", 6, ShopItemEffect.Energy, 18f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                    new Vector3(-0.28f, 1.06f, 0.44f), requiresCheckout: true);
                SpawnItem(bakkalFridge, "Yayık Ayran", 14, ShopItemEffect.Energy, 26f, ConsumeAnimKind.DrinkWater, ShopPropVisual.AyranCup,
                    new Vector3(0.0f, 1.06f, 0.44f), requiresCheckout: true);
                SpawnItem(bakkalFridge, "Enerji İçeceği", 50, ShopItemEffect.Energy, 100f, ConsumeAnimKind.DrinkWater, ShopPropVisual.EnergyDrink,
                    new Vector3(0.28f, 1.06f, 0.44f), requiresCheckout: true);
            }

            Transform bakkalDondurma = FindTransformByName("Bakkal_DondurmaDolabi");
            if (bakkalDondurma != null)
            {
                DisableOldShopCounterTrigger(bakkalDondurma);
                SpawnItem(bakkalDondurma, "Külah Dondurma", 15, ShopItemEffect.Energy, 25f, ConsumeAnimKind.EatIceCream, ShopPropVisual.IceCreamCone,
                    new Vector3(-0.34f, 0.96f, 0.05f), requiresCheckout: true);
                SpawnItem(bakkalDondurma, "Soğuk Gazoz", 18, ShopItemEffect.Energy, 32f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                    new Vector3(0.34f, 0.96f, 0.05f), requiresCheckout: true);
            }

            Transform bakkalManav = FindTransformByName("Bakkal_ManavReyonu");
            if (bakkalManav != null)
            {
                DisableOldShopCounterTrigger(bakkalManav);
                SpawnItem(bakkalManav, "Taze Portakal", 18, ShopItemEffect.Energy, 30f, ConsumeAnimKind.EatFood, ShopPropVisual.FreshFruit,
                    new Vector3(-0.62f, 0.92f, 0.16f), requiresCheckout: true);
                SpawnItem(bakkalManav, "Aydos Elması", 24, ShopItemEffect.Energy, 42f, ConsumeAnimKind.EatFood, ShopPropVisual.FreshFruit,
                    new Vector3(0.0f, 0.92f, 0.16f), requiresCheckout: true);
                SpawnItem(bakkalManav, "Vitamin Sepeti", 42, ShopItemEffect.Energy, 75f, ConsumeAnimKind.EatFood, ShopPropVisual.FreshFruit,
                    new Vector3(0.62f, 0.92f, 0.16f), requiresCheckout: true);
            }

            Transform bakkalSarkuteri = FindTransformByName("Bakkal_Sarkuteri");
            if (bakkalSarkuteri != null)
            {
                DisableOldShopCounterTrigger(bakkalSarkuteri);
                SpawnItem(bakkalSarkuteri, "Ezine Peynir & Zeytin", 24, ShopItemEffect.Energy, 38f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                    new Vector3(-0.46f, 1.18f, 0.14f), requiresCheckout: true);
                SpawnItem(bakkalSarkuteri, "Sucuklu Tost", 35, ShopItemEffect.Energy, 58f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                    new Vector3(0.46f, 1.18f, 0.14f), requiresCheckout: true);
            }

            Transform bakkalShelfR = FindTransformByName("Bakkal_ShelfBackR");
            if (bakkalShelfR != null)
            {
                SpawnItem(bakkalShelfR, "Taze Somun Ekmek", 12, ShopItemEffect.Energy, 24f, ConsumeAnimKind.EatFood, ShopPropVisual.BreadLoaf,
                    new Vector3(-0.42f, 1.06f, 0.28f), requiresCheckout: true);
                SpawnItem(bakkalShelfR, "Kaşarlı Tost", 25, ShopItemEffect.Energy, 40f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                    new Vector3(0.42f, 1.06f, 0.28f), requiresCheckout: true);
            }

            Transform bakkalShelfL = FindTransformByName("Bakkal_ShelfSideL");
            if (bakkalShelfL != null)
            {
                SpawnItem(bakkalShelfL, "Sıcak Simit", 12, ShopItemEffect.Energy, 22f, ConsumeAnimKind.EatFood, ShopPropVisual.Simit,
                    new Vector3(-0.42f, 1.06f, 0.28f), requiresCheckout: true);
                SpawnItem(bakkalShelfL, "Kahvaltı Paketi", 48, ShopItemEffect.Energy, 85f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                    new Vector3(0.42f, 1.06f, 0.28f), requiresCheckout: true);
            }

            // Label Bakkal_KasaBandi clearly as the physical checkout belt
            Transform bakkalKasa = FindTransformByName("Bakkal_KasaBandi");
            if (bakkalKasa != null)
            {
                InteractableTrigger kasaTrig = bakkalKasa.GetComponent<InteractableTrigger>();
                if (kasaTrig != null)
                {
                    kasaTrig.SetCustomPrompt("Kasa Bandı (Önce raftan ürün al)");
                }
            }

            // 2. Selman Çay Ocağı (No:95) — Direct over-the-counter physical drinks & toast!
            Transform cayCounter = FindTransformByName("CayOcagi_Counter");
            if (cayCounter != null)
            {
                SpawnItem(cayCounter, "İnce Belli Çay", 10, ShopItemEffect.Energy, 20f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishTea,
                    new Vector3(-0.62f, 1.02f, 0.10f), requiresCheckout: false);
                SpawnItem(cayCounter, "Türk Kahvesi", 20, ShopItemEffect.Energy, 35f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                    new Vector3(0.0f, 1.02f, 0.10f), requiresCheckout: false);
                SpawnItem(cayCounter, "Kaşarlı Tost", 32, ShopItemEffect.Energy, 55f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                    new Vector3(0.62f, 1.02f, 0.10f), requiresCheckout: false);
            }

            Transform cayFridge = FindTransformByName("CayOcagi_Fridge");
            if (cayFridge != null)
            {
                SpawnItem(cayFridge, "Soğuk Su", 6, ShopItemEffect.Energy, 18f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                    new Vector3(-0.20f, 1.06f, 0.44f), requiresCheckout: false);
                SpawnItem(cayFridge, "Yayık Ayran", 14, ShopItemEffect.Energy, 26f, ConsumeAnimKind.DrinkWater, ShopPropVisual.AyranCup,
                    new Vector3(0.20f, 1.06f, 0.44f), requiresCheckout: false);
            }

            // 3. Hafsa Tesisat (No:66 South) — Physical tools, fuel can, turbo kit & work gear!
            Transform hafsaBench = FindTransformByName("Hafsa_PlumberBench");
            if (hafsaBench != null)
            {
                SpawnItem(hafsaBench, "Acil Yakıt Bidonu", 55, ShopItemEffect.FuelCan, 0f, ConsumeAnimKind.None, ShopPropVisual.FuelCan,
                    new Vector3(-0.56f, 0.96f, 0.08f), requiresCheckout: false);
                SpawnItem(hafsaBench, "Turbo Motor Kiti", 320, ShopItemEffect.TurboKit, 0f, ConsumeAnimKind.None, ShopPropVisual.TurboKit,
                    new Vector3(0.04f, 0.96f, 0.08f), requiresCheckout: false);
                SpawnItem(hafsaBench, "Usta İkramı Çay", 12, ShopItemEffect.Energy, 22f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishTea,
                    new Vector3(0.60f, 0.96f, 0.08f), requiresCheckout: false);
            }

            Transform hafsaRack = FindTransformByName("Hafsa_ToolRack");
            if (hafsaRack != null)
            {
                SpawnItem(hafsaRack, "Yürüyüş Ayakkabısı", 165, ShopItemEffect.RunningShoes, 0f, ConsumeAnimKind.None, ShopPropVisual.RunningShoes,
                    new Vector3(-0.38f, 1.08f, 0.12f), requiresCheckout: false);
                SpawnItem(hafsaRack, "Zeytin İş Parkası", 200, ShopItemEffect.JacketColor, 2f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                    new Vector3(0.38f, 1.08f, 0.12f), requiresCheckout: false, jacketColorIdx: 2);
            }

            Transform hafsaOldTrig = FindTransformByName("HafsaTesisat_Interact");
            if (hafsaOldTrig != null)
            {
                DisableOldShopCounterTrigger(hafsaOldTrig);
            }

            // 4. All Street Vendor Stalls & Enterable Town Shops across the entire Hamidiye district!
            InteractableTrigger[] allTriggers = FindObjectsByType<InteractableTrigger>(FindObjectsInactive.Exclude);
            for (int i = 0; i < allTriggers.Length; i++)
            {
                InteractableTrigger trig = allTriggers[i];
                if (trig == null || trig.Kind != InteractableKind.ShopCounter) continue;
                if (trig.gameObject.name == "Bakkal_KasaBandi") continue;

                string id = (trig.InteractableId ?? string.Empty).ToLowerInvariant();
                Transform host = trig.transform.parent != null ? trig.transform.parent : trig.transform;

                // Street vendor stall ("VendorTrigger" parented to stall with counter top at y = 1.05)
                if (trig.gameObject.name == "VendorTrigger")
                {
                    PopulateVendorStall(host, id);
                    trig.enabled = false;
                }
                // Enterable shop ServiceCounter ("ShopCounter_Trigger" parented to ServiceCounter)
                else if (trig.gameObject.name == "ShopCounter_Trigger")
                {
                    PopulateServiceCounter(host, id);
                    trig.enabled = false;
                }
            }
        }

        private void PopulateVendorStall(Transform stall, string shopId)
        {
            if (stall == null) return;
            if (shopId.Contains("gear"))
            {
                SpawnItem(stall, "Koşu Ayakkabısı", 180, ShopItemEffect.RunningShoes, 0f, ConsumeAnimKind.None, ShopPropVisual.RunningShoes,
                    new Vector3(-0.84f, 1.06f, 0.12f), requiresCheckout: false);
                SpawnItem(stall, "Bordo Deri Ceket", 220, ShopItemEffect.JacketColor, 1f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                    new Vector3(-0.28f, 1.06f, 0.12f), requiresCheckout: false, jacketColorIdx: 1);
                SpawnItem(stall, "Zeytin Parka", 220, ShopItemEffect.JacketColor, 2f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                    new Vector3(0.28f, 1.06f, 0.12f), requiresCheckout: false, jacketColorIdx: 2);
                SpawnItem(stall, "Antrasit Mont", 220, ShopItemEffect.JacketColor, 3f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                    new Vector3(0.84f, 1.06f, 0.12f), requiresCheckout: false, jacketColorIdx: 3);
            }
            else if (shopId.Contains("auto"))
            {
                SpawnItem(stall, "Turbo Motor Kiti", 350, ShopItemEffect.TurboKit, 0f, ConsumeAnimKind.None, ShopPropVisual.TurboKit,
                    new Vector3(-0.82f, 1.06f, 0.12f), requiresCheckout: false);
                SpawnItem(stall, "Acil Yakıt Bidonu", 60, ShopItemEffect.FuelCan, 0f, ConsumeAnimKind.None, ShopPropVisual.FuelCan,
                    new Vector3(-0.26f, 1.06f, 0.12f), requiresCheckout: false);
                SpawnItem(stall, "Lacivert Ceket", 120, ShopItemEffect.JacketColor, 0f, ConsumeAnimKind.None, ShopPropVisual.JacketFolded,
                    new Vector3(0.30f, 1.06f, 0.12f), requiresCheckout: false, jacketColorIdx: 0);
                SpawnItem(stall, "Tamirci Kahvesi", 15, ShopItemEffect.Energy, 25f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                    new Vector3(0.82f, 1.06f, 0.12f), requiresCheckout: false);
            }
            else
            {
                // Simitçi / Çay / Sokak lezzetleri tezgahı
                SpawnItem(stall, "Sıcak Simit", 12, ShopItemEffect.Energy, 22f, ConsumeAnimKind.EatFood, ShopPropVisual.Simit,
                    new Vector3(-0.82f, 1.06f, 0.12f), requiresCheckout: false);
                SpawnItem(stall, "İnce Belli Çay", 10, ShopItemEffect.Energy, 20f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishTea,
                    new Vector3(-0.28f, 1.06f, 0.12f), requiresCheckout: false);
                SpawnItem(stall, "Kaşarlı Tost", 22, ShopItemEffect.Energy, 38f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                    new Vector3(0.28f, 1.06f, 0.12f), requiresCheckout: false);
                SpawnItem(stall, "Yayık Ayran", 14, ShopItemEffect.Energy, 26f, ConsumeAnimKind.DrinkWater, ShopPropVisual.AyranCup,
                    new Vector3(0.82f, 1.06f, 0.12f), requiresCheckout: false);
            }
        }

        private void PopulateServiceCounter(Transform counter, string shopId)
        {
            if (counter == null) return;
            if (shopId.Contains("coffee") || shopId.Contains("cay"))
            {
                SpawnItem(counter, "İnce Belli Çay", 10, ShopItemEffect.Energy, 20f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishTea,
                    new Vector3(0.22f, 1.06f, -0.35f), requiresCheckout: false);
                SpawnItem(counter, "Türk Kahvesi", 20, ShopItemEffect.Energy, 35f, ConsumeAnimKind.DrinkHotCup, ShopPropVisual.TurkishCoffee,
                    new Vector3(0.22f, 1.06f, 0.22f), requiresCheckout: false);
                SpawnItem(counter, "Kaşarlı Tost", 32, ShopItemEffect.Energy, 55f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                    new Vector3(0.22f, 1.06f, 0.80f), requiresCheckout: false);
                SpawnItem(counter, "Soğuk Su", 6, ShopItemEffect.Energy, 18f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                    new Vector3(0.22f, 1.06f, 1.35f), requiresCheckout: false);
            }
            else
            {
                SpawnItem(counter, "Soğuk Su", 6, ShopItemEffect.Energy, 18f, ConsumeAnimKind.DrinkWater, ShopPropVisual.WaterBottle,
                    new Vector3(0.22f, 1.06f, -0.35f), requiresCheckout: false);
                SpawnItem(counter, "Kaşarlı Tost", 25, ShopItemEffect.Energy, 40f, ConsumeAnimKind.EatFood, ShopPropVisual.ToastSandwich,
                    new Vector3(0.22f, 1.06f, 0.22f), requiresCheckout: false);
                SpawnItem(counter, "Sıcak Yemek", 40, ShopItemEffect.Energy, 65f, ConsumeAnimKind.EatFood, ShopPropVisual.CheeseOlives,
                    new Vector3(0.22f, 1.06f, 0.80f), requiresCheckout: false);
                SpawnItem(counter, "Enerji İçeceği", 55, ShopItemEffect.Energy, 100f, ConsumeAnimKind.DrinkWater, ShopPropVisual.EnergyDrink,
                    new Vector3(0.22f, 1.06f, 1.35f), requiresCheckout: false);
            }
        }

        private static void SpawnItem(
            Transform parent,
            string itemName,
            int price,
            ShopItemEffect effect,
            float param,
            ConsumeAnimKind consumeKind,
            ShopPropVisual visual,
            Vector3 localPos,
            bool requiresCheckout,
            int jacketColorIdx = 0)
        {
            if (parent == null) return;
            GameObject go = new GameObject($"PhysicalItem_{itemName}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;

            PhysicalShopItem item = go.AddComponent<PhysicalShopItem>();
            item.Configure(itemName, price, effect, param, consumeKind, visual, requiresCheckout, jacketColorIdx, interactionRadius: 2.0f, visualScale: 1.08f);
        }

        private static void DisableOldShopCounterTrigger(Transform t)
        {
            if (t == null) return;
            InteractableTrigger trig = t.GetComponent<InteractableTrigger>();
            if (trig != null && trig.Kind == InteractableKind.ShopCounter)
            {
                trig.enabled = false;
            }
        }

        private static Transform FindTransformByName(string exactName)
        {
            GameObject go = GameObject.Find(exactName);
            return go != null ? go.transform : null;
        }
    }
}
