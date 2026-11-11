using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Copilot.Engine.Model;

namespace Copilot.Engine.Lethal
{
    public enum LethalStatus
    {
        /// <summary>Search was exhaustive: no lethal with the modelled actions.</summary>
        None = 0,
        /// <summary>Budget or cancellation hit before the search finished. NOT a claim of "no lethal".</summary>
        Inconclusive = 1,
        /// <summary>A legal sequence exists, but unmodelled/unverified rules or secrets could break it.</summary>
        Likely = 2,
        /// <summary>A legal sequence exists and nothing unmodelled or unverified is involved.</summary>
        Proven = 3
    }

    public enum ActionKind { Attack = 0, Play = 1 }

    public sealed class PlannedAction
    {
        public ActionKind Kind { get; set; }
        /// <summary>Attack: attacking entity (LethalSolver.HeroAttacker for the hero).</summary>
        public int AttackerId { get; set; }
        /// <summary>Attack: target entity or LethalSolver.Face. Floating Lessons: chosen minion.</summary>
        public int TargetId { get; set; } = LethalSolver.NoTarget;
        /// <summary>Play: hand entity id.</summary>
        public int CardEntityId { get; set; }
        /// <summary>Play (minions): board slot, 0 = leftmost.</summary>
        public int Slot { get; set; } = -1;

        public override string ToString() => Kind == ActionKind.Play
            ? "Play #" + CardEntityId + (Slot >= 0 ? " @" + Slot : "") + (TargetId != LethalSolver.NoTarget ? " -> #" + TargetId : "")
            : (AttackerId == LethalSolver.HeroAttacker ? "Hero" : "#" + AttackerId) + " -> " + (TargetId == LethalSolver.Face ? "Face" : "#" + TargetId);
    }

    public sealed class LethalResult
    {
        public LethalStatus Status { get; set; }
        public List<PlannedAction> Actions { get; } = new List<PlannedAction>();
        /// <summary>Total damage the plan deals to the enemy hero (burn + attacks).</summary>
        public int FaceDamage { get; set; }
        /// <summary>Enemy health + armor, including armor gained during the plan (e.g. Deathrattle).</summary>
        public int RequiredDamage { get; set; }
        public List<string> Reasons { get; } = new List<string>();
        public int NodesVisited { get; set; }
    }

    /// <summary>
    /// Exact turn search: card plays from the registry (CardRules) and attacks, with mana, board limit,
    /// Taunt, Divine Shield, Poisonous, Windfury, Immune, Stealth, armor and Deathrattle armor.
    /// Iterative deepening on the number of cards played, so the first hit spends the fewest cards.
    /// Unmodelled own/enemy cards and enemy secrets cap a found line at Likely.
    /// </summary>
    public static class LethalSolver
    {
        public const int HeroAttacker = -1;
        public const int Face = -2;
        public const int NoTarget = -3;
        public const int DefaultNodeBudget = 200000;
        private const int MaxBoard = 7;

        private struct Att
        {
            public int Id, Atk, Hp, Swings;
            public bool Shield, Poison, Immune, CanFace, Hero, Alive;
        }

        private struct Foe
        {
            public int Id, Atk, Hp, DrArmor;
            public bool Shield, Poison, Immune, Taunt, Stealth, Alive;
        }

        private struct Cd
        {
            public int Id, Cost, Atk, Hp;
            public Fx Fx;
            public bool Spell, Used;
        }

        private sealed class S
        {
            public Att[] A;
            public Foe[] F;
            public Cd[] H;
            public int Mana, Eff, Dealt;
            public S Clone() => new S { A = (Att[])A.Clone(), F = (Foe[])F.Clone(), H = (Cd[])H.Clone(), Mana = Mana, Eff = Eff, Dealt = Dealt };
        }

