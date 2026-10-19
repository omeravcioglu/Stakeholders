using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>Everything that belongs to the shared company.</summary>
    public class Company
    {
        public string Name;
        public double Cash;
        public double Reputation = 35;
        public double Satisfaction = 70;
        public double BrandBonus;
        public long TotalShares = 1000000;
        public long InvestorShares;
        public double Valuation;
        public double PeakValuation;

        public List<CompanyProduct> Products = new List<CompanyProduct>();
        public List<Shipment> Shipments = new List<Shipment>();
        public List<Campaign> Campaigns = new List<Campaign>();
        public List<Employee> Employees = new List<Employee>();
        public List<Warehouse> Warehouses = new List<Warehouse>();
        public List<Loan> Loans = new List<Loan>();
        public List<string> Upgrades = new List<string>();
        public List<Discovery> ResearchBoard = new List<Discovery>();
        public Policies Policies = new Policies();

        public Financials Today = new Financials();
        public Financials Month = new Financials();
        public Financials LastMonth = new Financials();
        public Financials Quarter = new Financials();
        public Financials LastQuarter = new Financials();
        public Financials Year = new Financials();
        public Financials Lifetime = new Financials();
        public List<Transaction> Ledger = new List<Transaction>();

        // Operations (derived each day, stored for the UI)
        public double Backlog;
        public double CapacityPerDay;
        public double SupportPerDay;
        public double OrdersToday;
        public double StorageUsed;
        public double StorageCapacity;
        public long LifetimeUnits;

        // Temporary modifiers
        public double FeeModifier;
        public int FeeModifierUntil;
        public double DemandModifier = 1;
        public int DemandModifierUntil;
        public double HeldCash;
        public int HeldUntilDay;
        /// <summary>Consecutive days below the overdraft limit.</summary>
        public int NegativeCashDays;
        /// <summary>Consecutive days with any negative balance.</summary>
        public int OverdraftDays;

        public bool Sold;
        public double SalePrice;
        public bool Bankrupt;

        // Growth & strategy
        /// <summary>Growth stage (see <see cref="Stages"/>). Never goes down.</summary>
        public int Stage;
        /// <summary>"", "premium", "value" or "trend".</summary>
        public string Positioning = "";
        public int PositioningDay;
        public List<UpgradeBuild> Building = new List<UpgradeBuild>();
        public List<Contract> Contracts = new List<Contract>();
        /// <summary>Our share of each category's sales (0..1), updated daily.</summary>
        public Dictionary<string, double> CategoryShare = new Dictionary<string, double>();
        /// <summary>Categories where we acquired a rival's customers (permanent demand bonus).</summary>
        public List<string> AcquiredCategories = new List<string>();
        public bool IpoPending;
        public int IpoDay;
        public bool WentPublic;
        public bool ReachedTop;

        public Company ShallowCopy() => (Company)MemberwiseClone();
        public bool Has(string upgradeId) => Upgrades.Contains(upgradeId);

        public double Debt
        {
            get { double d = 0; for (int i = 0; i < Loans.Count; i++) d += Loans[i].Balance; return d; }
        }
    }

    public class Policies
    {
        /// <summary>0 strict, 1 standard, 2 generous.</summary>
        public int ReturnPolicy = 1;
        public bool SupplierAudits;
        /// <summary>Ledger anomaly flags are visible to every partner, not just accountants.</summary>
        public bool OpenBooks;
        public bool SecretBallot;
        public double DefaultSpendingLimit = 25000;
    }

    public class Financials
    {
        public double Revenue, Cogs, Fees, Refunds;
        public double Marketing, Payroll, PartnerSalaries, Rent, Interest, Overhead, Other, Bonuses, Tax;
        public double Dividends;
        public int Units;

        public double GrossProfit => Revenue - Cogs - Fees - Refunds;
        public double Expenses => Marketing + Payroll + PartnerSalaries + Rent + Interest + Overhead + Other + Bonuses;
        public double PreTaxProfit => GrossProfit - Expenses;
        public double NetProfit => PreTaxProfit - Tax;

        public void Add(Financials o)
        {
            Revenue += o.Revenue; Cogs += o.Cogs; Fees += o.Fees; Refunds += o.Refunds;
            Marketing += o.Marketing; Payroll += o.Payroll; PartnerSalaries += o.PartnerSalaries; Rent += o.Rent;
            Interest += o.Interest; Overhead += o.Overhead; Other += o.Other; Bonuses += o.Bonuses; Tax += o.Tax;
            Dividends += o.Dividends; Units += o.Units;
        }

        public Financials Copy() => (Financials)MemberwiseClone();
    }

    public class Transaction
    {
        public string Id;
        public int Day;
        public TxType Type;
        public double Amount;
        public int ActorId = -1;
        public string Text;
        /// <summary>Anomaly note ("18% above cheapest supplier"). Only accountants see it unless books are open.</summary>
        public string Flag;
        public int FlagLevel;

        public Transaction ShallowCopy() => (Transaction)MemberwiseClone();
    }

    public class CompanyProduct
    {
        public string Id;
        public string ProductId;
        public string Name;
        public string Category;
        public int ManagerId = -1;
        public int DiscovererId = -1;
        public int LaunchedDay;

        public double Price;
        public int Stock;
        public double AvgCost;
        public double Quality = 0.6;
        public double QualityBonus;
        public string SupplierId;
        public bool Active = true;
        public int LastOrderedBy = -1;

        public double Demand7;
        public double Sales7;
        public int SoldToday;
        public double LostToday;
        public int SoldTotal;
        public double Revenue;
        public double Profit;
        public double ProfitQuarter;
        public double ProfitCredited;
        public double NextMilestone = 50000;
        public bool ProfitableAwarded;
        public bool LossPenalized;
        public bool DeadStockFlagged;
        public int DaysNoSales;
        public int StockoutDays;
        public double Rating = 4.0;
        public List<float> SalesHistory = new List<float>();

        public double BoostMult = 1;
        public int BoostUntil;
        public double PenaltyMult = 1;
        public int PenaltyUntil;

        // Product development & tactics
        public string BaseName;
        public int Version = 1;
        /// <summary>Day an R&D upgrade finishes (0 = none in progress).</summary>
        public int UpgradeDoneDay;
        public int UpgradedBy = -1;
        /// <summary>Active promotion: "flash" or "freeship" (null = none).</summary>
        public string Promo;
        public int PromoUntil;
        public int PromoCooldownUntil;
        public int AfterglowUntil;
        public int PromoUnits;
        public double PromoRevenue;
        public bool AutoRestock;
        public int AutoRestockDays = 30;

        public bool PromoActive(int day) => Promo != null && day <= PromoUntil;
    }

    /// <summary>A company upgrade under construction.</summary>
    public class UpgradeBuild
    {
        public string Id;
        public int StartDay;
        public int DoneDay;
        public int OrderedBy = -1;
    }

    /// <summary>A B2B delivery contract: ship N units by a deadline for a fixed price.</summary>
    public class Contract
    {
        public string Id;
        public string Client;
        public string CompanyProductId;
        public string ProductName;
        public int Quantity;
        public double UnitPrice;
        public int CreatedDay;
        public int DueDay;
        public int AcceptedBy = -1;
        /// <summary>0 active, 1 delivered, 2 failed.</summary>
        public int Status;
        public double Penalty;
        public int CompletedDay;
    }

    /// <summary>A competing e-commerce company.</summary>
    public class Rival
    {
        public string Id;
        public string Name;
        /// <summary>"Discounter", "Premium", "Trend Chaser", "Generalist".</summary>
        public string Style;
        public List<string> Focus = new List<string>();
        public double Valuation;
        /// <summary>Market power, 0.3 .. 2.5.</summary>
        public double Strength = 1;
        public double LastMonthValuation;
        public bool Acquired;
        public int AcquiredDay;
        public int AcquiredBy = -1;
        public double AcquiredFor;
        public string LastMove;
        public int LastMoveDay;
    }

    public class Shipment
    {
        public string Id;
        public string CompanyProductId;
        public string SupplierId;
        public int Quantity;
        public double UnitCost;
        public double Quality;
        public int OrderedDay;
        public int EtaDay;
        public int OrderedBy;
        public bool Delayed;
        public bool Rush;
    }

    public class Campaign
    {
        public string Id;
        public string CompanyProductId;
        public AdChannel Channel;
        public double DailyBudget;
        public int StartDay;
        public int EndDay;
        public int CreatedBy;
        public double Efficiency = 1;
        public double Spent;
        public double AttributedRevenue;
        public double Spent7;
        public double Revenue7;
        public bool Active = true;
        public bool Reviewed;
        public double Roas => Spent > 0 ? AttributedRevenue / Spent : 0;
        public double Roas7 => Spent7 > 0 ? Revenue7 / Spent7 : 0;
    }

    public class Employee
    {
        public string Id;
        public string Name;
        public EmployeeRole Role;
        public int Skill = 2;
        public double Salary;
        public double Morale = 70;
        public int ManagerId = -1;
        public int HiredDay;
        public int LastRaiseDay;
    }

    public class Candidate
    {
        public string Id;
        public string Name;
        public EmployeeRole Role;
        public int Skill;
        public int ShownSkillLow;
        public int ShownSkillHigh;
        public double Salary;
        public int FoundBy;
        public int ExpiresDay;
    }

    public class Warehouse
    {
        public string Id;
        public string Tier;
        public string Name;
        public int Capacity;
        public double MonthlyCost;
        public double FulfillmentBonus;
        public int OpenedDay;
        public double SetupCost;
    }

    public class Loan
    {
        public string Id;
        public string Lender;
        public double Principal;
        public double Balance;
        public double AnnualRate;
        public double MonthlyPayment;
        public int MonthsLeft;
        public int TakenDay;
        public int TakenBy;
    }

    public class History
    {
        public const int MaxSamples = 360;
        public int StartDay = 1;
        public List<float> Cash = new List<float>();
        public List<float> Revenue = new List<float>();
        public List<float> Profit = new List<float>();
        public List<float> Valuation = new List<float>();
        public List<float> Satisfaction = new List<float>();
        public List<float> Reputation = new List<float>();
    }
}
