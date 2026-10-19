using System;
using System.Collections.Generic;

namespace Stakeholders.Core
{
    public partial class GameSimulation
    {
        public static int TotalXpForLevel(int level)
        {
            int l = Math.Max(1, level) - 1;
            return 100 * l + 20 * l * Math.Max(0, l - 1);
        }

        /// <summary>Accomplishment: adds XP (levels, skill points) and performance.</summary>
        public void Award(int playerId, int xp, string reason)
        {
            var p = P(playerId);
            if (p == null || xp <= 0) return;
            if (C.Has("ppl4")) xp = (int)Math.Round(xp * 1.2);
            p.Xp += xp;
            p.Performance += xp;
            p.QuarterXp += xp;
            Notify(playerId, NoticeKind.Xp, $"+{xp} XP: {reason}");
            int guard = 0;
            while (p.Xp >= TotalXpForLevel(p.Level + 1) && guard++ < 20)
            {
                p.Level++;
                p.SkillPoints++;
                Notify(playerId, NoticeKind.Success, $"Level up! You are now level {p.Level}. +1 skill point.");
                Log(LogKind.Personal, playerId, $"{p.Name} reached business level {p.Level}.");
            }
        }

        /// <summary>A mistake: lowers performance (never XP).</summary>
        public void Penalize(int playerId, int amount, string reason)
        {
            var p = P(playerId);
            if (p == null || amount <= 0) return;
            p.Performance -= amount;
            Notify(playerId, NoticeKind.Danger, $"-{amount} performance: {reason}");
            Log(LogKind.Personal, playerId, $"{p.Name}: -{amount} performance ({reason}).");
        }

        public bool CanUnlock(Player p, string skillId, out string reason)
        {
            reason = null;
            var def = Skills.Get(skillId);
            if (def == null) { reason = "Unknown skill."; return false; }
            if (p.HasSkill(skillId)) { reason = "Already learned."; return false; }
            if (p.SkillPoints <= 0) { reason = "No skill points. Gain XP to level up."; return false; }
            if (def.Tier >= 2)
            {
                if (p.Level < Skills.Tier2Level) { reason = $"Requires business level {Skills.Tier2Level}."; return false; }
                bool hasTree = false;
                foreach (var s in p.Skills)
                {
                    var d = Skills.Get(s);
                    if (d != null && d.Tree == def.Tree) { hasTree = true; break; }
                }
                if (!hasTree) { reason = $"Learn another {def.Tree} skill first."; return false; }
            }
            return true;
        }

        void TasksDaily()
        {
            for (int i = State.Tasks.Count - 1; i >= 0; i--)
            {
                var t = State.Tasks[i];
                if (State.Day < t.EndDay) continue;
                State.Tasks.RemoveAt(i);
                CompleteTask(t);
            }
            foreach (var p in State.Players)
                if (p.AwayUntilDay > 0 && p.AwayUntilDay <= State.Day && !string.IsNullOrEmpty(p.Status)) { p.Status = ""; p.AwayUntilDay = 0; }
            State.Candidates.RemoveAll(c => c.ExpiresDay < State.Day);
        }

