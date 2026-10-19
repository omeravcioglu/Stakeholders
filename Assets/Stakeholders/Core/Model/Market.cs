using System.Collections.Generic;

namespace Stakeholders.Core
{
    public class Market
    {
        public double Economy = 1.0;
        public List<MarketProduct> Products = new List<MarketProduct>();
    }

    /// <summary>
    /// A product type that exists in the market. Demand drivers are hidden truth: players only
    /// ever see estimates (research), observed sales, or skill-gated details.
    /// </summary>
    public class MarketProduct
    {
        public string Id;
        public string Name;
        public string Category;
        public double RefPrice;
        public double BaseCost;
        /// <summary>0 common, 1 uncommon, 2 rare (rare needs Early Access to discover).</summary>
        public int Rarity;

        // Hidden truth
        public double BaseDemand;
        public double Elasticity;
        public double ViralPotential;
        public double Trend = 1;
        public double TrendTarget = 1;
        public TrendPhase Phase = TrendPhase.Stable;
        public int PhaseDays;
        public double Competition = 0.5;

        // Observable market signals
        public double CompetitorPrice;
        public double CompetitorCut;
        public int CompetitorCutUntil;
        public bool Viral;
        public int ViralUntil;

        /// <summary>Early Access: this product is only discoverable by this player until ReservedUntil.</summary>
        public int ReservedBy = -1;
        public int ReservedUntil;

        /// <summary>True in views where hidden values were stripped.</summary>
        public bool Redacted;
        public bool TrendVisible;
        public bool DetailsVisible;

        public MarketProduct ShallowCopy() => (MarketProduct)MemberwiseClone();
    }

    /// <summary>A research result: an estimate, not the truth.</summary>
    public class Discovery
    {
        public string ProductId;
        public string Name;
        public string Category;
        public int FoundBy;
        public int Day;
        public double RefPrice;
        public double DemandLow;
        public double DemandHigh;
        public double EstCost;
        public bool PhaseKnown;
        public TrendPhase Phase;
        public bool DetailsKnown;
        public double Elasticity;
        public double Competition;
        public double Viral;
        public bool Exclusive;
        public bool Shared;
        public bool Launched;

        public double DemandMid => (DemandLow + DemandHigh) * 0.5;
        public Discovery Copy() => (Discovery)MemberwiseClone();
    }

    public class Supplier
    {
        public string Id;
        public string Name;
        public string Country;
        public List<string> Categories = new List<string>();
        public double CostFactor = 1;
        public double Quality = 0.6;
        public double Reliability = 0.85;
        public int LeadTime = 14;
        public int Moq = 100;
        public double Shadiness;
        public bool Exclusive;

        public double Relationship = 50;
        public double Discount;
        public int DiscountUntil;
        public int BlockedUntil;
        public bool Blacklisted;
        public bool Bankrupt;
        public double TotalPurchased;

        public Supplier ShallowCopy() => (Supplier)MemberwiseClone();

        public bool Serves(string category) => Categories.Contains(category) || Categories.Contains("*");
    }
}
