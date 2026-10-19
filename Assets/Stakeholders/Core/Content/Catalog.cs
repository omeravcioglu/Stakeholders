using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>Static game content. Edit these tables to rebalance or extend the game.</summary>
    public static class Catalog
    {
        public static readonly string[] Categories =
        {
            "Electronics", "Home & Kitchen", "Fitness", "Beauty", "Pets", "Toys & Games", "Fashion", "Outdoor"
        };

        public struct ProductDef
        {
            public string Name, Category;
            public double Price, Cost, Demand, Elasticity, Viral;
            public int Rarity;
            public ProductDef(string name, string cat, double price, double cost, double demand, double elasticity, double viral, int rarity)
            {
                Name = name; Category = cat; Price = price; Cost = cost; Demand = demand; Elasticity = elasticity; Viral = viral; Rarity = rarity;
            }
        }

        // name, category, ref price, base unit cost, base demand/day, price elasticity, viral potential, rarity
        public static readonly ProductDef[] Products =
        {
            new ProductDef("Wireless Earbuds", "Electronics", 39, 11, 55, 1.8, 0.25, 0),
            new ProductDef("Phone Charging Stand", "Electronics", 25, 6, 45, 1.6, 0.15, 0),
            new ProductDef("Smart LED Strip Lights", "Electronics", 29, 7, 50, 1.7, 0.45, 0),
            new ProductDef("Mini Projector", "Electronics", 119, 48, 14, 2.0, 0.35, 1),
            new ProductDef("Mechanical Keyboard", "Electronics", 89, 31, 18, 1.5, 0.30, 1),
            new ProductDef("Portable Power Bank", "Electronics", 35, 10, 60, 1.9, 0.10, 0),
            new ProductDef("AI Translator Earpiece", "Electronics", 149, 55, 12, 1.6, 0.60, 2),

            new ProductDef("Air Fryer Liners", "Home & Kitchen", 14, 2.5, 90, 2.2, 0.20, 0),
            new ProductDef("Electric Milk Frother", "Home & Kitchen", 19, 4.5, 60, 1.9, 0.30, 0),
            new ProductDef("Vacuum Storage Bags", "Home & Kitchen", 24, 5, 55, 1.8, 0.15, 0),
            new ProductDef("Smart Plant Sensor", "Home & Kitchen", 34, 9, 25, 1.6, 0.40, 1),
            new ProductDef("Cordless Knife Sharpener", "Home & Kitchen", 39, 12, 22, 1.7, 0.35, 1),
            new ProductDef("Sunset Projection Lamp", "Home & Kitchen", 27, 6, 40, 1.8, 0.70, 1),
            new ProductDef("Self-Stirring Mug", "Home & Kitchen", 22, 5.5, 35, 2.0, 0.50, 0),

            new ProductDef("Resistance Band Set", "Fitness", 24, 5, 70, 1.9, 0.25, 0),
            new ProductDef("Smart Jump Rope", "Fitness", 32, 8, 35, 1.8, 0.40, 1),
            new ProductDef("Massage Gun", "Fitness", 79, 24, 30, 1.7, 0.35, 0),
            new ProductDef("Cork Yoga Mat", "Fitness", 45, 13, 32, 1.6, 0.20, 0),
            new ProductDef("Adjustable Dumbbells", "Fitness", 159, 62, 12, 1.5, 0.20, 1),
            new ProductDef("Posture Corrector", "Fitness", 29, 6, 50, 2.0, 0.45, 0),

            new ProductDef("LED Face Mask", "Beauty", 89, 22, 16, 1.6, 0.55, 1),
            new ProductDef("Ice Roller", "Beauty", 15, 3, 70, 2.1, 0.50, 0),
            new ProductDef("Scalp Massager", "Beauty", 12, 2.2, 85, 2.2, 0.35, 0),
            new ProductDef("Heatless Curling Set", "Beauty", 18, 3.5, 65, 2.0, 0.65, 0),
            new ProductDef("Teeth Whitening Kit", "Beauty", 34, 7, 45, 1.8, 0.40, 0),
            new ProductDef("Microcurrent Toning Device", "Beauty", 129, 38, 10, 1.5, 0.50, 2),

            new ProductDef("Self-Cleaning Pet Brush", "Pets", 19, 4, 70, 1.9, 0.30, 0),
            new ProductDef("Automatic Pet Feeder", "Pets", 69, 24, 25, 1.6, 0.25, 0),
            new ProductDef("Cat Window Hammock", "Pets", 29, 7, 40, 1.8, 0.55, 0),
            new ProductDef("Interactive Laser Toy", "Pets", 25, 6, 45, 1.9, 0.50, 0),
            new ProductDef("GPS Pet Tracker", "Pets", 59, 19, 20, 1.5, 0.30, 1),
            new ProductDef("Lick Mat Set", "Pets", 13, 2.5, 60, 2.1, 0.35, 0),

            new ProductDef("Magnetic Building Tiles", "Toys & Games", 49, 15, 35, 1.7, 0.30, 0),
            new ProductDef("Fidget Cube Pack", "Toys & Games", 12, 2, 90, 2.3, 0.60, 0),
            new ProductDef("Kids Smart Watch", "Toys & Games", 59, 20, 22, 1.6, 0.25, 1),
            new ProductDef("Mini Drone", "Toys & Games", 79, 28, 20, 1.8, 0.45, 1),
            new ProductDef("Glow Puzzle", "Toys & Games", 22, 5, 35, 1.9, 0.35, 0),
            new ProductDef("Retro Handheld Console", "Toys & Games", 69, 23, 26, 1.7, 0.60, 2),

            new ProductDef("Minimalist Wallet", "Fashion", 29, 6, 50, 1.8, 0.30, 0),
            new ProductDef("Blue-Light Glasses", "Fashion", 22, 3.5, 60, 2.0, 0.35, 0),
            new ProductDef("Crossbody Phone Bag", "Fashion", 26, 6, 45, 1.9, 0.50, 0),
            new ProductDef("Anti-Theft Backpack", "Fashion", 55, 18, 28, 1.7, 0.30, 0),
            new ProductDef("Silk Sleep Mask", "Fashion", 16, 3, 55, 2.0, 0.30, 0),
            new ProductDef("Heated Vest", "Fashion", 99, 34, 14, 1.6, 0.40, 1),

            new ProductDef("Camping Lantern", "Outdoor", 29, 7, 40, 1.8, 0.20, 0),
            new ProductDef("Collapsible Water Bottle", "Outdoor", 18, 3.5, 60, 2.0, 0.30, 0),
            new ProductDef("Solar Power Generator", "Outdoor", 399, 190, 5, 1.4, 0.20, 1),
            new ProductDef("Insulated Tumbler", "Outdoor", 32, 7, 70, 1.9, 0.55, 0),
            new ProductDef("Hammock with Bug Net", "Outdoor", 45, 13, 22, 1.7, 0.25, 0),
            new ProductDef("Car Camping Mattress", "Outdoor", 89, 30, 12, 1.6, 0.30, 1),
        };

        public struct SupplierDef
        {
            public string Name, Country;
            public string[] Categories;
            public double CostFactor, Quality, Reliability, Shadiness;
            public int LeadTime, Moq;
            public bool Exclusive;
            public SupplierDef(string name, string country, string[] cats, double cost, double quality, double reliability, int lead, int moq, double shady, bool exclusive = false)
            {
                Name = name; Country = country; Categories = cats; CostFactor = cost; Quality = quality; Reliability = reliability;
                LeadTime = lead; Moq = moq; Shadiness = shady; Exclusive = exclusive;
            }
        }

        public static readonly SupplierDef[] Suppliers =
        {
            new SupplierDef("BrightTech Shenzhen", "China", new[] { "Electronics", "Toys & Games" }, 0.85, 0.55, 0.80, 18, 200, 0.50),
            new SupplierDef("Guangzhou HomeGoods", "China", new[] { "Home & Kitchen", "Pets" }, 0.82, 0.55, 0.82, 20, 300, 0.40),
            new SupplierDef("Yiwu Everything Market", "China", new[] { "Toys & Games", "Fashion", "Home & Kitchen", "Beauty" }, 0.75, 0.40, 0.70, 22, 500, 0.60),
            new SupplierDef("Hanoi Precision Mfg.", "Vietnam", new[] { "Electronics", "Fitness" }, 0.95, 0.70, 0.88, 16, 200, 0.20),
            new SupplierDef("Istanbul Textile House", "Turkey", new[] { "Fashion", "Home & Kitchen" }, 1.00, 0.75, 0.90, 10, 150, 0.30),
            new SupplierDef("Monterrey Industrial", "Mexico", new[] { "Fitness", "Outdoor", "Home & Kitchen" }, 1.05, 0.72, 0.90, 8, 150, 0.25),
            new SupplierDef("Seoul Beauty Lab", "South Korea", new[] { "Beauty" }, 1.10, 0.85, 0.92, 12, 100, 0.15),
            new SupplierDef("Dhaka Apparel Works", "Bangladesh", new[] { "Fashion" }, 0.78, 0.50, 0.75, 25, 400, 0.45),
            new SupplierDef("Bangkok Pet Supply", "Thailand", new[] { "Pets" }, 0.90, 0.65, 0.85, 15, 200, 0.30),
            new SupplierDef("Taipei Circuit Partners", "Taiwan", new[] { "Electronics" }, 1.08, 0.88, 0.94, 12, 100, 0.10),
            new SupplierDef("Ohio Outdoor Supply", "USA", new[] { "Outdoor", "Fitness" }, 1.25, 0.85, 0.96, 4, 50, 0.10),
            new SupplierDef("EuroGoods Poland", "Poland", new[] { "Home & Kitchen", "Toys & Games", "Outdoor" }, 1.12, 0.78, 0.93, 7, 100, 0.20),
            new SupplierDef("Mumbai Wellness Exports", "India", new[] { "Beauty", "Fitness", "Home & Kitchen" }, 0.88, 0.60, 0.80, 21, 250, 0.40),
            new SupplierDef("Ningbo Toy Factory", "China", new[] { "Toys & Games" }, 0.80, 0.55, 0.80, 19, 300, 0.45),
            new SupplierDef("Jakarta Outdoor Gear", "Indonesia", new[] { "Outdoor", "Fashion" }, 0.86, 0.60, 0.78, 20, 250, 0.35),
            new SupplierDef("QuickShip Dropship Hub", "USA", new[] { "*" }, 1.40, 0.65, 0.97, 3, 10, 0.20),
            // Exclusive network (Supplier Network skill)
            new SupplierDef("Kyoto Craft Collective", "Japan", new[] { "Home & Kitchen", "Beauty", "Fashion" }, 1.00, 0.95, 0.97, 10, 100, 0.05, true),
            new SupplierDef("Direct Factory Alliance", "China", new[] { "Electronics", "Toys & Games", "Home & Kitchen" }, 0.68, 0.70, 0.88, 16, 1000, 0.30, true),
            new SupplierDef("Nordic Design Makers", "Sweden", new[] { "Home & Kitchen", "Outdoor", "Fitness" }, 1.05, 0.93, 0.96, 6, 80, 0.05, true),
            new SupplierDef("Saigon Pet Innovations", "Vietnam", new[] { "Pets", "Toys & Games" }, 0.78, 0.80, 0.90, 14, 200, 0.10, true),
        };

        public class WarehouseDef
        {
            public string Id, Name;
            public int Capacity;
            public double MonthlyCost, SetupCost, FulfillmentBonus;
        }

        public static readonly WarehouseDef[] Warehouses =
        {
            new WarehouseDef { Id = "garage", Name = "Garage Storage", Capacity = 1500, MonthlyCost = 0, SetupCost = 0, FulfillmentBonus = 0 },
            new WarehouseDef { Id = "small", Name = "Small Warehouse", Capacity = 8000, MonthlyCost = 2500, SetupCost = 12000, FulfillmentBonus = 0.05 },
            new WarehouseDef { Id = "regional", Name = "Regional Fulfillment Center", Capacity = 40000, MonthlyCost = 9000, SetupCost = 60000, FulfillmentBonus = 0.15 },
            new WarehouseDef { Id = "mega", Name = "Automated Mega Warehouse", Capacity = 200000, MonthlyCost = 32000, SetupCost = 300000, FulfillmentBonus = 0.50 },
        };

        public static WarehouseDef GetWarehouse(string id)
        {
            foreach (var w in Warehouses) if (w.Id == id) return w;
            return null;
        }

        public class UpgradeDef
        {
            public string Id, Name, Description, Branch;
            /// <summary>1..4 inside a branch; 0 for special (Investor) deals.</summary>
            public int Tier;
            public double Cost;
            public int BuildDays;
            /// <summary>Minimum company growth stage.</summary>
            public int Stage;
            /// <summary>Upgrade that must be owned first.</summary>
            public string Requires;
            public bool InvestorOnly;
            public double MinValuation;
        }

        public static readonly string[] UpgradeBranches = { "Logistics", "Brand", "Sourcing", "Channels", "Data", "People" };

        static UpgradeDef U(string id, string branch, int tier, string name, double cost, int days, string desc)
            => new UpgradeDef
            {
                Id = id, Branch = branch, Tier = tier, Name = name, Cost = cost, BuildDays = days, Description = desc,
                Stage = tier - 1, Requires = tier > 1 ? id.Substring(0, id.Length - 1) + (tier - 1) : null
            };

        /// <summary>The company upgrade tree: 6 branches x 4 tiers, gated by growth stage.</summary>
        public static readonly UpgradeDef[] Upgrades =
        {
            U("log1", "Logistics", 1, "Organized Shelving", 8000, 3, "+25% warehouse storage in every building."),
            U("log2", "Logistics", 2, "Warehouse Management System", 40000, 8, "+20% fulfillment capacity."),
            U("log3", "Logistics", 3, "Express Delivery Network", 150000, 15, "+5% demand, +6 satisfaction, 10% fewer refunds."),
            U("log4", "Logistics", 4, "Robotic Fulfillment", 700000, 25, "+60% fulfillment capacity."),

            U("brd1", "Brand", 1, "Brand Identity Kit", 10000, 3, "Logo and packaging: +0.15 product ratings and +3 reputation."),
            U("brd2", "Brand", 2, "Content Studio", 50000, 8, "All ad campaigns are 20% more efficient."),
            U("brd3", "Brand", 3, "Loyalty Program", 180000, 15, "Repeat buyers: +8% demand on everything, email reaches twice as many customers."),
            U("brd4", "Brand", 4, "Celebrity Ambassador", 900000, 25, "+10 reputation ceiling and twice the viral chance."),

            U("src1", "Sourcing", 1, "Quality Inspections", 12000, 3, "+0.05 quality on every shipment and fewer defective batches."),
            U("src2", "Sourcing", 2, "Private Label Program", 80000, 10, "+0.08 quality on all products and unlocks Pro (v3) product upgrades."),
            U("src3", "Sourcing", 3, "Factory Partnership", 300000, 18, "All purchase prices 8% lower."),
            U("src4", "Sourcing", 4, "Own Factory", 1500000, 30, "Purchase prices another 12% lower and 30% shorter lead times."),

            U("chn1", "Channels", 1, "Own Web Store", 25000, 5, "Marketplace fees drop by 3 points."),
            U("chn2", "Channels", 2, "Social Commerce Shop", 70000, 10, "+8% demand and 50% more viral hits."),
            U("chn3", "Channels", 3, "Wholesale Division", 220000, 15, "Twice as many B2B contracts, and bigger ones."),
            U("chn4", "Channels", 4, "International Expansion", 1200000, 30, "New markets: +35% demand on everything, +$1 shipping per unit."),

            U("dat1", "Data", 1, "Analytics Dashboard", 15000, 3, "Research 30% cheaper and finds one more product. Shows each product's price sensitivity."),
            U("dat2", "Data", 2, "Demand Forecasting", 60000, 8, "Unlocks auto-restock: products reorder themselves from their preferred supplier."),
            U("dat3", "Data", 3, "Fraud Detection", 180000, 15, "Questionable deals are found twice as fast and suspicious orders are flagged for everyone."),
            U("dat4", "Data", 4, "AI Pricing Engine", 800000, 25, "Optimized pricing: +6% demand on everything."),

            U("ppl1", "People", 1, "Help Desk Platform", 20000, 3, "+30% customer support capacity and +3 satisfaction."),
            U("ppl2", "People", 2, "Training Academy", 60000, 8, "Every employee works 15% better."),
            U("ppl3", "People", 3, "Great Place to Work", 160000, 15, "+15 morale and better job candidates."),
            U("ppl4", "People", 4, "Leadership Program", 600000, 25, "Partners earn 20% more XP and get one extra task slot."),

            new UpgradeDef { Id = "treasury", Branch = "Investor", Name = "Treasury Reserve Program", Description = "Idle cash above $250K earns 4% a year.", Cost = 10000, BuildDays = 1, InvestorOnly = true },
            new UpgradeDef { Id = "supplierstake", Branch = "Investor", Name = "Stake in a Supplier", Description = "Minority stake in your suppliers: all purchase prices 6% lower.", Cost = 150000, BuildDays = 10, InvestorOnly = true, Stage = 2 },
        };

        public static UpgradeDef GetUpgrade(string id)
        {
            foreach (var u in Upgrades) if (u.Id == id) return u;
            return null;
        }

        public class AssetDef
        {
            public string Id, Name, Description;
            public double AnnualReturn, DailyVol, SellFee;
            public bool InvestorOnly;
        }

        public static readonly AssetDef[] PersonalAssets =
        {
            new AssetDef { Id = "index", Name = "Index Fund", Description = "Steady, low risk. ~7% a year.", AnnualReturn = 0.07, DailyVol = 0.006 },
            new AssetDef { Id = "tech", Name = "Tech Stocks", Description = "Higher growth, bumpy ride. ~12% a year.", AnnualReturn = 0.12, DailyVol = 0.017 },
            new AssetDef { Id = "crypto", Name = "Crypto", Description = "Moonshots and crashes.", AnnualReturn = 0.18, DailyVol = 0.045 },
            new AssetDef { Id = "realestate", Name = "Rental Property", Description = "Slow and solid: ~5% growth plus rent. 5% fee to sell.", AnnualReturn = 0.09, DailyVol = 0.002, SellFee = 0.05 },
            new AssetDef { Id = "privatecredit", Name = "Private Credit Fund", Description = "Investor skill: ~10% a year, very steady.", AnnualReturn = 0.10, DailyVol = 0.001, InvestorOnly = true },
            new AssetDef { Id = "angel", Name = "Angel Startup Fund", Description = "Investor skill: most bets fail, some return 10x.", AnnualReturn = 0.25, DailyVol = 0.03, InvestorOnly = true, SellFee = 0.1 },
        };

        public static AssetDef GetAsset(string id)
        {
            foreach (var a in PersonalAssets) if (a.Id == id) return a;
            return null;
        }

        public struct LoanDef
        {
            public string Id, Lender;
            public double Rate;
            public int Months;
        }

        public static readonly LoanDef[] Loans =
        {
            new LoanDef { Id = "credit", Lender = "First Commerce Bank line of credit", Rate = 0.09, Months = 24 },
            new LoanDef { Id = "growth", Lender = "SBA growth loan", Rate = 0.11, Months = 48 },
            new LoanDef { Id = "venture", Lender = "Venture debt fund", Rate = 0.14, Months = 36 },
            new LoanDef { Id = "mca", Lender = "Merchant cash advance", Rate = 0.35, Months = 12 },
        };

        public static readonly string[] FirstNames =
        {
            "Maria", "James", "Aisha", "Kenji", "Lucas", "Sofia", "Omar", "Elena", "Daniel", "Priya", "Mateo", "Hannah",
            "Yusuf", "Chloe", "Ivan", "Leila", "Noah", "Zeynep", "Arjun", "Emma", "Diego", "Fatima", "Liam", "Mei",
            "Tariq", "Grace", "Hugo", "Amara", "Felix", "Nadia", "Marco", "Ines", "Caleb", "Selin", "Ravi", "Julia"
        };

        public static readonly string[] LastNames =
        {
            "Garcia", "Chen", "Okafor", "Tanaka", "Silva", "Novak", "Haddad", "Kowalski", "Rossi", "Sharma", "Park", "Fischer",
            "Demir", "Moreau", "Petrov", "Nguyen", "Brown", "Yilmaz", "Mensah", "Larsen", "Costa", "Kim", "Ibrahim", "Weber"
        };

        public static readonly string[] AINames =
        {
            "Alex Mercer", "Priya Nair", "Viktor Hale", "Dana Brooks", "Marcus Webb", "Selin Arslan", "Hiro Sato", "Nora Lindqvist",
            "Camila Reyes", "Theo Grant"
        };

        public static readonly string[] Influencers =
        {
            "@TechTom", "@GlowWithGina", "@FitFamFelix", "@PawsAndPals", "@HomeHackHeidi", "@GadgetGuru", "@TrailTessa", "@DailyDripDev"
        };

        public static readonly string[] Competitors =
        {
            "CartCrafters", "PrimeNest", "ShopHaven", "Brightbuy Co.", "UrbanDrop", "ValueVault"
        };

        public static readonly string[] Consultants =
        {
            "Apex Growth Partners", "Blue Ridge Advisory", "Summit Digital Consulting", "Northstar Ops Group"
        };

        public static readonly string[] PlayerColors =
        {
            "#4F8CFF", "#FF7A59", "#2ECC71", "#F2C94C", "#B06CFF", "#00C2D1", "#FF5AA5", "#A3E635"
        };

        public static string RoleName(EmployeeRole role)
        {
            switch (role)
            {
                case EmployeeRole.Warehouse: return "Warehouse Associate";
                case EmployeeRole.Support: return "Customer Support";
                case EmployeeRole.Marketing: return "Marketing Specialist";
                case EmployeeRole.Buyer: return "Purchasing Buyer";
                case EmployeeRole.Analyst: return "Market Analyst";
                case EmployeeRole.Accountant: return "Accountant";
            }
            return role.ToString();
        }

        public static string RoleEffect(EmployeeRole role)
        {
            switch (role)
            {
                case EmployeeRole.Warehouse: return "Packs and ships orders (+fulfillment capacity).";
                case EmployeeRole.Support: return "Answers customers (+support capacity, satisfaction).";
                case EmployeeRole.Marketing: return "Makes every campaign more efficient.";
                case EmployeeRole.Buyer: return "Lowers purchase prices and shipping delays.";
                case EmployeeRole.Analyst: return "Faster, more accurate product research.";
                case EmployeeRole.Accountant: return "Cuts overhead and spots unusual transactions.";
            }
            return "";
        }

        public static double RoleBaseSalary(EmployeeRole role)
        {
            switch (role)
            {
                case EmployeeRole.Warehouse: return 2600;
                case EmployeeRole.Support: return 2800;
                case EmployeeRole.Marketing: return 4200;
                case EmployeeRole.Buyer: return 4000;
                case EmployeeRole.Analyst: return 4400;
                case EmployeeRole.Accountant: return 4600;
            }
            return 3000;
        }

        public static string PersonalityDescription(Personality p)
        {
            switch (p)
            {
                case Personality.Greedy: return "Wants money now. Loves dividends and side deals.";
                case Personality.Loyal: return "Company first. Reports bribes and exposes cheaters.";
                case Personality.RiskTaker: return "Big orders, loans and bold bets.";
                case Personality.Conservative: return "Keeps cash, avoids debt, votes against risk.";
                case Personality.Workaholic: return "Handles everything, then asks for a raise.";
                case Personality.Lazy: return "Does little, still expects a salary.";
                case Personality.Manipulative: return "Trades votes, breaks promises, plays people.";
                case Personality.Ambitious: return "Wants power: more shares, more authority.";
            }
            return "Human partner.";
        }

        public static IEnumerable<Personality> AllPersonalities()
        {
            yield return Personality.Greedy;
            yield return Personality.Loyal;
            yield return Personality.RiskTaker;
            yield return Personality.Conservative;
            yield return Personality.Workaholic;
            yield return Personality.Lazy;
            yield return Personality.Manipulative;
            yield return Personality.Ambitious;
        }
    }
}
