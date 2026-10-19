using System;
using System.Collections.Generic;
using System.Text;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App.UI
{
    /// <summary>
    /// The dashboard's living HQ: a hand-drawn side view of the company (buildings, stock, staff, deliveries)
    /// drawn with Painter2D from the game state. It grows from a garage to a mega warehouse, the next building
    /// waits on an empty lot with a price tag, and the delivery van runs while orders come in and stops with the clock.
    /// <para>
    /// UI Toolkit caps every element's mesh at 65,535 vertices and wraps around (spikes to the origin) past that,
    /// so the scene is split into layers (backdrop, one per building, people) and draws its own cheap polylines
    /// instead of Painter2D arcs and curves. The layers only repaint when the company changes; the van, parcels
    /// and clouds move on their own elements.
    /// </para>
    /// </summary>
    public class HqScene : VisualElement
    {
        /// <summary>One building type the company owns (several of a type are drawn once, with a count).</summary>
        public struct Site
        {
            public string Kind;
            public int Count;
            public int Capacity;
        }

        /// <summary>Everything the drawing depends on, copied from the view.</summary>
        public class Snapshot
        {
            public string Company = "";
            public readonly List<Site> Buildings = new List<Site>();
            public string NextKind;
            public double NextCost;
            public float Fill;
            public int Incoming, Free;
            public readonly int[] Staff = new int[6];
            public readonly List<Color> Partners = new List<Color>();
            public int Orders, FulfillCapacity, Backlog;
            public bool Paused;
            public bool Shelving, Robots, Brand, WebStore, Express, International, Factory;
            public string Building;
            public int BuildingDays;
            public int Stage;
            public bool Public;
            public bool Overflow;

            public int StaffTotal
            {
                get { int n = 0; foreach (var s in Staff) n += s; return n; }
            }

            internal string Key()
            {
                var sb = new StringBuilder(Company).Append('|');
                foreach (var b in Buildings) sb.Append(b.Kind).Append(b.Count).Append(':').Append(b.Capacity).Append(',');
                sb.Append('|').Append(NextKind).Append(NextCost).Append('|').Append(Mathf.RoundToInt(Fill * 40)).Append(Overflow).Append(Incoming).Append('/').Append(Free / 25).Append('|');
                foreach (var s in Staff) sb.Append(s).Append(',');
                foreach (var c in Partners) sb.Append(ColorUtility.ToHtmlStringRGB(c));
                sb.Append('|').Append(Orders).Append('/').Append(FulfillCapacity).Append('/').Append(Backlog).Append('|')
                  .Append(Shelving).Append(Robots).Append(Brand).Append(WebStore).Append(Express).Append(International).Append(Factory)
                  .Append('|').Append(Building).Append(BuildingDays).Append('|').Append(Stage).Append(Public);
                return sb.ToString();
            }
        }

        /// <summary>Copies what the scene needs from a (possibly per-player) view of the game.</summary>
        public static Snapshot From(GameState v)
        {
            var c = v.Company;
            var s = new Snapshot { Company = c.Name ?? "", Stage = c.Stage, Public = c.WentPublic };
            foreach (var kind in Kinds)
            {
                int n = 0, cap = 0;
                foreach (var w in c.Warehouses) if (w.Tier == kind) { n++; cap += w.Capacity; }
                if (n > 0) s.Buildings.Add(new Site { Kind = kind, Count = n, Capacity = cap });
            }
            if (s.Buildings.Count == 0) s.Buildings.Add(new Site { Kind = "garage", Count = 1, Capacity = 1500 });
            foreach (var kind in Kinds)
            {
                if (kind == "garage" || c.Warehouses.Exists(w => w.Tier == kind)) continue;
                s.NextKind = kind;
                s.NextCost = Catalog.GetWarehouse(kind)?.SetupCost ?? 0;
                break;
            }
            foreach (var sh in c.Shipments) s.Incoming += sh.Quantity;
            s.Fill = c.StorageCapacity > 0 ? Mathf.Clamp01((float)(c.StorageUsed / c.StorageCapacity)) : 0;
            s.Overflow = s.Incoming > 0 && c.StorageUsed + s.Incoming > c.StorageCapacity;
            s.Free = (int)Math.Max(0, c.StorageCapacity - c.StorageUsed);
            foreach (var e in c.Employees) s.Staff[Mathf.Clamp((int)e.Role, 0, 5)]++;
            foreach (var p in v.Players) s.Partners.Add(Ui.PlayerColor(p.Color));
            s.Orders = (int)Math.Round(c.OrdersToday);
            s.FulfillCapacity = (int)Math.Round(c.CapacityPerDay);
            s.Backlog = (int)Math.Round(c.Backlog);
            s.Paused = v.Paused || v.Phase != GamePhase.Running;
            s.Shelving = c.Has("log1");
            s.Express = c.Has("log3");
            s.Robots = c.Has("log4");
            s.Brand = c.Has("brd1");
            s.WebStore = c.Has("chn1");
            s.International = c.Has("chn4");
            s.Factory = c.Has("src4");
            UpgradeBuild soonest = null;
            foreach (var b in c.Building) if (soonest == null || b.DoneDay < soonest.DoneDay) soonest = b;
            if (soonest != null)
            {
                s.Building = Catalog.GetUpgrade(soonest.Id)?.Name ?? soonest.Id;
                s.BuildingDays = Math.Max(0, soonest.DoneDay - v.Day);
            }
            return s;
        }

        public static string ShortName(string kind)
        {
            switch (kind)
            {
                case "small": return "small warehouse";
                case "regional": return "fulfillment center";
                case "mega": return "mega warehouse";
                default: return "garage";
            }
        }

        /// <summary>Called by the "for sale" sign on the empty lot.</summary>
        public Action OpenOperations;

        /// <summary>One line describing the HQ, for the card header.</summary>
        public string Summary { get; private set; } = "";

        // Design space: 250 units tall, as wide as the element allows. Buildings stand on G; people stand in front, on Feet.
        const float H = 250f;
        const float G = 182f;
        const float Feet = 216f;
        static readonly string[] Kinds = { "garage", "small", "regional", "mega" };

        static readonly Color Ink = Ui.Hex("#2B2622");
        static readonly Color Faded = Ui.Hex("#A89A83");
        static readonly Color Card = Ui.Hex("#FFFCF5");
        static readonly Color Paper = Ui.Hex("#F5EEDF");
        static readonly Color Deep = Ui.Hex("#EBE1CC");
        static readonly Color Rule = Ui.Hex("#D8CBB3");
        static readonly Color Interior = Ui.Hex("#E9DBC2");
        static readonly Color Cardboard = Ui.Hex("#D8B27A");
        static readonly Color Glass = Ui.Hex("#CFE3FF");
        static readonly Color Solar = Ui.Hex("#2F5DA8");
        static readonly Color Ghost = Ui.Hex("#B8A98E");
        static readonly Color Haze = Ui.Hex("#EDE4D3");
        static readonly Color Leaf = Ui.Hex("#DCEBC8");
        static readonly Color Yellow = Ui.Hex("#FFD84D");
        static readonly Color Tomato = Ui.Hex("#F07A5F");
        static readonly Color Blue = Ui.Hex("#7FB0FF");
        static readonly Color Green = Ui.Hex("#5CBF7F");
        static readonly Color Lilac = Ui.Hex("#B89AF0");
        static readonly Color Orange = Ui.Hex("#FFB25B");
        static readonly Color NoFill = new Color(0, 0, 0, 0);
        static readonly List<Color> DefaultPie = new List<Color> { Blue, Tomato, Green, Yellow };

        // Staff by role (EmployeeRole order): a marker body and a lighter head, like the partner faces.
        static readonly Color[] RoleBody = { Ui.Hex("#FFB25B"), Ui.Hex("#5CBF7F"), Ui.Hex("#F06BAA"), Ui.Hex("#7FB0FF"), Ui.Hex("#B89AF0"), Ui.Hex("#FFD84D") };
        static readonly Color[] RoleHead = { Ui.Hex("#FFD6A8"), Ui.Hex("#D4EFC0"), Ui.Hex("#FFC9D6"), Ui.Hex("#CFE3FF"), Ui.Hex("#E4D6FF"), Ui.Hex("#FFE58A") };
        static readonly int[] RoleOrder = { 0, 1, 3, 2, 4, 5 };

        struct Slot
        {
            public Site Site;
            public float X, W;
            public Rect Sign;
            public Vector2 Door;
        }

        struct Person
        {
            public float X;
            public Color Body, Head;
            public int Role;
        }

        Snapshot _snap;
        string _key;
        readonly List<Slot> _slots = new List<Slot>();
        readonly List<Person> _people = new List<Person>();
        float _s = 1, _ox, _oy, _designW = 800;
        float _px, _py;
        float _dockX, _dockEnd, _ghostX = -1, _factoryX = -1;
        float _t;
        bool _vanMoving;
        Color _ink = Ink;

        readonly VisualElement _back, _houses, _front, _van, _parcel;
        readonly List<VisualElement> _houseLayers = new List<VisualElement>();
        readonly VisualElement[] _clouds = new VisualElement[2];
        readonly Label _ordersNote, _backlogNote, _crowdNote, _sticky;
        readonly Button _price;
        readonly List<Label> _captions = new List<Label>();
        readonly List<Label> _signs = new List<Label>();

        public HqScene()
        {
            AddToClassList("hq-scene");
            for (int i = 0; i < _clouds.Length; i++)
            {
                _clouds[i] = Layer(this, "hq-float");
                _clouds[i].generateVisualContent += DrawCloud;
            }
            // Clouds pass behind everything; the van and parcels move in front of the drawing.
            _back = Layer(this, "hq-canvas");
            _back.generateVisualContent += DrawBack;
            _houses = Layer(this, "hq-canvas");
            _front = Layer(this, "hq-canvas");
            _front.generateVisualContent += DrawFront;
            _parcel = Layer(this, "hq-parcel");
            _van = Layer(this, "hq-float");
            _van.generateVisualContent += DrawVan;
            _ordersNote = Note("hq-note");
            _backlogNote = Note("hq-note", "bad");
            _crowdNote = Note("hq-note");
            _sticky = Note("hq-sticky", "sticky", "sticky-orange");
            _price = Ui.Btn("", () => OpenOperations?.Invoke(), "btn-ghost", "btn-sm", "hq-price");
            _price.style.translate = new Translate(Length.Percent(-50), 0);
            Add(_price);
            RegisterCallback<GeometryChangedEvent>(_ => Relayout());
            schedule.Execute(Tick).Every(33);
        }

        static VisualElement Layer(VisualElement parent, string cls)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore };
            e.AddToClassList(cls);
            parent.Add(e);
            return e;
        }

        Label Note(params string[] classes)
        {
            var l = Ui.Text("", classes);
            l.pickingMode = PickingMode.Ignore;
            Add(l);
            return l;
        }

        public void SetState(Snapshot s)
        {
            if (s == null) return;
            _snap = s;
            string key = s.Key();
            if (key == _key) return;
            _key = key;
            Summary = Describe(s);
            Relayout();
        }

        static string Describe(Snapshot s)
        {
            var parts = new List<string>();
            foreach (var b in s.Buildings) parts.Add((b.Count > 1 ? b.Count + " × " : "") + ShortName(b.Kind));
            int staff = s.StaffTotal;
            string people = staff == 0 ? "the partners pack every box" : staff == 1 ? "1 employee" : staff + " employees";
            // What's under construction goes here rather than into the crowded drawing (the crane shows it there).
            string building = s.Building == null ? "" : $" · building {s.Building} ({(s.BuildingDays <= 1 ? "almost done" : s.BuildingDays + " days")})";
            return $"{string.Join(" + ", parts)} · {people} · storage {Fmt.Pct(s.Fill)} full{building}";
        }

        // ------------------------------------------------------------------ layout

        static float WidthOf(string kind) => kind == "garage" ? 150 : kind == "small" ? 210 : kind == "regional" ? 280 : 360;
        static float WallOf(string kind) => kind == "garage" ? 96 : kind == "small" ? 118 : kind == "regional" ? 136 : 148;
        /// <summary>Height of the highest part (roof, skylights, solar panels).</summary>
        static float TopOf(string kind) => kind == "garage" ? 140 : kind == "small" ? 145 : kind == "regional" ? 136 : 162;

        static Slot MakeSlot(Site site, float x)
        {
            var slot = new Slot { Site = site, X = x, W = WidthOf(site.Kind) };
            switch (site.Kind)
            {
                case "garage": slot.Door = new Vector2(x + 75, G - 56); break;
                case "small": slot.Sign = new Rect(x + 55, G - 112, 100, 20); slot.Door = new Vector2(x + 98, G - 60); break;
                case "regional": slot.Sign = new Rect(x + 90, G - 114, 100, 20); slot.Door = new Vector2(x + 234, G - 48); break;
                default: slot.Sign = new Rect(x + 124, G - 146, 112, 18); slot.Door = new Vector2(x + 318, G - 44); break;
            }
            return slot;
        }

        float VanParkX => _dockX + (_snap != null && _snap.Backlog > 0 ? 58 : 0);
        float VanLength => _snap != null && _snap.Express ? 100 : 78;
        Vector2 DoorPoint => _slots.Count > 0 ? _slots[_slots.Count - 1].Door : new Vector2(90, G - 50);
        bool Ready => _snap != null && _slots.Count > 0;

        void Relayout()
        {
            if (_snap == null) return;
            var r = contentRect;
            if (float.IsNaN(r.width) || r.width < 60 || r.height < 60) return;

            _slots.Clear();
            float x = 18;
            foreach (var b in _snap.Buildings)
            {
                var slot = MakeSlot(b, x);
                _slots.Add(slot);
                x += slot.W + 30;
            }
            _dockX = x + 6;
            _dockEnd = VanParkX + VanLength + 40 + (_snap.WebStore ? 44 : 0);
            x = _dockEnd + 16;
            _ghostX = -1;
            if (_snap.NextKind != null) { _ghostX = x; x += WidthOf(_snap.NextKind) + 26; }
            _factoryX = -1;
            if (_snap.Factory) { _factoryX = x; x += 150; }

            float needed = Mathf.Max(x + 8, 640);
            _s = Mathf.Min(r.height / H, r.width / needed);
            _ox = r.xMin;
            _oy = r.yMin + r.height - H * _s;
            _designW = r.width / _s;

            // One layer per building keeps each mesh far below the vertex cap, even with full warehouses.
            while (_houseLayers.Count < _slots.Count)
            {
                int index = _houseLayers.Count;
                var layer = Layer(_houses, "hq-canvas");
                layer.generateVisualContent += ctx => DrawHouse(ctx, index);
                _houseLayers.Add(layer);
            }
            for (int i = 0; i < _houseLayers.Count; i++)
            {
                _houseLayers[i].style.display = i < _slots.Count ? DisplayStyle.Flex : DisplayStyle.None;
                _houseLayers[i].MarkDirtyRepaint();
            }

            LayoutPeople();
            PlaceLabels(r);
            _back.MarkDirtyRepaint();
            _front.MarkDirtyRepaint();
            _van.MarkDirtyRepaint();
            foreach (var c in _clouds) c.MarkDirtyRepaint();
        }

        void LayoutPeople()
        {
            _people.Clear();
            float start = _slots[0].X + 24, end = _dockX - 14;
            int fit = Mathf.Max(1, Mathf.FloorToInt((end - start) / 30f) + 1);
            if (_snap.StaffTotal == 0)
            {
                // No employees yet: the partners pack the boxes themselves.
                for (int i = 0; i < _snap.Partners.Count && i < Mathf.Min(4, fit); i++)
                {
                    var c = _snap.Partners[i];
                    _people.Add(new Person { X = start + i * 30, Body = c, Head = Color.Lerp(c, Color.white, 0.6f), Role = -1 });
                }
                return;
            }
            foreach (int role in RoleOrder)
                for (int k = 0; k < _snap.Staff[role] && _people.Count < fit; k++)
                    _people.Add(new Person { X = start + _people.Count * 30, Body = RoleBody[role], Head = RoleHead[role], Role = role });
        }

        void Place(VisualElement e, float x, float y, float w = -1, float h = -1)
        {
            e.style.left = _ox + x * _s;
            e.style.top = _oy + y * _s;
            if (w >= 0) e.style.width = w * _s;
            if (h >= 0) e.style.height = h * _s;
        }

        void Pool(List<Label> pool, int count, string cls)
        {
            while (pool.Count < count) pool.Add(Note(cls));
            for (int i = 0; i < pool.Count; i++) pool[i].style.display = i < count ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void PlaceLabels(Rect r)
        {
            var s = _snap;
            Pool(_captions, _slots.Count, "hq-caption");
            Pool(_signs, _slots.Count, "hq-sign");
            for (int i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                double cap = slot.Site.Capacity * (s.Shelving ? 1.25 : 1);
                _captions[i].text = (slot.Site.Count > 1 ? slot.Site.Count + " × " : "") + $"{ShortName(slot.Site.Kind)} · {Fmt.Int(cap)} units";
                Place(_captions[i], slot.X - 10, Feet + 5, slot.W + 20);
                bool sign = slot.Sign.width > 0;
                _signs[i].style.display = sign ? DisplayStyle.Flex : DisplayStyle.None;
                if (sign)
                {
                    _signs[i].text = s.Company;
                    _signs[i].style.fontSize = Mathf.Min(slot.Sign.height * 0.62f, 170f / Mathf.Max(6, s.Company.Length)) * _s;
                    Place(_signs[i], slot.Sign.x, slot.Sign.y, slot.Sign.width, slot.Sign.height);
                }
            }

            _ordersNote.text = s.Orders > 0
                ? $"{Fmt.Int(s.Orders)} orders today" + (s.Orders > s.FulfillCapacity ? $"\n(we can ship {Fmt.Int(s.FulfillCapacity)})" : "")
                : "no orders today";
            // The dock column: today's orders above the van, the backlog pile's label below them.
            Place(_ordersNote, VanParkX - 2, G - (s.Orders > s.FulfillCapacity ? 116 : 100));

            _backlogNote.style.display = s.Backlog > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _backlogNote.text = $"{Fmt.Int(s.Backlog)} waiting";
            Place(_backlogNote, _dockX - 4, G - 64);

            int crowd = s.StaffTotal - (s.StaffTotal == 0 ? 0 : _people.Count);
            _crowdNote.style.display = crowd > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _crowdNote.text = $"+{crowd} more";
            if (_people.Count > 0) Place(_crowdNote, _people[_people.Count - 1].X + 13, Feet - 18);

            _price.style.display = _ghostX >= 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_ghostX >= 0)
            {
                _price.text = $"For sale: {ShortName(s.NextKind)} · {Fmt.Money(s.NextCost)}";
                Place(_price, _ghostX + WidthOf(s.NextKind) / 2, G - 36 - 30 / _s);
            }

            bool nearlyFull = s.Fill >= 0.92f;
            _sticky.style.display = s.Overflow || nearlyFull ? DisplayStyle.Flex : DisplayStyle.None;
            _sticky.text = s.Overflow
                ? $"{Fmt.Int(s.Incoming)} units are on the way, but there's only room for {Fmt.Int(s.Free)}. The rest gets turned away!"
                : $"Storage {Fmt.Pct(s.Fill)} full. Time for more space?";
            _sticky.style.left = r.xMax - 226;
            _sticky.style.top = r.yMin + 6;

            _parcel.style.width = 11 * _s;
            _parcel.style.height = 9 * _s;
            _van.style.width = (VanLength + 36) * _s;
            _van.style.height = 56 * _s;
            foreach (var c in _clouds) { c.style.width = 66 * _s; c.style.height = 30 * _s; }
        }

        // ------------------------------------------------------------------ animation

        void Tick(TimerState ts)
        {
            if (!Ready) return;
            float dt = Mathf.Clamp(ts.deltaTime / 1000f, 0f, 0.1f);
            if (!_snap.Paused) _t += dt;

            float span = _designW + 90;
            for (int i = 0; i < _clouds.Length; i++)
            {
                float cx = (i * 0.52f * span + _t * (4f + 2.5f * i)) % span - 70;
                _clouds[i].style.translate = new Translate(_ox + cx * _s, _oy + (8 + i * 22) * _s);
            }

            // The van loads at the dock (parcels fly out of the biggest building's door), drives off, and a fresh one pulls in.
            float park = VanParkX, vx = park, alpha = 1;
            bool moving = false, parcel = false;
            Vector2 parcelAt = default;
            if (_snap.Orders > 0)
            {
                float period = Mathf.Lerp(10f, 4f, Mathf.Clamp01(_snap.Orders / 250f));
                float ph = _t % period / period;
                if (ph < 0.4f)
                {
                    parcel = true;
                    float u = ph / 0.4f * 3f % 1f;
                    var from = DoorPoint;
                    var to = new Vector2(park + VanLength * 0.5f, G - 44);
                    var ctrl = new Vector2((from.x + to.x) * 0.5f, Mathf.Min(from.y, to.y) - 60);
                    parcelAt = Bezier(from, ctrl, to, u);
                }
                else if (ph < 0.85f)
                {
                    float u = (ph - 0.4f) / 0.45f;
                    vx = park + u * u * (_designW - park + 30);
                    moving = true;
                }
                else alpha = (ph - 0.85f) / 0.15f;
            }
            _van.style.translate = new Translate(_ox + (vx - 26) * _s, _oy + (G - 54) * _s);
            _van.style.opacity = alpha;
            if (moving != _vanMoving)
            {
                _vanMoving = moving;
                _van.MarkDirtyRepaint();
            }
            _parcel.style.display = parcel ? DisplayStyle.Flex : DisplayStyle.None;
            if (parcel) _parcel.style.translate = new Translate(_ox + (parcelAt.x - 5.5f) * _s, _oy + (parcelAt.y - 4.5f) * _s);
        }

        // ------------------------------------------------------------------ drawing helpers (design units)

        /// <summary>Starts a layer: design units map to pixels from the given origin at the current scale.</summary>
        Painter2D Begin(MeshGenerationContext ctx, float originX, float originY)
        {
            _px = originX;
            _py = originY;
            _ink = Ink;
            var p = ctx.painter2D;
            p.lineJoin = LineJoin.Round;
            p.lineCap = LineCap.Round;
            return p;
        }

        Vector2 P(float x, float y) => new Vector2(_px + x * _s, _py + y * _s);

        static Vector2 Bezier(Vector2 a, Vector2 c, Vector2 b, float t)
        {
            float u = 1 - t;
            return u * u * a + 2 * u * t * c + t * t * b;
        }

        /// <summary>Deterministic 0..1 noise, so the hand-drawn wobble is the same on every repaint.</summary>
        static float Jit(int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u;
                h ^= h >> 13;
                h *= 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        void Paint(Painter2D p, Color fill, float lw)
        {
            if (fill.a > 0) { p.fillColor = fill; p.Fill(); }
            if (lw > 0) { p.strokeColor = _ink; p.lineWidth = lw * _s; p.Stroke(); }
        }

        /// <summary>Adds a quadratic curve from a (the current point) to b as a short polyline.</summary>
        void CurveTo(Painter2D p, Vector2 a, Vector2 c, Vector2 b, int segments)
        {
            for (int i = 1; i <= segments; i++)
            {
                var q = Bezier(a, c, b, i / (float)segments);
                p.LineTo(P(q.x, q.y));
            }
        }

        /// <summary>On longer edges, a slightly nudged midpoint: the hand-drawn wobble, for two segments per edge.</summary>
        void Nudge(Painter2D p, Vector2 a, Vector2 b, int seed)
        {
            var d = b - a;
            float len = d.magnitude;
            if (len < 16f) return;
            var n = new Vector2(-d.y, d.x) / len;
            var m = (a + b) * 0.5f + n * ((Jit(seed) - 0.5f) * Mathf.Min(1.4f, len * 0.012f));
            p.LineTo(P(m.x, m.y));
        }

        /// <summary>A closed hand-drawn polygon.</summary>
        void Shape(Painter2D p, Color fill, float lw, int seed, params Vector2[] pts)
        {
            p.BeginPath();
            p.MoveTo(P(pts[0].x, pts[0].y));
            for (int i = 1; i < pts.Length; i++)
            {
                Nudge(p, pts[i - 1], pts[i], seed + i * 31);
                p.LineTo(P(pts[i].x, pts[i].y));
            }
            Nudge(p, pts[pts.Length - 1], pts[0], seed + pts.Length * 31);
            p.ClosePath();
            Paint(p, fill, lw);
        }

        /// <summary>A closed polygon with exact edges (vehicles, small parts).</summary>
        void Poly(Painter2D p, Color fill, float lw, params Vector2[] pts)
        {
            p.BeginPath();
            p.MoveTo(P(pts[0].x, pts[0].y));
            for (int i = 1; i < pts.Length; i++) p.LineTo(P(pts[i].x, pts[i].y));
            p.ClosePath();
            Paint(p, fill, lw);
        }

        void Box(Painter2D p, float x, float y, float w, float h, Color fill, float lw, int seed)
            => Shape(p, fill, lw, seed, new Vector2(x, y), new Vector2(x + w, y), new Vector2(x + w, y + h), new Vector2(x, y + h));

        void Line(Painter2D p, float x1, float y1, float x2, float y2, float lw, Color color)
        {
            if (Mathf.Abs(x2 - x1) + Mathf.Abs(y2 - y1) < 0.01f) return;
            p.BeginPath();
            p.MoveTo(P(x1, y1));
            p.LineTo(P(x2, y2));
            p.strokeColor = color;
            p.lineWidth = lw * _s;
            p.Stroke();
        }

        void Line(Painter2D p, float x1, float y1, float x2, float y2, float lw) => Line(p, x1, y1, x2, y2, lw, _ink);

        /// <summary>A circle as a polygon: Painter2D arcs cost hundreds of vertices each.</summary>
        void Circle(Painter2D p, float cx, float cy, float r, Color fill, float lw)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(5 + r * _s * 0.9f), 6, 28);
            p.BeginPath();
            p.MoveTo(P(cx + r, cy));
            for (int i = 1; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                p.LineTo(P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
            p.ClosePath();
            Paint(p, fill, lw);
        }

        /// <summary>Dashes along a polyline, all in one stroke with flat ends.</summary>
        void Dashed(Painter2D p, float lw, Color color, params Vector2[] pts)
        {
            p.BeginPath();
            for (int i = 1; i < pts.Length; i++)
            {
                var a = pts[i - 1];
                var b = pts[i];
                float len = Vector2.Distance(a, b);
                for (float d = 0; d < len; d += 11f)
                {
                    var s1 = Vector2.Lerp(a, b, d / len);
                    var s2 = Vector2.Lerp(a, b, Mathf.Min(len, d + 6f) / len);
                    p.MoveTo(P(s1.x, s1.y));
                    p.LineTo(P(s2.x, s2.y));
                }
            }
            p.strokeColor = color;
            p.lineWidth = lw * _s;
            p.lineCap = LineCap.Butt;
            p.Stroke();
            p.lineCap = LineCap.Round;
        }

        /// <summary>A marker stroke: an ink line with a colored line on top.</summary>
        void Marker(Painter2D p, float lw, Color color, params Vector2[] pts)
        {
            for (int pass = 0; pass < 2; pass++)
            {
                p.BeginPath();
                p.MoveTo(P(pts[0].x, pts[0].y));
                for (int i = 1; i < pts.Length; i++) p.LineTo(P(pts[i].x, pts[i].y));
                p.strokeColor = pass == 0 ? _ink : color;
                p.lineWidth = (pass == 0 ? lw + 2.6f : lw) * _s;
                p.Stroke();
            }
        }

        void Carton(Painter2D p, float x, float y, float w, float h, int seed)
        {
            Box(p, x, y, w, h, Cardboard, 1.5f, seed);
            Line(p, x + w * 0.5f, y, x + w * 0.5f, y + h * 0.35f, 1.1f);
        }

        /// <summary>Stacks cartons in a door opening from the floor up; fill is 0..1 of the company's storage.</summary>
        void Stock(Painter2D p, float x, float y, float w, float h, float fill, int seed)
        {
            const float bw = 17, bh = 13;
            int cols = Mathf.Max(1, Mathf.FloorToInt((w - 4) / (bw + 2)));
            int rows = Mathf.Max(1, Mathf.FloorToInt((h - 4) / (bh + 1)));
            int n = Mathf.RoundToInt(Mathf.Clamp01(fill) * cols * rows);
            if (fill > 0.01f && n == 0) n = 1;
            for (int k = 0; k < n; k++)
            {
                int row = k / cols, col = k % cols;
                float jx = (Jit(seed + k * 7) - 0.5f) * 3f;
                Carton(p, x + 3 + col * (bw + 2) + jx, y + h - (row + 1) * (bh + 1), bw, bh, seed + k * 13);
            }
            if (_snap.Shelving)
                for (int row = 2; row < rows; row += 2)
                    Line(p, x, y + h - row * (bh + 1) - 0.5f, x + w, y + h - row * (bh + 1) - 0.5f, 2f);
        }

        /// <summary>The company logo: a pie with one slice per partner, in their colors.</summary>
        void Pie(Painter2D p, float cx, float cy, float r, float lw)
        {
            var colors = _snap.Partners.Count > 0 ? _snap.Partners : DefaultPie;
            int n = Mathf.Min(4, colors.Count);
            int segments = Mathf.Max(3, 16 / n);
            for (int i = 0; i < n; i++)
            {
                float a0 = (-90 + i * 360f / n) * Mathf.Deg2Rad, a1 = (-90 + (i + 1) * 360f / n) * Mathf.Deg2Rad;
                p.BeginPath();
                p.MoveTo(P(cx, cy));
                for (int k = 0; k <= segments; k++)
                {
                    float a = Mathf.Lerp(a0, a1, k / (float)segments);
                    p.LineTo(P(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
                }
                p.ClosePath();
                p.fillColor = colors[i];
                p.Fill();
                p.strokeColor = _ink;
                p.lineWidth = lw * _s;
                p.Stroke();
            }
        }

        // ------------------------------------------------------------------ layers

        void DrawBack(MeshGenerationContext ctx)
        {
            if (!Ready) return;
            var p = Begin(ctx, _ox, _oy);
            DrawSky(p);
            DrawBackdrop(p);
            if (_factoryX >= 0) DrawFactory(p, _factoryX);
            if (_ghostX >= 0) DrawGhost(p, _snap.NextKind, _ghostX);
            DrawGround(p);
        }

        void DrawHouse(MeshGenerationContext ctx, int index)
        {
            if (!Ready || index >= _slots.Count) return;
            var p = Begin(ctx, _ox, _oy);
            var slot = _slots[index];
            bool biggest = index == _slots.Count - 1;
            int seed = 1000 * (index + 1);
            switch (slot.Site.Kind)
            {
                case "garage": DrawGarage(p, slot.X, seed); break;
                case "small": DrawSmall(p, slot.X, seed, biggest); break;
                case "regional": DrawRegional(p, slot.X, seed, biggest); break;
                default: DrawMega(p, slot.X, seed, biggest); break;
            }
            if (!biggest) return;
            DrawFlag(p, slot);
            if (_snap.Building != null) DrawCrane(p, slot);
        }

        void DrawFront(MeshGenerationContext ctx)
        {
            if (!Ready) return;
            var p = Begin(ctx, _ox, _oy);
            if (_snap.Backlog > 0) DrawBacklog(p);
            if (_snap.WebStore) DrawShopSign(p, _dockEnd - 22);
            foreach (var person in _people) DrawPerson(p, person);
        }

        // ------------------------------------------------------------------ the scene

        void DrawSky(Painter2D p)
        {
            Circle(p, 36, 30, 14, Yellow, 2.2f);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                Line(p, 36 + Mathf.Cos(a) * 19, 30 + Mathf.Sin(a) * 19, 36 + Mathf.Cos(a) * 24, 30 + Mathf.Sin(a) * 24, 1.8f);
            }
            foreach (var (bx, by) in new[] { (190f, 30f), (210f, 22f) })
            {
                p.BeginPath();
                p.MoveTo(P(bx, by));
                CurveTo(p, new Vector2(bx, by), new Vector2(bx + 4, by - 5), new Vector2(bx + 8, by), 3);
                CurveTo(p, new Vector2(bx + 8, by), new Vector2(bx + 12, by - 5), new Vector2(bx + 16, by), 3);
                p.strokeColor = _ink;
                p.lineWidth = 1.6f * _s;
                p.Stroke();
            }
            if (_snap.International)
            {
                // Selling worldwide: a little plane with a dashed trail.
                float px = _designW * 0.62f, py = 26;
                Dashed(p, 1.4f, Faded, new Vector2(px - 130, py + 12), new Vector2(px - 10, py + 3));
                Poly(p, Card, 1.8f, new Vector2(px - 8, py), new Vector2(px + 22, py - 2), new Vector2(px + 28, py + 2), new Vector2(px + 22, py + 5), new Vector2(px - 8, py + 5));
                Poly(p, Blue, 1.6f, new Vector2(px + 4, py + 2), new Vector2(px + 12, py + 2), new Vector2(px + 2, py + 13));
                Poly(p, Blue, 1.6f, new Vector2(px - 8, py), new Vector2(px - 3, py), new Vector2(px - 10, py - 8));
            }
        }

        /// <summary>Faded hills and trees, so the open lots read as countryside rather than blank paper.</summary>
        void DrawBackdrop(Painter2D p)
        {
            const float step = 170;
            var cur = new Vector2(0, G - 24);
            p.BeginPath();
            p.MoveTo(P(cur.x, cur.y));
            for (float x = 0; x < _designW; x += step)
            {
                var next = new Vector2(x + step, G - 22 - Jit((int)x + 12) * 12);
                CurveTo(p, cur, new Vector2(x + step / 2, G - 44 - Jit((int)x + 11) * 30), next, 6);
                cur = next;
            }
            p.strokeColor = Rule;
            p.lineWidth = 2.2f * _s;
            p.Stroke();

            _ink = Faded;
            int k = 0;
            for (float x = 64; x < _designW - 16; k++)
            {
                float h = 36 + Jit(k * 17 + 3) * 24, r = 9 + Jit(k * 17 + 4) * 6;
                Line(p, x, G, x, G - h + r, 2f);
                Circle(p, x, G - h, r, Leaf, 1.8f);
                if (Jit(k * 17 + 5) > 0.5f) Circle(p, x + r * 0.95f, G - h + r * 0.9f, r * 0.7f, Leaf, 1.6f);
                x += 80 + Jit(k * 17 + 6) * 120;
            }
            _ink = Ink;
        }

        void DrawGround(Painter2D p)
        {
            const float step = 90;
            var cur = new Vector2(0, G);
            p.BeginPath();
            p.MoveTo(P(cur.x, cur.y));
            for (float x = step; x < _designW + step; x += step)
            {
                var next = new Vector2(x, G);
                CurveTo(p, cur, new Vector2(x - step / 2, G + (Jit((int)x) - 0.5f) * 3f), next, 3);
                cur = next;
            }
            p.strokeColor = _ink;
            p.lineWidth = 2.6f * _s;
            p.Stroke();
            // the road the van takes
            Dashed(p, 1.8f, Rule, new Vector2(_dockX - 4, G + 10), new Vector2(_designW, G + 10));
            for (float x = 60; x < _designW; x += 170)
            {
                float tx = x + Jit((int)x + 5) * 40;
                Line(p, tx, G, tx + 3, G - 7, 1.6f);
                Line(p, tx + 3, G - 7, tx + 6, G, 1.6f);
            }
        }

        void DrawGarage(Painter2D p, float x, int seed)
        {
            Box(p, x, G - 96, 150, 96, Card, 2.6f, seed);
            Shape(p, Tomato, 2.6f, seed + 1, new Vector2(x - 12, G - 92), new Vector2(x + 75, G - 140), new Vector2(x + 162, G - 92));
            Line(p, x + 24, G - 100, x + 31, G - 110, 1.6f);
            Line(p, x + 52, G - 116, x + 59, G - 126, 1.6f);
            Line(p, x + 97, G - 118, x + 90, G - 128, 1.6f);
            Line(p, x + 127, G - 101, x + 120, G - 111, 1.6f);
            Circle(p, x + 75, G - 112, 8, Glass, 2f);
            Line(p, x + 75, G - 120, x + 75, G - 104, 1.2f);
            Line(p, x + 67, G - 112, x + 83, G - 112, 1.2f);
            Box(p, x + 22, G - 78, 106, 78, Interior, 2.4f, seed + 2);
            Stock(p, x + 25, G - 64, 100, 64, _snap.Fill, seed + 3);
            Box(p, x + 22, G - 78, 106, 12, Card, 2f, seed + 4);
            Line(p, x + 22, G - 72, x + 128, G - 72, 1f);
        }

        void DrawSmall(Painter2D p, float x, int seed, bool biggest)
        {
            Box(p, x, G - 118, 210, 118, Card, 2.6f, seed);
            Box(p, x - 5, G - 127, 220, 9, Deep, 2.2f, seed + 1);
            for (int i = 0; i < 5; i++)
                Shape(p, Glass, 2f, seed + 10 + i, new Vector2(x + 10 + i * 42, G - 127), new Vector2(x + 30 + i * 42, G - 145), new Vector2(x + 30 + i * 42, G - 127));
            Box(p, x + 55, G - 112, 100, 20, Cardboard, 2f, seed + 2);
            Box(p, x + 170, G - 104, 28, 17, Glass, 1.8f, seed + 3);
            Line(p, x + 184, G - 104, x + 184, G - 87, 1.2f);
            if (biggest && _snap.Brand) Pie(p, x + 26, G - 98, 11, 1.6f);
            Box(p, x + 36, G - 84, 124, 84, Interior, 2.4f, seed + 4);
            Stock(p, x + 39, G - 73, 118, 73, _snap.Fill, seed + 5);
            Box(p, x + 36, G - 84, 124, 10, Card, 2f, seed + 6);
        }

        void DrawRegional(Painter2D p, float x, int seed, bool biggest)
        {
            Box(p, x, G - 136, 280, 136, Card, 2.6f, seed);
            Box(p, x, G - 136, 280, 16, Blue, 2.2f, seed + 1);
            Box(p, x + 90, G - 114, 100, 20, Cardboard, 2f, seed + 2);
            if (biggest && _snap.Brand) Pie(p, x + 50, G - 104, 11, 1.6f);
            for (int i = 0; i < 5; i++) Box(p, x + 16 + i * 54, G - 86, 26, 14, Glass, 1.8f, seed + 10 + i);
            for (int i = 0; i < 3; i++)
            {
                float dx = x + 16 + i * 90;
                Box(p, dx, G - 66, 76, 66, Interior, 2.4f, seed + 20 + i);
                Stock(p, dx + 3, G - 57, 70, 57, _snap.Fill, seed + 30 + i * 17);
                Box(p, dx, G - 66, 76, 9, Card, 1.8f, seed + 25 + i);
            }
        }

        void DrawMega(Painter2D p, float x, int seed, bool biggest)
        {
            for (int i = 0; i < 5; i++)
            {
                float px = x + 62 + i * 56;
                Poly(p, Solar, 1.8f, new Vector2(px, G - 148), new Vector2(px + 46, G - 148), new Vector2(px + 38, G - 162), new Vector2(px - 8, G - 162));
                Line(p, px + 19, G - 148, px + 12, G - 162, 1f, Glass);
                Line(p, px - 4, G - 155, px + 42, G - 155, 1f, Glass);
            }
            Box(p, x, G - 148, 360, 148, Card, 2.6f, seed);
            Box(p, x + 140, G - 126, 80, 126, Glass, 2.4f, seed + 1);
            Line(p, x + 167, G - 126, x + 167, G, 1.4f);
            Line(p, x + 193, G - 126, x + 193, G, 1.4f);
            Line(p, x + 140, G - 84, x + 220, G - 84, 1.4f);
            Line(p, x + 140, G - 42, x + 220, G - 42, 1.4f);
            Box(p, x + 124, G - 146, 112, 18, Cardboard, 2f, seed + 2);
            if (biggest && _snap.Brand) Pie(p, x + 44, G - 104, 12, 1.6f);
            float[] docks = { x + 14, x + 74, x + 232, x + 292 };
            for (int i = 0; i < docks.Length; i++)
            {
                Box(p, docks[i], G - 62, 56, 62, Interior, 2.4f, seed + 20 + i);
                if (i == 0 && _snap.Robots) DrawRobot(p, docks[i]);
                else Stock(p, docks[i] + 3, G - 54, 50, 54, _snap.Fill, seed + 30 + i * 17);
                Box(p, docks[i], G - 62, 56, 8, Card, 1.8f, seed + 25 + i);
            }
        }

        void DrawRobot(Painter2D p, float dx)
        {
            Box(p, dx + 12, G - 9, 22, 9, Orange, 1.8f, 901);
            Marker(p, 4f, Orange, new Vector2(dx + 23, G - 9), new Vector2(dx + 28, G - 34), new Vector2(dx + 44, G - 42));
            Circle(p, dx + 28, G - 34, 3.2f, Card, 1.6f);
            Line(p, dx + 44, G - 42, dx + 49, G - 36, 2f);
            Line(p, dx + 44, G - 42, dx + 51, G - 44, 2f);
            Carton(p, dx + 36, G - 34, 15, 11, 903);
        }

        void DrawFlag(Painter2D p, Slot slot)
        {
            if (_snap.Stage < 2 && !_snap.Public) return;
            float px = slot.X + 20, py = G - 148;
            switch (slot.Site.Kind)
            {
                case "garage": px = slot.X + 75; py = G - 140; break;
                case "small": px = slot.X + 206; py = G - 127; break;
                case "regional": px = slot.X + 264; py = G - 136; break;
            }
            Line(p, px, py, px, py - 26, 2f);
            var color = _snap.Public ? Lilac : _snap.Stage >= 4 ? Green : Yellow;
            Shape(p, color, 1.8f, 555, new Vector2(px, py - 26), new Vector2(px + 20, py - 21), new Vector2(px, py - 15));
        }

        void DrawCrane(Painter2D p, Slot slot)
        {
            float cx = slot.X + slot.W - 30;
            float top = Mathf.Max(8, G - TopOf(slot.Site.Kind) - 22);
            Line(p, cx - 4, G, cx - 4, top, 2f);
            Line(p, cx + 4, G, cx + 4, top, 2f);
            for (float y = G; y > top + 8; y -= 12) Line(p, cx - 4, y, cx + 4, y - 12, 1.3f);
            Marker(p, 3f, Yellow, new Vector2(cx - 72, top), new Vector2(cx + 24, top));
            Box(p, cx + 10, top - 1, 16, 10, Deep, 1.6f, 707);
            Line(p, cx - 52, top, cx - 52, top + 30, 1.4f);
            Carton(p, cx - 60, top + 30, 16, 12, 708);
        }

        void DrawBacklog(Painter2D p)
        {
            int n = Mathf.Clamp(1 + _snap.Backlog / 25, 1, 6);
            float x = _dockX;
            float[,] spots = { { 0, 13 }, { 18, 13 }, { 36, 13 }, { 9, 26 }, { 27, 26 }, { 18, 39 } };
            for (int i = 0; i < n; i++) Carton(p, x + spots[i, 0], G - spots[i, 1], 17, 13, 600 + i);
        }

        /// <summary>The own web store: a signpost with a shopping cart on a screen.</summary>
        void DrawShopSign(Painter2D p, float x)
        {
            Line(p, x, G, x, G - 40, 2.2f);
            Box(p, x - 17, G - 66, 34, 26, Card, 2f, 811);
            Line(p, x - 10, G - 60, x - 6, G - 60, 1.4f);
            Line(p, x - 6, G - 60, x - 3, G - 50, 1.4f);
            Line(p, x - 3, G - 50, x + 8, G - 50, 1.4f);
            Line(p, x - 5, G - 56, x + 10, G - 56, 1.4f);
            Line(p, x + 10, G - 56, x + 8, G - 50, 1.4f);
            Circle(p, x - 1, G - 47, 1.4f, _ink, 0);
            Circle(p, x + 7, G - 47, 1.4f, _ink, 0);
        }

        /// <summary>The next warehouse type, as a dashed outline on an empty lot (the price sign is a button).</summary>
        void DrawGhost(Painter2D p, string kind, float x)
        {
            float w = WidthOf(kind), h = WallOf(kind);
            Dashed(p, 2f, Ghost, new Vector2(x, G), new Vector2(x, G - h), new Vector2(x + w, G - h), new Vector2(x + w, G));
            if (kind == "small") Dashed(p, 1.6f, Ghost, new Vector2(x - 5, G - h - 9), new Vector2(x + w + 5, G - h - 9));
            float mid = x + w / 2;
            Line(p, mid, G, mid, G - 36, 2.4f);
        }

        void DrawFactory(Painter2D p, float x)
        {
            _ink = Faded;
            Box(p, x + 102, G - 112, 14, 44, Haze, 2f, 931);
            Circle(p, x + 110, G - 122, 7, Paper, 1.5f);
            Circle(p, x + 118, G - 136, 9, Paper, 1.5f);
            Circle(p, x + 129, G - 152, 11, Paper, 1.5f);
            Shape(p, Haze, 2f, 932,
                new Vector2(x, G), new Vector2(x, G - 70), new Vector2(x + 30, G - 90), new Vector2(x + 30, G - 70), new Vector2(x + 60, G - 90),
                new Vector2(x + 60, G - 70), new Vector2(x + 90, G - 90), new Vector2(x + 90, G - 70), new Vector2(x + 130, G - 70), new Vector2(x + 130, G));
            _ink = Ink;
        }

        void DrawPerson(Painter2D p, Person person)
        {
            float x = person.X, f = Feet;
            var foot = new Vector2(x - 9, f);
            var top = new Vector2(x, f - 22);
            p.BeginPath();
            p.MoveTo(P(foot.x, foot.y));
            CurveTo(p, foot, new Vector2(x - 10, f - 20), top, 5);
            CurveTo(p, top, new Vector2(x + 10, f - 20), new Vector2(x + 9, f), 5);
            p.ClosePath();
            Paint(p, person.Body, 2f);
            Circle(p, x, f - 29, 7.5f, person.Head, 2f);
            Circle(p, x - 2.6f, f - 30, 1.1f, _ink, 0);
            Circle(p, x + 2.6f, f - 30, 1.1f, _ink, 0);
            p.BeginPath();
            p.MoveTo(P(x - 2.6f, f - 26.6f));
            CurveTo(p, new Vector2(x - 2.6f, f - 26.6f), new Vector2(x, f - 24.8f), new Vector2(x + 2.6f, f - 26.6f), 3);
            p.strokeColor = _ink;
            p.lineWidth = 1.2f * _s;
            p.Stroke();

            switch (person.Role)
            {
                case -1: // a partner, packing boxes themselves
                    Carton(p, x - 8, f - 17, 16, 11, (int)x);
                    break;
                case 0: // warehouse: cap and a carton
                    Poly(p, Solar, 1.6f, new Vector2(x - 7.5f, f - 31), new Vector2(x - 5, f - 37.5f), new Vector2(x + 5, f - 37.5f), new Vector2(x + 7.5f, f - 31));
                    Line(p, x + 2, f - 31.5f, x + 11, f - 30.5f, 2f);
                    Carton(p, x - 8, f - 17, 16, 11, (int)x + 1);
                    break;
                case 1: // support: headset
                    p.BeginPath();
                    p.MoveTo(P(x - 7.5f, f - 29));
                    CurveTo(p, new Vector2(x - 7.5f, f - 29), new Vector2(x, f - 42), new Vector2(x + 7.5f, f - 29), 5);
                    p.strokeColor = _ink;
                    p.lineWidth = 1.6f * _s;
                    p.Stroke();
                    Line(p, x - 7.5f, f - 28, x - 3.5f, f - 24.5f, 1.4f);
                    Circle(p, x - 7.8f, f - 29, 2f, _ink, 0);
                    break;
                case 2: // marketing: megaphone
                    Poly(p, Tomato, 1.4f, new Vector2(x + 7, f - 18), new Vector2(x + 15, f - 22), new Vector2(x + 15, f - 10), new Vector2(x + 7, f - 14));
                    break;
                case 3: // buyer: clipboard
                    Box(p, x - 6, f - 17, 11, 13, Card, 1.4f, (int)x);
                    Line(p, x - 3.5f, f - 13, x + 2.5f, f - 13, 1f);
                    Line(p, x - 3.5f, f - 9.5f, x + 2.5f, f - 9.5f, 1f);
                    break;
                case 4: // analyst: glasses
                    Circle(p, x - 2.7f, f - 30, 2.7f, NoFill, 1.2f);
                    Circle(p, x + 2.7f, f - 30, 2.7f, NoFill, 1.2f);
                    break;
                default: // accountant: the ledger
                    Box(p, x - 6, f - 17, 12, 10, Green, 1.4f, (int)x);
                    Line(p, x, f - 17, x, f - 7, 1f);
                    break;
            }
        }

        void DrawVan(MeshGenerationContext ctx)
        {
            if (_snap == null) return;
            var p = Begin(ctx, 0, 0);
            // Cargo box from x0 to x1, then the cab. Express delivery gets the longer truck.
            float x0 = 26, x1 = x0 + VanLength - 22;
            if (_vanMoving)
                foreach (var y in new[] { 16f, 27f, 38f }) Line(p, 3, y, 17, y, 2f);
            Poly(p, Card, 2.4f, new Vector2(x0, 46), new Vector2(x0, 12), new Vector2(x0 + 5, 7), new Vector2(x1, 7), new Vector2(x1, 46));
            Poly(p, Tomato, 1.2f, new Vector2(x0, 29), new Vector2(x1, 29), new Vector2(x1, 34), new Vector2(x0, 34));
            Poly(p, Card, 2.4f, new Vector2(x1, 46), new Vector2(x1, 20), new Vector2(x1 + 17, 20), new Vector2(x1 + 31, 34), new Vector2(x1 + 31, 46));
            Poly(p, Glass, 1.8f, new Vector2(x1 + 5, 24), new Vector2(x1 + 15, 24), new Vector2(x1 + 25, 34), new Vector2(x1 + 5, 34));
            Pie(p, (x0 + x1) * 0.5f, 19, 6.5f, 1.3f);
            foreach (var wx in new[] { x0 + 12, x1 + 17 })
            {
                Circle(p, wx, 47, 7, Ink, 0);
                Circle(p, wx, 47, 2.6f, Card, 0);
            }
        }

        void DrawCloud(MeshGenerationContext ctx)
        {
            var p = Begin(ctx, 0, 0);
            var pts = new[] { new Vector2(4, 26), new Vector2(18, 16), new Vector2(37, 10), new Vector2(54, 16), new Vector2(62, 26) };
            var ctrl = new[] { new Vector2(6, 14), new Vector2(24, 5), new Vector2(50, 4), new Vector2(64, 17) };
            p.BeginPath();
            p.MoveTo(P(pts[0].x, pts[0].y));
            for (int i = 0; i < ctrl.Length; i++) CurveTo(p, pts[i], ctrl[i], pts[i + 1], 4);
            p.ClosePath();
            Paint(p, Card, 2f);
        }
    }
}
