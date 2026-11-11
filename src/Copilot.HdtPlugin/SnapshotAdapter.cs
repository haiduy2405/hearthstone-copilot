using System;
using System.Collections.Generic;
using System.Linq;
using Copilot.Engine;
using Copilot.Engine.Model;
using HearthDb.Enums;
using Hearthstone_Deck_Tracker.Hearthstone;
using Hearthstone_Deck_Tracker.Hearthstone.Entities;

namespace Copilot.HdtPlugin
{
    /// <summary>
    /// Converts HDT's live entity table into an engine snapshot. Everything derived here from raw
    /// tags (attack readiness, Rush, Windfury) is an ASSUMPTION until validated against recorded
    /// games; the raw tags are dumped next to the snapshot for exactly that purpose.
    /// Must be called on the thread that owns HDT's game state (HDT's UI thread via OnUpdate).
    /// </summary>
    internal static class SnapshotAdapter
    {
        public static GameSnapshot Build(GameV2 game, string hdtVersion, string gameId)
        {
            var pe = game.PlayerEntity;
            var oe = game.OpponentEntity;
            if (pe == null || oe == null || game.Entities.Count == 0) return null;

            var snap = new GameSnapshot
            {
                GameId = gameId,
                Turn = game.GetTurnNumber(),
                MyTurn = Flag(pe, GameTag.CURRENT_PLAYER),
                Format = game.CurrentFormatType.ToString(),
                HdtVersion = hdtVersion,
                Timestamp = DateTime.UtcNow
            };
            snap.Me = BuildSide(game, pe, snap.MyTurn, includeRaw: snap.Raw);
            snap.Opp = BuildSide(game, oe, false, includeRaw: snap.Raw);
            snap.Opp.Secrets = game.OpponentSecretCount;
            return snap;
        }

        private static SideState BuildSide(GameV2 game, Entity playerEntity, bool isActive, List<RawEntity> includeRaw)
        {
            int pid = playerEntity.GetTag(GameTag.PLAYER_ID);
            var side = new SideState
            {
                Mana = playerEntity.GetTag(GameTag.RESOURCES) + playerEntity.GetTag(GameTag.TEMP_RESOURCES)
                       - playerEntity.GetTag(GameTag.RESOURCES_USED),
                HandCount = game.Entities.Values.Count(e => e.IsInHand && e.IsControlledBy(pid))
            };

            if (game.Entities.TryGetValue(playerEntity.GetTag(GameTag.HERO_ENTITY), out var hero))
            {
                side.HeroEntityId = hero.Id;
                side.HeroHealth = hero.GetTag(GameTag.HEALTH) - hero.GetTag(GameTag.DAMAGE);
                side.Armor = hero.GetTag(GameTag.ARMOR);
                side.HeroImmune = Flag(hero, GameTag.IMMUNE);
                side.HeroAttack = hero.GetTag(GameTag.ATK);
                bool blocked = Flag(hero, GameTag.FROZEN) || Flag(hero, GameTag.CANT_ATTACK);
                int max = Flag(hero, GameTag.WINDFURY) ? 2 : 1;
                side.HeroAttacksRemaining = isActive && !blocked
                    ? Math.Max(0, max - hero.GetTag(GameTag.NUM_ATTACKS_THIS_TURN)) : 0;
                includeRaw.Add(Raw(hero));
            }

            foreach (var e in game.Entities.Values
                         .Where(x => x.IsInPlay && x.IsMinion && x.IsControlledBy(pid))
                         .OrderBy(x => x.ZonePosition))
            {
                var m = BuildMinion(e, isActive);
                side.Board.Add(m);
                includeRaw.Add(Raw(e));
            }
            return side;
        }

        private static MinionState BuildMinion(Entity e, bool isActive)
        {
            bool frozen = Flag(e, GameTag.FROZEN);
            bool cantAttack = Flag(e, GameTag.CANT_ATTACK);
            bool charge = Flag(e, GameTag.CHARGE);
            bool rush = Flag(e, GameTag.RUSH);
            int turnsInPlay = e.GetTag(GameTag.NUM_TURNS_IN_PLAY);
            int max = Flag(e, GameTag.MEGA_WINDFURY) ? 4 : Flag(e, GameTag.WINDFURY) ? 2 : 1;
            int used = e.GetTag(GameTag.NUM_ATTACKS_THIS_TURN);
            bool ready = turnsInPlay > 0 || charge || rush;
            int attacks = isActive && ready && !frozen && !cantAttack ? Math.Max(0, max - used) : 0;

            string text = null;
            try { text = e.Card?.Text; } catch { /* card db miss -> Unknown below */ }
            var support = e.HasCardId ? TextSupport.Classify(text) : Support.Unknown;
            if (e.HasCardId && text == null) support = Support.Unknown;

            return new MinionState
            {
                Id = e.Id,
                CardId = e.CardId,
                Name = e.Card?.Name ?? e.CardId,
                Attack = e.GetTag(GameTag.ATK),
                Health = e.GetTag(GameTag.HEALTH) - e.GetTag(GameTag.DAMAGE),
                Taunt = Flag(e, GameTag.TAUNT),
                DivineShield = Flag(e, GameTag.DIVINE_SHIELD),
                Poisonous = Flag(e, GameTag.POISONOUS),
                Stealth = Flag(e, GameTag.STEALTH),
                Immune = Flag(e, GameTag.IMMUNE),
                AttacksRemaining = attacks,
                CanAttackHero = !(rush && !charge && turnsInPlay == 0),
                Support = support
            };
        }

        private static bool Flag(Entity e, GameTag tag) => e.GetTag(tag) > 0;

        private static RawEntity Raw(Entity e)
        {
            var r = new RawEntity { Id = e.Id, CardId = e.CardId, Name = e.Card?.Name };
            foreach (var kv in e.Tags) r.Tags[kv.Key.ToString()] = kv.Value;
            return r;
        }
    }
}