        public static LethalResult Solve(GameSnapshot s, int nodeBudget = DefaultNodeBudget, CancellationToken token = default(CancellationToken))
        {
            var result = new LethalResult();
            var me = s.Me;
            var opp = s.Opp;
            result.RequiredDamage = opp.HeroHealth + opp.Armor;

            if (!s.MyTurn) { result.Reasons.Add("Not your turn"); return result; }
            if (opp.HeroImmune) { result.Reasons.Add("Enemy hero is immune"); return result; }
            if (result.RequiredDamage <= 0) { result.Reasons.Add("Enemy hero already at 0"); return result; }

            var st = new S { Mana = me.Mana, Eff = result.RequiredDamage };

            var atts = new List<Att>
            {
                // Index 0 is always the hero so minion slots start at 1; Swings 0 means "cannot attack".
                new Att
                {
                    Id = HeroAttacker, Atk = me.HeroAttack, Hp = me.HeroHealth + me.Armor,
                    Swings = me.HeroAttack > 0 ? me.HeroAttacksRemaining : 0,
                    Immune = me.HeroImmune, CanFace = true, Hero = true, Alive = true
                }
            };
            foreach (var m in me.Board)
            {
                atts.Add(new Att
                {
                    Id = m.Id, Atk = m.Attack, Hp = m.Health, Swings = m.Attack > 0 ? m.AttacksRemaining : 0,
                    Shield = m.DivineShield, Poison = m.Poisonous, Immune = m.Immune, CanFace = m.CanAttackHero, Alive = m.Health > 0
                });
                if (m.Support == Support.Unknown && m.AttacksRemaining > 0 && m.Attack > 0)
                    result.Reasons.Add("Own card with unmodelled effect: " + m);
            }
            st.A = atts.ToArray();

            var foes = new List<Foe>();
            foreach (var m in opp.Board)
            {
                if (m.Support == Support.Unknown)
                    result.Reasons.Add("Enemy card with unmodelled effect: " + m);
                foes.Add(new Foe
                {
                    Id = m.Id, Atk = m.Attack, Hp = m.Health, Shield = m.DivineShield, Poison = m.Poisonous,
                    Immune = m.Immune, Taunt = m.Taunt, Stealth = m.Stealth, DrArmor = m.DeathrattleArmor, Alive = m.Health > 0
                });
            }
            st.F = foes.ToArray();
            if (opp.Secrets > 0)
                result.Reasons.Add(opp.Secrets + " unknown enemy secret(s)");

            var hand = new List<Cd>();
            var rulesByEntity = new Dictionary<int, CardRule>();
            foreach (var c in me.Hand)
            {
                var rule = CardRules.Get(c.CardId);
                hand.Add(new Cd { Id = c.Id, Cost = c.Cost, Atk = c.Attack, Hp = c.Health, Fx = rule?.Effect ?? Fx.None, Spell = c.Kind == CardKind.Spell });
                if (rule != null) rulesByEntity[c.Id] = rule;
            }
            st.H = hand.ToArray();

            var ctx = new Ctx { Budget = nodeBudget, Token = token, HeroHp = me.HeroHealth };
            bool found = false;
            for (int maxPlays = 0; maxPlays <= st.H.Length && !found && !ctx.Aborted; maxPlays++)
            {
                ctx.Failed.Clear();
                if (maxPlays > 0 && !AnyPlayable(st)) break;
                found = ctx.Dfs(st, maxPlays);
            }
            result.NodesVisited = ctx.Nodes;

            if (found)
            {
                result.Actions.AddRange(ctx.Plan);
                result.FaceDamage = ctx.FaceDamage;
                result.RequiredDamage = ctx.FinalRequired;
                foreach (var a in ctx.Plan)
                    if (a.Kind == ActionKind.Play && rulesByEntity.TryGetValue(a.CardEntityId, out var rule) && rule.Unverified)
                        result.Reasons.Add("Unverified rule: " + rule.Note);
                result.Status = result.Reasons.Count == 0 ? LethalStatus.Proven : LethalStatus.Likely;
                return result;
            }

            if (ctx.Aborted)
            {
                result.Status = LethalStatus.Inconclusive;
                result.Reasons.Add("Search budget/cancellation reached");
            }
            else
            {
                result.Status = LethalStatus.None;
            }
            return result;
        }