        void CompleteTask(PlayerTask t)
        {
            var p = P(t.PlayerId);
            if (p == null) return;
            switch (t.Kind)
            {
                case TaskKind.Research:
                {
                    var found = RunResearch(t.PlayerId, t.Param, t.Param2);
                    if (found.Count == 0)
                    {
                        Notify(t.PlayerId, NoticeKind.Info, "Research finished but found nothing new in that category.");
                        break;
                    }
                    var names = new List<string>();
                    foreach (var d in found) names.Add(d.Name);
                    Notify(t.PlayerId, NoticeKind.Private, $"Research complete: {string.Join(", ", names)}. Check your notebook.");
                    Award(t.PlayerId, 15 * found.Count, "Product research");
                    Log(LogKind.Market, t.PlayerId, $"{p.Name} finished researching {(string.IsNullOrEmpty(t.Param) || t.Param == "Any" ? "the market" : t.Param)}.");
                    break;
                }
                case TaskKind.Recruit:
                {
                    if (!Enum.TryParse(t.Param, out EmployeeRole role)) role = EmployeeRole.Warehouse;
                    bool recruiter = p.HasSkill(Skills.Recruiter);
                    int n = recruiter ? 5 : 3;
                    for (int k = 0; k < n; k++)
                    {
                        int skill = recruiter ? Rng.Range(2, 5) : Rng.Range(1, 4);
                        if (Rng.Chance(0.08)) skill = Math.Min(5, skill + 1);
                        if (C.Has("ppl3")) skill = Math.Min(5, skill + 1);
                        double salary = Math.Round(Catalog.RoleBaseSalary(role) * (0.8 + 0.2 * skill) * Rng.Range(0.9, 1.15) / 50) * 50;
                        var cand = new Candidate
                        {
                            Id = NewId("c"), Name = RandomPersonName(), Role = role, Skill = skill, Salary = salary, FoundBy = t.PlayerId,
                            ExpiresDay = State.Day + 12,
                            ShownSkillLow = recruiter ? skill : Math.Max(1, skill - Rng.Range(0, 1)),
                            ShownSkillHigh = recruiter ? skill : Math.Min(5, skill + Rng.Range(0, 1)),
                        };
                        if (!recruiter && cand.ShownSkillLow == cand.ShownSkillHigh)
                        {
                            if (cand.ShownSkillHigh < 5) cand.ShownSkillHigh++;
                            else cand.ShownSkillLow--;
                        }
                        State.Candidates.Add(cand);
                    }
                    Notify(t.PlayerId, NoticeKind.Info, $"Recruiting done: {n} {Catalog.RoleName(role)} candidates are waiting in Operations.");
                    Log(LogKind.Team, t.PlayerId, $"{p.Name} found {n} {Catalog.RoleName(role)} candidates.");
                    break;
                }
                case TaskKind.Investigate:
                {
                    int found = 0;
                    double chance = p.HasSkill(Skills.Investigator) ? 0.7 : 0.35;
                    foreach (var e in State.Evidence)
                    {
                        if (e.ActorId == t.PlayerId || e.Exposed || e.DiscoveredBy.Contains(t.PlayerId)) continue;
                        double c = chance * (Has(e.ActorId, Skills.Deception) ? 0.5 : 1);
                        if (Rng.Chance(c)) { DiscoverEvidence(e, t.PlayerId); found++; }
                    }
                    if (found == 0) Notify(t.PlayerId, NoticeKind.Info, "Your investigation of the books found nothing conclusive.");
                    else Notify(t.PlayerId, NoticeKind.Private, $"Your investigation uncovered {found} questionable deal{(found == 1 ? "" : "s")}. See the Private tab.");
                    break;
                }
                case TaskKind.PersonalGig:
                {
                    double amount = t.Param2;
                    PayPlayer(t.PlayerId, amount);
                    Notify(t.PlayerId, NoticeKind.Success, $"You earned {Fmt.Money(amount)} personally ({t.Label}).");
                    break;
                }
                case TaskKind.PublicRelations:
                    Notify(t.PlayerId, NoticeKind.Info, $"Finished: {t.Label}.");
                    break;
            }
        }

        public PlayerTask StartTask(int playerId, TaskKind kind, string label, int days, string param = null, int param2 = 0, bool secret = false)
        {
            var t = new PlayerTask
            {
                Id = NewId("k"), PlayerId = playerId, Kind = kind, Label = label, Param = param, Param2 = param2,
                StartDay = State.Day, EndDay = State.Day + Math.Max(1, days), Secret = secret
            };
            State.Tasks.Add(t);
            return t;
        }

        public string RandomPersonName() => Rng.Pick(Catalog.FirstNames) + " " + Rng.Pick(Catalog.LastNames);

        void ProgressionDaily()
        {
            // Quarterly career stats reset in StartBoardMeeting. Nothing daily yet besides away status.
        }

        // ------------------------------------------------------------------ personal finance

        public double HoldingsValue(int playerId)
        {
            double v = 0;
            var pr = Priv(playerId);
            if (pr != null) foreach (var h in pr.Holdings) v += h.Value;
            return v;
        }

        public double NetWorth(int playerId)
        {
            var p = P(playerId);
            var pr = Priv(playerId);
            if (p == null || pr == null) return 0;
            return pr.Cash + HoldingsValue(playerId) + OwnershipPct(p) * C.Valuation;
        }

        public double FairSharePrice(long shares) => C.TotalShares > 0 ? C.Valuation * shares / C.TotalShares : 0;
    }
}
