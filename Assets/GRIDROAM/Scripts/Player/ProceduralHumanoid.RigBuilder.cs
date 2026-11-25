using System;
using UnityEngine;

namespace OurWorld.Player
{
    public partial class ProceduralHumanoid
    {
        public static ProceduralHumanoid Build(
            Transform parent,
            Material skin,
            Material shirt,
            Material pants,
            Material shoes,
            Material hair,
            float height = 1.78f)
        {
            GameObject root = new GameObject("ProceduralHumanoid");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            float uniformScale = Mathf.Max(0.25f, height / 1.78f);
            root.transform.localScale = Vector3.one * uniformScale;

            ProceduralHumanoid hum = root.AddComponent<ProceduralHumanoid>();
            hum.hipsBaseY = 0.95f;

            hum.hips = CreatePivot("Hips", root.transform, new Vector3(0f, 0.95f, 0f));
            CreatePart("Pelvis", PrimitiveType.Capsule, hum.hips,
                new Vector3(0f, 0.00f, 0f), new Vector3(0.31f, 0.13f, 0.21f), pants);

            hum.spine = CreatePivot("Spine", hum.hips, new Vector3(0f, 0.06f, 0f));
            CreatePart("Waist", PrimitiveType.Capsule, hum.spine,
                new Vector3(0f, 0.13f, 0f), new Vector3(0.30f, 0.17f, 0.20f), shirt);
            GameObject chest = CreatePart("Chest", PrimitiveType.Capsule, hum.spine,
                new Vector3(0f, 0.30f, 0.01f), new Vector3(0.39f, 0.20f, 0.23f), shirt);
            hum.chestVisual = chest.transform;
            hum._chestBaseScale = chest.transform.localScale;

            hum.neck = CreatePivot("Neck", hum.spine, new Vector3(0f, 0.46f, 0f));
            CreatePart("NeckMesh", PrimitiveType.Cylinder, hum.neck,
                new Vector3(0f, 0.045f, 0f), new Vector3(0.105f, 0.055f, 0.105f), skin);

            hum.head = CreatePivot("Head", hum.neck, new Vector3(0f, 0.09f, 0f));
            CreatePart("HeadMesh", PrimitiveType.Sphere, hum.head,
                new Vector3(0f, 0.10f, 0f), new Vector3(0.22f, 0.25f, 0.23f), skin);
            CreatePart("HairCap", PrimitiveType.Sphere, hum.head,
                new Vector3(0f, 0.145f, -0.025f), new Vector3(0.235f, 0.175f, 0.235f), hair);
            CreatePart("Nose", PrimitiveType.Cube, hum.head,
                new Vector3(0f, 0.092f, 0.115f), new Vector3(0.036f, 0.048f, 0.042f), skin);

            BuildArm("L", -1f, hum.spine, shirt, skin, out hum.lShoulder, out hum.lElbow);
            BuildArm("R", 1f, hum.spine, shirt, skin, out hum.rShoulder, out hum.rElbow);

            BuildLeg("L", -1f, hum.hips, pants, shoes, out hum.lHip, out hum.lKnee, out hum.lAnkle);
            BuildLeg("R", 1f, hum.hips, pants, shoes, out hum.rHip, out hum.rKnee, out hum.rAnkle);

            return hum;
        }

        /// <summary>
        /// Builds the exact 14-pivot ProceduralHumanoid skeleton and mounts externally sculpted
        /// body-part meshes (e.g. Blender FBX parts centered at each joint origin) onto every pivot.
        /// </summary>
        public static ProceduralHumanoid BuildWithCustomParts(
            Transform parent,
            Func<string, Transform, Transform> mountPart,
            float height = 1.78f,
            float shoulderX = 0.198f,
            float hipX = 0.102f,
            float shoulderY = 0.382f)
        {
            GameObject root = new GameObject("ProceduralHumanoid");
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }

            float uniformScale = Mathf.Max(0.25f, height / 1.78f);
            root.transform.localScale = Vector3.one * uniformScale;

            ProceduralHumanoid hum = root.AddComponent<ProceduralHumanoid>();
            hum.hipsBaseY = 0.95f;

            hum.hips = CreatePivot("Hips", root.transform, new Vector3(0f, 0.95f, 0f));
            mountPart?.Invoke("Part_Pelvis", hum.hips);

            hum.spine = CreatePivot("Spine", hum.hips, new Vector3(0f, 0.06f, 0f));
            Transform chestMounted = mountPart?.Invoke("Part_Chest", hum.spine);
            if (chestMounted != null)
            {
                hum.chestVisual = chestMounted;
                hum._chestBaseScale = chestMounted.localScale;
            }

            hum.neck = CreatePivot("Neck", hum.spine, new Vector3(0f, 0.46f, 0f));
            mountPart?.Invoke("Part_Neck", hum.neck);

            hum.head = CreatePivot("Head", hum.neck, new Vector3(0f, 0.09f, 0f));
            mountPart?.Invoke("Part_Head", hum.head);

