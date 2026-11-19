using System.Collections.Generic;
using ChokeHold.Gameplay;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Finisare vizuala (pre-art pass): doar geometrie si materiale, fara asset-uri externe.
    //
    // Ruleaza dupa ce toate zonele sunt construite si citeste ce s-a plasat:
    //   - fetele peretilor de pe grila  -> plinta, lambriu / benzi, pilastri pe teme
    //   - colturile convexe             -> coloane
    //   - blocurile de perete           -> coronament
    //   - acoperisurile                 -> grinzi (ferme in hangar)
    //   - planseele de etaj             -> balustrade pe toate muchiile libere
    //   - luminile                      -> corpuri de iluminat cu tija; lampi noi unde interiorul e intunecat
    //   - podeaua                       -> culoare pe teme, marcaje de pericol, litere de site, sageti de ruta
    //
    // Detaliile nu au collider (nu schimba miscarea sau liniile de vedere masurate); balustradele au.
    public static partial class BlacksiteK17MapBuilder
    {
        private enum Theme
        {
            Exterior,
            Industrial,
            Lab,
            Hangar,
            Tunnel
        }

        private struct PlacedRect
        {
            public float XMin, ZMin, XMax, ZMax, Y;

            public PlacedRect(float xMin, float zMin, float xMax, float zMax, float y)
            {
                XMin = xMin;
                ZMin = zMin;
                XMax = xMax;
                ZMax = zMax;
                Y = y;
            }
        }

        private static readonly List<Transform> placedLights = new List<Transform>();
        private static readonly List<PlacedRect> placedPlatforms = new List<PlacedRect>();
        private static readonly List<PlacedRect> placedRoofs = new List<PlacedRect>();

        private static Transform gDetail;
        private static Transform gCeilingDetail;

        // Materiale de finisare
        private static Material plinthMat, frameMat, copingMat, beamMat, trussMat, fixtureMat;
        private static Material hazardYellowMat, hazardBlackMat, markingMat;
        private static Material extWainscotMat, indBandMat, indPilasterMat, labWainscotMat, labStripeMat, labPilasterMat;
        private static Material hangarPilasterMat, hangarBandMat, tunnelBandMat;
        private static Material extFloorMat, indFloorMat, labFloorMat, hangarFloorMat, tunnelFloorMat;

        // Stalpii de balustrada deja pusi: la colturi doua balustrade impart acelasi stalp.
        private static readonly HashSet<Vector3Int> placedPosts = new HashSet<Vector3Int>();

        private static void ResetDetailRecords()
        {
            placedPosts.Clear();
            placedLights.Clear();
            placedPlatforms.Clear();
            placedRoofs.Clear();
        }

        private static void CreateDetailMaterials()
        {
            plinthMat = GetOrCreateMaterial("GB_D_Plinth", new Color(0.16f, 0.16f, 0.18f));
            frameMat = GetOrCreateMaterial("GB_D_Frame", new Color(0.22f, 0.24f, 0.27f));
            // Inchis la culoare: in vederea de sus peretii trebuie sa se distinga de podeaua de afara.
            copingMat = GetOrCreateMaterial("GB_D_Coping", new Color(0.22f, 0.23f, 0.26f));
            beamMat = GetOrCreateMaterial("GB_D_Beam", new Color(0.30f, 0.31f, 0.34f));
            trussMat = GetOrCreateMaterial("GB_D_Truss", new Color(0.25f, 0.32f, 0.42f));
            hazardYellowMat = GetOrCreateMaterial("GB_D_HazardYellow", new Color(0.95f, 0.78f, 0.1f));
            hazardBlackMat = GetOrCreateMaterial("GB_D_HazardBlack", new Color(0.08f, 0.08f, 0.08f));
            markingMat = GetOrCreateMaterial("GB_D_Marking", new Color(0.82f, 0.81f, 0.76f));

            extWainscotMat = GetOrCreateMaterial("GB_D_ExtWainscot", new Color(0.40f, 0.39f, 0.36f));
            indBandMat = GetOrCreateMaterial("GB_D_IndBand", new Color(0.55f, 0.32f, 0.17f));
            indPilasterMat = GetOrCreateMaterial("GB_D_IndPilaster", new Color(0.31f, 0.30f, 0.29f));
            labWainscotMat = GetOrCreateMaterial("GB_D_LabWainscot", new Color(0.86f, 0.88f, 0.90f));
            labStripeMat = GetOrCreateMaterial("GB_D_LabStripe", new Color(0.15f, 0.65f, 0.75f));
            labPilasterMat = GetOrCreateMaterial("GB_D_LabPilaster", new Color(0.76f, 0.79f, 0.82f));
            hangarPilasterMat = GetOrCreateMaterial("GB_D_HangarPilaster", new Color(0.28f, 0.34f, 0.42f));
            hangarBandMat = GetOrCreateMaterial("GB_D_HangarBand", new Color(0.80f, 0.62f, 0.12f));
            tunnelBandMat = GetOrCreateMaterial("GB_D_TunnelBand", new Color(0.85f, 0.70f, 0.10f));

            extFloorMat = GetOrCreateMaterial("GB_D_FloorExterior", new Color(0.42f, 0.41f, 0.38f));
            indFloorMat = GetOrCreateMaterial("GB_D_FloorIndustrial", new Color(0.34f, 0.31f, 0.28f));
            labFloorMat = GetOrCreateMaterial("GB_D_FloorLab", new Color(0.60f, 0.63f, 0.66f));
            hangarFloorMat = GetOrCreateMaterial("GB_D_FloorHangar", new Color(0.37f, 0.39f, 0.43f));
            tunnelFloorMat = GetOrCreateMaterial("GB_D_FloorTunnel", new Color(0.27f, 0.27f, 0.29f));

            fixtureMat = GetOrCreateMaterial("GB_D_LampFixture", new Color(1f, 0.95f, 0.85f));
            fixtureMat.EnableKeyword("_EMISSION");
            fixtureMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            if (fixtureMat.HasProperty("_EmissionColor"))
            {
                fixtureMat.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.85f) * 2.5f);
            }
        }

        // ------------------------------------------------------------------
        // Teme pe zone
        // ------------------------------------------------------------------
        private static Theme ThemeOf(string areaName)
        {
            if (areaName.StartsWith("Tunnel"))
            {
                return Theme.Tunnel;
            }

            if (areaName.Contains("Stair Room") || areaName.Contains("Stair Door"))
            {
                return Theme.Industrial;
            }

            if (areaName == "Bombsite A" || areaName.StartsWith("A Control") || areaName.StartsWith("A Pipes") ||
                areaName.StartsWith("Depot") || areaName == "A Heaven Door")
            {
                return Theme.Industrial;
            }

            if (areaName == "Mid Lab" || areaName.StartsWith("Lab ") || areaName.StartsWith("Annex") ||
                areaName.StartsWith("Security"))
            {
                return Theme.Lab;
            }

            if (areaName.StartsWith("Hangar") || areaName.StartsWith("Loading") || areaName.StartsWith("Office"))
            {
                return Theme.Hangar;
            }

            return Theme.Exterior;
        }

        private static Theme ThemeAt(float x, float z)
        {
            if (IsOpenPit(x, z))
            {
                return Theme.Tunnel;
            }

            foreach (Area area in WalkableAreas)
            {
                if (Inside(area, x, z))
                {
                    return ThemeOf(area.Name);
                }
            }

            return Theme.Exterior;
        }

        private static Material FloorMatOf(Theme theme)
        {
            switch (theme)
            {
                case Theme.Industrial: return indFloorMat;
                case Theme.Lab: return labFloorMat;
                case Theme.Hangar: return hangarFloorMat;
                case Theme.Tunnel: return tunnelFloorMat;
                default: return extFloorMat;
            }
        }

        private static Color LampColorOf(Theme theme)
        {
            switch (theme)
            {
                case Theme.Industrial:
                case Theme.Tunnel:
                    return WarmLight;
                case Theme.Lab:
                    return ColdLight;
                case Theme.Exterior:
                    return HallLight;
                default:
                    return WhiteLight;
            }
        }

        // ------------------------------------------------------------------
        // Pipeline
        // ------------------------------------------------------------------
        private static void BuildDetails(Transform root)
        {
            gDetail = CreateGroup(root, "Details");
            gCeilingDetail = CreateGroup(gRoofs, "Ceiling Details");

            Physics.SyncTransforms();

            BuildFloorThemes();
            BuildWallFaces();
            BuildCornerColumns();
            BuildWallCoping();
            BuildFreeTopCaps();
            BuildCeilingBeams();
            BuildAutoRailings();
            BuildFloorMarkings();
            FillDarkInteriors();
            BuildLampFixtures();
        }

        /// <summary>Cutie decorativa: fara collider, fara efect asupra jocului.</summary>
        private static GameObject Decor(Transform parent, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject box = CreateBox(parent, name, center, size, material);
            Object.DestroyImmediate(box.GetComponent<Collider>());

            // Detaliile (benzi, plinte, rame, podele pe teme) stau lipite de pereti si podele:
            // umbra lor nu se vede, dar ar costa in fiecare cascada a soarelui.
            box.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return box;
        }

        // ------------------------------------------------------------------
        // Podea pe teme (strat subtire de 2 cm peste placile de baza)
        // ------------------------------------------------------------------
        private static void BuildFloorThemes()
        {
            Transform group = CreateGroup(gFloors, "Floor Themes");
            Transform tunnelGroup = CreateGroup(gUnderground, "Floor Theme Tunnel");

            foreach (Theme theme in System.Enum.GetValues(typeof(Theme)))
            {
                bool[,] cells = new bool[GridX, GridZ];

                for (int i = 0; i < GridX; i++)
                {
                    for (int j = 0; j < GridZ; j++)
                    {
                        float x = CellCenterX(i);
                        float z = CellCenterZ(j);
                        cells[i, j] = IsWalkable(x, z) && ThemeAt(x, z) == theme;
                    }
                }

                // Tunelul are si portiunea acoperita (sub harta plina), care nu e "walkable" la sol.
                // Subteranul are doua niveluri de podea (tuneluri si buncar): cate un strat pe fiecare.
                float[] levels = theme == Theme.Tunnel ? new[] { TunnelFloorY, BunkerFloorY } : new[] { 0f };
                Transform parent = theme == Theme.Tunnel ? tunnelGroup : group;
                int index = 0;

                foreach (float y in levels)
                {
                    if (theme == Theme.Tunnel)
                    {
                        for (int i = 0; i < GridX; i++)
                        {
                            for (int j = 0; j < GridZ; j++)
                            {
                                float x = CellCenterX(i), z = CellCenterZ(j);
                                cells[i, j] = IsPit(x, z) && Mathf.Approximately(PitFloorAt(x, z), y);
                            }
                        }
                    }

                    MergeCells(cells, (i, j, width, depth) =>
                    {
                        float sizeX = width * Cell;
                        float sizeZ = depth * Cell;

                        Decor(parent, $"Floor_{theme}_{index++:00}",
                            new Vector3(MinX + i * Cell + sizeX * 0.5f, y + 0.01f, MinZ + j * Cell + sizeZ * 0.5f),
                            new Vector3(sizeX, 0.02f, sizeZ),
                            FloorMatOf(theme));
                    });
                }
            }
        }

        // ------------------------------------------------------------------
        // Fetele peretilor de pe grila
        // ------------------------------------------------------------------
        private struct Face
        {
            public Vector3 Start;      // capatul de start al fetei, la nivelul bazei
            public Vector3 Along;      // directia de-a lungul fetei
            public Vector3 Normal;     // spre zona walkable
            public float Length;
            public float BaseY;
            public Theme Theme;
        }

        private static void BuildWallFaces()
        {
            Transform group = CreateGroup(gDetail, "Wall Faces");
            List<Face> faces = CollectWallFaces();
            int index = 0;

            foreach (Face face in faces)
            {
                DecorateFace(group, $"Face_{index++:000}", face);
            }
        }

        /// <summary>
        /// Gaseste toate fetele de perete care dau spre o zona walkable si le uneste in segmente
        /// continue, pe aceeasi tema si la aceeasi baza.
        /// </summary>
        private static List<Face> CollectWallFaces()
        {
            List<Face> faces = new List<Face>();

            int[] di = { 1, -1, 0, 0 };
            int[] dj = { 0, 0, 1, -1 };

            for (int d = 0; d < 4; d++)
            {
                bool faceAlongZ = di[d] != 0; // normala pe X -> fata se intinde pe Z
                int outer = faceAlongZ ? GridX : GridZ;
                int inner = faceAlongZ ? GridZ : GridX;

                for (int a = 0; a < outer; a++)
                {
                    int runStart = -1;
                    Theme runTheme = Theme.Exterior;
                    float runBase = 0f;

                    for (int b = 0; b <= inner; b++)
                    {
                        bool exposed = false;
                        Theme theme = Theme.Exterior;
                        float baseY = 0f;

                        if (b < inner)
                        {
                            int i = faceAlongZ ? a : b;
                            int j = faceAlongZ ? b : a;
                            int ni = i + di[d];
                            int nj = j + dj[d];

                            if (ni >= 0 && nj >= 0 && ni < GridX && nj < GridZ)
                            {
                                float cx = CellCenterX(i), cz = CellCenterZ(j);
                                float nx = CellCenterX(ni), nz = CellCenterZ(nj);

                                if (!IsPit(cx, cz) && IsPit(nx, nz))
                                {
                                    // Peretele santului tunelului (de la -3,6 m pana la sol)
                                    exposed = true;
                                    theme = Theme.Tunnel;
                                    baseY = PitFloorAt(nx, nz);
                                }
                                else if (!IsWalkable(cx, cz) && IsWalkable(nx, nz) && !IsOpenPit(nx, nz))
                                {
                                    exposed = true;
                                    theme = ThemeAt(nx, nz);
                                    baseY = 0f;
                                }
                            }
                        }

                        bool continues = exposed && runStart >= 0 && theme == runTheme && Mathf.Approximately(baseY, runBase);

                        if (runStart >= 0 && !continues)
                        {
                            faces.Add(MakeFace(d, di[d], dj[d], a, runStart, b, runTheme, runBase));
                            runStart = -1;
                        }

                        if (exposed && runStart < 0)
                        {
                            runStart = b;
                            runTheme = theme;
                            runBase = baseY;
                        }
                    }
                }
            }

            return faces;
        }

        private static Face MakeFace(int d, int di, int dj, int a, int from, int to, Theme theme, float baseY)
        {
            bool faceAlongZ = di != 0;
            Vector3 normal = new Vector3(di, 0f, dj);

            // Linia fetei: muchia celulei solide spre vecinul walkable.
            Vector3 start;

            if (faceAlongZ)
            {
                float x = MinX + (a + (di > 0 ? 1 : 0)) * Cell;
                start = new Vector3(x, baseY, MinZ + from * Cell);
            }
            else
            {
                float z = MinZ + (a + (dj > 0 ? 1 : 0)) * Cell;
                start = new Vector3(MinX + from * Cell, baseY, z);
            }

            return new Face
            {
                Start = start,
                Along = faceAlongZ ? Vector3.forward : Vector3.right,
                Normal = normal,
                Length = (to - from) * Cell,
                BaseY = baseY,
                Theme = theme
            };
        }

        /// <summary>Banda orizontala pe fata, de la y0 la y1, iesita cu depth din perete.</summary>
        private static void FaceBand(Transform parent, string name, Face face, float y0, float y1, float depth, Material material)
        {
            // La colturile interioare benzile a doi pereti perpendiculari se ating: cele de pe peretii
            // orientati pe Z sunt cu 4 mm mai inalte, ca fetele lor de sus sa nu fie in acelasi plan.
            if (face.Along == Vector3.forward)
            {
                y1 += 0.004f;
            }

            Vector3 start = face.Start;
            float length = face.Length;
            TrimFix(ref start, ref length, face);

            Vector3 mid = start + face.Along * (length * 0.5f) + face.Normal * (depth * 0.5f);
            mid.y = face.BaseY + (y0 + y1) * 0.5f;

            Vector3 size = face.Along * length + Abs(face.Normal) * depth + Vector3.up * (y1 - y0);
            Decor(parent, name, mid, size, material);
        }

        /// <summary>
        /// Capete de benzi potrivite de mana in scena (peretele de exterior de la z = 6, intre x = -78 si -56):
        /// benzile ajung pana la ancadramentul usii dintre fete. (centru x, z, lungime) ->
        /// (cat se muta inceputul, cat se muta capatul), pe directia fetei.
        /// </summary>
        private static readonly (float x, float z, float length, float startShift, float endShift)[] TrimFixes =
        {
            (-74f, 5.95f, 8f, 0.117f, 0.117f),
            (-62f, 5.95f, 12f, -0.138f, 0f),
        };

        private static void TrimFix(ref Vector3 start, ref float length, Face face)
        {
            if (face.Along != Vector3.right)
            {
                return;
            }

            Vector3 center = face.Start + face.Along * (face.Length * 0.5f);
            foreach (var fix in TrimFixes)
            {
                if (Mathf.Abs(center.x - fix.x) < 0.05f && Mathf.Abs(center.z - fix.z) < 0.2f && Mathf.Abs(face.Length - fix.length) < 0.05f)
                {
                    start += face.Along * fix.startShift;
                    length += fix.endShift - fix.startShift;
                    return;
                }
            }
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        /// <summary>Inaltimea libera in fata peretelui (pana la tavan sau pana la varful peretelui).</summary>
        private static float ClearHeightAt(Vector3 groundPoint, float max)
        {
            Vector3 from = groundPoint + Vector3.up * 0.5f;

            if (Physics.Raycast(from, Vector3.up, out RaycastHit hit, max, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y - groundPoint.y;
            }

            return max;
        }

        private static void DecorateFace(Transform parent, string name, Face face)
        {
            Transform group = CreateGroup(parent, name);

            // Plinta peste tot
            FaceBand(group, "Plinth", face, 0f, 0.25f, 0.06f, plinthMat);

            float pilasterSpacing;
            float pilasterWidth;
            float pilasterDepth;
            Material pilasterMat;

            switch (face.Theme)
            {
                case Theme.Industrial:
                    FaceBand(group, "BandLow", face, 1.0f, 1.15f, 0.05f, indBandMat);
                    FaceBand(group, "BandHigh", face, 3.0f, 3.12f, 0.05f, indBandMat);
                    pilasterSpacing = 5f; pilasterWidth = 0.45f; pilasterDepth = 0.15f; pilasterMat = indPilasterMat;
                    break;

                case Theme.Lab:
                    // Inaltimile benzilor nu cad pe multipli de 0,2 m (varfurile treptelor), ca sa nu fie in acelasi plan.
                    FaceBand(group, "Wainscot", face, 0.25f, 1.1f, 0.04f, labWainscotMat);
                    FaceBand(group, "Stripe", face, 1.1f, 1.18f, 0.05f, labStripeMat);
                    pilasterSpacing = 6f; pilasterWidth = 0.35f; pilasterDepth = 0.1f; pilasterMat = labPilasterMat;
                    break;

                case Theme.Hangar:
                    FaceBand(group, "SafetyBand", face, 0.9f, 1.05f, 0.04f, hangarBandMat);
                    pilasterSpacing = 6f; pilasterWidth = 0.5f; pilasterDepth = 0.3f; pilasterMat = hangarPilasterMat;
                    break;

                case Theme.Tunnel:
                    FaceBand(group, "HazardBand", face, 0.9f, 1.05f, 0.04f, tunnelBandMat);
                    pilasterSpacing = 5f; pilasterWidth = 0.4f; pilasterDepth = 0.15f; pilasterMat = plinthMat;
                    break;

                default:
                    FaceBand(group, "Wainscot", face, 0.25f, 0.95f, 0.05f, extWainscotMat);
                    pilasterSpacing = 6f; pilasterWidth = 0.5f; pilasterDepth = 0.15f; pilasterMat = extWainscotMat;
                    break;
            }

            if (face.Length < pilasterSpacing * 0.8f)
            {
                return;
            }

            int count = Mathf.FloorToInt(face.Length / pilasterSpacing);
            float step = face.Length / (count + 1);
            bool tunnelFace = face.BaseY < -1f;

            for (int k = 1; k <= count; k++)
            {
                Vector3 ground = face.Start + face.Along * (k * step);
                // Pilastrul urca pana in varful peretelui din spatele lui (sau pana in tavan, daca e mai jos).
                float maxHeight = tunnelFace ? -face.BaseY : WallTopAt(ground - face.Normal * 1f) - face.BaseY;
                float height = ClearHeightAt(ground + face.Normal * 0.4f, maxHeight);

                if (height < 1.5f)
                {
                    continue;
                }

                Vector3 center = ground + face.Normal * (pilasterDepth * 0.5f) + Vector3.up * (height * 0.5f);
                Vector3 size = face.Along * pilasterWidth + Abs(face.Normal) * pilasterDepth + Vector3.up * height;
                Decor(group, $"Pilaster_{k}", center, size, pilasterMat);
            }
        }

        // ------------------------------------------------------------------
        // Coloane pe colturile convexe ale peretilor
        // ------------------------------------------------------------------
        private static void BuildCornerColumns()
        {
            Transform group = CreateGroup(gDetail, "Corner Columns");
            int index = 0;

            for (int i = 0; i < GridX; i++)
            {
                for (int j = 0; j < GridZ; j++)
                {
                    if (IsWalkable(CellCenterX(i), CellCenterZ(j)))
                    {
                        continue;
                    }

                    for (int dx = -1; dx <= 1; dx += 2)
                    {
                        for (int dz = -1; dz <= 1; dz += 2)
                        {
                            int ni = i + dx, nj = j + dz;

                            if (ni < 0 || nj < 0 || ni >= GridX || nj >= GridZ)
                            {
                                continue;
                            }

                            float sideX = CellCenterX(ni), sideZ = CellCenterZ(nj);

                            bool convex =
                                IsWalkable(sideX, CellCenterZ(j)) &&
                                IsWalkable(CellCenterX(i), sideZ) &&
                                IsWalkable(sideX, sideZ);

                            if (!convex)
                            {
                                continue;
                            }

                            float baseY = IsOpenPit(sideX, sideZ) ? TunnelFloorY : 0f;
                            Vector3 corner = new Vector3(
                                MinX + (i + (dx > 0 ? 1 : 0)) * Cell,
                                baseY,
                                MinZ + (j + (dz > 0 ? 1 : 0)) * Cell);

                            // Coltul e deja imbracat (tocul unei usi, un perete despartitor): fara coloana.
                            // Verificam toate cele trei sferturi ale coloanei aflate in afara zidului.
                            bool occupied = false;

                            foreach (Vector3 quarter in new[] { new Vector3(dx, 0f, 0f), new Vector3(0f, 0f, dz), new Vector3(dx, 0f, dz) })
                            {
                                foreach (float checkY in new[] { 1.5f, 5f })
                                {
                                    if (Physics.CheckBox(corner + quarter * 0.2f + Vector3.up * checkY,
                                            new Vector3(0.12f, 0.1f, 0.12f), Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                                    {
                                        occupied = true;
                                    }
                                }
                            }

                            if (occupied)
                            {
                                continue;
                            }

                            // Coloana urca pana in varful blocului de perete (sau pana in tavan).
                            float max = baseY < 0f ? -TunnelFloorY : WallTopAt(new Vector3(CellCenterX(i), 0f, CellCenterZ(j)));
                            float height = ClearHeightAt(corner + new Vector3(dx, 0f, dz) * 0.6f, max);

                            if (height < 1.5f)
                            {
                                continue;
                            }

                            Theme theme = ThemeAt(sideX, sideZ);
                            Material material = theme == Theme.Lab ? labPilasterMat
                                : theme == Theme.Hangar ? hangarPilasterMat
                                : theme == Theme.Industrial ? indPilasterMat
                                : copingMat;

                            // Coloana de 0,7 m centrata pe colt, deci iese 0,35 m din ambele fete.
                            Decor(group, $"Column_{index++:000}",
                                corner + Vector3.up * (height * 0.5f),
                                new Vector3(0.7f, height, 0.7f),
                                material);
                        }
                    }
                }
            }
        }

        /// <summary>Inaltimea varfului peretelui (blocului solid) de la (x, z), masurata de sus.</summary>
        private static float WallTopAt(Vector3 point)
        {
            Vector3 from = new Vector3(point.x, 30f, point.z);

            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 40f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return InnerWallHeight;
        }

        // ------------------------------------------------------------------
        // Capac pe varful liber al peretilor despartitori si al blocurilor
        // ------------------------------------------------------------------

        /// <summary>
        /// Un perete despartitor care nu urca pana in tavan primeste un capac putin mai lat:
        /// muchia de sus arata terminata, nu taiata.
        /// </summary>
        private static void BuildFreeTopCaps()
        {
            Transform group = CreateGroup(gDetail, "Free Top Caps");
            int index = 0;

            foreach (BoxCollider box in gStructures.GetComponentsInChildren<BoxCollider>())
            {
                if (box.isTrigger || box.GetComponent<Destructible>() != null || box.name.Contains("Rail"))
                {
                    continue;
                }

                Transform t = box.transform;
                Vector3 size = t.lossyScale;

                // Doar elemente de tip perete / bloc (nu cover, nu tevi).
                if (size.y < 1.9f || PropKeywordsForCaps(t.name))
                {
                    continue;
                }

                Vector3 top = t.position + Vector3.up * (size.y * 0.5f);

                // Tavan imediat deasupra: peretele e deja legat, nu are nevoie de capac.
                if (Physics.Raycast(top + Vector3.up * 0.02f, Vector3.up, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                {
                    continue;
                }

                // Capacul iese 5 cm doar pe grosimea peretelui; pe lungime se opreste la capetele bucatii,
                // ca bucatile alaturate ale aceluiasi perete sa nu aiba capace suprapuse.
                bool thinX = size.x < 1f && size.x < size.z;
                bool thinZ = size.z < 1f && size.z < size.x;
                bool small = size.x < 1f && size.z < 1f && !thinX && !thinZ;
                bool large = !thinX && !thinZ && !small;

                Vector3 capSize = new Vector3(
                    size.x + (thinX || large ? 0.1f : 0f),
                    0.1f,
                    size.z + (thinZ || large ? 0.1f : 0f));

                // La colturile dintre doi pereti perpendiculari capacele se ating: cele de pe peretii
                // orientati pe Z (subtiri pe X) stau cu 4 mm mai sus.
                float lift = thinX ? 0.004f : small ? 0.008f : 0f;

                GameObject cap = Decor(group, $"Cap_{index++:000}", top + Vector3.up * (0.05f + lift), capSize, copingMat);

                cap.transform.rotation = t.rotation;
                cap.transform.localScale = capSize;
            }
        }

        private static bool PropKeywordsForCaps(string name)
        {
            return name.Contains("Tank") || name.Contains("_Pipe_") || name.Contains("Rock") ||
                   name.Contains("Crate") || name.Contains("Cover") || name.Contains("Container") ||
                   // utilajele primesc piese din kit (fara capac de perete deasupra)
                   name.Contains("Machine") || name.Contains("Generator") || name.Contains("ServerRack") ||
                   name.Contains("Lockers") || name.Contains("Shelf") ||
                   // acoperisurile accesibile au parapet, nu capac
                   name == "Outpost_Tower" || name == "Courtyard_Shed" || name == "Outpost_Shed";
        }

        // ------------------------------------------------------------------
        // Coronament pe blocurile de perete
        // ------------------------------------------------------------------
        private static void BuildWallCoping()
        {
            Transform group = CreateGroup(gDetail, "Wall Coping");
            int index = 0;

            foreach (Transform wall in gWalls)
            {
                if (!wall.name.StartsWith("Wall_"))
                {
                    continue;
                }

                Vector3 size = wall.localScale;
                Vector3 top = wall.position + Vector3.up * (size.y * 0.5f);

                Decor(group, $"Coping_{index++:000}",
                    top + Vector3.up * 0.15f,
                    new Vector3(size.x, 0.3f, size.z),
                    copingMat);
            }
        }

        // ------------------------------------------------------------------
        // Grinzi sub acoperisuri (ferme mai inalte in hangar)
        // ------------------------------------------------------------------
        private static void BuildCeilingBeams()
        {
            int index = 0;

            foreach (PlacedRect roof in placedRoofs)
            {
                float sizeX = roof.XMax - roof.XMin;
                float sizeZ = roof.ZMax - roof.ZMin;

                if (sizeX < 3f || sizeZ < 3f)
                {
                    continue;
                }

                float cx = (roof.XMin + roof.XMax) * 0.5f;
                float cz = (roof.ZMin + roof.ZMax) * 0.5f;
                // Hangarul si halele mari (fostele curti) au ferme; restul grinzi simple.
                Theme roofTheme = ThemeAt(cx, cz);
                bool hangar = roofTheme == Theme.Hangar ||
                              (roofTheme == Theme.Exterior && roof.Y > InnerWallHeight - 0.1f && Mathf.Min(sizeX, sizeZ) >= 8f);

                float depth = hangar ? 0.7f : 0.35f;
                float width = hangar ? 0.35f : 0.25f;
                float spacing = hangar ? 6f : 4f;
                Material material = hangar ? trussMat : beamMat;

                // Grinzile trec peste deschiderea mica; sunt repetate pe deschiderea mare.
                bool spanAlongX = sizeX <= sizeZ;
                float run = spanAlongX ? sizeZ : sizeX;
                int count = Mathf.Max(1, Mathf.FloorToInt(run / spacing));
                float step = run / (count + 1);

                for (int k = 1; k <= count; k++)
                {
                    Vector3 center = spanAlongX
                        ? new Vector3(cx, roof.Y - depth * 0.5f, roof.ZMin + k * step)
                        : new Vector3(roof.XMin + k * step, roof.Y - depth * 0.5f, cz);

                    Vector3 size = spanAlongX
                        ? new Vector3(sizeX, depth, width)
                        : new Vector3(width, depth, sizeZ);

                    Decor(gCeilingDetail, $"Beam_{index++:000}", center, size, material);
                }

                // In hangar: o grinda longitudinala care leaga fermele.
                if (hangar && run > 8f)
                {
                    Vector3 size = spanAlongX
                        ? new Vector3(0.25f, 0.3f, sizeZ)
                        : new Vector3(sizeX, 0.3f, 0.25f);

                    Decor(gCeilingDetail, $"Purlin_{index++:000}",
                        new Vector3(cx, roof.Y - 0.15f, cz), size, trussMat);
                }
            }
        }

        // ------------------------------------------------------------------
        // Balustrade: vizual stalpi + mana curenta + rigla, collider separat
        // ------------------------------------------------------------------
        private static void RailingVisual(Transform parent, string name,
            float x0, float z0, float x1, float z1, float platformHeight)
        {
            Transform group = CreateGroup(parent, name + "_Visual");

            Vector3 a = new Vector3(x0, platformHeight, z0);
            Vector3 b = new Vector3(x1, platformHeight, z1);
            Vector3 dir = (b - a).normalized;
            float length = Vector3.Distance(a, b);

            if (length < 0.05f)
            {
                return;
            }

            bool alongX = Mathf.Abs(dir.x) > Mathf.Abs(dir.z);

            // Barele se opresc in stalpii de capat (jumatate din grosime), ca la un colt
            // doua balustrade sa nu se suprapuna.
            Vector3 railSize(float thickness) => alongX
                ? new Vector3(Mathf.Max(0.01f, length - thickness), thickness, thickness)
                : new Vector3(thickness, thickness, Mathf.Max(0.01f, length - thickness));

            Vector3 mid = (a + b) * 0.5f;
            Decor(group, "TopRail", mid + Vector3.up * 1.0f, railSize(0.07f), frameMat);
            Decor(group, "MidRail", mid + Vector3.up * 0.5f, railSize(0.04f), frameMat);
            Decor(group, "Kick", mid + Vector3.up * 0.06f, railSize(0.12f), frameMat);

            int posts = Mathf.Max(1, Mathf.CeilToInt(length / 1.5f));

            for (int k = 0; k <= posts; k++)
            {
                Vector3 p = a + dir * (length * k / posts);
                Vector3Int key = Vector3Int.RoundToInt(p * 20f);

                if (!placedPosts.Add(key))
                {
                    continue; // stalp comun cu balustrada vecina
                }

                Decor(group, $"Post_{k}", p + Vector3.up * 0.5f, new Vector3(0.07f, 1f, 0.07f), frameMat);
            }
        }

        /// <summary>
        /// Pune balustrade pe toate muchiile libere ale platformelor si planseelor de etaj:
        /// acolo unde dincolo de muchie nu e podea la aceeasi inaltime si nici un perete.
        /// </summary>
        private static void BuildAutoRailings()
        {
            Transform group = CreateGroup(gUpper, "Auto Railings");
            int index = 0;

            foreach (PlacedRect platform in placedPlatforms)
            {
                // Platformele joase de la sol nu au nevoie de balustrada (cele din buncar, da).
                if (platform.Y > -1f && platform.Y < 1f)
                {
                    continue;
                }

                // Patru muchii: (start, end, normala spre exterior)
                Vector3[,] edges =
                {
                    { new Vector3(platform.XMin, 0f, platform.ZMin), new Vector3(platform.XMax, 0f, platform.ZMin), Vector3.back },
                    { new Vector3(platform.XMin, 0f, platform.ZMax), new Vector3(platform.XMax, 0f, platform.ZMax), Vector3.forward },
                    { new Vector3(platform.XMin, 0f, platform.ZMin), new Vector3(platform.XMin, 0f, platform.ZMax), Vector3.left },
                    { new Vector3(platform.XMax, 0f, platform.ZMin), new Vector3(platform.XMax, 0f, platform.ZMax), Vector3.right },
                };

                for (int e = 0; e < 4; e++)
                {
                    Vector3 start = edges[e, 0];
                    Vector3 end = edges[e, 1];
                    Vector3 outward = edges[e, 2];
                    float length = Vector3.Distance(start, end);
                    Vector3 dir = (end - start).normalized;

                    const float step = 0.5f;
                    int samples = Mathf.RoundToInt(length / step);
                    float runFrom = -1f;

                    for (int s = 0; s <= samples; s++)
                    {
                        bool needs = false;

                        if (s < samples)
                        {
                            Vector3 onEdge = start + dir * ((s + 0.5f) * step);
                            needs = EdgeNeedsRailing(onEdge, onEdge + outward * 0.3f, platform.Y);
                        }

                        if (needs && runFrom < 0f)
                        {
                            runFrom = s * step;
                        }
                        else if (!needs && runFrom >= 0f)
                        {
                            float runTo = s * step;

                            if (runTo - runFrom >= 0.9f)
                            {
                                Vector3 p0 = start + dir * runFrom;
                                Vector3 p1 = start + dir * runTo;
                                Railing(group, $"AutoRail_{index++:000}", p0.x, p0.z, p1.x, p1.z, platform.Y);
                            }

                            runFrom = -1f;
                        }
                    }
                }
            }
        }

        private static bool EdgeNeedsRailing(Vector3 onEdge, Vector3 outside, float platformY)
        {
            // Balustrada sau perete deja pe muchie: nu mai punem una.
            if (Physics.CheckBox(
                    new Vector3(onEdge.x, platformY + 0.7f, onEdge.z),
                    new Vector3(0.12f, 0.3f, 0.12f),
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Podea la aceeasi inaltime dincolo de muchie (alta placa, palier, capatul unei scari)?
            Vector3 above = new Vector3(outside.x, platformY + 1.2f, outside.z);

            if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, 1.6f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.point.y > platformY - 0.4f)
            {
                return false;
            }

            // Perete, parapet sau balustrada existenta?
            return !Physics.CheckBox(
                new Vector3(outside.x, platformY + 0.7f, outside.z),
                new Vector3(0.1f, 0.35f, 0.1f),
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        }

        // ------------------------------------------------------------------
        // Marcaje pe podea
        // ------------------------------------------------------------------
        private static void BuildFloorMarkings()
        {
            Transform group = CreateGroup(gFloors, "Floor Markings");

            // Benzi de pericol pe conturul zonelor de plant
            HazardRect(group, "Hazard_SiteA", SiteA, 0.05f);
            HazardRect(group, "Hazard_SiteB", SiteB, 0.05f);

            // Benzi de pericol pe pragul usilor de garaj dintre hangar si Loading Bay
            HazardStrip(group, "Hazard_Garage_W", new Vector3(30f, 0.03f, -38f), new Vector3(35f, 0.03f, -38f), 1.2f);
            HazardStrip(group, "Hazard_Garage_E", new Vector3(50f, 0.03f, -38f), new Vector3(55f, 0.03f, -38f), 1.2f);

            // Litere mari in zonele de plant
            FloorLetter(group, 'A', new Vector3(-42f, 0.06f, 47.5f), 4.5f, 0f);
            FloorLetter(group, 'B', new Vector3(44.5f, 0.06f, -14f), 4.5f, 0f);

            // Sageti de ruta la iesirile din spawn (litera = site-ul spre care duce)
            RouteMarker(group, new Vector3(-47f, 0.03f, -32f), 0f, 'A');
            RouteMarker(group, new Vector3(-50f, 0.03f, -48f), 180f, 'B');
            RouteMarker(group, new Vector3(60f, 0.03f, 46f), 270f, 'A');
            RouteMarker(group, new Vector3(71.5f, 0.03f, 42f), 180f, 'B');
        }

        private static void HazardRect(Transform parent, string name, Area area, float y)
        {
            Transform group = CreateGroup(parent, name);
            HazardStrip(group, "S", new Vector3(area.XMin, y, area.ZMin), new Vector3(area.XMax, y, area.ZMin), 0.35f);
            HazardStrip(group, "N", new Vector3(area.XMin, y, area.ZMax), new Vector3(area.XMax, y, area.ZMax), 0.35f);
            // Laturile de vest si est se opresc la marginea benzilor de nord si sud (fara suprapunere la colturi).
            HazardStrip(group, "W", new Vector3(area.XMin, y, area.ZMin + 0.175f), new Vector3(area.XMin, y, area.ZMax - 0.175f), 0.35f);
            HazardStrip(group, "E", new Vector3(area.XMax, y, area.ZMin + 0.175f), new Vector3(area.XMax, y, area.ZMax - 0.175f), 0.35f);
        }

        /// <summary>Banda galben-negru, in segmente de 0,6 m, de la a la b (paralel cu o axa).</summary>
        private static void HazardStrip(Transform parent, string name, Vector3 a, Vector3 b, float width)
        {
            Transform group = CreateGroup(parent, name);
            bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
            float length = Vector3.Distance(a, b);
            Vector3 dir = (b - a).normalized;

            const float segment = 0.6f;
            int count = Mathf.Max(1, Mathf.RoundToInt(length / segment));
            float step = length / count;

            for (int k = 0; k < count; k++)
            {
                Vector3 center = a + dir * ((k + 0.5f) * step);
                Vector3 size = alongX ? new Vector3(step, 0.01f, width) : new Vector3(width, 0.01f, step);
                Decor(group, $"Seg_{k:000}", center, size, k % 2 == 0 ? hazardYellowMat : hazardBlackMat);
            }
        }

        /// <summary>Litera pictata pe podea, din dungi. Varful literei e pe directia rotationY (0 = nord).</summary>
        private static void FloorLetter(Transform parent, char letter, Vector3 center, float size, float rotationY)
        {
            // Dungi in patratul unitate [-0.5, 0.5], cu "sus" pe +Z.
            Vector4[] strokes;
            float[] widths = null;      // latimea fiecarei dungi, fata de cea comuna
            float[] drops = null;       // cat coboara dunga (m), ca suprapunerile sa nu clipeasca

            switch (letter)
            {
                case 'A':
                    strokes = new[]
                    {
                        new Vector4(-0.35f, -0.5f, 0f, 0.5f),
                        new Vector4(0.35f, -0.5f, 0f, 0.5f),
                        new Vector4(-0.18f, -0.08f, 0.18f, -0.08f),
                    };
                    break;

                default: // 'B'
                    strokes = new[]
                    {
                        new Vector4(-0.3f, -0.5f, -0.3f, 0.5f),
                        new Vector4(-0.3f, 0.5f, 0.15f, 0.5f),
                        new Vector4(-0.3f, 0f, 0.15f, 0f),
                        new Vector4(-0.3f, -0.5f, 0.2f, -0.5f),
                        // burtile, corectate in scena: lipite de barele orizontale; cea de jos sta cu 2 cm mai jos,
                        // ca suprapunerea cu barele sa nu clipeasca
                        new Vector4(0.2521f, 0.4491f, 0.2521f, 0.0779f),
                        new Vector4(0.2487f, -0.0664f, 0.2487f, -0.4064f),
                    };
                    widths = new[] { 1f, 1f, 1f, 1f, 0.9698f, 1f };
                    drops = new[] { 0f, 0f, 0f, 0f, 0f, 0.021f };
                    break;
            }

            Transform group = CreateGroup(parent, $"Letter_{letter}");
            group.position = center;
            group.rotation = Quaternion.Euler(0f, rotationY, 0f);

            float width = size * 0.14f;

            for (int k = 0; k < strokes.Length; k++)
            {
                Vector3 p0 = new Vector3(strokes[k].x, 0f, strokes[k].y) * size;
                Vector3 p1 = new Vector3(strokes[k].z, 0f, strokes[k].w) * size;
                Vector3 local = (p0 + p1) * 0.5f;
                float strokeWidth = widths != null ? width * widths[k] : width;
                float length = Vector3.Distance(p0, p1) + strokeWidth;
                if (drops != null)
                {
                    local.y -= drops[k];
                }

                GameObject stroke = Decor(group, $"Stroke_{k}", Vector3.zero, new Vector3(strokeWidth, 0.01f, length), markingMat);
                stroke.transform.localPosition = local;
                stroke.transform.localRotation = Quaternion.LookRotation((p1 - p0).normalized);
                stroke.transform.localScale = new Vector3(strokeWidth, 0.01f, length);
            }
        }

        /// <summary>Sageata de ruta (corp + varf) urmata de litera site-ului. rotationY: 0 = nord.</summary>
        private static void RouteMarker(Transform parent, Vector3 position, float rotationY, char site)
        {
            Transform group = CreateGroup(parent, $"Route_{site}_{position.x:0}_{position.z:0}");
            group.position = position;
            group.rotation = Quaternion.Euler(0f, rotationY, 0f);

            void Piece(string name, Vector3 local, Vector3 size, float yaw)
            {
                GameObject piece = Decor(group, name, Vector3.zero, size, markingMat);
                piece.transform.localPosition = local;
                piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                piece.transform.localScale = size;
            }

            Piece("Shaft", new Vector3(0f, 0f, -0.2f), new Vector3(0.22f, 0.01f, 1.4f), 0f);
            Piece("HeadL", new Vector3(-0.22f, 0f, 0.35f), new Vector3(0.22f, 0.01f, 0.8f), 45f);
            Piece("HeadR", new Vector3(0.22f, 0f, 0.35f), new Vector3(0.22f, 0.01f, 0.8f), -45f);

            FloorLetter(group, site, position + group.rotation * new Vector3(0f, 0f, -1.9f), 1.2f, rotationY);
        }

        // ------------------------------------------------------------------
        // Lumina: fara colturi complet intunecate in interioare
        // ------------------------------------------------------------------
        private static void FillDarkInteriors()
        {
            const float coverage = 9f;
            float[] levels = { BunkerFloorY, TunnelFloorY, 0f, UpperFloorHeight };
            int added = 0;

            for (int i = 0; i < GridX; i++)
            {
                for (int j = 0; j < GridZ; j++)
                {
                    float x = CellCenterX(i);
                    float z = CellCenterZ(j);

                    foreach (float level in levels)
                    {
                        Vector3 p = new Vector3(x, level + 1f, z);

                        // Un loc unde poate sta un jucator: podea la nivelul asta si spatiu liber.
                        if (!Physics.Raycast(p, Vector3.down, out RaycastHit floor, 1.3f, ~0, QueryTriggerInteraction.Ignore) ||
                            Mathf.Abs(floor.point.y - level) > 0.35f ||
                            Physics.CheckSphere(p, 0.3f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        Theme theme = level < -1f ? Theme.Tunnel : ThemeAt(x, z);

                        // Exteriorul e luminat de soare; ne intereseaza doar interioarele acoperite.
                        if (theme == Theme.Exterior ||
                            !Physics.Raycast(p, Vector3.up, out RaycastHit ceiling, 9f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            continue;
                        }

                        bool lit = false;

                        foreach (Transform light in placedLights)
                        {
                            Vector3 lp = light.position;

                            if (Mathf.Abs(lp.y - (level + 2f)) < 3.5f &&
                                new Vector2(lp.x - x, lp.z - z).sqrMagnitude < coverage * coverage)
                            {
                                lit = true;
                                break;
                            }
                        }

                        if (lit)
                        {
                            continue;
                        }

                        float y = Mathf.Min(ceiling.point.y - 0.35f, level + 3.4f);
                        Lamp($"Light_Fill_{added++:000}", x, y, z, LampColorOf(theme), 15f, 4f);
                    }
                }
            }

            Debug.Log($"Finisare: {added} lampi adaugate in interioarele fara lumina.");
        }

        /// <summary>Corp de iluminat la fiecare lumina punctuala, cu tija pana in tavan.</summary>
        private static void BuildLampFixtures()
        {
            int index = 0;

            foreach (Transform light in placedLights)
            {
                Vector3 p = light.position;
                Transform group = CreateGroup(gCeilingDetail, $"Fixture_{index++:000}");

                Decor(group, "Lamp", p + Vector3.up * 0.12f, new Vector3(0.9f, 0.1f, 0.35f), fixtureMat);
                Decor(group, "Housing", p + Vector3.up * 0.2f, new Vector3(1f, 0.08f, 0.45f), frameMat);

                if (Physics.Raycast(p + Vector3.up * 0.3f, Vector3.up, out RaycastHit ceiling, 6f, ~0, QueryTriggerInteraction.Ignore))
                {
                    float top = ceiling.point.y;
                    float bottom = p.y + 0.24f;

                    if (top - bottom > 0.05f)
                    {
                        Decor(group, "Rod", new Vector3(p.x, (top + bottom) * 0.5f, p.z),
                            new Vector3(0.05f, top - bottom, 0.05f), frameMat);
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Rame la usi si ferestre
        // ------------------------------------------------------------------

        /// <summary>
        /// Rama de usa: doi montanti, un buiandrug si un prag. mid = mijlocul golului la sol;
        /// wallAlongX = peretele se intinde pe X; depth = grosimea peretelui.
        /// </summary>
        private static void DoorFrame(Transform parent, string name, Vector3 mid, bool wallAlongX,
            float width, float height, float depth, float baseY)
        {
            Transform group = CreateGroup(parent, name + "_Frame");
            const float trim = 0.16f;
            float frameDepth = depth + 0.1f;

            Vector3 along = wallAlongX ? Vector3.right : Vector3.forward;
            Vector3 across = wallAlongX ? Vector3.forward : Vector3.right;
            Vector3 bottom = new Vector3(mid.x, baseY, mid.z);

            Vector3 jambSize = along * trim + across * frameDepth + Vector3.up * height;
            Decor(group, "Jamb_L", bottom - along * (width * 0.5f + trim * 0.5f) + Vector3.up * (height * 0.5f), jambSize, frameMat);
            Decor(group, "Jamb_R", bottom + along * (width * 0.5f + trim * 0.5f) + Vector3.up * (height * 0.5f), jambSize, frameMat);

            Decor(group, "Head", bottom + Vector3.up * (height + trim * 0.5f),
                along * (width + trim * 2f) + across * frameDepth + Vector3.up * trim, frameMat);

            Decor(group, "Threshold", bottom + Vector3.up * 0.015f,
                along * width + across * depth + Vector3.up * 0.03f, plinthMat);
        }

        /// <summary>Rama de fereastra cu pervaz iesit. baseY = inaltimea pervazului.</summary>
        private static void WindowFrame(Transform parent, string name, Vector3 mid, bool wallAlongX,
            float width, float height, float depth, float baseY)
        {
            Transform group = CreateGroup(parent, name + "_Frame");
            const float trim = 0.12f;
            float frameDepth = depth + 0.08f;

            Vector3 along = wallAlongX ? Vector3.right : Vector3.forward;
            Vector3 across = wallAlongX ? Vector3.forward : Vector3.right;
            Vector3 bottom = new Vector3(mid.x, baseY, mid.z);

            Vector3 jambSize = along * trim + across * frameDepth + Vector3.up * height;
            Decor(group, "Jamb_L", bottom - along * (width * 0.5f + trim * 0.5f) + Vector3.up * (height * 0.5f), jambSize, frameMat);
            Decor(group, "Jamb_R", bottom + along * (width * 0.5f + trim * 0.5f) + Vector3.up * (height * 0.5f), jambSize, frameMat);
            Decor(group, "Head", bottom + Vector3.up * (height + trim * 0.5f),
                along * (width + trim * 2f) + across * frameDepth + Vector3.up * trim, frameMat);
            Decor(group, "Sill", bottom + Vector3.up * 0.04f,
                along * (width + 0.3f) + across * (depth + 0.25f) + Vector3.up * 0.08f, copingMat);
        }
    }
}
