using System.Collections.Generic;
using UnityEngine;
using UZS.Core;

namespace UZS.Gameplay
{
    /// <summary>
    /// A purchasable/usable map object. Built identically on every peer by <see cref="MapBuilder"/>, so it is
    /// addressed over the network by its stable <see cref="Id"/> instead of being a NetworkObject.
    /// The authority re-checks distance and price itself; see <see cref="MatchState"/>.
    /// </summary>
    public sealed class Interactable : MonoBehaviour
    {
        private static readonly Dictionary<int, Interactable> Registry = new Dictionary<int, Interactable>();

        public int Id;
        public InteractableKind Kind;
        public int Param;

        /// <summary>Half-length of the interaction segment along local X (doors and wall boards are wide).</summary>
        public float HalfWidth;

        /// <summary>World position the player must be near (in front of the object).</summary>
        public Vector3 Point => transform.position;

        /// <summary>Distance from <paramref name="position"/> to the interaction segment. Used by client prompts and server validation.</summary>
        public float DistanceTo(Vector3 position)
        {
            var local = transform.InverseTransformPoint(position);
            local.x = Mathf.Max(0f, Mathf.Abs(local.x) - HalfWidth);
            return local.magnitude;
        }

        public static IEnumerable<Interactable> All => Registry.Values;

        public static bool TryGet(int id, out Interactable interactable) => Registry.TryGetValue(id, out interactable) && interactable != null;

        public void Register(int id, InteractableKind kind, int param)
        {
            Id = id;
            Kind = kind;
            Param = param;
            Registry[id] = this;
        }

        private void OnDestroy()
        {
            if (Registry.TryGetValue(Id, out var existing) && existing == this) Registry.Remove(Id);
        }

        /// <summary>Whether this interactable is currently usable at all (e.g. open doors and hidden boxes are not).</summary>
        public bool IsActive(MatchState match)
        {
            if (match == null || match.Phase != MatchPhase.Playing) return false;
            switch (Kind)
            {
                case InteractableKind.Door: return (match.DoorMask & (1UL << Param)) == 0;
                case InteractableKind.MysteryBox: return match.BoxState != BoxState.Moving;
                default: return true;
            }
        }

        /// <summary>The quote shown to <paramref name="player"/>; the server evaluates exactly the same function.</summary>
        public PurchaseQuote Quote(MatchState match, PlayerAvatar player)
        {
            var loadout = player.Weapons.View;
            switch (Kind)
            {
                case InteractableKind.Door:
                    return PurchaseRules.Door(Param, match.DoorMask);
                case InteractableKind.WallBuy:
                    return PurchaseRules.WallBuy(loadout, (WeaponId)Param);
                case InteractableKind.Perk:
                    return PurchaseRules.Perk(player.Perks, (PerkId)Param);
                case InteractableKind.AmmoCrate:
                    return PurchaseRules.AmmoCrate(loadout);
                case InteractableKind.UpgradeBench:
                    return PurchaseRules.Upgrade(loadout);
                case InteractableKind.MysteryBox:
                    switch (match.BoxState)
                    {
                        case BoxState.Idle: return PurchaseQuote.Of(QuoteKind.BoxRoll, MysteryBoxRules.Cost);
                        case BoxState.Offering:
                            return match.BoxOfferOwner == player.OwnerClientId
                                ? PurchaseQuote.Of(QuoteKind.BoxTake, 0)
                                : PurchaseQuote.No("Another survivor's weapon");
                        default: return PurchaseQuote.No("Supply crate busy");
                    }
                case InteractableKind.EscapeConsole:
                {
                    if (match.EscapeFunded >= match.EscapeCost) return PurchaseQuote.No("Launch sequence engaged");
                    int give = EscapeRules.Contribution(player.Points, match.EscapeFunded, match.EscapeCost);
                    return give > 0 ? PurchaseQuote.Of(QuoteKind.Contribute, give) : PurchaseQuote.No("You have no points to contribute");
                }
                default:
                    return PurchaseQuote.No(string.Empty);
            }
        }

        public string Describe(MatchState match, PlayerAvatar player, string key)
        {
            var q = Quote(match, player);
            string title = Title(match);
            if (!q.Available) return string.IsNullOrEmpty(q.Reason) ? title : title + "  -  " + q.Reason;
            switch (q.Kind)
            {
                case QuoteKind.OpenDoor: return $"[{key}] Open {title}  -  {q.Cost}";
                case QuoteKind.NewWeapon: return $"[{key}] Buy {title}  -  {q.Cost}";
                case QuoteKind.Ammo: return $"[{key}] Buy ammo for {title}  -  {q.Cost}";
                case QuoteKind.Perk: return $"[{key}] Drink {title} ({PerkCatalog.Description((PerkId)Param)})  -  {q.Cost}";
                case QuoteKind.AmmoCrate: return $"[{key}] Restock all ammo  -  {q.Cost}";
                case QuoteKind.Upgrade: return $"[{key}] Upgrade {WeaponCatalog.NameOf(player.Weapons.View.Active.Weapon, false)} with alien tech  -  {q.Cost}";
                case QuoteKind.BoxRoll: return $"[{key}] Open the Supply Drop crate (random weapon)  -  {q.Cost}";
                case QuoteKind.BoxTake: return $"[{key}] Take {WeaponCatalog.NameOf(match.BoxOfferWeapon, false)}";
                case QuoteKind.Contribute:
                    return $"[{key}] Contribute {q.Cost} points to the UFO launch  ({match.EscapeFunded}/{match.EscapeCost})";
                default: return title;
            }
        }

        public string Title(MatchState match)
        {
            switch (Kind)
            {
                case InteractableKind.Door: return MapCatalog.TryGetDoor(Param, out var d) ? d.Name : "Door";
                case InteractableKind.WallBuy: return WeaponCatalog.NameOf((WeaponId)Param, false);
                case InteractableKind.Perk: return PerkCatalog.Name((PerkId)Param);
                case InteractableKind.AmmoCrate: return "Ammo Crate";
                case InteractableKind.UpgradeBench: return "Xeno-Forge";
                case InteractableKind.MysteryBox: return "Supply Drop";
                case InteractableKind.EscapeConsole: return "Saucer Launch Console";
                default: return name;
            }
        }
    }
}
