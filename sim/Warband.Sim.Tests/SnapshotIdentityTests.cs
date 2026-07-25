using System.Collections.Generic;
using System.IO;
using Warband.Sim;
using Xunit;

namespace Warband.Sim.Tests
{
    /// <summary>
    /// The snapshot's stat/identity block (replay v3). Deployment has to answer "how far does
    /// this reach, how fast does it close, what is it holding" BEFORE a fight exists, and the
    /// only unit model the client gets is PlaybackUnit — so the composer must fill it, the
    /// projection must carry it, and the wire format must survive a round-trip with it.
    /// </summary>
    public class SnapshotIdentityTests
    {
        private static ChassisDef Ranger() => new ChassisDef
        {
            Id = "ranger", Name = "Ranger", MaxHp = 100, ManaMax = 20, MoveInterval = 4,
            StarterWeapon = new WeaponDef { Name = "Shortbow", Damage = 9, Interval = 10, Range = 4 },
        };

        [Fact]
        public void ComposeStampsChassisWeaponAndTier()
        {
            var relicBow = new WeaponDef { Name = "Longbow", Damage = 12, Interval = 11, Range = 5 };
            var def = Loadout.Compose(Ranger(), relicBow, tier: WeaponTier.Relic).Def;

            Assert.Equal("ranger", def.ChassisId);
            Assert.Equal("Longbow", def.WeaponName);
            Assert.Equal(WeaponTier.Relic, def.WeaponTier);
        }

        [Fact]
        public void TraitsListNodesAndTrinketsInMergeOrder()
        {
            var charm = new TrinketDef { Name = "Vitality Charm", HpBonus = 30 };
            var nodeA = new SpecNode { Name = "Sniper" };
            var nodeB = new SpecNode { Name = "Volley" };

            var def = Loadout.Compose(Ranger(), null, new[] { charm }, new[] { nodeA, nodeB }).Def;

            // Merge order is the documented composition order (ADR 0005) and it is meaningful —
            // a later node's signature override wins, so the renderer must show it in this order.
            Assert.Equal(new[] { "Vitality Charm", "Sniper", "Volley" }, def.Traits);
        }

        [Fact]
        public void ViewCarriesTheBaseStatBlockIncludingMasteryReach()
        {
            // The bow's mastery rider is +1 range. Placement must show the reach the unit
            // ACTUALLY has, not the reach printed on the weapon.
            var bow = new WeaponDef
            {
                Name = "Longbow", Category = "bow", Damage = 12, Interval = 11,
                Range = 4, CritChance = 15, MasteryRangeBonus = 1,
            };
            var composed = Loadout.Compose(Ranger(), bow, mastered: true);
            var view = PlaybackUnit.From(Loadout.Spawn(0, 0, composed, Hex.FromRowCol(0, 0)));

            Assert.Equal(5, view.Range);          // 4 + mastery
            Assert.Equal(12, view.Attack);
            Assert.Equal(11, view.AttackInterval);
            Assert.Equal(4, view.MoveInterval);
            Assert.Equal(15, view.CritChance);
            Assert.False(view.HealAutos);
            Assert.Equal("ranger", view.ChassisId);
            Assert.Equal("Longbow", view.WeaponName);
        }

        [Fact]
        public void CenserHealAutosIsVisibleBeforeTheFight()
        {
            // The censer law inverts what an "attack" means. A player deploying this unit has to
            // know that from the snapshot alone, or its placement reads as a broken damage dealer.
            var censer = new WeaponDef { Name = "Censer", Damage = 6, Interval = 12, Range = 2, HealAutos = true };
            var composed = Loadout.Compose(Ranger(), censer);
            var view = PlaybackUnit.From(Loadout.Spawn(0, 0, composed, Hex.FromRowCol(0, 0)));

            Assert.True(view.HealAutos);
        }

        [Fact]
        public void ReplayRoundTripPreservesIdentityAndViewHash()
        {
            var result = new Battle(BattleTests.Duel(BattleTests.Grunt(), BattleTests.Grunt(90))).Run();
            // Give the snapshot a full identity block so the round-trip has something to lose.
            result.InitialUnits[0].ChassisId = "bulwark";
            result.InitialUnits[0].WeaponName = "Tower Shield";
            result.InitialUnits[0].WeaponTier = WeaponTier.Honed;
            result.InitialUnits[0].Traits.Add("Warden");
            result.InitialUnits[0].Range = 1;
            result.InitialUnits[0].MoveInterval = 6;

            var ms = new MemoryStream();
            Replay.Write(ms, result.InitialUnits, result.Events);
            var (initial, events) = Replay.Read(new MemoryStream(ms.ToArray()));

            Assert.Equal("bulwark", initial[0].ChassisId);
            Assert.Equal("Tower Shield", initial[0].WeaponName);
            Assert.Equal(WeaponTier.Honed, initial[0].WeaponTier);
            Assert.Equal(new[] { "Warden" }, initial[0].Traits);
            Assert.Equal(1, initial[0].Range);
            Assert.Equal(6, initial[0].MoveInterval);
            Assert.Equal(result.Events.Count, events.Count);

            // The end-state hash is the standing proof used by `make scenarios`.
            var live = PlaybackState.From(result.InitialUnits);
            var round = PlaybackState.From(initial);
            live.AdvanceToTick(result.Events, int.MaxValue);
            round.AdvanceToTick(events, int.MaxValue);
            Assert.Equal(live.ViewHash(), round.ViewHash());
        }

        [Fact]
        public void ViewHashNoticesIdentityDrift()
        {
            // Without this, hashing identity would be decoration: the round-trip check above
            // only has teeth if a dropped field actually moves the hash.
            var baseline = new List<PlaybackUnit>
            {
                new PlaybackUnit { Id = 0, Name = "Bulwark", ChassisId = "bulwark", WeaponName = "Tower Shield", Range = 1 },
            };
            var fields = new List<PlaybackField>();
            ulong h = PlaybackState.HashView(baseline, fields);

            void AssertDiffers(string because, PlaybackUnit changed) =>
                Assert.True(h != PlaybackState.HashView(new List<PlaybackUnit> { changed }, fields), because);

            AssertDiffers("range must be hashed", new PlaybackUnit
            { Id = 0, Name = "Bulwark", ChassisId = "bulwark", WeaponName = "Tower Shield", Range = 2 });
            AssertDiffers("chassis id must be hashed", new PlaybackUnit
            { Id = 0, Name = "Bulwark", ChassisId = "phalanx", WeaponName = "Tower Shield", Range = 1 });
            AssertDiffers("weapon must be hashed", new PlaybackUnit
            { Id = 0, Name = "Bulwark", ChassisId = "bulwark", WeaponName = "Longbow", Range = 1 });
            AssertDiffers("traits must be hashed", new PlaybackUnit
            { Id = 0, Name = "Bulwark", ChassisId = "bulwark", WeaponName = "Tower Shield", Range = 1, Traits = { "Warden" } });
        }

        [Fact]
        public void TraitOrderAndBoundariesAreDistinguished()
        {
            // FNV over concatenated strings would collide on ("ab","c") vs ("a","bc"), and trait
            // order encodes override precedence — both must move the hash.
            var fields = new List<PlaybackField>();
            ulong Hash(params string[] traits) => PlaybackState.HashView(
                new List<PlaybackUnit> { new PlaybackUnit { Id = 0, Traits = new List<string>(traits) } }, fields);

            Assert.NotEqual(Hash("ab", "c"), Hash("a", "bc"));
            Assert.NotEqual(Hash("Sniper", "Volley"), Hash("Volley", "Sniper"));
        }
    }
}
