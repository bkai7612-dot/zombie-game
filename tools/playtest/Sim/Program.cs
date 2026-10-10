// Playtest balance harness. Everything numeric comes from the real UZS.Core rules (compiled from
// Game/Assets/_Project/Scripts/Core); only player behaviour (hit rate, headshots, range, purchase plan) is assumed.
// Run: dotnet run -c Release --project tools/playtest/Sim
using System;
using System.Collections.Generic;
using System.Linq;
using UZS.Core;

internal static class Program
{
    private const double HitRate = 0.60;   // fraction of trigger pulls that land on the zombie
    private const double HeadRate = 0.30;  // fraction of landed hits that are headshots (=> ~30% headshot kills)
    private const float MinRange = 5f, MaxRange = 15f;
    private static readonly int[] KeyRounds = { 1, 5, 10, 15, 20, 30, 40, 55 };
    private static readonly WeaponId[] Guns = { WeaponId.Pistol, WeaponId.Smg, WeaponId.Shotgun, WeaponId.AssaultRifle, WeaponId.MarksmanRifle, WeaponId.PlasmaRifle };

    private static void Main(string[] args)
    {
        Rounds();
        StkTable();
        PerZombieEconomy();
        Difficulty();
        Economy(1, crateRefillsUpgraded: true);
        Economy(4, crateRefillsUpgraded: true);
        Economy(1, crateRefillsUpgraded: false);
        StarterArea();
        Box();
        Spawns();
    }

    private static string Name(WeaponId w, bool up) => up ? WeaponCatalog.NameOf(w, true) + "*" : WeaponCatalog.Get(w).DisplayName.Split(' ')[0];

    // ------------------------------------------------------------------ rounds
    private static void Rounds()
    {
        Console.WriteLine("== Round table (RoundRules)");
        Console.WriteLine("round  hp       zSolo z4p  spawnInt  soloHP-total   maxAlive1/4  dmg");
        foreach (int r in new[] { 1, 2, 3, 4, 5, 8, 10, 12, 15, 20, 25, 30, 40, 55, 70 })
            Console.WriteLine($"{r,5}  {RoundRules.ZombieHealth(r),7:0}  {RoundRules.ZombiesForRound(r, 1),5} {RoundRules.ZombiesForRound(r, 4),4}  {RoundRules.SpawnIntervalSeconds(r),6:0.00}s  {RoundRules.ZombieHealth(r) * RoundRules.ZombiesForRound(r, 1),12:0}   {RoundRules.MaxAlive(1)}/{RoundRules.MaxAlive(4)}        {RoundRules.ZombieDamage(r)}");
        Console.WriteLine();
    }

    // ------------------------------------------------------------------ shots to kill
    /// <summary>Fraction of shotgun pellets that land inside a 0.64 x 1.5 m torso when the aim point is centred on it.</summary>
    private static readonly Dictionary<int, double> pelletCache = new Dictionary<int, double>();
    private static double PelletFraction(float dist)
    {
        int key = (int)Math.Round(dist * 10);
        if (pelletCache.TryGetValue(key, out var f)) return f;
        var rng = new Random(1);
        double spread = WeaponCatalog.Get(WeaponId.Shotgun).SpreadDegrees * Math.PI / 180.0;
        int hit = 0, n = 200000;
        for (int i = 0; i < n; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2, rad = Math.Sqrt(rng.NextDouble()) * spread; // Ballistics.Spread distribution
            double x = Math.Tan(rad) * dist * Math.Cos(a), y = Math.Tan(rad) * dist * Math.Sin(a);
            if (Math.Abs(x) <= 0.32 && Math.Abs(y) <= 0.75) hit++;
        }
        return pelletCache[key] = (double)hit / n;
    }

    private static double ShotDamage(WeaponId w, bool up, float dist, bool head)
    {
        double d = WeaponCatalog.DamageAt(w, up, dist, head);
        if (w == WeaponId.Shotgun) d *= WeaponCatalog.Get(w).Pellets * (head ? Math.Min(1.0, PelletFraction(dist) * 1.0) : PelletFraction(dist));
        return d;
    }

