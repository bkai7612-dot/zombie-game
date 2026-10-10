using System;

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
        /// <summary>Minimum crate price; the crate charges each non-full slot its normal refill price.</summary>
        public const int AmmoCrateMinimum = 600;
        public const int UpgradePrice = 5000;

        public static PurchaseQuote WallBuy(Loadout loadout, WeaponId weapon)
        {
            if (!WeaponCatalog.TryGet(weapon, out var stats) || stats.WallPrice <= 0)
                return PurchaseQuote.No("Not for sale");
            int slot = loadout.SlotOf(weapon);
            if (slot < 0) return PurchaseQuote.Of(QuoteKind.NewWeapon, stats.WallPrice);
            if (loadout.IsFull(slot)) return PurchaseQuote.No("Ammo full");
            return PurchaseQuote.Of(QuoteKind.Ammo, WeaponCatalog.AmmoPriceFor(loadout[slot]));
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

        /// <summary>
        /// Refills every weapon in one go. It charges the same per-slot prices as the walls (so upgraded guns are
        /// never cheaper here) and is mainly the only way to restock the starter pistol and box weapons.
        /// </summary>
        public static PurchaseQuote AmmoCrate(Loadout loadout)
        {
            int total = 0;
            for (int i = 0; i < Loadout.SlotCount; i++)
                if (!loadout.IsFull(i)) total += WeaponCatalog.AmmoPriceFor(loadout[i]);
            if (total == 0) return PurchaseQuote.No("Ammo full");
            return PurchaseQuote.Of(QuoteKind.AmmoCrate, Math.Max(AmmoCrateMinimum, total));
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
