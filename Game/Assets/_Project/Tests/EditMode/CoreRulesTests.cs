using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UZS.Core;

namespace UZS.Tests
{
    public class WeaponAndLoadoutTests
    {
        [Test]
        public void SixDistinctWeaponTypesExist()
        {
            var ids = WeaponCatalog.All.Select(w => w.Id).Distinct().ToList();
            Assert.AreEqual(6, ids.Count);
            Assert.IsFalse(ids.Contains(WeaponId.None));
        }

        [Test]
        public void StarterLoadoutIsPistolWithFullAmmo()
        {
            var l = Loadout.CreateStarter();
            Assert.AreEqual(WeaponId.Pistol, l.Active.Weapon);
            Assert.AreEqual(WeaponCatalog.Get(WeaponId.Pistol).MagazineSize, l.Active.Magazine);
            Assert.AreEqual(WeaponCatalog.Get(WeaponId.Pistol).MaxReserve, l.Active.Reserve);
            Assert.IsTrue(l[1].IsEmpty);
        }

        [Test]
        public void GiveFillsEmptySlotThenReplacesActive()
        {
            var l = Loadout.CreateStarter();
            Assert.AreEqual(WeaponId.None, l.Give(WeaponId.Smg, false));
            Assert.AreEqual(1, l.ActiveSlot);
            Assert.AreEqual(WeaponId.Smg, l.Give(WeaponId.Shotgun, false));
            Assert.AreEqual(WeaponId.Shotgun, l[1].Weapon);
            Assert.AreEqual(WeaponId.Pistol, l[0].Weapon);
        }

        [Test]
        public void ConsumeAndReloadMovesReserveIntoMagazine()
        {
            var l = Loadout.CreateStarter();
            int mag = l.Active.Magazine, reserve = l.Active.Reserve;
            Assert.IsTrue(l.TryConsumeRound(0));
            Assert.IsTrue(l.TryConsumeRound(0));
            Assert.AreEqual(mag - 2, l.Active.Magazine);
            Assert.AreEqual(2, l.CompleteReload(0));
            Assert.AreEqual(mag, l.Active.Magazine);
            Assert.AreEqual(reserve - 2, l.Active.Reserve);
            Assert.IsFalse(l.CanReload(0));
        }

        [Test]
        public void CannotFireEmptyMagazine()
        {
            var l = Loadout.CreateStarter();
            int mag = l.Active.Magazine;
            for (int i = 0; i < mag; i++) Assert.IsTrue(l.TryConsumeRound(0));
            Assert.IsFalse(l.TryConsumeRound(0));
            Assert.IsFalse(l.TryConsumeRound(1), "Empty slot can never fire");
        }

        [Test]
        public void UpgradeDoublesDamageAndCannotRepeat()
        {
            var l = Loadout.CreateStarter();
            Assert.IsTrue(l.Upgrade(0));
            Assert.IsFalse(l.Upgrade(0));
            float normal = WeaponCatalog.DamageAt(WeaponId.Pistol, false, 1f, false);
            float upgraded = WeaponCatalog.DamageAt(WeaponId.Pistol, true, 1f, false);
            Assert.AreEqual(normal * 2f, upgraded, 0.001f);
            Assert.Greater(l.Active.Magazine, WeaponCatalog.Get(WeaponId.Pistol).MagazineSize);
        }

        [Test]
        public void DamageFallsOffAndIsZeroBeyondRange()
        {
            var s = WeaponCatalog.Get(WeaponId.Shotgun);
            Assert.Greater(WeaponCatalog.DamageAt(s.Id, false, 1f, false), WeaponCatalog.DamageAt(s.Id, false, s.Range * 0.9f, false));
            Assert.AreEqual(0f, WeaponCatalog.DamageAt(s.Id, false, s.Range + 1f, false));
            Assert.Greater(WeaponCatalog.DamageAt(s.Id, false, 5f, true), WeaponCatalog.DamageAt(s.Id, false, 5f, false));
        }

        [Test]
        public void RestoreClampsTamperedAmmo()
        {
            var l = new Loadout();
            l.Restore(new WeaponSlot { Weapon = WeaponId.Smg, Magazine = 9999, Reserve = -5 }, default, 1);
            Assert.AreEqual(0, l.ActiveSlot, "Active slot falls back to a non-empty slot");
            Assert.AreEqual(WeaponCatalog.Get(WeaponId.Smg).MagazineSize, l[0].Magazine);
            Assert.AreEqual(0, l[0].Reserve);
        }
    }

