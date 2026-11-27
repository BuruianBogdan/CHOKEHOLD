using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Inventarul jucatorului, ca in CS: arma principala (pusca / lunetista), pistolul si cutitul.
    ///   1 / 2 / 3 sau rotita mouse-ului schimba arma; fiecare arma isi pastreaza munitia.
    ///   Cine moare pierde arma principala si primeste pistolul de baza; cine supravietuieste pastreaza armele.
    /// Armele cumparate (BuyMenu) intra in slotul categoriei lor si se scot imediat.
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    public class PlayerLoadout : MonoBehaviour
    {
        public const int PrimarySlot = 0;
        public const int SecondarySlot = 1;
        public const int KnifeSlot = 2;

        [SerializeField] private WeaponCatalog catalog;

        private sealed class Slot
        {
            public WeaponData Data;
            public int Ammo = -1;
            public int Reserve = -1;
        }

        private readonly Slot[] slots = { new Slot(), new Slot(), new Slot() };
        private HitscanWeapon weapon;
        private TeamMember member;
        private CharacterVisuals visuals;
        private bool initialized;
        private int current = -1;
        private int previous = SecondarySlot;
        private Team equippedTeam;
        private Grenades.GrenadeInventory grenades;

        public WeaponCatalog Catalog => catalog;
        public HitscanWeapon Weapon => weapon;
        public int CurrentSlot => current;
        public WeaponData Current => current >= 0 ? slots[current].Data : null;

        /// <summary>Inventarul s-a schimbat (cumparare, schimbare de arma, runda noua).</summary>
        public event Action Changed;

        public WeaponData InSlot(int slot) => slot >= 0 && slot < slots.Length ? slots[slot].Data : null;

        public bool Owns(WeaponData data)
        {
            foreach (Slot slot in slots)
            {
                if (slot.Data == data)
                {
                    return true;
                }
            }
            return false;
        }

        private void Start()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            member = GetComponent<TeamMember>();
            weapon = GetComponentInChildren<HitscanWeapon>(true);
            visuals = GetComponent<CharacterVisuals>();

            if (GetComponent<PlayerEconomy>() == null)
            {
                gameObject.AddComponent<PlayerEconomy>();
            }

            if (GetComponent<UI.BuyMenu>() == null)
            {
                gameObject.AddComponent<UI.BuyMenu>();
            }

            if (GetComponent<UI.SettingsMenu>() == null)
            {
                gameObject.AddComponent<UI.SettingsMenu>();
            }

            grenades = GetComponent<Grenades.GrenadeInventory>();
            if (grenades == null)
            {
                grenades = gameObject.AddComponent<Grenades.GrenadeInventory>();
            }

            GiveDefaults(true);
            EquipBest();
        }

        private void Update()
        {
            if (!initialized || member == null || !member.IsAlive || PlayerInputGate.Blocked || Keyboard.current == null || (grenades != null && grenades.IsThrowing))
            {
                return;
            }

            if (Keyboard.current.digit1Key.wasPressedThisFrame) Equip(PrimarySlot);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame) Equip(SecondarySlot);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame) Equip(KnifeSlot);

            // rotita schimba armele doar daca nu e folosita pentru saritura (bunny hop)
            if (Mouse.current != null && !GameSettings.ScrollWheelJump)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    Cycle(scroll < 0f ? 1 : -1);
                }
            }
        }

        // ------------------------------------------------------------------
        // Inventar
        // ------------------------------------------------------------------

        /// <summary>Arma noua in slotul categoriei ei, cu munitie plina, si scoasa in mana.</summary>
        public void Give(WeaponData data)
        {
            Initialize();
            if (data == null)
            {
                return;
            }

            int slot = SlotOf(data);
            slots[slot].Data = data;
            slots[slot].Ammo = -1;
            slots[slot].Reserve = -1;
            current = -1;           // forteaza re-echiparea chiar daca era deja in mana
            Equip(slot);
        }

        public void Equip(int slot)
        {
            if (slot < 0 || slot >= slots.Length || slots[slot].Data == null || weapon == null)
            {
                return;
            }

            if (slot == current)
            {
                return;
            }

            SaveAmmo();
            if (current >= 0)
            {
                previous = current;
            }
            current = slot;

            Slot s = slots[slot];
            weapon.Equip(s.Data, s.Ammo, s.Reserve);

            if (visuals != null)
            {
                visuals.AttachWeapon(s.Data);
            }

            Changed?.Invoke();
        }

        private void Cycle(int direction)
        {
            int start = current < 0 ? 0 : current;
            for (int i = 1; i <= slots.Length; i++)
            {
                int slot = ((start + direction * i) % slots.Length + slots.Length) % slots.Length;
                if (slots[slot].Data != null)
                {
                    Equip(slot);
                    return;
                }
            }
        }

        private void SaveAmmo()
        {
            if (current < 0 || weapon == null || slots[current].Data == null)
            {
                return;
            }

            slots[current].Ammo = weapon.CurrentAmmo;
            slots[current].Reserve = weapon.ReserveAmmo;
        }

        private static int SlotOf(WeaponData data)
        {
            switch (data.category)
            {
                case WeaponCategory.Knife: return KnifeSlot;
                case WeaponCategory.Pistol: return SecondarySlot;
                default: return PrimarySlot;
            }
        }

        /// <summary>Cutitul si pistolul echipei; la moarte sau la schimbarea echipei se pierde si arma principala.</summary>
        private void GiveDefaults(bool lostEverything)
        {
            if (catalog == null || member == null)
            {
                return;
            }

            Team team = member.Team;
            if (lostEverything || equippedTeam != team)
            {
                slots[PrimarySlot].Data = null;
                slots[SecondarySlot].Data = catalog.DefaultPistol(team);
                slots[SecondarySlot].Ammo = -1;
                slots[SecondarySlot].Reserve = -1;
            }

            slots[KnifeSlot].Data = catalog.DefaultKnife(team);
            equippedTeam = team;
        }

        private void EquipBest()
        {
            current = -1;
            if (slots[PrimarySlot].Data != null) Equip(PrimarySlot);
            else if (slots[SecondarySlot].Data != null) Equip(SecondarySlot);
            else Equip(KnifeSlot);
        }

        /// <summary>
        /// Runda noua (apelat de TeamMember la respawn). Mort in runda trecuta = echipament de baza;
        /// in viata = isi pastreaza armele, cu munitia refacuta.
        /// </summary>
        public void OnRespawn(bool wasDead)
        {
            Initialize();
            GiveDefaults(wasDead);
            if (grenades != null)
            {
                grenades.OnRespawn(wasDead);
            }

            foreach (Slot slot in slots)
            {
                slot.Ammo = -1;
                slot.Reserve = -1;
            }

            EquipBest();
        }
    }
}
