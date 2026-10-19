using System;
using System.Collections.Generic;
using System.Globalization;

namespace Stakeholders.Core
{
    /// <summary>
    /// Runs the game. Owns no Unity code: it can run inside the Unity player (solo/host),
    /// a dedicated server, or a plain .NET test harness.
    /// </summary>
    public partial class GameSimulation
    {
        public GameState State { get; private set; }
        public Rng Rng { get; private set; }

        /// <summary>Raised after every simulated day (host uses it to push views).</summary>
        public event Action DayStepped;

        public GameSimulation(GameState state)
        {
            State = state;
            Rng = new Rng(state);
        }

        Company C => State.Company;

        // ------------------------------------------------------------------ time

        /// <summary>Advance by real time. Handles day stepping, board meeting timers and AI reaction delays.</summary>
        public void Advance(double realSeconds)
        {
            if (State.Phase == GamePhase.GameOver || State.Phase == GamePhase.Lobby) return;
            if (realSeconds < 0) realSeconds = 0;
            if (realSeconds > 1) realSeconds = 1;

            if (State.Phase == GamePhase.BoardMeeting)
            {
                TickMeeting(realSeconds);
                return;
            }

            TickApprovalsRealtime(realSeconds);

            if (State.Paused || State.Speed <= 0) return;
            State.DayProgress += realSeconds * State.Speed / Math.Max(0.05, State.Settings.SecondsPerDay);
            int guard = 0;
            while (State.DayProgress >= 1 && guard++ < 16 && State.Phase == GamePhase.Running)
            {
                State.DayProgress -= 1;
                StepDay();
            }
            if (State.DayProgress >= 1) State.DayProgress = 0.99;
        }

        /// <summary>Simulate one full day.</summary>
        public void StepDay()
        {
            if (State.Phase != GamePhase.Running) return;
            State.Day++;
            C.Today = new Financials();

            UpdateMarket();
            ArriveShipments();
            ContractsDaily();
            RunCampaigns();
            SimulateSales();
            AutoRestockDaily();
            UpdateOperations();
            DailyUpkeep();
            if (State.Day % GameCalendar.DaysPerMonth == 0) MonthEnd();
            UpdateHoldings();
            UpdateEvents();
            IntrigueDaily();
            TasksDaily();
            ApprovalsDaily();
            OffersDaily();
            ProgressionDaily();
            AIDaily();
            CloseDayBooks();
            UpdateValuation();
            GrowthDaily();
            RecordHistory();
            CheckGameEnd();
            if (State.Phase == GamePhase.Running && State.Day >= State.Board.NextMeetingDay) StartBoardMeeting();
            State.Revision++;
            DayStepped?.Invoke();
        }

        void CloseDayBooks()
        {
            var t = C.Today;
            C.Month.Add(t);
            C.Quarter.Add(t);
            C.Year.Add(t);
            C.Lifetime.Add(t);
            if (State.Day % GameCalendar.DaysPerMonth == 0)
            {
                C.LastMonth = C.Month.Copy();
                C.Month = new Financials();
            }
            if (State.Day % GameCalendar.DaysPerYear == 0) C.Year = new Financials();
            // The bank tolerates a modest overdraft; bankruptcy comes from staying deep in the red,
            // or from never climbing out of it.
            if (C.Cash < -OverdraftLimit()) C.NegativeCashDays++;
            else C.NegativeCashDays = 0;
            if (C.Cash < 0) C.OverdraftDays++;
            else C.OverdraftDays = 0;
        }

        public double OverdraftLimit() => Math.Max(20000, Math.Max(C.LastMonth.Revenue, C.Month.Revenue) * 0.5);

        // ------------------------------------------------------------------ helpers

        public string NewId(string prefix) => prefix + (State.NextId++).ToString(CultureInfo.InvariantCulture);

        public Player P(int id) => State.GetPlayer(id);
        public PlayerPrivate Priv(int id) => State.GetPrivate(id);

        public bool AnyoneHas(string skill)
        {
            foreach (var p in State.Players) if (p.HasSkill(skill)) return true;
            return false;
        }

        public bool Has(int playerId, string skill)
        {
            var p = P(playerId);
            return p != null && p.HasSkill(skill);
        }

        public double OwnershipPct(Player p) => C.TotalShares > 0 ? (double)p.Shares / C.TotalShares : 0;

        public double VotingWeight(Player p)
        {
            long total = State.TotalPlayerShares;
            return total > 0 ? (double)p.Shares / total : 0;
        }

        public bool IsAway(Player p) => p.AwayUntilDay > State.Day;

