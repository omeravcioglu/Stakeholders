# Stakeholders (working title)

*Build the company together. Build your fortune separately.*

**A UI-driven business management game in Unity 6, for solo play with AI partners or online with friends.** The partners run one shared e-commerce company, while each of them builds a personal career, fortune and ownership stake. Each is also tempted by private offers that pay them at the company's expense. At the end, the Reckoning reveals every secret deal.

---

## Gameplay

- **One shared company:** cash, revenue, profit and debt; products, inventory and suppliers; ads, employees and warehouses; satisfaction, reputation and valuation.
- **Your own career:** personal cash, salary, ownership, XP and levels, and performance that can also go down. There are 24 skills in 6 trees, including a hidden **Intrigue** tree (Investigator, Deception, Networker, Spin Doctor).
- **Always something to do:** 28 kinds of situations, scaled to the company's size and the number of players.
- **Private offers:** kickbacks, data sales, fake invoices, influencer skims, poaching, insider tips, speaking gigs, vote buying and vote trading.
- **Betrayal without a traitor role:** evidence, discovery, blackmail, public exposure, discipline votes, and ledger anomalies that only accountants see.
- **Board meetings:** vote on salaries, bonuses, dividends, loans, warehouses, investments, new shares, policy, spending authority, discipline and product transfers. Selling the company needs a 75% supermajority.
- **The long game:**
  - growth stages from Garage Startup to IPO Ready ($100M);
  - 24 upgrades in 6 branches, and a company strategy (Premium, Value or Trend);
  - 5 rival companies with price wars, copycats and poaching, and you can buy the weak ones;
  - B2B contracts, R&D for Plus and Pro versions, and an IPO.
- **Ownership versus wealth:** partners trade shares, earn performance equity each quarter, and are ranked by final net worth. Each one also has a secret personal ambition.
- **Solo play:** AI partners with 8 personalities (Greedy, Loyal, Risk-Taker, Conservative, Workaholic, Lazy, Manipulative, Ambitious). They vote, trade votes, take bribes and blackmail, and work only in the areas you delegate to them.
- **Multiplayer:** LAN or direct IP, or online through Unity Relay with a join code. Board meetings are time-limited, any partner can pause the clock, and players who drop out can rejoin.

**Status:** a playable prototype.
- The full design is in place, with solo saves, 16 achievements and procedural sound effects.
- Not done yet:
  - online Relay hosting is implemented but untested;
  - multiplayer games can't be saved yet;
  - the AI partners' chat uses canned lines;
  - the art is intentionally minimal, and there's no music yet.

## Tech stack

| Area | What it uses |
|---|---|
| Engine | **Unity 6** (6000.0.32f1), 2D |
| Simulation | **Pure C# with no UnityEngine references.** Every action is a `Command` that `GameSimulation.Execute` validates, whether it comes from the UI, an AI partner or a remote client, so solo and multiplayer behave the same. |
| Multiplayer | **Netcode for GameObjects** + Unity Transport; **Unity Multiplayer Services** (Relay sessions with join codes). The host runs the simulation and sends each client only its own filtered view. |
| UI | **UI Toolkit screens built in code**, with all colors and styles in one stylesheet and a "founder's notebook" look. Its sprites are defined as SVG and rasterized by a Node script. |
| Saving | JSON saves: an autosave every in-game month plus a manual slot |
| Testing | A headless **.NET 8 harness** that plays whole games for balance, fuzzes thousands of random and invalid commands, round-trips saves and checks views for leaks. Builds have flags for automated screenshot tours and LAN host/join smoke tests. |

## What I built

All of it: 49 C# scripts, about 18k lines.

| Area | Where |
|---|---|
| The simulation: model, content tables, rules, AI partners, per-player views | `Assets/Stakeholders/Core/` (23 files, ~9k lines) |
| Economy, progression, events, intrigue, board votes, growth | `Core/Sim.*.cs` (`Sim.Economy`, `Sim.Progression`, `Sim.Events`, `Sim.Intrigue`, `Sim.Board`, `Sim.Growth`) |
| Content: products, suppliers, skills, personalities, stages, strategies, ambitions | `Core/Content/` (`Catalog`, `Skills`, `Traits`, `Growth`) |
| The Unity layer: UI Toolkit screens, sessions, networking, saves | `Assets/Stakeholders/App/` (24 files, ~8.5k lines) |
| Build script | `Assets/Stakeholders/Editor/` |
| Headless balance and fuzz harness | `Tools/SimHarness/` |
| The notebook art generator | `Tools/NotebookArt/` (`build.mjs`, `slices.json`) |

### Code highlights

- **`ViewBuilder.Build(state, playerId)`:** the UI only ever reads a view. Before anything reaches a player, the view drops:
  - other partners' cash, offers and evidence;
  - hidden skills and the hidden truth about the market;
  - flags meant only for accountants.

  In multiplayer, private deals never leave the host for anyone else, and the harness checks the views for leaks.
- **Commands everywhere:** humans, AI partners and remote players all go through the same validated `Execute`. That's what makes AI partners, multiplayer and fuzzing share one code path.
- **`Tools/SimHarness`:**
  - `balance` runs AI-only three-year games across seeds;
  - `passive` simulates an idle human, who usually goes bankrupt within a year by design;
  - `fuzz` throws thousands of invalid commands at the simulation;
  - `serialize` round-trips saves;
  - `trace` prints one company's log day by day.

  In the current benchmark, about a third of AI-run companies reach an IPO in year 3.

## Scenes

| Scene | Purpose |
|---|---|
| `Assets/Scenes/SampleScene.unity` | Any scene works: `Bootstrap` in `GameApp.cs` builds the game when you press Play. |

## Integrated third-party assets

No Asset Store content. The only third-party asset is the **Coming Soon** font (Apache License 2.0), with its license file next to it in `Resources/StakeholdersUI/Notebook/Fonts/`.

## Docs

- [`PROJECT_README.md`](PROJECT_README.md): the project's own notes, covering running it, multiplayer, every design pillar with where it lives, the architecture, tuning and adding content, the smoke tests and known gaps

## About this repository

This public repository is a **showcase**. It contains the documentation and the **58 source files I wrote** for this project. The complete Unity project, with its settings and Unity Cloud link, is kept in a private repository.

Copyright © Omer Avcioglu (McHunter Studio). **All rights reserved.** Viewing only; see [LICENSE](LICENSE).
