using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>A shared situation (problem or opportunity) any partner can handle.</summary>
    public class GameEvent
    {
        public string Id;
        public string TemplateId;
        public EventCategory Category;
        public string Title;
        public string Text;
        /// <summary>1 low, 2 medium, 3 critical.</summary>
        public int Severity = 1;
        public int CreatedDay;
        public int DeadlineDay;
        public int ClaimedBy = -1;
        public int ClaimedUntil;
        public List<EventChoice> Choices = new List<EventChoice>();

        // Context
        public string ProductId;      // CompanyProduct id (or market product id for opportunities)
        public string SupplierId;
        public string EmployeeId;
        public string CampaignId;
        public string ShipmentId;
        public double Amount;
        public double Amount2;
        public int Count;

        /// <summary>Players who see this event before it becomes public (Trend Hunter early notices).</summary>
        public List<int> EarlyAccess = new List<int>();
        public int PublicDay;

        public bool Resolved;
        public int ResolvedBy = -1;
        public int ResolvedDay;
        public int ChosenIndex = -1;
        public string Outcome;
        public bool PendingApproval;
    }

    public class EventChoice
    {
        public string Label;
        public string Detail;
        public double Cost;
        public string RequiresSkill;
        /// <summary>Alternative skill that also satisfies the requirement.</summary>
        public string RequiresSkillAlt;
        public int BusyDays;
        // AI hints
        public double AiCompany;
        public double AiSelf;
        public double AiRisk;
    }

    /// <summary>A private opportunity only the target sees. The best choice for you is not always the best for the company.</summary>
    public class PrivateOffer
    {
        public string Id;
        public OfferKind Kind;
        public int TargetId;
        public int FromId = -1;
        public string FromName;
        public string Title;
        public string Text;
        public double PersonalAmount;
        public double CompanyCost;
        public string SupplierId;
        public string ProductId;
        public string ProposalId;
        public string EvidenceId;
        public string EventId;
        public long Shares;
        public int Quantity;
        public double PremiumPct;
        public int CreatedDay;
        public int ExpiresDay;
        public int FulfillBy;
        public bool Reportable = true;
        public OfferStatus Status;
        public string Result;
    }

    /// <summary>A trace left by a questionable action. It may be discovered, used for blackmail, or exposed.</summary>
    public class Evidence
    {
        public string Id;
        public int ActorId;
        public OfferKind Kind;
        public string Summary;
        public double Amount;
        public int Day;
        public double Severity;
        public List<int> DiscoveredBy = new List<int>();
        public List<int> Silenced = new List<int>();
        public bool Exposed;
        public int ExposedBy = -1;
        public int ExposedDay;
        public bool Denied;

        public Evidence ShallowCopy() => (Evidence)MemberwiseClone();
    }

    public class Board
    {
        public int NextMeetingDay = GameCalendar.DaysPerQuarter;
        public int MeetingNumber;
        public double MeetingTimeLeft;
        public double MeetingElapsed;
        public bool Extraordinary;
        public double LastValuation;
        public List<Proposal> Agenda = new List<Proposal>();
        public List<Proposal> History = new List<Proposal>();
        public QuarterReport LastReport;

        public Board ShallowCopy() => (Board)MemberwiseClone();
    }

    public class Proposal
    {
        public string Id;
        public ProposalType Type;
        public int ProposerId;
        public int TargetId = -1;
        public double Amount;
        public string OptionId;
        public string Text;
        public int CreatedDay;
        public List<VoteRecord> Votes = new List<VoteRecord>();
        public ProposalStatus Status;
        public string Result;
        public int ResolvedDay;
        /// <summary>Seconds this proposal has been open during a meeting (drives AI vote timing).</summary>
        public double Age;

        // Context shown to voters
        public double CurrentValue;
        public double ContextProfit;
        public int ContextPerformance;

        public Proposal ShallowCopy() => (Proposal)MemberwiseClone();
    }

    public class VoteRecord
    {
        public int PlayerId;
        public VoteChoice Choice;
        public double Weight;
    }

    public class QuarterReport
    {
        public int Year;
        public int Quarter;
        public double Revenue;
        public double Profit;
        public double CashEnd;
        public double ValuationStart;
        public double ValuationEnd;
        public List<PlayerQuarter> Players = new List<PlayerQuarter>();
    }

    public class PlayerQuarter
    {
        public int PlayerId;
        public double ProfitGenerated;
        public int Xp;
        public int Events;
        public int Performance;
        public long SharesGranted;
    }

    /// <summary>Spending above a partner's limit waits for the other partners.</summary>
    public class ApprovalRequest
    {
        public string Id;
        public int RequesterId;
        public Command Command;
        public string Summary;
        public double Amount;
        public List<VoteRecord> Votes = new List<VoteRecord>();
        public int CreatedDay;
        public int ExpiresDay;
        public ProposalStatus Status;
        public string Result;
        public double Age;
    }

    public class ShareOffer
    {
        public string Id;
        public int FromId;
        public int ToId;
        /// <summary>True: From wants to buy shares from To. False: From wants to sell shares to To.</summary>
        public bool FromBuys;
        public long Shares;
        public double Price;
        public string Note;
        public int CreatedDay;
        public int ExpiresDay;
        public OfferStatus Status;
    }

    public class PlayerTask
    {
        public string Id;
        public int PlayerId;
        public TaskKind Kind;
        public string Label;
        public string Param;
        public int Param2;
        public int StartDay;
        public int EndDay;
        public bool Secret;
    }

    public class Negotiation
    {
        public string Id;
        public int PlayerId;
        public string SupplierId;
        public double MaxDiscount;
        public double HintLow;
        public double HintHigh;
        public bool HintKnown;
        public int RoundsLeft;
        public double LastCounter;
        public List<string> Transcript = new List<string>();
        public NegotiationStatus Status;
        public double AgreedDiscount;
        public int StartedDay;

        public Negotiation ShallowCopy() => (Negotiation)MemberwiseClone();
    }

    public class Notification
    {
        public int Id;
        public int PlayerId = -1;
        public int Day;
        public NoticeKind Kind;
        public string Text;
    }

    public class LogEntry
    {
        public int Day;
        public int ActorId = -1;
        public LogKind Kind;
        public string Text;
    }

    public class ChatMessage
    {
        public int Id;
        public int Day;
        public int FromId;
        /// <summary>-1 = everyone.</summary>
        public int ToId = -1;
        public string Text;
    }

    public class GameOverSummary
    {
        public string Reason;
        public double FinalValuation;
        public int Stage;
        public int CompanyRank;
        public int CompanyCount;
        public bool Ipo;
        public List<FinalStanding> Standings = new List<FinalStanding>();
        public List<string> Secrets = new List<string>();
        public List<string> Awards = new List<string>();
    }

    public class FinalStanding
    {
        public int PlayerId;
        public string Name;
        public double PersonalCash;
        public double Investments;
        public double EquityValue;
        public double NetWorth;
        public double OwnershipPct;
        public int Performance;
        public int Level;
        public double SecretIncome;
        public int Rank;
        public string Ambition;
        public bool AmbitionDone;
        public double AmbitionBonus;
    }

    /// <summary>
    /// Every player action is a Command. Solo, host and AI all go through the same processor,
    /// which keeps multiplayer and single player identical.
    /// </summary>
    public class Command
    {
        public CommandType Type;
        public int PlayerId;
        public string A;
        public string B;
        public int I;
        public int J;
        public double X;
        public double Y;
        public string Text;
        public bool Flag;
        /// <summary>Set by the host only, when replaying an approved request.</summary>
        public bool Approved;

        public Command Clone() => (Command)MemberwiseClone();
    }

    public struct CmdResult
    {
        public bool Ok;
        public string Message;

        public static CmdResult Success(string message = null) => new CmdResult { Ok = true, Message = message };
        public static CmdResult Fail(string message) => new CmdResult { Ok = false, Message = message };
    }
}