        public int MaxTasks(Player p) => 2 + (p.Level >= 8 ? 1 : 0) + (C.Has("ppl4") ? 1 : 0);

        public int ActiveTaskCount(int playerId)
        {
            int n = 0;
            foreach (var t in State.Tasks) if (t.PlayerId == playerId) n++;
            return n;
        }

        public int HumanCount
        {
            get { int n = 0; foreach (var p in State.Players) if (!p.IsAI) n++; return n; }
        }

        public double DifficultyMult => State.Settings.Difficulty == 0 ? 0.75 : State.Settings.Difficulty == 2 ? 1.3 : 1.0;

        public void Log(LogKind kind, int actor, string text)
        {
            State.Log.Add(new LogEntry { Day = State.Day, ActorId = actor, Kind = kind, Text = text });
            if (State.Log.Count > 300) State.Log.RemoveRange(0, State.Log.Count - 300);
        }

        public void Notify(int playerId, NoticeKind kind, string text)
        {
            if (playerId >= 0)
            {
                var target = P(playerId);
                if (target == null || target.IsAI) return; // AI partners do not read notifications
            }
            State.Notifications.Add(new Notification { Id = State.NextNoticeId++, PlayerId = playerId, Day = State.Day, Kind = kind, Text = text });
            if (State.Notifications.Count > 400) State.Notifications.RemoveRange(0, State.Notifications.Count - 400);
        }

        public void NotifyAll(NoticeKind kind, string text) => Notify(-1, kind, text);

        public void Say(int fromId, string text, int toId = -1)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > 400) text = text.Substring(0, 400);
            State.Chat.Add(new ChatMessage { Id = State.NextChatId++, Day = State.Day, FromId = fromId, ToId = toId, Text = text });
            if (State.Chat.Count > 300) State.Chat.RemoveRange(0, State.Chat.Count - 300);
        }

        /// <summary>Company pays money out. Books the expense category and writes a ledger line.</summary>
        public Transaction Spend(double amount, TxType type, int actor, string text, string flag = null, int flagLevel = 0, bool ledger = true)
        {
            if (amount <= 0) return null;
            C.Cash -= amount;
            Book(type, amount);
            return ledger ? AddLedger(type, -amount, actor, text, flag, flagLevel) : null;
        }

        /// <summary>Company receives money (not sales revenue; that is booked by the sales model).</summary>
        public Transaction Receive(double amount, TxType type, int actor, string text, bool ledger = true)
        {
            if (amount <= 0) return null;
            C.Cash += amount;
            if (type == TxType.Insurance || type == TxType.Other) C.Today.Other -= amount;
            return ledger ? AddLedger(type, amount, actor, text, null, 0) : null;
        }

        void Book(TxType type, double amount)
        {
            var t = C.Today;
            switch (type)
            {
                case TxType.Marketing: t.Marketing += amount; break;
                case TxType.Payroll:
                case TxType.Hiring:
                case TxType.Severance: t.Payroll += amount; break;
                case TxType.PartnerSalary: t.PartnerSalaries += amount; break;
                case TxType.Rent: t.Rent += amount; break;
                case TxType.Fees: t.Fees += amount; break;
                case TxType.Refunds: t.Refunds += amount; break;
                case TxType.Dividend: t.Dividends += amount; break;
                case TxType.Bonus: t.Bonuses += amount; break;
                case TxType.Tax: t.Tax += amount; break;
                case TxType.Inventory:
                case TxType.LoanPayment:
                case TxType.Capital:
                // Capital spending (upgrades, acquisitions, warehouse fit-outs) becomes an asset, not a loss:
                // building the business must not crash the valuation.
                case TxType.Investment:
                case TxType.Warehouse: break; // balance-sheet movements
                default: t.Other += amount; break;
            }
        }

        public Transaction AddLedger(TxType type, double signedAmount, int actor, string text, string flag, int flagLevel)
        {
            var tx = new Transaction
            {
                Id = NewId("t"), Day = State.Day, Type = type, Amount = signedAmount, ActorId = actor, Text = text,
                Flag = flag, FlagLevel = flagLevel
            };
            C.Ledger.Add(tx);
            if (C.Ledger.Count > 400) C.Ledger.RemoveRange(0, C.Ledger.Count - 400);
            return tx;
        }

        public void PayPlayer(int playerId, double amount)
        {
            var pr = Priv(playerId);
            if (pr != null) pr.Cash += amount;
        }

        public static double Clamp(double v, double lo, double hi) => v < lo ? lo : (v > hi ? hi : v);
        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);

        public string ProductName(string companyProductId)
        {
            var cp = State.GetCompanyProduct(companyProductId);
            return cp != null ? cp.Name : "a product";
        }
    }
}
