using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.Core;

namespace Stakeholders.App
{
    public class NextStep
    {
        public int Priority;
        public string Text;
        public string Button;
        public Action Act;
        public bool Urgent;
    }

    /// <summary>
    /// "What should I do now?" Reads the local player's view and suggests the most valuable actions,
    /// focusing on the areas no AI partner is handling.
    /// </summary>
    public static class Advisor
    {
        public static List<NextStep> For(GameScreen s)
        {
            var list = new List<NextStep>();
            var v = s.View;
            var me = s.MePlayer;
            var pr = s.MePriv;
            var q = s.Q;
            if (v == null || me == null || pr == null || q == null || v.Phase == GamePhase.GameOver) return list;
            var c = v.Company;

            void Add(int priority, string text, string button, Action act, bool urgent = false) =>
                list.Add(new NextStep { Priority = priority, Text = text, Button = button, Act = act, Urgent = urgent });
            bool Delegated(string role) => v.Players.Any(p => p.IsAI && p.Roles.Contains(role));

            if (string.IsNullOrEmpty(pr.Ambition) && pr.AmbitionChoices.Count > 0)
                Add(100, "Choose your secret ambition. Completing it pays a big bonus at the end.", "Choose", () => AmbitionPicker.Show(s), true);

            if (v.Phase == GamePhase.BoardMeeting)
                Add(98, "The board meeting is in session. Cast your votes.", "Go to the board", () => s.Navigate(s.Board), true);

            foreach (var e in v.Events.Where(e => e.Severity >= 3 && !e.PendingApproval && (e.ClaimedBy < 0 || e.ClaimedBy == s.Me || e.ClaimedUntil < v.Day)).OrderBy(e => e.DeadlineDay).Take(1))
                Add(92, $"Crisis: {e.Title} ({DaysText(e.DeadlineDay - v.Day)} left).", "Handle it", () => s.Navigate(s.Situations), true);

            foreach (var cp in c.Products.Where(p => p.Active && p.ManagerId < 0).Take(1))
            {
                var prod = cp;
                Add(88, $"Nobody manages {cp.Name}. Take charge and its profits count toward your performance.", "Claim it", () => s.Send(new Command { Type = CommandType.ClaimProduct, A = prod.Id }));
            }

            // Restocking: always worth flagging when it's urgent, even if an AI partner is in charge.
            bool buyingIsMine = !Delegated(Roles.Buying);
            foreach (var cp in c.Products.Where(p => p.Active && !p.AutoRestock))
            {
                int inbound = c.Shipments.Where(x => x.CompanyProductId == cp.Id).Sum(x => x.Quantity);
                if (inbound > 0) continue;
                double rate = Math.Max(cp.Sales7, cp.Demand7 * 0.8);
                if (rate < 0.5) continue;
                double days = cp.Stock / rate;
                var sup = v.GetSupplier(cp.SupplierId);
                int lead = sup?.LeadTime ?? 10;
                if (days > lead + 4) continue;
                if (!buyingIsMine && days > 2) continue;
                var prod = cp;
                Add(cp.Stock == 0 ? 90 : 84, cp.Stock == 0 ? $"{cp.Name} is sold out. Every day costs sales." : $"Restock {cp.Name}: about {DaysText(Math.Max(1, (int)days))} of stock left, deliveries take {DaysText(lead)}.",
                    "Order", () => Dialogs.Order(s, prod), cp.Stock == 0);
                break;
            }

            foreach (var k in c.Contracts.Where(k => k.Status == 0))
            {
                var cp = v.Company.Products.FirstOrDefault(p => p.Id == k.CompanyProductId);
                if (cp == null) continue;
                int inbound = c.Shipments.Where(x => x.CompanyProductId == cp.Id).Sum(x => x.Quantity);
                if (cp.Stock + inbound >= k.Quantity) continue;
                var prod = cp;
                Add(82, $"The {k.Client} contract needs {Fmt.Int(k.Quantity)} x {cp.Name} by {Fmt.ShortDate(k.DueDay)}. We have {Fmt.Int(cp.Stock + inbound)}.", "Order", () => Dialogs.Order(s, prod), k.DueDay - v.Day < 7);
                break;
            }

            int votes = v.Board.Agenda.Count(p => p.Status == ProposalStatus.Pending && !p.Votes.Exists(x => x.PlayerId == s.Me))
                        + v.Approvals.Count(a => a.Status == ProposalStatus.Pending && !a.Votes.Exists(x => x.PlayerId == s.Me));
            if (votes > 0 && v.Phase != GamePhase.BoardMeeting)
                Add(70, votes == 1 ? "A vote is waiting for you." : $"{votes} votes are waiting for you.", "Vote", () => s.Navigate(s.Board));

            if (me.SkillPoints > 0)
                Add(66, $"You have {me.SkillPoints} skill point{(me.SkillPoints == 1 ? "" : "s")} to spend.", "Learn a skill", () => s.Navigate(s.Career));

            // Products pipeline
            int active = c.Products.Count(p => p.Active);
            int desired = Math.Min(16, 3 + v.Day / 60);
            if (!Delegated(Roles.Research) || active < 3)
            {
                var ideas = pr.Notebook.Concat(c.ResearchBoard).Where(d => !d.Launched && v.GetCompanyProductByMarketId(d.ProductId) == null && v.Day - d.Day < 90)
                    .OrderByDescending(d => d.DemandMid * (d.RefPrice * 0.86 - d.EstCost - GameSimulation.ShippingPerUnit)).ToList();
                if (active < desired && ideas.Count > 0)
                {
                    var best = ideas[0];
                    Add(60, $"Launch {best.Name}: about {Fmt.Int(best.DemandMid)} sales a day at {Fmt.Price(best.RefPrice)}.", "Launch", () => Dialogs.Launch(s, best));
                }
                else if (active < desired && !v.Tasks.Any(t => t.PlayerId == s.Me && t.Kind == TaskKind.Research))
                    Add(58, "Your product pipeline is empty. Research the market for new products.", "Research", () => s.Navigate(s.Market));
            }

            if (c.Stage >= 1 && string.IsNullOrEmpty(c.Positioning) && !v.Board.Agenda.Any(x => x.Type == ProposalType.Positioning && x.Status == ProposalStatus.Pending))
                Add(56, "The company has no strategy yet. Premium, value or trends: pick a direction.", "Choose", () => s.Navigate(s.Growth));

            if (!Delegated(Roles.Upgrades) || c.Cash > 400000)
            {
                var up = Catalog.Upgrades.Where(u => !c.Has(u.Id) && !c.Building.Exists(b => b.Id == u.Id) && q.UpgradeUnlocked(u, me, out _) && u.Cost <= c.Cash * 0.4)
                    .OrderBy(u => u.Tier).ThenBy(u => u.Cost).FirstOrDefault();
                if (up != null) Add(50, $"The company can afford {up.Name} ({Fmt.Money(up.Cost)}): {up.Description}", "See upgrades", () => s.Navigate(s.Growth));
            }

            if (!Delegated(Roles.Research) || c.Cash > 300000)
            {
                var hero = c.Products.Where(p => p.Active && p.Sales7 >= 5 && q.CanUpgradeProduct(p, out _) && q.ProductUpgradeCost(p) <= c.Cash * 0.35)
                    .OrderByDescending(p => p.Sales7 * p.Price).FirstOrDefault();
                if (hero != null)
                {
                    var h = hero;
                    Add(46, $"Develop {hero.BaseName ?? hero.Name} {(hero.Version == 1 ? "Plus" : "Pro")} ({Fmt.Money(q.ProductUpgradeCost(hero))}): more demand and a higher price for your best seller.",
                        "Start R&D", () => s.Send(new Command { Type = CommandType.UpgradeProduct, A = h.Id }));
                }
            }

            if (!Delegated(Roles.Marketing))
            {
                var slow = c.Products.Where(p => p.Active && !p.PromoActive(v.Day) && v.Day >= p.PromoCooldownUntil && p.Stock >= 50 && p.Sales7 >= 1 && p.Stock / Math.Max(0.1, p.Sales7) > 60)
                    .OrderByDescending(p => p.Stock / Math.Max(0.1, p.Sales7)).FirstOrDefault();
                if (slow != null)
                {
                    var p0 = slow;
                    Add(42, $"{slow.Name} has {Fmt.Int(slow.Stock / Math.Max(0.1, slow.Sales7))} days of stock. A flash sale would move it.", "Flash sale", () => s.Send(new Command { Type = CommandType.StartPromotion, A = p0.Id, I = 0 }));
                }
            }

            if (c.Has("dat2"))
            {
                var manual = c.Products.FirstOrDefault(p => p.Active && !p.AutoRestock && p.Sales7 >= 3);
                if (manual != null)
                {
                    var p0 = manual;
                    Add(38, $"Demand Forecasting is built: let {manual.Name} restock itself.", "Auto-restock", () => s.Send(new Command { Type = CommandType.SetAutoRestock, A = p0.Id, Flag = true, I = 30 }));
                }
            }

            var target = v.Rivals.Where(r => q.CanAcquire(r, out _) && c.Cash >= q.AcquisitionPrice(r)).OrderBy(r => q.AcquisitionPrice(r)).FirstOrDefault();
            if (target != null && !v.Board.Agenda.Any(x => x.Type == ProposalType.AcquireRival && x.Status == ProposalStatus.Pending))
                Add(36, $"We could buy {target.Name} for {Fmt.Money(q.AcquisitionPrice(target))} and take over their customers.", "Competition", () => s.Navigate(s.Rivals));

            if (c.Stage >= Stages.Max && !c.IpoPending && !c.WentPublic && !v.Board.Agenda.Any(x => x.Type == ProposalType.GoPublic && x.Status == ProposalStatus.Pending))
                Add(95, "The company is big enough to go public. Propose the IPO.", "IPO", () => s.Navigate(s.Rivals), true);

            if (!Delegated(Roles.Hiring) && c.Backlog > c.CapacityPerDay * 0.4)
                Add(64, $"{Fmt.Int(c.Backlog)} orders are waiting to ship. Hire warehouse staff.", "Staff", () => s.Navigate(s.Operations));

            int incomingAll = c.Shipments.Sum(x => x.Quantity);
            if (c.StorageCapacity > 0 && (c.StorageUsed + incomingAll) > c.StorageCapacity * 0.88 && !Delegated(Roles.Upgrades))
                Add(54, "The warehouse is almost full. Open more space before deliveries get turned away.", "Warehouses", () => s.Navigate(s.Operations));

            int humans = v.Players.Count(p => !p.IsAI);
            int mine = Roles.All.Count(r => !Delegated(r));
            if (humans == 1 && mine >= 5 && v.Players.Any(p => p.IsAI) && v.Day < 120)
                Add(30, "You are doing almost everything yourself. Hand some areas to your AI partners.", "Delegate", () => s.Navigate(s.Partners));

            return list.OrderByDescending(x => x.Priority).Take(6).ToList();
        }

        static string DaysText(int d) => d <= 0 ? "today" : d == 1 ? "1 day" : d + " days";
    }
}
