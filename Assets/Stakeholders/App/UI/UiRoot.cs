using System;
using System.Collections.Generic;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App.UI
{
    /// <summary>Creates the UI Toolkit panel at runtime and owns the screen, modal and toast layers.</summary>
    public class UiRoot
    {
        public readonly UIDocument Document;
        public readonly VisualElement Root;
        public readonly VisualElement ScreenLayer;
        public readonly VisualElement ModalLayer;
        public readonly VisualElement ToastLayer;
        readonly List<Modal> _modals = new List<Modal>();

        public UiRoot(GameObject host)
        {
            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.name = "Stakeholders Panel";
            panel.themeStyleSheet = Resources.Load<ThemeStyleSheet>("StakeholdersUI/Theme");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.clearColor = true;
            panel.colorClearValue = Ui.Hex("#F5EEDF");
            panel.sortingOrder = 10;

            Document = host.AddComponent<UIDocument>();
            Document.panelSettings = panel;

            Root = Document.rootVisualElement;
            var uss = Resources.Load<StyleSheet>("StakeholdersUI/Game");
            if (uss != null) Root.styleSheets.Add(uss);
            // The notebook theme re-skins Game.uss, so it must load after it.
            var theme = Resources.Load<StyleSheet>("StakeholdersUI/Notebook");
            if (theme != null) Root.styleSheets.Add(theme);
            Root.AddToClassList("app");
            Root.style.flexGrow = 1;

            ScreenLayer = Ui.Div("fill");
            ScreenLayer.style.flexDirection = FlexDirection.Column;
            ModalLayer = Ui.Div("modal-layer");
            ModalLayer.pickingMode = PickingMode.Ignore;
            ToastLayer = Ui.Div("toast-layer");
            ToastLayer.pickingMode = PickingMode.Ignore;
            Root.Add(ScreenLayer);
            Root.Add(ModalLayer);
            Root.Add(ToastLayer);
            Select.OverlayRoot = Root;

            Root.RegisterCallback<KeyDownEvent>(OnKey, TrickleDown.TrickleDown);
        }

        void OnKey(KeyDownEvent e)
        {
            if (e.keyCode == KeyCode.Escape && _modals.Count > 0)
            {
                _modals[_modals.Count - 1].Close();
                e.StopPropagation();
            }
        }

        public void SetScreen(VisualElement screen)
        {
            CloseAllModals();
            ScreenLayer.Clear();
            screen.style.flexGrow = 1;
            ScreenLayer.Add(screen);
        }

        public Modal ShowModal(string title, bool wide = false)
        {
            var m = new Modal(this, title, wide);
            _modals.Add(m);
            ModalLayer.Add(m.Overlay);
            return m;
        }

        internal void Remove(Modal m)
        {
            _modals.Remove(m);
            m.Overlay.RemoveFromHierarchy();
        }

        public void CloseAllModals()
        {
            foreach (var m in _modals.ToArray()) m.Close();
        }

        public bool HasModal => _modals.Count > 0;

        public void RefreshModals()
        {
            foreach (var m in _modals.ToArray()) m.Refresh?.Invoke();
        }

        public void Toast(string text, NoticeKind kind = NoticeKind.Info)
        {
            string cls = kind switch
            {
                NoticeKind.Success => "toast-good",
                NoticeKind.Warning => "toast-warn",
                NoticeKind.Danger => "toast-bad",
                NoticeKind.Private => "toast-secret",
                NoticeKind.Xp => "toast-xp",
                _ => null
            };
            var t = Ui.Div("toast", cls);
            var label = Ui.Text(text);
            label.pickingMode = PickingMode.Ignore;
            t.pickingMode = PickingMode.Ignore;
            t.Add(label);
            t.style.opacity = 0;
            t.style.translate = new Translate(30, 0);
            ToastLayer.Add(t);
            while (ToastLayer.childCount > 5) ToastLayer.RemoveAt(0);
            t.schedule.Execute(() => { t.style.opacity = 1; t.style.translate = new Translate(0, 0); }).StartingIn(20);
            t.schedule.Execute(() => { t.style.opacity = 0; }).StartingIn(kind == NoticeKind.Private || kind == NoticeKind.Danger ? 7000 : 5000);
            t.schedule.Execute(() => t.RemoveFromHierarchy()).StartingIn(kind == NoticeKind.Private || kind == NoticeKind.Danger ? 7400 : 5400);
        }

        public void Confirm(string title, string text, string okText, Action onOk, string okClass = null)
        {
            var m = ShowModal(title);
            m.Body.Add(Ui.Text(text, "para"));
            m.AddButton("Cancel", m.Close, "btn-ghost");
            m.AddButton(okText, () => { m.Close(); onOk(); }, okClass);
        }
    }

    public class Modal
    {
        readonly UiRoot _root;
        public readonly VisualElement Overlay;
        public readonly VisualElement Panel;
        public readonly VisualElement Body;
        public readonly VisualElement Foot;
        public readonly Label Title;
        public Action Refresh;
        public Action OnClosed;
        public bool Closed { get; private set; }

        public Modal(UiRoot root, string title, bool wide)
        {
            _root = root;
            Overlay = Ui.Div("modal-overlay");
            Panel = Ui.Div("modal", wide ? "modal-wide" : null);
            var head = Ui.Div("modal-head");
            Title = Ui.Text(title, "h2", "grow");
            head.Add(Title);
            head.Add(Ui.Btn("Close", Close, "btn-ghost", "btn-sm"));
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexShrink = 1;
            Body = Ui.Div("modal-body");
            scroll.Add(Body);
            Foot = Ui.Div("modal-foot");
            Panel.Add(head);
            Panel.Add(scroll);
            Panel.Add(Foot);
            Overlay.Add(Panel);
            Overlay.RegisterCallback<PointerDownEvent>(e => { if (e.target == Overlay) Close(); });
        }

        public Button AddButton(string text, Action onClick, string cls = null)
        {
            var b = Ui.Btn(text, onClick, cls);
            Foot.Add(b);
            return b;
        }

        public void Close()
        {
            if (Closed) return;
            Closed = true;
            _root.Remove(this);
            OnClosed?.Invoke();
        }
    }
}
