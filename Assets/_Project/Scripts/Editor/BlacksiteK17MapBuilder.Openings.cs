using System.Collections.Generic;
using UnityEngine;

namespace ChokeHold.EditorTools
{
    // Pereti despartitori cu goluri de dimensiuni standard si cover de dimensiuni standard.
    //
    // Standard de navigatie (metri):
    //   usa simpla   1,4 x 2,4        usa dubla    3,0 x 2,4       usa de garaj  latime libera x 3,2
    //   fereastra    pervaz la 1,0, deschidere de 1,2 inaltime     trapa         1,2 x 1,2
    //
    // Standard de cover (metri):
    //   crouch   1,1 inaltime, minimum 2 latime        standing  2,0 inaltime, minimum 2 latime
    //   lada     2 x 2 x 2        stiva de lazi  2 x 2 x 3,5
    //   container 20 ft  2,6 x 2,6 x 6        container 40 ft  2,6 x 2,6 x 12
    public static partial class BlacksiteK17MapBuilder
    {
        private const float DoorWidth = 1.4f;
        private const float DoubleDoorWidth = 3f;
        private const float DoorHeight = 2.4f;
        private const float GarageHeight = 3.2f;
        private const float WindowSill = 1f;
        private const float WindowHeight = 1.2f;
        private const float HatchSize = 1.2f;

        private const float CrouchCoverHeight = 1.1f;
        private const float StandingCoverHeight = 2f;
        private const float MinCoverWidth = 2f;

        private const float PartitionThickness = 0.5f;

        private enum GapKind
        {
            Open,
            Door,
            DoubleDoor,
            Garage,
            Window,
            Soft
        }

        /// <summary>Un gol intr-un perete despartitor. At = distanta de la capatul de start pana la mijlocul golului.</summary>
        private struct Gap
        {
            public float At;
            public float Width;
            public float BaseY;
            public float Height;
            public GapKind Kind;
            public bool Barricade;
            public bool Hinged;

            /// <summary>
            /// Paravan in spatele golului: +1 = pe partea pozitiva a normalei peretelui (X sau Z crescator),
            /// -1 = pe partea negativa, 0 = fara. Din pragul usii raman doua unghiuri: stanga si dreapta paravanului.
            /// </summary>
            public int ScreenSide;

            public float Start => At - Width * 0.5f;
            public float End => At + Width * 0.5f;
        }

        private static Gap DoorGap(float at, bool hinged = false, bool barricade = false)
        {
            return new Gap { At = at, Width = DoorWidth, Height = DoorHeight, Kind = GapKind.Door, Hinged = hinged, Barricade = barricade };
        }

        private static Gap DoubleGap(float at, bool barricade = false)
        {
            return new Gap { At = at, Width = DoubleDoorWidth, Height = DoorHeight, Kind = GapKind.DoubleDoor, Barricade = barricade };
        }

        /// <summary>Adauga un paravan (cover in picioare) la 3 m in spatele golului, pe partea data.</summary>
        private static Gap Screened(this Gap gap, int side)
        {
            gap.ScreenSide = side;
            return gap;
        }

        private static Gap GarageGap(float at, float width)
        {
            return new Gap { At = at, Width = width, Height = GarageHeight, Kind = GapKind.Garage };
        }

        private static Gap WindowGap(float at, float width = 1.6f, bool barricade = true)
        {
            return new Gap { At = at, Width = width, BaseY = WindowSill, Height = WindowHeight, Kind = GapKind.Window, Barricade = barricade };
        }

        private static Gap SoftGap(float at, float width, float height = 3f)
        {
            return new Gap { At = at, Width = width, Height = height, Kind = GapKind.Soft };
        }

        /// <summary>Gol liber la inaltime (ex. trecerea unei pasarele prin perete).</summary>
        private static Gap UpperGap(float at, float width, float baseY, float height = 2.6f)
        {
            return new Gap { At = at, Width = width, BaseY = baseY, Height = height, Kind = GapKind.Open };
        }

