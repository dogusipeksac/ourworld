#if UNITY_EDITOR
using System;
using System.IO;
using OurWorld.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace OurWorld.Editor
{
    /// <summary>
    /// One-click Mixamo / Ready Player Me Humanoid Character & Animation Importer for GRIDROAM!
    /// Drop any commercial-safe Mixamo rigged character FBX (+ optional Idle/Walk/Run/Punch/Crouch/Roll/Jump FBX clips)
    /// into Assets/GRIDROAM/Art/Mixamo/ and click:
    ///   GRIDROAM -> Karakterler -> Mixamo Karakteri Otomatik Bağla (Deniz)
    /// Automatically configures Humanoid Rig, builds the AnimatorController wired to PlayerFootController,
    /// and mounts the character cleanly onto Player_GRIDROAM.prefab!
    /// </summary>
    public static class MixamoCharacterImporterTool
    {
        private const string MixamoFolder = "Assets/GRIDROAM/Art/Mixamo";
        private const string ControllerPath = "Assets/GRIDROAM/Art/Mixamo/Mixamo_Deniz_Controller.controller";
        private const string PlayerPrefabPath = "Assets/GRIDROAM/Prefabs/Player_GRIDROAM.prefab";

        [MenuItem("GRIDROAM/Karakterler/1. Mixamo Klasörünü Oluştur")]
        public static void CreateMixamoFolder()
        {
            if (!Directory.Exists(MixamoFolder))
            {
                Directory.CreateDirectory(MixamoFolder);
                AssetDatabase.Refresh();
            }
            UnityEngine.Object folderObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(MixamoFolder);
            if (folderObj != null) EditorGUIUtility.PingObject(folderObj);
            EditorUtility.DisplayDialog(
                "Mixamo Klasörü Hazır",
                "Mixamo.com üzerinden ücretsiz indirdiğin Rigged Karakter (.fbx, T-Pose) ve animasyon dosyalarını " +
                "(Idle.fbx, Walk.fbx, Run.fbx, Punch.fbx, Crouch.fbx, Roll.fbx, Jump.fbx) şu klasöre bırak:\n\n" +
                MixamoFolder + "\n\nArdından 'GRIDROAM -> Karakterler -> 2. Mixamo Karakteri Otomatik Bağla (Deniz)' menüsüne tıkla!",
                "Tamam");
        }

        [MenuItem("GRIDROAM/Karakterler/2. Mixamo Karakteri Otomatik Bağla (Deniz)")]
        public static void AutoWireMixamoCharacterToPlayer()
        {
            if (!Directory.Exists(MixamoFolder))
            {
                CreateMixamoFolder();
                return;
            }

            string[] fbxGuids = AssetDatabase.FindAssets("t:Model", new[] { MixamoFolder });
            if (fbxGuids == null || fbxGuids.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Mixamo FBX Bulunamadı",
                    $"'{MixamoFolder}' klasöründe henüz .fbx dosyası yok.\n\nMixamo'dan en az 1 karakter (.fbx, With Skin) ve opsiyonel animasyon klipleri indirip bu klasöre kopyala.",
                    "Tamam");
                return;
            }

            GameObject characterModel = null;
            AnimationClip idleClip = null, walkClip = null, runClip = null, punchClip = null, crouchClip = null, rollClip = null, jumpClip = null;

            // 1. Configure every FBX in MixamoFolder as Humanoid Rig & extract clips
            for (int i = 0; i < fbxGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(fbxGuids[i]);
                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer != null && importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.SaveAndReimport();
                }

                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (model != null && characterModel == null && model.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
                {
                    characterModel = model;
                }

                string low = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                UnityEngine.Object[] subs = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int s = 0; s < subs.Length; s++)
                {
                    AnimationClip clip = subs[s] as AnimationClip;
                    if (clip == null || clip.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase)) continue;
                    string cl = (low + "_" + clip.name).ToLowerInvariant();

                    if (idleClip == null && cl.Contains("idle")) idleClip = clip;
                    else if (runClip == null && (cl.Contains("run") || cl.Contains("sprint"))) runClip = clip;
                    else if (walkClip == null && cl.Contains("walk")) walkClip = clip;
                    else if (punchClip == null && (cl.Contains("punch") || cl.Contains("hook") || cl.Contains("box"))) punchClip = clip;
                    else if (crouchClip == null && cl.Contains("crouch")) crouchClip = clip;
                    else if (rollClip == null && (cl.Contains("roll") || cl.Contains("dodge"))) rollClip = clip;
                    else if (jumpClip == null && cl.Contains("jump")) jumpClip = clip;
                }
            }

            if (characterModel == null)
            {
                EditorUtility.DisplayDialog(
                    "Skinned Karakter Bulunamadı",
                    "Mixamo klasöründe 'With Skin' olarak indirilmiş bir karakter modeli bulunamadı.",
                    "Tamam");
                return;
            }

            // 2. Build clean AnimatorController matching PlayerFootController's exact parameters!
            AnimatorController ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("IsSwimming", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Punch", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("DodgeRoll", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("HitReact", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine sm = ctrl.layers[0].stateMachine;
            AnimatorState locoState = sm.AddState("Locomotion");
            sm.defaultState = locoState;

            BlendTree locoTree = new BlendTree { name = "LocomotionBlend", blendParameter = "Speed", blendType = BlendTreeType.Simple1D };
            AssetDatabase.AddObjectToAsset(locoTree, ctrl);
            if (idleClip != null) locoTree.AddChild(idleClip, 0.0f);
            if (walkClip != null) locoTree.AddChild(walkClip, 2.25f);
            if (runClip != null)  locoTree.AddChild(runClip, 4.25f);
            locoState.motion = locoTree;

            WireOptionalTriggerState(sm, locoState, "Punch", punchClip);
            WireOptionalTriggerState(sm, locoState, "DodgeRoll", rollClip);
            WireOptionalTriggerState(sm, locoState, "Jump", jumpClip);
            if (crouchClip != null)
            {
                AnimatorState crouchState = sm.AddState("Crouch");
                crouchState.motion = crouchClip;
                AnimatorStateTransition toCrouch = locoState.AddTransition(crouchState);
                toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "IsCrouching");
                toCrouch.hasExitTime = false;
                toCrouch.duration = 0.14f;
                AnimatorStateTransition toStand = crouchState.AddTransition(locoState);
                toStand.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsCrouching");
                toStand.hasExitTime = false;
                toStand.duration = 0.14f;
            }

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // 3. Mount Mixamo character cleanly onto Player_GRIDROAM.prefab
            if (!File.Exists(PlayerPrefabPath))
            {
                EditorUtility.DisplayDialog("Player Prefab Bulunamadı", $"Prefab bulunamadı: {PlayerPrefabPath}", "Tamam");
                return;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                PlayerFootController foot = prefabRoot.GetComponent<PlayerFootController>();
                if (foot == null) throw new InvalidOperationException("PlayerFootController missing on Player_GRIDROAM.prefab!");

                Transform oldMixamo = prefabRoot.transform.Find("MixamoVisualRoot");
                if (oldMixamo != null) UnityEngine.Object.DestroyImmediate(oldMixamo.gameObject);

                GameObject mixRoot = new GameObject("MixamoVisualRoot");
                mixRoot.transform.SetParent(prefabRoot.transform, false);

                GameObject charInst = (GameObject)PrefabUtility.InstantiatePrefab(characterModel, mixRoot.transform);
                charInst.name = "Mixamo_Deniz_Rig";
                charInst.transform.localPosition = Vector3.zero;
                charInst.transform.localRotation = Quaternion.identity;

                Animator anim = charInst.GetComponent<Animator>();
                if (anim == null) anim = charInst.AddComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;

                // Keep ProceduralHumanoid hidden as fallback, switch visualRoot + characterAnimator to Mixamo!
                Transform oldVis = prefabRoot.transform.Find("VisualRoot");
                if (oldVis != null) oldVis.gameObject.SetActive(false);

                foot.ConfigureVisuals(mixRoot.transform, anim, foot.Humanoid);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Mixamo Karakter Bağlandı!",
                $"'{characterModel.name}' başarıyla Player_GRIDROAM prefab'ına bağlandı ve '{ControllerPath}' oluşturuldu!",
                "Harika!");
        }

        private static void WireOptionalTriggerState(AnimatorStateMachine sm, AnimatorState returnState, string triggerName, AnimationClip clip)
        {
            if (clip == null) return;
            AnimatorState st = sm.AddState(triggerName);
            st.motion = clip;
            AnimatorStateTransition enter = sm.AddAnyStateTransition(st);
            enter.AddCondition(AnimatorConditionMode.If, 0f, triggerName);
            enter.hasExitTime = false;
            enter.duration = 0.08f;

            AnimatorStateTransition exit = st.AddTransition(returnState);
            exit.hasExitTime = true;
            exit.exitTime = 0.85f;
            exit.duration = 0.14f;
        }
    }
}
#endif