    private static void StkTable()
    {
        Console.WriteLine("== Shots to kill (every shot lands), body/head, at 10 m. Shotgun = expected pellets on torso. TTK(body) in s at fire rate, no reload.");
        Console.Write("weapon            dmg@5/10/15m   ");
        foreach (int r in KeyRounds) Console.Write($"  R{r,-2}(hp{RoundRules.ZombieHealth(r),6:0})");
        Console.WriteLine();
        foreach (var w in Guns)
            foreach (bool up in new[] { false, true })
            {
                var s = WeaponCatalog.Get(w);
                Console.Write($"{Name(w, up),-17} {ShotDamage(w, up, 5, false),4:0}/{ShotDamage(w, up, 10, false),4:0}/{ShotDamage(w, up, 15, false),4:0}  ");
                foreach (int r in KeyRounds)
                {
                    double hp = RoundRules.ZombieHealth(r);
                    int b = (int)Math.Ceiling(hp / ShotDamage(w, up, 10, false));
                    int h = (int)Math.Ceiling(hp / ShotDamage(w, up, 10, true));
                    double ttk = (b - 1) / s.RoundsPerSecond;
                    Console.Write($"  {b,4}/{h,-4}{ttk,5:0.0}s");
                }
                Console.WriteLine($"   ammo {WeaponCatalog.MagazineSize(w, up)}+{WeaponCatalog.MaxReserve(w, up)}");
            }
        Console.Write($"{"Knife",-17} 150 flat          ");
        foreach (int r in KeyRounds) Console.Write($"  {Math.Ceiling(RoundRules.ZombieHealth(r) / MeleeRules.Damage),4} hits      ");
        Console.WriteLine("\n");
    }

    // ------------------------------------------------------------------ per-zombie Monte Carlo
    private struct KillResult { public int Shots; public int Points; public bool Head; }

    private static KillResult KillOne(Random rng, WeaponId w, bool up, double hp)
    {
        var res = new KillResult();
        while (true)
        {
            res.Shots++;
            if (rng.NextDouble() >= HitRate) continue;
            bool head = rng.NextDouble() < HeadRate;
            float dist = MinRange + (float)rng.NextDouble() * (MaxRange - MinRange);
            double dmg = WeaponCatalog.DamageAt(w, up, dist, head);
            if (w == WeaponId.Shotgun)
            {
                int pellets = 0;
                double f = PelletFraction(dist);
                for (int i = 0; i < 8; i++) if (rng.NextDouble() < f) pellets++;
                if (pellets == 0) continue;
                dmg *= pellets;
            }
            hp -= dmg;
            if (hp <= 0) { res.Points += PointsRules.KillRewardFor(head, false); res.Head = head; return res; }
            res.Points += PointsRules.HitReward;
            if (res.Shots > 100000) return res;
        }
    }

    private static (double shots, double pts) Avg(WeaponId w, bool up, int round, int n = 4000)
    {
        var rng = new Random(round * 31 + (int)w * 7 + (up ? 1 : 0));
        double hp = RoundRules.ZombieHealth(round), s = 0, p = 0;
        for (int i = 0; i < n; i++) { var k = KillOne(rng, w, up, hp); s += k.Shots; p += k.Points; }
        return (s / n, p / n);
    }

    private static int RefillPrice(WeaponId w, bool up)
    {
        var s = WeaponCatalog.Get(w);
        if (up) return WeaponCatalog.UpgradedAmmoPrice;
        return s.WallPrice > 0 ? s.AmmoPrice : PurchaseRules.AmmoCratePrice;
    }

