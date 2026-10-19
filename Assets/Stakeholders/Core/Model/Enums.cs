namespace Stakeholders.Core
{
    public enum GamePhase { Lobby = 0, Running = 1, BoardMeeting = 2, GameOver = 3 }

    public enum Personality { Human = 0, Greedy, Loyal, RiskTaker, Conservative, Workaholic, Lazy, Manipulative, Ambitious }

    public enum TrendPhase { Dormant, Emerging, Rising, Peak, Declining, Stable }

    public enum AdChannel { Search, Social, Email, Influencer }

    public enum EmployeeRole { Warehouse, Support, Marketing, Buyer, Analyst, Accountant }

    public enum EventCategory { Inventory, Marketing, Customer, Supplier, HR, Competition, Opportunity, Logistics, Finance, Operations, Crisis }

    public enum OfferKind { Kickback, DataSale, FakeInvoice, InfluencerSkim, PoachJob, InsiderTip, SpeakingGig, SideConsulting, VoteBuy, VoteTrade, ShareSale, Blackmail }

    public enum OfferStatus { Pending, Accepted, Rejected, Reported, Expired, Fulfilled, Broken, Paid }

    public enum ProposalType { SalaryChange, Bonus, Dividend, Loan, Warehouse, Investment, RaiseCapital, Policy, ExecutiveLimit, Discipline, TransferProduct, SellCompany, Positioning, AcquireRival, GoPublic }

    public enum ProposalStatus { Pending, Passed, Failed, Withdrawn }

    public enum VoteChoice { None, Yes, No, Abstain }

    public enum TaskKind { Research, Recruit, Investigate, PersonalGig, PublicRelations }

    public enum NegotiationStatus { Active, Accepted, Failed, WalkedAway }

    public enum NoticeKind { Info, Success, Warning, Danger, Private, Xp, Celebration }

    public enum LogKind { Info, Company, Finance, Board, Scandal, Personal, Market, Event, Team }

    public enum TxType
    {
        Revenue, Inventory, Marketing, Payroll, PartnerSalary, Rent, LoanIn, LoanPayment, Fees, Refunds,
        Research, Hiring, Warehouse, Investment, Dividend, Bonus, Consulting, EventCost, Fine, Liquidation,
        Capital, Insurance, Severance, Other, Tax
    }

    public enum CommandType
    {
        // session / meta
        SetSpeed, TogglePause, Chat, SetReady,
        // research & products
        StartResearch, ShareDiscovery, LaunchProduct, SetPrice, OrderInventory, Liquidate, SetPreferredSupplier,
        // suppliers
        StartNegotiation, NegotiationOffer, NegotiationWalkAway,
        // marketing
        StartCampaign, StopCampaign,
        // operations
        Recruit, Hire, Fire, BuyWarehouse,
        // finance
        TakeLoan, RepayLoan, CompanyInvest, SetPolicy,
        // situations
        ClaimEvent, ResolveEvent,
        // private
        RespondOffer, ExposeEvidence, BlackmailEvidence, IgnoreEvidence, StartInvestigation,
        // board
        CreateProposal, CastVote, WithdrawProposal, VoteApproval,
        // shares
        CreateShareOffer, RespondShareOffer, CancelShareOffer,
        // career
        UnlockSkill, PersonalInvest, PersonalSell,
        // debug / host
        AdvanceDays,
        // growth & tactics (added in the strategy pass; appended to keep saved numbers stable)
        BuyUpgrade, StartPromotion, UpgradeProduct, SetAutoRestock, ClaimProduct, SetRoles, ChooseAmbition, AcceptContract, SetOption
    }
}
