using System.Collections.Generic;
using System.Text;
using OurWorld.Core;
using UnityEngine;
using UnityEngine.UI;

namespace OurWorld.UI
{
    /// <summary>
    /// "Öneri / Mesajlar" tab inside the phone. Built at runtime so the scene does not need regenerating.
    /// Left column: nickname, category, message composer -> writes to Firestore `feedback`.
    /// Right column: live Firestore community feed displaying recent player ideas & messages.
    /// </summary>
    public class FeedbackTabView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.11f, 0.14f, 0.19f, 0.86f);
        private static readonly Color InputColor = new Color(0.07f, 0.09f, 0.12f, 0.96f);
        private static readonly Color OffWhite = new Color(0.93f, 0.94f, 0.95f, 1f);
        private static readonly Color Muted = new Color(0.66f, 0.71f, 0.76f, 1f);
        private static readonly Color Accent = new Color(0.36f, 0.68f, 0.65f, 0.95f);
        private static readonly Color Inactive = new Color(0.16f, 0.20f, 0.26f, 0.90f);

        private static readonly string[] CategoryIds = { "idea", "bug", "other" };
        private static readonly string[] CategoryKeys = { "feedback.cat.idea", "feedback.cat.bug", "feedback.cat.other" };

        private Font _font;
        private Sprite _sprite;
        private Text _title;
        private Text _info;
        private Text _privacy;
        private Text _status;
        private Text _placeholder;
        private Text _namePlaceholder;
        private Text _sendLabel;
        private Text _counter;
        private Text _feedTitle;
        private Text _feedText;
        private Text _refreshLabel;
        private InputField _nameInput;
        private InputField _input;
        private Button _sendButton;
        private Button _refreshButton;
        private readonly Button[] _catButtons = new Button[3];
        private readonly Text[] _catLabels = new Text[3];
        private int _selectedCategory;
        private bool _sending;
        private bool _loadingFeed;

        public static FeedbackTabView Create(Transform phonePanel, Font font, Sprite roundedSprite)
        {
            GameObject root = new GameObject("Tab_Feedback", typeof(RectTransform));
            root.transform.SetParent(phonePanel, false);
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(30f, 24f);
            rt.offsetMax = new Vector2(-30f, -88f);

            FeedbackTabView view = root.AddComponent<FeedbackTabView>();
            view._font = font;
            view._sprite = roundedSprite;
            view.Build();
            root.SetActive(false);
            return view;
        }

        private void Build()
        {
            _title = MakeText("Title", transform, 28, FontStyle.Bold, OffWhite, TextAnchor.MiddleCenter, new Vector2(0f, -4f), new Vector2(1140f, 40f));
            _info = MakeText("Info", transform, 19, FontStyle.Normal, Muted, TextAnchor.MiddleCenter, new Vector2(0f, -44f), new Vector2(1140f, 32f));

            // --- LEFT COLUMN: Message Composer ---
            const float leftX = -290f;

            // Nickname field
            GameObject nameGo = MakePanel("NameInput", transform, new Vector2(leftX, -88f), new Vector2(540f, 52f), InputColor);
            _nameInput = nameGo.AddComponent<InputField>();
            _nameInput.targetGraphic = nameGo.GetComponent<Image>();
            Text nameText = MakeStretchText("Text", nameGo.transform, 21, FontStyle.Normal, OffWhite);
            nameText.supportRichText = false;
            _namePlaceholder = MakeStretchText("Placeholder", nameGo.transform, 21, FontStyle.Italic, new Color(Muted.r, Muted.g, Muted.b, 0.7f));
            _nameInput.textComponent = nameText;
            _nameInput.placeholder = _namePlaceholder;
            _nameInput.characterLimit = FeedbackService.MaxSenderNameLength;
            if (FeedbackService.Instance != null)
            {
                _nameInput.text = FeedbackService.Instance.SavedSenderName;
            }

            // Category buttons
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                (Button b, Text l) = MakeButton($"Btn_Cat_{CategoryIds[i]}", transform, new Vector2(leftX + (i - 1) * 182f, -152f), new Vector2(172f, 48f), 20);
                b.onClick.AddListener(() => SelectCategory(idx));
                _catButtons[i] = b;
                _catLabels[i] = l;
            }

            // Multiline message input
            GameObject inputGo = MakePanel("FeedbackInput", transform, new Vector2(leftX, -214f), new Vector2(540f, 250f), InputColor);
            _input = inputGo.AddComponent<InputField>();
            _input.targetGraphic = inputGo.GetComponent<Image>();

            Text inputText = MakeStretchText("Text", inputGo.transform, 22, FontStyle.Normal, OffWhite);
            inputText.supportRichText = false;
            _placeholder = MakeStretchText("Placeholder", inputGo.transform, 22, FontStyle.Italic, new Color(Muted.r, Muted.g, Muted.b, 0.7f));

            _input.textComponent = inputText;
            _input.placeholder = _placeholder;
            _input.lineType = InputField.LineType.MultiLineNewline;
            _input.characterLimit = FeedbackService.MaxLength;
            _input.onValueChanged.AddListener(_ => UpdateCounter());

            _counter = MakeText("Counter", transform, 16, FontStyle.Normal, Muted, TextAnchor.MiddleRight, new Vector2(leftX, -468f), new Vector2(540f, 24f));

            (Button send, Text sendLbl) = MakeButton("Btn_SendFeedback", transform, new Vector2(leftX, -502f), new Vector2(280f, 58f), 24);
            send.GetComponent<Image>().color = Accent;
            send.onClick.AddListener(Send);
            _sendButton = send;
            _sendLabel = sendLbl;

            _status = MakeText("Status", transform, 19, FontStyle.Bold, OffWhite, TextAnchor.MiddleCenter, new Vector2(leftX, -570f), new Vector2(540f, 34f));
            _privacy = MakeText("Privacy", transform, 15, FontStyle.Normal, Muted, TextAnchor.MiddleCenter, new Vector2(leftX, -608f), new Vector2(540f, 28f));

            // --- RIGHT COLUMN: Live Community Messages (Firestore) ---
            const float rightX = 290f;
            GameObject feedPanel = MakePanel("CommunityFeedPanel", transform, new Vector2(rightX, -88f), new Vector2(550f, 540f), PanelColor);

            _feedTitle = MakeText("FeedTitle", feedPanel.transform, 21, FontStyle.Bold, OffWhite, TextAnchor.MiddleLeft, new Vector2(-118f, -14f), new Vector2(280f, 38f));

            (Button whatsNewBtn, Text whatsNewLbl) = MakeButton("Btn_WhatsNew", feedPanel.transform, new Vector2(82f, -12f), new Vector2(118f, 40f), 16);
            whatsNewBtn.GetComponent<Image>().color = new Color(0.24f, 0.32f, 0.44f, 0.96f);
            whatsNewLbl.text = LocalizationService.Tr("whatsnew.btn", "Yenilikler");
            whatsNewBtn.onClick.AddListener(() =>
            {
                if (PhonePanelController.Instance != null) PhonePanelController.Instance.Close();
                if (ReleaseNotesService.Instance != null) ReleaseNotesService.Instance.ShowNow(markSeen: false);
            });

            (Button refBtn, Text refLbl) = MakeButton("Btn_RefreshFeed", feedPanel.transform, new Vector2(204f, -12f), new Vector2(114f, 40f), 17);
            refBtn.onClick.AddListener(RefreshFeed);
            _refreshButton = refBtn;
            _refreshLabel = refLbl;

            // ScrollRect for community messages
            GameObject scrollGo = new GameObject("FeedScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
            scrollGo.transform.SetParent(feedPanel.transform, false);
            RectTransform scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(14f, 14f);
            scrollRt.offsetMax = new Vector2(-14f, -64f);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.color = InputColor;
            scrollGo.GetComponent<Mask>().showMaskGraphic = true;

            GameObject contentGo = new GameObject("FeedContent", typeof(RectTransform), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(scrollGo.transform, false);
            RectTransform contentRt = (RectTransform)contentGo.transform;
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 460f);

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _feedText = contentGo.AddComponent<Text>();
            _feedText.font = _font;
            _feedText.fontSize = 18;
            _feedText.color = OffWhite;
            _feedText.alignment = TextAnchor.UpperLeft;
            _feedText.supportRichText = true;
            _feedText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _feedText.verticalOverflow = VerticalWrapMode.Overflow;
            _feedText.lineSpacing = 1.18f;
            _feedText.raycastTarget = false;

            ScrollRect sr = scrollGo.GetComponent<ScrollRect>();
            sr.viewport = scrollRt;
            sr.content = contentRt;
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 28f;

            SelectCategory(0);
            RefreshTexts();
        }

        public void RefreshTexts()
        {
            if (_title == null) return;
            _title.text = LocalizationService.Tr("feedback.title", "Our World Topluluk & Öneri");
            _info.text = LocalizationService.Tr("feedback.info", "Oyunda ne olsun istersin? Yaz, Firestore'a düşsün ve yeni sürümde ekleyelim!");
            _namePlaceholder.text = LocalizationService.Tr("feedback.name_placeholder", "Adın / Takma ad (isteğe bağlı)");
            _placeholder.text = LocalizationService.Tr("feedback.placeholder", "Fikrini, isteğini veya mesajını yaz…");
            _privacy.text = LocalizationService.Tr("feedback.privacy", "Anonim olarak Firestore'a kaydedilir.");
            _feedTitle.text = LocalizationService.Tr("feedback.feed_title", "Canlı Oyuncu Mesajları");
            _refreshLabel.text = _loadingFeed ? "..." : LocalizationService.Tr("feedback.refresh", "Yenile");
            for (int i = 0; i < 3; i++)
            {
                _catLabels[i].text = LocalizationService.Tr(CategoryKeys[i], CategoryIds[i]);
            }
            _sendLabel.text = LocalizationService.Tr(_sending ? "feedback.sending" : "feedback.send", "Gönder");
            UpdateCounter();
        }

        private void OnEnable()
        {
            if (_status != null && !_sending) _status.text = string.Empty;
            if (_nameInput != null && FeedbackService.Instance != null && string.IsNullOrEmpty(_nameInput.text))
            {
                _nameInput.text = FeedbackService.Instance.SavedSenderName;
            }
            RefreshFeed();
        }

        private void RefreshFeed()
        {
            if (_loadingFeed || _feedText == null) return;
            FeedbackService svc = FeedbackService.Instance;
            if (svc == null)
            {
                _feedText.text = "  Mesaj servisi hazır değil.";
                return;
            }

            _loadingFeed = true;
            if (_refreshButton != null) _refreshButton.interactable = false;
            if (_refreshLabel != null) _refreshLabel.text = "...";
            _feedText.text = "  Mesajlar yükleniyor…";

            svc.FetchRecentMessages(25, (messages, error) =>
            {
                _loadingFeed = false;
                if (_refreshButton != null) _refreshButton.interactable = true;
                RefreshTexts();

                if (_feedText == null) return;
                if (!string.IsNullOrEmpty(error))
                {
                    _feedText.text = $"  {error}";
                    return;
                }

                if (messages == null || messages.Count == 0)
                {
                    _feedText.text = "  Henüz mesaj yok. İlk fikri sen yaz!";
                    return;
                }

                StringBuilder sb = new StringBuilder(1024);
                for (int i = 0; i < messages.Count; i++)
                {
                    CommunityMessage m = messages[i];
                    string tag = m.Category == "bug" ? "[HATA]" : (m.Category == "other" ? "[MESAJ]" : "[FİKİR]");
                    string statusTag = m.Status == "done" ? "  <color=#6CE5B1>(Eklendi)</color>"
                        : (m.Status == "planned" ? "  <color=#F2C94C>(Planlandı)</color>" : string.Empty);

                    sb.Append("<color=#5EB8B0><b>").Append(tag).Append(' ')
                      .Append(EscapeRich(m.SenderName)).Append("</b></color>")
                      .Append(statusTag).Append('\n')
                      .Append(EscapeRich(m.Text)).Append("\n\n");
                }
                _feedText.text = sb.ToString();
            });
        }

        private static string EscapeRich(string s)
        {
            return string.IsNullOrEmpty(s) ? string.Empty : s.Replace("<", "‹").Replace(">", "›");
        }

        private void SelectCategory(int idx)
        {
            _selectedCategory = Mathf.Clamp(idx, 0, 2);
            for (int i = 0; i < 3; i++)
            {
                if (_catButtons[i] != null)
                {
                    _catButtons[i].GetComponent<Image>().color = i == _selectedCategory ? Accent : Inactive;
                }
            }
        }

        private void UpdateCounter()
        {
            if (_counter == null || _input == null) return;
            _counter.text = $"{_input.text.Length}/{FeedbackService.MaxLength}";
        }

        private void Send()
        {
            if (_sending) return;
            FeedbackService svc = FeedbackService.Instance;
            if (svc == null)
            {
                SetStatus("feedback.error", "Gönderilemedi.");
                return;
            }

            string senderName = _nameInput != null ? _nameInput.text : "Oyuncu";
            FeedbackSubmitResult result = svc.Submit(CategoryIds[_selectedCategory], _input.text, senderName, ok =>
            {
                _sending = false;
                if (_sendButton != null) _sendButton.interactable = true;
                SetStatus(ok ? "feedback.sent" : "feedback.queued", ok ? "Gönderildi! Teşekkürler." : "Sıraya alındı.");
                RefreshTexts();
                if (ok) RefreshFeed();
            });

            switch (result)
            {
                case FeedbackSubmitResult.TooShort:
                    SetStatus("feedback.empty", "En az 5 karakter yaz.");
                    break;
                case FeedbackSubmitResult.TooSoon:
                    SetStatus("feedback.too_soon", "Yeni mesaj için biraz bekle.");
                    break;
                case FeedbackSubmitResult.Queued:
                    _input.text = string.Empty;
                    SetStatus("feedback.queued", "Çevrimdışı kaydedildi, bağlanınca gönderilecek.");
                    break;
                case FeedbackSubmitResult.Sent:
                    _input.text = string.Empty;
                    if (_status.text != LocalizationService.Tr("feedback.sent", "Gönderildi! Teşekkürler."))
                    {
                        _sending = true;
                        _sendButton.interactable = false;
                        SetStatus("feedback.sending", "Gönderiliyor…");
                    }
                    break;
            }
            RefreshTexts();
        }

        private void SetStatus(string key, string fallback)
        {
            if (_status != null) _status.text = LocalizationService.Tr(key, fallback);
        }

        // ------------------------------------------------------------------ UI helpers

        private GameObject MakePanel(string name, Transform parent, Vector2 topPos, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = topPos;
            rt.sizeDelta = size;
            Image img = go.GetComponent<Image>();
            img.sprite = _sprite;
            img.type = _sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            return go;
        }

        private (Button, Text) MakeButton(string name, Transform parent, Vector2 topPos, Vector2 size, int fontSize)
        {
            GameObject go = MakePanel(name, parent, topPos, size, Inactive);
            Button b = go.AddComponent<Button>();
            b.targetGraphic = go.GetComponent<Image>();
            UiButtonFeedback fb = go.AddComponent<UiButtonFeedback>();
            fb.Configure(b, go.GetComponent<Image>());
            Text label = MakeStretchText("Label", go.transform, fontSize, FontStyle.Bold, OffWhite);
            label.alignment = TextAnchor.MiddleCenter;
            RectTransform lrt = (RectTransform)label.transform;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return (b, label);
        }

        private Text MakeText(string name, Transform parent, int size, FontStyle style, Color color, TextAnchor anchor, Vector2 topPos, Vector2 boxSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = topPos;
            rt.sizeDelta = boxSize;
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }

        private Text MakeStretchText(string name, Transform parent, int size, FontStyle style, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(16f, 10f);
            rt.offsetMax = new Vector2(-16f, -10f);
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }
    }
}