    private static void PerZombieEconomy()
    {
        Console.WriteLine($"== Per zombie at {HitRate:P0} hit rate, {HeadRate:P0} headshots, {MinRange}-{MaxRange} m: shots fired / points earned / ammo cost (wall refill price pro-rata; crate 600 in brackets)");
        Console.WriteLine("   and SOLO round totals: shots needed vs one full load (mag+reserve), net points after paying for ammo at the wall.");
        foreach (var w in Guns)
            foreach (bool up in new[] { false, true })
            {
                int cap = WeaponCatalog.MagazineSize(w, up) + WeaponCatalog.MaxReserve(w, up);
                int price = RefillPrice(w, up);
                Console.Write($"{Name(w, up),-17}");
                foreach (int r in KeyRounds)
                {
                    var (shots, pts) = Avg(w, up, r);
                    double cost = shots / WeaponCatalog.MaxReserve(w, up) * price;
                    int z = RoundRules.ZombiesForRound(r, 1);
                    double loads = shots * z / cap;
                    double net = (pts - cost) * z;
                    Console.Write($" | R{r} {shots,6:0.0}sh {pts,5:0}p {cost,5:0}c x{loads,5:0.0}L {net,7:0}");
                }
                Console.WriteLine();
            }
        Console.WriteLine("   (sh = shots per kill, p = points per kill, c = wall-ammo cost per kill, xL = full loads needed for the whole solo round, last = net points for the round)\n");
    }

    // ------------------------------------------------------------------ difficulty
    private static double KillSeconds(WeaponId w, bool up, int round, bool rapid, bool quick)
    {
        var (shots, _) = Avg(w, up, round, 1500);
        float interval = WeaponCatalog.SecondsBetweenShots(w, rapid);
        int mag = WeaponCatalog.MagazineSize(w, up);
        double reload = WeaponCatalog.ReloadSeconds(w, quick);
        return shots * interval + shots / mag * reload;
    }

    private static void Difficulty()
    {
        Console.WriteLine("== Difficulty: seconds of trigger time to kill one zombie vs seconds between spawns (pressure > 1 = horde grows).");
        Console.WriteLine("   Rounds where the horde would fill max-alive before the round's spawns are done are marked '!'.");
        var loadouts = new (WeaponId w, bool up, bool perks)[]
        {
            (WeaponId.Pistol, false, false), (WeaponId.Smg, false, false), (WeaponId.AssaultRifle, false, false),
            (WeaponId.AssaultRifle, true, true), (WeaponId.MarksmanRifle, true, true), (WeaponId.PlasmaRifle, true, true), (WeaponId.Smg, true, true),
        };
        foreach (int players in new[] { 1, 4 })
        {
            Console.WriteLine($"-- {players} player(s), everyone using the same loadout");
            foreach (var l in loadouts)
            {
                Console.Write($"{Name(l.w, l.up) + (l.perks ? "+RF+QH" : ""),-24}");
                int wall = -1;
                foreach (int r in new[] { 1, 3, 5, 8, 10, 12, 15, 18, 20, 25, 30, 35, 40, 55 })
                {
                    double kill = KillSeconds(l.w, l.up, r, l.perks, l.perks) / players;
                    double spawn = RoundRules.SpawnIntervalSeconds(r);
                    double pressure = kill / spawn;
                    int z = RoundRules.ZombiesForRound(r, players);
                    bool over = false;
                    if (pressure > 1)
                    {
                        double growthPerSec = 1 / spawn - 1 / kill;
                        double fill = RoundRules.MaxAlive(players) / growthPerSec;
                        over = fill < z * spawn;
                    }
                    if (over && wall < 0) wall = r;
                    Console.Write($" R{r}:{pressure,5:0.00}{(over ? "!" : " ")}");
                }
                Console.WriteLine($"   first '!' round: {(wall < 0 ? "none" : wall.ToString())}");
            }
        }
        Console.WriteLine();
    }

    // ------------------------------------------------------------------ economy
    private sealed class Gun { public WeaponId W; public bool Up; public int Ammo; }

