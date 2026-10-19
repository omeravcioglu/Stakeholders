using System;
using System.Collections.Generic;
using System.Linq;
using Stakeholders.App.UI;
using Stakeholders.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Stakeholders.App
{
    // ====================================================================== Suppliers
    public class SuppliersPage : Page
    {
        public override string Title => "Suppliers";
        KeyedList<Supplier> _suppliers;
        KeyedList<Shipment> _shipments;
        VisualElement _negBanner;
        Label _negText;

        public SuppliersPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            _negBanner = Ui.Div("banner");
            _negText = Ui.Text("", "grow", "b");
            _negBanner.Add(_negText);
            _negBanner.Add(Ui.Btn("Continue negotiating", () =>
            {
                var n = V.Negotiations.FirstOrDefault(x => x.PlayerId == Me && x.Status == NegotiationStatus.Active);
                if (n != null) Dialogs.Negotiation(S, n.SupplierId, false);
            }));
            Body.Add(_negBanner);

            var card = Ui.Card("Suppliers");
            card.Add(Ui.Text("Prices shown are for 1,000 units of a typical product. Negotiate for discounts; bigger orders unlock bulk pricing. A supplier that's more expensive than the alternatives is worth asking questions about.", "para", "dim", "small", "mb8"));
            card.Add(Header(("Supplier", 0), ("Categories", 210), ("Price", 60), ("Quality", 80), ("On-time", 68), ("Lead", 46), ("MOQ", 52), ("Relation", 84), ("Deal", 150), ("", 100)));
            var list = Ui.Col();
            card.Add(list);
            _suppliers = new KeyedList<Supplier>(list, s => s.Id, CreateSupplierRow);
            Body.Add(card);

            var ship = Ui.Card("Shipments on the way");
            ship.Add(Header(("Product", 0), ("Qty", 80), ("Supplier", 200), ("Unit cost", 80), ("Ordered by", 110), ("Arrives", 90), ("", 110)));
            var slist = Ui.Col();
            ship.Add(slist);
            _shipments = new KeyedList<Shipment>(slist, s => s.Id, CreateShipmentRow, "Nothing in transit.");
            Body.Add(ship);
        }

        ListRow<Supplier> CreateSupplierRow()
        {
            var row = new ListRow<Supplier>();
            var tr = Ui.Div("tr");
            var nameBox = CellBox(tr, 0);
            var col = Ui.Col();
            var name = Ui.Text("", "b");
            var country = Ui.Text("", "tiny", "dim");
            col.Add(name);
            col.Add(country);
            nameBox.Add(col);
            var cats = Cell(tr, 210, "small");
            cats.AddToClassList("para");
            var level = Cell(tr, 60);
            var qBox = CellBox(tr, 80);
            var qBar = Ui.Bar(0);
            qBox.Add(qBar);
            var ontime = Cell(tr, 68);
            var lead = Cell(tr, 46);
            var moq = Cell(tr, 52);
            var relBox = CellBox(tr, 84);
            var relBar = Ui.Bar(0, Ui.Good);
            relBox.Add(relBar);
            var deal = Cell(tr, 150, "small");
            var act = CellBox(tr, 100);
            var neg = Ui.Btn("Negotiate", () => Dialogs.Negotiation(S, row.Item.Id, true), "btn-sm");
            act.Add(neg);
            row.Root = tr;
            row.Bind = s =>
            {
                var me = MeP;
                bool locked = s.Exclusive && (me == null || !me.HasSkill(Skills.SupplierNetwork));
                name.SetText(s.Name + (s.Exclusive ? "  (exclusive network)" : ""));
                country.SetText(s.Country);
                cats.SetText(s.Categories.Contains("*") ? "Everything (dropshipping)" : string.Join(", ", s.Categories));
                level.SetText(Fmt.Fixed(s.CostFactor, 2) + "x");
                level.style.color = s.CostFactor <= 0.85 ? Ui.Good : s.CostFactor >= 1.2 ? Ui.Warn : Ui.TextColor;
                Ui.SetBar(qBar, (float)s.Quality, s.Quality >= 0.8 ? Ui.Good : s.Quality < 0.55 ? Ui.Warn : Ui.Accent);
                ontime.SetText(Fmt.Pct(s.Reliability));
                lead.SetText(s.LeadTime + "d");
                moq.SetText(Fmt.Int(s.Moq));
                Ui.SetBar(relBar, (float)(s.Relationship / 100.0));
                string status;
                if (s.Bankrupt) status = "Bankrupt";
                else if (s.Blacklisted) status = "Blacklisted";
                else if (V.Day < s.BlockedUntil) status = "Refusing us";
                else if (s.Discount > 0 && V.Day <= s.DiscountUntil) status = $"-{Fmt.Pct(s.Discount, 1)} until {Fmt.ShortDate(s.DiscountUntil)}";
                else status = locked ? "Locked" : "-";
                deal.SetText(status);
                deal.style.color = s.Bankrupt || s.Blacklisted ? Ui.Bad : s.Discount > 0 && V.Day <= s.DiscountUntil ? Ui.Good : Ui.Dim;
                neg.SetEnabled(!locked && !s.Bankrupt && !s.Blacklisted && V.Day >= s.BlockedUntil);
                tr.style.opacity = locked || s.Bankrupt || s.Blacklisted ? 0.5f : 1f;
            };
            return row;
        }

        ListRow<Shipment> CreateShipmentRow()
        {
            var row = new ListRow<Shipment>();
            var tr = Ui.Div("tr");
            var product = Cell(tr, 0, "b");
            var qty = Cell(tr, 80);
            var sup = Cell(tr, 200);
            var unit = Cell(tr, 80);
            var by = Cell(tr, 110);
            var eta = Cell(tr, 90);
            var flags = Cell(tr, 110, "small");
            row.Root = tr;
            row.Bind = sh =>
            {
                product.SetText(V.GetCompanyProduct(sh.CompanyProductId)?.Name ?? "-");
                qty.SetText(Fmt.Int(sh.Quantity));
                sup.SetText(V.GetSupplier(sh.SupplierId)?.Name ?? "-");
                unit.SetText(Fmt.Price(sh.UnitCost));
                by.SetText(Name(sh.OrderedBy));
                eta.SetText(DaysLeft(sh.EtaDay, V.Day));
                flags.SetText(sh.Delayed ? "Delayed" : sh.Rush ? "Air freight" : "");
                flags.style.color = sh.Delayed ? Ui.Warn : Ui.Accent;
            };
            return row;
        }

        public override void Refresh()
        {
            var n = V.Negotiations.FirstOrDefault(x => x.PlayerId == Me && x.Status == NegotiationStatus.Active);
            _negBanner.Show(n != null);
            if (n != null) _negText.SetText($"You are negotiating with {V.GetSupplier(n.SupplierId)?.Name}. {n.RoundsLeft} rounds left.");
            Subtitle.SetText($"{V.Suppliers.Count(s => !s.Bankrupt && !s.Blacklisted)} suppliers · buyer discount {Fmt.Pct(Math.Min(0.10, V.Company.Employees.Count(e => e.Role == EmployeeRole.Buyer) * 0.04))}");
            _suppliers.Sync(V.Suppliers.OrderBy(s => s.Exclusive).ThenBy(s => s.Bankrupt || s.Blacklisted).ThenBy(s => s.Name).ToList());
            _shipments.Sync(V.Company.Shipments.OrderBy(s => s.EtaDay).ToList());
        }
    }

    // ====================================================================== Marketing
    public class MarketingPage : Page
    {
        public override string Title => "Marketing";
        KeyedList<Campaign> _active, _past;
        Label _spend;

        public MarketingPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            HeadRight.Add(Ui.Btn("New campaign", () => Dialogs.Campaign(S, null), "btn-good"));
            var guide = Ui.Card("How advertising works");
            guide.Add(Ui.Text("Ads buy extra customers at a cost per sale. Search ads convert best; social ads can make a product go viral; email is cheap but only reaches your existing customers. Small budgets are efficient, big budgets hit diminishing returns. Hot, well-priced, well-reviewed products convert far better. ROAS = sales generated per $1 of ad spend. Below about 2x you're probably losing money after product costs.", "para", "dim", "small"));
            _spend = Ui.Text("", "b", "mt8");
            guide.Add(_spend);
            Body.Add(guide);

            var card = Ui.Card("Running campaigns");
            card.Add(Header(("Product", 0), ("Channel", 80), ("Budget/day", 90), ("Days left", 74), ("Spent", 80), ("Sales from ads", 110), ("ROAS 7d", 70), ("ROAS total", 80), ("By", 100), ("", 70)));
            var list = Ui.Col();
            card.Add(list);
            _active = new KeyedList<Campaign>(list, c => c.Id, () => CreateRow(true), "No campaigns running.");
            Body.Add(card);

            var past = Ui.Card("Finished campaigns");
            past.Add(Header(("Product", 0), ("Channel", 80), ("Budget/day", 90), ("Ended", 74), ("Spent", 80), ("Sales from ads", 110), ("ROAS 7d", 70), ("ROAS total", 80), ("By", 100), ("", 70)));
            var plist = Ui.Col();
            past.Add(plist);
            _past = new KeyedList<Campaign>(plist, c => c.Id, () => CreateRow(false), "None yet.");
            Body.Add(past);
        }

        ListRow<Campaign> CreateRow(bool active)
        {
            var row = new ListRow<Campaign>();
            var tr = Ui.Div("tr");
            var product = Cell(tr, 0, "b");
            var channel = Cell(tr, 80);
            var budget = Cell(tr, 90);
            var left = Cell(tr, 74);
            var spent = Cell(tr, 80);
            var sales = Cell(tr, 110);
            var roas7 = Cell(tr, 70);
            var roas = Cell(tr, 80, "b");
            var by = Cell(tr, 100);
            var act = CellBox(tr, 70);
            if (active) act.Add(Ui.Btn("Stop", () => Send(new Command { Type = CommandType.StopCampaign, A = row.Item.Id }), "btn-ghost", "btn-sm"));
            row.Root = tr;
            row.Bind = c =>
            {
                product.SetText(V.GetCompanyProduct(c.CompanyProductId)?.Name ?? "-");
                channel.SetText(c.Channel.ToString());
                budget.SetText(Fmt.Money(c.DailyBudget));
                left.SetText(active ? DaysLeft(c.EndDay, V.Day) : Fmt.ShortDate(c.EndDay));
                spent.SetText(Fmt.Money(c.Spent));
                sales.SetText(Fmt.Money(c.AttributedRevenue));
                roas7.SetText(c.Spent7 > 0 ? Fmt.Fixed(c.Roas7, 2) + "x" : "-");
                roas.SetText(c.Spent > 0 ? Fmt.Fixed(c.Roas, 2) + "x" : "-");
                roas.style.color = c.Spent <= 0 ? Ui.Dim : c.Roas >= 2.5 ? Ui.Good : c.Roas < 1.8 ? Ui.Bad : Ui.TextColor;
                by.SetText(Name(c.CreatedBy));
            };
            return row;
        }

        public override void Refresh()
        {
            var active = V.Company.Campaigns.Where(c => c.Active).ToList();
            double daily = active.Sum(c => c.DailyBudget);
            Subtitle.SetText($"{active.Count} running · {Fmt.Money(daily)} per day · marketing this month {Fmt.Money(V.Company.Month.Marketing)}");
            _spend.SetText($"Current ad spend: {Fmt.Money(daily)}/day. Marketing staff: {V.Company.Employees.Count(e => e.Role == EmployeeRole.Marketing)}.");
            _active.Sync(active);
            _past.Sync(V.Company.Campaigns.Where(c => !c.Active).OrderByDescending(c => c.EndDay).Take(12).ToList());
        }
    }

    // ====================================================================== Operations
    public class OperationsPage : Page
    {
        public override string Title => "Operations";
        VisualElement _capBar, _supBar, _storeBar;
        Label _capText, _supText, _storeText, _backlog;
        Select _role;
        KeyedList<Employee> _team;
        KeyedList<Candidate> _candidates;
        KeyedList<PlayerTask> _recruiting;
        KeyedList<Warehouse> _warehouses;
        Label _upgradeSummary;
        static readonly EmployeeRole[] Roles = (EmployeeRole[])Enum.GetValues(typeof(EmployeeRole));

        public OperationsPage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var cap = Ui.Card("Capacity");
            _capText = Ui.Text("", "small");
            _capBar = Ui.Bar(0, Ui.Accent);
            _supText = Ui.Text("", "small", "mt8");
            _supBar = Ui.Bar(0, Ui.Good);
            _storeText = Ui.Text("", "small", "mt8");
            _storeBar = Ui.Bar(0, Ui.Warn);
            _backlog = Ui.Text("", "small", "dim", "mt8", "para");
            cap.Add(_capText); cap.Add(_capBar); cap.Add(_supText); cap.Add(_supBar); cap.Add(_storeText); cap.Add(_storeBar); cap.Add(_backlog);

            var recruit = Ui.Card("Recruiting");
            recruit.Add(Ui.Text("Post a job ad ($500). Candidates arrive in a couple of days and stay available for about 12 days. Whoever hires someone becomes their manager.", "para", "dim", "small", "mb8"));
            var rrow = Ui.Row();
            var names = Roles.Select(r => Catalog.RoleName(r)).ToList();
            _role = new Select(names, 0);
            _role.style.width = 200;
            _role.style.marginRight = 8;
            rrow.Add(_role);
            rrow.Add(Ui.Btn("Recruit", () => Send(new Command { Type = CommandType.Recruit, A = Roles[_role.Index].ToString() }), "btn-good"));
            recruit.Add(rrow);
            var roleHelp = Ui.Col("mt8");
            foreach (var r in Roles) roleHelp.Add(Ui.Text($"{Catalog.RoleName(r)}: {Catalog.RoleEffect(r)}", "tiny", "dim"));
            recruit.Add(roleHelp);
            var rtasks = Ui.Col("mt8");
            recruit.Add(rtasks);
            _recruiting = new KeyedList<PlayerTask>(rtasks, t => t.Id, CreateTaskRow);
            Body.Add(Cols(cap, recruit));

            var cand = Ui.Card("Candidates");
            cand.Add(Header(("Name", 0), ("Role", 170), ("Skill", 80), ("Salary", 90), ("Found by", 110), ("Leaves", 70), ("", 80)));
            var clist = Ui.Col();
            cand.Add(clist);
            _candidates = new KeyedList<Candidate>(clist, c => c.Id, CreateCandidateRow, "No candidates. Recruit for a role above.");
            Body.Add(cand);

            var team = Ui.Card("Team");
            team.Add(Header(("Name", 0), ("Role", 170), ("Skill", 60), ("Salary", 90), ("Morale", 110), ("Manager", 110), ("Hired", 80), ("", 70)));
            var tlist = Ui.Col();
            team.Add(tlist);
            _team = new KeyedList<Employee>(tlist, e => e.Id, CreateEmployeeRow, "No employees yet. The partners are packing boxes themselves.");
            Body.Add(team);

            var wh = Ui.Card("Warehouses");
            var wlist = Ui.Col();
            wh.Add(wlist);
            _warehouses = new KeyedList<Warehouse>(wlist, w => w.Id, CreateWarehouseRow);
            var wopts = Ui.Row("wrap", "mt8");
            foreach (var def in Catalog.Warehouses)
            {
                if (def.Id == "garage") continue;
                var box = Ui.Div("inset");
                box.style.marginRight = 8;
                box.style.marginBottom = 8;
                box.style.width = 240;
                box.Add(Ui.Text(def.Name, "b"));
                box.Add(Ui.Text($"+{Fmt.Int(def.Capacity)} units · {Fmt.Money(def.MonthlyCost)}/month" + (def.FulfillmentBonus > 0 ? $" · +{Fmt.Pct(def.FulfillmentBonus)} fulfillment" : ""), "tiny", "dim", "para"));
                var d = def;
                box.Add(Ui.Btn($"Open for {Fmt.Money(def.SetupCost)}", () => Send(new Command { Type = CommandType.BuyWarehouse, A = d.Id }), "btn-sm", "mt8"));
                wopts.Add(box);
            }
            wh.Add(wopts);

            var up = Ui.Card("Company upgrades");
            _upgradeSummary = Ui.Text("", "para", "small");
            up.Add(_upgradeSummary);
            up.Add(Ui.Btn("Open the upgrade tree", () => S.Navigate(S.Growth), "btn-sm", "mt8"));
            Body.Add(Cols(wh, up));
        }

        ListRow<PlayerTask> CreateTaskRow()
        {
            var row = new ListRow<PlayerTask>();
            var t = Ui.Text("", "small", "accent");
            row.Root = t;
            row.Bind = task => t.SetText($"{Name(task.PlayerId)}: {task.Label} ({DaysLeft(task.EndDay, V.Day)})");
            return row;
        }

        ListRow<Candidate> CreateCandidateRow()
        {
            var row = new ListRow<Candidate>();
            var tr = Ui.Div("tr");
            var name = Cell(tr, 0, "b");
            var role = Cell(tr, 170);
            var skill = Cell(tr, 80);
            var salary = Cell(tr, 90);
            var by = Cell(tr, 110);
            var leaves = Cell(tr, 70, "dim");
            var act = CellBox(tr, 80);
            act.Add(Ui.Btn("Hire", () => Send(new Command { Type = CommandType.Hire, A = row.Item.Id }), "btn-good", "btn-sm"));
            row.Root = tr;
            row.Bind = c =>
            {
                name.SetText(c.Name);
                role.SetText(Catalog.RoleName(c.Role));
                skill.SetText(c.ShownSkillLow == c.ShownSkillHigh ? c.ShownSkillLow + "/5" : $"{c.ShownSkillLow}-{c.ShownSkillHigh}/5");
                salary.SetText(Fmt.Money(c.Salary) + "/mo");
                by.SetText(Name(c.FoundBy));
                leaves.SetText(DaysLeft(c.ExpiresDay, V.Day));
            };
            return row;
        }

        ListRow<Employee> CreateEmployeeRow()
        {
            var row = new ListRow<Employee>();
            var tr = Ui.Div("tr");
            var name = Cell(tr, 0, "b");
            var role = Cell(tr, 170);
            var skill = Cell(tr, 60);
            var salary = Cell(tr, 90);
            var mBox = CellBox(tr, 110);
            var morale = Ui.Bar(0);
            mBox.Add(morale);
            var mgr = Cell(tr, 110);
            var hired = Cell(tr, 80, "dim");
            var act = CellBox(tr, 70);
            act.Add(Ui.Btn("Fire", () => Overlay.Confirm("Fire " + row.Item.Name + "?", $"Severance costs one month of salary ({Fmt.Money(row.Item.Salary)}) and the rest of the team won't love it.", "Fire", () => Send(new Command { Type = CommandType.Fire, A = row.Item.Id }), "btn-bad"), "btn-ghost", "btn-sm"));
            row.Root = tr;
            row.Bind = e =>
            {
                name.SetText(e.Name);
                role.SetText(Catalog.RoleName(e.Role));
                skill.SetText(e.Skill + "/5");
                salary.SetText(Fmt.Money(e.Salary));
                Ui.SetBar(morale, (float)(e.Morale / 100.0), e.Morale >= 60 ? Ui.Good : e.Morale >= 35 ? Ui.Warn : Ui.Bad);
                mgr.SetText(Name(e.ManagerId));
                hired.SetText(Fmt.ShortDate(e.HiredDay));
            };
            return row;
        }

        ListRow<Warehouse> CreateWarehouseRow()
        {
            var row = new ListRow<Warehouse>();
            var r = Ui.Row("mb4");
            var name = Ui.Text("", "b", "grow");
            var info = Ui.Text("", "small", "dim");
            r.Add(name);
            r.Add(info);
            row.Root = r;
            row.Bind = w =>
            {
                name.SetText(w.Name);
                info.SetText($"{Fmt.Int(w.Capacity)} units · {(w.MonthlyCost > 0 ? Fmt.Money(w.MonthlyCost) + "/month" : "free")}");
            };
            return row;
        }

        public override void Refresh()
        {
            var c = V.Company;
            Subtitle.SetText($"{c.Employees.Count} employees · payroll {Fmt.Money(c.Employees.Sum(e => e.Salary))}/month");
            double orders = c.Products.Where(p => p.Active).Sum(p => p.Sales7);
            _capText.SetText($"Fulfillment: {Fmt.Int(orders)} orders/day of {Fmt.Int(c.CapacityPerDay)} capacity");
            Ui.SetBar(_capBar, (float)(orders / Math.Max(1, c.CapacityPerDay)), orders > c.CapacityPerDay * 0.9 ? Ui.Bad : Ui.Accent);
            _supText.SetText($"Customer support: {Fmt.Int(orders)} orders/day of {Fmt.Int(c.SupportPerDay)} capacity");
            Ui.SetBar(_supBar, (float)(orders / Math.Max(1, c.SupportPerDay)), orders > c.SupportPerDay * 0.9 ? Ui.Bad : Ui.Good);
            int incoming = c.Shipments.Sum(s => s.Quantity);
            _storeText.SetText($"Storage: {Fmt.Int(c.StorageUsed)} in stock + {Fmt.Int(incoming)} incoming of {Fmt.Int(c.StorageCapacity)} units");
            Ui.SetBar(_storeBar, (float)((c.StorageUsed + incoming) / Math.Max(1, c.StorageCapacity)));
            _backlog.SetText(c.Backlog > 1 ? $"Backlog: {Fmt.Int(c.Backlog)} orders waiting to ship. Late orders hurt satisfaction and get cancelled." : "No backlog. Orders ship on time.");
            _recruiting.Sync(V.Tasks.Where(t => t.Kind == TaskKind.Recruit).ToList());
            _candidates.Sync(V.Candidates);
            _team.Sync(c.Employees.OrderBy(e => e.Role).ThenByDescending(e => e.Skill).ToList());
            _warehouses.Sync(c.Warehouses);
            var owned = c.Upgrades.Select(id => Catalog.GetUpgrade(id)?.Name).Where(n => n != null).ToList();
            string building = c.Building.Count > 0 ? "\nUnder construction: " + string.Join(", ", c.Building.Select(b => Catalog.GetUpgrade(b.Id)?.Name ?? b.Id)) + "." : "";
            _upgradeSummary.SetText((owned.Count == 0 ? "No upgrades yet. Storage, fulfillment, support and sourcing all improve through the upgrade tree." : $"{owned.Count} owned: {string.Join(", ", owned)}.") + building);
        }
    }

    // ====================================================================== Finance
    public class FinancePage : Page
    {
        public override string Title => "Finance";
        readonly Dictionary<string, Label[]> _pl = new Dictionary<string, Label[]>();
        Label _balance;
        KeyedList<Loan> _loans;
        KeyedList<Catalog.LoanDef> _offers;
        KeyedList<Transaction> _ledger;
        Label _ledgerNote;

        static readonly string[] Rows = { "Revenue", "Product costs", "Fees & shipping", "Refunds", "Gross profit", "Marketing", "Payroll", "Partner salaries", "Rent", "Interest", "Other", "Bonuses", "Income tax", "Net profit", "Dividends paid" };

        public FinancePage(GameScreen s) : base(s) { }

        protected override void Build()
        {
            var pl = Ui.Card("Profit & loss");
            pl.Add(Header(("", 0), ("This month", 110), ("Last month", 110), ("This quarter", 110), ("Last quarter", 110), ("This year", 110), ("Lifetime", 110)));
            foreach (var r in Rows)
            {
                var tr = Ui.Div("tr");
                bool total = r == "Gross profit" || r == "Net profit";
                Cell(tr, 0, total ? "b" : "dim").text = r;
                var cells = new Label[6];
                for (int i = 0; i < 6; i++) cells[i] = Cell(tr, 110, total ? "b" : null);
                pl.Add(tr);
                _pl[r] = cells;
            }
            Body.Add(pl);

            var bal = Ui.Card("Balance");
            _balance = Ui.Text("", "para");
            bal.Add(_balance);
            var loans = Ui.Card("Loans");
            var llist = Ui.Col();
            loans.Add(llist);
            _loans = new KeyedList<Loan>(llist, l => l.Id, CreateLoanRow, "No active loans.");
            loans.Add(Ui.Text("OFFERS", "field-label", "mt12"));
            var olist = Ui.Col();
            loans.Add(olist);
            _offers = new KeyedList<Catalog.LoanDef>(olist, l => l.Id, CreateOfferRow);
            loans.Add(Ui.Text("Borrowing usually needs your partners' approval unless you control the company.", "tiny", "dim", "mt8"));
            Body.Add(Cols(bal, loans));

            var ledger = Ui.Card("Ledger");
            _ledgerNote = Ui.Text("", "tiny", "dim", "mb8", "para");
            ledger.Add(_ledgerNote);
            ledger.Add(Header(("Date", 70), ("Type", 100), ("Description", 0), ("By", 110), ("Amount", 100)));
            var list = Ui.Col();
            ledger.Add(list);
            _ledger = new KeyedList<Transaction>(list, t => t.Id, CreateTxRow);
            Body.Add(ledger);
        }

        ListRow<Loan> CreateLoanRow()
        {
            var row = new ListRow<Loan>();
            var r = Ui.Row("mb8");
            var col = Ui.Col("grow");
            var name = Ui.Text("", "b");
            var sub = Ui.Text("", "tiny", "dim");
            col.Add(name);
            col.Add(sub);
            r.Add(col);
            r.Add(Ui.Btn("Repay", () => Send(new Command { Type = CommandType.RepayLoan, A = row.Item.Id }), "btn-ghost", "btn-sm"));
            row.Root = r;
            row.Bind = l =>
            {
                name.SetText($"{l.Lender}: {Fmt.Money(l.Balance)} left");
                sub.SetText($"{Fmt.Pct(l.AnnualRate)} APR · {Fmt.Money(l.MonthlyPayment)}/month · {l.MonthsLeft} months left");
            };
            return row;
        }

        ListRow<Catalog.LoanDef> CreateOfferRow()
        {
            var row = new ListRow<Catalog.LoanDef>();
            var r = Ui.Row("mb8");
            var col = Ui.Col("grow");
            var name = Ui.Text("", "b");
            var sub = Ui.Text("", "tiny", "dim");
            col.Add(name);
            col.Add(sub);
            var btn = Ui.Btn("Borrow", () => Send(new Command { Type = CommandType.TakeLoan, A = row.Item.Id }), "btn-sm");
            r.Add(col);
            r.Add(btn);
            row.Root = r;
            row.Bind = def =>
            {
                double monthlyRevenue = V.Company.LastMonth.Revenue > 0 ? V.Company.LastMonth.Revenue : V.Company.Month.Revenue;
                double amount; string why = null;
                switch (def.Id)
                {
                    case "credit": amount = Math.Round(Math.Min(500000, Math.Max(25000, monthlyRevenue * 1.5)) / 1000) * 1000; break;
                    case "growth": amount = V.Day < 90 ? 0 : Math.Round(Math.Min(2000000, Math.Max(50000, V.Company.Valuation * 0.12)) / 1000) * 1000; if (V.Day < 90) why = "Needs 90 days of history"; break;
                    case "venture": amount = V.Company.Valuation < 2000000 ? 0 : Math.Round(Math.Min(10000000, Math.Max(250000, V.Company.Valuation * 0.25)) / 1000) * 1000; if (amount <= 0) why = "Needs a $2M valuation"; break;
                    default: amount = Math.Round(Math.Min(1000000, Math.Max(20000, monthlyRevenue)) / 1000) * 1000; break;
                }
                bool have = V.Company.Loans.Any(l => l.Lender == def.Lender);
                name.SetText(amount > 0 ? $"{Fmt.Money(amount)} · {def.Lender}" : def.Lender);
                sub.SetText(why ?? $"{Fmt.Pct(def.Rate)} APR over {def.Months} months · {Fmt.Money(GameSimulation.MonthlyPayment(Math.Max(1, amount), def.Rate, def.Months))}/month");
                btn.SetEnabled(amount > 0 && !have);
                btn.text = have ? "Taken" : "Borrow";
            };
            return row;
        }

        ListRow<Transaction> CreateTxRow()
        {
            var row = new ListRow<Transaction>();
            var tr = Ui.Div("tr");
            var date = Cell(tr, 70, "dim");
            var type = Cell(tr, 100, "small");
            var descBox = CellBox(tr, 0);
            var descCol = Ui.Col();
            var desc = Ui.Text("", "small", "para");
            var flag = Ui.Text("", "tiny", "bad");
            descCol.Add(desc);
            descCol.Add(flag);
            descBox.Add(descCol);
            var by = Cell(tr, 110, "small");
            var amount = Cell(tr, 100, "b");
            amount.AddToClassList("right");
            row.Root = tr;
            row.Bind = t =>
            {
                date.SetText(Fmt.ShortDate(t.Day));
                type.SetText(t.Type.ToString());
                desc.SetText(t.Text);
                flag.SetText(string.IsNullOrEmpty(t.Flag) ? "" : "FLAG: " + t.Flag);
                flag.Show(!string.IsNullOrEmpty(t.Flag));
                by.SetText(t.ActorId >= 0 ? Name(t.ActorId) : "");
                amount.SetText(Fmt.SignedMoney(t.Amount));
                amount.style.color = t.Amount >= 0 ? Ui.Good : Ui.TextColor;
                tr.EnableInClassList("tr-flag", !string.IsNullOrEmpty(t.Flag));
            };
            return row;
        }

        void SetRow(string key, Func<Financials, double> f)
        {
            var c = V.Company;
            var set = new[] { c.Month, c.LastMonth, c.Quarter, c.LastQuarter, c.Year, c.Lifetime };
            var cells = _pl[key];
            for (int i = 0; i < 6; i++)
            {
                double v = f(set[i]);
                cells[i].SetText(Fmt.Money(v));
                if (key == "Net profit" || key == "Gross profit") cells[i].style.color = v >= 0 ? Ui.Good : Ui.Bad;
            }
        }

        public override void Refresh()
        {
            var c = V.Company;
            Subtitle.SetText($"Cash {Fmt.Money(c.Cash)} · debt {Fmt.Money(c.Debt)} · valuation {Fmt.Money(c.Valuation)}");
            SetRow("Revenue", f => f.Revenue);
            SetRow("Product costs", f => -f.Cogs);
            SetRow("Fees & shipping", f => -f.Fees);
            SetRow("Refunds", f => -f.Refunds);
            SetRow("Gross profit", f => f.GrossProfit);
            SetRow("Marketing", f => -f.Marketing);
            SetRow("Payroll", f => -f.Payroll);
            SetRow("Partner salaries", f => -f.PartnerSalaries);
            SetRow("Rent", f => -f.Rent);
            SetRow("Interest", f => -f.Interest);
            SetRow("Other", f => -f.Other);
            SetRow("Bonuses", f => -f.Bonuses);
            SetRow("Income tax", f => -f.Tax);
            SetRow("Net profit", f => f.NetProfit);
            SetRow("Dividends paid", f => -f.Dividends);
            double inv = c.Products.Sum(p => p.Stock * p.AvgCost) + c.Shipments.Sum(s => s.Quantity * s.UnitCost);
            long players = V.TotalPlayerShares;
            _balance.SetText($"Cash: {Fmt.MoneyFull(c.Cash)}\nHeld by payment processor: {Fmt.MoneyFull(c.HeldCash)}\nInventory (at cost): {Fmt.MoneyFull(inv)}\nDebt: {Fmt.MoneyFull(c.Debt)}\nValuation: {Fmt.MoneyFull(c.Valuation)}\n" +
                             $"Shares: {Fmt.Int(c.TotalShares)} ({Fmt.Pct(players / (double)c.TotalShares, 1)} partners, {Fmt.Pct(c.InvestorShares / (double)c.TotalShares, 1)} outside investors)\n" +
                             $"Policies: returns {(c.Policies.ReturnPolicy == 0 ? "strict" : c.Policies.ReturnPolicy == 2 ? "generous" : "standard")}, supplier audits {(c.Policies.SupplierAudits ? "on" : "off")}, open books {(c.Policies.OpenBooks ? "on" : "off")}, secret ballot {(c.Policies.SecretBallot ? "on" : "off")}");
            _loans.Sync(c.Loans);
            _offers.Sync(Catalog.Loans);
            var me = MeP;
            bool flags = c.Policies.OpenBooks || (me != null && me.HasSkill(Skills.Accountant));
            _ledgerNote.SetText(flags ? "You can see anomaly flags (overpriced suppliers, odd invoices). They're worth a question or two." : "Anomaly flags are hidden. Learn Accountant, or vote for Open Books, to see which transactions look suspicious.");
            var list = new List<Transaction>();
            for (int i = c.Ledger.Count - 1; i >= 0 && list.Count < 120; i--) list.Add(c.Ledger[i]);
            _ledger.Sync(list);
        }
    }
}
