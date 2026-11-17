using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ChokeHold.EditorTools
{
    // Pasul de realism: texturi PBR pe toate materialele hartii si post-procesare.
    //
    // Texturile vin din Tools/Blender/k17_textures.py (Assets/_Project/Art/Textures/K17): pentru fiecare set
    // <Set>_A (culoare), <Set>_N (normal map), <Set>_M (R = metalic, A = luciu). Geometria are UV in metri,
    // deci tiling-ul unui material = 1 / (cati metri acopera textura).
    //
    // Culoarea fiecarui material ramane cea din generator (nuanta); textura aduce detaliul.
    public static partial class BlacksiteK17MapBuilder
    {
        private const string TextureFolder = "Assets/_Project/Art/Textures/K17";
        private const string PostProfilePath = "Assets/_Project/Settings/K17_PostProcess.asset";

        // material -> (set de texturi, cati metri acopera textura)
        private static readonly Dictionary<string, (string set, float meters)> SurfaceMap = new Dictionary<string, (string, float)>
        {
            // Structura
            { "GB_Wall", ("Concrete", 2f) },
            { "GB_WallOuter", ("ConcreteDark", 2f) },
            { "GB_Floor", ("ConcreteFloor", 2f) },
            { "GB_UpperFloor", ("ConcreteFloor", 2f) },
            { "GB_Roof", ("Concrete", 2f) },
            { "GB_Stairs", ("Concrete", 2f) },
            { "GB_Cover", ("Concrete", 2f) },
            { "GB_SoftWall", ("Plaster", 2f) },
            { "GB_Platform", ("CheckerPlate", 1f) },
            { "GB_Ramp", ("CheckerPlate", 1f) },
            { "GB_Hatch", ("CheckerPlate", 1f) },
            { "GB_Reinforced", ("Steel", 1f) },
            { "GB_Barricade", ("Wood", 2f) },
            { "GB_Door", ("PaintedMetal", 1f) },
            { "GB_Pipe", ("PaintedMetal", 1f) },
            { "GB_ContainerRed", ("PaintedMetal", 1f) },
            { "GB_ContainerBlue", ("PaintedMetal", 1f) },
            { "GB_SiteFloor", ("ConcreteFloor", 2f) },
            { "GB_T1Floor", ("ConcreteFloor", 2f) },
            { "GB_T2Floor", ("ConcreteFloor", 2f) },

            // Finisare
            { "GB_D_FloorExterior", ("Asphalt", 2f) },
            { "GB_D_FloorIndustrial", ("ConcreteFloor", 2f) },
            { "GB_D_FloorHangar", ("ConcreteFloor", 2f) },
            { "GB_D_FloorTunnel", ("ConcreteFloor", 2f) },
            { "GB_D_FloorLab", ("LabTiles", 2f) },
            { "GB_D_Plinth", ("ConcreteDark", 2f) },
            { "GB_D_Coping", ("ConcreteDark", 2f) },
            { "GB_D_ExtWainscot", ("ConcreteDark", 2f) },
            { "GB_D_IndPilaster", ("Concrete", 2f) },
            { "GB_D_LabPilaster", ("Plaster", 2f) },
            { "GB_D_LabWainscot", ("PaintedMetal", 1f) },
            { "GB_D_IndBand", ("PaintedMetal", 1f) },
            { "GB_D_HangarBand", ("PaintedMetal", 1f) },
            { "GB_D_HangarPilaster", ("PaintedMetal", 1f) },
            { "GB_D_TunnelBand", ("PaintedMetal", 1f) },
            { "GB_D_Beam", ("PaintedMetal", 1f) },
            { "GB_D_Truss", ("PaintedMetal", 1f) },
            { "GB_D_Frame", ("PaintedMetal", 1f) },
            { "GB_D_HazardYellow", ("PaintedMetal", 1f) },
            { "GB_D_HazardBlack", ("PaintedMetal", 1f) },
            { "GB_D_Marking", ("Plaster", 2f) },                // vopsea mata, fara rosturi

            // Kit
            { "K17_Concrete", ("Concrete", 2f) },
            { "K17_ConcreteDark", ("ConcreteDark", 2f) },
            { "K17_Steel", ("Steel", 1f) },
            { "K17_SteelDark", ("Steel", 1f) },
            { "K17_Rust", ("Steel", 1f) },
            { "K17_Wood", ("Wood", 2f) },
            { "K17_WoodDark", ("Wood", 2f) },
            { "K17_Rubber", ("Rubber", 1f) },
            { "K17_Black", ("Rubber", 1f) },
            { "K17_Hesco", ("Hesco", 1f) },
            { "K17_Canvas", ("Canvas", 1f) },
            { "K17_Gravel", ("Gravel", 2f) },
            { "K17_Soil", ("Gravel", 2f) },
            { "K17_Paint", ("PaintedMetal", 1f) },
            { "K17_ReactorPaint", ("PaintedMetal", 1f) },
            { "K17_TankPaint", ("PaintedMetal", 1f) },
            { "K17_MotorPaint", ("PaintedMetal", 1f) },
            { "K17_TurbinePaint", ("PaintedMetal", 1f) },
            { "K17_PipePaint", ("PaintedMetal", 1f) },
            { "K17_PipePaintGrey", ("PaintedMetal", 1f) },
            { "K17_DoorPaint", ("PaintedMetal", 1f) },
            { "K17_BarrelPaint", ("PaintedMetal", 1f) },
            { "K17_MilitaryPaint", ("PaintedMetal", 1f) },
            { "K17_Hazard", ("PaintedMetal", 1f) },

            // Personaje
            { "K17_CT_Camo", ("CT_Camo", 0.5f) },
            { "K17_T_Camo", ("T_Camo", 0.5f) },
            { "K17_T_CamoDesert", ("T_CamoDesert", 0.5f) },
            { "K17_Nylon", ("Nylon", 0.5f) },
            { "K17_NylonOlive", ("NylonOlive", 0.5f) },
            { "K17_Plastic", ("Plastic", 1f) },
            { "K17_Leather", ("Rubber", 0.5f) },
            { "K17_Maroon", ("Canvas", 0.5f) },
            { "K17_PatchBlue", ("Canvas", 0.25f) },
            { "K17_PatchGrey", ("Canvas", 0.25f) },
            { "K17_GunMetal", ("Steel", 0.25f) },
            { "K17_Polymer", ("Plastic", 0.25f) },
            { "K17_CamoTape", ("T_Camo", 0.25f) },
            { "K17_BluedSteel", ("Steel", 0.25f) },
            { "K17_MagPolymer", ("Plastic", 0.25f) },
        };

        internal static void ApplySurfaces(GameObject root)
        {
            if (!AssetDatabase.IsValidFolder(TextureFolder))
            {
                Debug.LogWarning("Texturile lipsesc (ruleaza Tools/Blender/k17_textures.py). Materialele raman netexturate.");
                return;
            }

            HashSet<Material> materials = new HashSet<Material>();

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }

            int textured = 0;

            foreach (Material material in materials)
            {
                if (!SurfaceMap.TryGetValue(material.name, out var surface) || !ApplySurface(material, surface.set, surface.meters))
                {
                    continue;
                }

                textured++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Suprafete: {textured} din {materials.Count} materiale texturate.");
        }

        private static bool ApplySurface(Material material, string set, float meters)
        {
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{set}_A.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{set}_N.png");
            Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{set}_M.png");

            if (albedo == null || !material.HasProperty("_BaseMap"))
            {
                return false;
            }

            Vector2 tiling = Vector2.one / meters;

            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", tiling);

            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", 1f);
                material.EnableKeyword("_NORMALMAP");
            }

            if (mask != null)
            {
                material.SetTexture("_MetallicGlossMap", mask);
                material.SetFloat("_SmoothnessTextureChannel", 0f);   // luciul din alfa hartii metalice
                material.SetFloat("_Smoothness", 1f);                  // inmultitor pentru alfa
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            EditorUtility.SetDirty(material);
            return true;
        }

        // ------------------------------------------------------------------
        // Post-procesare: tonemapping ACES, expunere si contrast, bloom discret, vigneta usoara
        // ------------------------------------------------------------------
        private static void BuildPostProcessing(GameObject root)
        {
            EnsureFolder("Assets/_Project/Settings");
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProfilePath);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, PostProfilePath);
            }

            T Get<T>() where T : VolumeComponent
            {
                if (!profile.TryGet(out T component))
                {
                    component = profile.Add<T>(true);
                    component.name = typeof(T).Name;
                    AssetDatabase.AddObjectToAsset(component, profile);
                }

                component.active = true;
                return component;
            }

            Tonemapping tonemapping = Get<Tonemapping>();
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments color = Get<ColorAdjustments>();
            color.postExposure.Override(0.35f);
            color.contrast.Override(12f);
            color.saturation.Override(-8f);

            WhiteBalance white = Get<WhiteBalance>();
            white.temperature.Override(-6f);

            Bloom bloom = Get<Bloom>();
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);

            Vignette vignette = Get<Vignette>();
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);

            Transform existing = root.transform.Find("PostProcessing");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject go = new GameObject("PostProcessing");
            go.transform.SetParent(root.transform, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = profile;

            // Camera jucatorului (si orice alta camera din scena) trebuie sa randeze post-procesarea.
            foreach (Camera camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
            {
                UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();

                if (data != null)
                {
                    data.renderPostProcessing = true;
                    data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    data.antialiasingQuality = AntialiasingQuality.High;
                    EditorUtility.SetDirty(data);
                }
            }
        }
    }

    /// <summary>Setari de import pentru texturile K17: normal map, masti liniare, filtrare anizotropa.</summary>
    public class K17TextureImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/_Project/Art/Textures/K17/"))
            {
                return;
            }

            TextureImporter importer = (TextureImporter)assetImporter;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;

            if (assetPath.EndsWith("_N.png"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (assetPath.EndsWith("_M.png"))
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
    }
}