    private sealed class Sim
    {
        public int Points = GameInfo.StartingPoints;
        public int Spent, Earned, AmmoSpent, KnifeKills;
        public PerkId PerkMask;
        public List<Gun> Guns = new List<Gun> { new Gun { W = WeaponId.Pistol, Ammo = 72 } };
        public int Cap(Gun g) => WeaponCatalog.MagazineSize(g.W, g.Up) + WeaponCatalog.MaxReserve(g.W, g.Up);
        public bool Owns(WeaponId w) => Guns.Any(g => g.W == w);
        public Gun Best => Guns.OrderByDescending(g => Rank(g)).First();
        public static int Rank(Gun g) => (int)g.W * 2 + (g.Up ? 20 : 0);
    }

    private static readonly Dictionary<WeaponId, int> WallZone = new Dictionary<WeaponId, int>
    { [WeaponId.Smg] = 0, [WeaponId.Shotgun] = 1, [WeaponId.AssaultRifle] = 2, [WeaponId.MarksmanRifle] = 3 };

    private static void Economy(int players, bool crateRefillsUpgraded)
    {
        // Purchase plan; door items are paid by the richest player, personal items by everyone, escape is pooled.
        var plan = new List<(string name, string kind, int cost, int param)>
        {
            ("SMG wall buy", "gun", 1000, (int)WeaponId.Smg),
            ("D1 Checkpoint", "door", 1000, 1),
            ("AR-7 wall buy", "gun", 1600, (int)WeaponId.AssaultRifle),
            ("D0 Alley Gate", "door", 750, 0),
            ("D3 Lab", "door", 1500, 3),
            ("Iron Hide", "perk", 2500, 1),
            ("Trigger Happy", "perk", 2000, 4),
            ("Upgrade AR", "upgrade", 5000, 0),
            ("Quickhands", "perk", 3000, 2),
            ("D4 Hangar", "door", 2500, 4),
            ("Fleetfoot (perk set done)", "perk", 2000, 8),
            ("ESCAPE funded", "escape", EscapeRules.CostFor(players), 0),
        };
        Console.WriteLine($"== Economy run: {players} player(s), {HitRate:P0} hits, {HeadRate:P0} headshots, buys in order, refills ammo when dry " +
                          $"({(crateRefillsUpgraded ? "ammo crate 600 refills upgraded guns - as coded" : "upgraded refills forced to the 2500 wall price - as designed")})");
        var team = Enumerable.Range(0, players).Select(_ => new Sim()).ToList();
        ulong doors = 0;
        int step = 0;
        var rng = new Random(42 + players);
        double clock = 0;
        int lastRound = 0;
        var log = new List<string>();
        var roundPts = new List<string>();
        for (int r = 1; r <= 60 && step < plan.Count; r++)
        {
            int z = RoundRules.ZombiesForRound(r, players);
            double hp = RoundRules.ZombieHealth(r);
            int earnedBefore = team.Sum(p => p.Earned);
            var trigger = new double[players];
            for (int i = 0; i < z; i++)
            {
                var p = team[i % players];
                trigger[i % players] += KillWithLoadout(rng, p, hp, doors, crateRefillsUpgraded);
            }
            double spawnTime = 1 + z * RoundRules.SpawnIntervalSeconds(r) + 12; // last spawn + walk-in
            double roundTime = Math.Max(spawnTime, trigger.Max() + 8);
            clock += roundTime + RoundRules.IntermissionSeconds;
            int earned = team.Sum(p => p.Earned) - earnedBefore;
            if (r <= 6 || r % 5 == 0) roundPts.Add($"R{r}:{earned / players}");
            // Intermission purchases (also allowed mid-round in reality; this is a lower bound on speed).
            bool progressed = true;
            while (progressed && step < plan.Count)
            {
                progressed = false;
                var item = plan[step];
                switch (item.kind)
                {
                    case "door":
                    {
                        var payer = team.OrderByDescending(p => p.Points).First();
                        if (payer.Points >= item.cost + 300) { payer.Points -= item.cost; payer.Spent += item.cost; doors |= 1UL << item.param; progressed = true; }
                        break;
                    }
                    case "escape":
                    {
                        int pool = team.Sum(p => Math.Max(0, p.Points - 600));
                        if (pool >= item.cost) progressed = true;
                        break;
                    }
                    default:
                    {
                        bool allDone = true;
                        foreach (var p in team)
                        {
                            if (Has(p, item)) continue;
                            if (p.Points >= item.cost + 300) { Buy(p, item); }
                            if (!Has(p, item)) allDone = false;
                        }
                        progressed = allDone;
                        break;
                    }
                }
                if (progressed) { log.Add($"{item.name} (R{r}, ~{clock / 60:0} min)"); step++; }
            }
            lastRound = r;
        }
        Console.WriteLine("   avg points earned per player per round: " + string.Join("  ", roundPts));
        Console.WriteLine("   milestones (end of round): " + string.Join(" -> ", log));
        var t0 = team[0];
        Console.WriteLine($"   per player: earned {t0.Earned}, ammo spend {t0.AmmoSpent}, knife-only kills (gun dry & broke) {t0.KnifeKills}, stopped at R{lastRound}\n");
    }