    public class PurchaseTests
    {
        [Test]
        public void WallBuyChargesFullPriceThenAmmoPrice()
        {
            var l = Loadout.CreateStarter();
            var q = PurchaseRules.WallBuy(l, WeaponId.Smg);
            Assert.AreEqual(QuoteKind.NewWeapon, q.Kind);
            Assert.AreEqual(1000, q.Cost);

            l.Give(WeaponId.Smg, false);
            Assert.IsFalse(PurchaseRules.WallBuy(l, WeaponId.Smg).Available, "Full ammo cannot be bought");
            l.TryConsumeRound(1);
            q = PurchaseRules.WallBuy(l, WeaponId.Smg);
            Assert.AreEqual(QuoteKind.Ammo, q.Kind);
            Assert.AreEqual(500, q.Cost);
        }

        [Test]
        public void StarterAndBoxOnlyWeaponsAreNotWallBuyable()
        {
            var l = Loadout.CreateStarter();
            Assert.IsFalse(PurchaseRules.WallBuy(l, WeaponId.Pistol).Kind == QuoteKind.NewWeapon);
            Assert.IsFalse(PurchaseRules.WallBuy(l, WeaponId.PlasmaRifle).Available);
        }

        [Test]
        public void DoorCannotBeBoughtTwice()
        {
            var q = PurchaseRules.Door(0, 0UL);
            Assert.IsTrue(q.Available);
            Assert.AreEqual(MapCatalog.Doors[0].Cost, q.Cost);
            Assert.IsFalse(PurchaseRules.Door(0, 1UL).Available);
            Assert.IsFalse(PurchaseRules.Door(99, 0UL).Available);
        }

        [Test]
        public void AffordabilityIsStrict()
        {
            var q = PurchaseQuote.Of(QuoteKind.Perk, 2000);
            Assert.IsFalse(q.Affordable(1999));
            Assert.IsTrue(q.Affordable(2000));
            Assert.IsFalse(PurchaseQuote.No("x").Affordable(int.MaxValue));
        }

        [Test]
        public void PerksAreOneTimePurchases()
        {
            Assert.IsTrue(PurchaseRules.Perk(PerkId.None, PerkId.IronHide).Available);
            Assert.IsFalse(PurchaseRules.Perk(PerkId.IronHide, PerkId.IronHide).Available);
            Assert.AreEqual(250f, HealthRules.MaxHealth(PerkId.IronHide));
        }
    }

    public class MapTests
    {
        [Test]
        public void OnlyStartZoneActiveInitially()
        {
            var active = MapCatalog.ActiveZones(0UL);
            Assert.IsTrue(active[MapCatalog.StartZone]);
            Assert.AreEqual(1, active.Count(a => a));
        }

        [Test]
        public void AllZonesReachableWithAllDoorsOpen()
        {
            ulong all = 0;
            foreach (var d in MapCatalog.Doors) all |= 1UL << d.Id;
            Assert.IsTrue(MapCatalog.ActiveZones(all).All(a => a));
        }

        [Test]
        public void HangarRequiresLabDoor()
        {
            ulong allButHangar = (1UL << 0) | (1UL << 1) | (1UL << 2) | (1UL << 3);
            var active = MapCatalog.ActiveZones(allButHangar);
            Assert.IsFalse(active[4]);
            Assert.IsTrue(active[3]);
        }

        [Test]
        public void DoorIdsMatchIndicesAndInteractableIdsAreUnique()
        {
            for (int i = 0; i < MapCatalog.Doors.Length; i++) Assert.AreEqual(i, MapCatalog.Doors[i].Id);
            var ids = new HashSet<int>(MapCatalog.Doors.Select(d => d.Id));
            foreach (var s in MapCatalog.Stations) Assert.IsTrue(ids.Add(s.Id), "Duplicate interactable id " + s.Id);
            Assert.IsFalse(ids.Contains(MapCatalog.MysteryBoxInteractableId), "Mystery box id must not collide");
        }

        [Test]
        public void StartZoneHasAWallBuyAndBoxLocation()
        {
            Assert.IsTrue(MapCatalog.Stations.Any(s => s.Zone == MapCatalog.StartZone && s.Kind == InteractableKind.WallBuy));
            Assert.IsTrue(MapCatalog.BoxLocations.Any(s => s.Zone == MapCatalog.StartZone));
        }

        [Test]
        public void EveryWallBuyWeaponIsSoldSomewhere()
        {
            var sold = MapCatalog.Stations.Where(s => s.Kind == InteractableKind.WallBuy).Select(s => (WeaponId)s.Param).ToList();
            foreach (var w in WeaponCatalog.All.Where(w => w.WallPrice > 0)) CollectionAssert.Contains(sold, w.Id);
        }

        [Test]
        public void EveryPerkHasAMachineAndEveryZoneHasSpawns()
        {
            var perks = MapCatalog.Stations.Where(s => s.Kind == InteractableKind.Perk).Select(s => (PerkId)s.Param).ToList();
            CollectionAssert.AreEquivalent(PerkCatalog.All, perks);
            foreach (var z in MapCatalog.Zones) Assert.Greater(z.ZombieSpawns.Length, 0);
        }

