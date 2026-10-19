using Stakeholders.App.UI;
using UnityEngine;

namespace Stakeholders.App
{
    /// <summary>The "How to play" guide. Shown automatically on the first game.</summary>
    public static class HelpView
    {
        const string SeenKey = "stk_help_seen";

        public static void ShowIfFirstTime(GameScreen s)
        {
            if (PlayerPrefs.GetInt(SeenKey, 0) == 1) return;
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
            Show(s);
        }

        public static void Show(GameScreen s)
        {
            var m = s.UiRoot.ShowModal("How to play", true);
            void Section(string title, string text)
            {
                m.Body.Add(Ui.Text(title, "h2", "mt12"));
                m.Body.Add(Ui.Text(text, "para", "dim", "mt8"));
            }
            m.Body.Add(Ui.Text("Build the company together. Build your fortune separately.", "h1", "accent"));
            Section("The shared company",
                "Everyone runs the same e-commerce company. Company cash, products, suppliers, staff and warehouses belong to all partners. " +
                "If anyone spends money it leaves the shared account; if anyone's product makes money, everyone's shares are worth more.");
            Section("Your first minutes",
                "1. Pick your secret ambition, then look at Next steps on the Dashboard: it always suggests what to do.\n" +
                "2. The garage already holds one product nobody manages. Claim it on the Products page and its profits count toward your performance.\n" +
                "3. Open Market & Research. You have two product leads in your private notebook. Launch one: pick a supplier, a quantity and a price.\n" +
                "4. Watch Products: restock before you sell out, adjust prices, run ads, flash sales and free-shipping weeks.\n" +
                "5. Keep an eye on Situations. Whoever handles a problem or opportunity earns the XP.");
            Section("Growing the company",
                "The company climbs six stages as its valuation grows, from Garage Startup to IPO Ready. Each stage unlocks more of the upgrade tree " +
                "(Logistics, Brand, Sourcing, Channels, Data, People), a company strategy (premium, value or trends), product R&D (Plus and Pro versions), " +
                "bigger B2B contracts and, later, acquisitions. Five rival companies grow alongside you: they start price wars, copy your best sellers and poach staff. " +
                "Beat them on the Competition page, buy the weak ones, and at IPO Ready take the company public for a 50% premium.");
            Section("Working with AI partners",
                "AI partners only handle the areas you give them on the Partners page: purchasing, marketing, products, hiring, situations or upgrades. " +
                "Everything else is yours. Delegate wisely: a greedy partner in charge of purchasing may take kickbacks.");
            Section("Your career",
                "XP levels you up and gives skill points (Career page). Skills let partners specialize: research, negotiation, marketing, finance, management, " +
                "and hidden intrigue skills. Performance is your track record; mistakes cost performance. With performance equity on, each quarter's XP turns into a small grant of new shares.");
            Section("Board meetings",
                "Every quarter the clock stops for a board meeting. Propose raises, bonuses, dividends, loans, warehouses, policies or discipline, then vote. " +
                "Votes are weighted by ownership. Deals made in chat are not enforced by anyone. Spending above your authority needs your partners' approval.");
            Section("Personal interest",
                "Your personal cash is separate from the company's. You earn salary, dividends, bonuses and share sales, and you can invest it. " +
                "Private offers arrive in the Private tab: kickbacks, side gigs, data sales. Taking them makes you richer and may hurt the company. " +
                "Every questionable deal can leave evidence, and partners who find it can expose you, or ask for a price to stay quiet.");
            Section("Winning",
                "When the game ends, net worth decides: personal cash + investments + your share of the company's valuation, plus a bonus if you completed your secret ambition. " +
                "Then every secret is revealed. Going public multiplies everyone's shares, so the whole team wins big together, but only one partner finishes richest.");
            Section("Controls",
                "Space pauses, 1/2/3 change speed. Chat and private messages are in the right panel. The game autosaves every month in solo play.");
            m.AddButton("Let's go", m.Close);
        }
    }
}