    private static bool Has(Sim p, (string name, string kind, int cost, int param) item)
    {
        switch (item.kind)
        {
            case "gun": return p.Owns((WeaponId)item.param);
            case "upgrade": return p.Guns.Any(g => g.Up);
            case "perk": return p.PerkMask.HasFlag((PerkId)item.param);
            default: return true;
        }
    }

    private static void Buy(Sim p, (string name, string kind, int cost, int param) item)
    {
        p.Points -= item.cost; p.Spent += item.cost;
        switch (item.kind)
        {
            case "gun":
            {
                var w = (WeaponId)item.param;
                var g = new Gun { W = w, Ammo = WeaponCatalog.MagazineSize(w, false) + WeaponCatalog.MaxReserve(w, false) };
                if (p.Guns.Count < 2) p.Guns.Add(g);
                else p.Guns[p.Guns.FindIndex(x => Sim.Rank(x) == p.Guns.Min(Sim.Rank))] = g;
                break;
            }
            case "upgrade":
            {
                var g = p.Best; g.Up = true; g.Ammo = p.Cap(g);
                break;
            }
            case "perk": p.PerkMask |= (PerkId)item.param; break;
        }
    }

    /// <summary>Kills one zombie with the best gun that has ammo, buying ammo when dry. Returns trigger seconds.</summary>
    private static double KillWithLoadout(Random rng, Sim p, double hp, ulong doors, bool crateRefillsUpgraded)
    {
        double seconds = 0;
        var active = ZonesOpen(doors);
        bool crate = active[1] || active[2];
        while (true)
        {
            var gun = p.Guns.Where(g => g.Ammo > 0).OrderByDescending(Sim.Rank).FirstOrDefault();
            var best = p.Best;
            if (gun != best || gun == null)
            {
                // Try to refill the best gun.
                int price = int.MaxValue;
                if (crate && (!best.Up || crateRefillsUpgraded)) price = PurchaseRules.AmmoCratePrice;
                if (WallZone.TryGetValue(best.W, out int zone) && active[zone]) price = Math.Min(price, RefillPrice(best.W, best.Up));
                if (price != int.MaxValue && p.Points >= price)
                {
                    p.Points -= price; p.AmmoSpent += price;
                    if (price == PurchaseRules.AmmoCratePrice) foreach (var g in p.Guns) g.Ammo = p.Cap(g); else best.Ammo = p.Cap(best);
                    continue;
                }
            }
            if (gun == null)
            {
                // Knife it.
                double h = hp;
                while (h > 0) { h -= MeleeRules.Damage; seconds += MeleeRules.Cooldown; if (h > 0) { p.Points += 10; p.Earned += 10; } }
                p.Points += PointsRules.MeleeKillReward; p.Earned += PointsRules.MeleeKillReward; p.KnifeKills++;
                return seconds;
            }
            var stats = WeaponCatalog.Get(gun.W);
            // Fire one shot.
            gun.Ammo--;
            seconds += WeaponCatalog.SecondsBetweenShots(gun.W, p.PerkMask.HasFlag(PerkId.RapidFire)) +
                       WeaponCatalog.ReloadSeconds(gun.W, p.PerkMask.HasFlag(PerkId.Quickhands)) / WeaponCatalog.MagazineSize(gun.W, gun.Up);
            if (rng.NextDouble() >= HitRate) continue;
            bool head = rng.NextDouble() < HeadRate;
            float dist = MinRange + (float)rng.NextDouble() * (MaxRange - MinRange);
            double dmg = WeaponCatalog.DamageAt(gun.W, gun.Up, dist, head);
            if (gun.W == WeaponId.Shotgun)
            {
                int pellets = 0; double f = PelletFraction(dist);
                for (int i = 0; i < stats.Pellets; i++) if (rng.NextDouble() < f) pellets++;
                if (pellets == 0) continue;
                dmg *= pellets;
            }
            hp -= dmg;
            int pts = hp <= 0 ? PointsRules.KillRewardFor(head, false) : PointsRules.HitReward;
            p.Points += pts; p.Earned += pts;
            if (hp <= 0) return seconds;
        }
    }