            hum.lShoulder = CreatePivot("L_Shoulder", hum.spine, new Vector3(-shoulderX, shoulderY, 0f));
            mountPart?.Invoke("Part_L_UpperArm", hum.lShoulder);
            hum.lElbow = CreatePivot("L_Elbow", hum.lShoulder, new Vector3(0f, -0.295f, 0f));
            mountPart?.Invoke("Part_L_Forearm", hum.lElbow);

            hum.rShoulder = CreatePivot("R_Shoulder", hum.spine, new Vector3(shoulderX, shoulderY, 0f));
            mountPart?.Invoke("Part_R_UpperArm", hum.rShoulder);
            hum.rElbow = CreatePivot("R_Elbow", hum.rShoulder, new Vector3(0f, -0.295f, 0f));
            mountPart?.Invoke("Part_R_Forearm", hum.rElbow);

            hum.lHip = CreatePivot("L_Hip", hum.hips, new Vector3(-hipX, -0.02f, 0f));
            mountPart?.Invoke("Part_L_Thigh", hum.lHip);
            hum.lKnee = CreatePivot("L_Knee", hum.lHip, new Vector3(0f, -0.43f, 0f));
            mountPart?.Invoke("Part_L_Shin", hum.lKnee);
            hum.lAnkle = CreatePivot("L_Ankle", hum.lKnee, new Vector3(0f, -0.42f, 0f));
            mountPart?.Invoke("Part_L_Foot", hum.lAnkle);

            hum.rHip = CreatePivot("R_Hip", hum.hips, new Vector3(hipX, -0.02f, 0f));
            mountPart?.Invoke("Part_R_Thigh", hum.rHip);
            hum.rKnee = CreatePivot("R_Knee", hum.rHip, new Vector3(0f, -0.43f, 0f));
            mountPart?.Invoke("Part_R_Shin", hum.rKnee);
            hum.rAnkle = CreatePivot("R_Ankle", hum.rKnee, new Vector3(0f, -0.42f, 0f));
            mountPart?.Invoke("Part_R_Foot", hum.rAnkle);

            return hum;
        }

        private static void BuildArm(
            string side,
            float xSign,
            Transform spineParent,
            Material shirt,
            Material skin,
            out Transform shoulderPivot,
            out Transform elbowPivot)
        {
            shoulderPivot = CreatePivot($"{side}_Shoulder", spineParent, new Vector3(xSign * 0.225f, 0.40f, 0f));
            CreatePart($"{side}_UpperArm", PrimitiveType.Capsule, shoulderPivot,
                new Vector3(0f, -0.15f, 0f), new Vector3(0.115f, 0.165f, 0.115f), shirt);

            elbowPivot = CreatePivot($"{side}_Elbow", shoulderPivot, new Vector3(0f, -0.30f, 0f));
            CreatePart($"{side}_Forearm", PrimitiveType.Capsule, elbowPivot,
                new Vector3(0f, -0.135f, 0f), new Vector3(0.095f, 0.15f, 0.095f), shirt);
            CreatePart($"{side}_Hand", PrimitiveType.Sphere, elbowPivot,
                new Vector3(0f, -0.295f, 0.01f), new Vector3(0.072f, 0.105f, 0.045f), skin);
        }

        private static void BuildLeg(
            string side,
            float xSign,
            Transform hipsParent,
            Material pants,
            Material shoes,
            out Transform hipPivot,
            out Transform kneePivot,
            out Transform anklePivot)
        {
            hipPivot = CreatePivot($"{side}_Hip", hipsParent, new Vector3(xSign * 0.105f, -0.02f, 0f));
            CreatePart($"{side}_Thigh", PrimitiveType.Capsule, hipPivot,
                new Vector3(0f, -0.215f, 0f), new Vector3(0.155f, 0.23f, 0.16f), pants);

            kneePivot = CreatePivot($"{side}_Knee", hipPivot, new Vector3(0f, -0.43f, 0f));
            CreatePart($"{side}_Shin", PrimitiveType.Capsule, kneePivot,
                new Vector3(0f, -0.21f, 0f), new Vector3(0.125f, 0.225f, 0.13f), pants);

            anklePivot = CreatePivot($"{side}_Ankle", kneePivot, new Vector3(0f, -0.42f, 0f));
            CreatePart($"{side}_Foot", PrimitiveType.Cube, anklePivot,
                new Vector3(0f, -0.04f, 0.055f), new Vector3(0.11f, 0.08f, 0.25f), shoes);
        }

        private static Transform CreatePivot(string pivotName, Transform parent, Vector3 localPos)
        {
            GameObject go = new GameObject(pivotName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            return go.transform;
        }

        private static GameObject CreatePart(
            string partName,
            PrimitiveType type,
            Transform parent,
            Vector3 localPos,
            Vector3 localScale,
            Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = partName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = localScale;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(col);
                }
                else
                {
                    DestroyImmediate(col);
                }
            }

            if (mat != null)
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = mat;
                }
            }

            return go;
        }

    }
}
