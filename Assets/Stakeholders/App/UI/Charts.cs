using System;
using System.Collections.Generic;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App.UI
{
    /// <summary>Simple vector line chart drawn with Painter2D.</summary>
    public class LineChart : VisualElement
    {
        public class Series
        {
            public List<float> Values = new List<float>();
            public Color Color;
            public string Name;
            public bool Fill;
        }

        readonly List<Series> _series = new List<Series>();
        readonly Label _max, _min, _legend;
        float _lo, _hi;
        public Func<double, string> Format = v => Fmt.Money(v);

        public LineChart(float height = 170)
        {
            style.height = height;
            style.flexGrow = 1;
            style.marginTop = 6;
            generateVisualContent += Draw;
            _max = Ui.Text("", "tiny", "faint");
            _max.style.position = Position.Absolute;
            _max.style.right = 2; _max.style.top = -2;
            _min = Ui.Text("", "tiny", "faint");
            _min.style.position = Position.Absolute;
            _min.style.right = 2; _min.style.bottom = -2;
            _legend = Ui.Text("", "tiny", "dim");
            _legend.style.position = Position.Absolute;
            _legend.style.left = 2; _legend.style.top = -2;
            Add(_max); Add(_min); Add(_legend);
        }

        public void SetData(params Series[] series)
        {
            _series.Clear();
            _series.AddRange(series);
            _lo = float.MaxValue; _hi = float.MinValue;
            foreach (var s in _series)
                foreach (var v in s.Values) { if (v < _lo) _lo = v; if (v > _hi) _hi = v; }
            if (_lo == float.MaxValue) { _lo = 0; _hi = 1; }
            if (_lo > 0) _lo = 0;
            if (_hi - _lo < 1e-3f) _hi = _lo + 1;
            _max.text = Format(_hi);
            _min.text = Format(_lo);
            var names = new List<string>();
            foreach (var s in _series) if (!string.IsNullOrEmpty(s.Name)) names.Add(s.Name);
            _legend.text = string.Join("   ", names);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            if (r.width < 20 || r.height < 20) return;
            var p = ctx.painter2D;
            float top = r.yMin + 12, bottom = r.yMax - 12;
            float h = bottom - top;
            p.lineWidth = 1;
            p.strokeColor = new Color(0.17f, 0.15f, 0.13f, 0.1f);
            for (int i = 0; i <= 4; i++)
            {
                float y = top + h * i / 4f;
                p.BeginPath(); p.MoveTo(new Vector2(r.xMin, y)); p.LineTo(new Vector2(r.xMax, y)); p.Stroke();
            }
            if (_lo < 0 && _hi > 0)
            {
                float zy = bottom - (0 - _lo) / (_hi - _lo) * h;
                p.strokeColor = new Color(0.17f, 0.15f, 0.13f, 0.35f);
                p.BeginPath(); p.MoveTo(new Vector2(r.xMin, zy)); p.LineTo(new Vector2(r.xMax, zy)); p.Stroke();
            }
            foreach (var s in _series)
            {
                int n = s.Values.Count;
                if (n < 2) continue;
                Vector2 Pt(int i) => new Vector2(r.xMin + r.width * i / (n - 1), bottom - (s.Values[i] - _lo) / (_hi - _lo) * h);
                if (s.Fill)
                {
                    float baseY = bottom - (Mathf.Max(0, _lo) - _lo) / (_hi - _lo) * h;
                    p.fillColor = new Color(s.Color.r, s.Color.g, s.Color.b, 0.12f);
                    p.BeginPath();
                    p.MoveTo(new Vector2(r.xMin, baseY));
                    for (int i = 0; i < n; i++) p.LineTo(Pt(i));
                    p.LineTo(new Vector2(r.xMax, baseY));
                    p.ClosePath();
                    p.Fill();
                }
                p.strokeColor = s.Color;
                p.lineWidth = 2.6f;
                p.lineJoin = LineJoin.Round;
                p.lineCap = LineCap.Round;
                p.BeginPath();
                p.MoveTo(Pt(0));
                for (int i = 1; i < n; i++) p.LineTo(Pt(i));
                p.Stroke();
            }
        }

        public static List<float> Smooth(List<float> src, int window, int take)
        {
            var result = new List<float>();
            int start = Math.Max(0, src.Count - take);
            for (int i = start; i < src.Count; i++)
            {
                float sum = 0; int c = 0;
                for (int k = Math.Max(0, i - window + 1); k <= i; k++) { sum += src[k]; c++; }
                result.Add(c > 0 ? sum / c : 0);
            }
            return result;
        }

        public static List<float> Tail(List<float> src, int take)
        {
            int start = Math.Max(0, src.Count - take);
            return src.GetRange(start, src.Count - start);
        }
    }

    /// <summary>Donut chart for the cap table.</summary>
    public class PieChart : VisualElement
    {
        readonly List<(float value, Color color)> _slices = new List<(float, Color)>();

        public PieChart(float size = 160)
        {
            style.width = size;
            style.height = size;
            style.flexShrink = 0;
            generateVisualContent += Draw;
        }

        public void SetSlices(IEnumerable<(float value, Color color)> slices)
        {
            _slices.Clear();
            _slices.AddRange(slices);
            MarkDirtyRepaint();
        }

        void Draw(MeshGenerationContext ctx)
        {
            var r = contentRect;
            float total = 0;
            foreach (var s in _slices) total += Mathf.Max(0, s.value);
            if (total <= 0 || r.width < 10) return;
            var p = ctx.painter2D;
            var center = r.center;
            float radius = Mathf.Min(r.width, r.height) / 2f - 2;
            // Marker-colored slices, each outlined in ink, around a paper hole.
            p.strokeColor = Notebook.Ink;
            p.lineWidth = 2f;
            p.lineJoin = LineJoin.Round;
            float angle = -90f;
            foreach (var s in _slices)
            {
                float sweep = Mathf.Max(0, s.value) / total * 360f;
                if (sweep <= 0.01f) continue;
                p.fillColor = Notebook.FillFor(s.color);
                p.BeginPath();
                p.MoveTo(center);
                p.Arc(center, radius, Angle.Degrees(angle), Angle.Degrees(angle + sweep));
                p.ClosePath();
                p.Fill();
                p.Stroke();
                angle += sweep;
            }
            p.fillColor = Notebook.Card;
            p.BeginPath();
            p.MoveTo(center + new Vector2(radius * 0.58f, 0));
            p.Arc(center, radius * 0.58f, Angle.Degrees(0), Angle.Degrees(360));
            p.ClosePath();
            p.Fill();
            p.Stroke();
        }
    }
}
