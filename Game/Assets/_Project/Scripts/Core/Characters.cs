namespace UZS.Core
{
    public enum Headgear : byte
    {
        None,
        CombatHelmet,
        BallCap,
        Beret,
        Hood,
        HardHat,
        WideBrimHat,
        Headset,
        Goggles,
        Beanie,
    }

    /// <summary>Simple RGB colour without engine dependencies.</summary>
    public readonly struct Rgb
    {
        public readonly float R, G, B;
        public Rgb(float r, float g, float b) { R = r; G = g; B = b; }

        public static Rgb Hex(int hex) => new Rgb(((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f);
    }

    public sealed class CharacterDef
    {
        public int Id;
        public string Name;
        public string Role;
        public string Bio;
        public Rgb Jacket;
        public Rgb Trousers;
        public Rgb Skin;
        public Rgb Accent;
        public Headgear Headgear;
        public bool Backpack;
        public float HeightScale;
        public float BuildScale;
    }

    /// <summary>
    /// Ten original survivors. Their in-game models are assembled from primitives at runtime
    /// (clearly placeholder art) using these colour and silhouette parameters.
    /// </summary>
    public static class CharacterCatalog
    {
        public static readonly CharacterDef[] All =
        {
            new CharacterDef { Id = 0, Name = "Dana Reyes", Role = "Sheriff's Deputy", Bio = "Last officer in Hollow Pines. Refuses to leave anyone behind.",
                Jacket = Rgb.Hex(0x4A5A3A), Trousers = Rgb.Hex(0x2B2B2B), Skin = Rgb.Hex(0xC08A62), Accent = Rgb.Hex(0xD8B23A), Headgear = Headgear.WideBrimHat, Backpack = false, HeightScale = 1.0f, BuildScale = 1.0f },
            new CharacterDef { Id = 1, Name = "Marcus Hale", Role = "Army Corporal", Bio = "Checkpoint guard who saw what came out of the lab.",
                Jacket = Rgb.Hex(0x55613F), Trousers = Rgb.Hex(0x4B5236), Skin = Rgb.Hex(0x6E4A33), Accent = Rgb.Hex(0x2F3524), Headgear = Headgear.CombatHelmet, Backpack = true, HeightScale = 1.05f, BuildScale = 1.1f },
            new CharacterDef { Id = 2, Name = "Dr. Ingrid Vos", Role = "Xenobiologist", Bio = "Wrote the containment protocol. Knows it failed.",
                Jacket = Rgb.Hex(0xE6E6E0), Trousers = Rgb.Hex(0x3A4250), Skin = Rgb.Hex(0xE8C4A8), Accent = Rgb.Hex(0x3FD17A), Headgear = Headgear.Goggles, Backpack = false, HeightScale = 0.97f, BuildScale = 0.9f },
            new CharacterDef { Id = 3, Name = "Tommy Burke", Role = "Mechanic", Bio = "Can hotwire anything. Including, he hopes, a flying saucer.",
                Jacket = Rgb.Hex(0x2E4A6B), Trousers = Rgb.Hex(0x23324A), Skin = Rgb.Hex(0xD9A57E), Accent = Rgb.Hex(0xC24A2B), Headgear = Headgear.BallCap, Backpack = false, HeightScale = 1.02f, BuildScale = 1.05f },
            new CharacterDef { Id = 4, Name = "Priya Nair", Role = "Radio Operator", Bio = "Kept the emergency channel open for nine straight nights.",
                Jacket = Rgb.Hex(0x6B4E2E), Trousers = Rgb.Hex(0x2E2A26), Skin = Rgb.Hex(0xA9774F), Accent = Rgb.Hex(0xE0E0E0), Headgear = Headgear.Headset, Backpack = true, HeightScale = 0.95f, BuildScale = 0.92f },
            new CharacterDef { Id = 5, Name = "Walt Hendricks", Role = "Retired Hunter", Bio = "Old, stubborn, and very good with a rifle.",
                Jacket = Rgb.Hex(0x8A3B2A), Trousers = Rgb.Hex(0x3F3A2E), Skin = Rgb.Hex(0xDDB596), Accent = Rgb.Hex(0x1F1F1F), Headgear = Headgear.Beanie, Backpack = false, HeightScale = 0.98f, BuildScale = 1.05f },
            new CharacterDef { Id = 6, Name = "Sgt. Ama Owusu", Role = "Combat Medic", Bio = "Patches people up, then puts zombies down.",
                Jacket = Rgb.Hex(0x5E6B4A), Trousers = Rgb.Hex(0x4A533A), Skin = Rgb.Hex(0x5A3A26), Accent = Rgb.Hex(0xC8282D), Headgear = Headgear.Beret, Backpack = true, HeightScale = 1.0f, BuildScale = 0.97f },
            new CharacterDef { Id = 7, Name = "Eli Park", Role = "Delivery Driver", Bio = "Was on his last route when the sirens started.",
                Jacket = Rgb.Hex(0xD27D2C), Trousers = Rgb.Hex(0x34495E), Skin = Rgb.Hex(0xE3BC94), Accent = Rgb.Hex(0x2C3E50), Headgear = Headgear.Hood, Backpack = true, HeightScale = 1.0f, BuildScale = 0.95f },
            new CharacterDef { Id = 8, Name = "Rosa Delgado", Role = "Construction Foreman", Bio = "Built half the houses on Main Street. Knows every shortcut.",
                Jacket = Rgb.Hex(0xE3C02B), Trousers = Rgb.Hex(0x3B4B5C), Skin = Rgb.Hex(0xB98463), Accent = Rgb.Hex(0xE86A1E), Headgear = Headgear.HardHat, Backpack = false, HeightScale = 0.96f, BuildScale = 1.0f },
            new CharacterDef { Id = 9, Name = "Specimen K-7", Role = "Escaped Test Subject", Bio = "Human. Mostly. The aliens left a mark - and a way home.",
                Jacket = Rgb.Hex(0x2A2F3A), Trousers = Rgb.Hex(0x1E222A), Skin = Rgb.Hex(0x9FB5A0), Accent = Rgb.Hex(0x5CFFB0), Headgear = Headgear.None, Backpack = false, HeightScale = 1.04f, BuildScale = 0.9f },
        };

        public static int Count => All.Length;

        public static CharacterDef Get(int id) => All[Clamp(id)];

        public static bool IsValid(int id) => id >= 0 && id < All.Length;

        public static int Clamp(int id) => id < 0 ? 0 : (id >= All.Length ? All.Length - 1 : id);
    }
}