    private static bool[] ZonesOpen(ulong doors) => MapCatalog.ActiveZones(doors);

    // ------------------------------------------------------------------ starter area
    private static void StarterArea()
    {
        Console.WriteLine("== Starter area (Main Street only, no ammo crate, pistol cannot be refilled without opening a door)");
        foreach (bool knifeOpen in new[] { false, true })
        {
            var rng = new Random(7);
            var p = new Sim();
            Console.Write(knifeOpen ? "knife rounds 1-2, then guns: " : "guns only:                  ");
            for (int r = 1; r <= 8; r++)
            {
                double hp = RoundRules.ZombieHealth(r);
                for (int i = 0; i < RoundRules.ZombiesForRound(r, 1); i++)
                {
                    if (knifeOpen && r <= 2)
                    {
                        double h = hp;
                        while (h > 0) { h -= MeleeRules.Damage; if (h > 0) { p.Points += 10; p.Earned += 10; } }
                        p.Points += 130; p.Earned += 130;
                    }
                    else KillWithLoadout(rng, p, hp, 0, true);
                }
                if (!p.Owns(WeaponId.Smg) && p.Points >= 1000) Buy(p, ("SMG", "gun", 1000, (int)WeaponId.Smg));
                Console.Write($" R{r}: {p.Points}pts pistol={p.Guns[0].Ammo} {(p.Owns(WeaponId.Smg) ? "smg=" + p.Guns.First(g => g.W == WeaponId.Smg).Ammo : "")};");
            }
            Console.WriteLine($"  knife-only kills={p.KnifeKills}");
        }
        Console.WriteLine();
    }

    // ------------------------------------------------------------------ mystery box
    private static void Box()
    {
        Console.WriteLine("== Supply Drop: chance per paid roll and expected cost to get the plasma rifle (declining every other result)");
        var starts = new (string name, WeaponId[] owned)[]
        {
            ("pistol only", new[] { WeaponId.Pistol }),
            ("pistol + SMG", new[] { WeaponId.Pistol, WeaponId.Smg }),
            ("AR + SMG", new[] { WeaponId.AssaultRifle, WeaponId.Smg }),
        };
        foreach (var s in starts)
        {
            var rng = new Random(99);
            int trials = 200000;
            long paid = 0, moves = 0, plasmaFirst = 0, rollsTotal = 0;
            for (int t = 0; t < trials; t++)
            {
                var l = new Loadout();
                foreach (var w in s.owned) l.Give(w, false);
                int uses = 0;
                while (true)
                {
                    var got = MysteryBoxRules.Roll(rng, l, uses);
                    uses++;
                    rollsTotal++;
                    if (got == WeaponId.None) { moves++; uses = 0; continue; } // refunded, walk to the next spot
                    paid++;
                    if (got == WeaponId.PlasmaRifle) break;
                }
            }
            int weight = 0;
            foreach (var w in WeaponCatalog.All) if (w.InMysteryBox && !s.owned.Contains(w.Id)) weight += MysteryBoxRules.Weight(w.Id);
            Console.WriteLine($"{s.name,-14} plasma odds per weapon roll {1.0 / weight:P1}; avg paid rolls {paid / (double)trials:0.0} = {paid / (double)trials * MysteryBoxRules.Cost:0} points; " +
                              $"avg relocations on the way {moves / (double)trials:0.0}; P(plasma within 5 paid rolls) {1 - Math.Pow(1 - 1.0 / weight, 5):P0}");
        }
        Console.WriteLine();
    }

