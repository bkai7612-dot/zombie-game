namespace UZS.Core
{
    public enum InteractableKind : byte
    {
        Door = 0,
        WallBuy = 1,
        Perk = 2,
        AmmoCrate = 3,
        MysteryBox = 4,
        UpgradeBench = 5,
        EscapeConsole = 6,
    }

    public enum QuoteKind : byte
    {
        Unavailable = 0,
        NewWeapon,
        Ammo,
        OpenDoor,
        Perk,
        AmmoCrate,
        Upgrade,
        BoxRoll,
        BoxTake,
        Contribute,
    }

    /// <summary>The authority's answer to "what would interacting with this cost me right now?"</summary>
    public struct PurchaseQuote
    {
        public QuoteKind Kind;
        public int Cost;
        public string Reason;

        public bool Available => Kind != QuoteKind.Unavailable;

        public static PurchaseQuote No(string reason) => new PurchaseQuote { Kind = QuoteKind.Unavailable, Reason = reason };
        public static PurchaseQuote Of(QuoteKind kind, int cost) => new PurchaseQuote { Kind = kind, Cost = cost };

        public bool Affordable(int points) => Available && points >= Cost;
    }

    /// <summary>
    /// Pure price/eligibility rules. The same functions build client-side prompts and are re-evaluated by the
    /// authority when a request arrives, so a client can never choose its own price.
    /// </summary>
    public static class PurchaseRules
    {
        public const int AmmoCratePrice = 600;
        public const int UpgradePrice = 5000;

        public static PurchaseQuote WallBuy(Loadout loadout, WeaponId weapon)
        {
            if (!WeaponCatalog.TryGet(weapon, out var stats) || stats.WallPrice <= 0)
                return PurchaseQuote.No("Not for sale");
            int slot = loadout.SlotOf(weapon);
            if (slot < 0) return PurchaseQuote.Of(QuoteKind.NewWeapon, stats.WallPrice);
            if (loadout.IsFull(slot)) return PurchaseQuote.No("Ammo full");
            int price = loadout[slot].Upgraded ? WeaponCatalog.UpgradedAmmoPrice : stats.AmmoPrice;
            return PurchaseQuote.Of(QuoteKind.Ammo, price);
        }

        public static PurchaseQuote Door(int doorId, ulong unlockedMask)
        {
            if (!MapCatalog.TryGetDoor(doorId, out var door)) return PurchaseQuote.No("Unknown door");
            if ((unlockedMask & (1UL << doorId)) != 0) return PurchaseQuote.No("Already open");
            return PurchaseQuote.Of(QuoteKind.OpenDoor, door.Cost);
        }

        public static PurchaseQuote Perk(PerkId owned, PerkId perk)
        {
            if ((owned & perk) != 0) return PurchaseQuote.No("Already owned");
            return PurchaseQuote.Of(QuoteKind.Perk, PerkCatalog.Price(perk));
        }

        public static PurchaseQuote AmmoCrate(Loadout loadout)
        {
            for (int i = 0; i < Loadout.SlotCount; i++)
                if (!loadout.IsFull(i)) return PurchaseQuote.Of(QuoteKind.AmmoCrate, AmmoCratePrice);
            return PurchaseQuote.No("Ammo full");
        }

        public static PurchaseQuote Upgrade(Loadout loadout)
        {
            var active = loadout.Active;
            if (active.IsEmpty) return PurchaseQuote.No("No weapon");
            if (active.Upgraded) return PurchaseQuote.No("Already upgraded");
            return PurchaseQuote.Of(QuoteKind.Upgrade, UpgradePrice);
        }
    }
}