        [Test]
        public void ZoneLookupPlacesStationsInTheirZones()
        {
            foreach (var s in MapCatalog.Stations)
                Assert.AreEqual(s.Zone, MapCatalog.ZoneAt(s.Position.X, s.Position.Y, s.Position.Z), "Station " + s.Id);
            foreach (var z in MapCatalog.Zones)
            foreach (var p in z.ZombieSpawns)
                Assert.AreEqual(z.Id, MapCatalog.ZoneAt(p.X, p.Y, p.Z), "Spawn " + p);
        }

        [Test]
        public void EscapeConsoleIsInHangar()
        {
            var console = MapCatalog.FindStation(MapCatalog.EscapeConsoleInteractableId);
            Assert.NotNull(console);
            Assert.AreEqual(InteractableKind.EscapeConsole, console.Kind);
            Assert.AreEqual(4, console.Zone);
        }
    }

    public class RoundTests
    {
        [Test]
        public void ZombieCountGrowsWithRoundAndPlayers()
        {
            int prev = 0;
            for (int r = 1; r <= 60; r++)
            {
                int n = RoundRules.ZombiesForRound(r, 1);
                Assert.GreaterOrEqual(n, prev);
                prev = n;
            }
            Assert.Greater(RoundRules.ZombiesForRound(5, 4), RoundRules.ZombiesForRound(5, 1));
            Assert.LessOrEqual(RoundRules.ZombiesForRound(1000, 4), RoundRules.MaxZombiesPerRound);
        }

        [Test]
        public void HealthIsMonotonicAndCappedForEndlessPlay()
        {
            float prev = 0;
            for (int r = 1; r <= 200; r++)
            {
                float hp = RoundRules.ZombieHealth(r);
                Assert.GreaterOrEqual(hp, prev);
                Assert.IsFalse(float.IsInfinity(hp) || float.IsNaN(hp));
                prev = hp;
            }
            Assert.AreEqual(RoundRules.ZombieHealth(RoundRules.HealthCapRound), RoundRules.ZombieHealth(500));
        }

        [Test]
        public void EarlyRoundsOnlyWalk()
        {
            for (double roll = 0; roll < 1; roll += 0.05)
                Assert.AreEqual(ZombieSpeed.Walk, RoundRules.RollSpeed(1, roll));
            Assert.AreEqual(ZombieSpeed.Sprint, RoundRules.RollSpeed(30, 0.0));
        }

        [Test]
        public void SpawnIntervalHasFloor()
        {
            Assert.AreEqual(0.35f, RoundRules.SpawnIntervalSeconds(500), 0.0001f);
        }
    }

    public class BoxEscapeAndSessionTests
    {
        [Test]
        public void BoxNeverOffersOwnedWeapon()
        {
            var rng = new Random(1234);
            var l = Loadout.CreateStarter();
            l.Give(WeaponId.Smg, false);
            for (int i = 0; i < 500; i++)
            {
                var w = MysteryBoxRules.Roll(rng, l, 0);
                Assert.AreNotEqual(WeaponId.Smg, w);
                Assert.AreNotEqual(WeaponId.None, w, "No relocation before minimum uses");
                Assert.IsTrue(WeaponCatalog.Get(w).InMysteryBox);
            }
        }

        [Test]
        public void BoxEventuallyRelocatesAndPicksDifferentLocation()
        {
            var rng = new Random(7);
            var l = Loadout.CreateStarter();
            bool relocated = false;
            for (int i = 0; i < 200 && !relocated; i++)
                relocated = MysteryBoxRules.Roll(rng, l, MysteryBoxRules.MinUsesBeforeMove) == WeaponId.None;
            Assert.IsTrue(relocated);
            for (int i = 0; i < 100; i++)
                Assert.AreNotEqual(1, MysteryBoxRules.NextLocation(rng, 1, 3));
        }

        [Test]
        public void EscapeContributionNeverExceedsRemainingOrPoints()
        {
            int cost = EscapeRules.CostFor(2);
            Assert.AreEqual(18000, cost);
            Assert.AreEqual(500, EscapeRules.Contribution(500, 0, cost));
            Assert.AreEqual(1000, EscapeRules.Contribution(5000, cost - 1000, cost));
            Assert.AreEqual(0, EscapeRules.Contribution(5000, cost, cost));
            Assert.AreEqual(0, EscapeRules.Contribution(-10, 0, cost));
        }

        [Test]
        public void TenOriginalCharacters()
        {
            Assert.AreEqual(10, CharacterCatalog.Count);
            Assert.AreEqual(10, CharacterCatalog.All.Select(c => c.Name).Distinct().Count());
            for (int i = 0; i < CharacterCatalog.Count; i++) Assert.AreEqual(i, CharacterCatalog.All[i].Id);
        }

