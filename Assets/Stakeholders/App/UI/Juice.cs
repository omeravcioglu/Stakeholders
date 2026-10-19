using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App.UI
{
    /// <summary>Procedural sound effects: every sound is synthesized at startup, so no audio assets are needed.</summary>
    public static class Sfx
    {
        const string PrefKey = "stk_sfx";
        static AudioSource _source;
        static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        static float _lastClick;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(PrefKey, 1) == 1;
            set
            {
                if (Automation.Active) return;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void Init(GameObject host)
        {
            if (_source != null) return;
            if (UnityEngine.Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0;
            _source.volume = 0.35f;
            Clips["click"] = Make("click", new[] { 1250f }, 0.025f, 0.2f, 0.05f);
            Clips["coin"] = Make("coin", new[] { 988f, 1319f }, 0.07f, 0.5f, 0.35f);
            Clips["good"] = Make("good", new[] { 523f, 659f, 784f }, 0.07f, 0.35f, 0.3f);
            Clips["warn"] = Make("warn", new[] { 440f, 349f }, 0.11f, 0.5f, 0.25f);
            Clips["bad"] = Make("bad", new[] { 233f, 196f }, 0.16f, 0.7f, 0.2f);
            Clips["secret"] = Make("secret", new[] { 740f, 587f, 740f }, 0.09f, 0f, 0.4f);
            Clips["level"] = Make("level", new[] { 523f, 659f, 784f, 1047f }, 0.08f, 0.35f, 0.35f);
            Clips["fanfare"] = Make("fanfare", new[] { 392f, 523f, 659f, 784f, 659f, 784f, 1047f }, 0.11f, 0.4f, 0.45f, 3.5f);
        }

        /// <summary>A short melody. <paramref name="square"/> blends a sine (0) toward a square wave (1); <paramref name="ring"/> sets how long notes sustain.</summary>
        static AudioClip Make(string name, float[] notes, float noteLen, float square, float ring, float lastHold = 1f)
        {
            const int rate = 44100;
            var lengths = new int[notes.Length];
            int total = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                lengths[i] = (int)(rate * noteLen * (i == notes.Length - 1 ? lastHold : 1f));
                total += lengths[i];
            }
            var data = new float[total];
            int offset = 0;
            for (int i = 0; i < notes.Length; i++)
            {
                float f = notes[i];
                float decay = noteLen * (0.3f + ring) * (i == notes.Length - 1 ? lastHold : 1f);
                for (int s = 0; s < lengths[i]; s++)
                {
                    float t = s / (float)rate;
                    float attack = Mathf.Min(1f, t / 0.004f);
                    float env = attack * Mathf.Exp(-t / Mathf.Max(0.005f, decay));
                    float sine = Mathf.Sin(2f * Mathf.PI * f * t);
                    float wave = (1f - square) * sine + square * Mathf.Sign(sine) * 0.55f;
                    data[offset + s] = wave * env * 0.45f;
                }
                offset += lengths[i];
            }
            var clip = AudioClip.Create(name, total, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static void Play(string name, float volume = 1f)
        {
            if (_source == null || Automation.Active || !Enabled) return;
            if (Clips.TryGetValue(name, out var clip)) _source.PlayOneShot(clip, volume);
        }

        public static void Click()
        {
            if (Time.unscaledTime - _lastClick < 0.05f) return;
            _lastClick = Time.unscaledTime;
            Play("click", 0.45f);
        }

        public static void ForNotice(NoticeKind kind)
        {
            switch (kind)
            {
                case NoticeKind.Success: Play("coin", 0.8f); break;
                case NoticeKind.Warning: Play("warn", 0.7f); break;
                case NoticeKind.Danger: Play("bad", 0.8f); break;
                case NoticeKind.Private: Play("secret", 0.7f); break;
                case NoticeKind.Xp: Play("good", 0.6f); break;
            }
        }
    }

    /// <summary>Milestone celebrations: a big moment with confetti and a fanfare.</summary>
    public static class Celebration
    {
        static readonly Color[] Colors = { Notebook.MarkerYellow, Notebook.MarkerGreen, Notebook.MarkerBlue, Notebook.MarkerLilac, Notebook.MarkerPink, Notebook.MarkerTeal };

        class Piece
        {
            public VisualElement El;
            public float X, Y, Vx, Vy, Rot, Vr, Wobble;
        }

        /// <summary>Shows "Title|Subtitle" as a celebration.</summary>
        public static void Show(UiRoot ui, string text)
        {
            string title = text ?? "", sub = "";
            int bar = title.IndexOf('|');
            if (bar >= 0) { sub = title.Substring(bar + 1); title = title.Substring(0, bar); }
            var m = ui.ShowModal("");
            m.Panel.AddToClassList("celebrate");
            m.Body.Add(Ui.Text("MILESTONE", "celebrate-kicker"));
            m.Body.Add(Ui.Text(title, "celebrate-title"));
            if (sub.Length > 0) m.Body.Add(Ui.Text(sub, "celebrate-sub"));
            var ok = m.AddButton("Awesome!", m.Close, "btn-good");
            ok.AddToClassList("btn-lg");
            Confetti(m.Overlay);
            Sfx.Play("fanfare");
            if (Automation.Active) m.Overlay.schedule.Execute(m.Close).StartingIn(1200);
        }

        static void Confetti(VisualElement host)
        {
            var layer = new VisualElement { pickingMode = PickingMode.Ignore };
            layer.style.position = Position.Absolute;
            layer.style.left = 0; layer.style.top = 0; layer.style.right = 0; layer.style.bottom = 0;
            layer.style.overflow = Overflow.Hidden;
            host.Add(layer);
            var rng = new System.Random();
            var pieces = new List<Piece>();
            for (int i = 0; i < 110; i++)
            {
                var el = new VisualElement { pickingMode = PickingMode.Ignore };
                el.style.position = Position.Absolute;
                el.style.width = 6 + rng.Next(8);
                el.style.height = 4 + rng.Next(7);
                el.style.backgroundColor = Colors[rng.Next(Colors.Length)];
                el.style.borderTopLeftRadius = el.style.borderBottomRightRadius = 2;
                layer.Add(el);
                pieces.Add(new Piece
                {
                    El = el, X = (float)rng.NextDouble(), Y = -0.05f - (float)rng.NextDouble() * 0.7f,
                    Vx = ((float)rng.NextDouble() - 0.5f) * 0.08f, Vy = 0.22f + (float)rng.NextDouble() * 0.3f,
                    Rot = rng.Next(360), Vr = ((float)rng.NextDouble() - 0.5f) * 540f, Wobble = (float)rng.NextDouble() * 6f
                });
            }
            float elapsed = 0;
            IVisualElementScheduledItem tick = null;
            tick = layer.schedule.Execute(() =>
            {
                const float dt = 0.016f;
                elapsed += dt;
                float w = layer.resolvedStyle.width, h = layer.resolvedStyle.height;
                if (float.IsNaN(w) || w <= 0 || h <= 0) return;
                foreach (var p in pieces)
                {
                    p.Y += p.Vy * dt;
                    p.X += p.Vx * dt + Mathf.Sin(elapsed * 3f + p.Wobble) * 0.0008f;
                    p.Rot += p.Vr * dt;
                    p.El.style.left = p.X * w;
                    p.El.style.top = p.Y * h;
                    p.El.style.rotate = new Rotate(new Angle(p.Rot, AngleUnit.Degree));
                }
                if (elapsed > 4.5f)
                {
                    tick?.Pause();
                    layer.RemoveFromHierarchy();
                }
            }).Every(16);
        }
    }

    /// <summary>Achievements that persist across games on this computer.</summary>
    public static class Achievements
    {
        public class Def
        {
            public string Id, Name, Description;
        }

        public static readonly Def[] All =
        {
            new Def { Id = "first_launch", Name = "Open for Business", Description = "Launch your first product." },
            new Def { Id = "stage1", Name = "Online Shop", Description = "Grow a company to a $500K valuation." },
            new Def { Id = "stage2", Name = "Growing Brand", Description = "Reach a $2.5M valuation." },
            new Def { Id = "stage3", Name = "Scale-up", Description = "Reach a $10M valuation." },
            new Def { Id = "stage4", Name = "Market Leader", Description = "Reach a $35M valuation." },
            new Def { Id = "stage5", Name = "IPO Ready", Description = "Reach a $100M valuation." },
            new Def { Id = "ipo", Name = "Ring the Bell", Description = "Take a company public." },
            new Def { Id = "topdog", Name = "Top Dog", Description = "Become #1 on the industry leaderboard." },
            new Def { Id = "acquirer", Name = "Hostile Takeover", Description = "Acquire a rival company." },
            new Def { Id = "pro", Name = "Pro Move", Description = "Develop a product all the way to its Pro version." },
            new Def { Id = "contract", Name = "Signed, Sealed, Delivered", Description = "Deliver a B2B contract you signed." },
            new Def { Id = "tree", Name = "Fully Loaded", Description = "Own 12 company upgrades at once." },
            new Def { Id = "whistle", Name = "Whistleblower", Description = "Expose a partner's shady deal." },
            new Def { Id = "millionaire", Name = "Millionaire", Description = "Reach $1M in personal net worth." },
            new Def { Id = "ambition", Name = "Secret Agenda", Description = "Complete your secret ambition by the end of a game." },
            new Def { Id = "winner", Name = "Richest Partner", Description = "Finish a game with the highest net worth." },
        };

        static string Key(string id) => "stk_ach_" + id;
        public static bool Has(string id) => PlayerPrefs.GetInt(Key(id), 0) == 1;
        public static int UnlockedCount => All.Count(d => Has(d.Id));

        /// <summary>Checks the local player's view for newly earned achievements.</summary>
        public static void Check(GameScreen s)
        {
            if (Automation.Active) return;
            var v = s.View;
            var me = s.MePlayer;
            if (v == null || me == null) return;
            var c = v.Company;
            Try(s, "first_launch", me.ProductsLaunched > 0);
            for (int i = 1; i <= 5; i++) Try(s, "stage" + i, c.Stage >= i);
            Try(s, "ipo", c.WentPublic);
            Try(s, "topdog", c.ReachedTop);
            Try(s, "acquirer", v.Rivals.Exists(r => r.Acquired));
            Try(s, "pro", c.Products.Exists(p => p.Version >= 3));
            Try(s, "contract", me.ContractsDelivered > 0);
            Try(s, "tree", c.Upgrades.Count >= 12);
            Try(s, "whistle", me.Exposures > 0);
            var pr = s.MePriv;
            if (pr != null)
            {
                double worth = pr.Cash + pr.Holdings.Sum(h => h.Value) + me.Shares / (double)Math.Max(1, c.TotalShares) * c.Valuation;
                Try(s, "millionaire", worth >= 1000000);
            }
            if (v.Phase == GamePhase.GameOver && v.Summary != null)
            {
                var st = v.Summary.Standings.Find(x => x.PlayerId == s.Me);
                if (st != null)
                {
                    Try(s, "ambition", st.AmbitionDone);
                    Try(s, "winner", st.Rank == 1 && v.Players.Count > 1);
                }
            }
        }

        static void Try(GameScreen s, string id, bool condition)
        {
            if (!condition || Has(id)) return;
            PlayerPrefs.SetInt(Key(id), 1);
            PlayerPrefs.Save();
            var def = All.FirstOrDefault(d => d.Id == id);
            s.UiRoot.Toast($"Achievement unlocked: {def?.Name ?? id}", NoticeKind.Xp);
            Sfx.Play("level");
        }

        public static void ShowList(UiRoot ui)
        {
            var m = ui.ShowModal($"Achievements  ·  {UnlockedCount} of {All.Length}");
            foreach (var d in All)
            {
                bool got = Has(d.Id);
                var row = Ui.Row("inset", "mb8");
                var star = Ui.Div("ach-medal");
                star.style.backgroundColor = Notebook.FillFor(got ? Ui.Gold : Ui.Faint);
                var col = Ui.Col("grow");
                col.style.marginLeft = 10;
                col.Add(Ui.Text(d.Name, "b"));
                col.Add(Ui.Text(d.Description, "small", "dim"));
                row.Add(star);
                row.Add(col);
                if (!got) row.style.opacity = 0.6f;
                m.Body.Add(row);
            }
            m.AddButton("Close", m.Close);
        }
    }
}
