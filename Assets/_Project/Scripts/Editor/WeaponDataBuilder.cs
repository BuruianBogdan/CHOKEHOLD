using ChokeHold.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>Creeaza / actualizeaza asset-urile WeaponData (Assets/_Project/Weapons) cu modelele si statisticile armelor.</summary>
    public static class WeaponDataBuilder
    {
        private const string Folder = "Assets/_Project/Weapons";
        private const string PrefabFolder = "Assets/_Project/Prefabs/Weapons";

        [MenuItem("CHOKEHOLD/Characters/Build Weapon Data")]
        public static void BuildAll()
        {
            BlacksiteK17MapBuilder.EnsureFolder(Folder);

            Define("M4A4M_Vandal", "M4A4-M Vandal", WeaponKind.Rifle, Team.Defenders,
                damage: 33f, fireRate: 11f, automatic: true, magazine: 30, reload: 2.2f, reloadEmpty: 2.8f,
                recoilV: 0.8f, recoilH: 0.18f, kick: 0.07f, hipSpread: 0.022f, aimSpread: 0.004f, aimFov: 46f);
            Define("M4_SD", "M4-SD", WeaponKind.Rifle, Team.Defenders,
                damage: 30f, fireRate: 11f, automatic: true, magazine: 30, reload: 2.2f, reloadEmpty: 2.8f,
                recoilV: 0.6f, recoilH: 0.14f, kick: 0.05f, hipSpread: 0.020f, aimSpread: 0.003f, aimFov: 46f);
            Define("M15T_Breacher", "M15-T Breacher", WeaponKind.Pistol, Team.Attackers,
                damage: 45f, fireRate: 5f, automatic: false, magazine: 15, reload: 1.8f, reloadEmpty: 2.1f,
                recoilV: 1.4f, recoilH: 0.3f, kick: 0.06f, hipSpread: 0.03f, aimSpread: 0.008f, aimFov: 55f);
            Define("P_SD", "P-SD", WeaponKind.Pistol, Team.Defenders,
                damage: 36f, fireRate: 6f, automatic: false, magazine: 12, reload: 1.8f, reloadEmpty: 2.1f,
                recoilV: 1.0f, recoilH: 0.22f, kick: 0.05f, hipSpread: 0.025f, aimSpread: 0.006f, aimFov: 55f);

            AssetDatabase.SaveAssets();
        }

        private static void Define(string id, string displayName, WeaponKind kind, Team team, float damage, float fireRate,
            bool automatic, int magazine, float reload, float reloadEmpty, float recoilV, float recoilH, float kick,
            float hipSpread, float aimSpread, float aimFov)
        {
            string path = $"{Folder}/{id}.asset";
            WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>(path);

            if (data == null)
            {
                data = ScriptableObject.CreateInstance<WeaponData>();
                AssetDatabase.CreateAsset(data, path);
            }

            data.displayName = displayName;
            data.kind = kind;
            data.team = team;
            data.firstPersonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{id}_FP.prefab");
            data.thirdPersonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabFolder}/{id}_TP.prefab");
            data.damage = damage;
            data.fireRate = fireRate;
            data.automatic = automatic;
            data.magazineSize = magazine;
            data.reloadTime = reload;
            data.reloadEmptyTime = reloadEmpty;
            data.recoilVertical = recoilV;
            data.recoilHorizontal = recoilH;
            data.recoilKick = kick;
            data.hipSpread = hipSpread;
            data.aimSpread = aimSpread;
            data.aimFieldOfView = aimFov;
            data.firstPersonOffset = kind == WeaponKind.Rifle ? new Vector3(0.14f, -0.16f, 0.30f) : new Vector3(0.12f, -0.13f, 0.34f);
            EditorUtility.SetDirty(data);

            Debug.Log($"WeaponData: {path} (FP={data.firstPersonPrefab != null}, TP={data.thirdPersonPrefab != null})");
        }
    }
}
