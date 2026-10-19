# Stakeholders (working title)

*Build the company together. Build your fortune separately.*

A 2D, UI-driven business management game in Unity 6 (6000.0.32f1). Partners run one shared e-commerce company (solo with AI partners, or online with friends) while each builds a personal career, fortune and ownership stake, and each is tempted by private offers that pay them at the company's expense.

## Running it

1. Open the project in Unity 6000.0.32f1. Unity installs the added packages automatically (Netcode for GameObjects, Multiplayer Services, Newtonsoft JSON).
2. Press **Play** in any scene. The game boots itself (`Bootstrap` in `GameApp.cs`), so there's no scene setup or prefab to wire up.
3. To make a standalone build, use the **Stakeholders > Build Windows Player** menu.

## Multiplayer

The host runs the simulation. Each client sends commands and receives only its own filtered view of the game, so your private offers and evidence never leave the host for anyone else. Players who drop out can rejoin with the same name.

- **LAN / direct IP**: *Host on LAN* on one machine, *Join by IP* on the others (UDP port 7777). To test on one PC, run two copies of a build: one hosts, the other joins `127.0.0.1`.
- **Online (Unity Relay)**: *Host online game* gives you a join code; friends enter it on the main menu. This uses Unity Gaming Services. The project is already linked to a Unity Cloud project (`ProjectSettings > cloudProjectId`), but **Relay must be enabled for that project in the Unity Cloud dashboard**. If hosting online fails, the error message tells you why. Online hosting has not been tested yet. LAN play has been tested with two local copies.

Board meetings are time-limited in multiplayer (2/4/6 minutes, or none). Any partner can pause the clock.

## What's in the game

| Design pillar | Where it lives |
|---|---|
| Shared company: cash, revenue, profit, debt, products, inventory, suppliers, ads, employees, warehouses, satisfaction, reputation, valuation | `Core/Model/Company.cs`, `Sim.Economy.cs` |
| Personal progression: personal cash, salary, ownership, XP, levels, performance (which can go down) | `Core/Model/GameState.cs`, `Sim.Progression.cs` |
| 24 skills in 6 trees (Product Research, Negotiation, Marketing, Finance, Management, plus hidden **Intrigue**: Investigator, Deception, Networker, Spin Doctor) | `Core/Content/Skills.cs` |
| Always something to do: 28 situation types, scaled to company size and player count | `Sim.Events.cs` |
| Private offers: kickbacks, data sales, fake invoices, influencer skims, poaching, insider tips, speaking gigs, vote buying and vote trading | `Sim.Intrigue.cs` |
| Betrayal without a traitor: evidence, discovery, blackmail, public exposure, discipline votes, ledger anomaly flags for accountants | `Sim.Intrigue.cs`, `ViewBuilder.cs` |
| Board meetings: salary, bonus, dividend, loan, warehouse, investment, new shares, policy, spending authority, discipline, product transfer, company sale (75% supermajority) | `Sim.Board.cs` |
| Ownership vs performance vs wealth: share trading between partners, performance equity (1% of shares per quarter, split by XP), final net worth ranking | `Sim.Board.cs`, `Sim.Economy.cs` (`EndGame`) |
| Solo play: AI partners with 8 personalities (Greedy, Loyal, Risk-Taker, Conservative, Workaholic, Lazy, Manipulative, Ambitious) who vote, trade votes, take bribes and blackmail, and work only in the areas you delegate to them (purchasing, marketing, products, hiring, situations, upgrades) | `Sim.AI.cs`, `Content/Traits.cs`, `Content/Growth.cs` (`Roles`) |
| The Reckoning: at game over every secret deal is revealed | `Sim.Economy.cs` (`EndGame`), `GameOverView` |

### Long-term play (the strategy layer)