        [Test]
        public void PayloadRoundTripsAndSanitizes()
        {
            var p = new ConnectionPayload { PlayerKey = "abc-123", DisplayName = "Zed<script>\n", CharacterId = 42 };
            Assert.IsTrue(ConnectionPayload.TryDecode(p.Encode(), out var d));
            Assert.AreEqual("abc-123", d.PlayerKey);
            Assert.AreEqual("Zedscript", d.DisplayName);
            Assert.AreEqual(CharacterCatalog.Count - 1, d.CharacterId);
            Assert.AreEqual(GameInfo.ProtocolVersion, d.Version);
        }

        [Test]
        public void PayloadRejectsGarbage()
        {
            Assert.IsFalse(ConnectionPayload.TryDecode(null, out _));
            Assert.IsFalse(ConnectionPayload.TryDecode(new byte[] { 1, 2, 3 }, out _));
            Assert.IsFalse(ConnectionPayload.TryDecode(new byte[2048], out _));
        }

        [Test]
        public void ApprovalRules()
        {
            var ok = new ConnectionPayload { PlayerKey = "k" };
            Assert.AreEqual(ApprovalResult.Approved, UZS.Core.ApprovalRules.Evaluate(ok, true, true, true, 0, MatchPhase.Lobby));
            Assert.AreEqual(ApprovalResult.SinglePlayerOnly, UZS.Core.ApprovalRules.Evaluate(ok, true, false, true, 1, MatchPhase.Lobby));
            Assert.AreEqual(ApprovalResult.SessionFull, UZS.Core.ApprovalRules.Evaluate(ok, true, false, false, 4, MatchPhase.Playing));
            Assert.AreEqual(ApprovalResult.MatchOver, UZS.Core.ApprovalRules.Evaluate(ok, true, false, false, 2, MatchPhase.Victory));
            var old = new ConnectionPayload { PlayerKey = "k", Version = "0.0.1" };
            Assert.AreEqual(ApprovalResult.VersionMismatch, UZS.Core.ApprovalRules.Evaluate(old, true, false, false, 1, MatchPhase.Lobby));
            Assert.AreEqual(ApprovalResult.BadPayload, UZS.Core.ApprovalRules.Evaluate(null, false, false, false, 1, MatchPhase.Lobby));
        }

        [Test]
        public void ReconnectSnapshotIsConsumedOnce()
        {
            var ledger = new ReconnectLedger();
            string key = ledger.Claim("player");
            ledger.Release(key, new PlayerSnapshot { Points = 4200 });
            Assert.IsTrue(ledger.TryConsume("player", out var snap));
            Assert.AreEqual(4200, snap.Points);
            Assert.IsFalse(ledger.TryConsume("player", out _), "Second reconnect must not duplicate progress");
        }

        [Test]
        public void DuplicateKeysGetUniqueSuffix()
        {
            var ledger = new ReconnectLedger();
            Assert.AreEqual("same", ledger.Claim("same"));
            Assert.AreEqual("same#2", ledger.Claim("same"));
        }

        [Test]
        public void FireRateLimiterAllowsSmallBurstButCapsSustainedRate()
        {
            var limiter = new FireRateLimiter();
            float interval = 0.1f;
            int accepted = 0;
            // 3 shots arriving in the same frame (network batching) are fine.
            for (int i = 0; i < 3; i++) if (limiter.TryFire(1.0, interval)) accepted++;
            Assert.AreEqual(3, accepted);
            Assert.IsFalse(limiter.TryFire(1.0, interval), "Fourth simultaneous shot rejected");

            // A cheater sending 100 shots per second for 2 seconds gets roughly the weapon's rate.
            limiter.Reset();
            accepted = 0;
            for (int i = 0; i < 200; i++) if (limiter.TryFire(10.0 + i * 0.01, interval)) accepted++;
            Assert.LessOrEqual(accepted, 2.0 / (interval * UZS.Core.ShotValidation.FireIntervalTolerance) + FireRateLimiter.BurstShots + 1);
            Assert.GreaterOrEqual(accepted, 18);
        }

        [Test]
        public void ShotValidation()
        {
            Assert.IsTrue(UZS.Core.ShotValidation.OriginPlausible(1f));
            Assert.IsFalse(UZS.Core.ShotValidation.OriginPlausible(10f));
            float interval = WeaponCatalog.Get(WeaponId.Pistol).SecondsBetweenShots;
            Assert.IsFalse(UZS.Core.ShotValidation.FireIntervalOk(10.0, 10.0 - interval * 0.5, interval));
            Assert.IsTrue(UZS.Core.ShotValidation.FireIntervalOk(10.0, 10.0 - interval, interval));
        }
    }
}
