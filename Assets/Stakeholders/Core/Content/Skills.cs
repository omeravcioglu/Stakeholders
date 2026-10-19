using System.Collections.Generic;

namespace Stakeholders.Core
{
    public class SkillDef
    {
        public string Id;
        public string Tree;
        public string Name;
        public string Description;
        /// <summary>1 or 2. Tier 2 needs another skill from the same tree and level 4.</summary>
        public int Tier;
        /// <summary>Hidden skills are not shown to other partners.</summary>
        public bool Hidden;
    }

    public static class Skills
    {
        // Product research
        public const string TrendHunter = "trend_hunter";
        public const string MarketAnalyst = "market_analyst";
        public const string ProductExpert = "product_expert";
        public const string EarlyAccess = "early_access";
        // Negotiation
        public const string Negotiator = "negotiator";
        public const string BulkSpecialist = "bulk_specialist";
        public const string FastTalker = "fast_talker";
        public const string SupplierNetwork = "supplier_network";
        // Marketing
        public const string AdSpecialist = "ad_specialist";
        public const string ViralMarketing = "viral_marketing";
        public const string InfluencerNetwork = "influencer_network";
        public const string BrandBuilder = "brand_builder";
        // Finance
        public const string Accountant = "accountant";
        public const string CashFlowExpert = "cash_flow_expert";
        public const string TaxSpecialist = "tax_specialist";
        public const string Investor = "investor";
        // Management
        public const string Leader = "leader";
        public const string Recruiter = "recruiter";
        public const string Executive = "executive";
        public const string BoardInfluence = "board_influence";
        // Intrigue (hidden from partners)
        public const string Investigator = "investigator";
        public const string Deception = "deception";
        public const string Networker = "networker";
        public const string SpinDoctor = "spin_doctor";

        public static readonly string[] Trees = { "Product Research", "Negotiation", "Marketing", "Finance", "Management", "Intrigue" };

        public static readonly List<SkillDef> All = new List<SkillDef>
        {
            new SkillDef { Id = TrendHunter, Tree = "Product Research", Tier = 1, Name = "Trend Hunter", Description = "See the trend phase of products you research, and get private early notice of trending products." },
            new SkillDef { Id = MarketAnalyst, Tree = "Product Research", Tier = 1, Name = "Market Analyst", Description = "Demand estimates become far more accurate (about ±12% instead of ±35%)." },
            new SkillDef { Id = ProductExpert, Tree = "Product Research", Tier = 2, Name = "Product Expert", Description = "Research reveals price sensitivity, competition and viral potential." },
            new SkillDef { Id = EarlyAccess, Tree = "Product Research", Tier = 2, Name = "Early Access", Description = "Discover rare products, and your discoveries stay exclusive to you for 30 days." },

            new SkillDef { Id = Negotiator, Tree = "Negotiation", Tier = 1, Name = "Negotiator", Description = "Suppliers accept deeper discounts, and you see their likely range." },
            new SkillDef { Id = BulkSpecialist, Tree = "Negotiation", Tier = 1, Name = "Bulk Specialist", Description = "Unlocks 5,000 and 10,000 unit bulk tiers (up to 18% off)." },
            new SkillDef { Id = FastTalker, Tree = "Negotiation", Tier = 2, Name = "Fast Talker", Description = "One extra negotiation round, and suppliers take offense less often." },
            new SkillDef { Id = SupplierNetwork, Tree = "Negotiation", Tier = 2, Name = "Supplier Network", Description = "Access to exclusive, high-quality suppliers." },

            new SkillDef { Id = AdSpecialist, Tree = "Marketing", Tier = 1, Name = "Ad Specialist", Description = "Campaigns you launch are 25% more efficient." },
            new SkillDef { Id = ViralMarketing, Tree = "Marketing", Tier = 1, Name = "Viral Marketing", Description = "Your campaigns are 2.5x more likely to make a product go viral." },
            new SkillDef { Id = InfluencerNetwork, Tree = "Marketing", Tier = 2, Name = "Influencer Network", Description = "Influencer deals cost 30% less and come along more often." },
            new SkillDef { Id = BrandBuilder, Tree = "Marketing", Tier = 2, Name = "Brand Builder", Description = "Your campaigns build long-term brand reputation. Unlocks the best PR crisis responses." },

            new SkillDef { Id = Accountant, Tree = "Finance", Tier = 1, Name = "Accountant", Description = "See anomaly flags in the ledger, like overpriced suppliers and odd invoices." },
            new SkillDef { Id = CashFlowExpert, Tree = "Finance", Tier = 1, Name = "Cash Flow Expert", Description = "See the cash runway forecast and get early warnings about cash shortages." },
            new SkillDef { Id = TaxSpecialist, Tree = "Finance", Tier = 2, Name = "Tax Specialist", Description = "The company pays 15% less in fees and overhead while you are a partner." },
            new SkillDef { Id = Investor, Tree = "Finance", Tier = 2, Name = "Investor", Description = "Unlocks extra personal and company investment opportunities." },

            new SkillDef { Id = Leader, Tree = "Management", Tier = 1, Name = "Leader", Description = "Employees you hired work 20% better and stay happier." },
            new SkillDef { Id = Recruiter, Tree = "Management", Tier = 1, Name = "Recruiter", Description = "Recruiting finds more and better candidates, and shows their true skill." },
            new SkillDef { Id = Executive, Tree = "Management", Tier = 2, Name = "Executive", Description = "+$40K company spending without partner approval." },
            new SkillDef { Id = BoardInfluence, Tree = "Management", Tier = 2, Name = "Board Influence", Description = "Your proposals carry 10% extra voting weight, and AI partners warm to them." },

            new SkillDef { Id = Investigator, Tree = "Intrigue", Tier = 1, Hidden = true, Name = "Investigator", Description = "Much higher chance to uncover partners' questionable deals." },
            new SkillDef { Id = Deception, Tree = "Intrigue", Tier = 1, Hidden = true, Name = "Deception", Description = "Your questionable actions are half as likely to be discovered." },
            new SkillDef { Id = Networker, Tree = "Intrigue", Tier = 2, Hidden = true, Name = "Networker", Description = "Receive more frequent and more lucrative private offers." },
            new SkillDef { Id = SpinDoctor, Tree = "Intrigue", Tier = 2, Hidden = true, Name = "Spin Doctor", Description = "If exposed, the damage is halved, and you can deny it once." },
        };

        public static SkillDef Get(string id)
        {
            for (int i = 0; i < All.Count; i++) if (All[i].Id == id) return All[i];
            return null;
        }

        public static bool IsHidden(string id)
        {
            var s = Get(id);
            return s != null && s.Hidden;
        }

        public const int Tier2Level = 4;

        public static int XpForNextLevel(int level) => 100 + 40 * (level - 1);
    }
}
