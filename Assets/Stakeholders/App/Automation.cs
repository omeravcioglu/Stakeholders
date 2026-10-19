using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;

namespace Stakeholders.App
{
    /// <summary>
    /// Command-line driven smoke tests for builds:
    ///   -stk-autotest DIR          solo run through every page and dialog, screenshots to DIR, then quit
    ///   -stk-mp-host DIR           LAN host that waits for one client, starts, screenshots, quits
    ///   -stk-mp-join IP DIR        LAN client that joins, screenshots, quits
    /// </summary>
    public static class Automation
    {
        static readonly List<string> Errors = new List<string>();

        /// <summary>True while an automated test run is driving the game.</summary>
        public static bool Active { get; private set; }

        public static bool TryStart(GameApp app)
        {
            var args = Environment.GetCommandLineArgs();
            string Arg(string name, int offset = 1)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + offset < args.Length ? args[i + offset] : null;
            }
            string dir = Arg("-stk-autotest");
            if (dir != null) { Begin(app, dir); app.StartCoroutine(SoloTest(app, dir)); return true; }
            dir = Arg("-stk-mp-host");
            if (dir != null) { Begin(app, dir); app.StartCoroutine(HostTest(app, dir)); return true; }
            string ip = Arg("-stk-mp-join");
            dir = Arg("-stk-mp-join", 2);
            if (ip != null && dir != null) { Begin(app, dir); app.StartCoroutine(ClientTest(app, ip, dir)); return true; }
            return false;
        }

        static void Begin(GameApp app, string dir)
        {
            Active = true;
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += (msg, stack, type) =>
            {
                if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                    Errors.Add($"[{type}] {msg}\n{stack}");
            };
        }