        /// <summary>
        /// Perete despartitor de la (x0, z0) la (x1, z1), paralel cu o axa, de la sol pana la topY,
        /// cu golurile date. Transforma o deschidere lata dintre doua zone in intrari controlate.
        /// </summary>
        private static void Partition(string name, float x0, float z0, float x1, float z1, float topY, params Gap[] gaps)
        {
            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
            float sign = alongX ? Mathf.Sign(x1 - x0) : Mathf.Sign(z1 - z0);

            // Capetele se lipesc de zidul / peretele cel mai apropiat (daca e la mai putin de 1 m),
            // ca sa nu ramana fante intre peretele despartitor si restul constructiei.
            Vector3 axis = alongX ? new Vector3(sign, 0f, 0f) : new Vector3(0f, 0f, sign);
            float extendStart = GapToStructure(new Vector3(x0, 0f, z0), -axis, topY);
            float extendEnd = GapToStructure(new Vector3(x1, 0f, z1), axis, topY);

            x0 -= axis.x * extendStart;
            z0 -= axis.z * extendStart;
            x1 += axis.x * extendEnd;
            z1 += axis.z * extendEnd;

            if (extendStart > 0f)
            {
                for (int g = 0; g < gaps.Length; g++)
                {
                    gaps[g].At += extendStart;
                }
            }

            float length = alongX ? Mathf.Abs(x1 - x0) : Mathf.Abs(z1 - z0);

            Transform group = CreateGroup(gStructures, name);

            Vector3 PointAt(float distance, float y)
            {
                return alongX
                    ? new Vector3(x0 + sign * distance, y, z0)
                    : new Vector3(x0, y, z0 + sign * distance);
            }

            // Taiem peretele in felii pe lungime, la marginile golurilor.
            List<float> cuts = new List<float> { 0f, length };

            foreach (Gap gap in gaps)
            {
                cuts.Add(Mathf.Clamp(gap.Start, 0f, length));
                cuts.Add(Mathf.Clamp(gap.End, 0f, length));
            }

            cuts.Sort();

            int piece = 0;

            for (int i = 0; i < cuts.Count - 1; i++)
            {
                float from = cuts[i];
                float to = cuts[i + 1];

                if (to - from < 0.02f)
                {
                    continue;
                }

                float mid = (from + to) * 0.5f;

                // Golurile verticale din aceasta felie, sortate de jos in sus.
                List<Vector2> holes = new List<Vector2>();

                foreach (Gap gap in gaps)
                {
                    if (mid > gap.Start && mid < gap.End)
                    {
                        // Trecere la etaj: placa (galerie, pasarela) umple grosimea planseului,
                        // asa ca peretele de dedesubt se opreste sub ea, nu in acelasi plan cu podeaua.
                        float holeBottom = gap.Kind == GapKind.Open && gap.BaseY >= 1f
                            ? gap.BaseY - SlabThickness
                            : gap.BaseY;

                        holes.Add(new Vector2(
                            Mathf.Clamp(holeBottom, 0f, topY),
                            Mathf.Clamp(gap.BaseY + gap.Height, 0f, topY)));
                    }
                }

                holes.Sort((a, b) => a.x.CompareTo(b.x));

                float cursor = 0f;

                foreach (Vector2 hole in holes)
                {
                    if (hole.x - cursor > 0.02f)
                    {
                        PartitionPiece(group, $"{name}_{piece++:00}", PointAt(mid, 0f), to - from, cursor, hole.x, alongX);
                    }

                    cursor = Mathf.Max(cursor, hole.y);
                }

                if (topY - cursor > 0.02f)
                {
                    PartitionPiece(group, $"{name}_{piece++:00}", PointAt(mid, 0f), to - from, cursor, topY, alongX);
                }
            }

            // Ce sta in goluri: pereti moi, baricade, usi.
            int index = 0;

            foreach (Gap gap in gaps)
            {
                index++;
                Vector3 start = PointAt(gap.Start, 0f);
                Vector3 end = PointAt(gap.End, 0f);
                Vector3 middle = PointAt(gap.At, 0f);

                if (gap.Kind == GapKind.Soft)
                {
                    SoftWall(group, $"{name}_Soft{index}", start.x, start.z, end.x, end.z, gap.BaseY, gap.Height);
                }

                if (gap.Kind == GapKind.Door || gap.Kind == GapKind.DoubleDoor || gap.Kind == GapKind.Garage)
                {
                    DoorFrame(group, $"{name}_Gap{index}", middle, alongX, gap.Width, gap.Height, PartitionThickness, gap.BaseY);
                }

                if (gap.Kind == GapKind.Window)
                {
                    WindowFrame(group, $"{name}_Gap{index}", middle, alongX, gap.Width, gap.Height, PartitionThickness, gap.BaseY);
                }

                if (gap.Barricade)
                {
                    Barricade(gBarricades, $"Barricade_{name}_{index}", middle.x, middle.z, gap.BaseY, gap.Width, gap.Height, alongX);
                }

                if (gap.ScreenSide != 0)
                {
                    const float distance = 3f;
                    float half = gap.Width * 0.5f + 0.75f;

                    Vector3 normal = (alongX ? Vector3.forward : Vector3.right) * gap.ScreenSide;
                    Vector3 a = PointAt(gap.At - half, 0f) + normal * distance;
                    Vector3 b = PointAt(gap.At + half, 0f) + normal * distance;

                    HighCover(a.x, a.z, b.x, b.z, gap.BaseY);
                }

                if (gap.Hinged)
                {
                    Vector3 hinge = sign > 0f ? start : end;
                    Door(gDoors, $"Door_{name}_{index}", hinge, alongX ? Vector3.right : Vector3.forward, gap.Width);
                }
            }
        }