        private static bool AnyPlayable(S s)
        {
            for (int i = 0; i < s.H.Length; i++) if (s.H[i].Fx != Fx.None && s.H[i].Cost <= s.Mana) return true;
            return false;
        }

        private sealed class Ctx
        {
            public int Budget, Nodes, HeroHp, FaceDamage, FinalRequired;
            public bool Aborted;
            public CancellationToken Token;
            public readonly List<PlannedAction> Plan = new List<PlannedAction>();
            public readonly HashSet<string> Failed = new HashSet<string>();
            private readonly List<PlannedAction> _path = new List<PlannedAction>();

            public bool Dfs(S s, int playsLeft)
            {
                if (Token.IsCancellationRequested || ++Nodes > Budget) { Aborted = true; return false; }

                if (s.Eff <= 0) return Succeed(s);

                bool anyTaunt = false;
                for (int j = 0; j < s.F.Length; j++)
                    if (s.F[j].Alive && s.F[j].Taunt && !s.F[j].Stealth) { anyTaunt = true; break; }

                int potential = Potential(s, out int totalSwings, out int maxSwings);
                if (!anyTaunt && potential >= s.Eff) return Succeed(s);
                if (potential + Bound(s, playsLeft, totalSwings, maxSwings) < s.Eff) return false;

                var key = Key(s, playsLeft);
                if (Failed.Contains(key)) return false;

                // Card plays first: they are what iterative deepening is budgeting.
                if (playsLeft > 0)
                    for (int i = 0; i < s.H.Length; i++)
                    {
                        if (s.H[i].Used || s.H[i].Fx == Fx.None || s.H[i].Cost > s.Mana) continue;
                        if (TryPlay(s, i, playsLeft)) return true;
                        if (Aborted) return false;
                    }

                if (anyTaunt)
                    for (int i = 0; i < s.A.Length; i++)
                    {
                        if (!s.A[i].Alive || s.A[i].Swings <= 0 || s.A[i].Atk <= 0) continue;
                        for (int j = 0; j < s.F.Length; j++)
                        {
                            var f = s.F[j];
                            if (!f.Alive || !f.Taunt || f.Stealth || f.Immune) continue;
                            var n = s.Clone();
                            if (!Attack(n, i, j)) continue;
                            _path.Add(new PlannedAction { Kind = ActionKind.Attack, AttackerId = s.A[i].Id, TargetId = f.Id });
                            if (Dfs(n, playsLeft)) return true;
                            _path.RemoveAt(_path.Count - 1);
                            if (Aborted) return false;
                        }
                    }

                Failed.Add(key);
                return false;
            }

            private bool Succeed(S s)
            {
                Plan.AddRange(_path);
                // s.Eff is what is still missing after burn; add face swings until it is covered.
                int attackDamage = 0;
                for (int i = 0; i < s.A.Length && attackDamage < s.Eff; i++)
                {
                    if (!s.A[i].Alive || !s.A[i].CanFace || s.A[i].Swings <= 0 || s.A[i].Atk <= 0) continue;
                    for (int k = 0; k < s.A[i].Swings && attackDamage < s.Eff; k++)
                    {
                        Plan.Add(new PlannedAction { Kind = ActionKind.Attack, AttackerId = s.A[i].Id, TargetId = Face });
                        attackDamage += s.A[i].Atk;
                    }
                }
                FaceDamage = s.Dealt + attackDamage;
                FinalRequired = s.Dealt + s.Eff;
                return true;
            }

            private static int Potential(S s, out int totalSwings, out int maxSwings)
            {
                int p = 0; totalSwings = 0; maxSwings = 0;
                for (int i = 0; i < s.A.Length; i++)
                {
                    if (!s.A[i].Alive || s.A[i].Atk <= 0 || s.A[i].Swings <= 0) continue;
                    totalSwings += s.A[i].Swings;
                    if (s.A[i].Swings > maxSwings) maxSwings = s.A[i].Swings;
                    if (s.A[i].CanFace) p += s.A[i].Atk * s.A[i].Swings;
                }
                return p;
            }