        static IEnumerator Shot(string dir, string name)
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png"));
            yield return null;
        }

        static IEnumerator Finish(string dir, string summary)
        {
            yield return new WaitForSeconds(0.5f);
            File.WriteAllText(Path.Combine(dir, "result.txt"), summary + "\n\nErrors: " + Errors.Count + "\n" + string.Join("\n\n", Errors));
            Application.Quit();
        }

        static void Step(GameSimulation sim, int days)
        {
            for (int i = 0; i < days && sim.State.Phase != GamePhase.GameOver; i++)
            {
                if (sim.State.Phase == GamePhase.BoardMeeting) sim.EndMeeting();
                sim.StepDay();
            }
        }

        static IEnumerator SoloTest(GameApp app, string dir)
        {
            app.PlayerName = "Omer";
            app.ShowMainMenu();
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "a0_main_menu");
            app.Show(new SetupScreen(app, SetupMode.Solo));
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "a1_setup_solo");
            app.Show(new SetupScreen(app, SetupMode.HostRelay));
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "a2_setup_host");

            var st = new GameSettings { Seed = 4242, CompanyName = "Nimbus Goods", GameYears = 3 };
            st.Seats.Add(new SeatConfig { Name = "Omer", IsAI = false });
            st.Seats.Add(new SeatConfig { Name = "Priya Nair", IsAI = true, Personality = Personality.Workaholic });
            st.Seats.Add(new SeatConfig { Name = "Viktor Hale", IsAI = true, Personality = Personality.Greedy });
            st.Seats.Add(new SeatConfig { Name = "Dana Brooks", IsAI = true, Personality = Personality.Loyal });
            app.StartSolo(st);
            yield return new WaitForSeconds(1f);
            var screen = FindScreen();
            HelpView.Show(screen);
            yield return Shot(dir, "a3_help");
            app.Ui.CloseAllModals();
            yield return Shot(dir, "00_start");

            var session = (LocalSession)app.Session;
            var sim = session.Sim;
            var me = sim.State.GetPlayer(0);
            var pr = sim.State.GetPrivate(0);
            AmbitionPicker.Show(screen);
            yield return Shot(dir, "a4_ambition");
            app.Ui.CloseAllModals();
            app.Session.Send(new Command { Type = CommandType.ChooseAmbition, A = pr.AmbitionChoices[0] });
            // Play a little as the human: claim the starter stock, launch our first lead, research, learn a skill.
            var starter = sim.State.Company.Products.FirstOrDefault(p => p.ManagerId < 0);
            if (starter != null) app.Session.Send(new Command { Type = CommandType.ClaimProduct, A = starter.Id });
            var disc = pr.Notebook[0];
            var mp = sim.State.GetMarketProduct(disc.ProductId);
            var sup = sim.State.Suppliers.First(x => x.Serves(mp.Category) && !x.Exclusive);
            app.Session.Send(new Command { Type = CommandType.LaunchProduct, A = disc.ProductId, B = sup.Id, I = Math.Max(sup.Moq, 600), X = mp.RefPrice });
            app.Session.Send(new Command { Type = CommandType.StartResearch, A = "Any", I = 0 });
            app.Session.Send(new Command { Type = CommandType.UnlockSkill, A = Skills.TrendHunter });
            var dana = sim.State.Players.First(p => p.Name == "Dana Brooks");
            app.Session.Send(new Command { Type = CommandType.SetRoles, I = dana.Id, Text = string.Join(",", dana.Roles.Concat(new[] { Roles.Hiring }).Distinct()) });
            Step(sim, 75);
            me.SkillPoints += 3;
            app.Session.Send(new Command { Type = CommandType.UnlockSkill, A = Skills.Accountant });
            app.Session.Send(new Command { Type = CommandType.UnlockSkill, A = Skills.Negotiator });
            app.Session.Send(new Command { Type = CommandType.CreateProposal, I = (int)ProposalType.SalaryChange, J = 0, X = 4500, Text = "I launched our first hit." });
            var promoTarget = sim.State.Company.Products.Where(p => p.Active && p.Stock >= 20).OrderByDescending(p => p.Stock).FirstOrDefault();
            if (promoTarget != null) app.Session.Send(new Command { Type = CommandType.StartPromotion, A = promoTarget.Id, I = 0 });
            app.Session.Send(new Command { Type = CommandType.BuyUpgrade, A = "log1" });
            yield return new WaitForSeconds(0.6f);

            string[] pages = { "Dashboard", "Situations", "Products", "Market", "Suppliers", "Marketing", "Operations", "Finance", "Growth", "Competition", "Partners", "Board", "Career", "Private" };
            screen = FindScreen();
            var list = new Page[] { screen.Dashboard, screen.Situations, screen.Products, screen.Market, screen.Suppliers, screen.Marketing, screen.Operations, screen.Finance, screen.Growth, screen.Rivals, screen.Partners, screen.Board, screen.Career, screen.Private };
            for (int i = 0; i < list.Length; i++)
            {
                screen.Navigate(list[i]);
                yield return new WaitForSeconds(0.35f);
                yield return Shot(dir, $"{i + 1:00}_{pages[i].ToLowerInvariant()}");
            }

            // Dialogs
            screen.Navigate(screen.Products);
            var cp = sim.State.Company.Products.FirstOrDefault(p => p.Active);
            if (cp != null)
            {
                Dialogs.Order(screen, session.View.GetCompanyProduct(cp.Id));
                yield return Shot(dir, "20_dialog_order");
                app.Ui.CloseAllModals();
                Dialogs.Campaign(screen, session.View.GetCompanyProduct(cp.Id));
                yield return Shot(dir, "21_dialog_campaign");
                app.Ui.CloseAllModals();
                screen.Products.ProductMenu(session.View.GetCompanyProduct(cp.Id));
                yield return Shot(dir, "21b_dialog_product_menu");
                app.Ui.CloseAllModals();
            }
            Celebration.Show(app.Ui, "Nimbus Goods is now a Growing Brand!|Valuation passed $2.5M. Unlocked: Tier 3 upgrades, Pro (v3) products, bigger contracts.");
            yield return new WaitForSeconds(0.9f);
            yield return Shot(dir, "25_celebration");
            app.Ui.CloseAllModals();
            var d2 = session.View.GetPrivate(0).Notebook.FirstOrDefault(x => session.View.GetCompanyProductByMarketId(x.ProductId) == null);
            if (d2 != null)
            {
                Dialogs.Launch(screen, d2);
                yield return Shot(dir, "22_dialog_launch");
                app.Ui.CloseAllModals();
            }
            var negSup = sim.State.Suppliers.First(x => !x.Exclusive && !x.Bankrupt && !x.Blacklisted && sim.State.Day >= x.BlockedUntil);
            Dialogs.Negotiation(screen, negSup.Id, true);
            yield return new WaitForSeconds(0.4f);
            var neg = session.View.Negotiations.FirstOrDefault(n => n.PlayerId == 0 && n.Status == NegotiationStatus.Active);
            if (neg != null) app.Session.Send(new Command { Type = CommandType.NegotiationOffer, A = neg.Id, X = 0.25 });
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "23_dialog_negotiation");
            app.Ui.CloseAllModals();
            Dialogs.Proposal(screen);
            yield return Shot(dir, "24_dialog_proposal");
            app.Ui.CloseAllModals();

            // Intrigue: make sure the private page has something to show.
            if (!sim.State.Offers.Any(o => o.TargetId == 0 && o.Status == OfferStatus.Pending))
            {
                sim.State.Offers.Add(new PrivateOffer
                {
                    Id = sim.NewId("o"), Kind = OfferKind.SpeakingGig, TargetId = 0, FromName = "Retail Innovators Forum", Title = "Speak at the Retail Innovators Forum",
                    Text = "The Retail Innovators Forum invites you as a speaker for a $9,500 fee. You'd be away for 3 days and unable to work on the company.",
                    PersonalAmount = 9500, CreatedDay = sim.State.Day, ExpiresDay = sim.State.Day + 5, Status = OfferStatus.Pending, Reportable = false
                });
            }
            var victim = sim.State.Players.First(p => p.Id != 0);
            var ev = sim.CreateEvidence(victim.Id, OfferKind.Kickback, $"{victim.Name} received about $12,500 from BrightTech Shenzhen for a 2,000-unit order (18% above the cheapest supplier).", 12500, 0.6);
            sim.DiscoverEvidence(ev, 0);
            sim.State.Revision++;
            yield return new WaitForSeconds(0.4f);
            screen.Navigate(screen.Private);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "30_private_with_offers");

            // Board meeting in session
            sim.State.Board.NextMeetingDay = sim.State.Day + 1;
            sim.StepDay();
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "31_board_meeting");
            sim.EndMeeting();

            // Fast forward a year and look again
            Step(sim, 300);
            yield return new WaitForSeconds(0.6f);
            screen.Navigate(screen.Dashboard);
            yield return new WaitForSeconds(0.4f);
            yield return Shot(dir, "40_dashboard_later");
            screen.Navigate(screen.Products);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "41_products_later");
            screen.Navigate(screen.Finance);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "42_finance_later");
            screen.Navigate(screen.Growth);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "43_growth_later");
            screen.Navigate(screen.Rivals);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "44_competition_later");
            screen.Navigate(screen.Partners);
            yield return new WaitForSeconds(0.3f);
            yield return Shot(dir, "45_partners_later");

            // The dashboard HQ drawing with every building and upgrade: staged for one screenshot, then put back.
            var hq = sim.State.Company;
            var keepWarehouses = hq.Warehouses.ToList();
            var keepUpgrades = hq.Upgrades.ToList();
            var keepStaff = hq.Employees.ToList();
            var keepBuilding = hq.Building.ToList();
            int keepStage = hq.Stage;
            double keepUsed = hq.StorageUsed, keepCap = hq.StorageCapacity, keepBacklog = hq.Backlog, keepOrders = hq.OrdersToday;
            bool keepPaused = sim.State.Paused;
            var keepPhase = sim.State.Phase;
            int keepSpeed = sim.State.Speed;
            // "Running" at speed 0: the calendar stands still, but the HQ animates as it does in play.
            sim.State.Phase = GamePhase.Running;
            sim.State.Speed = 0;
            sim.State.Paused = false;
            foreach (var tier in new[] { "small", "small", "regional", "mega" })
            {
                var wd = Catalog.GetWarehouse(tier);
                hq.Warehouses.Add(new Warehouse { Id = "hq-shot-" + hq.Warehouses.Count, Tier = wd.Id, Name = wd.Name, Capacity = wd.Capacity, MonthlyCost = wd.MonthlyCost, FulfillmentBonus = wd.FulfillmentBonus, OpenedDay = sim.State.Day });
            }
            foreach (var up in new[] { "log1", "log3", "log4", "brd1", "chn1", "chn4", "src4" })
                if (!hq.Has(up)) hq.Upgrades.Add(up);
            for (int e = 0; e < 30; e++)
                hq.Employees.Add(new Employee { Id = "hq-shot-e" + e, Name = "Temp " + e, Role = (EmployeeRole)(e < 12 ? e % 6 : 0), HiredDay = sim.State.Day });
            hq.Building.Add(new UpgradeBuild { Id = "log2", StartDay = sim.State.Day, DoneDay = sim.State.Day + 4 });
            hq.Stage = 3;
            hq.StorageCapacity = hq.Warehouses.Sum(w => w.Capacity) * 1.25;
            hq.StorageUsed = hq.StorageCapacity * 0.95;
            hq.OrdersToday = 1240;
            hq.Backlog = 160;
            sim.State.Revision++;
            app.Ui.CloseAllModals();
            screen.Navigate(screen.Dashboard);
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "46_dashboard_hq_full");
            yield return new WaitForSeconds(1.3f);
            yield return Shot(dir, "47_dashboard_hq_moving");
            hq.Warehouses.Clear();
            hq.Warehouses.AddRange(keepWarehouses);
            hq.Upgrades.Clear();
            hq.Upgrades.AddRange(keepUpgrades);
            hq.Employees.Clear();
            hq.Employees.AddRange(keepStaff);
            hq.Building.Clear();
            hq.Building.AddRange(keepBuilding);
            hq.Stage = keepStage;
            hq.StorageUsed = keepUsed;
            hq.StorageCapacity = keepCap;
            hq.Backlog = keepBacklog;
            hq.OrdersToday = keepOrders;
            sim.State.Paused = keepPaused;
            sim.State.Speed = keepSpeed;
            sim.State.Phase = keepPhase;
            sim.State.Revision++;

            // End of game
            sim.EndGame("Automated test: the partnership is over.");
            yield return new WaitForSeconds(0.8f);
            yield return Shot(dir, "50_game_over");

            var co = sim.State.Company;
            string summary = $"Solo test finished on day {sim.State.Day}. Valuation {Fmt.Money(co.Valuation)}, stage {co.Stage}, products {co.Products.Count}, upgrades [{string.Join(",", co.Upgrades)}], " +
                             $"strategy {co.Positioning ?? "-"}, contracts {co.Contracts.Count}, my ambition {pr.Ambition}, roles: " +
                             string.Join("; ", sim.State.Players.Where(p => p.IsAI).Select(p => p.Name + "=" + string.Join("/", p.Roles))) + $", log {sim.State.Log.Count}.";
            yield return Finish(dir, summary);
        }

        static GameScreen FindScreen()
        {
            var f = typeof(GameApp).GetField("_screen", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return f?.GetValue(GameApp.Instance) as GameScreen;
        }

        static IEnumerator HostTest(GameApp app, string dir)
        {
            app.PlayerName = "Host Omer";
            var net = app.EnsureNet();
            if (!net.StartHostDirect(NetBridge.DefaultPort, "127.0.0.1")) { yield return Finish(dir, "Host failed: " + net.LastError); yield break; }
            var st = new GameSettings { Seed = 777, CompanyName = "LAN Test Co", MeetingSeconds = 60 };
            var host = new LobbyHost(net, app.PlayerName, st);
            host.Info.Address = "127.0.0.1:" + NetBridge.DefaultPort;
            host.AddAI(Personality.Manipulative);
            var lobby = new LobbyScreen(app, host, null);
            app.Show(lobby);
            float t = 0;
            while (host.Info.Seats.Count < 3 && t < 60) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "host_00_lobby");
            if (host.Info.Seats.Count < 3) { yield return Finish(dir, "No client joined within 60s."); yield break; }
            var session = host.StartGame();
            app.BeginSession(session);
            yield return new WaitForSeconds(3f);
            session.Send(new Command { Type = CommandType.Chat, Text = "Welcome aboard, partner!", I = -1 });
            yield return new WaitForSeconds(6f);
            yield return Shot(dir, "host_01_game");
            var screen = FindScreen();
            screen.Navigate(screen.Partners);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "host_02_partners");
            yield return new WaitForSeconds(8f);
            var s = session.Sim.State;
            var client = s.Players.FirstOrDefault(p => !p.IsAI && p.Id != 0);
            string summary = $"Host test: day {s.Day}, players {s.Players.Count}, client connected {client?.Connected}, chat {s.Chat.Count} messages: " +
                             string.Join(" | ", s.Chat.Select(c => s.PlayerName(c.FromId) + ": " + c.Text));
            yield return Finish(dir, summary);
        }

        static IEnumerator ClientTest(GameApp app, string ip, string dir)
        {
            app.PlayerName = "Client Ayse";
            yield return new WaitForSeconds(2f);
            var net = app.EnsureNet();
            var lobby = new LobbyScreen(app, null, new LobbyClient(net, app.PlayerName));
            if (!net.StartClientDirect(ip, NetBridge.DefaultPort)) { yield return Finish(dir, "Client failed: " + net.LastError); yield break; }
            app.Show(lobby);
            float t = 0;
            while (!(app.Session is ClientSession) && t < 60) { t += Time.deltaTime; yield return null; }
            if (!(app.Session is ClientSession cs)) { yield return Shot(dir, "client_00_fail"); yield return Finish(dir, "Never received the start message."); yield break; }
            t = 0;
            while (cs.View == null && t < 20) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(2f);
            cs.Send(new Command { Type = CommandType.Chat, Text = "Hi from the client!", I = -1 });
            var d = cs.View?.GetPrivate(cs.LocalPlayerId)?.Notebook.FirstOrDefault();
            if (d != null)
            {
                var mp = cs.View.GetMarketProduct(d.ProductId);
                var sup = cs.View.Suppliers.First(x => x.Serves(mp.Category) && !x.Exclusive);
                cs.Send(new Command { Type = CommandType.LaunchProduct, A = d.ProductId, B = sup.Id, I = Math.Max(sup.Moq, 500), X = mp.RefPrice });
            }
            yield return new WaitForSeconds(4f);
            yield return Shot(dir, "client_01_game");
            var screen = FindScreen();
            screen.Navigate(screen.Products);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "client_02_products");
            screen.Navigate(screen.Private);
            yield return new WaitForSeconds(0.5f);
            yield return Shot(dir, "client_03_private");
            var v = cs.View;
            string summary = $"Client test: player {cs.LocalPlayerId}, day {v?.Day}, products {v?.Company.Products.Count}, private entries {v?.Private.Count}, " +
                             $"offers visible {v?.Offers.Count}, chat: " + (v != null ? string.Join(" | ", v.Chat.Select(c => v.PlayerName(c.FromId) + ": " + c.Text)) : "");
            yield return Finish(dir, summary);
        }
    }
}