    // ------------------------------------------------------------------ spawn placement
    private static void Spawns()
    {
        Console.WriteLine("== Spawn choice with every door open: the 4 nearest spawn points by straight-line distance (RoundDirector.TrySpawn) vs walking route");
        var spots = new (string name, V3 pos)[]
        {
            ("start street", new V3(-10, 0, 0)), ("corner store SMG", new V3(-4, 0, -18)), ("box in House A", new V3(-24, 0, -16)),
            ("checkpoint AR", new V3(27, 0, 30)), ("Xeno-Forge", new V3(34, 0, -40)), ("launch console", new V3(4, -6, -30)),
        };
        ulong all = 0b11111;
        foreach (var (name, pos) in spots)
        {
            var cands = new List<(float d, V3 p, int zone)>();
            for (int z = 0; z < MapCatalog.Zones.Length; z++)
                foreach (var sp in MapCatalog.Zones[z].ZombieSpawns)
                {
                    float d = MapCatalog.Distance(sp, pos.X, pos.Y, pos.Z);
                    if (d >= 6f) cands.Add((d, sp, z));
                }
            var pick = cands.OrderBy(c => c.d).Take(4).ToList();
            int myZone = MapCatalog.ZoneAt(pos.X, pos.Y, pos.Z);
            Console.Write($"{name,-18}(zone {myZone}): ");
            foreach (var c in pick)
            {
                double walk = PathLength(c.p, c.zone, pos, myZone);
                Console.Write($" z{c.zone} {c.d,4:0}m line/{walk,4:0}m walk ({walk / RoundRules.MoveSpeed(ZombieSpeed.Walk),3:0}s walker);");
            }
            Console.WriteLine();
        }
        Console.WriteLine();
    }

    // Waypoint graph through door centres (plus the hangar's inner door at the bottom of the ramp).
    private static double PathLength(V3 from, int fromZone, V3 to, int toZone)
    {
        var nodes = new List<(V3 p, int a, int b)>();
        foreach (var d in MapCatalog.Doors)
        {
            if (d.Id == 4) { nodes.Add((d.Position, 3, 5)); continue; }
            nodes.Add((d.Position, d.ZoneA, d.ZoneB));
        }
        nodes.Add((new V3(10, -6, -34), 5, 4)); // ramp corridor = pseudo zone 5
        var all = new List<(V3 p, int a, int b)> { (from, fromZone, fromZone), (to, toZone, toZone) };
        all.AddRange(nodes);
        int n = all.Count;
        var dist = Enumerable.Repeat(double.MaxValue, n).ToArray();
        var done = new bool[n];
        dist[0] = 0;
        for (int it = 0; it < n; it++)
        {
            int u = -1;
            for (int i = 0; i < n; i++) if (!done[i] && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0 || dist[u] == double.MaxValue) break;
            done[u] = true;
            for (int v = 0; v < n; v++)
            {
                if (done[v]) continue;
                bool share = all[u].a == all[v].a || all[u].a == all[v].b || all[u].b == all[v].a || all[u].b == all[v].b;
                if (!share) continue;
                double w = MapCatalog.Distance(all[u].p, all[v].p.X, all[v].p.Y, all[v].p.Z);
                if (dist[u] + w < dist[v]) dist[v] = dist[u] + w;
            }
        }
        return dist[1];
    }
}