        private static void PartitionPiece(Transform parent, string name, Vector3 groundPoint,
            float lengthAlong, float fromY, float toY, bool alongX)
        {
            Vector3 size = alongX
                ? new Vector3(lengthAlong, toY - fromY, PartitionThickness)
                : new Vector3(PartitionThickness, toY - fromY, lengthAlong);

            CreateBox(parent, name, groundPoint + Vector3.up * (fromY + toY) * 0.5f, size, wallMat);

            // Plinta pe ambele fete (o singura cutie putin mai groasa decat peretele).
            // E cu 1-2 cm mai joasa decat plinta zidurilor (0,25 m), diferita pe cele doua axe:
            // la imbinari si colturi fetele de sus nu stau in acelasi plan.
            if (fromY < 0.01f && toY > 0.3f)
            {
                float plinthHeight = alongX ? 0.24f : 0.23f;

                Vector3 plinth = alongX
                    ? new Vector3(lengthAlong, plinthHeight, PartitionThickness + 0.12f)
                    : new Vector3(PartitionThickness + 0.12f, plinthHeight, lengthAlong);

                Decor(parent, name + "_Plinth", groundPoint + Vector3.up * (plinthHeight * 0.5f), plinth, plinthMat);
            }
        }

        /// <summary>
        /// Usa standard (1,4 x 2,4 m) intr-un gol de 2 m dintr-un perete gros: doi montanti, buiandrug
        /// pana la top si, optional, canatul care se deschide cu F.
        /// passageAlongX = true daca prin usa se trece pe axa X.
        /// </summary>
        private static void DoorCell(string name,
            float cellXMin, float cellZMin, float cellXMax, float cellZMax,
            bool passageAlongX, float top, bool hinged = true)
        {
            Transform group = CreateGroup(gDoors, name);

            float midX = (cellXMin + cellXMax) * 0.5f;
            float midZ = (cellZMin + cellZMax) * 0.5f;

            // Latimea golului (perpendicular pe directia de trecere) si grosimea peretelui.
            float gapWidth = passageAlongX ? cellZMax - cellZMin : cellXMax - cellXMin;
            float jamb = Mathf.Max(0f, (gapWidth - DoorWidth) * 0.5f);

            float wallDepth = passageAlongX ? cellXMax - cellXMin : cellZMax - cellZMin;
            DoorFrame(group, "Door", new Vector3(midX, 0f, midZ), !passageAlongX, DoorWidth, DoorHeight, wallDepth, 0f);

            if (passageAlongX)
            {
                if (jamb > 0.01f)
                {
                    SolidBlock(group, "Jamb_A", cellXMin, cellZMin, cellXMax, cellZMin + jamb, 0f, top, wallMat);
                    SolidBlock(group, "Jamb_B", cellXMin, cellZMax - jamb, cellXMax, cellZMax, 0f, top, wallMat);
                }

                SolidBlock(group, "Lintel", cellXMin, cellZMin + jamb, cellXMax, cellZMax - jamb, DoorHeight, top - DoorHeight, wallMat);

                if (hinged)
                {
                    Door(group, "Leaf", new Vector3(midX, 0f, cellZMin + jamb), Vector3.forward, DoorWidth);
                }
            }
            else
            {
                if (jamb > 0.01f)
                {
                    SolidBlock(group, "Jamb_A", cellXMin, cellZMin, cellXMin + jamb, cellZMax, 0f, top, wallMat);
                    SolidBlock(group, "Jamb_B", cellXMax - jamb, cellZMin, cellXMax, cellZMax, 0f, top, wallMat);
                }

                SolidBlock(group, "Lintel", cellXMin + jamb, cellZMin, cellXMax - jamb, cellZMax, DoorHeight, top - DoorHeight, wallMat);

                if (hinged)
                {
                    Door(group, "Leaf", new Vector3(cellXMin + jamb, 0f, midZ), Vector3.right, DoorWidth);
                }
            }
        }

