using System;
using Sekai.MusicScoreMaker.Ingame;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sekai.MenuUI
{
    // Repairs legacy dialog metrics on instances; gameplay font assets stay independent.
    [DisallowMultipleComponent]
    public sealed class DialogTextLayout : MonoBehaviour
    {
        private DialogBase dialog;
        private TMP_Text[] labels;
        private RectTransform window;
        private int lastSignature;
        private bool initialized;
        private ScrollRect messageScroll;

        public static DialogTextLayout Prepare(DialogBase owner)
        {
            var layout = owner.GetComponent<DialogTextLayout>() ?? owner.gameObject.AddComponent<DialogTextLayout>();
            layout.dialog = owner;
            layout.window = owner.WindowRoot != null ? owner.WindowRoot.transform as RectTransform : null;
            layout.labels = owner.GetComponentsInChildren<TMP_Text>(true);
            layout.Refresh();
            return layout;
        }

        private void LateUpdate()
        {
            if (initialized && Signature() != lastSignature) Refresh();
        }

        private int Signature()
        {
            unchecked
            {
                int value = window != null ? window.rect.width.GetHashCode() : 0;
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null) value = value * 31 + ((RectTransform)canvas.transform).rect.size.GetHashCode();
                foreach (var label in labels)
                {
                    if (label == null) continue;
                    value = value * 31 + (label.text?.GetHashCode() ?? 0);
                    value = value * 31 + (label.font != null ? label.font.GetEntityId().GetHashCode() : 0);
                    value = value * 31 + label.fontSize.GetHashCode();
                    value = value * 31 + (label.font != null ? label.font.faceInfo.pointSize.GetHashCode() : 0);
                }
                return value;
            }
        }

        public void Refresh()
        {
            if (labels == null || window == null) return;
            foreach (var label in labels)
                if (label != null && label.lineSpacing < 0) label.lineSpacing = 0;

            // Only simple message dialogs share this structure. Form dialogs retain their fields.
            Type type = dialog.GetType();
            if (type == typeof(Common1ButtonDialog) || type == typeof(Common2ButtonDialog) || dialog is CommonMultiButtonDialog)
                LayoutMessage();
            if (dialog is MusicScoreMakerCustomQuantizeDialog) LayoutQuantize();
            if (dialog is AddMusicScoreEventDataDialog) LayoutEventMessage();
            LayoutRebuilder.MarkLayoutForRebuild(window);
            initialized = true;
            lastSignature = Signature();
        }

        private TMP_Text LabelAt(string path) => window.Find(path)?.GetComponent<TMP_Text>();

        private static void UseNaturalHeight(TMP_Text label)
        {
            label.enableAutoSizing = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            var localization = label.GetComponent<Localization.LocalizedTextBinding>();
            if (localization != null) localization.PreserveLayout = true;
        }

        private void LayoutMessage()
        {
            var body = LabelAt("ContentRoot/Content/MessageBody");
            if (body == null) return;
            UseNaturalHeight(body);
            body.fontSize = dialog is CommonMultiButtonDialog ? 36 : 32;
            var slot = (RectTransform)window.Find("ContentRoot");
            var viewport = (RectTransform)body.transform.parent;
            float width = Mathf.Max(160, window.rect.width - 128);
            float required = Mathf.Ceil(body.GetPreferredValues(body.text, width, float.PositiveInfinity).y) + 8;
            var canvas = GetComponentInParent<Canvas>();
            float canvasHeight = canvas != null ? ((RectTransform)canvas.transform).rect.height : 1080;
            float maximum = Mathf.Clamp(canvasHeight - 320, 160, 600);
            float visible = Mathf.Clamp(required, 120, maximum);
            var slotLayout = slot.GetComponent<LayoutElement>() ?? slot.gameObject.AddComponent<LayoutElement>();
            slotLayout.minHeight = slotLayout.preferredHeight = visible + 48;
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(64,24); viewport.offsetMax = new Vector2(-64,-24);
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.GetComponent<Image>() ?? viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear; hit.raycastTarget = true;
            if (messageScroll == null)
            {
                messageScroll = slot.gameObject.AddComponent<ScrollRect>();
                messageScroll.viewport = viewport; messageScroll.content = body.rectTransform;
                messageScroll.horizontal = false; messageScroll.movementType = ScrollRect.MovementType.Clamped;
                messageScroll.scrollSensitivity = 36;
            }
            messageScroll.vertical = required > visible;
            var rect = body.rectTransform;
            rect.anchorMin = new Vector2(0,1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f,1);
            rect.sizeDelta = new Vector2(0,Mathf.Max(required,visible)); rect.anchoredPosition = Vector2.zero;
            body.alignment = required > visible ? TextAlignmentOptions.Top : TextAlignmentOptions.Center;
            messageScroll.StopMovement(); messageScroll.verticalNormalizedPosition = 1;
        }

        private void LayoutQuantize()
        {
            var description = LabelAt("ContentRoot/Content/Text");
            if (description != null)
            {
                UseNaturalHeight(description); description.fontSize = 32;
                description.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                    Mathf.Max(120,description.GetPreferredValues(description.text,description.rectTransform.rect.width,float.PositiveInfinity).y+16));
            }
            var row = window.Find("ContentRoot/Content/Title") as RectTransform;
            if (row == null) return;
            var group = row.GetComponent<HorizontalLayoutGroup>();
            if (group == null) return;
            group.childControlWidth = true; group.childControlHeight = true;
            group.childForceExpandWidth = false; group.childForceExpandHeight = false;
            foreach (var label in row.GetComponentsInChildren<TMP_Text>(true))
            {
                label.enableAutoSizing = false; label.fontSize = 28;
                var element = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
                element.minWidth = element.preferredWidth = Mathf.Ceil(label.GetPreferredValues(label.text).x) + 12;
                element.minHeight = element.preferredHeight = 48;
            }
            LayoutRebuilder.MarkLayoutForRebuild(row);
        }

        private void LayoutEventMessage()
        {
            var body = LabelAt("ContentRoot/Content/MessageBody");
            if (body == null) return;
            // This leaf incorrectly carried an empty layout group that collapsed its own height.
            var group = body.GetComponent<LayoutGroup>(); if (group != null) group.enabled = false;
            var fitter = body.GetComponent<ContentSizeFitter>(); if (fitter != null) fitter.enabled = false;
            UseNaturalHeight(body); body.fontSize = 32;
            var rect = body.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f,.5f); rect.pivot = new Vector2(.5f,.5f);
            float width = Mathf.Max(160, window.rect.width - 160);
            rect.sizeDelta = new Vector2(width,Mathf.Max(100,body.GetPreferredValues(body.text,width,float.PositiveInfinity).y+16));
            rect.anchoredPosition = new Vector2(0,128);
        }
    }
}
