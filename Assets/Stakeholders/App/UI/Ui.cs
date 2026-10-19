using System;
using System.Collections.Generic;
using System.Globalization;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App.UI
{
    /// <summary>Tiny fluent helpers for building UI Toolkit trees in code.</summary>
    public static class Ui
    {
        public static VisualElement Div(params string[] classes) => new VisualElement().Cls(classes);
        public static VisualElement Row(params string[] classes) => new VisualElement().Cls("row").Cls(classes);
        public static VisualElement Col(params string[] classes) => new VisualElement().Cls("col").Cls(classes);
        public static VisualElement Spacer(int size = 8) => new VisualElement().Cls("sp" + size);
        public static VisualElement Grow() => new VisualElement().Cls("grow");

        public static Label Text(string text, params string[] classes)
        {
            var l = new Label(text ?? "");
            l.Cls(classes);
            return l;
        }

        public static Button Btn(string text, Action onClick, params string[] classes)
        {
            var b = new Button(onClick) { text = text };
            b.AddToClassList("btn");
            b.Cls(classes);
            return b;
        }

        public static T Cls<T>(this T e, params string[] classes) where T : VisualElement
        {
            if (classes == null) return e;
            foreach (var c in classes) if (!string.IsNullOrEmpty(c)) e.AddToClassList(c);
            return e;
        }

        public static T Toggle<T>(this T e, string cls, bool on) where T : VisualElement
        {
            e.EnableInClassList(cls, on);
            return e;
        }

        public static T Kids<T>(this T parent, params VisualElement[] children) where T : VisualElement
        {
            foreach (var c in children) if (c != null) parent.Add(c);
            return parent;
        }

        public static T Show<T>(this T e, bool visible) where T : VisualElement
        {
            e.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            return e;
        }

        public static T W<T>(this T e, float px) where T : VisualElement
        {
            e.style.width = px;
            e.style.flexShrink = 0;
            return e;
        }

        public static T WP<T>(this T e, float percent) where T : VisualElement
        {
            e.style.width = Length.Percent(percent);
            return e;
        }

        public static void SetText(this Label l, string text)
        {
            if (l.text != text) l.text = text;
        }

        public static void SetColor(this VisualElement e, Color c) => e.style.color = c;

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.white;
        }

        // Text inks for the notebook's paper (Notebook.uss). Notebook.FillFor turns them into marker fills.
        public static readonly Color Good = Hex("#23704A");
        public static readonly Color Bad = Hex("#B3361E");
        public static readonly Color Warn = Hex("#9A5200");
        public static readonly Color Accent = Hex("#2F5DA8");
        public static readonly Color Secret = Hex("#6B3FA0");
        public static readonly Color Gold = Hex("#8A5E00");
        public static readonly Color TextColor = Hex("#2B2622");
        public static readonly Color Dim = Hex("#5E554C");
        public static readonly Color Faint = Hex("#72675B");

        /// <summary>A player's seat color as a fill (faces, dots, slices).</summary>
        public static Color PlayerColor(int index) => Notebook.PlayerFill(index);

        /// <summary>A player's seat color dark enough for text (names).</summary>
        public static Color PlayerInk(int index) => Notebook.PlayerInk(index);

        public static Color CategoryColor(EventCategory c)
        {
            switch (c)
            {
                case EventCategory.Crisis: return Notebook.MarkerTomato;
                case EventCategory.Customer: return Hex("#FF7A59");
                case EventCategory.Opportunity: return Notebook.MarkerGreen;
                case EventCategory.Marketing: return Notebook.MarkerPink;
                case EventCategory.Inventory: return Notebook.MarkerOrange;
                case EventCategory.Supplier: return Notebook.MarkerTeal;
                case EventCategory.Finance: return Notebook.MarkerYellow;
                case EventCategory.HR: return Hex("#9BD442");
                case EventCategory.Competition: return Hex("#FF9A6B");
                case EventCategory.Logistics: return Notebook.MarkerBlue;
                default: return Notebook.MarkerBlue;
            }
        }

        public static string Initials(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "?";
            var parts = name.Trim().Split(' ');
            string s = parts[0].Substring(0, 1);
            if (parts.Length > 1 && parts[parts.Length - 1].Length > 0) s += parts[parts.Length - 1].Substring(0, 1);
            return s.ToUpperInvariant();
        }

        /// <summary>A doodled face: the head is tinted with the seat color, the face shows the personality.</summary>
        public static VisualElement Avatar(Player p, string size = null)
        {
            var a = Div("avatar", size);
            var face = Div("avatar-face");
            face.pickingMode = PickingMode.Ignore;
            a.Add(face);
            UpdateAvatar(a, p);
            return a;
        }

        public static void UpdateAvatar(VisualElement avatar, Player p)
        {
            avatar.style.unityBackgroundImageTintColor = p != null ? PlayerColor(p.Color) : Notebook.Rule;
            avatar.tooltip = p?.Name;
            var face = avatar.Q(className: "avatar-face");
            if (face == null) return;
            string kind = Notebook.FaceClass(p);
            foreach (var c in Notebook.FaceClasses) face.EnableInClassList(c, c == kind);
        }

        public static VisualElement Dot(Color c)
        {
            var d = Div("dot");
            d.style.backgroundColor = Notebook.FillFor(c);
            return d;
        }

        public static Label Badge(string text, string variant = null) => Text(text, "badge", variant != null ? "badge-" + variant : null);

        public static VisualElement Bar(float value01, Color? color = null, string extra = null)
        {
            var bar = Div("bar", extra);
            var fill = Div("bar-fill");
            bar.Add(fill);
            SetBar(bar, value01, color);
            return bar;
        }

        public static void SetBar(VisualElement bar, float value01, Color? color = null)
        {
            var fill = bar.ElementAt(0);
            fill.style.width = Length.Percent(Mathf.Clamp01(value01) * 100f);
            if (color.HasValue) fill.style.backgroundColor = Notebook.FillFor(color.Value);
        }

        public static VisualElement Card(string title = null, params string[] classes)
        {
            var c = Div("card").Cls(classes);
            if (!string.IsNullOrEmpty(title)) c.Add(Text(title.ToUpperInvariant(), "card-title"));
            return c;
        }

        public static TextField Input(string value = "", Action<string> onChange = null, string extraClass = null)
        {
            var t = new TextField { value = value ?? "" };
            t.Cls(extraClass);
            if (onChange != null) t.RegisterValueChangedCallback(e => onChange(e.newValue));
            return t;
        }

        public static double ParseNumber(string s, double fallback = 0)
        {
            if (string.IsNullOrWhiteSpace(s)) return fallback;
            s = s.Replace("$", "").Replace(",", "").Replace("%", "").Trim().ToLowerInvariant();
            double mult = 1;
            if (s.EndsWith("k")) { mult = 1000; s = s.Substring(0, s.Length - 1); }
            else if (s.EndsWith("m")) { mult = 1000000; s = s.Substring(0, s.Length - 1); }
            return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v * mult : fallback;
        }

        public static VisualElement Field(string label, VisualElement control)
        {
            var f = Div("field");
            f.Add(Text(label.ToUpperInvariant(), "field-label"));
            f.Add(control);
            return f;
        }

        /// <summary>Segmented control. Returns the container; the callback receives the new index.</summary>
        public static VisualElement Segmented(IList<string> options, int selected, Action<int> onChange)
        {
            var seg = Div("seg");
            var buttons = new List<Button>();
            for (int i = 0; i < options.Count; i++)
            {
                int idx = i;
                var b = new Button { text = options[i] };
                b.AddToClassList("seg-btn");
                b.clicked += () =>
                {
                    for (int k = 0; k < buttons.Count; k++) buttons[k].EnableInClassList("on", k == idx);
                    onChange?.Invoke(idx);
                };
                b.EnableInClassList("on", i == selected);
                buttons.Add(b);
                seg.Add(b);
            }
            return seg;
        }

        public static void Clear(VisualElement e)
        {
            if (e.childCount > 0) e.Clear();
        }
    }

    /// <summary>A dropdown built from plain buttons (fully themeable, works in any panel).</summary>
    public class Select : Button
    {
        readonly List<string> _options = new List<string>();
        int _index;
        public event Action<int> Changed;

        public int Index => _index;
        public string Value => _index >= 0 && _index < _options.Count ? _options[_index] : null;

        public Select(IEnumerable<string> options, int index = 0, Action<int> onChange = null)
        {
            AddToClassList("select");
            var chevron = new VisualElement { pickingMode = PickingMode.Ignore };
            chevron.AddToClassList("select-chevron");
            Add(chevron);
            SetOptions(options, index);
            if (onChange != null) Changed += onChange;
            clicked += Open;
        }

        public void SetOptions(IEnumerable<string> options, int index)
        {
            _options.Clear();
            _options.AddRange(options);
            _index = Mathf.Clamp(index, 0, Math.Max(0, _options.Count - 1));
            text = Value ?? "";
        }

        public void SetIndex(int index, bool notify = false)
        {
            _index = Mathf.Clamp(index, 0, Math.Max(0, _options.Count - 1));
            text = Value ?? "";
            if (notify) Changed?.Invoke(_index);
        }

        /// <summary>Where popups are attached (the app root, so app styles apply).</summary>
        public static VisualElement OverlayRoot;

        void Open()
        {
            var root = OverlayRoot ?? panel?.visualTree;
            if (root == null || _options.Count == 0) return;
            var blocker = new VisualElement();
            blocker.style.position = Position.Absolute;
            blocker.style.left = 0; blocker.style.top = 0; blocker.style.right = 0; blocker.style.bottom = 0;
            var menu = new ScrollView();
            menu.AddToClassList("select-menu");
            var wb = worldBound;
            var rootBound = root.worldBound;
            float left = wb.xMin - rootBound.xMin;
            float top = wb.yMax - rootBound.yMin + 2;
            float estimated = Mathf.Min(420, _options.Count * 28 + 10);
            if (top + estimated > rootBound.height) top = Mathf.Max(8, wb.yMin - rootBound.yMin - estimated - 2);
            menu.style.left = left;
            menu.style.top = top;
            menu.style.minWidth = Mathf.Max(160, wb.width);
            for (int i = 0; i < _options.Count; i++)
            {
                int idx = i;
                var item = new Button { text = _options[i] };
                item.AddToClassList("select-item");
                item.EnableInClassList("on", i == _index);
                item.clicked += () =>
                {
                    blocker.RemoveFromHierarchy();
                    SetIndex(idx);
                    Changed?.Invoke(idx);
                };
                menu.Add(item);
            }
            blocker.RegisterCallback<PointerDownEvent>(e => { if (e.target == blocker) blocker.RemoveFromHierarchy(); });
            blocker.Add(menu);
            root.Add(blocker);
        }
    }
}
