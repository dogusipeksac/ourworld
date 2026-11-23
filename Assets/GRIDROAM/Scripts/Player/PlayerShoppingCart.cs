using System.Collections.Generic;
using OurWorld.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace OurWorld.Player
{
    public enum ShopItemEffect
    {
        Energy = 0,
        RunningShoes = 1,
        TurboKit = 2,
        JacketColor = 3,
        FuelCan = 4
    }

    public enum ShopPropVisual
    {
        WaterBottle = 0,
        AyranCup = 1,
        EnergyDrink = 2,
        TurkishTea = 3,
        TurkishCoffee = 4,
        IceCreamCone = 5,
        Simit = 6,
        ToastSandwich = 7,
        CheeseOlives = 8,
        FreshFruit = 9,
        BreadLoaf = 10,
        RunningShoes = 11,
        JacketFolded = 12,
        TurboKit = 13,
        FuelCan = 14
    }

    public struct ShopCartItem
    {
        public string Name;
        public int Price;
        public ShopItemEffect Effect;
        public float EffectParam;
        public ConsumeAnimKind ConsumeKind;
        public ShopPropVisual Visual;
        public int JacketColorIndex;
    }

    /// <summary>
    /// Shared procedural 3D product prop generator used on shop shelves/counters AND
    /// inside the player's hand while carrying items to the checkout counter.
    /// </summary>
    public static class ShopPropFactory
    {
        private static Shader _cachedShader;

        public static GameObject BuildProp(
            Transform parent,
            ShopPropVisual visual,
            Vector3 localPos,
            float uniformScale = 1f,
            int jacketColorIndex = 1)
        {
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            }

            GameObject root = new GameObject($"ShopProp_{visual}");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one * uniformScale;

            switch (visual)
            {
                case ShopPropVisual.WaterBottle:
                    Part("Body", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.085f, 0.105f, 0.085f), Mat(new Color(0.55f, 0.85f, 0.98f), 0.86f, 0.05f));
                    Part("Label", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.089f, 0.038f, 0.089f), Mat(new Color(0.10f, 0.45f, 0.88f), 0.45f, 0f));
                    Part("Neck", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.225f, 0f), new Vector3(0.036f, 0.022f, 0.036f), Mat(new Color(0.72f, 0.92f, 1.0f), 0.85f, 0f));
                    Part("Cap", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.042f, 0.015f, 0.042f), Mat(new Color(0.08f, 0.36f, 0.85f), 0.55f, 0f));
                    break;

                case ShopPropVisual.AyranCup:
                    Part("Cup", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.075f, 0f), new Vector3(0.088f, 0.075f, 0.088f), Mat(new Color(0.95f, 0.96f, 0.98f), 0.55f, 0f));
                    Part("Band", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.091f, 0.028f, 0.091f), Mat(new Color(0.16f, 0.64f, 0.36f), 0.40f, 0f));
                    Part("FoilLid", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.152f, 0f), new Vector3(0.095f, 0.008f, 0.095f), Mat(new Color(0.84f, 0.87f, 0.90f), 0.78f, 0.65f));
                    break;

                case ShopPropVisual.EnergyDrink:
                    Part("Can", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.072f, 0.10f, 0.072f), Mat(new Color(0.10f, 0.14f, 0.22f), 0.72f, 0.55f));
                    Part("Stripe", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.075f, 0.032f, 0.075f), Mat(new Color(0.24f, 0.92f, 0.52f), 0.65f, 0.25f));
                    Part("TopRim", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.202f, 0f), new Vector3(0.068f, 0.008f, 0.068f), Mat(new Color(0.82f, 0.85f, 0.88f), 0.85f, 0.80f));
                    break;

                case ShopPropVisual.TurkishTea:
                    Part("Saucer", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.008f, 0f), new Vector3(0.14f, 0.008f, 0.14f), Mat(new Color(0.95f, 0.94f, 0.90f), 0.72f, 0.05f));
                    Part("GlassLower", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.065f, 0f), new Vector3(0.070f, 0.055f, 0.070f), Mat(new Color(0.64f, 0.16f, 0.04f), 0.90f, 0.08f));
                    Part("GlassRim", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.125f, 0f), new Vector3(0.064f, 0.015f, 0.064f), Mat(new Color(0.88f, 0.92f, 0.95f), 0.92f, 0.10f));
                    break;

                case ShopPropVisual.TurkishCoffee:
                    Part("Saucer", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.008f, 0f), new Vector3(0.14f, 0.008f, 0.14f), Mat(new Color(0.94f, 0.90f, 0.84f), 0.68f, 0.05f));
                    Part("Cup", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.055f, 0f), new Vector3(0.082f, 0.048f, 0.082f), Mat(new Color(0.95f, 0.92f, 0.86f), 0.70f, 0.05f));
                    Part("Foam", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.102f, 0f), new Vector3(0.072f, 0.008f, 0.072f), Mat(new Color(0.32f, 0.18f, 0.10f), 0.45f, 0f));
                    break;

                case ShopPropVisual.IceCreamCone:
                    Part("Cone", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.085f, 0f), new Vector3(0.064f, 0.085f, 0.064f), Mat(new Color(0.84f, 0.62f, 0.30f), 0.22f, 0f));
                    Part("Scoop1", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.185f, 0f), new Vector3(0.105f, 0.098f, 0.105f), Mat(new Color(0.98f, 0.93f, 0.85f), 0.35f, 0f));
                    Part("Scoop2", PrimitiveType.Sphere, root.transform, new Vector3(0.015f, 0.235f, 0f), new Vector3(0.072f, 0.068f, 0.072f), Mat(new Color(0.94f, 0.56f, 0.64f), 0.38f, 0f));
                    break;

                case ShopPropVisual.Simit:
                    Material crust = Mat(new Color(0.76f, 0.45f, 0.16f), 0.24f, 0f);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 0.25f;
                        Part($"Ring{i}", PrimitiveType.Sphere, root.transform,
                            new Vector3(Mathf.Cos(a) * 0.072f, 0.032f, Mathf.Sin(a) * 0.072f),
                            new Vector3(0.056f, 0.044f, 0.056f), crust);
                    }
                    break;

                case ShopPropVisual.ToastSandwich:
                    Part("BreadB", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.035f, 0f), new Vector3(0.18f, 0.045f, 0.13f), Mat(new Color(0.82f, 0.58f, 0.26f), 0.22f, 0f));
                    Part("Cheese", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.062f, 0f), new Vector3(0.186f, 0.018f, 0.136f), Mat(new Color(0.96f, 0.80f, 0.24f), 0.40f, 0f));
                    Part("BreadT", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.090f, 0f), new Vector3(0.18f, 0.042f, 0.13f), Mat(new Color(0.78f, 0.52f, 0.22f), 0.20f, 0f));
                    break;

                case ShopPropVisual.CheeseOlives:
                    Part("Tray", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.22f, 0.024f, 0.16f), Mat(new Color(0.94f, 0.94f, 0.96f), 0.55f, 0f));
                    Part("Cheese", PrimitiveType.Cube, root.transform, new Vector3(-0.04f, 0.062f, 0f), new Vector3(0.10f, 0.075f, 0.11f), Mat(new Color(0.97f, 0.95f, 0.88f), 0.35f, 0f));
                    Part("Olive1", PrimitiveType.Sphere, root.transform, new Vector3(0.05f, 0.042f, 0.025f), new Vector3(0.04f, 0.036f, 0.04f), Mat(new Color(0.14f, 0.13f, 0.15f), 0.65f, 0f));
                    Part("Olive2", PrimitiveType.Sphere, root.transform, new Vector3(0.06f, 0.042f, -0.03f), new Vector3(0.04f, 0.036f, 0.04f), Mat(new Color(0.14f, 0.13f, 0.15f), 0.65f, 0f));
                    break;

                case ShopPropVisual.FreshFruit:
                    Part("Crate", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.025f, 0f), new Vector3(0.22f, 0.045f, 0.18f), Mat(new Color(0.62f, 0.44f, 0.24f), 0.18f, 0f));
                    Material orange = Mat(new Color(0.96f, 0.52f, 0.08f), 0.48f, 0f);
                    Material apple = Mat(new Color(0.84f, 0.16f, 0.14f), 0.56f, 0f);
                    Part("F1", PrimitiveType.Sphere, root.transform, new Vector3(-0.045f, 0.078f, -0.025f), Vector3.one * 0.078f, orange);
                    Part("F2", PrimitiveType.Sphere, root.transform, new Vector3(0.045f, 0.078f, -0.025f), Vector3.one * 0.078f, apple);
                    Part("F3", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.078f, 0.04f), Vector3.one * 0.080f, orange);
                    Part("F4", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.132f, 0f), Vector3.one * 0.074f, apple);
                    break;

                case ShopPropVisual.BreadLoaf:
                    Part("Loaf", PrimitiveType.Capsule, root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.11f, 0.14f, 0.11f), Mat(new Color(0.82f, 0.58f, 0.28f), 0.16f, 0f), Quaternion.Euler(0f, 0f, 90f));
                    break;

                case ShopPropVisual.RunningShoes:
                    Material shoeMain = Mat(new Color(0.16f, 0.68f, 0.88f), 0.45f, 0.05f);
                    Material sole = Mat(new Color(0.95f, 0.96f, 0.98f), 0.35f, 0f);
                    Part("SoleL", PrimitiveType.Cube, root.transform, new Vector3(-0.07f, 0.022f, 0f), new Vector3(0.095f, 0.035f, 0.24f), sole);
                    Part("UpperL", PrimitiveType.Cube, root.transform, new Vector3(-0.07f, 0.062f, -0.02f), new Vector3(0.088f, 0.065f, 0.18f), shoeMain);
                    Part("SoleR", PrimitiveType.Cube, root.transform, new Vector3(0.07f, 0.022f, 0.02f), new Vector3(0.095f, 0.035f, 0.24f), sole);
                    Part("UpperR", PrimitiveType.Cube, root.transform, new Vector3(0.07f, 0.062f, 0f), new Vector3(0.088f, 0.065f, 0.18f), shoeMain);
                    break;

                case ShopPropVisual.JacketFolded:
                    Color[] jacketPalette =
                    {
                        new Color(0.16f, 0.24f, 0.42f), // Lacivert
                        new Color(0.54f, 0.14f, 0.18f), // Bordo
                        new Color(0.28f, 0.38f, 0.22f), // Zeytin
                        new Color(0.18f, 0.20f, 0.24f)  // Antrasit
                    };
                    Color jc = jacketPalette[Mathf.Clamp(jacketColorIndex, 0, jacketPalette.Length - 1)];
                    Part("FoldedBody", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.055f, 0f), new Vector3(0.28f, 0.095f, 0.24f), Mat(jc, 0.28f, 0.04f));
                    Part("Collar", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.112f, -0.05f), new Vector3(0.16f, 0.035f, 0.09f), Mat(jc * 0.85f, 0.32f, 0.04f));
                    Part("Zipper", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.104f, 0.02f), new Vector3(0.018f, 0.010f, 0.18f), Mat(new Color(0.78f, 0.80f, 0.84f), 0.80f, 0.85f));
                    break;

                case ShopPropVisual.TurboKit:
                    Part("Housing", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.11f, 0f), new Vector3(0.18f, 0.085f, 0.18f), Mat(new Color(0.74f, 0.78f, 0.82f), 0.82f, 0.88f), Quaternion.Euler(90f, 0f, 0f));
                    Part("Intake", PrimitiveType.Cylinder, root.transform, new Vector3(0.09f, 0.13f, 0f), new Vector3(0.09f, 0.10f, 0.09f), Mat(new Color(0.84f, 0.18f, 0.14f), 0.65f, 0.40f), Quaternion.Euler(0f, 0f, 90f));
                    break;

                case ShopPropVisual.FuelCan:
                    Part("CanBody", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.28f, 0.14f), Mat(new Color(0.84f, 0.14f, 0.12f), 0.45f, 0.05f));
                    Part("Handle", PrimitiveType.Cube, root.transform, new Vector3(-0.02f, 0.305f, 0f), new Vector3(0.14f, 0.035f, 0.045f), Mat(new Color(0.76f, 0.12f, 0.10f), 0.42f, 0.05f));
                    Part("Nozzle", PrimitiveType.Cylinder, root.transform, new Vector3(0.08f, 0.32f, 0f), new Vector3(0.035f, 0.055f, 0.035f), Mat(new Color(0.95f, 0.82f, 0.14f), 0.50f, 0f), Quaternion.Euler(0f, 0f, -25f));
                    break;
            }

            return root;
        }

        private static void Part(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            if (rot.HasValue) go.transform.localRotation = rot.Value;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static Material Mat(Color col, float smoothness, float metallic)
        {
            Material m = new Material(_cachedShader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            return m;
        }
    }

    /// <summary>
    /// Tracks real physical items picked off supermarket shelves (e.g. Selman Bakkal) while the
    /// player carries them cleanly in hand (upright in right palm for drinks/cones/simit, or held
    /// level with both hands in front of chest for fruit crates/trays/bread) to the checkout register.
    /// Zero modal UI menus — everything happens physically in the 3D world!
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class PlayerShoppingCart : MonoBehaviour
    {
        private const int MaxCartItems = 3;
        private const float MaxLeaveDistance = 19f;

        private static PlayerShoppingCart _activeInstance;

        private readonly List<ShopCartItem> _items = new List<ShopCartItem>();
        private PlayerFootController _foot;
        private GameObject _heldProp;
        private ShopPropVisual _heldVisual;
        private Vector3 _shopOrigin;

        public static bool HasActiveItems => _activeInstance != null && _activeInstance._items.Count > 0;

        public static string ActiveSummary
        {
            get
            {
                if (_activeInstance == null || _activeInstance._items.Count == 0) return string.Empty;
                int total = _activeInstance.TotalPrice;
                if (_activeInstance._items.Count == 1)
                {
                    return $"{_activeInstance._items[0].Name} ({total} ₺)";
                }
                return $"{_activeInstance._items.Count} ürün ({total} ₺)";
            }
        }

        public int TotalPrice
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _items.Count; i++) sum += _items[i].Price;
                return sum;
            }
        }

        public static PlayerShoppingCart GetOrCreate(PlayerModeController mode)
        {
            if (mode == null || mode.FootController == null) return null;
            PlayerShoppingCart cart = mode.FootController.GetComponent<PlayerShoppingCart>();
            if (cart == null)
            {
                cart = mode.FootController.gameObject.AddComponent<PlayerShoppingCart>();
            }
            _activeInstance = cart;
            return cart;
        }

        private void Awake()
        {
            _foot = GetComponent<PlayerFootController>();
            _activeInstance = this;
        }

        private void OnDisable()
        {
            ClearCart(silent: true);
        }

        private void OnDestroy()
        {
            ClearCart(silent: true);
            if (_activeInstance == this) _activeInstance = null;
        }

        private void Update()
        {
            if (_items.Count == 0) return;

            PlayerModeController mode = PlayerModeController.Instance;
            if (mode == null || mode.CurrentMode != ControlMode.OnFoot)
            {
                ClearCart(silent: true);
                return;
            }

            // Keep arms visibly carrying the picked product while walking to Kasa Bandı
            if (_foot != null && _foot.Humanoid != null)
            {
                PlayerConsumeAnimator consume = _foot.GetComponent<PlayerConsumeAnimator>();
                PlayerActionAnimator action = _foot.GetComponent<PlayerActionAnimator>();
                bool busy = (consume != null && consume.IsPlaying) || (action != null && action.IsPlaying);
                if (!busy)
                {
                    if (UsesTwoHandCarry(_heldVisual))
                    {
                        Vector3 carryCenter = GetTwoHandCarryCenter();
                        Vector3 leftGrip = carryCenter - transform.right * 0.135f + Vector3.up * 0.045f;
                        Vector3 rightGrip = carryCenter + transform.right * 0.135f + Vector3.up * 0.045f;
                        _foot.Humanoid.SetConsumePose(0, 0f, 0f);
                        _foot.Humanoid.SetDualHandAction(true, leftGrip, rightGrip, 0.96f, 0.96f, 3f, 5f, 0f);
                    }
                    else
                    {
                        _foot.Humanoid.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
                        _foot.Humanoid.SetConsumePose(1, 0.88f, 0f);
                    }
                }
            }

            // If player walks far away from the market without paying, cleanly return items to shelf
            float distSq = (transform.position - _shopOrigin).sqrMagnitude;
            if (distSq > MaxLeaveDistance * MaxLeaveDistance)
            {
                ClearCart(silent: false, message: "Hüseyin Abi: Komşu elindekileri kasadan geçirmeden çıktın, rafa geri bıraktın!");
            }
        }

        private void LateUpdate()
        {
            if (_heldProp == null || _items.Count == 0 || _foot == null || _foot.Humanoid == null) return;

            if (UsesTwoHandCarry(_heldVisual))
            {
                // Level & upright squarely between both gripping hands in front of the chest
                _heldProp.transform.position = GetTwoHandCarryCenter();
                _heldProp.transform.rotation = transform.rotation;
            }
            else if (_foot.Humanoid.RightForearm != null)
            {
                // Upright cleanly on top of the right palm (never rotated upside-down along forearm bone!)
                Vector3 palmPos = _foot.Humanoid.RightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
                _heldProp.transform.position = palmPos - Vector3.up * 0.06f + transform.forward * 0.02f;
                _heldProp.transform.rotation = transform.rotation;
            }
        }

        private Vector3 GetTwoHandCarryCenter()
        {
            return transform.position + transform.forward * 0.36f + Vector3.up * 1.03f;
        }

        private static bool UsesTwoHandCarry(ShopPropVisual visual)
        {
            switch (visual)
            {
                case ShopPropVisual.FreshFruit:
                case ShopPropVisual.CheeseOlives:
                case ShopPropVisual.ToastSandwich:
                case ShopPropVisual.BreadLoaf:
                case ShopPropVisual.RunningShoes:
                case ShopPropVisual.JacketFolded:
                case ShopPropVisual.TurboKit:
                case ShopPropVisual.FuelCan:
                    return true;
                default:
                    return false;
            }
        }

        public bool TryAddItem(PlayerModeController mode, ShopCartItem item, Vector3 shelfWorldPos)
        {
            if (_items.Count >= MaxCartItems)
            {
                if (mode != null)
                {
                    mode.RaiseStatusMessage($"Elin dolu ({ActiveSummary}) — Önce Kasa Bandına (Hüseyin Abi) götürüp öde!");
                }
                return false;
            }

            if (_items.Count == 0)
            {
                _shopOrigin = shelfWorldPos;
            }
            _items.Add(item);
            RebuildHeldProp(item);

            if (mode != null)
            {
                mode.RaiseStatusMessage($"{item.Name} ({item.Price} ₺) elinde — Kasa Bandına (Hüseyin Abi) götürüp E ile öde! (Toplam: {TotalPrice} ₺)");
            }
            return true;
        }

        public void Checkout(PlayerModeController mode, Transform registerTransform)
        {
            if (_items.Count == 0 || mode == null || SaveManager.Instance == null || SaveManager.Instance.Data == null)
            {
                return;
            }

            int total = TotalPrice;
            if (SaveManager.Instance.Data.money < total)
            {
                mode.RaiseStatusMessage($"Yetersiz bakiye! ({total} ₺ gerekli · Bakiye: {SaveManager.Instance.Data.money} ₺) — Ürünler rafa bırakıldı.");
                ClearCart(silent: true);
                return;
            }

            List<ShopCartItem> snapshot = new List<ShopCartItem>(_items);
            ClearCart(silent: true);

            float pendingEnergy = 0f;
            ConsumeAnimKind pendingConsume = ConsumeAnimKind.None;
            string pendingLabel = snapshot[snapshot.Count - 1].Name;
            bool paidOk = false;

            PlayerActionAnimator.Play(mode, registerTransform, PlayerActionKind.AtmKeypad, onClimax: () =>
            {
                if (SaveManager.Instance == null || !SaveManager.Instance.TrySpend(total))
                {
                    mode.RaiseStatusMessage("Ödeme tamamlanamadı.");
                    return;
                }

                paidOk = true;
                GameFeelAudio.PlayCoinChime();
                Vector3 fxPos = registerTransform != null
                    ? registerTransform.position + Vector3.up * 1.15f
                    : mode.ActiveWorldPosition + Vector3.up * 1.25f;
                GameFeelAudio.SpawnBurst(fxPos, new Color(0.98f, 0.84f, 0.28f), count: 14, spread: 0.50f);

                for (int i = 0; i < snapshot.Count; i++)
                {
                    ShopCartItem it = snapshot[i];
                    if (it.Effect == ShopItemEffect.Energy)
                    {
                        pendingEnergy += it.EffectParam;
                        if (it.ConsumeKind != ConsumeAnimKind.None)
                        {
                            pendingConsume = it.ConsumeKind;
                            pendingLabel = it.Name;
                        }
                    }
                    else
                    {
                        ApplyNonEnergyEffect(mode, it);
                    }
                }

                if (pendingConsume == ConsumeAnimKind.None)
                {
                    if (pendingEnergy > 0f && PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(pendingEnergy);
                    }
                    mode.RaiseStatusMessage($"Kasada ödendi (-{total} ₺) · Hayırlı olsun komşu!");
                }
                else
                {
                    mode.RaiseStatusMessage($"Kasada ödendi (-{total} ₺) · {pendingLabel} afiyetle tüketiliyor!");
                }
            }, onFinished: () =>
            {
                if (!paidOk) return;
                if (pendingConsume != ConsumeAnimKind.None && mode.FootController != null && mode.CurrentMode == ControlMode.OnFoot)
                {
                    PlayerConsumeAnimator.PlayPurchase(
                        mode.FootController,
                        "checkout",
                        pendingConsume,
                        pendingLabel,
                        pendingEnergy,
                        explicitTarget: registerTransform);
                }
                else if (pendingConsume != ConsumeAnimKind.None && pendingEnergy > 0f && PlayerNeedsController.Instance != null)
                {
                    PlayerNeedsController.Instance.RestoreEnergy(pendingEnergy);
                }
            }, startStatus: $"Kasada ödeme yapılıyor ({total} ₺)...");
        }

        public static void ApplyNonEnergyEffect(PlayerModeController mode, ShopCartItem item)
        {
            if (SaveManager.Instance == null || SaveManager.Instance.Data == null) return;
            SaveData data = SaveManager.Instance.Data;

            switch (item.Effect)
            {
                case ShopItemEffect.RunningShoes:
                    data.hasRunningShoes = true;
                    SaveManager.Instance.Save();
                    break;
                case ShopItemEffect.TurboKit:
                    data.hasTurboKit = true;
                    SaveManager.Instance.Save();
                    break;
                case ShopItemEffect.JacketColor:
                    int idx = Mathf.Clamp(item.JacketColorIndex, 0, 3);
                    data.playerJacketColorIndex = idx;
                    SaveManager.Instance.Save();
                    if (mode != null && mode.FootController != null)
                    {
                        mode.FootController.ApplyJacketColor(idx);
                    }
                    break;
                case ShopItemEffect.FuelCan:
                    if (mode != null && mode.PlayerSedan != null)
                    {
                        mode.PlayerSedan.SetFuel(1f);
                    }
                    data.missionSedanFuel = 1f;
                    SaveManager.Instance.Save();
                    break;
            }
        }

        public void ClearCart(bool silent, string message = null)
        {
            _items.Clear();
            if (_heldProp != null)
            {
                Destroy(_heldProp);
                _heldProp = null;
            }
            if (_foot != null && _foot.Humanoid != null)
            {
                _foot.Humanoid.SetConsumePose(0, 0f, 0f);
                _foot.Humanoid.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
            }
            if (!silent && !string.IsNullOrEmpty(message) && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(message);
            }
        }

        private void RebuildHeldProp(ShopCartItem item)
        {
            if (_heldProp != null)
            {
                Destroy(_heldProp);
                _heldProp = null;
            }
            if (_foot == null || _foot.Humanoid == null) return;

            _heldVisual = item.Visual;
            // Keep unparented from forearm bone so LateUpdate keeps the prop 100% upright & level in hand(s)!
            _heldProp = ShopPropFactory.BuildProp(
                null,
                item.Visual,
                GetTwoHandCarryCenter(),
                uniformScale: UsesTwoHandCarry(item.Visual) ? 0.95f : 0.88f,
                jacketColorIndex: item.JacketColorIndex);
        }
    }
}
