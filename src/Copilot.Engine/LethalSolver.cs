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
        /// <summary>A legal sequence exists, but unmodelled cards/secrets could break it.</summary>
        Likely = 2,
        /// <summary>A legal sequence exists and nothing unmodelled is in play.</summary>
        Proven = 3
    }

    public sealed class PlannedAttack
    {
        public int AttackerId { get; set; }
        public int TargetId { get; set; }
        public override string ToString() =>
            (AttackerId == LethalSolver.HeroAttacker ? "Hero" : "#" + AttackerId) + " -> " +
            (TargetId == LethalSolver.Face ? "Face" : "#" + TargetId);
    }

    public sealed class LethalResult
    {
        public LethalStatus Status { get; set; }
        public List<PlannedAttack> Actions { get; } = new List<PlannedAttack>();
        public int FaceDamage { get; set; }
        public int RequiredDamage { get; set; }
        public List<string> Reasons { get; } = new List<string>();
        public int NodesVisited { get; set; }
    }

    /// <summary>
    /// Exact attack-only lethal search (minion + hero attacks, Taunt, Divine Shield, Poisonous,
    /// Windfury, Immune, Stealth, armor). Spells, hero powers and triggered effects are NOT modelled;
    /// any unmodelled enemy/own card caps the result at Likely.
    /// </summary>
    public static class LethalSolver
    {
        public const int HeroAttacker = -1;
        public const int Face = -2;
        public const int DefaultNodeBudget = 200000;

        private struct Att
        {
            public int Id, Atk, Hp, Swings;
            public bool Shield, Poison, Immune, CanFace, Hero, Alive;
        }

        private struct Tnt
        {
            public int Id, Atk, Hp;
            public bool Shield, Poison, Immune, Alive;
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

            var atts = new List<Att>();
            if (me.HeroAttack > 0 && me.HeroAttacksRemaining > 0)
                atts.Add(new Att
                {
                    Id = HeroAttacker, Atk = me.HeroAttack, Hp = me.HeroHealth + me.Armor,
                    Swings = me.HeroAttacksRemaining, Immune = me.HeroImmune, CanFace = true, Hero = true, Alive = true
                });
            foreach (var m in me.Board)
            {
                if (m.AttacksRemaining <= 0 || m.Attack <= 0) continue;
                atts.Add(new Att
                {
                    Id = m.Id, Atk = m.Attack, Hp = m.Health, Swings = m.AttacksRemaining,
                    Shield = m.DivineShield, Poison = m.Poisonous, Immune = m.Immune,
                    CanFace = m.CanAttackHero, Alive = true
                });
                if (m.Support == Support.Unknown)
                    result.Reasons.Add("Own card with unmodelled effect: " + m);
            }

            var tnts = new List<Tnt>();
            foreach (var m in opp.Board)
            {
                if (m.Support == Support.Unknown)
                    result.Reasons.Add("Enemy card with unmodelled effect: " + m);
                // Stealthed Taunt minions do not enforce Taunt.
                if (m.Taunt && !m.Stealth)
                    tnts.Add(new Tnt
                    {
                        Id = m.Id, Atk = m.Attack, Hp = m.Health, Shield = m.DivineShield,
                        Poison = m.Poisonous, Immune = m.Immune, Alive = true
                    });
            }
            if (opp.Secrets > 0)
                result.Reasons.Add(opp.Secrets + " unknown enemy secret(s)");

            var ctx = new Ctx { Need = result.RequiredDamage, Budget = nodeBudget, Token = token };
            bool found = ctx.Dfs(atts.ToArray(), tnts.ToArray());
            result.NodesVisited = ctx.Nodes;

            if (found)
            {
                result.Actions.AddRange(ctx.Plan);
                result.FaceDamage = ctx.FaceDamage;
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

        private sealed class Ctx
        {
            public int Need, Budget, Nodes;
            public bool Aborted;
            public CancellationToken Token;
            public readonly List<PlannedAttack> Plan = new List<PlannedAttack>();
            public int FaceDamage;
            private readonly List<PlannedAttack> _path = new List<PlannedAttack>();
            private readonly HashSet<string> _failed = new HashSet<string>();

            public bool Dfs(Att[] a, Tnt[] t)
            {
                if (Token.IsCancellationRequested || ++Nodes > Budget) { Aborted = true; return false; }

                bool anyTaunt = false;
                for (int j = 0; j < t.Length; j++) if (t[j].Alive) { anyTaunt = true; break; }

                int potential = 0;
                for (int i = 0; i < a.Length; i++)
                    if (a[i].Alive && a[i].CanFace) potential += a[i].Atk * a[i].Swings;

                if (!anyTaunt)
                {
                    if (potential < Need) return false;
                    Plan.AddRange(_path);
                    for (int i = 0; i < a.Length; i++)
                        if (a[i].Alive && a[i].CanFace)
                            for (int k = 0; k < a[i].Swings; k++)
                                Plan.Add(new PlannedAttack { AttackerId = a[i].Id, TargetId = Face });
                    FaceDamage = potential;
                    return true;
                }

                if (potential < Need) return false;
                var key = Key(a, t);
                if (_failed.Contains(key)) return false;

                for (int i = 0; i < a.Length; i++)
                {
                    if (!a[i].Alive || a[i].Swings <= 0 || a[i].Atk <= 0) continue;
                    for (int j = 0; j < t.Length; j++)
                    {
                        if (!t[j].Alive || t[j].Immune) continue;
                        var a2 = (Att[])a.Clone();
                        var t2 = (Tnt[])t.Clone();
                        if (!Apply(ref a2[i], ref t2[j])) continue;
                        _path.Add(new PlannedAttack { AttackerId = a[i].Id, TargetId = t[j].Id });
                        if (Dfs(a2, t2)) return true;
                        _path.RemoveAt(_path.Count - 1);
                        if (Aborted) return false;
                    }
                }
                _failed.Add(key);
                return false;
            }

            /// <summary>Resolves one attack into a Taunt minion. Returns false if our hero would die.</summary>
            private static bool Apply(ref Att att, ref Tnt tnt)
            {
                att.Swings--;
                if (tnt.Shield) tnt.Shield = false;
                else
                {
                    tnt.Hp -= att.Atk;
                    if (att.Poison && att.Atk > 0) tnt.Hp = 0;
                }
                if (tnt.Hp <= 0) tnt.Alive = false;

                if (tnt.Atk > 0 && !att.Immune)
                {
                    if (att.Shield) att.Shield = false;
                    else
                    {
                        att.Hp -= tnt.Atk;
                        if (tnt.Poison && !att.Hero) att.Hp = 0;
                    }
                }
                if (att.Hp <= 0)
                {
                    att.Alive = false;
                    if (att.Hero) return false;
                }
                return true;
            }

            private static string Key(Att[] a, Tnt[] t)
            {
                var sb = new StringBuilder(64);
                for (int i = 0; i < a.Length; i++)
                    sb.Append(a[i].Alive ? a[i].Hp : -1).Append(a[i].Shield ? 'D' : '-').Append(a[i].Swings).Append(';');
                sb.Append('|');
                for (int j = 0; j < t.Length; j++)
                    sb.Append(t[j].Alive ? t[j].Hp : -1).Append(t[j].Shield ? 'D' : '-').Append(';');
                return sb.ToString();
            }
        }
    }
}
