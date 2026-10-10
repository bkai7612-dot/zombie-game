using System;

namespace UZS.Core
{
    /// <summary>One weapon slot. Plain data so it can be mirrored into network variables.</summary>
    public struct WeaponSlot
    {
        public WeaponId Weapon;
        public bool Upgraded;
        public int Magazine;
        public int Reserve;

        public bool IsEmpty => Weapon == WeaponId.None;
    }

    /// <summary>
    /// Authoritative weapon inventory of one player: two slots plus an always-available knife.
    /// Only the authority mutates this; clients see a mirrored copy.
    /// </summary>
    public sealed class Loadout
    {
        public const int SlotCount = 2;

        private readonly WeaponSlot[] slots = new WeaponSlot[SlotCount];

        public int ActiveSlot { get; private set; }

        public WeaponSlot this[int index] => slots[index];

        public WeaponSlot Active => slots[ActiveSlot];

        public static Loadout CreateStarter()
        {
            var l = new Loadout();
            l.Give(WeaponId.Pistol, false);
            return l;
        }

        public void Restore(WeaponSlot s0, WeaponSlot s1, int active)
        {
            slots[0] = Sanitize(s0);
            slots[1] = Sanitize(s1);
            ActiveSlot = active == 1 ? 1 : 0;
            if (slots[ActiveSlot].IsEmpty && !slots[1 - ActiveSlot].IsEmpty) ActiveSlot = 1 - ActiveSlot;
        }

        public int SlotOf(WeaponId id)
        {
            if (id == WeaponId.None) return -1;
            for (int i = 0; i < SlotCount; i++)
                if (slots[i].Weapon == id) return i;
            return -1;
        }

        public bool Owns(WeaponId id) => SlotOf(id) >= 0;

        public bool SetActiveSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount || slots[slot].IsEmpty) return false;
            ActiveSlot = slot;
            return true;
        }

        /// <summary>
        /// Gives a weapon. If already owned, its ammo is refilled. Otherwise it fills an empty slot,
        /// or replaces the active weapon when both slots are full. Returns the weapon that was dropped (or None).
        /// </summary>
        public WeaponId Give(WeaponId id, bool upgraded)
        {
            if (!WeaponCatalog.TryGet(id, out _)) throw new ArgumentException("Cannot give " + id);
            int owned = SlotOf(id);
            if (owned >= 0)
            {
                if (upgraded) slots[owned].Upgraded = true;
                Refill(owned);
                ActiveSlot = owned;
                return WeaponId.None;
            }

            int target = -1;
            for (int i = 0; i < SlotCount; i++)
                if (slots[i].IsEmpty) { target = i; break; }

            WeaponId dropped = WeaponId.None;
            if (target < 0)
            {
                target = ActiveSlot;
                dropped = slots[target].Weapon;
            }

            slots[target] = new WeaponSlot { Weapon = id, Upgraded = upgraded };
            Refill(target);
            ActiveSlot = target;
            return dropped;
        }

        public void Refill(int slot)
        {
            var s = slots[slot];
            if (s.IsEmpty) return;
            s.Magazine = WeaponCatalog.MagazineSize(s.Weapon, s.Upgraded);
            s.Reserve = WeaponCatalog.MaxReserve(s.Weapon, s.Upgraded);
            slots[slot] = s;
        }

        public void RefillAll()
        {
            for (int i = 0; i < SlotCount; i++) Refill(i);
        }

        public bool IsFull(int slot)
        {
            var s = slots[slot];
            if (s.IsEmpty) return true;
            return s.Magazine >= WeaponCatalog.MagazineSize(s.Weapon, s.Upgraded) &&
                   s.Reserve >= WeaponCatalog.MaxReserve(s.Weapon, s.Upgraded);
        }

        public bool Upgrade(int slot)
        {
            var s = slots[slot];
            if (s.IsEmpty || s.Upgraded) return false;
            s.Upgraded = true;
            slots[slot] = s;
            Refill(slot);
            return true;
        }

        public bool TryConsumeRound(int slot)
        {
            var s = slots[slot];
            if (s.IsEmpty || s.Magazine <= 0) return false;
            s.Magazine--;
            slots[slot] = s;
            return true;
        }

        public bool CanReload(int slot)
        {
            var s = slots[slot];
            return !s.IsEmpty && s.Reserve > 0 && s.Magazine < WeaponCatalog.MagazineSize(s.Weapon, s.Upgraded);
        }

        /// <summary>Moves rounds from reserve into the magazine. Returns rounds moved.</summary>
        public int CompleteReload(int slot)
        {
            if (!CanReload(slot)) return 0;
            var s = slots[slot];
            int need = WeaponCatalog.MagazineSize(s.Weapon, s.Upgraded) - s.Magazine;
            int moved = Math.Min(need, s.Reserve);
            s.Magazine += moved;
            s.Reserve -= moved;
            slots[slot] = s;
            return moved;
        }

        private static WeaponSlot Sanitize(WeaponSlot s)
        {
            if (s.IsEmpty || !WeaponCatalog.TryGet(s.Weapon, out _)) return default;
            s.Magazine = Math.Max(0, Math.Min(s.Magazine, WeaponCatalog.MagazineSize(s.Weapon, s.Upgraded)));
            s.Reserve = Math.Max(0, Math.Min(s.Reserve, WeaponCatalog.MaxReserve(s.Weapon, s.Upgraded)));
            return s;
        }
    }
}
