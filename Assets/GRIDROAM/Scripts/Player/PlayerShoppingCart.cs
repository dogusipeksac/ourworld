using System.Collections.Generic;
using OurWorld.Core;
using OurWorld.World;
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
        FuelCan = 4,
        Sunglasses = 5,
        StreetCap = 6,
        ReputationPrestige = 7,
        SeedPack = 8
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
        FuelCan = 14,
        Sunglasses = 15,
        Wristwatch = 16,
        StreetCap = 17,
        PerfumeBottle = 18,
        TechGadget = 19,
        MedicineBox = 20,
        PorcelainSet = 21,
        BookStack = 22,
        MealTray = 23
    }

    public struct ShopCartItem
    {
        public string Name;
        public string NameTr;
        public string NameEn;
        public int Price;
        public ShopItemEffect Effect;
        public float EffectParam;
        public ConsumeAnimKind ConsumeKind;
        public ShopPropVisual Visual;
        public int JacketColorIndex;
        public int ReputationXp;
        public bool IsTryOnWearable;
        public string SourceStoreName;
    }

    /// <summary>
    /// Shared procedural 3D product prop generator used on shop shelves/counters AND
    /// inside the player's hand while carrying items to the checkout counter.
    /// </summary>
    public static class ShopPropFactory
    {
        private static Shader _cachedShader;
        private static readonly Dictionary<int, Material> MaterialCache = new Dictionary<int, Material>(32);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticCache()
        {
            _cachedShader = null;
            MaterialCache.Clear();
        }

        public static GameObject BuildProp(
            Transform parent,
            ShopPropVisual visual,
            Vector3 localPos,
            float uniformScale = 1f,
            int jacketColorIndex = 1)
        {
            if (_cachedShader == null)
            {
                _cachedShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
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
                    Color jc = PlayerFootController.GetJacketColor(jacketColorIndex);
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

                case ShopPropVisual.Sunglasses:
                    Material lensM = Mat(new Color(0.06f, 0.10f, 0.18f), 0.94f, 0.45f);
                    Material goldM = Mat(new Color(0.90f, 0.74f, 0.26f), 0.88f, 0.82f);
                    Part("StandBase", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.14f, 0.014f, 0.14f), Mat(new Color(0.14f, 0.16f, 0.20f), 0.65f, 0.4f));
                    Part("StandNeck", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.075f, 0f), new Vector3(0.022f, 0.055f, 0.022f), goldM);
                    Part("LensL", PrimitiveType.Cube, root.transform, new Vector3(-0.062f, 0.14f, 0.04f), new Vector3(0.095f, 0.054f, 0.018f), lensM);
                    Part("LensR", PrimitiveType.Cube, root.transform, new Vector3( 0.062f, 0.14f, 0.04f), new Vector3(0.095f, 0.054f, 0.018f), lensM);
                    Part("Bridge", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.152f, 0.042f), new Vector3(0.046f, 0.012f, 0.016f), goldM);
                    Part("TopBar", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.172f, 0.041f), new Vector3(0.22f, 0.011f, 0.016f), goldM);
                    Part("ArmL", PrimitiveType.Cube, root.transform, new Vector3(-0.112f, 0.145f, -0.045f), new Vector3(0.012f, 0.014f, 0.17f), goldM);
                    Part("ArmR", PrimitiveType.Cube, root.transform, new Vector3( 0.112f, 0.145f, -0.045f), new Vector3(0.012f, 0.014f, 0.17f), goldM);
                    break;

                case ShopPropVisual.Wristwatch:
                    Material velvet = Mat(new Color(0.10f, 0.13f, 0.22f), 0.25f, 0.05f);
                    Material steel = Mat(new Color(0.84f, 0.87f, 0.92f), 0.90f, 0.88f);
                    Material dial = Mat(new Color(0.08f, 0.22f, 0.42f), 0.85f, 0.35f);
                    Part("Cushion", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.045f, 0f), new Vector3(0.16f, 0.085f, 0.16f), velvet);
                    Part("Band", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.10f, 0f), new Vector3(0.125f, 0.036f, 0.125f), steel, Quaternion.Euler(0f, 0f, 90f));
                    Part("Case", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.142f, 0.02f), new Vector3(0.095f, 0.018f, 0.095f), steel, Quaternion.Euler(24f, 0f, 0f));
                    Part("Dial", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.150f, 0.024f), new Vector3(0.078f, 0.014f, 0.078f), dial, Quaternion.Euler(24f, 0f, 0f));
                    break;

                case ShopPropVisual.StreetCap:
                    Material capNavy = Mat(new Color(0.10f, 0.24f, 0.48f), 0.25f, 0.02f);
                    Material capGold = Mat(new Color(0.95f, 0.72f, 0.20f), 0.40f, 0.12f);
                    Part("CapCrown", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.08f, -0.01f), new Vector3(0.24f, 0.15f, 0.24f), capNavy);
                    Part("CapVisor", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.045f, 0.135f), new Vector3(0.19f, 0.015f, 0.115f), capNavy, Quaternion.Euler(8f, 0f, 0f));
                    Part("CapEmblem", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.105f, 0.105f), new Vector3(0.06f, 0.028f, 0.015f), capGold);
                    break;

                case ShopPropVisual.PerfumeBottle:
                    Material crystal = Mat(new Color(0.84f, 0.46f, 0.88f), 0.92f, 0.15f);
                    Material capG = Mat(new Color(0.94f, 0.78f, 0.26f), 0.88f, 0.82f);
                    Part("Flacon", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.085f, 0f), new Vector3(0.11f, 0.15f, 0.07f), crystal);
                    Part("Label", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.085f, 0.038f), new Vector3(0.075f, 0.065f, 0.008f), Mat(new Color(0.98f, 0.96f, 0.92f), 0.4f, 0f));
                    Part("Neck", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.175f, 0f), new Vector3(0.032f, 0.018f, 0.032f), capG);
                    Part("SprayCap", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.21f, 0f), new Vector3(0.048f, 0.025f, 0.048f), capG);
                    break;

                case ShopPropVisual.TechGadget:
                    Material slate = Mat(new Color(0.10f, 0.12f, 0.16f), 0.78f, 0.65f);
                    Material screen = Mat(new Color(0.14f, 0.82f, 0.86f), 0.95f, 0.10f);
                    Part("DeviceBody", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.055f, 0f), new Vector3(0.24f, 0.036f, 0.14f), slate, Quaternion.Euler(-18f, 0f, 0f));
                    Part("DeviceScreen", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.066f, 0.006f), new Vector3(0.18f, 0.024f, 0.105f), screen, Quaternion.Euler(-18f, 0f, 0f));
                    break;

                case ShopPropVisual.MedicineBox:
                    Material boxW = Mat(new Color(0.96f, 0.97f, 0.99f), 0.42f, 0f);
                    Material crossR = Mat(new Color(0.88f, 0.16f, 0.18f), 0.45f, 0f);
                    Part("Pack", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.065f, 0f), new Vector3(0.18f, 0.12f, 0.10f), boxW);
                    Part("CrossH", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.07f, 0.053f), new Vector3(0.07f, 0.020f, 0.008f), crossR);
                    Part("CrossV", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.07f, 0.053f), new Vector3(0.020f, 0.07f, 0.008f), crossR);
                    break;

                case ShopPropVisual.PorcelainSet:
                    Material porc = Mat(new Color(0.96f, 0.95f, 0.92f), 0.84f, 0.08f);
                    Material trimG = Mat(new Color(0.90f, 0.72f, 0.24f), 0.86f, 0.78f);
                    Part("Plate", PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.015f, 0f), new Vector3(0.24f, 0.014f, 0.24f), porc);
                    Part("Teapot", PrimitiveType.Sphere, root.transform, new Vector3(-0.04f, 0.095f, 0f), new Vector3(0.13f, 0.12f, 0.13f), porc);
                    Part("GoldBand", PrimitiveType.Cylinder, root.transform, new Vector3(-0.04f, 0.155f, 0f), new Vector3(0.07f, 0.015f, 0.07f), trimG);
                    Part("CupSmall", PrimitiveType.Cylinder, root.transform, new Vector3(0.07f, 0.055f, 0.03f), new Vector3(0.068f, 0.042f, 0.068f), porc);
                    break;

                case ShopPropVisual.BookStack:
                    Part("Book1", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.025f, 0f), new Vector3(0.22f, 0.042f, 0.16f), Mat(new Color(0.16f, 0.34f, 0.66f), 0.28f, 0.05f));
                    Part("Book2", PrimitiveType.Cube, root.transform, new Vector3(0.01f, 0.066f, 0f), new Vector3(0.20f, 0.038f, 0.15f), Mat(new Color(0.76f, 0.20f, 0.22f), 0.28f, 0.05f), Quaternion.Euler(0f, 8f, 0f));
                    Part("Book3", PrimitiveType.Cube, root.transform, new Vector3(-0.01f, 0.104f, 0f), new Vector3(0.18f, 0.035f, 0.14f), Mat(new Color(0.90f, 0.70f, 0.22f), 0.32f, 0.08f), Quaternion.Euler(0f, -6f, 0f));
                    break;

                case ShopPropVisual.MealTray:
                    Part("FoodTray", PrimitiveType.Cube, root.transform, new Vector3(0f, 0.014f, 0f), new Vector3(0.28f, 0.020f, 0.20f), Mat(new Color(0.84f, 0.18f, 0.16f), 0.45f, 0.05f));
                    Part("MainBun", PrimitiveType.Sphere, root.transform, new Vector3(-0.04f, 0.068f, 0f), new Vector3(0.13f, 0.088f, 0.13f), Mat(new Color(0.86f, 0.56f, 0.22f), 0.28f, 0f));
                    Part("Patty", PrimitiveType.Cylinder, root.transform, new Vector3(-0.04f, 0.056f, 0f), new Vector3(0.135f, 0.016f, 0.135f), Mat(new Color(0.42f, 0.20f, 0.10f), 0.22f, 0f));
                    Part("DrinkCup", PrimitiveType.Cylinder, root.transform, new Vector3(0.085f, 0.075f, -0.02f), new Vector3(0.065f, 0.065f, 0.065f), Mat(new Color(0.96f, 0.96f, 0.98f), 0.55f, 0f));
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
            Color32 c32 = col;
            int key = (c32.r << 24) ^ (c32.g << 16) ^ (c32.b << 8) ^ c32.a
                      ^ (Mathf.RoundToInt(smoothness * 100f) * 397)
                      ^ (Mathf.RoundToInt(metallic * 100f) * 7919);
            if (MaterialCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material m = new Material(_cachedShader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            MaterialCache[key] = m;
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
        private const int MaxCartItems = 4;
        private const float MaxLeaveDistance = 13.5f;
        private const float InspectHoldDuration = 0.46f;
        private const float InspectSettleDuration = 0.40f;

        private static PlayerShoppingCart _activeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetActiveCart() => _activeInstance = null;

        public static void ClearAndRevertOnTeleport()
        {
            if (_activeInstance != null && _activeInstance._items.Count > 0)
            {
                _activeInstance.ClearCart(silent: true, revertTryOn: true);
            }
        }

        private readonly List<ShopCartItem> _items = new List<ShopCartItem>();
        private PlayerFootController _foot;
        private PlayerConsumeAnimator _consumeAnim;
        private PlayerActionAnimator _actionAnim;
        private GameObject _heldProp;
        private ShopPropVisual _heldVisual;
        private Vector3 _shopOrigin;
        private string _activeStoreName = string.Empty;
        private bool _hasUnpaidTryOn;
        private bool _checkingOut;
        private bool _inspectActive;
        private float _inspectTimer;
        private float _inspectYaw;
        private float _carryBlend;
        private bool _wasBusy;

        public static bool HasActiveItems => _activeInstance != null && _activeInstance._items.Count > 0;
        public static Vector3 ActiveShopOrigin => _activeInstance != null ? _activeInstance._shopOrigin : Vector3.zero;

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
                bool en = LocalizationService.IsEnglish;
                return en
                    ? $"{_activeInstance._items.Count} items ({total} ₺)"
                    : $"{_activeInstance._items.Count} ürün ({total} ₺)";
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
            _consumeAnim = GetComponent<PlayerConsumeAnimator>();
            _actionAnim = GetComponent<PlayerActionAnimator>();
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

            // Keep arms visibly inspecting & carrying a physical prop ONLY when actually holding a non-wearable prop!
            if (_heldProp != null && _foot != null && _foot.Humanoid != null)
            {
                bool busy = PlayerConsumeAnimator.IsAnyPlaying || PlayerActionAnimator.IsAnyPlaying ||
                            (_consumeAnim != null && _consumeAnim.IsPlaying) || (_actionAnim != null && _actionAnim.IsPlaying);
                _wasBusy = busy;
                if (!busy)
                {
                    float dt = Time.deltaTime;
                    _carryBlend = Mathf.Lerp(_carryBlend, 1f, 1f - Mathf.Exp(-10f * dt));
                    bool twoHand = UsesTwoHandCarry(_heldVisual);

                    if (_inspectActive)
                    {
                        _inspectTimer += dt;
                        float totalInspect = InspectHoldDuration + InspectSettleDuration;
                        if (_inspectTimer >= totalInspect)
                        {
                            _inspectActive = false;
                            _inspectYaw = 0f;
                        }
                        else
                        {
                            float settleU = _inspectTimer < InspectHoldDuration
                                ? 0f
                                : Mathf.SmoothStep(0f, 1f, (_inspectTimer - InspectHoldDuration) / InspectSettleDuration);
                            float inspectPhase = Mathf.Clamp01(_inspectTimer / InspectHoldDuration);
                            _inspectYaw = Mathf.Sin(inspectPhase * Mathf.PI) * 18f * (1f - settleU);

                            Vector3 inspectCenter = GetInspectCenter();
                            Vector3 propCenter = Vector3.Lerp(inspectCenter, GetTwoHandCarryCenter(), settleU);
                            _foot.Humanoid.SetLookAtPoint(true, propCenter);

                            if (twoHand)
                            {
                                float spread = Mathf.Lerp(0.125f, 0.135f, settleU);
                                Vector3 leftGrip = propCenter - transform.right * spread + Vector3.up * 0.045f;
                                Vector3 rightGrip = propCenter + transform.right * spread + Vector3.up * 0.045f;
                                _foot.Humanoid.SetConsumePose(0, 0f, 0f);
                                _foot.Humanoid.SetDualHandAction(true, leftGrip, rightGrip, 0.96f * _carryBlend, 0.96f * _carryBlend,
                                    Mathf.Lerp(5f, 3f, settleU) * _carryBlend, Mathf.Lerp(14f, 5f, settleU) * _carryBlend, 0f);
                            }
                            else
                            {
                                Vector3 oneHandTarget = Vector3.Lerp(inspectCenter + transform.right * 0.05f, GetOneHandCarryTarget(), settleU);
                                Vector3 leftSupport = propCenter - transform.right * 0.14f - Vector3.up * 0.04f;
                                float leftW = (1f - settleU) * 0.45f * _carryBlend;
                                _foot.Humanoid.SetConsumePose(0, 0f, 0f);
                                _foot.Humanoid.SetDualHandAction(true, leftSupport, oneHandTarget, leftW, 0.94f * _carryBlend,
                                    Mathf.Lerp(4f, 1f, settleU) * _carryBlend, Mathf.Lerp(14f, 3f, settleU) * _carryBlend, 0f);
                            }
                        }
                    }

                    if (!_inspectActive)
                    {
                        if (twoHand)
                        {
                            Vector3 carryCenter = GetTwoHandCarryCenter();
                            Vector3 leftGrip = carryCenter - transform.right * 0.135f + Vector3.up * 0.045f;
                            Vector3 rightGrip = carryCenter + transform.right * 0.135f + Vector3.up * 0.045f;
                            _foot.Humanoid.SetConsumePose(0, 0f, 0f);
                            _foot.Humanoid.SetDualHandAction(true, leftGrip, rightGrip, 0.96f * _carryBlend, 0.96f * _carryBlend,
                                3f * _carryBlend, 5f * _carryBlend, 0f);
                        }
                        else
                        {
                            _foot.Humanoid.SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
                            _foot.Humanoid.SetConsumePose(1, 0.88f * _carryBlend, 0f);
                        }
                    }
                }
            }

            // Fallback perimeter security catch if player somehow leaves the store zone without crossing the door gate!
            float distSq = (transform.position - _shopOrigin).sqrMagnitude;
            if (distSq > MaxLeaveDistance * MaxLeaveDistance)
            {
                StoreAntiTheftSystem.EnsureInstance().TriggerCatch(mode, _activeStoreName, _shopOrigin);
            }
        }

        private void LateUpdate()
        {
            if (_heldProp == null || _items.Count == 0 || _foot == null || _foot.Humanoid == null) return;

            bool actionPulling = _wasBusy || PlayerActionAnimator.IsAnyPlaying || (_actionAnim != null && _actionAnim.IsPlaying);
            if (actionPulling && _foot.Humanoid.RightForearm != null)
            {
                // During the shelf reach & pull-back, keep the item glued to the right palm so it only moves with the hand!
                Vector3 palmPos = _foot.Humanoid.RightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
                _heldProp.transform.position = palmPos - Vector3.up * 0.055f + transform.forward * 0.02f;
                _heldProp.transform.rotation = transform.rotation;
                return;
            }

            if (UsesTwoHandCarry(_heldVisual))
            {
                // Level & upright squarely between both gripping hands (elevated during chest inspection, then settled into carry)
                float settleU = !_inspectActive ? 1f : (_inspectTimer < InspectHoldDuration
                    ? 0f
                    : Mathf.SmoothStep(0f, 1f, (_inspectTimer - InspectHoldDuration) / InspectSettleDuration));
                _heldProp.transform.position = Vector3.Lerp(GetInspectCenter(), GetTwoHandCarryCenter(), settleU);
                _heldProp.transform.rotation = transform.rotation * Quaternion.Euler(0f, _inspectYaw, 0f);
            }
            else if (_foot.Humanoid.RightForearm != null)
            {
                // Upright cleanly on top of the right palm (never rotated upside-down along forearm bone!)
                Vector3 palmPos = _foot.Humanoid.RightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
                _heldProp.transform.position = palmPos - Vector3.up * 0.06f + transform.forward * 0.02f;
                _heldProp.transform.rotation = transform.rotation * Quaternion.Euler(0f, _inspectYaw, 0f);
            }
        }

        private Vector3 GetInspectCenter()
        {
            return transform.position + transform.forward * 0.38f + Vector3.up * 1.18f;
        }

        private Vector3 GetOneHandCarryTarget()
        {
            if (_foot != null && _foot.Humanoid != null && _foot.Humanoid.HeadTransform != null)
            {
                Transform hTr = _foot.Humanoid.transform;
                return _foot.Humanoid.HeadTransform.position + hTr.forward * 0.28f + hTr.right * 0.14f - hTr.up * 0.24f;
            }
            return transform.position + transform.forward * 0.30f + transform.right * 0.14f + Vector3.up * 1.24f;
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
                case ShopPropVisual.PorcelainSet:
                case ShopPropVisual.BookStack:
                case ShopPropVisual.MealTray:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsWearingUnpaidTrial(ShopItemEffect effect, int jacketColorIndex = 0)
        {
            if (_activeInstance == null || _activeInstance._items.Count == 0) return false;
            for (int i = 0; i < _activeInstance._items.Count; i++)
            {
                ShopCartItem it = _activeInstance._items[i];
                if (!it.IsTryOnWearable || it.Effect != effect) continue;
                if (effect == ShopItemEffect.JacketColor && it.JacketColorIndex != jacketColorIndex) continue;
                return true;
            }
            return false;
        }

        public static bool TryTakeOffTrialItem(PlayerModeController mode, ShopItemEffect effect, int jacketColorIndex = 0)
        {
            if (_activeInstance == null || _activeInstance._items.Count == 0) return false;
            for (int i = _activeInstance._items.Count - 1; i >= 0; i--)
            {
                ShopCartItem it = _activeInstance._items[i];
                if (!it.IsTryOnWearable || it.Effect != effect) continue;
                if (effect == ShopItemEffect.JacketColor && it.JacketColorIndex != jacketColorIndex) continue;

                string removedName = it.Name;
                _activeInstance._items.RemoveAt(i);
                _activeInstance.ReapplyActiveTryOnPreview();
                GameFeelAudio.PlayUiClick();
                bool en = LocalizationService.IsEnglish;
                mode?.RaiseStatusMessage(en
                    ? $"Took off {removedName} and placed it back on the stand."
                    : $"{removedName} çıkarılıp stanta geri bırakıldı.");
                return true;
            }
            return false;
        }

        public static void ConfiscateBySecurity(PlayerModeController mode)
        {
            if (_activeInstance == null) return;
            _activeInstance.ClearCart(silent: true);
        }

        private void ReapplyActiveTryOnPreview()
        {
            if (_foot == null) return;
            _foot.ApplySavedAppearance();
            _hasUnpaidTryOn = false;

            SaveData d = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            bool glass = d != null && d.ownsSunglasses && d.equipSunglasses;
            bool cap = d != null && d.ownsStreetCap && d.equipStreetCap;

            for (int i = 0; i < _items.Count; i++)
            {
                ShopCartItem it = _items[i];
                if (!it.IsTryOnWearable) continue;
                _hasUnpaidTryOn = true;
                if (it.Effect == ShopItemEffect.JacketColor)
                {
                    _foot.ApplyJacketColor(it.JacketColorIndex);
                }
                else if (it.Effect == ShopItemEffect.Sunglasses)
                {
                    glass = true;
                }
                else if (it.Effect == ShopItemEffect.StreetCap)
                {
                    cap = true;
                }
            }
            if (_hasUnpaidTryOn)
            {
                _foot.ApplyHeadAccessories(glass, cap);
            }
        }

        public bool TryAddItem(PlayerModeController mode, ShopCartItem item, Vector3 shelfWorldPos)
        {
            bool en = LocalizationService.IsEnglish;

            // If already trying on the EXACT same wearable, take it off cleanly!
            if (item.IsTryOnWearable && IsWearingUnpaidTrial(item.Effect, item.JacketColorIndex))
            {
                TryTakeOffTrialItem(mode, item.Effect, item.JacketColorIndex);
                return false;
            }

            // If trying on a different jacket while already wearing an unpaid trial jacket, swap cleanly!
            if (item.IsTryOnWearable)
            {
                for (int i = _items.Count - 1; i >= 0; i--)
                {
                    if (_items[i].IsTryOnWearable && _items[i].Effect == item.Effect)
                    {
                        _items.RemoveAt(i);
                    }
                }
            }

            if (_items.Count >= MaxCartItems)
            {
                if (mode != null)
                {
                    mode.RaiseStatusMessage(en
                        ? $"Hands full ({ActiveSummary}) — Head to the Checkout Counter to pay first!"
                        : $"Elin dolu ({ActiveSummary}) — Önce Kasaya götürüp öde!");
                }
                return false;
            }

            if (_items.Count == 0)
            {
                _shopOrigin = shelfWorldPos;
                _activeStoreName = item.SourceStoreName ?? string.Empty;
            }
            else if (!string.IsNullOrEmpty(item.SourceStoreName))
            {
                _activeStoreName = item.SourceStoreName;
            }
            if (_actionAnim == null && _foot != null) _actionAnim = _foot.GetComponent<PlayerActionAnimator>();
            if (_consumeAnim == null && _foot != null) _consumeAnim = _foot.GetComponent<PlayerConsumeAnimator>();

            _items.Add(item);

            if (item.IsTryOnWearable)
            {
                ReapplyActiveTryOnPreview();
                if (mode != null)
                {
                    mode.RaiseStatusMessage(en
                        ? $"Trying on {item.Name} ({item.Price} ₺) in 3D! Pay at the register — leaving without paying triggers the alarm! (Total: {TotalPrice} ₺)"
                        : $"{item.Name} ({item.Price} ₺) üzerinde deniyorsun! Kasada öde — ödemeden kapıdan çıkarsan alarm çalar! (Toplam: {TotalPrice} ₺)");
                }
            }
            else
            {
                _inspectActive = true;
                _inspectTimer = 0f;
                _inspectYaw = 0f;
                _carryBlend = 0.85f;
                RebuildHeldProp(item, shelfWorldPos);

                if (mode != null)
                {
                    mode.RaiseStatusMessage(en
                        ? $"Holding {item.Name} ({item.Price} ₺) — Pay at the register! Leaving without paying triggers the alarm! (Total: {TotalPrice} ₺)"
                        : $"{item.Name} ({item.Price} ₺) elinde — Kasaya götürüp öde! Ödemeden kapıdan çıkarsan alarm çalar! (Toplam: {TotalPrice} ₺)");
                }
            }
            return true;
        }

        public void Checkout(PlayerModeController mode, Transform registerTransform)
        {
            if (_checkingOut || _items.Count == 0 || mode == null || SaveManager.Instance == null || SaveManager.Instance.Data == null)
            {
                return;
            }

            bool en = LocalizationService.IsEnglish;
            int total = TotalPrice;
            if (SaveManager.Instance.Data.money < total)
            {
                mode.RaiseStatusMessage(en
                    ? $"Insufficient funds! ({total} ₺ needed · Balance: {SaveManager.Instance.Data.money} ₺) — Items returned to the stands."
                    : $"Yetersiz bakiye! ({total} ₺ gerekli · Bakiye: {SaveManager.Instance.Data.money} ₺) — Ürünler stantlara geri bırakıldı.");
                ClearCart(silent: true);
                return;
            }

            _checkingOut = true;
            List<ShopCartItem> snapshot = new List<ShopCartItem>(_items);
            bool fromMall = !string.IsNullOrEmpty(_activeStoreName) &&
                            _activeStoreName.IndexOf("Bakkal", System.StringComparison.OrdinalIgnoreCase) < 0;
            // Keep Try-On preview smoothly visible during keypad animation until permanently applied in onClimax
            ClearCart(silent: true, revertTryOn: false);

            float pendingEnergy = 0f;
            int pendingRepXp = 0;
            ConsumeAnimKind pendingConsume = ConsumeAnimKind.None;
            string pendingLabel = snapshot[snapshot.Count - 1].Name;
            bool paidOk = false;

            PlayerActionAnimator.Play(mode, registerTransform, PlayerActionKind.AtmKeypad, onClimax: () =>
            {
                if (SaveManager.Instance == null || !SaveManager.Instance.TrySpend(total))
                {
                    _foot?.ApplySavedAppearance();
                    mode.RaiseStatusMessage(en ? "Payment could not be completed." : "Ödeme tamamlanamadı.");
                    return;
                }

                paidOk = true;
                GameFeelAudio.PlayCoinChime();
                Vector3 fxPos = registerTransform != null
                    ? registerTransform.position + Vector3.up * 1.15f
                    : mode.ActiveWorldPosition + Vector3.up * 1.25f;
                GameFeelAudio.SpawnBurst(fxPos, new Color(0.98f, 0.84f, 0.28f), count: 14, spread: 0.50f);

                int packedServings = 0;
                for (int i = 0; i < snapshot.Count; i++)
                {
                    ShopCartItem it = snapshot[i];
                    pendingRepXp += it.ReputationXp;
                    if (it.Effect == ShopItemEffect.Energy)
                    {
                        if (PlayerBackpackInventory.ShouldPackIntoBackpack(it, isCheckoutStore: true))
                        {
                            packedServings += PlayerBackpackInventory.EnsureInstance().AddFromShopCartItem(it);
                        }
                        else
                        {
                            pendingEnergy += it.EffectParam;
                            if (it.ConsumeKind != ConsumeAnimKind.None)
                            {
                                pendingConsume = it.ConsumeKind;
                                pendingLabel = it.Name;
                            }
                        }
                    }
                    else
                    {
                        ApplyNonEnergyEffect(mode, it);
                    }
                }

                if (pendingRepXp > 0)
                {
                    NeighborhoodReputation.AddReputation(pendingRepXp);
                }
                if (fromMall)
                {
                    DailyQuestAndAchievementSystem.RecordMallPurchase();
                }

                string xpNote = pendingRepXp > 0 ? $" · +{pendingRepXp} XP" : string.Empty;
                if (packedServings > 0 && pendingConsume == ConsumeAnimKind.None)
                {
                    VisualEffectsManager.SpawnFloatingText(
                        mode.ActiveWorldPosition + Vector3.up * 2.3f,
                        en ? $"+{packedServings} Backpack!" : $"+{packedServings} Çantaya!",
                        new Color(0.32f, 0.94f, 0.62f),
                        scale: 1.0f,
                        duration: 1.15f);
                    mode.RaiseStatusMessage(en
                        ? $"Paid at checkout (-{total} ₺{xpNote}) · Packed {packedServings} item(s) into your Backpack! Open Bag (B) whenever hungry."
                        : $"Kasada ödendi (-{total} ₺{xpNote}) · {packedServings} ürün Çantana eklendi! Acıkınca Çanta'dan (B) yiyebilirsin.");
                }
                else if (pendingConsume == ConsumeAnimKind.None)
                {
                    if (pendingEnergy > 0f && PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(pendingEnergy);
                    }
                    mode.RaiseStatusMessage(en
                        ? $"Paid at checkout (-{total} ₺{xpNote}) · Enjoy, neighbor!"
                        : $"Kasada ödendi (-{total} ₺{xpNote}) · Güle güle kullan komşu!");
                }
                else
                {
                    string packNote = packedServings > 0
                        ? (en ? $" (+{packedServings} to Backpack)" : $" (+{packedServings} Çantaya)")
                        : string.Empty;
                    mode.RaiseStatusMessage(en
                        ? $"Paid at checkout (-{total} ₺{xpNote}){packNote} · Enjoying {pendingLabel}!"
                        : $"Kasada ödendi (-{total} ₺{xpNote}){packNote} · {pendingLabel} afiyetle tüketiliyor!");
                }
            }, onFinished: () =>
            {
                _checkingOut = false;
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
            }, startStatus: en ? $"Paying at checkout ({total} ₺)..." : $"Kasada ödeme yapılıyor ({total} ₺)...");
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
                    int idx = Mathf.Clamp(item.JacketColorIndex, 0, PlayerFootController.JacketColorCount - 1);
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
                case ShopItemEffect.Sunglasses:
                    data.ownsSunglasses = true;
                    data.equipSunglasses = true;
                    SaveManager.Instance.Save();
                    mode?.FootController?.ApplySavedAppearance();
                    break;
                case ShopItemEffect.StreetCap:
                    data.ownsStreetCap = true;
                    data.equipStreetCap = true;
                    SaveManager.Instance.Save();
                    mode?.FootController?.ApplySavedAppearance();
                    break;
                case ShopItemEffect.SeedPack:
                    data.invTomatoes += 2;
                    data.invStrawberries += 2;
                    SaveManager.Instance.Save();
                    break;
                case ShopItemEffect.ReputationPrestige:
                    if (item.EffectParam > 0f && PlayerNeedsController.Instance != null)
                    {
                        PlayerNeedsController.Instance.RestoreEnergy(item.EffectParam);
                    }
                    break;
            }
        }

        public void ClearCart(bool silent, string message = null, bool revertTryOn = true)
        {
            bool hadTryOn = _hasUnpaidTryOn;
            _items.Clear();
            _hasUnpaidTryOn = false;
            _activeStoreName = string.Empty;
            _inspectActive = false;
            _inspectTimer = 0f;
            _inspectYaw = 0f;
            _carryBlend = 0f;
            _wasBusy = false;
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
            if (revertTryOn && hadTryOn && _foot != null)
            {
                _foot.ApplySavedAppearance();
            }
            if (!silent && !string.IsNullOrEmpty(message) && PlayerModeController.Instance != null)
            {
                PlayerModeController.Instance.RaiseStatusMessage(message);
            }
            PlayerModeController.Instance?.RefreshCurrentInteractPrompt();
        }

        private void RebuildHeldProp(ShopCartItem item, Vector3 spawnWorldPos)
        {
            if (_heldProp != null)
            {
                Destroy(_heldProp);
                _heldProp = null;
            }
            if (_foot == null || _foot.Humanoid == null) return;

            _heldVisual = item.Visual;
            Vector3 initialPos = _foot.Humanoid.RightForearm != null
                ? _foot.Humanoid.RightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f)) - Vector3.up * 0.055f
                : spawnWorldPos;
            // Keep unparented from forearm bone so LateUpdate keeps the prop 100% upright & level in hand(s)!
            _heldProp = ShopPropFactory.BuildProp(
                null,
                item.Visual,
                initialPos,
                uniformScale: UsesTwoHandCarry(item.Visual) ? 0.95f : 0.88f,
                jacketColorIndex: item.JacketColorIndex);
        }
    }
}
