using System.Collections.Generic;

namespace Stakeholders.Core
{
    /// <summary>
    /// Builds what one player is allowed to see. Solo, host and clients all render from views,
    /// so the rules about secrets are enforced in exactly one place.
    /// The result shares unchanged sub-objects with the source: treat it as read-only.
    /// </summary>
    public static class ViewBuilder
    {
        public static GameState Build(GameState s, int viewerId)
        {
            var v = s.ShallowCopy();
            v.ViewerId = viewerId;
            v.RngState = 0;
            var viewer = s.GetPlayer(viewerId);
            var viewerPriv = s.GetPrivate(viewerId);

            // Partners: hide intrigue skills of others; surface AI trust toward the viewer.
            v.Players = new List<Player>(s.Players.Count);
            foreach (var p in s.Players)
            {
                var copy = p.ShallowCopy();
                if (p.Id != viewerId)
                {
                    copy.Skills = new List<string>();
                    foreach (var sk in p.Skills) if (!Skills.IsHidden(sk)) copy.Skills.Add(sk);
                }
                copy.TrustToViewer = -1;
                if (p.IsAI)
                {
                    var pr = s.GetPrivate(p.Id);
                    if (pr?.AI != null) copy.TrustToViewer = pr.AI.Trust.TryGetValue(viewerId, out double t) ? t : 50;
                }
                v.Players.Add(copy);
            }

            v.Private = new List<PlayerPrivate>();
            if (viewerPriv != null) v.Private.Add(viewerPriv);

            v.Offers = s.Offers.FindAll(o => o.TargetId == viewerId || o.FromId == viewerId);

            v.Evidence = new List<Evidence>();
            foreach (var e in s.Evidence)
            {
                if (!e.Exposed && !e.DiscoveredBy.Contains(viewerId)) continue;
                var copy = e.ShallowCopy();
                copy.DiscoveredBy = e.DiscoveredBy.Contains(viewerId) ? new List<int> { viewerId } : new List<int>();
                copy.Silenced = e.Silenced.Contains(viewerId) ? new List<int> { viewerId } : new List<int>();
                v.Evidence.Add(copy);
            }

            v.Negotiations = new List<Negotiation>();
            foreach (var n in s.Negotiations)
            {
                if (n.PlayerId != viewerId) continue;
                var copy = n.ShallowCopy();
                copy.MaxDiscount = 0;
                v.Negotiations.Add(copy);
            }

            v.Notifications = s.Notifications.FindAll(n => n.PlayerId == viewerId || n.PlayerId == -1);
            v.Chat = s.Chat.FindAll(m => m.ToId == -1 || m.ToId == viewerId || m.FromId == viewerId);
            v.ShareOffers = s.ShareOffers.FindAll(o => o.FromId == viewerId || o.ToId == viewerId);
            v.Events = s.Events.FindAll(e => s.Day >= e.PublicDay || e.EarlyAccess.Contains(viewerId));
            v.Tasks = s.Tasks.FindAll(t => t.PlayerId == viewerId || !t.Secret);

            // Market: hidden truth stays hidden.
            bool trendHunter = viewer != null && viewer.HasSkill(Skills.TrendHunter);
            bool expert = viewer != null && viewer.HasSkill(Skills.ProductExpert);
            v.Market = new Market { Economy = s.Market.Economy };
            foreach (var mp in s.Market.Products)
            {
                var copy = mp.ShallowCopy();
                copy.Redacted = true;
                copy.TrendVisible = trendHunter;
                copy.DetailsVisible = expert;
                copy.BaseDemand = 0;
                copy.TrendTarget = 0;
                copy.PhaseDays = 0;
                if (!trendHunter) { copy.Trend = 0; copy.Phase = TrendPhase.Stable; }
                if (!expert) { copy.Elasticity = 0; copy.ViralPotential = 0; copy.Competition = 0; }
                if (copy.ReservedBy != viewerId) { copy.ReservedBy = -1; copy.ReservedUntil = 0; }
                v.Market.Products.Add(copy);
            }

            v.Suppliers = new List<Supplier>(s.Suppliers.Count);
            foreach (var sup in s.Suppliers)
            {
                var copy = sup.ShallowCopy();
                copy.Shadiness = 0;
                v.Suppliers.Add(copy);
            }

            // Ledger anomaly flags are only visible to accountants (or everyone with open books).
            bool seesFlags = s.Company.Policies.OpenBooks || s.Company.Has("dat3") || (viewer != null && viewer.HasSkill(Skills.Accountant));
            if (!seesFlags)
            {
                var c = s.Company.ShallowCopy();
                c.Ledger = new List<Transaction>(s.Company.Ledger.Count);
                foreach (var tx in s.Company.Ledger)
                {
                    if (tx.Flag == null) { c.Ledger.Add(tx); continue; }
                    var copy = tx.ShallowCopy();
                    copy.Flag = null;
                    copy.FlagLevel = 0;
                    c.Ledger.Add(copy);
                }
                v.Company = c;
            }

            // Secret ballot: others' pending votes are visible as "voted" only.
            if (s.Company.Policies.SecretBallot)
            {
                var b = s.Board.ShallowCopy();
                b.Agenda = new List<Proposal>();
                foreach (var prop in s.Board.Agenda)
                {
                    var copy = prop.ShallowCopy();
                    copy.Votes = new List<VoteRecord>();
                    foreach (var vote in prop.Votes)
                        copy.Votes.Add(vote.PlayerId == viewerId ? vote : new VoteRecord { PlayerId = vote.PlayerId, Choice = VoteChoice.None, Weight = vote.Weight });
                    b.Agenda.Add(copy);
                }
                v.Board = b;
            }
            return v;
        }
    }
}