            /// <summary>Optimistic extra damage still obtainable from unplayed cards (for pruning only).</summary>
            private static int Bound(S s, int playsLeft, int totalSwings, int maxSwings)
            {
                if (playsLeft <= 0) return 0;
                int b = 0;
                for (int i = 0; i < s.H.Length; i++)
                {
                    if (s.H[i].Used) continue;
                    switch (s.H[i].Fx)
                    {
                        case Fx.Blasphemy: b += 4; break;
                        case Fx.Tachyon: b += 6; break;
                        case Fx.Entropic: b += totalSwings; break;
                        case Fx.Floating: b += (2 * (s.H[i].Cost + 3)) * Math.Max(1, maxSwings); break;
                        case Fx.Vaultkeeper: b += 4 * Math.Max(1, maxSwings); break;
                    }
                }
                return b;
            }

            private bool TryPlay(S s, int i, int playsLeft)
            {
                var c = s.H[i];
                switch (c.Fx)
                {
                    case Fx.Blasphemy:
                    {
                        var n = Pay(s, i);
                        bool highest = true;
                        for (int k = 0; k < s.H.Length; k++)
                            if (k != i && !s.H[k].Used && s.H[k].Cost >= c.Cost) { highest = false; break; }
                        Aoe(n, 2);
                        if (highest) Aoe(n, 2);
                        return Recurse(n, playsLeft, new PlannedAction { Kind = ActionKind.Play, CardEntityId = c.Id });
                    }
                    case Fx.Tachyon:
                    {
                        // Random split: deterministic only when no enemy minion can absorb a point.
                        for (int j = 0; j < s.F.Length; j++) if (s.F[j].Alive) return false;
                        var n = Pay(s, i);
                        n.Eff -= 6; n.Dealt += 6;
                        return Recurse(n, playsLeft, new PlannedAction { Kind = ActionKind.Play, CardEntityId = c.Id });
                    }
                    case Fx.Entropic:
                    {
                        var n = Pay(s, i);
                        bool any = false;
                        for (int k = 1; k < n.A.Length; k++)
                            if (n.A[k].Alive) { n.A[k].Atk++; n.A[k].Hp++; any = true; }
                        if (!any) return false;
                        return Recurse(n, playsLeft, new PlannedAction { Kind = ActionKind.Play, CardEntityId = c.Id });
                    }
                    case Fx.Floating:
                    {
                        int x = 2 * c.Cost;
                        for (int k = 1; k < s.A.Length; k++)
                        {
                            if (!s.A[k].Alive) continue;
                            var n = Pay(s, i);
                            n.A[k].Atk += x; n.A[k].Hp += x;
                            if (Recurse(n, playsLeft, new PlannedAction { Kind = ActionKind.Play, CardEntityId = c.Id, TargetId = s.A[k].Id })) return true;
                            if (Aborted) return false;
                        }
                        return false;
                    }
                    case Fx.Vaultkeeper:
                    {
                        var alive = AliveMinions(s);
                        if (alive.Count >= MaxBoard) return false;
                        for (int p = 0; p <= alive.Count; p++)
                        {
                            var n = Pay(s, i);
                            var left = p - 1 >= 0 ? alive[p - 1] : -1;
                            var right = p < alive.Count ? alive[p] : -1;
                            if (left >= 0) { n.A[left].Atk += 2; n.A[left].Hp += 2; }
                            if (right >= 0) { n.A[right].Atk += 2; n.A[right].Hp += 2; }
                            n.A = InsertMinion(n.A, alive, p, new Att { Id = c.Id, Atk = c.Atk, Hp = c.Hp, Swings = 0, CanFace = true, Alive = true });
                            if (Recurse(n, playsLeft, new PlannedAction { Kind = ActionKind.Play, CardEntityId = c.Id, Slot = p })) return true;
                            if (Aborted) return false;
                        }
                        return false;
                    }
                    case Fx.RushShred:
                    {
                        var alive = AliveMinions(s);
                        if (alive.Count >= MaxBoard || HeroHp <= 3) return false;
                        var n = Pay(s, i);
                        n.A = InsertMinion(n.A, alive, alive.Count, new Att { Id = c.Id, Atk = c.Atk, Hp = c.Hp, Swings = 1, CanFace = false, Alive = true });
                        return Recurse(n, playsLeft, new PlannedAction { Kind = ActionKind.Play, CardEntityId = c.Id, Slot = alive.Count });
                    }
                }
                return false;
            }

