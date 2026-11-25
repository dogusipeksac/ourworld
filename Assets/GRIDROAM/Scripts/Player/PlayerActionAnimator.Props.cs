using System;
using System.Collections;
using System.Collections.Generic;
using OurWorld.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OurWorld.Player
{
    public partial class PlayerActionAnimator
    {
        private static void PositionCylinderBetween(Transform cyl, Vector3 a, Vector3 b, float radius)
        {
            Vector3 diff = b - a;
            float len = diff.magnitude;
            if (len < 0.005f) return;
            cyl.position = (a + b) * 0.5f;
            cyl.up = diff / len;
            cyl.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
        }

        private static GameObject SpawnMilkBucket(Vector3 groundPos, out Transform milkFill, out GameObject streamL, out GameObject streamR)
        {
            GameObject root = new GameObject("TempMilkBucket");
            root.transform.position = groundPos;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material steel = MakeSimpleMat(urpLit, new Color(0.78f, 0.82f, 0.86f), 0.76f, 0.85f);
            Material rim = MakeSimpleMat(urpLit, new Color(0.90f, 0.74f, 0.26f), 0.72f, 0.84f);
            Material milk = MakeSimpleMat(urpLit, new Color(0.99f, 0.99f, 0.97f), 0.58f, 0.0f);

            CreatePropPart("PailBody", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.17f, 0f), new Vector3(0.28f, 0.17f, 0.28f), steel);
            CreatePropPart("PailRim", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.335f, 0f), new Vector3(0.305f, 0.012f, 0.305f), rim);
            CreatePropPart("BailHandle", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.36f, 0f), new Vector3(0.28f, 0.016f, 0.016f), steel);

            GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fillGo.name = "MilkFill";
            fillGo.transform.SetParent(root.transform, false);
            fillGo.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            fillGo.transform.localScale = new Vector3(0.248f, 0.015f, 0.248f);
            Destroy(fillGo.GetComponent<Collider>());
            Renderer fr = fillGo.GetComponent<Renderer>();
            if (fr != null) { fr.sharedMaterial = milk; fr.shadowCastingMode = ShadowCastingMode.Off; }
            milkFill = fillGo.transform;

            streamL = CreateWorldMilkStream("MilkStreamL", milk);
            streamR = CreateWorldMilkStream("MilkStreamR", milk);
            streamL.transform.SetParent(root.transform, true);
            streamR.transform.SetParent(root.transform, true);
            streamL.SetActive(false);
            streamR.SetActive(false);
            return root;
        }

        private static GameObject CreateWorldMilkStream(string name, Material milkMat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) { r.sharedMaterial = milkMat; r.shadowCastingMode = ShadowCastingMode.Off; }
            return go;
        }

        private static GameObject SpawnMilkingStool(Vector3 groundPos, Quaternion rot)
        {
            GameObject root = new GameObject("TempMilkingStool");
            root.transform.SetPositionAndRotation(groundPos, rot);
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material oak = MakeSimpleMat(urpLit, new Color(0.56f, 0.36f, 0.18f), 0.24f, 0.02f);
            Material darkWood = MakeSimpleMat(urpLit, new Color(0.42f, 0.26f, 0.12f), 0.20f, 0.02f);

            CreatePropPart("Seat", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.030f, 0.34f), oak);
            for (int i = 0; i < 3; i++)
            {
                float a = (i / 3f) * Mathf.PI * 2f + 0.5f;
                Vector3 lp = new Vector3(Mathf.Cos(a) * 0.105f, 0.21f, Mathf.Sin(a) * 0.105f);
                Quaternion lr = Quaternion.Euler(Mathf.Sin(a) * 10f, 0f, -Mathf.Cos(a) * 10f);
                CreatePropPart($"Leg_{i}", PrimitiveType.Cylinder, root.transform,
                    lp, new Vector3(0.038f, 0.21f, 0.038f), darkWood, lr);
            }
            return root;
        }

        private static GameObject SpawnArticulatedShears(Transform rightForearm, out Transform bladeLPivot, out Transform bladeRPivot)
        {
            bladeLPivot = null;
            bladeRPivot = null;
            if (rightForearm == null) return null;

            GameObject root = new GameObject("HeldArticulatedShears");
            root.transform.position = rightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material steel = MakeSimpleMat(urpLit, new Color(0.88f, 0.90f, 0.94f), 0.88f, 0.92f);
            Material redGrip = MakeSimpleMat(urpLit, new Color(0.88f, 0.16f, 0.14f), 0.38f, 0.05f);
            Material bolt = MakeSimpleMat(urpLit, new Color(0.94f, 0.78f, 0.24f), 0.74f, 0.86f);

            CreatePropPart("GripLoopL", PrimitiveType.Cube, root.transform,
                new Vector3(-0.028f, 0f, -0.02f), new Vector3(0.028f, 0.028f, 0.14f), redGrip, Quaternion.Euler(0f, 6f, 0f));
            CreatePropPart("GripLoopR", PrimitiveType.Cube, root.transform,
                new Vector3( 0.028f, 0f, -0.02f), new Vector3(0.028f, 0.028f, 0.14f), redGrip, Quaternion.Euler(0f, -6f, 0f));
            CreatePropPart("PivotBolt", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, 0.002f, 0.065f), new Vector3(0.032f, 0.020f, 0.032f), bolt);

            GameObject pivL = new GameObject("BladeLPivot");
            pivL.transform.SetParent(root.transform, false);
            pivL.transform.localPosition = new Vector3(0f, 0.006f, 0.065f);
            CreatePropPart("BladeL", PrimitiveType.Cube, pivL.transform,
                new Vector3(-0.012f, 0f, 0.14f), new Vector3(0.028f, 0.010f, 0.28f), steel);
            bladeLPivot = pivL.transform;

            GameObject pivR = new GameObject("BladeRPivot");
            pivR.transform.SetParent(root.transform, false);
            pivR.transform.localPosition = new Vector3(0f, -0.006f, 0.065f);
            CreatePropPart("BladeR", PrimitiveType.Cube, pivR.transform,
                new Vector3(0.012f, 0f, 0.14f), new Vector3(0.028f, 0.010f, 0.28f), steel);
            bladeRPivot = pivR.transform;

            return root;
        }

        private static GameObject SpawnGrowingWoolPile(Vector3 groundPos)
        {
            GameObject root = new GameObject("TempShearedWoolPile");
            root.transform.position = groundPos;
            root.transform.localScale = Vector3.one * 0.05f;
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material wool = MakeSimpleMat(urpLit, new Color(0.97f, 0.95f, 0.90f), 0.10f, 0.0f);
            CreatePropPart("WoolBase", PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.38f, 0.22f, 0.34f), wool);
            CreatePropPart("WoolPuffA", PrimitiveType.Sphere, root.transform, new Vector3(-0.14f, 0.14f, 0.08f), new Vector3(0.24f, 0.20f, 0.22f), wool);
            CreatePropPart("WoolPuffB", PrimitiveType.Sphere, root.transform, new Vector3( 0.15f, 0.15f, -0.06f), new Vector3(0.25f, 0.21f, 0.24f), wool);
            CreatePropPart("WoolTop", PrimitiveType.Sphere, root.transform, new Vector3(0.02f, 0.24f, 0.02f), new Vector3(0.26f, 0.19f, 0.25f), wool);
            return root;
        }

        private static GameObject SpawnWickerBasket(Transform leftForearm, bool withEggSlot, out GameObject eggInside)
        {
            eggInside = null;
            if (leftForearm == null) return null;

            // Anchored cleanly at HandleTop (y = 0) so Deniz's left palm grips the wicker handle naturally!
            GameObject root = new GameObject("HeldWickerBasket");
            root.transform.position = leftForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material wicker = MakeSimpleMat(urpLit, new Color(0.72f, 0.50f, 0.24f), 0.22f, 0.02f);
            Material rim = MakeSimpleMat(urpLit, new Color(0.56f, 0.36f, 0.16f), 0.20f, 0.02f);
            Material straw = MakeSimpleMat(urpLit, new Color(0.92f, 0.78f, 0.34f), 0.12f, 0.0f);
            Material eggMat = MakeSimpleMat(urpLit, new Color(0.98f, 0.94f, 0.84f), 0.45f, 0.0f);

            CreatePropPart("HandleTop", PrimitiveType.Cube, root.transform,
                new Vector3(0f, 0.00f, 0.02f), new Vector3(0.25f, 0.024f, 0.028f), rim);
            CreatePropPart("HandleL", PrimitiveType.Cylinder, root.transform,
                new Vector3(-0.13f, -0.085f, 0.02f), new Vector3(0.022f, 0.088f, 0.022f), rim, Quaternion.Euler(0f, 0f, -10f));
            CreatePropPart("HandleR", PrimitiveType.Cylinder, root.transform,
                new Vector3( 0.13f, -0.085f, 0.02f), new Vector3(0.022f, 0.088f, 0.022f), rim, Quaternion.Euler(0f, 0f,  10f));
            CreatePropPart("BasketRim", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.165f, 0.02f), new Vector3(0.305f, 0.016f, 0.285f), rim);
            CreatePropPart("BasketBody", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.255f, 0.02f), new Vector3(0.28f, 0.095f, 0.26f), wicker);
            CreatePropPart("StrawNest", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.185f, 0.02f), new Vector3(0.25f, 0.025f, 0.23f), straw);

            if (withEggSlot)
            {
                GameObject egg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                egg.name = "BasketEgg";
                egg.transform.SetParent(root.transform, false);
                egg.transform.localPosition = new Vector3(0f, -0.125f, 0.02f);
                egg.transform.localScale = new Vector3(0.10f, 0.135f, 0.10f);
                egg.transform.localRotation = Quaternion.Euler(22f, 18f, -14f);
                Destroy(egg.GetComponent<Collider>());
                Renderer r = egg.GetComponent<Renderer>();
                if (r != null) { r.sharedMaterial = eggMat; r.shadowCastingMode = ShadowCastingMode.Off; }
                eggInside = egg;
            }
            return root;
        }

        private static GameObject SpawnHandheldEgg(Transform rightForearm)
        {
            if (rightForearm == null) return null;
            GameObject root = new GameObject("HeldVillageEgg");
            root.transform.position = rightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material eggMat = MakeSimpleMat(urpLit, new Color(0.98f, 0.94f, 0.84f), 0.48f, 0.0f);
            CreatePropPart("EggMesh", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, -0.02f, 0.03f), new Vector3(0.095f, 0.128f, 0.095f), eggMat);
            return root;
        }

        private static GameObject SpawnGrainScoop(Transform leftForearm)
        {
            if (leftForearm == null) return null;
            GameObject root = new GameObject("HeldGrainScoop");
            root.transform.position = leftForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            Material wood = MakeSimpleMat(urpLit, new Color(0.60f, 0.40f, 0.20f), 0.25f, 0.02f);
            Material grain = MakeSimpleMat(urpLit, new Color(0.94f, 0.76f, 0.22f), 0.18f, 0.0f);
            CreatePropPart("Bowl", PrimitiveType.Cylinder, root.transform,
                new Vector3(0f, -0.04f, 0.04f), new Vector3(0.22f, 0.055f, 0.22f), wood);
            CreatePropPart("GrainHeap", PrimitiveType.Sphere, root.transform,
                new Vector3(0f, 0.015f, 0.04f), new Vector3(0.19f, 0.075f, 0.19f), grain);
            return root;
        }

        private static GameObject SpawnActionProp(Transform rightForearm, PlayerActionKind kind)
        {
            if (rightForearm == null) return null;

            GameObject root = new GameObject($"HeldActionProp_{kind}");
            root.transform.position = rightForearm.TransformPoint(new Vector3(0f, -0.305f, 0f));
            root.transform.rotation = Quaternion.identity;

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

            if (kind == PlayerActionKind.UseTvRemote)
            {
                CreatePropPart("RemoteBody", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 0f, 0.05f), new Vector3(0.048f, 0.020f, 0.165f),
                    MakeSimpleMat(urpLit, new Color(0.10f, 0.11f, 0.13f), 0.35f, 0.05f));
                CreatePropPart("PowerBtn", PrimitiveType.Cube, root.transform,
                    new Vector3(-0.012f, 0.012f, 0.105f), new Vector3(0.012f, 0.008f, 0.012f),
                    MakeSimpleMat(urpLit, new Color(0.88f, 0.16f, 0.14f), 0.50f, 0f));
                CreatePropPart("ChannelPad", PrimitiveType.Cube, root.transform,
                    new Vector3(0.004f, 0.011f, 0.055f), new Vector3(0.032f, 0.006f, 0.048f),
                    MakeSimpleMat(urpLit, new Color(0.45f, 0.48f, 0.52f), 0.40f, 0.15f));
            }
            else if (kind == PlayerActionKind.RefuelCar)
            {
                CreatePropPart("NozzleGrip", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, -0.02f, 0.02f), new Vector3(0.045f, 0.12f, 0.075f),
                    MakeSimpleMat(urpLit, new Color(0.12f, 0.58f, 0.24f), 0.40f, 0.10f));
                CreatePropPart("NozzleSpout", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, 0.03f, 0.13f), new Vector3(0.026f, 0.10f, 0.026f),
                    MakeSimpleMat(urpLit, new Color(0.70f, 0.74f, 0.78f), 0.75f, 0.80f),
                    Quaternion.Euler(82f, 0f, 0f));
            }
            else if (kind == PlayerActionKind.FarmWatering)
            {
                Material canMat = MakeSimpleMat(urpLit, new Color(0.12f, 0.66f, 0.64f), 0.48f, 0.20f);
                Material brassMat = MakeSimpleMat(urpLit, new Color(0.90f, 0.74f, 0.26f), 0.70f, 0.80f);
                CreatePropPart("CanBody", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, -0.10f, 0.06f), new Vector3(0.20f, 0.13f, 0.20f), canMat);
                CreatePropPart("CanSpout", PrimitiveType.Cylinder, root.transform,
                    new Vector3(0f, -0.06f, 0.24f), new Vector3(0.035f, 0.14f, 0.035f), canMat,
                    Quaternion.Euler(64f, 0f, 0f));
                CreatePropPart("CanRose", PrimitiveType.Sphere, root.transform,
                    new Vector3(0f, 0.01f, 0.36f), new Vector3(0.08f, 0.08f, 0.05f), brassMat);
            }
            else if (kind == PlayerActionKind.ShearSheep)
            {
                Material bladeMat = MakeSimpleMat(urpLit, new Color(0.84f, 0.86f, 0.90f), 0.82f, 0.88f);
                Material gripMat = MakeSimpleMat(urpLit, new Color(0.84f, 0.18f, 0.16f), 0.35f, 0.05f);
                CreatePropPart("ShearsGrip", PrimitiveType.Cube, root.transform,
                    new Vector3(0f, 0f, 0.02f), new Vector3(0.05f, 0.03f, 0.12f), gripMat);
                CreatePropPart("BladeL", PrimitiveType.Cube, root.transform,
                    new Vector3(-0.02f, 0f, 0.15f), new Vector3(0.018f, 0.012f, 0.18f), bladeMat,
                    Quaternion.Euler(0f, -8f, 0f));
                CreatePropPart("BladeR", PrimitiveType.Cube, root.transform,
                    new Vector3( 0.02f, 0f, 0.15f), new Vector3(0.018f, 0.012f, 0.18f), bladeMat,
                    Quaternion.Euler(0f,  8f, 0f));
            }
            else if (kind == PlayerActionKind.GrillMangal)
            {
                Material steel = MakeSimpleMat(urpLit, new Color(0.82f, 0.84f, 0.88f), 0.85f, 0.90f);
                CreatePropPart("TongsA", PrimitiveType.Cube, root.transform,
                    new Vector3(-0.018f, 0f, 0.16f), new Vector3(0.012f, 0.014f, 0.32f), steel,
                    Quaternion.Euler(0f, -4f, 0f));
                CreatePropPart("TongsB", PrimitiveType.Cube, root.transform,
                    new Vector3( 0.018f, 0f, 0.16f), new Vector3(0.012f, 0.014f, 0.32f), steel,
                    Quaternion.Euler(0f,  4f, 0f));
            }

            return root;
        }

        private static void CreatePropPart(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat,
            Quaternion? localRot = null)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (localRot.HasValue) part.transform.localRotation = localRot.Value;
            Collider col = part.GetComponent<Collider>();
            if (col != null) Destroy(col);
            Renderer r = part.GetComponent<Renderer>();
            if (r != null)
            {
                r.sharedMaterial = mat;
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<int, Material> SimpleMatCache =
            new System.Collections.Generic.Dictionary<int, Material>(48);

        private static Material MakeSimpleMat(Shader shader, Color col, float smoothness, float metallic)
        {
            Color32 c32 = col;
            int shId = shader != null ? shader.GetHashCode() : 0;
            int key = shId ^ (c32.r << 24) ^ (c32.g << 16) ^ (c32.b << 8) ^ c32.a
                      ^ (Mathf.RoundToInt(smoothness * 100f) * 397)
                      ^ (Mathf.RoundToInt(metallic * 100f) * 7919);
            if (SimpleMatCache.TryGetValue(key, out Material cached) && cached != null)
            {
                return cached;
            }

            Material m = new Material(shader);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            SimpleMatCache[key] = m;
            return m;
        }
    }
}
