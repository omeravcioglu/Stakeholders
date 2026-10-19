using System.Collections.Generic;

namespace Stakeholders.Core
{
    public class StageDef
    {
        public int Index;
        public string Name;
        public double Valuation;
        public string Unlocks;
    }

    /// <summary>Company growth stages: the long-term arc of a game.</summary>
    public static class Stages
    {
        public static readonly StageDef[] All =
        {
            new StageDef { Index = 0, Name = "Garage Startup", Valuation = 0, Unlocks = "Tier 1 upgrades" },
            new StageDef { Index = 1, Name = "Online Shop", Valuation = 500000, Unlocks = "Tier 2 upgrades, a company strategy, v2 product upgrades" },
            new StageDef { Index = 2, Name = "Growing Brand", Valuation = 2500000, Unlocks = "Tier 3 upgrades, Pro (v3) products, bigger contracts" },
            new StageDef { Index = 3, Name = "Scale-up", Valuation = 10000000, Unlocks = "Tier 4 upgrades, acquiring rivals" },
            new StageDef { Index = 4, Name = "Market Leader", Valuation = 35000000, Unlocks = "Prestige: +5 reputation" },
            new StageDef { Index = 5, Name = "IPO Ready", Valuation = 100000000, Unlocks = "Taking the company public" },
        };

        public static StageDef Get(int index) => All[index < 0 ? 0 : index >= All.Length ? All.Length - 1 : index];
        public static int Max => All.Length - 1;

        /// <summary>The stage name as it reads in a sentence: "an Online Shop", "a Scale-up", "IPO Ready".</summary>
        public static string InSentence(int index)
        {
            var name = Get(index).Name;
            if (name.EndsWith("Ready")) return name;
            return ("AEIOU".IndexOf(name[0]) >= 0 ? "an " : "a ") + name;
        }
    }

    public class PositioningDef
    {
        public string Id, Name, Description;
    }

    /// <summary>The company's strategic identity, chosen by board vote.</summary>
    public static class Positionings
    {
        public static readonly PositioningDef[] All =
        {
            new PositioningDef { Id = "premium", Name = "Premium Brand", Description = "Customers pay for quality: price increases hurt demand 25% less, +0.1 ratings, +5 reputation ceiling. Products rated 4.0+ sell 10% more; products under 3.5 sell 15% less." },
            new PositioningDef { Id = "value", Name = "Value Leader", Description = "Win on price: purchase prices 5% lower and +12% demand, but -3 reputation ceiling." },
            new PositioningDef { Id = "trend", Name = "Trend Machine", Description = "Ride the hype: +25% demand on emerging and rising products, -10% on everything else. Research is a day faster." },
        };

        public static PositioningDef Get(string id)
        {
            foreach (var p in All) if (p.Id == id) return p;
            return null;
        }
    }

    public class AmbitionDef
    {
        public string Id, Name, Description;
        public bool NeedsIntrigue;
    }

    /// <summary>Secret personal objectives. Completing yours is worth a large bonus at the end.</summary>
    public static class Ambitions
    {
        public static readonly AmbitionDef[] All =
        {
            new AmbitionDef { Id = "tycoon", Name = "Tycoon", Description = "Own at least 35% of the company at the end." },
            new AmbitionDef { Id = "rainmaker", Name = "Rainmaker", Description = "Generate the most product profit of all partners." },
            new AmbitionDef { Id = "workhorse", Name = "Workhorse", Description = "Handle {N} situations." },
            new AmbitionDef { Id = "shark", Name = "Shark", Description = "Earn {M} off the books.", NeedsIntrigue = true },
            new AmbitionDef { Id = "boyscout", Name = "Boy Scout", Description = "Never accept a shady deal, and report or expose at least 2 of them.", NeedsIntrigue = true },
            new AmbitionDef { Id = "visionary", Name = "Visionary", Description = "Launch {N} products yourself." },
            new AmbitionDef { Id = "empire", Name = "Empire Builder", Description = "Get the company to the {S} stage." },
            new AmbitionDef { Id = "fatcat", Name = "Fat Cat", Description = "Finish with the highest salary of all partners." },
            new AmbitionDef { Id = "kingmaker", Name = "Kingmaker", Description = "Get {N} of your board proposals passed." },
            new AmbitionDef { Id = "mogul", Name = "Mogul", Description = "Build {M} of personal wealth outside the company (cash + investments)." },
            new AmbitionDef { Id = "dealmaker", Name = "Dealmaker", Description = "Win {N} supplier negotiations or delivered contracts." },
            new AmbitionDef { Id = "talent", Name = "Talent Scout", Description = "Hire {N} employees." },
            new AmbitionDef { Id = "topdog", Name = "Top Dog", Description = "Make the company #1 on the industry leaderboard." },
        };

