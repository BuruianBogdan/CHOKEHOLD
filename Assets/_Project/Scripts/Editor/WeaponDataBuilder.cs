using System.Collections.Generic;
using ChokeHold.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Creeaza / actualizeaza asset-urile WeaponData (Assets/_Project/Weapons): modele, statistici, preturi, ochire;
    /// catalogul de arme (ce se cumpara, cu ce incepe fiecare echipa) si inventarul jucatorului din scena deschisa.
    /// Statisticile sunt in stilul CS, potrivite cu zonele de lovire (cap x4, membre x0,75) si viata de 100.
    /// </summary>
    public static class WeaponDataBuilder
    {
        private const string Folder = "Assets/_Project/Weapons";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Weapons";
        public const string CatalogPath = Folder + "/WeaponCatalog.asset";

        private static readonly Vector3 RifleOffset = new Vector3(0.12f, -0.15f, 0.30f);
        private static readonly Vector3 SniperOffset = new Vector3(0.12f, -0.16f, 0.31f);
        private static readonly Vector3 PistolOffset = new Vector3(0.09f, -0.12f, 0.42f);
        private static readonly Vector3 KnifeOffset = new Vector3(0.13f, -0.15f, 0.31f);

        private sealed class Spec
        {
            public string Id, Name;
            public WeaponKind Kind = WeaponKind.Rifle;
            public WeaponCategory Category = WeaponCategory.Rifle;
            public Team Team = Team.Defenders;
            public bool AnyTeam, Buyable = true, Automatic = true, Suppressed;
            public int Price = 1000, KillReward = 300, Magazine = 30, Reserve = 90;
            public float Damage = 30f, FireRate = 10f, Reload = 2.5f, ReloadEmpty = 2.5f, Draw = 1f, Move = 1f;
            public float Kick = 0.07f, Hip = 0.022f, Aim = 0.004f, AimFov = 50f;
            // reculul in grade: { sus primul glont, lateral primul glont, sus in plus pe glont, lateral in plus pe glont,
            //                     sus maxim, lateral maxim, sansa de schimbare a partii (1 din n + 1) }
            public float[] Recoil = { 0.8f, 0.32f, 0.12f, 0.03f, 4.75f, 1.5f, 8f };
            public SightType Sight = SightType.Iron;
            public float[] Zoom = new float[0];
            public LeftGrip LeftGrip = LeftGrip.Handguard;
            public Vector3 Rotation = Vector3.zero;
            public Vector3? Offset;             // pozitia FP proprie (altfel cea a categoriei)
            public bool Reverse;                // cutit tinut invers (karambit)
        }

        [MenuItem("CHOKEHOLD/Characters/Build Weapon Data")]
        public static void BuildAll()
        {
            BlacksiteK17MapBuilder.EnsureFolder(Folder);
            List<WeaponData> all = new List<WeaponData>();

            // --- Cutite (echipamentul de baza, nu se cumpara)
            all.Add(Define(new Spec { Id = "Knife_Karambit", Name = "Karambit", Kind = WeaponKind.Knife, Category = WeaponCategory.Knife,
                Team = Team.Attackers, Buyable = false, Price = 0, KillReward = 1500, Sight = SightType.None, LeftGrip = LeftGrip.None,
                Draw = 0.45f, Move = 1f, Damage = 34f, Reverse = true, Rotation = new Vector3(112f, 155f, 0f),
                Offset = new Vector3(0.14f, -0.09f, 0.34f) }));
            all.Add(Define(new Spec { Id = "Knife_Combat", Name = "Cutit de lupta", Kind = WeaponKind.Knife, Category = WeaponCategory.Knife,
                Team = Team.Defenders, Buyable = false, Price = 0, KillReward = 1500, Sight = SightType.None, LeftGrip = LeftGrip.None,
                Draw = 0.45f, Move = 1f, Damage = 34f, Rotation = new Vector3(-58f, -15f, 0f) }));

            // --- Pistoale
            all.Add(Define(new Spec { Id = "P17_K", Name = "P17-K", Kind = WeaponKind.Pistol, Category = WeaponCategory.Pistol,
                Team = Team.Attackers, Price = 200, Automatic = false, Damage = 30f, FireRate = 6.6f, Magazine = 20, Reserve = 120,
                Reload = 2.2f, ReloadEmpty = 2.2f, Draw = 0.5f, Recoil = new[] { 2f, 0.3f, 0f, 0f, 6f, 1.5f, 3f }, Kick = 0.05f, Hip = 0.014f, Aim = 0.006f,
                AimFov = 55f, LeftGrip = LeftGrip.PistolSupport }));
            all.Add(Define(new Spec { Id = "P_SD", Name = "P-SD", Kind = WeaponKind.Pistol, Category = WeaponCategory.Pistol,
                Team = Team.Defenders, Price = 200, Automatic = false, Suppressed = true, Damage = 35f, FireRate = 5.5f, Magazine = 12,
                Reserve = 24, Reload = 2.2f, ReloadEmpty = 2.2f, Draw = 0.5f, Recoil = new[] { 2.2f, 0.3f, 0f, 0f, 6f, 1.5f, 3f }, Kick = 0.05f, Hip = 0.016f,
                Aim = 0.006f, AimFov = 55f, LeftGrip = LeftGrip.PistolSupport }));
            all.Add(Define(new Spec { Id = "M15T_Breacher", Name = "M15-T Breacher", Kind = WeaponKind.Pistol, Category = WeaponCategory.Pistol,
                Team = Team.Attackers, Price = 700, Automatic = false, Damage = 48f, FireRate = 4f, Magazine = 7, Reserve = 35,
                Reload = 2.2f, ReloadEmpty = 2.2f, Draw = 0.6f, Recoil = new[] { 4.5f, 0.6f, 0f, 0f, 10f, 2f, 2f }, Kick = 0.07f, Hip = 0.025f, Aim = 0.008f,
                AimFov = 55f, LeftGrip = LeftGrip.PistolSupport }));

            // --- Pusti
            all.Add(Define(new Spec { Id = "Galil_K", Name = "Galil-K", Team = Team.Attackers, Price = 1800, Damage = 30f, FireRate = 11f,
                Magazine = 35, Reserve = 90, Reload = 3.0f, ReloadEmpty = 3.0f, Recoil = new[] { 0.9f, 0.35f, 0.15f, 0.03f, 5f, 1.6f, 7f }, Hip = 0.024f, Aim = 0.005f,
                Sight = SightType.RedDot, Move = 0.96f }));
            all.Add(Define(new Spec { Id = "AK_K17", Name = "AK-K17", Team = Team.Attackers, Price = 2700, Damage = 36f, FireRate = 10f,
                Magazine = 30, Reserve = 90, Reload = 2.5f, ReloadEmpty = 2.5f, Recoil = new[] { 1f, 0.375f, 0.175f, 0.0375f, 5.75f, 1.75f, 8f }, Hip = 0.025f, Aim = 0.0045f,
                Sight = SightType.RedDot, LeftGrip = LeftGrip.VerticalGrip, Move = 0.884f }));
            all.Add(Define(new Spec { Id = "M4A1_S", Name = "M4A1-S", Team = Team.Defenders, Price = 2900, Damage = 38f, FireRate = 10f,
                Magazine = 20, Reserve = 80, Reload = 3.0f, ReloadEmpty = 3.0f, Suppressed = true, Recoil = new[] { 0.7f, 0.3f, 0.1f, 0.025f, 4.25f, 1.25f, 8f },
                Hip = 0.02f, Aim = 0.0035f, Sight = SightType.Holo, Move = 0.92f }));
            all.Add(Define(new Spec { Id = "M4A4", Name = "M4A4", Team = Team.Defenders, Price = 3100, Damage = 33f, FireRate = 11f,
                Magazine = 30, Reserve = 90, Reload = 3.1f, ReloadEmpty = 3.1f, Recoil = new[] { 0.8f, 0.32f, 0.12f, 0.03f, 4.75f, 1.5f, 8f }, Hip = 0.022f, Aim = 0.004f,
                AimFov = 40f, Sight = SightType.Acog, Zoom = new[] { 40f }, LeftGrip = LeftGrip.VerticalGrip, Move = 0.92f }));

            // --- Lunetiste (ambele echipe)
            all.Add(Define(new Spec { Id = "SSG_K", Name = "SSG-K", Category = WeaponCategory.Sniper, AnyTeam = true, Price = 1700, Automatic = false, Damage = 88f,
                FireRate = 0.8f, Magazine = 10, Reserve = 90, Reload = 3.7f, ReloadEmpty = 3.7f, Draw = 1.1f, Recoil = new[] { 3f, 0.5f, 0f, 0f, 3f, 0.5f, 1f },
                Kick = 0.1f, Hip = 0.05f, Aim = 0.001f, AimFov = 40f, Sight = SightType.SniperScope, Zoom = new[] { 40f, 15f }, Move = 1.04f }));
            all.Add(Define(new Spec { Id = "AW_K17", Name = "AW-K17", Category = WeaponCategory.Sniper, AnyTeam = true, Price = 4750, KillReward = 100, Automatic = false,
                Damage = 115f, FireRate = 0.68f, Magazine = 5, Reserve = 30, Reload = 3.7f, ReloadEmpty = 3.7f, Draw = 1.25f, Recoil = new[] { 4f, 0.6f, 0f, 0f, 4f, 0.6f, 1f },
                Kick = 0.12f, Hip = 0.09f, Aim = 0.0006f, AimFov = 40f, Sight = SightType.SniperScope,
                Zoom = new[] { 40f, 15f }, Move = 0.85f }));

            // --- Armele vechi facute din cod: raman pentru tinte, nu se mai cumpara (le inlocuiesc M4A1-S si M4A4)
            all.Add(Define(new Spec { Id = "M4A4M_Vandal", Name = "M4A4-M Vandal", Team = Team.Defenders, Buyable = false, Price = 3100,
                Damage = 33f, FireRate = 11f, Reload = 2.2f, ReloadEmpty = 2.8f, Sight = SightType.Holo }));
            all.Add(Define(new Spec { Id = "M4_SD", Name = "M4-SD", Team = Team.Defenders, Buyable = false, Price = 2900, Damage = 30f,
                FireRate = 11f, Reload = 2.2f, ReloadEmpty = 2.8f, Suppressed = true, Recoil = new[] { 0.7f, 0.3f, 0.1f, 0.025f, 4.25f, 1.25f, 8f }, Sight = SightType.Holo }));

            WeaponCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<WeaponCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.weapons = all.ToArray();
            catalog.attackerKnife = Find(all, "Knife_Karambit");
            catalog.attackerPistol = Find(all, "P17_K");
            catalog.defenderKnife = Find(all, "Knife_Combat");
            catalog.defenderPistol = Find(all, "P_SD");
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            FpsController player = Object.FindAnyObjectByType<FpsController>();
            if (player != null)
            {
                WirePlayer(player);
                AssignTargetWeapons();
                EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
                EditorSceneManager.SaveScene(player.gameObject.scene);
            }

            Debug.Log($"WeaponCatalog: {all.Count} arme, de cumparat: {all.FindAll(w => w.buyable).Count}");
        }

        /// <summary>Inventarul (catalogul), economia si meniul de cumparare pe jucator.</summary>
        internal static void WirePlayer(FpsController player)
        {
            WeaponCatalog catalog = AssetDatabase.LoadAssetAtPath<WeaponCatalog>(CatalogPath);
            if (catalog == null)
            {
                return;
            }

            PlayerLoadout loadout = player.GetComponent<PlayerLoadout>();
            if (loadout == null)
            {
                loadout = player.gameObject.AddComponent<PlayerLoadout>();
            }

            SerializedObject serialized = new SerializedObject(loadout);
            serialized.FindProperty("catalog").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (player.GetComponent<PlayerEconomy>() == null)
            {
                player.gameObject.AddComponent<PlayerEconomy>();
            }

            if (player.GetComponent<UI.BuyMenu>() == null)
            {
                player.gameObject.AddComponent<UI.BuyMenu>();
            }

            if (player.GetComponent<UI.SettingsMenu>() == null)
            {
                player.gameObject.AddComponent<UI.SettingsMenu>();
            }
        }

        /// <summary>
        /// Arma TP a unei tinte (manechin / bot), dupa echipa: CT cu M4A4 sau M4A1-S, T cu AK-K17 sau Galil-K.
        /// Alegerea depinde doar de nume, ca generatorul hartii si scena sa dea acelasi rezultat.
        /// </summary>
        internal static string DummyWeapon(string name, Team team)
        {
            int sum = 0;
            foreach (char c in name)
            {
                sum += c;
            }

            bool first = sum % 2 == 0;
            return team == Team.Defenders ? (first ? "M4A4" : "M4A1_S") : (first ? "AK_K17" : "Galil_K");
        }

        /// <summary>Tintele din scena (fara FpsController) primesc arme noi in locul pistolului vechi.</summary>
        private static void AssignTargetWeapons()
        {
            foreach (CharacterVisuals visuals in Object.FindObjectsByType<CharacterVisuals>(FindObjectsInactive.Include))
            {
                if (visuals.GetComponent<FpsController>() != null)
                {
                    continue;
                }

                TeamMember member = visuals.GetComponent<TeamMember>();
                Team team = member != null ? member.Team : Team.Defenders;
                WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>($"{Folder}/{DummyWeapon(visuals.name, team)}.asset");
                SerializedObject serialized = new SerializedObject(visuals);
                SerializedProperty weapon = serialized.FindProperty("weapon");
                if (data != null && weapon.objectReferenceValue != data)
                {
                    weapon.objectReferenceValue = data;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static WeaponData Find(List<WeaponData> all, string id) => all.Find(w => w != null && w.name == id);

        private static WeaponData Define(Spec spec)
        {
            string path = $"{Folder}/{spec.Id}.asset";
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.displayName = spec.Name;
            data.kind = spec.Kind;
            data.category = spec.Category;
            data.team = spec.Team;
            data.anyTeam = spec.AnyTeam;
            data.buyable = spec.Buyable;
            data.price = spec.Price;
            data.killReward = spec.KillReward;
            data.firstPersonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{spec.Id}_FP.prefab");
            data.thirdPersonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{spec.Id}_TP.prefab");
            data.damage = spec.Damage;
            data.fireRate = spec.FireRate;
            data.automatic = spec.Automatic;
            data.magazineSize = spec.Magazine;
            data.reserveAmmo = spec.Reserve;
            data.reloadTime = spec.Reload;
            data.reloadEmptyTime = spec.ReloadEmpty;
            data.drawTime = spec.Draw;
            data.moveSpeed = spec.Move;
            data.suppressed = spec.Suppressed;
            data.kickBase = new Vector2(spec.Recoil[0], spec.Recoil[1]);
            data.kickPerShot = new Vector2(spec.Recoil[2], spec.Recoil[3]);
            data.kickMax = new Vector2(spec.Recoil[4], spec.Recoil[5]);
            data.kickFlip = Mathf.RoundToInt(spec.Recoil[6]);
            data.recoilKick = spec.Kick * 0.35f;     // cat merge arma inapoi in mana la foc (m); mai mult o scoate din palme
            data.hipSpread = spec.Hip;
            data.aimSpread = spec.Aim;
            data.aimFieldOfView = spec.AimFov;
            data.sight = spec.Sight;
            data.zoomFieldOfView = spec.Zoom;
            data.leftGrip = spec.LeftGrip;
            data.firstPersonRotation = spec.Rotation;
            data.reverseGrip = spec.Reverse;
            data.slashDamage = 34f;
            data.stabDamage = 65f;

            switch (spec.Category)
            {
                case WeaponCategory.Knife:
                    data.firstPersonOffset = KnifeOffset;
                    data.adsEyeRelief = 0.3f;
                    data.range = 2f;
                    break;
                case WeaponCategory.Pistol:
                    data.firstPersonOffset = PistolOffset;
                    data.adsEyeRelief = 0.38f;
                    data.range = 120f;
                    break;
                case WeaponCategory.Sniper:
                    data.firstPersonOffset = SniperOffset;
                    data.adsEyeRelief = 0.08f;
                    data.range = 400f;
                    break;
                default:
                    data.firstPersonOffset = RifleOffset;
                    data.adsEyeRelief = spec.Sight == SightType.Acog ? 0.10f : 0.14f;
                    data.range = 200f;
                    break;
            }

            if (spec.Offset.HasValue)
            {
                data.firstPersonOffset = spec.Offset.Value;
            }

            EditorUtility.SetDirty(data);
            if (data.firstPersonPrefab == null || data.thirdPersonPrefab == null)
            {
                Debug.LogWarning($"WeaponData {spec.Id}: lipseste prefab-ul FP sau TP");
            }
            return data;
        }
    }
}
