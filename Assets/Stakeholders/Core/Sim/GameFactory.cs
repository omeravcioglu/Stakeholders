using System;
using System.Collections.Generic;
using System.Text;

namespace Stakeholders.Core
{
    /// <summary>Builds a fresh game from lobby settings.</summary>
    public static class GameFactory
    {
        public static string Slug(string name)
        {
            var sb = new StringBuilder();
            foreach (char ch in name.ToLowerInvariant())
            {
                if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9')) sb.Append(ch);
                else if (sb.Length > 0 && sb[sb.Length - 1] != '_') sb.Append('_');
            }
            return sb.ToString().Trim('_');
        }

        public static GameState Create(GameSettings settings)
        {
            if (settings.Seats.Count == 0) settings.Seats.Add(new SeatConfig { Name = "Founder", IsAI = false });
            var s = new GameState
            {
                Settings = settings,
                RngState = (ulong)(uint)settings.Seed * 0x9E3779B97F4A7C15UL + 0x632BE59BD9B4E019UL,
                Phase = GamePhase.Running,
                Day = 1,
                Speed = 1,
            };
            var sim = new GameSimulation(s);
            var rng = sim.Rng;

            // Company
            var c = s.Company;
            c.Name = string.IsNullOrWhiteSpace(settings.CompanyName) ? "Nimbus Goods" : settings.CompanyName.Trim();
            c.Cash = settings.StartingCash;
            c.Policies.DefaultSpendingLimit = settings.DefaultSpendingLimit;
            var garage = Catalog.GetWarehouse("garage");
            c.Warehouses.Add(new Warehouse
            {
                Id = sim.NewId("w"), Tier = garage.Id, Name = garage.Name, Capacity = garage.Capacity, MonthlyCost = 0, OpenedDay = 1
            });

            // Market
            foreach (var def in Catalog.Products)
            {
                var mp = new MarketProduct
                {
                    Id = Slug(def.Name), Name = def.Name, Category = def.Category, RefPrice = def.Price, BaseCost = def.Cost,
                    BaseDemand = def.Demand * rng.Range(0.85, 1.15), Elasticity = def.Elasticity, ViralPotential = def.Viral, Rarity = def.Rarity,
                    Competition = GameSimulation.Clamp(rng.Range(0.3, 0.7) - def.Rarity * 0.1, 0.1, 0.9),
                    CompetitorPrice = def.Price * rng.Range(0.92, 1.08)
                };
                sim.InitTrend(mp);
                s.Market.Products.Add(mp);
            }

            // Suppliers
            foreach (var def in Catalog.Suppliers)
            {
                var sup = new Supplier
                {
                    Id = Slug(def.Name), Name = def.Name, Country = def.Country, CostFactor = def.CostFactor, Quality = def.Quality,
                    Reliability = def.Reliability, LeadTime = def.LeadTime, Moq = def.Moq, Shadiness = def.Shadiness, Exclusive = def.Exclusive,
                    Relationship = rng.Range(40, 60)
                };
                sup.Categories.AddRange(def.Categories);
                s.Suppliers.Add(sup);
            }

            // Partners
            double explicitPct = 0;
            int implicitSeats = 0;
            foreach (var seat in settings.Seats)
            {
                if (seat.OwnershipPct > 0) explicitPct += seat.OwnershipPct;
                else implicitSeats++;
            }
            double remaining = Math.Max(0, 100 - explicitPct);
            var personalities = new List<Personality>(Catalog.AllPersonalities());
            rng.Shuffle(personalities);
            int pIndex = 0;
            long assigned = 0;
            for (int i = 0; i < settings.Seats.Count; i++)
            {
                var seat = settings.Seats[i];
                var personality = seat.IsAI ? (seat.Personality == Personality.Human ? personalities[pIndex++ % personalities.Count] : seat.Personality) : Personality.Human;
                double pct = seat.OwnershipPct > 0 ? seat.OwnershipPct : (implicitSeats > 0 ? remaining / implicitSeats : 0);
                if (explicitPct > 100) pct = pct * 100 / explicitPct;
                long shares = (long)Math.Round(c.TotalShares * pct / 100.0);
                if (i == settings.Seats.Count - 1) shares = Math.Max(0, c.TotalShares - assigned);
                assigned += shares;
                var p = new Player
                {
                    Id = i, Name = string.IsNullOrWhiteSpace(seat.Name) ? (seat.IsAI ? Catalog.AINames[i % Catalog.AINames.Length] : "Partner " + (i + 1)) : seat.Name.Trim(),
                    IsAI = seat.IsAI, Personality = personality, Color = i % Catalog.PlayerColors.Length, Salary = settings.StartingSalary,
                    Shares = shares, SpendingLimit = settings.DefaultSpendingLimit
                };
                s.Players.Add(p);
                s.Private.Add(new PlayerPrivate { PlayerId = p.Id, Cash = settings.StartingPersonalCash, AI = seat.IsAI ? new AIMemory() : null });
            }
            // Any unassigned remainder goes to outside investors (e.g. seats configured below 100%).
            long playerShares = s.TotalPlayerShares;
            if (playerShares < c.TotalShares) c.InvestorShares = c.TotalShares - playerShares;

            // Each partner starts with two leads in their notebook so the first minutes are about decisions, not waiting.
            var common = s.Market.Products.FindAll(m => m.Rarity == 0);
            rng.Shuffle(common);
            int k = 0;
            foreach (var p in s.Players)
            {
                var pr = s.GetPrivate(p.Id);
                for (int n = 0; n < 2 && k < common.Count; n++, k++)
                    pr.Notebook.Add(sim.MakeDiscovery(common[k], p.Id, 0));
            }

            // Rivals, a starter product (so the first minutes already have sales), secret ambitions and AI roles.
            sim.InitRivals();
            var starterPool = s.Market.Products.FindAll(m => m.Rarity == 0 && m.BaseDemand >= 40 && m.Phase != TrendPhase.Dormant && m.Phase != TrendPhase.Declining);
            if (starterPool.Count > 0)
            {
                var mp = rng.Pick(starterPool);
                sim.CheapestUnitPrice(mp, 500, -1, out var sup);
                double unit = sup != null ? sim.UnitPrice(sup, mp, 500, -1) : mp.BaseCost;
                c.Products.Add(new CompanyProduct
                {
                    Id = sim.NewId("cp"), ProductId = mp.Id, Name = mp.Name, BaseName = mp.Name, Category = mp.Category, ManagerId = -1, DiscovererId = -1,
                    LaunchedDay = -20, Price = mp.RefPrice, Stock = 400, AvgCost = unit, SupplierId = sup?.Id, Quality = sup?.Quality ?? 0.6,
                    Rating = 3.9
                });
                sim.Log(LogKind.Company, -1, $"The garage already holds 400 units of {mp.Name}. Nobody manages it yet: whoever claims it gets the credit.");
            }
            sim.AssignAmbitionChoices();
            sim.AssignDefaultRoles();

            s.Board.NextMeetingDay = GameCalendar.DaysPerQuarter;
            s.Board.LastValuation = settings.StartingCash;
            c.Valuation = settings.StartingCash;
            c.PeakValuation = settings.StartingCash;
            c.StorageCapacity = garage.Capacity;
            c.CapacityPerDay = sim.FulfillmentCapacity();
            c.SupportPerDay = sim.SupportCapacity();
            sim.Log(LogKind.Company, -1, $"{c.Name} is founded with {Fmt.Money(c.Cash)} in the bank.");
            foreach (var p in s.Players)
                sim.Log(LogKind.Company, p.Id, $"{p.Name} joins as a partner with {Fmt.Pct(sim.OwnershipPct(p), 1)} ownership{(p.IsAI ? " (" + p.Personality + ")" : "")}.");
            sim.NotifyAll(NoticeKind.Info, "Welcome! Research products, launch them, and build the company. Board meetings happen every quarter.");
            return s;
        }

        /// <summary>A default solo setup: you plus AI partners.</summary>
        public static GameSettings SoloSettings(string playerName, int aiPartners, int seed)
        {
            var st = new GameSettings { Seed = seed };
            st.Seats.Add(new SeatConfig { Name = string.IsNullOrWhiteSpace(playerName) ? "You" : playerName, IsAI = false });
            var names = new List<string>(Catalog.AINames);
            for (int i = 0; i < aiPartners; i++)
                st.Seats.Add(new SeatConfig { Name = names[(seed + i * 3) % names.Count], IsAI = true, Personality = Personality.Human });
            return st;
        }
    }
}
