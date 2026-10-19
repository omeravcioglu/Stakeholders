using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>
    /// The complete, authoritative state of one game. Plain data only: every rule lives in
    /// <see cref="GameSimulation"/>. The same type is used for per-player views (see
    /// <see cref="ViewBuilder"/>), where secret fields and collections are stripped.
    /// </summary>
    public class GameState
    {
        public int SaveVersion = 1;
        public GameSettings Settings = new GameSettings();
        public GamePhase Phase = GamePhase.Running;
        public int Day = 1;
        public double DayProgress;
        public int Speed = 1;
        public bool Paused;
        public ulong RngState = 0x9E3779B97F4A7C15UL;
        public int NextId = 1;
        public int NextNoticeId = 1;
        public int NextChatId = 1;
        public long Revision;

        /// <summary>-1 for the authoritative state; otherwise the player this view was built for.</summary>
        public int ViewerId = -1;

        public Company Company = new Company();
        public Market Market = new Market();
        public List<Rival> Rivals = new List<Rival>();
        public List<Supplier> Suppliers = new List<Supplier>();
        public List<Player> Players = new List<Player>();
        public List<PlayerPrivate> Private = new List<PlayerPrivate>();

        public List<GameEvent> Events = new List<GameEvent>();
        public List<GameEvent> ResolvedEvents = new List<GameEvent>();
        public List<PrivateOffer> Offers = new List<PrivateOffer>();
        public List<Evidence> Evidence = new List<Evidence>();
        public Board Board = new Board();
        public List<ApprovalRequest> Approvals = new List<ApprovalRequest>();
        public List<ShareOffer> ShareOffers = new List<ShareOffer>();
        public List<PlayerTask> Tasks = new List<PlayerTask>();
        public List<Negotiation> Negotiations = new List<Negotiation>();
        public List<Candidate> Candidates = new List<Candidate>();
        public List<Notification> Notifications = new List<Notification>();
        public List<LogEntry> Log = new List<LogEntry>();
        public List<ChatMessage> Chat = new List<ChatMessage>();
        public History History = new History();
        public GameOverSummary Summary;

        public GameState ShallowCopy() => (GameState)MemberwiseClone();

        public Player GetPlayer(int id)
        {
            for (int i = 0; i < Players.Count; i++) if (Players[i].Id == id) return Players[i];
            return null;
        }

        public PlayerPrivate GetPrivate(int id)
        {
            for (int i = 0; i < Private.Count; i++) if (Private[i].PlayerId == id) return Private[i];
            return null;
        }

        public string PlayerName(int id)
        {
            var p = GetPlayer(id);
            return p != null ? p.Name : (id == -1 ? "System" : "Unknown");
        }

        public MarketProduct GetMarketProduct(string id)
        {
            var list = Market.Products;
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i];
            return null;
        }

        public Supplier GetSupplier(string id)
        {
            for (int i = 0; i < Suppliers.Count; i++) if (Suppliers[i].Id == id) return Suppliers[i];
            return null;
        }

        public CompanyProduct GetCompanyProduct(string id)
        {
            var list = Company.Products;
            for (int i = 0; i < list.Count; i++) if (list[i].Id == id) return list[i];
            return null;
        }

        public CompanyProduct GetCompanyProductByMarketId(string marketId)
        {
            var list = Company.Products;
            for (int i = 0; i < list.Count; i++) if (list[i].ProductId == marketId && list[i].Active) return list[i];
            return null;
        }

        public GameEvent GetEvent(string id)
        {
            for (int i = 0; i < Events.Count; i++) if (Events[i].Id == id) return Events[i];
            return null;
        }

        public int Year => (Day - 1) / GameCalendar.DaysPerYear + 1;
        public int MonthIndex => ((Day - 1) / GameCalendar.DaysPerMonth) % 12;
        public int DayOfMonth => (Day - 1) % GameCalendar.DaysPerMonth + 1;
        public int QuarterOfYear => MonthIndex / 3 + 1;
        public long TotalPlayerShares
        {
            get { long s = 0; for (int i = 0; i < Players.Count; i++) s += Players[i].Shares; return s; }
        }
    }

    public static class GameCalendar
    {
        public const int DaysPerMonth = 30;
        public const int DaysPerQuarter = 90;
        public const int DaysPerYear = 360;
        public static readonly string[] MonthNames = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
    }

    public class GameSettings
    {
        public string CompanyName = "Nimbus Goods";
        public int Seed = 12345;
        public double StartingCash = 100000;
        public double StartingPersonalCash = 15000;
        public double StartingSalary = 3000;
        public double DefaultSpendingLimit = 25000;
        /// <summary>0 = endless.</summary>
        public int GameYears = 3;
        /// <summary>0 relaxed, 1 normal, 2 cutthroat.</summary>
        public int Difficulty = 1;
        public double SecondsPerDay = 3.0;
        /// <summary>Private offers, kickbacks, evidence and blackmail.</summary>
        public bool Intrigue = true;
        /// <summary>Real-time limit for board meetings (seconds). 0 = no limit.</summary>
        public double MeetingSeconds = 0;
        /// <summary>Percent of the company issued each quarter to partners in proportion to the XP they earned.</summary>
        public double PerformanceEquityPct = 1.0;
        /// <summary>Solo: pause automatically when a critical situation appears.</summary>
        public bool AutoPause = true;
        public bool Multiplayer;
        public List<SeatConfig> Seats = new List<SeatConfig>();
    }

    public class SeatConfig
    {
        public string Name;
        public bool IsAI;
        public Personality Personality;
        /// <summary>Ownership in percent; 0 means "split equally with the others".</summary>
        public double OwnershipPct;
    }

    public class Player
    {
        public int Id;
        public string Name;
        public bool IsAI;
        public Personality Personality;
        public int Color;
        public bool Connected = true;

        public double Salary;
        public long Shares;
        public int Performance;
        public int Xp;
        public int Level = 1;
        public int SkillPoints = 1;
        public List<string> Skills = new List<string>();
        public double Reputation = 50;
        public double SpendingLimit;
        public bool Ready;
        public int AwayUntilDay;
        public string Status = "";

        // Public career stats
        public double ProfitGenerated;
        public double QuarterProfit;
        public int QuarterXp;
        public int QuarterEvents;
        public int EventsResolved;
        public int ProductsLaunched;
        public int NegotiationsWon;
        public int CrisesHandled;
        public int ProposalsPassed;
        public int ScandalCount;
        public int Hires;
        public int Reports;
        public int Exposures;
        public int ContractsDelivered;
        public int UpgradesBought;
        /// <summary>AI partners only work on the areas they are responsible for (see <see cref="Roles"/>).</summary>
        public List<string> Roles = new List<string>();

        /// <summary>View-only: how much this AI partner trusts the viewing player (0-100), or -1.</summary>
        public double TrustToViewer = -1;

        public bool HasSkill(string id) => Skills.Contains(id);
        public Player ShallowCopy() => (Player)MemberwiseClone();
    }

    /// <summary>Data only the owning player (and the host) may see.</summary>
    public class PlayerPrivate
    {
        public int PlayerId;
        public double Cash;
        public List<Holding> Holdings = new List<Holding>();
        public List<Discovery> Notebook = new List<Discovery>();
        public double SalaryReceived;
        public double DividendsReceived;
        public double BonusesReceived;
        public double SecretIncome;
        public double ShareTradeIncome;
        public int OffersAccepted;
        public int OffersRejected;
        public int OffersReported;
        public AIMemory AI;
        /// <summary>Secret personal objective (see <see cref="Ambitions"/>). Empty until chosen.</summary>
        public string Ambition = "";
        public List<string> AmbitionChoices = new List<string>();
    }

    public class Holding
    {
        public string AssetId;
        public double Value;
        public double Invested;
        public int BoughtDay;
    }

    public class AIMemory
    {
        public Dictionary<int, double> Trust = new Dictionary<int, double>();
        public List<Promise> Promises = new List<Promise>();
        public double ActionBudget;
        public int LastChatDay;
        public int LastShareOfferDay;
        public int LastNeglectDay = -100;
        public List<string> HandledEvidence = new List<string>();
    }

    public class Promise
    {
        /// <summary>The player the AI made a deal with.</summary>
        public int OtherId;
        /// <summary>"support": AI promised to vote yes on OtherId's next proposal. "owed": OtherId promised to vote yes on ProposalId.</summary>
        public string Kind;
        public string ProposalId;
        public double Payment;
        public int CreatedDay;
        public bool Done;
    }
}