        /// <summary>
        /// Trapa standard (1,2 x 1,2 m) intr-un gol de 2 x 2 m din planseu: rama plina in jur
        /// si un panou care se sparge cu gloante.
        /// </summary>
        private static void StandardHatch(Transform parent, string name,
            float xMin, float zMin, float xMax, float zMax, float top)
        {
            float cx = (xMin + xMax) * 0.5f;
            float cz = (zMin + zMax) * 0.5f;
            float half = HatchSize * 0.5f;

            Transform group = CreateGroup(parent, name);
            float baseY = top - SlabThickness;

            SolidBlock(group, "Frame_W", xMin, zMin, cx - half, zMax, baseY, SlabThickness, upperFloorMat);
            SolidBlock(group, "Frame_E", cx + half, zMin, xMax, zMax, baseY, SlabThickness, upperFloorMat);
            SolidBlock(group, "Frame_S", cx - half, zMin, cx + half, cz - half, baseY, SlabThickness, upperFloorMat);
            SolidBlock(group, "Frame_N", cx - half, cz + half, cx + half, zMax, baseY, SlabThickness, upperFloorMat);

            Hatch(group, "Panel", cx - half, cz - half, cx + half, cz + half, top);
        }

        // ------------------------------------------------------------------
        // Cover de dimensiuni standard
        // ------------------------------------------------------------------

        /// <summary>Cover de crouch (1,1 m), de la (x0, z0) la (x1, z1), paralel cu o axa.</summary>
        private static void LowCover(float x0, float z0, float x1, float z1, float baseY = 0f)
        {
            CoverWall("LowCover", x0, z0, x1, z1, baseY, CrouchCoverHeight, 0.6f);
        }

        /// <summary>Cover in picioare (2 m), de la (x0, z0) la (x1, z1), paralel cu o axa.</summary>
        private static void HighCover(float x0, float z0, float x1, float z1, float baseY = 0f, float minLength = MinCoverWidth)
        {
            CoverWall("HighCover", x0, z0, x1, z1, baseY, StandingCoverHeight, 0.8f, minLength);
        }

        private static void CoverWall(string prefix, float x0, float z0, float x1, float z1,
            float baseY, float height, float thickness, float minLength = MinCoverWidth)
        {
            bool alongX = Mathf.Abs(x1 - x0) > Mathf.Abs(z1 - z0);
            float length = Mathf.Max(minLength, alongX ? Mathf.Abs(x1 - x0) : Mathf.Abs(z1 - z0));

            Vector3 size = alongX
                ? new Vector3(length, height, thickness)
                : new Vector3(thickness, height, length);

            CreateBox(
                gCover,
                $"{prefix}_{coverIndex++:000}",
                new Vector3((x0 + x1) * 0.5f, baseY + height * 0.5f, (z0 + z1) * 0.5f),
                size,
                coverMat
            );
        }

        /// <summary>Lada mare 2 x 2 x 2 m (standing cover).</summary>
        private static void BigCrate(float x, float z, float baseY = 0f)
        {
            CreateBox(gCover, $"BigCrate_{coverIndex++:000}",
                new Vector3(x, baseY + 1f, z), new Vector3(2f, 2f, 2f), coverMat);
        }

        /// <summary>Stiva de lazi 2 x 2 x 3,5 m: blocheaza si vederea de pe un obiect de 1-2 m.</summary>
        private static void CrateStack(float x, float z, float baseY = 0f)
        {
            CreateBox(gCover, $"CrateStack_{coverIndex++:000}",
                new Vector3(x, baseY + 1.75f, z), new Vector3(2f, 3.5f, 2f), coverMat);
        }

        /// <summary>Container de 40 ft (2,6 x 2,6 x 12 m). rotationY = 0: lung pe axa Z.</summary>
        private static void Container40(float x, float z, float rotationY, Material material, float baseY = 0f)
        {
            GameObject container = CreateBox(
                gCover,
                $"Container40_{coverIndex++:000}",
                new Vector3(x, baseY + 1.3f, z),
                new Vector3(2.6f, 2.6f, 12f),
                material
            );

            container.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        }
    }
}
