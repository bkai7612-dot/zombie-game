using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>Handles to the parts of the built map that change at runtime.</summary>
    public sealed class MapInstance
    {
        public GameObject Root;
        public Transform Static;
        public readonly List<DoorView> Doors = new List<DoorView>();
        public BoxView Box;
        public UfoView Ufo;
        public Bounds NavBounds;
    }

    /// <summary>
    /// Builds "Hollow Pines" deterministically from <see cref="MapCatalog"/> on every peer. All geometry is
    /// placeholder primitive art (clearly not final), arranged for readable gameplay at night.
    /// </summary>
    public static class MapBuilder
    {
        private const float WallH = 3.2f;
        private const float BoundaryH = 4f;

        public static MapInstance Current { get; private set; }

        public static MapInstance Build()
        {
            if (Current != null) Destroy();

            var map = new MapInstance { Root = new GameObject("Map_HollowPines") };
            map.Static = Geo.Group(map.Root.transform, "Static").transform;
            var dyn = Geo.Group(map.Root.transform, "Dynamic").transform;

            BuildEnvironment(map.Root.transform);
            BuildGround(map.Static);
            BuildMainStreet(map.Static);
            BuildBackAlley(map.Static);
            BuildCheckpoint(map.Static);
            BuildLab(map.Static);
            BuildHangar(map.Static, dyn, map);
            BuildBoundaries(map.Static);

            foreach (var d in MapCatalog.Doors) map.Doors.Add(BuildDoor(dyn, d));
            foreach (var s in MapCatalog.Stations) BuildStation(dyn, s);
            map.Box = BuildBox(dyn);

            map.NavBounds = new Bounds(new Vector3(9f, -1f, -5f), new Vector3(110f, 16f, 104f));
            StaticBatchingUtility.Combine(map.Static.gameObject);
            Current = map;
            return map;
        }

        public static void Destroy()
        {
            if (Current != null && Current.Root != null) Object.Destroy(Current.Root);
            Current = null;
            NavMesh.RemoveAllNavMeshData();
        }

        // ------------------------------------------------------------------ environment

        private static void BuildEnvironment(Transform root)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.016f;
            RenderSettings.fogColor = new Color(0.06f, 0.075f, 0.1f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.16f, 0.19f, 0.27f);
            RenderSettings.ambientEquatorColor = new Color(0.1f, 0.11f, 0.13f);
            RenderSettings.ambientGroundColor = new Color(0.05f, 0.05f, 0.05f);
            RenderSettings.skybox = null;

            var moonGo = new GameObject("Moonlight");
            moonGo.transform.SetParent(root, false);
            moonGo.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            var moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.62f, 0.7f, 0.9f);
            moon.intensity = 0.55f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.8f;

            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 6);
            QualitySettings.shadowDistance = 60f;
        }

        private static void BuildGround(Transform t)
        {
            // Suburb (street + alley) slab; its underside doubles as the hangar ceiling.
            Geo.Span(t, "Ground_Suburb", new Vector3(-40, -0.5f, -26), new Vector3(20, 0, 40), Palette.Grass);
            Geo.Span(t, "Ground_Checkpoint", new Vector3(20, -0.5f, -18), new Vector3(58, 0, 40), Palette.Dirt);
            Geo.Span(t, "Ground_Lab", new Vector3(24, -0.5f, -44), new Vector3(58, 0, -18), Palette.LabFloor);
            // Outer apron so the fog edge never shows a void.
            Geo.Span(t, "Ground_Outer", new Vector3(-90, -0.62f, -90), new Vector3(110, -0.52f, 90), Palette.Dirt, false);
        }

        private static void Lamp(Transform t, Vector3 pos, float height = 5.5f, float armDir = 1f)
        {
            var g = Geo.Group(t, "StreetLamp", pos).transform;
            Geo.Cylinder(g, "pole", new Vector3(0, height * 0.5f, 0), 0.08f, height, Palette.DarkMetal);
            Geo.Box(g, "arm", new Vector3(0, height, armDir * 0.6f), new Vector3(0.08f, 0.08f, 1.2f), Palette.DarkMetal, false);
            Geo.Box(g, "head", new Vector3(0, height - 0.08f, armDir * 1.1f), new Vector3(0.35f, 0.12f, 0.5f), Palette.LampWarm, false);
            Geo.PointLight(g, new Vector3(0, height - 0.5f, armDir * 1.1f), new Color(1f, 0.78f, 0.5f), 2.2f, 16f);
        }

        private static void Car(Transform t, Vector3 pos, float yaw, Color color, bool wrecked = false)
        {
            var g = Geo.Group(t, "Car", pos, yaw).transform;
            var paint = Palette.Tint(color);
            Geo.Box(g, "body", new Vector3(0, 0.65f, 0), new Vector3(1.9f, 0.7f, 4.4f), paint);
            Geo.Box(g, "cabin", new Vector3(0, 1.25f, -0.2f), new Vector3(1.7f, 0.55f, 2.2f), wrecked ? Palette.Rust : paint);
            Geo.Box(g, "windshield", new Vector3(0, 1.25f, 0.92f), new Vector3(1.6f, 0.45f, 0.05f), Palette.Window, false);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.95f : 0.95f;
                float z = i < 2 ? 1.4f : -1.4f;
                Geo.Cylinder(g, "wheel", new Vector3(x, 0.35f, z), 0.35f, 0.25f, Palette.DarkMetal, false, new Vector3(0, 0, 90));
            }
            Geo.Box(g, "tail", new Vector3(0, 0.75f, -2.21f), new Vector3(1.6f, 0.12f, 0.03f), Palette.LampRed, false);
        }

        private static void Crate(Transform t, Vector3 pos, float size, float yaw = 0f)
        {
            Geo.Box(t, "Crate", pos + Vector3.up * size * 0.5f, Vector3.one * size, Palette.Wood, true, yaw);
        }

        private static void Barrel(Transform t, Vector3 pos, Material mat)
        {
            Geo.Cylinder(t, "Barrel", pos + Vector3.up * 0.45f, 0.3f, 0.9f, mat);
        }

        private static void Sandbags(Transform t, Vector3 a, Vector3 b)
        {
            var dir = b - a;
            float len = dir.magnitude;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            Geo.Box(t, "Sandbags", (a + b) * 0.5f + Vector3.up * 0.55f, new Vector3(0.9f, 1.1f, len), Palette.Sandbag, true, yaw);
        }

        private static void Fence(Transform t, string name, Vector2 a, Vector2 b, float height, Material mat, params Vector4[] openings)
        {
            Geo.Wall(t, name, a, b, 0f, height, 0.15f, mat, openings);
        }

        /// <summary>A simple enterable house: floor, walls with openings, flat roof with parapet, porch light.</summary>
        private static Transform House(Transform t, string name, float x0, float z0, float x1, float z1, Material siding,
            Vector4[] north, Vector4[] south, Vector4[] west, Vector4[] east, bool interiorLight = true)
        {
            var g = Geo.Group(t, name).transform;
            const float th = 0.3f;
            Geo.Span(g, "floor", new Vector3(x0, 0f, z0), new Vector3(x1, 0.05f, z1), Palette.DarkWood, false);
            Geo.Wall(g, "N", new Vector2(x0, z1), new Vector2(x1, z1), 0f, WallH, th, siding, north);
            Geo.Wall(g, "S", new Vector2(x0, z0), new Vector2(x1, z0), 0f, WallH, th, siding, south);
            Geo.Wall(g, "W", new Vector2(x0, z0), new Vector2(x0, z1), 0f, WallH, th, siding, west);
            Geo.Wall(g, "E", new Vector2(x1, z0), new Vector2(x1, z1), 0f, WallH, th, siding, east);
            Geo.Span(g, "roof", new Vector3(x0 - 0.4f, WallH, z0 - 0.4f), new Vector3(x1 + 0.4f, WallH + 0.3f, z1 + 0.4f), Palette.Roof);
            if (interiorLight)
                Geo.PointLight(g, new Vector3((x0 + x1) * 0.5f, WallH - 0.5f, (z0 + z1) * 0.5f), new Color(1f, 0.75f, 0.5f), 1.1f, 9f);
            return g;
        }

        // ------------------------------------------------------------------ zones

        private static void BuildMainStreet(Transform root)
        {
            var t = Geo.Group(root, "Zone0_MainStreet").transform;
            Geo.Span(t, "Road", new Vector3(-40, 0f, -6), new Vector3(20, 0.03f, 6), Palette.Asphalt, false);
            Geo.Span(t, "SidewalkS", new Vector3(-40, 0f, -8), new Vector3(20, 0.12f, -6), Palette.Sidewalk);
            Geo.Span(t, "SidewalkN", new Vector3(-40, 0f, 6), new Vector3(20, 0.12f, 7.8f), Palette.Sidewalk);
            for (float x = -38; x < 20; x += 6) Geo.Box(t, "Stripe", new Vector3(x, 0.035f, 0), new Vector3(2.5f, 0.01f, 0.18f), Palette.Chalk, false);

            // House A (box location 0) - door on the street side.
            var houseA = House(t, "HouseA", -32, -20, -20, -10, Palette.Siding,
                new[] { Geo.Door(6f, 2f), Geo.Window(2.5f), Geo.Window(9.5f) }, new[] { Geo.Window(6f) },
                new[] { Geo.Window(5f) }, new[] { Geo.Window(5f) });
            Geo.Box(houseA, "Couch", new Vector3(-30.5f, 0.45f, -14f), new Vector3(0.9f, 0.9f, 2.4f), Palette.Tint(new Color(0.3f, 0.2f, 0.2f)));
            Geo.Box(houseA, "Table", new Vector3(-23f, 0.4f, -13f), new Vector3(1.4f, 0.8f, 0.9f), Palette.Wood);
            Geo.PointLight(houseA, new Vector3(-26f, 2.6f, -9.2f), new Color(1f, 0.75f, 0.45f), 1.4f, 7f);

            // Corner store (starter wall buy).
            var store = House(t, "CornerStore", -10, -20, 2, -10, Palette.Brick,
                new[] { Geo.Door(6f, 3f, 2.6f), Geo.Window(2f, 2.4f, 0.8f, 2.4f), Geo.Window(10f, 2.4f, 0.8f, 2.4f) }, new Vector4[0],
                new[] { Geo.Window(5f) }, new Vector4[0]);
            Geo.Box(store, "Shelf1", new Vector3(-7.5f, 0.9f, -15f), new Vector3(0.6f, 1.8f, 4f), Palette.Metal);
            Geo.Box(store, "Shelf2", new Vector3(-0.5f, 0.9f, -15f), new Vector3(0.6f, 1.8f, 4f), Palette.Metal);
            Geo.Box(store, "Counter", new Vector3(-4f, 0.5f, -12.5f), new Vector3(3f, 1f, 0.8f), Palette.Wood);
            Geo.Box(store, "Sign", new Vector3(-4f, 3.6f, -9.75f), new Vector3(5f, 0.7f, 0.15f), Palette.LampBlue, false);

            // House C - boarded up, not enterable (fills the block).
            House(t, "HouseC", 6, -20, 14, -12, Palette.SidingBlue, new[] { Geo.Window(4f) }, new Vector4[0], new Vector4[0], new Vector4[0], false);
            Geo.Box(t, "Boards", new Vector3(10f, 1.6f, -11.8f), new Vector3(1.6f, 1.4f, 0.1f), Palette.Wood, false);

            // Front yards, mailboxes, hedges.
            Geo.Box(t, "Hedge1", new Vector3(-36f, 0.5f, -9f), new Vector3(6f, 1f, 0.8f), Palette.Grass);
            Geo.Box(t, "Hedge2", new Vector3(-15f, 0.5f, -9f), new Vector3(6f, 1f, 0.8f), Palette.Grass);
            Geo.Box(t, "Mailbox", new Vector3(-28.5f, 0.6f, -8.6f), new Vector3(0.3f, 1.2f, 0.3f), Palette.DarkMetal);

            // Street furniture and wrecks for cover.
            Car(t, new Vector3(-30f, 0f, 3f), 85f, new Color(0.35f, 0.08f, 0.08f));
            Car(t, new Vector3(-18f, 0f, -3.2f), 270f, new Color(0.2f, 0.3f, 0.4f), true);
            Car(t, new Vector3(4f, 0f, 2.5f), 100f, new Color(0.5f, 0.5f, 0.45f));
            Car(t, new Vector3(12f, 0f, -4f), 250f, new Color(0.15f, 0.15f, 0.15f), true);
            Barrel(t, new Vector3(-22f, 0f, 5.5f), Palette.Rust);
            Crate(t, new Vector3(-35f, 0f, -5f), 1f, 20f);
            for (float x = -36; x <= 16; x += 13)
            {
                Lamp(t, new Vector3(x, 0.12f, -7.2f), 5.5f, 1f);
                Lamp(t, new Vector3(x + 6.5f, 0.12f, 7.2f), 5.5f, -1f);
            }

            // West barricade: overturned school bus.
            var bus = Geo.Group(t, "BusWreck", new Vector3(-37.5f, 0f, 0f), 90f).transform;
            Geo.Box(bus, "body", new Vector3(0, 1.4f, 0), new Vector3(2.5f, 2.8f, 10f), Palette.Hazard);
            Geo.Box(bus, "windows", new Vector3(0, 2.0f, 0), new Vector3(2.55f, 0.7f, 8f), Palette.Window, false);
        }

        private static void BuildBackAlley(Transform root)
        {
            var t = Geo.Group(root, "Zone1_BackAlley").transform;
            Geo.Span(t, "Alley", new Vector3(-40, 0f, 11), new Vector3(20, 0.03f, 21), Palette.Asphalt, false);

            // House B (shotgun wall buy inside).
            House(t, "HouseB", -30, 24, -16, 36, Palette.SidingBlue,
                new[] { Geo.Window(4f), Geo.Window(10f) }, new[] { Geo.Door(7f, 2f), Geo.Window(3f), Geo.Window(11f) },
                new[] { Geo.Window(6f) }, new[] { Geo.Door(8f, 1.6f) });

            // Garage with Quickhands machine in front.
            var garage = House(t, "Garage", -6, 26, 6, 34, Palette.Concrete,
                new Vector4[0], new[] { Geo.Door(6f, 8f, 2.8f) }, new Vector4[0], new Vector4[0]);
            Geo.Box(garage, "Workbench", new Vector3(0f, 0.5f, 33.2f), new Vector3(4f, 1f, 0.9f), Palette.Wood);
            Car(garage, new Vector3(-2.5f, 0f, 30f), 0f, new Color(0.3f, 0.32f, 0.2f), true);

            // Wooden fences along back yards, dumpsters, power poles.
            Fence(t, "YardFence", new Vector2(-40, 22), new Vector2(-30, 22), 1.8f, Palette.Wood);
            Fence(t, "YardFence2", new Vector2(-16, 22), new Vector2(-8, 22), 1.8f, Palette.Wood);
            Geo.Box(t, "Dumpster1", new Vector3(-6f, 0.75f, 12.2f), new Vector3(2.2f, 1.5f, 1.3f), Palette.MilitaryGreen);
            Geo.Box(t, "Dumpster2", new Vector3(14f, 0.75f, 20f), new Vector3(1.3f, 1.5f, 2.2f), Palette.Rust);
            Crate(t, new Vector3(-33f, 0f, 19f), 1f);
            Crate(t, new Vector3(-32f, 0f, 19.4f), 0.8f, 30f);
            Barrel(t, new Vector3(16f, 0f, 13f), Palette.MilitaryGreen);
            for (float x = -34; x <= 18; x += 13) Lamp(t, new Vector3(x, 0f, 21.5f), 6f, -1f);
            Geo.PointLight(t, new Vector3(-36f, 2.2f, 16f), new Color(1f, 0.6f, 0.35f), 1.5f, 6f);
        }

        private static void BuildCheckpoint(Transform root)
        {
            var t = Geo.Group(root, "Zone2_Checkpoint").transform;
            Geo.Span(t, "Pad", new Vector3(22, 0f, -6), new Vector3(46, 0.04f, 8), Palette.Concrete, false);

            // Tents.
            Tent(t, new Vector3(36f, 0f, 12f), 0f);
            Tent(t, new Vector3(48f, 0f, 2f), 90f);

            // Freestanding plywood board for the AR wall buy.
            Geo.Box(t, "BuyBoard", new Vector3(28.75f, 1.25f, 30f), new Vector3(0.2f, 2.5f, 5f), Palette.Wood);

            // Military trucks and barriers.
            Truck(t, new Vector3(32f, 0f, 22f), 15f);
            Truck(t, new Vector3(43f, 0f, -13f), 270f);
            Sandbags(t, new Vector3(24f, 0f, -4f), new Vector3(24f, 0f, -10f));
            Sandbags(t, new Vector3(26f, 0f, 6f), new Vector3(30f, 0f, 9f));
            Sandbags(t, new Vector3(42f, 0f, 30f), new Vector3(48f, 0f, 30f));
            for (int i = 0; i < 4; i++) Geo.Box(t, "Jersey", new Vector3(30f + i * 3.2f, 0.5f, -3f), new Vector3(3f, 1f, 0.6f), Palette.Concrete);
            Crate(t, new Vector3(37f, 0f, -10f), 1.1f);
            Crate(t, new Vector3(38.2f, 0f, -10f), 0.9f, 15f);
            Barrel(t, new Vector3(44f, 0f, 20f), Palette.MilitaryGreen);
            Barrel(t, new Vector3(44.8f, 0f, 20.6f), Palette.MilitaryGreen);

            // Watchtower with floodlight.
            var tower = Geo.Group(t, "Watchtower", new Vector3(53f, 0f, -12f)).transform;
            for (int i = 0; i < 4; i++)
                Geo.Box(tower, "leg", new Vector3(i % 2 == 0 ? -1.2f : 1.2f, 3f, i < 2 ? -1.2f : 1.2f), new Vector3(0.25f, 6f, 0.25f), Palette.Wood);
            Geo.Box(tower, "deck", new Vector3(0, 6.1f, 0), new Vector3(3.2f, 0.2f, 3.2f), Palette.Wood);
            Geo.Box(tower, "roof", new Vector3(0, 8.2f, 0), new Vector3(3.6f, 0.2f, 3.6f), Palette.MilitaryGreen);
            Geo.Box(tower, "flood", new Vector3(-1.4f, 6.8f, 1.4f), new Vector3(0.5f, 0.4f, 0.4f), Palette.LampBlue, false);
            Geo.PointLight(tower, new Vector3(-3f, 5.5f, 4f), new Color(0.75f, 0.85f, 1f), 2.4f, 22f);

            // Floodlights on poles + red warning beacons near the lab.
            Flood(t, new Vector3(26f, 0f, 36f));
            Flood(t, new Vector3(54f, 0f, 34f));
            Flood(t, new Vector3(30f, 0f, -14f));
            Geo.PointLight(t, new Vector3(42f, 4.2f, -17f), new Color(1f, 0.15f, 0.1f), 2f, 9f);
            Geo.Label(t, "RESTRICTED - BIOHAZARD", new Vector3(42f, 4.9f, -17.8f), 180f, 0.06f, new Color(1f, 0.85f, 0.2f));
        }

        private static void Tent(Transform t, Vector3 pos, float yaw)
        {
            var g = Geo.Group(t, "Tent", pos, yaw).transform;
            Geo.Box(g, "back", new Vector3(0, 1.2f, 3f), new Vector3(6f, 2.4f, 0.1f), Palette.Canvas);
            Geo.Box(g, "sideL", new Vector3(-3f, 1.2f, 0), new Vector3(0.1f, 2.4f, 6f), Palette.Canvas);
            Geo.Box(g, "sideR", new Vector3(3f, 1.2f, 0), new Vector3(0.1f, 2.4f, 6f), Palette.Canvas);
            Geo.Rotated(g, "roofL", new Vector3(-1.6f, 3f, 0), new Vector3(3.6f, 0.1f, 6.2f), new Vector3(0, 0, 25f), Palette.Canvas);
            Geo.Rotated(g, "roofR", new Vector3(1.6f, 3f, 0), new Vector3(3.6f, 0.1f, 6.2f), new Vector3(0, 0, -25f), Palette.Canvas);
            Geo.Box(g, "cot", new Vector3(-1.8f, 0.3f, 1f), new Vector3(0.8f, 0.6f, 2f), Palette.MilitaryGreen);
            Geo.Box(g, "table", new Vector3(1.5f, 0.45f, 2f), new Vector3(1.6f, 0.9f, 0.8f), Palette.Wood);
            Geo.PointLight(g, new Vector3(0, 2.2f, 0.5f), new Color(1f, 0.85f, 0.6f), 1.2f, 7f);
        }

        private static void Truck(Transform t, Vector3 pos, float yaw)
        {
            var g = Geo.Group(t, "Truck", pos, yaw).transform;
            Geo.Box(g, "bed", new Vector3(0, 1.2f, -1f), new Vector3(2.4f, 1.2f, 5f), Palette.MilitaryGreen);
            Geo.Box(g, "cover", new Vector3(0, 2.5f, -1f), new Vector3(2.4f, 1.4f, 5f), Palette.Canvas);
            Geo.Box(g, "cab", new Vector3(0, 1.5f, 2.6f), new Vector3(2.3f, 1.8f, 1.8f), Palette.MilitaryGreen);
            Geo.Box(g, "windshield", new Vector3(0, 1.9f, 3.51f), new Vector3(2f, 0.6f, 0.05f), Palette.Window, false);
            for (int i = 0; i < 6; i++)
            {
                float x = i % 2 == 0 ? -1.15f : 1.15f;
                float z = i < 2 ? 2.6f : (i < 4 ? -0.4f : -2.4f);
                Geo.Cylinder(g, "wheel", new Vector3(x, 0.5f, z), 0.5f, 0.35f, Palette.DarkMetal, false, new Vector3(0, 0, 90));
            }
        }

        private static void Flood(Transform t, Vector3 pos)
        {
            var g = Geo.Group(t, "Floodlight", pos).transform;
            Geo.Cylinder(g, "pole", new Vector3(0, 3.5f, 0), 0.1f, 7f, Palette.DarkMetal);
            Geo.Box(g, "lamp", new Vector3(0, 7f, 0), new Vector3(0.8f, 0.5f, 0.4f), Palette.LampBlue, false);
            Geo.PointLight(g, new Vector3(0, 6.2f, 0), new Color(0.78f, 0.86f, 1f), 2.6f, 20f);
        }

        private static void BuildLab(Transform root)
        {
            var t = Geo.Group(root, "Zone3_Lab").transform;
            const float h = 4.5f;
            var d3 = MapCatalog.Doors[3];
            var d4 = MapCatalog.Doors[4];
            Geo.Wall(t, "LabN", new Vector2(24, -18), new Vector2(58, -18), 0f, h, 0.4f, Palette.LabWhite,
                Geo.Door(d3.Position.X - 24f, d3.Width, d3.Height));
            Geo.Wall(t, "LabS", new Vector2(24, -44), new Vector2(58, -44), 0f, h, 0.4f, Palette.LabWhite);
            Geo.Wall(t, "LabW", new Vector2(24, -44), new Vector2(24, -18), 0f, h, 0.4f, Palette.LabWhite,
                Geo.Door(d4.Position.Z + 44f, d4.Width, d4.Height));
            Geo.Wall(t, "LabE", new Vector2(58, -44), new Vector2(58, -18), 0f, h, 0.4f, Palette.LabWhite);
            Geo.Span(t, "LabRoof", new Vector3(23.8f, h, -44.2f), new Vector3(58.2f, h + 0.4f, -17.8f), Palette.Concrete);
            Geo.Wall(t, "Partition", new Vector2(24, -31), new Vector2(50, -31), 0f, h, 0.25f, Palette.LabWhite,
                Geo.Door(6f, 3f, 3f), Geo.Door(22f, 3f, 3f));

            // Ceiling light panels (cool white) and emergency reds.
            foreach (var p in new[] { new Vector3(32, h - 0.1f, -24), new Vector3(50, h - 0.1f, -24), new Vector3(32, h - 0.1f, -38), new Vector3(46, h - 0.1f, -38), new Vector3(55, h - 0.1f, -31) })
            {
                Geo.Box(t, "Panel", p, new Vector3(2.4f, 0.08f, 0.8f), Palette.LampBlue, false);
                Geo.PointLight(t, p + Vector3.down * 0.5f, new Color(0.8f, 0.9f, 1f), 1.6f, 11f);
            }
            Geo.PointLight(t, new Vector3(25f, 3.6f, -34f), new Color(1f, 0.12f, 0.08f), 1.8f, 7f);

            // Specimen tanks (alien research), desks, server racks.
            for (int i = 0; i < 3; i++)
            {
                var pos = new Vector3(30f + i * 4f, 0f, -27f);
                Geo.Cylinder(t, "TankBase", pos + Vector3.up * 0.2f, 0.9f, 0.4f, Palette.DarkMetal);
                Geo.Cylinder(t, "TankGlass", pos + Vector3.up * 1.5f, 0.8f, 2.2f, Palette.Glass);
                Geo.Sphere(t, "Specimen", pos + Vector3.up * 1.5f, new Vector3(0.5f, 0.9f, 0.5f), Palette.LampAlien);
            }
            Geo.PointLight(t, new Vector3(34f, 2f, -27f), new Color(0.4f, 1f, 0.6f), 1.5f, 8f);
            Geo.Box(t, "Desk1", new Vector3(52f, 0.45f, -36f), new Vector3(2f, 0.9f, 1f), Palette.Metal);
            Geo.Box(t, "Monitor", new Vector3(52f, 1.15f, -36.2f), new Vector3(0.9f, 0.5f, 0.05f), Palette.Screen, false);
            Geo.Box(t, "Desk2", new Vector3(40f, 0.45f, -35f), new Vector3(1f, 0.9f, 2.4f), Palette.Metal);
            for (int i = 0; i < 3; i++) Geo.Box(t, "Rack", new Vector3(26.2f, 1.1f, -40f + i * 1.3f), new Vector3(0.9f, 2.2f, 1.1f), Palette.DarkMetal);
            Geo.Box(t, "Hazard", new Vector3(24.3f, 3.9f, -34f), new Vector3(0.05f, 0.4f, 4f), Palette.Hazard, false);
        }

        private static void BuildHangar(Transform root, Transform dyn, MapInstance map)
        {
            var t = Geo.Group(root, "Zone4_Hangar").transform;
            const float floor = -6f;
            const float top = -0.5f;
            var d4 = MapCatalog.Doors[4];

            Geo.Span(t, "HangarFloor", new Vector3(-20, floor - 0.5f, -50), new Vector3(10, floor, -24), Palette.HangarMetal);
            Geo.Wall(t, "HN", new Vector2(-20, -24), new Vector2(10, -24), floor, top - floor, 0.4f, Palette.HangarMetal);
            Geo.Wall(t, "HS", new Vector2(-20, -50), new Vector2(10, -50), floor, top - floor, 0.4f, Palette.HangarMetal);
            Geo.Wall(t, "HW", new Vector2(-20, -50), new Vector2(-20, -24), floor, top - floor, 0.4f, Palette.HangarMetal);
            Geo.Wall(t, "HE", new Vector2(10, -50), new Vector2(10, -24), floor, 3.5f - floor, 0.4f, Palette.HangarMetal,
                Geo.Door(d4.Position.Z + 50f, d4.Width, 3.5f));

            // Ceiling with a launch opening above the saucer (outside the suburb slab region).
            Geo.Span(t, "CeilWest", new Vector3(-20, top, -50), new Vector3(-11, 0f, -26), Palette.Concrete);
            Geo.Span(t, "CeilEast", new Vector3(1, top, -50), new Vector3(10, 0f, -26), Palette.Concrete);
            Geo.Span(t, "CeilSouth", new Vector3(-11, top, -50), new Vector3(1, 0f, -43), Palette.Concrete);
            Geo.Span(t, "CeilNorth", new Vector3(-11, top, -31), new Vector3(1, 0f, -26), Palette.Concrete);

            // Ramp corridor from the lab's west door down to the hangar.
            float zc = d4.Position.Z;
            Geo.Span(t, "Landing", new Vector3(21.8f, -0.5f, zc - 2f), new Vector3(24f, 0f, zc + 2f), Palette.HangarMetal);
            float rampLen = Mathf.Sqrt(11.8f * 11.8f + 6f * 6f);
            float angle = Mathf.Atan2(6f, 11.8f) * Mathf.Rad2Deg;
            Geo.Rotated(t, "Ramp", new Vector3(15.9f, -3.25f, zc), new Vector3(rampLen, 0.5f, 4f), new Vector3(0, 0, angle), Palette.Hazard);
            Geo.Wall(t, "CorrN", new Vector2(10, zc + 2.2f), new Vector2(24, zc + 2.2f), floor, 3.5f - floor, 0.4f, Palette.Concrete);
            Geo.Wall(t, "CorrS", new Vector2(10, zc - 2.2f), new Vector2(24, zc - 2.2f), floor, 3.5f - floor, 0.4f, Palette.Concrete);
            Geo.Span(t, "CorrRoof", new Vector3(10, 3.5f, zc - 2.4f), new Vector3(24, 3.9f, zc + 2.4f), Palette.Concrete);
            Geo.Span(t, "CorrFloorLow", new Vector3(9.8f, floor - 0.5f, zc - 2.2f), new Vector3(10.4f, floor, zc + 2.2f), Palette.HangarMetal);
            Geo.PointLight(t, new Vector3(17f, 1.5f, zc), new Color(1f, 0.15f, 0.1f), 1.6f, 8f);

            // Alien and emergency lighting.
            foreach (var p in new[] { new Vector3(-17, -1.2f, -27), new Vector3(7, -1.2f, -27), new Vector3(-17, -1.2f, -47), new Vector3(7, -1.2f, -47) })
            {
                Geo.Box(t, "AlienLamp", p, new Vector3(1.2f, 0.2f, 1.2f), Palette.LampAlien, false);
                Geo.PointLight(t, p + Vector3.down * 0.6f, new Color(0.35f, 1f, 0.55f), 1.8f, 13f);
            }
            Geo.Label(t, "HANGAR 51-B  //  NO UNAUTHORISED LAUNCH", new Vector3(-5f, -2f, -24.3f), 180f, 0.07f, new Color(0.5f, 1f, 0.7f));
            for (int i = 0; i < 5; i++) Crate(t, new Vector3(-17f + (i % 2) * 1.3f, floor, -38f - i * 1.6f), 1.2f, i * 12f);
            Geo.Box(t, "Gantry", new Vector3(5f, floor + 2.5f, -45f), new Vector3(6f, 5f, 1f), Palette.DarkMetal);

            // Saucer (dynamic so it can rotate and launch).
            var ufoRoot = Geo.Group(dyn, "UFO", Geo.V(MapCatalog.UfoPosition)).transform;
            var ufo = ufoRoot.gameObject.AddComponent<UfoView>();
            var saucer = Geo.Group(ufoRoot, "Saucer", new Vector3(0, 3f, 0)).transform;
            Geo.Sphere(saucer, "Hull", Vector3.zero, new Vector3(10f, 1.1f, 10f), Palette.UfoHull, true);
            Geo.Sphere(saucer, "Dome", new Vector3(0, 0.6f, 0), new Vector3(3.6f, 2f, 3.6f), Palette.Glass);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                Geo.Sphere(saucer, "Rim", new Vector3(Mathf.Cos(a) * 4.6f, -0.1f, Mathf.Sin(a) * 4.6f), Vector3.one * 0.35f, Palette.LampAlien);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI * 2f / 3f + 0.5f;
                Geo.Box(ufoRoot, "Leg", new Vector3(Mathf.Cos(a) * 3.8f, 1.3f, Mathf.Sin(a) * 3.8f), new Vector3(0.25f, 2.6f, 0.25f), Palette.DarkMetal);
            }
            var glow = Geo.PointLight(ufoRoot, new Vector3(0, 1.5f, 0), new Color(0.4f, 1f, 0.7f), 1.2f, 14f);
            var beam = Geo.Cylinder(ufoRoot, "BoardingBeam", new Vector3(0, 1.4f, 0), MapCatalog.BoardingRadius * 0.6f, 2.8f, Palette.Trans("beam", new Color(0.4f, 1f, 0.7f, 0.18f)), false);
            beam.SetActive(false);
            var ring = Geo.Cylinder(ufoRoot, "BoardingRing", new Vector3(0, 0.02f, 0), MapCatalog.BoardingRadius, 0.02f, Palette.Trans("ring", new Color(0.4f, 1f, 0.7f, 0.25f)), false);
            ring.name = "BoardingRing";

            var hatchRoot = Geo.Group(dyn, "Hatch", new Vector3(-5f, -0.25f, -37f)).transform;
            var hatchA = Geo.Box(hatchRoot, "HatchA", new Vector3(-3f, 0, 0), new Vector3(6f, 0.5f, 12f), Palette.Hazard);
            var hatchB = Geo.Box(hatchRoot, "HatchB", new Vector3(3f, 0, 0), new Vector3(6f, 0.5f, 12f), Palette.Hazard);

            ufo.Saucer = saucer;
            ufo.Glow = glow;
            ufo.BeamCone = beam.transform;
            ufo.HatchA = hatchA.transform;
            ufo.HatchB = hatchB.transform;
            map.Ufo = ufo;
        }

        private static void BuildBoundaries(Transform root)
        {
            var t = Geo.Group(root, "Boundaries").transform;
            var d0 = MapCatalog.Doors[0];
            var d1 = MapCatalog.Doors[1];
            var d2 = MapCatalog.Doors[2];

            // Suburb outer fences.
            Fence(t, "WestFence", new Vector2(-40, -26), new Vector2(-40, 40), BoundaryH, Palette.DarkWood);
            Fence(t, "SouthFence", new Vector2(-40, -26), new Vector2(20, -26), BoundaryH, Palette.DarkWood);
            Fence(t, "NorthFence", new Vector2(-40, 40), new Vector2(20, 40), BoundaryH, Palette.DarkWood);
            // Street / alley divider with the Alley Gate opening.
            Fence(t, "AlleyFence", new Vector2(-40, 8), new Vector2(20, 8), 2.6f, Palette.Wood,
                Geo.Door(d0.Position.X + 40f, d0.Width, d0.Height + 0.5f));

            // Checkpoint perimeter: concrete T-walls.
            Geo.Wall(t, "CheckpointWest", new Vector2(20, -26), new Vector2(20, 40), 0f, BoundaryH, 0.6f, Palette.Concrete,
                Geo.Door(d1.Position.Z + 26f, d1.Width, d1.Height), Geo.Door(d2.Position.Z + 26f, d2.Width, d2.Height));
            Geo.Wall(t, "CheckpointNorth", new Vector2(20, 40), new Vector2(58, 40), 0f, BoundaryH, 0.6f, Palette.Concrete);
            Geo.Wall(t, "CheckpointEast", new Vector2(58, -18), new Vector2(58, 40), 0f, BoundaryH, 0.6f, Palette.Concrete);
            Geo.Wall(t, "CheckpointSouthStub", new Vector2(20, -18), new Vector2(24, -18), 0f, BoundaryH, 0.6f, Palette.Concrete);
            for (float z = -24; z <= 38; z += 4) Geo.Box(t, "Wire", new Vector3(20f, BoundaryH + 0.25f, z), new Vector3(0.1f, 0.5f, 3.6f), Palette.Metal, false);
        }

        // ------------------------------------------------------------------ interactables

        private static DoorView BuildDoor(Transform parent, DoorDef d)
        {
            float yaw = d.AlongZ ? 90f : 0f;
            var root = Geo.Group(parent, "Door_" + d.Id, Geo.V(d.Position), yaw).transform;
            var view = root.gameObject.AddComponent<DoorView>();
            var panel = Geo.Group(root, "Panel").transform;

            // Style per door; all share: solid blocker, red lock stripe, price sign.
            Material mat = d.Id == 3 || d.Id == 4 ? Palette.HangarMetal : (d.Id == 1 ? Palette.MilitaryGreen : Palette.Wood);
            Geo.Box(panel, "Blocker", new Vector3(0, d.Height * 0.5f, 0), new Vector3(d.Width, d.Height, 0.3f), mat);
            if (d.Id == 0 || d.Id == 2)
            {
                for (int i = 0; i < 4; i++)
                    Geo.Rotated(panel, "Plank", new Vector3(0, 0.5f + i * 0.7f, 0.2f), new Vector3(d.Width + 0.3f, 0.25f, 0.06f), new Vector3(0, 0, i % 2 == 0 ? 8f : -8f), Palette.DarkWood, false);
            }
            else
            {
                Geo.Box(panel, "HazardTop", new Vector3(0, d.Height - 0.25f, 0.17f), new Vector3(d.Width, 0.3f, 0.04f), Palette.Hazard, false);
                Geo.Box(panel, "HazardTop2", new Vector3(0, d.Height - 0.25f, -0.17f), new Vector3(d.Width, 0.3f, 0.04f), Palette.Hazard, false);
            }
            Geo.Box(panel, "LockStripe", new Vector3(0, 1.2f, 0.18f), new Vector3(d.Width * 0.6f, 0.08f, 0.02f), Palette.LockedStripe, false);
            Geo.Box(panel, "LockStripe2", new Vector3(0, 1.2f, -0.18f), new Vector3(d.Width * 0.6f, 0.08f, 0.02f), Palette.LockedStripe, false);
            string sign = d.Name + "\n" + d.Cost;
            Geo.Label(panel, sign, new Vector3(0, 1.8f, 0.2f), 180f, 0.05f, new Color(1f, 0.9f, 0.7f));
            Geo.Label(panel, sign, new Vector3(0, 1.8f, -0.2f), 0f, 0.05f, new Color(1f, 0.9f, 0.7f));
            var lockLight = Geo.PointLight(root, new Vector3(0, d.Height + 0.4f, 0), new Color(1f, 0.25f, 0.15f), 1.4f, 6f);

            var obstacle = root.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0, d.Height * 0.5f, 0);
            obstacle.size = new Vector3(d.Width, d.Height, 0.6f);
            obstacle.carving = true;
            obstacle.carveOnlyStationary = false;

            view.Init(d.Id, panel, d.Height, obstacle, lockLight);

            // Interaction points on both sides of the door share one id.
            var interact = root.gameObject.AddComponent<Interactable>();
            interact.Register(d.Id, InteractableKind.Door, d.Id);
            interact.HalfWidth = d.Width * 0.5f;
            return view;
        }

        private static void BuildStation(Transform parent, StationDef s)
        {
            var root = Geo.Group(parent, s.Kind + "_" + s.Id, Geo.V(s.Position), s.Yaw).transform;
            var interact = root.gameObject.AddComponent<Interactable>();
            interact.Register(s.Id, s.Kind, s.Param);
            if (s.Kind == InteractableKind.WallBuy) interact.HalfWidth = 0.8f;
            // Visual body sits behind the interaction point (local -Z).
            var body = Geo.Group(root, "Body").transform;

            switch (s.Kind)
            {
                case InteractableKind.WallBuy:
                {
                    // Chalk outline panel on the wall with the weapon silhouette and price.
                    var weapon = (WeaponId)s.Param;
                    Geo.Box(body, "Board", new Vector3(0, 0, -0.08f), new Vector3(1.6f, 0.9f, 0.04f), Palette.DarkWood, false);
                    Geo.Box(body, "FrameT", new Vector3(0, 0.45f, -0.05f), new Vector3(1.6f, 0.04f, 0.02f), Palette.Chalk, false);
                    Geo.Box(body, "FrameB", new Vector3(0, -0.45f, -0.05f), new Vector3(1.6f, 0.04f, 0.02f), Palette.Chalk, false);
                    Geo.Box(body, "FrameL", new Vector3(-0.8f, 0, -0.05f), new Vector3(0.04f, 0.9f, 0.02f), Palette.Chalk, false);
                    Geo.Box(body, "FrameR", new Vector3(0.8f, 0, -0.05f), new Vector3(0.04f, 0.9f, 0.02f), Palette.Chalk, false);
                    var model = WeaponModels.Build(body, weapon, false, 1.6f, Palette.Chalk);
                    model.transform.localPosition = new Vector3(0, 0.05f, -0.03f);
                    model.transform.localRotation = Quaternion.Euler(0, 90f, 0);
                    // Flatten the model's width (its local X) so it reads as a chalk outline on the board.
                    model.transform.localScale = new Vector3(0.15f, 1.6f, 1.6f);
                    var stats = WeaponCatalog.Get(weapon);
                    Geo.Label(body, stats.DisplayName + "  " + stats.WallPrice, new Vector3(0, -0.32f, -0.03f), 0f, 0.025f, new Color(1f, 0.95f, 0.8f));
                    Geo.PointLight(root, new Vector3(0, 0.9f, 0.6f), new Color(1f, 0.85f, 0.6f), 1.3f, 4.5f);
                    break;
                }
                case InteractableKind.Perk:
                {
                    var perk = (PerkId)s.Param;
                    Color c = PerkColor(perk);
                    Geo.Box(body, "Machine", new Vector3(0, 1.1f, -0.75f), new Vector3(1.2f, 2.2f, 0.8f), Palette.Tint(c));
                    Geo.Box(body, "Panel", new Vector3(0, 1.4f, -0.34f), new Vector3(0.9f, 1.1f, 0.03f), Palette.Emit("perk" + (int)perk, c, c * 2.2f), false);
                    Geo.Label(body, PerkCatalog.Name(perk) + "\n" + PerkCatalog.Price(perk), new Vector3(0, 2.4f, -0.34f), 180f, 0.03f, Color.white);
                    Geo.PointLight(root, new Vector3(0, 1.8f, 0.4f), c, 1.6f, 5f);
                    break;
                }
                case InteractableKind.AmmoCrate:
                    Geo.Box(body, "Crate", new Vector3(0, 0.4f, -0.6f), new Vector3(1.4f, 0.8f, 0.8f), Palette.MilitaryGreen);
                    Geo.Box(body, "Stripe", new Vector3(0, 0.55f, -0.19f), new Vector3(1.4f, 0.12f, 0.02f), Palette.Hazard, false);
                    Geo.Label(body, "AMMO " + PurchaseRules.AmmoCratePrice, new Vector3(0, 0.95f, -0.6f), 0f, 0.03f, new Color(1f, 0.9f, 0.4f));
                    Geo.PointLight(root, new Vector3(0, 1.6f, 0.3f), new Color(1f, 0.85f, 0.5f), 1f, 4f);
                    break;
                case InteractableKind.UpgradeBench:
                    Geo.Box(body, "Bench", new Vector3(0, 0.5f, -0.6f), new Vector3(2.2f, 1f, 1f), Palette.DarkMetal);
                    Geo.Sphere(body, "Core", new Vector3(0, 1.4f, -0.6f), new Vector3(0.6f, 0.6f, 0.6f), Palette.LampPurple);
                    Geo.Cylinder(body, "Ring", new Vector3(0, 1.4f, -0.6f), 0.55f, 0.03f, Palette.UfoHull, false, new Vector3(90, 0, 0));
                    Geo.Label(body, "XENO-FORGE " + PurchaseRules.UpgradePrice, new Vector3(0, 2.1f, -0.6f), 0f, 0.03f, new Color(0.85f, 0.6f, 1f));
                    Geo.PointLight(root, new Vector3(0, 1.8f, 0f), new Color(0.75f, 0.4f, 1f), 1.8f, 6f);
                    break;
                case InteractableKind.EscapeConsole:
                    Geo.Box(body, "Console", new Vector3(0, 0.6f, -0.6f), new Vector3(1.6f, 1.2f, 0.7f), Palette.UfoHull);
                    Geo.Rotated(body, "Screen", new Vector3(0, 1.3f, -0.45f), new Vector3(1.4f, 0.6f, 0.05f), new Vector3(-25, 0, 0), Palette.Screen, false);
                    Geo.Label(body, "LAUNCH CONTROL", new Vector3(0, 1.85f, -0.5f), 0f, 0.03f, new Color(0.5f, 1f, 0.7f));
                    Geo.PointLight(root, new Vector3(0, 1.8f, 0.3f), new Color(0.4f, 1f, 0.6f), 1.5f, 5f);
                    break;
            }
            // Labels are authored facing -Z by TextMesh convention; face them toward the player.
            foreach (var label in body.GetComponentsInChildren<TextMesh>())
                label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static BoxView BuildBox(Transform parent)
        {
            var root = Geo.Group(parent, "SupplyDrop").transform;
            var view = root.gameObject.AddComponent<BoxView>();
            var interact = root.gameObject.AddComponent<Interactable>();
            interact.Register(MapCatalog.MysteryBoxInteractableId, InteractableKind.MysteryBox, 0);
            var body = Geo.Group(root, "Body", new Vector3(0, 0, -0.7f)).transform;
            Geo.Box(body, "Crate", new Vector3(0, 0.4f, 0), new Vector3(1.8f, 0.8f, 0.9f), Palette.MilitaryGreen);
            Geo.Box(body, "Seam", new Vector3(0, 0.8f, 0), new Vector3(1.82f, 0.05f, 0.92f), Palette.LampBlue, false);
            var lidPivot = Geo.Group(body, "LidPivot", new Vector3(0, 0.82f, -0.45f)).transform;
            Geo.Box(lidPivot, "Lid", new Vector3(0, 0.05f, 0.45f), new Vector3(1.8f, 0.1f, 0.9f), Palette.MilitaryGreen, false);
            Geo.Label(body, "SUPPLY DROP " + MysteryBoxRules.Cost, new Vector3(0, 0.45f, 0.47f), 180f, 0.025f, new Color(0.6f, 0.85f, 1f));
            view.OfferAnchor = Geo.Group(body, "Offer", new Vector3(0, 1.1f, 0)).transform;
            view.Lid = lidPivot;
            view.Beam = Geo.PointLight(root, new Vector3(0, 2.2f, -0.4f), new Color(0.5f, 0.75f, 1f), 2f, 7f);
            // The crate moves between locations, so it carves the navmesh instead of being baked into it.
            var obstacle = body.gameObject.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0, 0.5f, 0);
            obstacle.size = new Vector3(1.9f, 1f, 1f);
            obstacle.carving = true;
            var def = MapCatalog.BoxLocations[0];
            root.position = Geo.V(def.Position);
            root.rotation = Quaternion.Euler(0, def.Yaw, 0);
            return view;
        }

        private static Color PerkColor(PerkId perk)
        {
            switch (perk)
            {
                case PerkId.IronHide: return new Color(0.75f, 0.12f, 0.1f);
                case PerkId.Quickhands: return new Color(0.1f, 0.6f, 0.25f);
                case PerkId.RapidFire: return new Color(0.85f, 0.55f, 0.1f);
                default: return new Color(0.15f, 0.45f, 0.85f);
            }
        }
    }
}
