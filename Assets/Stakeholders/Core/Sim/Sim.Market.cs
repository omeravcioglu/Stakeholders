using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    public partial class GameSimulation
    {
        // ------------------------------------------------------------------ trends

        public void InitTrend(MarketProduct mp)
        {
            double r = Rng.Next();
            TrendPhase phase =
                r < 0.25 ? TrendPhase.Dormant :
                r < 0.70 ? TrendPhase.Stable :
                r < 0.82 ? TrendPhase.Emerging :
                r < 0.92 ? TrendPhase.Rising :
                r < 0.97 ? TrendPhase.Peak : TrendPhase.Declining;
            EnterPhase(mp, phase);
            mp.Trend = mp.TrendTarget * Rng.Range(0.85, 1.05);
            mp.PhaseDays = Rng.Range(5, Math.Max(6, mp.PhaseDays));
        }

        double TrendAmplitude(MarketProduct mp) => 1.2 + 1.5 * mp.ViralPotential;

        void EnterPhase(MarketProduct mp, TrendPhase phase)
        {
            mp.Phase = phase;
            switch (phase)
            {
                case TrendPhase.Dormant: mp.TrendTarget = Rng.Range(0.35, 0.6); mp.PhaseDays = Rng.Range(60, 160); break;
                case TrendPhase.Emerging: mp.TrendTarget = Rng.Range(0.9, 1.15); mp.PhaseDays = Rng.Range(12, 25); break;
                case TrendPhase.Rising: mp.TrendTarget = TrendAmplitude(mp) * Rng.Range(0.85, 1.1); mp.PhaseDays = Rng.Range(18, 40); break;
                case TrendPhase.Peak: mp.TrendTarget = Math.Max(mp.Trend, 1.1) * Rng.Range(0.95, 1.05); mp.PhaseDays = Rng.Range(12, 35); break;
                case TrendPhase.Declining: mp.TrendTarget = Rng.Range(0.7, 0.95); mp.PhaseDays = Rng.Range(25, 50); break;
                case TrendPhase.Stable: mp.TrendTarget = Rng.Range(0.8, 1.15); mp.PhaseDays = Rng.Range(40, 150); break;
            }
        }

        static double PhaseRate(TrendPhase p)
        {
            switch (p)
            {
                case TrendPhase.Emerging: return 0.08;
                case TrendPhase.Rising: return 0.07;
                case TrendPhase.Peak: return 0.05;
                case TrendPhase.Declining: return 0.04;
                default: return 0.03;
            }
        }

        void UpdateMarket()
        {
            var m = State.Market;
            m.Economy = Clamp(m.Economy + (1 - m.Economy) * 0.01 + Rng.Normal(0, 0.004), 0.8, 1.2);
            foreach (var mp in m.Products)
            {
                mp.PhaseDays--;
                if (mp.PhaseDays <= 0)
                {
                    TrendPhase next;
                    switch (mp.Phase)
                    {
                        case TrendPhase.Dormant: next = Rng.Chance(0.6) ? TrendPhase.Emerging : TrendPhase.Stable; break;
                        case TrendPhase.Emerging: next = TrendPhase.Rising; break;
                        case TrendPhase.Rising: next = TrendPhase.Peak; break;
                        case TrendPhase.Peak: next = TrendPhase.Declining; break;
                        case TrendPhase.Declining: next = TrendPhase.Stable; break;
                        default:
                            double r = Rng.Next();
                            next = r < 0.35 ? TrendPhase.Dormant : r < 0.6 ? TrendPhase.Emerging : TrendPhase.Stable;
                            break;
                    }
                    EnterPhase(mp, next);
                    if (next == TrendPhase.Rising) OnProductStartsRising(mp);
                }
                mp.Trend = Clamp(mp.Trend + (mp.TrendTarget - mp.Trend) * PhaseRate(mp.Phase) + Rng.Normal(0, 0.008), 0.15, 5);

                double compTarget = mp.RefPrice * (0.97 - 0.05 * mp.Competition - (DiscounterIn(mp.Category) ? 0.06 : 0));
                mp.CompetitorPrice = Clamp(mp.CompetitorPrice + (compTarget - mp.CompetitorPrice) * 0.03 + Rng.Normal(0, mp.RefPrice * 0.004), mp.RefPrice * 0.6, mp.RefPrice * 1.3);
                // Success attracts copycats: the more we sell, the more competitors pile in.
                var sold = State.GetCompanyProductByMarketId(mp.Id);
                double copycats = sold != null ? 0.0009 * Math.Min(4, sold.Sales7 / 40.0) * (State.Settings.Difficulty == 2 ? 1.4 : 1) : 0;
                // Rival companies focused on this category keep competition high.
                double compBase = Clamp(0.33 + 0.08 * RivalPresence(mp.Category), 0.2, 0.8);
                double drift = (mp.Phase == TrendPhase.Peak ? 0.002 : mp.Phase == TrendPhase.Dormant ? -0.002 : 0) + (compBase - mp.Competition) * 0.002;
                mp.Competition = Clamp(mp.Competition + drift + copycats + Rng.Normal(0, 0.003), 0.05, 0.95);
                if (mp.Viral && State.Day > mp.ViralUntil) mp.Viral = false;
                if (mp.ReservedBy >= 0 && State.Day > mp.ReservedUntil) mp.ReservedBy = -1;

                // Organic virality for products we actively sell during a hype phase.
                if (!mp.Viral && (mp.Phase == TrendPhase.Rising || mp.Phase == TrendPhase.Peak))
                {
                    var cp = State.GetCompanyProductByMarketId(mp.Id);
                    if (cp != null && cp.Stock > 0 && Rng.Chance(mp.ViralPotential * 0.0008 * ViralMultiplier())) TriggerViral(mp, cp, -1);
                }
            }
        }

        void OnProductStartsRising(MarketProduct mp)
        {
            if (State.GetCompanyProductByMarketId(mp.Id) != null) return;
            if (Rng.Chance(0.55)) SpawnTrendEvent(mp);
        }

        // ------------------------------------------------------------------ research

        public double ResearchCost(int depth)
        {
            double cost = depth == 1 ? 5000 : 1500;
            if (C.Has("dat1")) cost *= 0.7;
            return cost;
        }

        public int ResearchDays(int depth)
        {
            int days = depth == 1 ? 4 : 2;
            if (EmployeePower(EmployeeRole.Analyst) >= 1.5) days--;
            if (C.Positioning == "trend") days--;
            return Math.Max(1, days);
        }

        /// <summary>Generate research discoveries for a player.</summary>
        public List<Discovery> RunResearch(int playerId, string category, int depth)
        {
            var result = new List<Discovery>();
            var pr = Priv(playerId);
            var p = P(playerId);
            int count = depth == 1 ? 3 : 2;
            if (C.Has("dat1")) count++;
            bool trendHunter = p.HasSkill(Skills.TrendHunter);
            bool early = p.HasSkill(Skills.EarlyAccess);

            var pool = new List<MarketProduct>();
            foreach (var mp in State.Market.Products)
            {
                if (!string.IsNullOrEmpty(category) && category != "Any" && mp.Category != category) continue;
                if (State.GetCompanyProductByMarketId(mp.Id) != null) continue;
                if (mp.Rarity >= 2 && !early) continue;
                if (mp.ReservedBy >= 0 && mp.ReservedBy != playerId) continue;
                bool known = false;
                foreach (var d in pr.Notebook) if (d.ProductId == mp.Id) { known = true; break; }
                if (known) continue;
                pool.Add(mp);
            }

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                var pick = Rng.PickWeighted(pool, mp =>
                {
                    double w = mp.Rarity == 1 ? 0.6 : mp.Rarity == 2 ? 0.8 : 1.0;
                    if (mp.Phase == TrendPhase.Emerging || mp.Phase == TrendPhase.Rising) w *= trendHunter ? 3.0 : 1.5;
                    if (mp.Phase == TrendPhase.Dormant) w *= 0.6;
                    if (depth == 1) w *= 0.5 + mp.BaseDemand / 60.0;
                    return w;
                });
                pool.Remove(pick);
                var disc = MakeDiscovery(pick, playerId, depth);
                if (early && (pick.Rarity >= 1 || Rng.Chance(0.5)))
                {
                    disc.Exclusive = true;
                    pick.ReservedBy = playerId;
                    pick.ReservedUntil = State.Day + 30;
                }
                pr.Notebook.Add(disc);
                result.Add(disc);
            }
            if (pr.Notebook.Count > 40) pr.Notebook.RemoveRange(0, pr.Notebook.Count - 40);
            return result;
        }

        public Discovery MakeDiscovery(MarketProduct mp, int playerId, int depth)
        {
            var p = P(playerId);
            double truth = mp.BaseDemand * mp.Trend * Seasonality(mp.Category, State.MonthIndex) * State.Market.Economy;
            double sigma = 0.35;
            if (p.HasSkill(Skills.MarketAnalyst)) sigma = 0.12;
            else if (EmployeePower(EmployeeRole.Analyst) >= 1) sigma = 0.22;
            if (depth == 1) sigma *= 0.8;
            double est = truth * Math.Max(0.3, 1 + Rng.Normal(0, sigma * 0.6));
            CheapestUnitPrice(mp, 500, playerId, out var cheapest);
            double estCost = cheapest != null ? UnitPrice(cheapest, mp, Math.Max(500, cheapest.Moq), playerId) : mp.BaseCost;
            var d = new Discovery
            {
                ProductId = mp.Id, Name = mp.Name, Category = mp.Category, FoundBy = playerId, Day = State.Day,
                RefPrice = mp.RefPrice, DemandLow = Math.Max(1, est * (1 - sigma)), DemandHigh = est * (1 + sigma), EstCost = estCost,
            };
            if (p.HasSkill(Skills.TrendHunter)) { d.PhaseKnown = true; d.Phase = mp.Phase; }
            if (p.HasSkill(Skills.ProductExpert))
            {
                d.DetailsKnown = true;
                d.Elasticity = mp.Elasticity;
                d.Competition = mp.Competition;
                d.Viral = mp.ViralPotential;
            }
            return d;
        }

        public Discovery FindDiscovery(int playerId, string marketProductId)
        {
            var pr = Priv(playerId);
            if (pr != null)
                foreach (var d in pr.Notebook) if (d.ProductId == marketProductId) return d;
            foreach (var d in C.ResearchBoard) if (d.ProductId == marketProductId) return d;
            return null;
        }
    }
}
