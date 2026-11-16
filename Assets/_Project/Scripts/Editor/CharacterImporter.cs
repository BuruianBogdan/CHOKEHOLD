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

        private void OnPreprocessModel()
        {
            bool character = assetPath.StartsWith(CharactersFolder);
            bool animation = assetPath.StartsWith(AnimationsFolder);

            if (!character && !animation)
            {
                return;
            }

            ModelImporter importer = (ModelImporter)assetImporter;
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
            importer.animationType = ModelImporterAnimationType.Human;

            if (animation || assetPath != BaseBodyPath)
            {
                // Toate personajele si clipurile folosesc avatarul corpului de baza (acelasi schelet).
                Avatar baseAvatar = AssetDatabase.LoadAssetAtPath<Avatar>(BaseBodyPath);

                if (baseAvatar != null)
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                    importer.sourceAvatar = baseAvatar;
                }
            }
            else
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            }

            importer.importAnimation = animation;
            importer.optimizeGameObjects = false;   // oasele raman accesibile pentru socket-uri si hitbox-uri
        }
    }
}
