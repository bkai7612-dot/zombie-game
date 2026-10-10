using System;
using System.Collections.Generic;

namespace UZS.Core
{
    /// <summary>Engine-independent vector so map data can live in (and be tested from) the core assembly.</summary>
    public readonly struct V3
    {
        public readonly float X, Y, Z;
        public V3(float x, float y, float z) { X = x; Y = y; Z = z; }
        public override string ToString() => $"({X}, {Y}, {Z})";
    }

    public sealed class ZoneDef
    {
        public int Id;
        public string Name;
        /// <summary>Axis-aligned floor bounds (X/Z) used to decide which zone a position belongs to.</summary>
        public float MinX, MaxX, MinZ, MaxZ;
        public float FloorY;
        public V3[] ZombieSpawns;

        public bool Contains(float x, float z) => x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }

    public sealed class DoorDef
    {
        public int Id;
        public string Name;
        public int Cost;
        public int ZoneA;
        public int ZoneB;
        /// <summary>Centre of the doorway at floor level.</summary>
        public V3 Position;
        public float Width;
        public float Height;
        /// <summary>True when the doorway lies in a wall running along Z (i.e. players walk through it along X).</summary>
        public bool AlongZ;
    }

    public sealed class StationDef
    {
        public int Id;
        public InteractableKind Kind;
        public int Zone;
        public V3 Position;
        /// <summary>Degrees; 0 faces +Z, 90 faces +X.</summary>
        public float Yaw;
        /// <summary>WeaponId for wall buys, PerkId for perk machines, unused otherwise.</summary>
        public int Param;
    }

    /// <summary>
    /// The single map: "Hollow Pines" - a compact suburban street, back alley, army checkpoint,
    /// research lab and a concealed underground UFO hangar. Positions are in metres.
    /// </summary>
    public static class MapCatalog
    {
        public const string MapName = "Hollow Pines";
        public const int StartZone = 0;
        public const int MysteryBoxInteractableId = 50;
        public const int EscapeConsoleInteractableId = 60;

        public static readonly ZoneDef[] Zones =
        {
            new ZoneDef
            {
                Id = 0, Name = "Main Street", MinX = -40, MaxX = 20, MinZ = -26, MaxZ = 8, FloorY = 0,
                ZombieSpawns = new[] { new V3(-37, 0, -3), new V3(-37, 0, 4), new V3(-30, 0, -24), new V3(-12, 0, -24), new V3(8, 0, -24), new V3(16, 0, -20) },
            },
            new ZoneDef
            {
                Id = 1, Name = "Back Alley", MinX = -40, MaxX = 20, MinZ = 8, MaxZ = 40, FloorY = 0,
                ZombieSpawns = new[] { new V3(-37, 0, 30), new V3(-10, 0, 38), new V3(12, 0, 38), new V3(17, 0, 11) },
            },
            new ZoneDef
            {
                Id = 2, Name = "Army Checkpoint", MinX = 20, MaxX = 58, MinZ = -18, MaxZ = 40, FloorY = 0,
                ZombieSpawns = new[] { new V3(56, 0, 38), new V3(56, 0, -15), new V3(24, 0, 38), new V3(45, 0, 38) },
            },
            new ZoneDef
            {
                Id = 3, Name = "Research Lab", MinX = 24, MaxX = 58, MinZ = -44, MaxZ = -18, FloorY = 0,
                ZombieSpawns = new[] { new V3(56, 0, -42), new V3(27, 0, -21), new V3(56, 0, -21) },
            },
            new ZoneDef
            {
                Id = 4, Name = "Hangar 51-B", MinX = -20, MaxX = 24, MinZ = -50, MaxZ = -24, FloorY = -6,
                ZombieSpawns = new[] { new V3(-18, -6, -48), new V3(8, -6, -48), new V3(-18, -6, -27) },
            },
        };

        public static readonly DoorDef[] Doors =
        {
            new DoorDef { Id = 0, Name = "Alley Gate", Cost = 750, ZoneA = 0, ZoneB = 1, Position = new V3(-12, 0, 8), Width = 3.5f, Height = 3f, AlongZ = false },
            new DoorDef { Id = 1, Name = "Checkpoint Barrier", Cost = 1000, ZoneA = 0, ZoneB = 2, Position = new V3(20, 0, 0), Width = 8f, Height = 3.5f, AlongZ = true },
            new DoorDef { Id = 2, Name = "Fence Breach", Cost = 1250, ZoneA = 1, ZoneB = 2, Position = new V3(20, 0, 16), Width = 3.5f, Height = 3f, AlongZ = true },
            new DoorDef { Id = 3, Name = "Lab Blast Door", Cost = 1500, ZoneA = 2, ZoneB = 3, Position = new V3(42, 0, -18), Width = 4f, Height = 3.5f, AlongZ = false },
            new DoorDef { Id = 4, Name = "Hangar Lift Door", Cost = 2500, ZoneA = 3, ZoneB = 4, Position = new V3(24, 0, -34), Width = 4f, Height = 3.5f, AlongZ = true },
        };

