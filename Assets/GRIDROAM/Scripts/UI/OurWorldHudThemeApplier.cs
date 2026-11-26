using System.Collections;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// Unifies the entire in-game HUD and scene-baked modals (Canvas_GRIDROAM_HUD: Minimap, Mission Card,
    /// Status Bar, Interact Pill, Dialogue Overlay, Phone/Map Guide, Shop Counter, Paint Palette,
    /// Mission Complete/Failed modals, Speedometer, and Touch Controls) with the flagship
    /// WhatsNewOverlay / OurWorldUiKit design system (Istanbul Night Slate + Turquoise + Golden Amber,
    /// anti-aliased SDF rounded corners, soft drop shadows, glass hairline borders & top accent glows).
    /// </summary>
    public sealed class OurWorldHudThemeApplier : MonoBehaviour
    {
        private bool _applied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<OurWorldHudThemeApplier>() != null) return;
            GameHUDController hud = FindAnyObjectByType<GameHUDController>();
            if (hud == null) return;

            GameObject go = new GameObject("OurWorld_HudThemeApplier");
            go.transform.SetParent(hud.transform, false);
            go.AddComponent<OurWorldHudThemeApplier>();
        }

        private IEnumerator Start()
        {
            // Wait 1 frame so PhonePanelController.BuildFeedbackTab(), HudIconSkin & ResponsiveTabletLayout finish mounting
            yield return null;
            ApplyFlagshipTheme();
        }

        public void ApplyFlagshipTheme()
        {
            if (_applied) return;
            _applied = true;

            GameHUDController hud = FindAnyObjectByType<GameHUDController>();
            if (hud == null) return;

            OurWorldUiKit.EnsureInitialized();
            Transform root = hud.transform;

            // 1. TOP-LEFT: Minimap, Phone Button, Mission Objective Card
            StyleMinimap(root);
            StylePillControl(FindChild(root, "Btn_Phone"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary, addShadow: true);
            StyleElevatedCard(FindChild(root, "MissionBox"), OurWorldUiKit.CardBg, OurWorldUiKit.AccentTeal, glowHeight: 86f, glowAlpha: 0.13f, shadowSpread: 18f);
            StyleTextColor(FindChild(root, "MissionTitle"), OurWorldUiKit.AccentTeal);
            StyleTextColor(FindChild(root, "MissionObjective"), OurWorldUiKit.TextPrimary);
            StyleTextColor(FindChild(root, "JobObjective"), OurWorldUiKit.AccentGold);
            StylePillControl(FindChild(root, "Btn_CancelJob"), new Color(0.36f, 0.14f, 0.16f, 0.94f), new Color(OurWorldUiKit.AccentCoral.r, OurWorldUiKit.AccentCoral.g, OurWorldUiKit.AccentCoral.b, 0.40f), OurWorldUiKit.TextPrimary);

            // 2. TOP-CENTRE: Compass Waypoint Pill, Timer, Tail Warning
            StylePillControl(FindChild(root, "WaypointIndicator"), OurWorldUiKit.CardBg, new Color(OurWorldUiKit.AccentTeal.r, OurWorldUiKit.AccentTeal.g, OurWorldUiKit.AccentTeal.b, 0.32f), null, addShadow: true);
            StyleTextColor(FindChild(root, "WaypointIndicator/Arrow"), OurWorldUiKit.AccentTeal);
            StyleTextColor(FindChild(root, "WaypointIndicator/Distance"), OurWorldUiKit.TextPrimary);
            StylePillControl(FindChild(root, "TimerBox"), OurWorldUiKit.CardBg, OurWorldUiKit.BorderSubtle, null, addShadow: true);
            StylePillControl(FindChild(root, "TailWarningBox"), OurWorldUiKit.CardBg, OurWorldUiKit.BorderSubtle, null, addShadow: true);

            // 3. TOP-RIGHT: Status Card (Clock, Money, Energy Bar, Tired Badge, Utility)
            Transform statusCard = FindChild(root, "StatusCard_TopRight");
            StyleElevatedCard(statusCard, OurWorldUiKit.CardBg, OurWorldUiKit.AccentGold, glowHeight: 54f, glowAlpha: 0.11f, shadowSpread: 16f, useSubCard: true);
            StyleTextColor(FindChild(root, "ClockText"), OurWorldUiKit.TextSecondary);
            StyleTextColor(FindChild(root, "MoneyText"), OurWorldUiKit.AccentGold);
            StyleTextColor(FindChild(root, "MoneyDeltaPop"), OurWorldUiKit.AccentTeal);
            ApplyRoundedOnly(FindChild(root, "EnergyTrack"), OurWorldUiKit.PillSprite, new Color(1f, 1f, 1f, 0.11f));
            ApplyRoundedOnly(FindChild(root, "EnergyFill"), OurWorldUiKit.PillSprite, OurWorldUiKit.AccentTeal, preserveImageType: true);
            StylePillControl(FindChild(root, "TiredBadge"), OurWorldUiKit.AccentCoral, Color.clear, OurWorldUiKit.InkDark, addShadow: true);
            StylePillControl(FindChild(root, "Btn_GraphicsTier"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StylePillControl(FindChild(root, "Btn_ResetSave"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StylePillControl(FindChild(root, "TelemetryPanel"), OurWorldUiKit.CardBg, OurWorldUiKit.BorderSubtle, null);

            // 4. SHARED INTERACT PILL & STATUS TOAST
            StylePillControl(FindChild(root, "Btn_Interact"), OurWorldUiKit.AccentTeal, new Color(1f, 1f, 1f, 0.28f), OurWorldUiKit.InkDark, addShadow: true);
            Transform toastTf = FindChild(root, "StatusToast");
            if (toastTf != null)
            {
                StyleTextColor(toastTf, OurWorldUiKit.TextPrimary);
                if (toastTf.GetComponent<Shadow>() == null)
                {
                    Shadow sh = toastTf.gameObject.AddComponent<Shadow>();
                    sh.effectColor = new Color(0f, 0f, 0f, 0.72f);
                    sh.effectDistance = new Vector2(0f, -2f);
                }
            }

            // 5. TOUCH CONTROLS & SPEEDOMETER
            StyleCircularControl(FindChild(root, "Btn_Sprint"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StyleCircularControl(FindChild(root, "Btn_Attack"), new Color(0.56f, 0.19f, 0.20f, 0.90f), new Color(OurWorldUiKit.AccentCoral.r, OurWorldUiKit.AccentCoral.g, OurWorldUiKit.AccentCoral.b, 0.45f), OurWorldUiKit.TextPrimary);
            StyleCircularControl(FindChild(root, "Btn_Jump"), OurWorldUiKit.AccentTeal, new Color(1f, 1f, 1f, 0.28f), OurWorldUiKit.InkDark);

            StyleSubCardControl(FindChild(root, "Btn_SteerLeft"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StyleSubCardControl(FindChild(root, "Btn_SteerRight"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StyleSubCardControl(FindChild(root, "Btn_Brake"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StyleSubCardControl(FindChild(root, "Btn_Gas"), OurWorldUiKit.AccentTeal, new Color(1f, 1f, 1f, 0.25f), OurWorldUiKit.InkDark);
            StyleSubCardControl(FindChild(root, "Btn_Handbrake"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StyleSubCardControl(FindChild(root, "Btn_ExitCar"), new Color(0.32f, 0.16f, 0.20f, 0.92f), new Color(OurWorldUiKit.AccentCoral.r, OurWorldUiKit.AccentCoral.g, OurWorldUiKit.AccentCoral.b, 0.35f), OurWorldUiKit.TextPrimary);

            Transform speedDisc = FindChild(root, "SpeedDisc");
            if (speedDisc != null)
            {
                ApplyRoundedOnly(speedDisc, OurWorldUiKit.CircleSprite, OurWorldUiKit.CardBg);
                RemoveLegacyShadow(speedDisc);
                EnsureHairlineBorder(speedDisc, OurWorldUiKit.CircleOutlineSprite, OurWorldUiKit.BorderSubtle);
                OurWorldUiKit.AddCardDropShadow(speedDisc, spread: 18f, offsetY: -8f, alpha: 0.48f);
                StyleTextColor(FindChild(speedDisc, "SpeedText"), OurWorldUiKit.TextPrimary);
                StyleTextColor(FindChild(speedDisc, "SpeedUnit"), OurWorldUiKit.TextMuted);
                StyleTextColor(FindChild(speedDisc, "GearText"), OurWorldUiKit.AccentGold);
            }
            ApplyRoundedOnly(FindChild(root, "FuelBar_Track"), OurWorldUiKit.PillSprite, OurWorldUiKit.CardBg);
            ApplyRoundedOnly(FindChild(root, "FuelBar_Fill"), OurWorldUiKit.PillSprite, OurWorldUiKit.AccentTeal, preserveImageType: true);

            // 6. STORY DIALOGUE PANEL
            Transform dlg = FindChild(root, "DialoguePanel");
            StyleElevatedCard(dlg, OurWorldUiKit.CardBg, OurWorldUiKit.AccentTeal, glowHeight: 110f, glowAlpha: 0.15f, shadowSpread: 24f);
            StyleTextColor(FindChild(dlg, "SpeakerName"), OurWorldUiKit.AccentTeal);
            StyleTextColor(FindChild(dlg, "DialogueBody"), OurWorldUiKit.TextPrimary);
            StylePillControl(FindChild(dlg, "Btn_DialogueContinue"), OurWorldUiKit.AccentTeal, Color.clear, OurWorldUiKit.InkDark, addShadow: false);

            // 7. MISSION FAILED & MISSION COMPLETE OVERLAYS
            Transform failPanel = FindChild(root, "MissionFailedPanel");
            StyleElevatedCard(failPanel, OurWorldUiKit.CardBg, OurWorldUiKit.AccentCoral, glowHeight: 130f, glowAlpha: 0.16f, shadowSpread: 26f);
            StyleTextColor(FindChild(failPanel, "FailedTitle"), OurWorldUiKit.AccentCoral);
            StyleTextColor(FindChild(failPanel, "FailedReason"), OurWorldUiKit.TextSecondary);
            StylePillControl(FindChild(failPanel, "Btn_RetryMission"), OurWorldUiKit.AccentTeal, Color.clear, OurWorldUiKit.InkDark);

            Transform bannerPanel = FindChild(root, "MissionCompleteBanner");
            StyleElevatedCard(bannerPanel, OurWorldUiKit.CardBg, OurWorldUiKit.AccentGold, glowHeight: 96f, glowAlpha: 0.18f, shadowSpread: 24f,
                borderOverride: new Color(OurWorldUiKit.AccentGold.r, OurWorldUiKit.AccentGold.g, OurWorldUiKit.AccentGold.b, 0.36f));
            StyleTextColor(FindChild(bannerPanel, "BannerText"), OurWorldUiKit.AccentGold);

            // 8. PAINT PALETTE MODAL
            Transform paintPanel = FindChild(root, "PaintPalettePanel");
            StyleElevatedCard(paintPanel, OurWorldUiKit.CardBg, OurWorldUiKit.AccentTeal, glowHeight: 140f, glowAlpha: 0.14f, shadowSpread: 26f);
            StyleTextColor(FindChild(paintPanel, "PaintTitle"), OurWorldUiKit.TextPrimary);
            StyleTextColor(FindChild(paintPanel, "PaintJobHint"), OurWorldUiKit.AccentGold);
            for (int i = 1; i <= 8; i++)
            {
                Transform sw = FindChild(paintPanel, $"Btn_PaintSwatch_{i}");
                if (sw == null) continue;
                Image img = sw.GetComponent<Image>();
                if (img != null) OurWorldUiKit.ApplyRounded(img, OurWorldUiKit.SubCardSprite, img.color);
                RemoveLegacyShadow(sw);
                EnsureHairlineBorder(sw, OurWorldUiKit.SubCardOutlineSprite, new Color(1f, 1f, 1f, 0.22f));
            }
            StylePillControl(FindChild(paintPanel, "Btn_PaintCancel"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextSecondary);

            // 9. SHOP COUNTER MODAL
            Transform shopPanel = FindChild(root, "ShopCounterPanel");
            StyleElevatedCard(shopPanel, OurWorldUiKit.CardBg, OurWorldUiKit.AccentGold, glowHeight: 150f, glowAlpha: 0.14f, shadowSpread: 26f);
            StyleTextColor(FindChild(shopPanel, "ShopTitle"), OurWorldUiKit.TextPrimary);
            StyleTextColor(FindChild(shopPanel, "ShopBalanceStatus"), OurWorldUiKit.AccentGold);
            for (int i = 1; i <= 5; i++)
            {
                StyleSubCardControl(FindChild(shopPanel, $"Btn_ShopItem_{i}"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            }
            StylePillControl(FindChild(shopPanel, "Btn_ShopClose"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextSecondary);

            // 10. PHONE / TOWN GUIDE MODAL (Map, Jobs, Stats, Ideas!)
            StylePhoneModal(FindChild(root, "PhonePanel"));
        }

        private static void StyleMinimap(Transform root)
        {
            Transform frame = FindChild(root, "Minimap_Container");
            if (frame == null) return;
            ApplyRoundedOnly(frame, OurWorldUiKit.CircleSprite, OurWorldUiKit.CardBg);
            RemoveLegacyShadow(frame);
            OurWorldUiKit.AddCardDropShadow(frame, spread: 18f, offsetY: -8f, alpha: 0.50f);

            Transform rim = FindChild(frame, "Minimap_RingBorder");
            if (rim != null)
            {
                Image rimImg = rim.GetComponent<Image>();
                if (rimImg != null)
                {
                    rimImg.sprite = OurWorldUiKit.CircleOutlineSprite;
                    rimImg.type = Image.Type.Simple;
                    rimImg.color = new Color(OurWorldUiKit.AccentTeal.r, OurWorldUiKit.AccentTeal.g, OurWorldUiKit.AccentTeal.b, 0.45f);
                }
            }
        }

        private static void StylePhoneModal(Transform phonePanel)
        {
            if (phonePanel == null) return;

            StyleElevatedCard(phonePanel, OurWorldUiKit.CardBg, OurWorldUiKit.AccentTeal, glowHeight: 190f, glowAlpha: 0.14f, shadowSpread: 30f);
            StyleTextColor(FindChild(phonePanel, "PhoneHeaderTitle"), OurWorldUiKit.TextPrimary);

            // Subtle header divider line matching WhatsNewOverlay
            if (phonePanel.Find("HeaderDivider") == null)
            {
                Image div = OurWorldUiKit.AddDividerLine(phonePanel,
                    new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(34f, -84f), new Vector2(-34f, -82.5f),
                    OurWorldUiKit.Divider);
                div.gameObject.name = "HeaderDivider";
            }

            string[] tabNames = { "Btn_Tab_Map", "Btn_Tab_Jobs", "Btn_Tab_Stats", "Btn_Tab_Feedback" };
            for (int i = 0; i < tabNames.Length; i++)
            {
                Transform tab = FindChild(phonePanel, tabNames[i]);
                if (tab == null) continue;
                bool isFirst = (i == 0);
                StylePillControl(tab,
                    isFirst ? OurWorldUiKit.AccentTeal : OurWorldUiKit.SurfaceBg,
                    isFirst ? Color.clear : OurWorldUiKit.BorderSubtle,
                    isFirst ? OurWorldUiKit.InkDark : OurWorldUiKit.TextSecondary);
            }

            StylePillControl(FindChild(phonePanel, "Btn_LanguageToggle"), OurWorldUiKit.SurfaceBg, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);
            StylePillControl(FindChild(phonePanel, "Btn_PhoneClose"), new Color(0.16f, 0.20f, 0.28f, 0.94f), OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextSecondary);

            // Map tab cards
            Transform mapFrame = FindChild(phonePanel, "MapViewportFrame");
            StyleElevatedCard(mapFrame, OurWorldUiKit.SurfaceBg, Color.clear, glowHeight: 0f, glowAlpha: 0f, shadowSpread: 0f, useSubCard: true);

            Transform sidebar = FindChild(phonePanel, "MapSidebarCard");
            StyleElevatedCard(sidebar, OurWorldUiKit.SurfaceBg, OurWorldUiKit.AccentTeal, glowHeight: 90f, glowAlpha: 0.10f, shadowSpread: 0f, useSubCard: true);
            StyleTextColor(FindChild(sidebar, "MapHint"), OurWorldUiKit.AccentTeal);
            StyleTextColor(FindChild(sidebar, "MapLegend"), OurWorldUiKit.TextSecondary);
            StylePillControl(FindChild(sidebar, "Btn_ClearWaypoint"), OurWorldUiKit.SurfaceSoft, OurWorldUiKit.BorderSubtle, OurWorldUiKit.TextPrimary);

            // Jobs tab rows
            for (int i = 1; i <= 4; i++)
            {
                Transform row = FindChild(phonePanel, $"JobRow_{i}");
                StyleElevatedCard(row, OurWorldUiKit.SurfaceBg, Color.clear, glowHeight: 0f, glowAlpha: 0f, shadowSpread: 0f, useSubCard: true);
                StyleTextColor(FindChild(row, "JobTitle"), OurWorldUiKit.TextPrimary);
                StyleTextColor(FindChild(row, "JobDesc"), OurWorldUiKit.TextSecondary);
                StylePillControl(FindChild(row, $"Btn_PinJob_{i}"), OurWorldUiKit.AccentTeal, Color.clear, OurWorldUiKit.InkDark);
            }
            StylePillControl(FindChild(phonePanel, "Btn_CancelActiveJob_Phone"), OurWorldUiKit.AccentRed, Color.clear, OurWorldUiKit.TextPrimary);

            // Stats tab rows
            for (int i = 1; i <= 7; i++)
            {
                Transform row = FindChild(phonePanel, $"StatRow_{i}");
                StyleElevatedCard(row, OurWorldUiKit.SurfaceBg, Color.clear, glowHeight: 0f, glowAlpha: 0f, shadowSpread: 0f, useSubCard: true);
                StyleTextColor(FindChild(row, "StatName"), OurWorldUiKit.TextSecondary);
                StyleTextColor(FindChild(row, "StatValue"), OurWorldUiKit.AccentGold);
            }

            // Re-sync active tab tint via PhonePanelController
            PhonePanelController phone = FindAnyObjectByType<PhonePanelController>();
            if (phone != null)
            {
                phone.SelectTab(0);
            }
        }

        private static void StyleElevatedCard(
            Transform tf,
            Color bgColor,
            Color glowAccent,
            float glowHeight,
            float glowAlpha,
            float shadowSpread,
            bool useSubCard = false,
            Color? borderOverride = null)
        {
            if (tf == null) return;
            Image img = tf.GetComponent<Image>();
            if (img != null)
            {
                OurWorldUiKit.ApplyRounded(img, useSubCard ? OurWorldUiKit.SubCardSprite : OurWorldUiKit.CardSprite, bgColor);
            }
            RemoveLegacyShadow(tf);

            if (shadowSpread > 0.5f && tf.Find("DropShadow") == null)
            {
                OurWorldUiKit.AddCardDropShadow(tf, spread: shadowSpread, offsetY: -Mathf.Round(shadowSpread * 0.45f), alpha: 0.52f);
            }

            if (glowHeight > 1f && glowAlpha > 0.005f && tf.Find("TopAccentGlow") == null)
            {
                Image glow = OurWorldUiKit.AddSoftTopGlow(tf, glowAccent, heightPx: glowHeight, alpha: glowAlpha);
                // Place right above DropShadow (index 1) so all text and buttons stay cleanly in front
                int targetSibling = tf.Find("DropShadow") != null ? 1 : 0;
                glow.transform.SetSiblingIndex(targetSibling);
            }

            EnsureHairlineBorder(tf,
                useSubCard ? OurWorldUiKit.SubCardOutlineSprite : OurWorldUiKit.CardOutlineSprite,
                borderOverride ?? OurWorldUiKit.BorderSubtle);
        }

        private static void StylePillControl(Transform tf, Color bgColor, Color borderColor, Color? labelColor, bool addShadow = false)
        {
            if (tf == null) return;
            Image img = tf.GetComponent<Image>();
            if (img != null) OurWorldUiKit.ApplyRounded(img, OurWorldUiKit.PillSprite, bgColor);
            RemoveLegacyShadow(tf);

            if (addShadow && tf.Find("DropShadow") == null)
            {
                OurWorldUiKit.AddCardDropShadow(tf, spread: 14f, offsetY: -6f, alpha: 0.42f);
            }

            if (borderColor.a > 0.005f)
            {
                EnsureHairlineBorder(tf, OurWorldUiKit.PillOutlineSprite, borderColor);
            }

            SyncButtonFeedbackAndLabel(tf, img, bgColor, labelColor);
        }

        private static void StyleSubCardControl(Transform tf, Color bgColor, Color borderColor, Color? labelColor)
        {
            if (tf == null) return;
            Image img = tf.GetComponent<Image>();
            if (img != null) OurWorldUiKit.ApplyRounded(img, OurWorldUiKit.SubCardSprite, bgColor);
            RemoveLegacyShadow(tf);

            if (borderColor.a > 0.005f)
            {
                EnsureHairlineBorder(tf, OurWorldUiKit.SubCardOutlineSprite, borderColor);
            }

            SyncButtonFeedbackAndLabel(tf, img, bgColor, labelColor);
        }

        private static void StyleCircularControl(Transform tf, Color bgColor, Color borderColor, Color? labelColor)
        {
            if (tf == null) return;
            Image img = tf.GetComponent<Image>();
            if (img != null) OurWorldUiKit.ApplyRounded(img, OurWorldUiKit.CircleSprite, bgColor);
            RemoveLegacyShadow(tf);

            if (borderColor.a > 0.005f)
            {
                EnsureHairlineBorder(tf, OurWorldUiKit.CircleOutlineSprite, borderColor);
            }

            SyncButtonFeedbackAndLabel(tf, img, bgColor, labelColor);
        }

        private static void SyncButtonFeedbackAndLabel(Transform tf, Image bgImg, Color bgColor, Color? labelColor)
        {
            if (labelColor.HasValue)
            {
                Text lbl = tf.GetComponentInChildren<Text>(true);
                if (lbl != null) lbl.color = labelColor.Value;

                Transform hudIcon = tf.Find("HudIcon");
                if (hudIcon != null)
                {
                    Image iconImg = hudIcon.GetComponent<Image>();
                    if (iconImg != null) iconImg.color = labelColor.Value;
                }
            }

            Button btn = tf.GetComponent<Button>();
            UiButtonFeedback fb = tf.GetComponent<UiButtonFeedback>();
            if (btn != null && fb != null && bgImg != null)
            {
                fb.Configure(btn, bgImg);
            }

            HoldButton hold = tf.GetComponent<HoldButton>();
            if (hold != null && bgImg != null)
            {
                Color pressed = Color.Lerp(bgColor, OurWorldUiKit.AccentTeal, 0.55f);
                pressed.a = 0.96f;
                hold.ConfigureVisual(bgImg, bgColor, pressed, releaseOnExit: false);
            }
        }

        private static void ApplyRoundedOnly(Transform tf, Sprite sprite, Color color, bool preserveImageType = false)
        {
            if (tf == null) return;
            Image img = tf.GetComponent<Image>();
            if (img == null) return;
            Image.Type prevType = img.type;
            OurWorldUiKit.ApplyRounded(img, sprite, color);
            if (preserveImageType) img.type = prevType;
        }

        private static void EnsureHairlineBorder(Transform tf, Sprite outlineSprite, Color color)
        {
            if (tf == null) return;
            Transform existing = tf.Find("HairlineBorder");
            if (existing != null)
            {
                Image img = existing.GetComponent<Image>();
                if (img != null) img.color = color;
                return;
            }
            OurWorldUiKit.AddHairlineBorder(tf, outlineSprite, color);
        }

        private static void RemoveLegacyShadow(Transform tf)
        {
            if (tf == null) return;
            Shadow sh = tf.GetComponent<Shadow>();
            if (sh != null && !(sh is Outline))
            {
                Destroy(sh);
            }
        }

        private static void StyleTextColor(Transform tf, Color color)
        {
            if (tf == null) return;
            Text t = tf.GetComponent<Text>();
            if (t != null) t.color = color;
        }

        private static Transform FindChild(Transform root, string nameOrPath)
        {
            if (root == null || string.IsNullOrEmpty(nameOrPath)) return null;
            Transform direct = root.Find(nameOrPath);
            if (direct != null) return direct;

            // Search recursively for a single name or relative suffix path
            string[] parts = nameOrPath.Split('/');
            string leaf = parts[parts.Length - 1];
            string parentName = parts.Length >= 2 ? parts[parts.Length - 2] : null;

            Transform[] all = root.GetComponentsInChildren<Transform>(includeInactive: true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null || t.name != leaf) continue;
                if (parentName != null && (t.parent == null || t.parent.name != parentName)) continue;
                return t;
            }
            return null;
        }
    }
}
