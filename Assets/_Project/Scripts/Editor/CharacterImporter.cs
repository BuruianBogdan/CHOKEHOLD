using UnityEditor;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    /// <summary>
    /// Setari de import pentru personaje (Assets/_Project/Art/Characters): rig Humanoid cu avatar din
    /// scheletul K17, scara 1 m, fara collidere, materiale URP. Clipurile (Art/Animations) iau avatarul
    /// corpului de baza, ca sa mearga pe toate personajele.
    /// </summary>
    public class CharacterImporter : AssetPostprocessor
    {
        private const string CharactersFolder = "Assets/_Project/Art/Characters/";
        private const string AnimationsFolder = "Assets/_Project/Art/Animations/";
        private const string BaseBodyPath = "Assets/_Project/Art/Characters/Base/K17_BaseBody.fbx";

        private const string WeaponsFolder = "Assets/_Project/Art/Weapons/";

        private void OnPreprocessModel()
        {
            bool character = assetPath.StartsWith(CharactersFolder);
            bool animation = assetPath.StartsWith(AnimationsFolder);
            bool weapon = assetPath.StartsWith(WeaponsFolder);

            if (!character && !animation && !weapon)
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;

            if (weapon)
            {
                // Arme: fara rig, materialele dupa numele slotului (le leaga WeaponPrefabBuilder), socket-urile raman
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.bakeAxisConversion = true;
                importer.importNormals = ModelImporterNormals.Import;
                importer.addCollider = false;
                importer.isReadable = false;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
                importer.preserveHierarchy = true;
                return;
            }
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.importNormals = ModelImporterNormals.Import;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = animation ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            // Mainile FP se misca prin IK din cod: rig Generic, fara avatar
            bool fpArms = assetPath.Contains("/FPArms/");
            importer.animationType = fpArms ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.Human;

            // Fiecare FBX (personaj sau clip) are acelasi schelet K17, deci isi creeaza propriul avatar
            // Humanoid; retargetarea intre ele merge prin spatiul Humanoid. (Copierea avatarului din
            // corpul de baza esueaza cand radacina modelului are alt nume.)
            importer.avatarSetup = fpArms ? ModelImporterAvatarSetup.NoAvatar : ModelImporterAvatarSetup.CreateFromThisModel;

            importer.importAnimation = animation;
            importer.optimizeGameObjects = false;   // oasele raman accesibile pentru socket-uri si hitbox-uri
        }

        private static readonly string[] LoopingClips =
        {
            "_Loco_Idle", "_Loco_Walk_", "_Loco_Run_", "_Loco_Sprint", "_Loco_CrouchIdle", "_Loco_CrouchWalk_",
            "_Air_Fall", "_Action_Plant", "_Action_Defuse", "_Action_Ladder", "_Action_Barricade",
        };

        /// <summary>Clipurile generate: bucla dupa nume, fara root motion (miscarea vine din FpsController).</summary>
        private void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(AnimationsFolder))
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            string name = System.IO.Path.GetFileNameWithoutExtension(assetPath);

            foreach (ModelImporterClipAnimation clip in clips)
            {
                clip.name = name;
                bool loop = false;
                foreach (string pattern in LoopingClips)
                {
                    if (name.Contains(pattern))
                    {
                        loop = true;
                    }
                }

                clip.loopTime = loop;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalOrientation = true;
                clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
                clip.heightFromFeet = false;
            }

            importer.clipAnimations = clips;
        }
    }
}