        public static readonly StationDef[] Stations =
        {
            // Main Street: starter wall buy inside the corner store.
            new StationDef { Id = 10, Kind = InteractableKind.WallBuy, Zone = 0, Position = new V3(-4, 1.3f, -19.7f), Yaw = 0, Param = (int)WeaponId.Smg },
            // Back Alley.
            new StationDef { Id = 11, Kind = InteractableKind.WallBuy, Zone = 1, Position = new V3(-23, 1.3f, 35.7f), Yaw = 180, Param = (int)WeaponId.Shotgun },
            new StationDef { Id = 20, Kind = InteractableKind.Perk, Zone = 1, Position = new V3(9, 0, 22.5f), Yaw = 180, Param = (int)PerkId.Quickhands },
            new StationDef { Id = 30, Kind = InteractableKind.AmmoCrate, Zone = 1, Position = new V3(-36, 0, 16), Yaw = 90 },
            // Checkpoint.
            new StationDef { Id = 12, Kind = InteractableKind.WallBuy, Zone = 2, Position = new V3(28.55f, 1.3f, 30), Yaw = 270, Param = (int)WeaponId.AssaultRifle },
            new StationDef { Id = 21, Kind = InteractableKind.Perk, Zone = 2, Position = new V3(56, 0, 20), Yaw = 270, Param = (int)PerkId.IronHide },
            new StationDef { Id = 31, Kind = InteractableKind.AmmoCrate, Zone = 2, Position = new V3(36, 0, -8), Yaw = 0 },
            // Research lab.
            new StationDef { Id = 13, Kind = InteractableKind.WallBuy, Zone = 3, Position = new V3(57.7f, 1.3f, -30), Yaw = 270, Param = (int)WeaponId.MarksmanRifle },
            new StationDef { Id = 22, Kind = InteractableKind.Perk, Zone = 3, Position = new V3(50, 0, -42.5f), Yaw = 0, Param = (int)PerkId.RapidFire },
            new StationDef { Id = 40, Kind = InteractableKind.UpgradeBench, Zone = 3, Position = new V3(34, 0, -41.5f), Yaw = 0 },
            // Hangar.
            new StationDef { Id = 23, Kind = InteractableKind.Perk, Zone = 4, Position = new V3(-18.5f, -6, -30), Yaw = 90, Param = (int)PerkId.Fleetfoot },
            new StationDef { Id = EscapeConsoleInteractableId, Kind = InteractableKind.EscapeConsole, Zone = 4, Position = new V3(4, -6, -29), Yaw = 180 },
        };

        /// <summary>Possible mystery box locations (index is synchronised, not the position).</summary>
        public static readonly StationDef[] BoxLocations =
        {
            new StationDef { Id = MysteryBoxInteractableId, Kind = InteractableKind.MysteryBox, Zone = 0, Position = new V3(-26, 0, -17.5f), Yaw = 90 },
            new StationDef { Id = MysteryBoxInteractableId, Kind = InteractableKind.MysteryBox, Zone = 2, Position = new V3(40, 0, 36), Yaw = 180 },
            new StationDef { Id = MysteryBoxInteractableId, Kind = InteractableKind.MysteryBox, Zone = 3, Position = new V3(45, 0, -25), Yaw = 180 },
        };

        public static readonly V3[] PlayerSpawns = { new V3(-14, 0, -2), new V3(-10, 0, 2), new V3(-6, 0, -2), new V3(-2, 0, 2) };

        /// <summary>Centre of the UFO boarding area and its radius.</summary>
        public static readonly V3 UfoPosition = new V3(-5, -6, -37);
        public const float BoardingRadius = 7f;

        public static bool TryGetDoor(int id, out DoorDef door)
        {
            door = id >= 0 && id < Doors.Length ? Doors[id] : null;
            return door != null && door.Id == id;
        }

        public static StationDef FindStation(int id)
        {
            foreach (var s in Stations)
                if (s.Id == id) return s;
            return null;
        }

        /// <summary>Zones reachable from the start zone through unlocked doors.</summary>
        public static bool[] ActiveZones(ulong unlockedDoorMask)
        {
            var active = new bool[Zones.Length];
            var queue = new Queue<int>();
            active[StartZone] = true;
            queue.Enqueue(StartZone);
            while (queue.Count > 0)
            {
                int z = queue.Dequeue();
                foreach (var d in Doors)
                {
                    if ((unlockedDoorMask & (1UL << d.Id)) == 0) continue;
                    int other = d.ZoneA == z ? d.ZoneB : (d.ZoneB == z ? d.ZoneA : -1);
                    if (other < 0 || active[other]) continue;
                    active[other] = true;
                    queue.Enqueue(other);
                }
            }
            return active;
        }

        /// <summary>
        /// Number of unlocked doors to cross from <paramref name="fromZone"/> to each zone (int.MaxValue if unreachable).
        /// Used instead of straight-line distance because the hangar lies directly under Main Street.
        /// </summary>
        public static int[] ZoneHops(int fromZone, ulong unlockedDoorMask)
        {
            var hops = new int[Zones.Length];
            for (int i = 0; i < hops.Length; i++) hops[i] = int.MaxValue;
            if (fromZone < 0 || fromZone >= Zones.Length) return hops;
            var queue = new Queue<int>();
            hops[fromZone] = 0;
            queue.Enqueue(fromZone);
            while (queue.Count > 0)
            {
                int z = queue.Dequeue();
                foreach (var d in Doors)
                {
                    if ((unlockedDoorMask & (1UL << d.Id)) == 0) continue;
                    int other = d.ZoneA == z ? d.ZoneB : (d.ZoneB == z ? d.ZoneA : -1);
                    if (other < 0 || hops[other] != int.MaxValue) continue;
                    hops[other] = hops[z] + 1;
                    queue.Enqueue(other);
                }
            }
            return hops;
        }

        public static int ZoneAt(float x, float y, float z)
        {
            // Hangar sits below Main Street, so disambiguate by height first.
            if (y < -3f) return 4;
            for (int i = 0; i < Zones.Length; i++)
                if (i != 4 && Zones[i].Contains(x, z)) return i;
            return -1;
        }

        public static float Distance(V3 a, float x, float y, float z)
        {
            float dx = a.X - x, dy = a.Y - y, dz = a.Z - z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