| System | What it does | Where it lives |
|---|---|---|
| Growth stages | Garage Startup → Online Shop ($500K) → Growing Brand ($2.5M) → Scale-up ($10M) → Market Leader ($35M) → IPO Ready ($100M). A stage needs a week of sustained valuation; each one unlocks more | `Content/Growth.cs`, `Sim.Growth.cs` |
| Upgrade tree | 24 upgrades in 6 branches (Logistics, Brand, Sourcing, Channels, Data, People), 4 tiers each, built over days. Plus 2 Investor-only deals | `Content/Catalog.cs`, `Sim.Growth.cs` |
| Company strategy | Premium, Value or Trend positioning, chosen by board vote | `Content/Growth.cs`, `Sim.Board.cs` |
| Rivals | 5 rival companies with styles and focus categories: leaderboard, market share per category, price wars, copycats, poaching, stumbles. Buy weak rivals from the Scale-up stage | `Sim.Growth.cs`, `PagesGrowth.cs` |
| Product levers | Flash sales and free-shipping weeks (with cooldowns), R&D to Plus and Pro versions, auto-restock, B2B contracts with deadlines and penalties | `Sim.Growth.cs`, `Sim.Events.cs` |
| Secret ambitions | Each partner picks one of three personal goals; completing it adds a bonus to final net worth | `Content/Growth.cs`, `Sim.Growth.cs` |
| The IPO | At IPO Ready the board can take the company public: +50% valuation, game ends 30 days later | `Sim.Board.cs`, `Sim.Growth.cs` |
| Guidance and feedback | "Next steps" advisor on the Dashboard, milestone celebrations with confetti, procedural sound effects, 16 local achievements | `Screens/Advisor.cs`, `UI/Juice.cs` |

## Architecture

```
Assets/Stakeholders/
  Core/     Pure C# simulation (no UnityEngine). Model, content tables, rules, AI, per-player views.
  App/      Unity layer: UI Toolkit screens built in code, sessions, networking, saves.
  Editor/   Build script (menu item and command line).
  Resources/StakeholdersUI/  Theme and stylesheet (Game.uss holds all the colors and styles).
Tools/SimHarness/  .NET console app that runs whole games headlessly (balance, fuzz, save/load tests).
```

- **Every action is a `Command`.** Humans (UI), AI partners and remote clients all go through `GameSimulation.Execute`, which validates everything. That's why solo and multiplayer behave the same.
- **The UI only reads views.** `ViewBuilder.Build(state, playerId)` strips everything a player may not see (other partners' cash, offers and evidence, hidden skills, hidden market truth, accountant-only flags, secret ballots). Solo, host and clients all render from views.
- **Saves**: solo games autosave every in-game month, and **Menu > Save game** writes a manual slot. Both are JSON files in `Application.persistentDataPath/saves`.

## Tuning and adding content

- Products, suppliers, warehouses, loans, investments, names: `Core/Content/Catalog.cs`
- Skills: `Core/Content/Skills.cs`; AI personalities: `Core/Content/Traits.cs`
- New situations: add a `Make...()` template and a `case` in `ResolveEventChoice` (`Sim.Events.cs`)
- Economy knobs: demand model (`DemandFor`, `StrategyDemandMult`), ad model (`CampaignUnits`), valuation (`UpdateValuation`), staff productivity (`FulfillmentCapacity` / `SupportCapacity`), overhead and corporate tax (`MonthEnd`) in `Sim.Economy.cs`
- Stages, strategies, ambitions, rivals and delegation roles: `Core/Content/Growth.cs`; their rules: `Sim.Growth.cs`

Check balance changes headlessly (requires the .NET 8 SDK):

```
cd Tools/SimHarness
dotnet run -c Release -- balance 4 4     # 4 seeds, 4 AI partners, 3-year games
dotnet run -c Release -- passive 3       # an idle human with 2 AI partners
dotnet run -c Release -- fuzz 5          # thousands of random/invalid commands
dotnet run -c Release -- serialize       # save/load round trip + view leak check
dotnet run -c Release -- trace 1 4 120   # the company log of one AI game, day by day
```

Set `SIM_VERBOSE=1` for per-quarter P&L, staff and product breakdowns. Current AI-only benchmark (16 seeds, 3-year games): about a third of companies IPO in year 3, a couple more reach IPO Ready without going public, most of the rest finish as a Scale-up or Market Leader, and about 1 in 16 goes bankrupt.

## Automated smoke tests (builds)

```
Stakeholders.exe -screen-fullscreen 0 -stk-autotest C:\shots               # every page and dialog, screenshots, quit
Stakeholders.exe -stk-mp-host C:\shots\host                                  # LAN host, waits for one client
Stakeholders.exe -stk-mp-join 127.0.0.1 C:\shots\client                      # LAN client
```

Each run writes `result.txt` with a summary and any runtime errors.

## Known gaps and next steps

- Online Relay hosting is implemented but untested (see Multiplayer above).
- Multiplayer saves: only solo games save and load; a host can't resume a saved multiplayer game yet.
- AI chat replies are canned lines based on keywords.
- Art is intentionally minimal (color, typography, charts, procedural sounds). A logo, icons and music are natural next steps.
- Balance is tuned against AI-only games and an idle-human scenario; real playtests will tell more. A human who does nothing usually goes bankrupt within a year, by design.
