using Gridroam.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Gridroam.UI
{
    /// <summary>
    /// "Öneri / Ideas" tab inside the phone. Built at runtime so the scene does not need regenerating.
    /// Players pick a category, type an idea and send it to Firebase via <see cref="FeedbackService"/>.
    /// </summary>
    public class FeedbackTabView : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.11f, 0.14f, 0.19f, 0.80f);
        private static readonly Color InputColor = new Color(0.07f, 0.09f, 0.12f, 0.95f);
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
        private Text _sendLabel;
        private Text _counter;
        private InputField _input;
        private Button _sendButton;
        private readonly Button[] _catButtons = new Button[3];
        private readonly Text[] _catLabels = new Text[3];
        private int _selectedCategory;
        private bool _sending;

        public static FeedbackTabView Create(Transform phonePanel, Font font, Sprite roundedSprite)
        {
            GameObject root = new GameObject("Tab_Feedback", typeof(RectTransform));
            root.transform.SetParent(phonePanel, false);
            RectTransform rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(36f, 28f);
            rt.offsetMax = new Vector2(-36f, -92f);

            FeedbackTabView view = root.AddComponent<FeedbackTabView>();
            view._font = font;
            view._sprite = roundedSprite;
            view.Build();
            root.SetActive(false);
            return view;
        }

        private void Build()
        {
            _title = MakeText("Title", transform, 30, FontStyle.Bold, OffWhite, TextAnchor.MiddleCenter, 0f, 1100f, 46f);
            _info = MakeText("Info", transform, 22, FontStyle.Normal, Muted, TextAnchor.MiddleCenter, -52f, 1100f, 60f);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                (Button b, Text l) = MakeButton($"Btn_Cat_{CategoryIds[i]}", transform, new Vector2((i - 1) * 200f, -126f), new Vector2(184f, 52f), 22);
                b.onClick.AddListener(() => SelectCategory(idx));
                _catButtons[i] = b;
                _catLabels[i] = l;
            }

            // Input field
            GameObject inputGo = MakePanel("FeedbackInput", transform, new Vector2(0f, -196f), new Vector2(1100f, 290f), InputColor);
            _input = inputGo.AddComponent<InputField>();
            _input.targetGraphic = inputGo.GetComponent<Image>();

            Text inputText = MakeStretchText("Text", inputGo.transform, 24, FontStyle.Normal, OffWhite);
            inputText.supportRichText = false;
            _placeholder = MakeStretchText("Placeholder", inputGo.transform, 24, FontStyle.Italic, new Color(Muted.r, Muted.g, Muted.b, 0.7f));

            _input.textComponent = inputText;
            _input.placeholder = _placeholder;
            _input.lineType = InputField.LineType.MultiLineNewline;
            _input.characterLimit = FeedbackService.MaxLength;
            _input.onValueChanged.AddListener(_ => UpdateCounter());

            _counter = MakeText("Counter", transform, 18, FontStyle.Normal, Muted, TextAnchor.MiddleRight, -490f, 1100f, 26f);

            (Button send, Text sendLbl) = MakeButton("Btn_SendFeedback", transform, new Vector2(0f, -526f), new Vector2(320f, 64f), 26);
            send.GetComponent<Image>().color = Accent;
            send.onClick.AddListener(Send);
            _sendButton = send;
            _sendLabel = sendLbl;

            _status = MakeText("Status", transform, 22, FontStyle.Bold, OffWhite, TextAnchor.MiddleCenter, -600f, 1100f, 36f);
            _privacy = MakeText("Privacy", transform, 17, FontStyle.Normal, Muted, TextAnchor.MiddleCenter, -644f, 1100f, 30f);

            SelectCategory(0);
            RefreshTexts();
        }

        public void RefreshTexts()
        {
            if (_title == null) return;
            _title.text = LocalizationService.Tr("feedback.title", "Oyunu birlikte geliştirelim");
            _info.text = LocalizationService.Tr("feedback.info", "Oyunda ne olsun istersin?");
            _placeholder.text = LocalizationService.Tr("feedback.placeholder", "Fikrini yaz…");
            _privacy.text = LocalizationService.Tr("feedback.privacy", "");
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

            FeedbackSubmitResult result = svc.Submit(CategoryIds[_selectedCategory], _input.text, ok =>
            {
                _sending = false;
                _sendButton.interactable = true;
                SetStatus(ok ? "feedback.sent" : "feedback.queued", ok ? "Teşekkürler!" : "Kaydedildi.");
                RefreshTexts();
            });

            switch (result)
            {
                case FeedbackSubmitResult.TooShort:
                    SetStatus("feedback.empty", "En az 5 karakter yaz.");
                    break;
                case FeedbackSubmitResult.TooSoon:
                    SetStatus("feedback.too_soon", "Biraz bekle.");
                    break;
                case FeedbackSubmitResult.Queued:
                    _input.text = string.Empty;
                    SetStatus("feedback.queued", "Kaydedildi.");
                    break;
                case FeedbackSubmitResult.Sent:
                    _input.text = string.Empty;
                    if (_status.text != LocalizationService.Tr("feedback.sent", "Teşekkürler!"))
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

        private Text MakeText(string name, Transform parent, int size, FontStyle style, Color color, TextAnchor anchor, float topY, float width, float height)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, topY);
            rt.sizeDelta = new Vector2(width, height);
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
            rt.offsetMin = new Vector2(20f, 14f);
            rt.offsetMax = new Vector2(-20f, -14f);
            Text t = go.GetComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.UpperLeft;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            return t;
        }
    }
}