        public static AmbitionDef Get(string id)
        {
            foreach (var a in All) if (a.Id == id) return a;
            return null;
        }
    }

    public struct RivalDef
    {
        public string Name, Style;
        public double Valuation, Strength;
        public string[] Focus;
    }

    public static class RivalCatalog
    {
        public static readonly RivalDef[] All =
        {
            new RivalDef { Name = "ShopHaven", Style = "Generalist", Valuation = 8000000, Strength = 1.25, Focus = new[] { "Electronics", "Outdoor", "Fitness" } },
            new RivalDef { Name = "PrimeNest", Style = "Premium", Valuation = 4500000, Strength = 1.1, Focus = new[] { "Home & Kitchen", "Beauty" } },
            new RivalDef { Name = "CartCrafters", Style = "Generalist", Valuation = 2500000, Strength = 1.0, Focus = new[] { "Fashion", "Pets", "Home & Kitchen" } },
            new RivalDef { Name = "ValueVault", Style = "Discounter", Valuation = 1800000, Strength = 1.0, Focus = new[] { "Toys & Games", "Fashion", "Electronics" } },
            new RivalDef { Name = "UrbanDrop", Style = "Trend Chaser", Valuation = 900000, Strength = 0.9, Focus = new[] { "Beauty", "Fitness" } },
        };
    }

    /// <summary>Areas of responsibility that can be delegated to AI partners.</summary>
    public static class Roles
    {
        public const string Buying = "buying";
        public const string Marketing = "marketing";
        public const string Research = "research";
        public const string Hiring = "hiring";
        public const string Situations = "situations";
        public const string Upgrades = "upgrades";

        public static readonly string[] All = { Buying, Marketing, Research, Hiring, Situations, Upgrades };

        public static string Name(string role)
        {
            switch (role)
            {
                case Buying: return "Purchasing";
                case Marketing: return "Marketing";
                case Research: return "Products";
                case Hiring: return "Hiring";
                case Situations: return "Situations";
                case Upgrades: return "Upgrades";
            }
            return role;
        }

        public static string Description(string role)
        {
            switch (role)
            {
                case Buying: return "Restocks products, picks suppliers, negotiates discounts.";
                case Marketing: return "Runs ad campaigns and promotions.";
                case Research: return "Researches, launches and upgrades products.";
                case Hiring: return "Recruits, hires and lets people go.";
                case Situations: return "Handles problems and opportunities.";
                case Upgrades: return "Buys company upgrades and warehouses.";
            }
            return "";
        }

        /// <summary>Preferred roles per personality, most preferred first.</summary>
        public static string[] Preferred(Personality p)
        {
            switch (p)
            {
                case Personality.Workaholic: return new[] { Situations, Buying, Research };
                case Personality.Loyal: return new[] { Hiring, Situations, Upgrades };
                case Personality.Greedy: return new[] { Buying, Marketing };
                case Personality.RiskTaker: return new[] { Marketing, Research, Upgrades };
                case Personality.Conservative: return new[] { Buying, Hiring };
                case Personality.Lazy: return new[] { Marketing };
                case Personality.Manipulative: return new[] { Buying, Upgrades };
                case Personality.Ambitious: return new[] { Research, Upgrades, Marketing };
            }
            return new[] { Situations };
        }
    }
}
