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

        /// <summary>Texturile personajelor (din Tripo): culoare sRGB, normale ca normal map, metalic + luciu liniar.</summary>
        private void OnPreprocessTexture()
        {
            if (!(assetPath.StartsWith(CharactersFolder) || assetPath.StartsWith(WeaponsFolder)) || !assetPath.Contains("/Textures/"))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.wrapMode = TextureWrapMode.Clamp;

            if (assetPath.Contains("_Normal."))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (assetPath.Contains("_MetallicSmoothness."))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
            }
        }

        /// <summary>
        /// Materialul unui slot din FBX-urile personajelor: "<Nume>_Body" (modelele din Tripo) primeste un material
        /// URP Lit facut din texturile lui; restul sloturilor (personajele facute din cod) folosesc materialele K17.
        /// </summary>
        internal static Material CharacterMaterial(string slot)
        {
            if (!slot.EndsWith("_Body"))
            {
                return BlacksiteK17MapBuilder.KitMaterial(slot);
            }

            string name = slot.Substring(0, slot.Length - "_Body".Length);
            // personajele si armele din Tripo: Art/Characters/<Nume> sau Art/Weapons/<Nume>
            string folder = AssetDatabase.IsValidFolder($"{WeaponsFolder}{name}/Textures") ? $"{WeaponsFolder}{name}" : $"{CharactersFolder}{name}";
            string textures = $"{folder}/Textures/{name}";
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textures}_BaseColor.jpg");
            if (baseColor == null)
            {
                return BlacksiteK17MapBuilder.KitMaterial(slot);
            }

            string path = $"{folder}/{slot}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", baseColor);
            material.SetColor("_BaseColor", Color.white);

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textures}_Normal.jpg");
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            if (normal != null) material.EnableKeyword("_NORMALMAP"); else material.DisableKeyword("_NORMALMAP");

            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{textures}_MetallicSmoothness.png");
            material.SetTexture("_MetallicGlossMap", mask);
            material.SetFloat("_SmoothnessTextureChannel", 0f);    // luciul din alfa hartii metalice
            // Hartile de rugozitate din Tripo sunt prea lucioase pe metalul armelor (par cromate in lumina de afara)
            material.SetFloat("_Smoothness", folder.StartsWith(WeaponsFolder) ? 0.7f : 1f);
            material.SetFloat("_Metallic", 1f);
            if (mask != null) material.EnableKeyword("_METALLICSPECGLOSSMAP"); else material.DisableKeyword("_METALLICSPECGLOSSMAP");

            EditorUtility.SetDirty(material);
            return material;
        }

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
