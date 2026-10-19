using Stakeholders.Core;
using UnityEngine;

namespace Stakeholders.App.UI
{
    /// <summary>
    /// The "founder's notebook" palette for colors that code sets directly. Notebook.uss styles everything else,
    /// and the art lives in Resources/StakeholdersUI/Notebook (rendered by Tools/NotebookArt).
    /// </summary>
    public static class Notebook
    {
        public static readonly Color Paper = Ui.Hex("#F5EEDF");
        public static readonly Color PaperDeep = Ui.Hex("#EBE1CC");
        public static readonly Color Card = Ui.Hex("#FFFCF5");
        public static readonly Color Ink = Ui.Hex("#2B2622");
        public static readonly Color Rule = Ui.Hex("#D8CBB3");

        // Marker fills: bars, dots, pie slices and confetti. Text never uses them.
        public static readonly Color MarkerYellow = Ui.Hex("#FFD84D");
        public static readonly Color MarkerGreen = Ui.Hex("#5CBF7F");
        public static readonly Color MarkerTomato = Ui.Hex("#F07A5F");
        public static readonly Color MarkerBlue = Ui.Hex("#7FB0FF");
        public static readonly Color MarkerLilac = Ui.Hex("#B89AF0");
        public static readonly Color MarkerOrange = Ui.Hex("#FFB25B");
        public static readonly Color MarkerPink = Ui.Hex("#F06BAA");
        public static readonly Color MarkerTeal = Ui.Hex("#1FB5C4");

        // One seat color per player: a fill for faces, dots and pie slices, and a darker ink for their name.
        static readonly Color[] SeatFill = Parse("#5B8DEF", "#FF7A59", "#3DBE7A", "#F2C94C", "#A374F2", "#1FB5C4", "#F06BAA", "#9BD442");
        static readonly Color[] SeatInk = Parse("#2F5DA8", "#B3471F", "#23704A", "#8A5E00", "#6B3FA0", "#0F6E78", "#A8326E", "#466E10");

        public static Color PlayerFill(int index) => SeatFill[Mathf.Abs(index) % SeatFill.Length];
        public static Color PlayerInk(int index) => SeatInk[Mathf.Abs(index) % SeatInk.Length];

        /// <summary>Ui's text inks are heavy as fills, so bars, dots and slices swap them for the matching marker.</summary>
        public static Color FillFor(Color c)
        {
            if (c == Ui.Good) return MarkerGreen;
            if (c == Ui.Bad) return MarkerTomato;
            if (c == Ui.Warn) return MarkerOrange;
            if (c == Ui.Accent) return MarkerBlue;
            if (c == Ui.Secret) return MarkerLilac;
            if (c == Ui.Gold) return MarkerYellow;
            if (c == Ui.Dim || c == Ui.Faint || c == Ui.TextColor) return Rule;
            return c;
        }

        /// <summary>Style-class key for a game page ("icon-market", "nav-market", "page-market").</summary>
        public static string PageKey(string title)
        {
            if (string.IsNullOrEmpty(title)) return "page";
            if (title.StartsWith("Market")) return "market";
            return title.ToLowerInvariant().Replace(' ', '-').Replace("&", "and");
        }

        /// <summary>Face class for a partner's avatar: humans get the plain face, AI partners their personality.</summary>
        public static string FaceClass(Player p)
        {
            if (p == null) return null;
            return !p.IsAI || p.Personality == Personality.Human ? "face-you" : "face-" + p.Personality.ToString().ToLowerInvariant();
        }

        public static readonly string[] FaceClasses =
        {
            "face-you", "face-greedy", "face-loyal", "face-risktaker", "face-conservative",
            "face-workaholic", "face-lazy", "face-manipulative", "face-ambitious", "face-employee"
        };

        static Color[] Parse(params string[] hex)
        {
            var colors = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++) colors[i] = Ui.Hex(hex[i]);
            return colors;
        }
    }
}