            private bool Recurse(S n, int playsLeft, PlannedAction action)
            {
                _path.Add(action);
                if (Dfs(n, playsLeft - 1)) return true;
                _path.RemoveAt(_path.Count - 1);
                return false;
            }

            private static S Pay(S s, int i)
            {
                var n = s.Clone();
                n.Mana -= n.H[i].Cost;
                n.H[i].Used = true;
                return n;
            }

            private static List<int> AliveMinions(S s)
            {
                var l = new List<int>();
                for (int k = 1; k < s.A.Length; k++) if (s.A[k].Alive) l.Add(k);
                return l;
            }

            private static Att[] InsertMinion(Att[] a, List<int> alive, int slot, Att m)
            {
                int idx = slot == 0 ? (alive.Count > 0 ? alive[0] : a.Length) : alive[slot - 1] + 1;
                var r = new Att[a.Length + 1];
                Array.Copy(a, 0, r, 0, idx);
                r[idx] = m;
                Array.Copy(a, idx, r, idx + 1, a.Length - idx);
                return r;
            }

            /// <summary>Damage to the enemy hero and every enemy minion.</summary>
            private static void Aoe(S s, int dmg)
            {
                s.Eff -= dmg; s.Dealt += dmg;
                for (int j = 0; j < s.F.Length; j++)
                {
                    if (!s.F[j].Alive || s.F[j].Immune) continue;
                    if (s.F[j].Shield) { s.F[j].Shield = false; continue; }
                    s.F[j].Hp -= dmg;
                    if (s.F[j].Hp <= 0) { s.F[j].Alive = false; s.Eff += s.F[j].DrArmor; }
                }
            }

            /// <summary>Resolves one attack into a Taunt minion. Returns false if our hero would die.</summary>
            private static bool Attack(S s, int ai, int fj)
            {
                var att = s.A[ai];
                var tnt = s.F[fj];
                att.Swings--;
                if (tnt.Shield) tnt.Shield = false;
                else
                {
                    tnt.Hp -= att.Atk;
                    if (att.Poison && att.Atk > 0) tnt.Hp = 0;
                }
                if (tnt.Hp <= 0 && tnt.Alive) { tnt.Alive = false; s.Eff += tnt.DrArmor; }

                if (tnt.Atk > 0 && !att.Immune)
                {
                    if (att.Shield) att.Shield = false;
                    else
                    {
                        att.Hp -= tnt.Atk;
                        if (tnt.Poison && !att.Hero) att.Hp = 0;
                    }
                }
                bool ok = true;
                if (att.Hp <= 0)
                {
                    att.Alive = false;
                    if (att.Hero) ok = false;
                }
                s.A[ai] = att;
                s.F[fj] = tnt;
                return ok;
            }

            private static string Key(S s, int playsLeft)
            {
                var sb = new StringBuilder(96);
                sb.Append(playsLeft).Append('|').Append(s.Mana).Append('|').Append(s.Eff).Append('|');
                for (int i = 0; i < s.A.Length; i++)
                    sb.Append(s.A[i].Id).Append(s.A[i].Alive ? "" : "x").Append(',').Append(s.A[i].Atk).Append(',').Append(s.A[i].Hp)
                      .Append(s.A[i].Shield ? 'D' : '-').Append(s.A[i].Swings).Append(';');
                sb.Append('|');
                for (int j = 0; j < s.F.Length; j++)
                    sb.Append(s.F[j].Alive ? s.F[j].Hp : -1).Append(s.F[j].Shield ? 'D' : '-').Append(';');
                sb.Append('|');
                for (int k = 0; k < s.H.Length; k++)
                    sb.Append(s.H[k].Used ? 'u' : '.').Append(s.H[k].Cost).Append(';');
                return sb.ToString();
            }
        }
    }
}
