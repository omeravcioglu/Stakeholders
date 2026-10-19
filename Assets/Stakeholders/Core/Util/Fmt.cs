using System;
using System.Globalization;

namespace Stakeholders.Core
{
    /// <summary>Culture-independent formatting (the game must read the same on every locale).</summary>
    public static class Fmt
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Compact money: $950, $48.5K, $4.20M.</summary>
        public static string Money(double v)
        {
            string sign = v < 0 ? "-" : "";
            double a = Math.Abs(v);
            if (a >= 1e9) return sign + "$" + (a / 1e9).ToString("0.00", Inv) + "B";
            if (a >= 1e6) return sign + "$" + (a / 1e6).ToString("0.00", Inv) + "M";
            if (a >= 1e4) return sign + "$" + (a / 1e3).ToString("0.0", Inv) + "K";
            if (a >= 100) return sign + "$" + a.ToString("#,0", Inv);
            return sign + "$" + a.ToString("0.##", Inv);
        }

        /// <summary>Full money: $4,200,000.</summary>
        public static string MoneyFull(double v)
        {
            string sign = v < 0 ? "-" : "";
            return sign + "$" + Math.Abs(v).ToString("#,0", Inv);
        }

        public static string Price(double v) => "$" + v.ToString("0.00", Inv);

        public static string SignedMoney(double v) => (v >= 0 ? "+" : "") + Money(v);

        public static string Pct(double fraction, int decimals = 0)
        {
            string f = decimals <= 0 ? "0" : "0." + new string('0', decimals);
            return (fraction * 100).ToString(f, Inv) + "%";
        }

        public static string SignedPct(double fraction, int decimals = 0) => (fraction >= 0 ? "+" : "") + Pct(fraction, decimals);

        public static string Int(double v) => Math.Round(v).ToString("#,0", Inv);

        public static string Num(double v, int decimals = 1) => v.ToString("0." + new string('#', Math.Max(1, decimals)), Inv);

        public static string Fixed(double v, int decimals = 1) => v.ToString(decimals <= 0 ? "0" : "0." + new string('0', decimals), Inv);

        public static string Date(int day)
        {
            int d = Math.Max(1, day);
            int year = (d - 1) / GameCalendar.DaysPerYear + 1;
            int month = ((d - 1) / GameCalendar.DaysPerMonth) % 12;
            int dom = (d - 1) % GameCalendar.DaysPerMonth + 1;
            return GameCalendar.MonthNames[month] + " " + dom + ", Y" + year;
        }

        public static string ShortDate(int day)
        {
            int d = Math.Max(1, day);
            int month = ((d - 1) / GameCalendar.DaysPerMonth) % 12;
            int dom = (d - 1) % GameCalendar.DaysPerMonth + 1;
            return GameCalendar.MonthNames[month] + " " + dom;
        }

        public static string Days(int n) => n == 1 ? "1 day" : n + " days";

        public static string Shares(long shares, long total) => Pct(total > 0 ? (double)shares / total : 0, 1);

        public static string Title(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
