using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Toate armele jocului si echipamentul de start al fiecarei echipe (cutit + pistol).
    /// Il folosesc inventarul jucatorului (PlayerLoadout) si meniul de cumparare (BuyMenu).
    /// </summary>
    [CreateAssetMenu(menuName = "CHOKEHOLD/Weapon Catalog", fileName = "WeaponCatalog")]
    public class WeaponCatalog : ScriptableObject
    {
        public WeaponData[] weapons = new WeaponData[0];

        [Header("Atacatori")]
        public WeaponData attackerKnife;
        public WeaponData attackerPistol;

        [Header("Aparatori")]
        public WeaponData defenderKnife;
        public WeaponData defenderPistol;

        public WeaponData DefaultKnife(Team team) => team == Team.Attackers ? attackerKnife : defenderKnife;
        public WeaponData DefaultPistol(Team team) => team == Team.Attackers ? attackerPistol : defenderPistol;

        /// <summary>Armele de cumparat pentru o echipa, intr-o categorie, in ordinea pretului.</summary>
        public List<WeaponData> Buyable(Team team, WeaponCategory category)
        {
            List<WeaponData> list = new List<WeaponData>();
            foreach (WeaponData weapon in weapons)
            {
                if (weapon != null && weapon.buyable && weapon.category == category && weapon.AvailableTo(team))
                {
                    list.Add(weapon);
                }
            }

            list.Sort((a, b) => a.price.CompareTo(b.price));
            return list;
        }
    }
}
