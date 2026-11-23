using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
{
    public sealed partial class NeighborhoodSports
    {
        private const float LoftFloor = 3.39f;

        private void BuildDuplexHomes(Shader shader, Material plaster, Material green)
        {
            Transform atelier = null;
            Transform conservatory = null;
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (child.name == "Selman_EnterableHome_No62") atelier = child;
                    if (child.name == "Zubeyde_SouthHouse_3") conservatory = child;
                }
                if (atelier != null && conservatory != null) break;
            }
            if (atelier == null && conservatory == null) return;

            Material oak = MaterialFor(shader, "Honey oak", new Color(0.51f, 0.31f, 0.16f), 0.34f);
            Material walnut = MaterialFor(shader, "Dark oiled walnut", new Color(0.22f, 0.125f, 0.071f), 0.31f);
            Material clay = MaterialFor(shader, "Warm interior terracotta", new Color(0.64f, 0.25f, 0.15f), 0.2f);
            Material blue = MaterialFor(shader, "Zubeyde blue plaster", new Color(0.35f, 0.57f, 0.67f), 0.13f);
            Material linen = MaterialFor(shader, "Undyed woven linen", new Color(0.74f, 0.64f, 0.44f), 0.18f);

            if (atelier != null)
            {
                Transform ceiling = atelier.Find("InteriorWallsAndFloors_Theme2/InteriorCeiling");
                if (ceiling != null)
                {
                    Transform loft = Child(atelier, "Music and art loft", Vector3.zero);
                    Transform bed = atelier.Find("InteriorFurniture/Atelier_Bed");
                    float stairZ = bed != null ? Mathf.Sign(bed.localPosition.z) * 3.72f : 3.72f;
                    AddLoftAndStair(loft, 4.95f, 5.35f, -1.50f, 4.18f, stairZ, oak, walnut, plaster);
                    BuildMusicRoom(loft, stairZ, oak, walnut, plaster, clay, green, linen);
                    ceiling.gameObject.SetActive(false);
                }
            }

            if (conservatory != null)
            {
                // Keep the original street position and orientation while replacing the closed display shell.
                Transform shell = conservatory.Find("Zubeyde_SouthHouse_3_Mesh");
                if (shell != null)
                {
                    Transform home = Child(conservatory, "Conservatory duplex", Vector3.zero);
                    home.localRotation = Quaternion.Euler(0f, -90f, 0f);
                    BuildConservatory(home, blue, plaster, oak, walnut, clay, green, linen);
                    shell.gameObject.SetActive(false);
                    foreach (BoxCollider collider in conservatory.GetComponents<BoxCollider>()) collider.enabled = false;
                }
            }
        }

        private void BuildConservatory(Transform home, Material blue, Material plaster, Material oak, Material walnut,
            Material clay, Material green, Material linen)
        {
            MeshParts shell = new MeshParts(this);
            Solid(shell, home, blue, "Back wall", new Vector3(-4.30f, 3f, 0f), new Vector3(0.23f, 6f, 8.72f));
            foreach (float side in new[] { -1f, 1f })
            {
                Solid(shell, home, blue, "Side wall", new Vector3(0f, 3f, side * 4.26f), new Vector3(8.63f, 6f, 0.22f));
                Solid(shell, home, blue, "Front wing", new Vector3(4.30f, 3f, side * 2.73f),
                    new Vector3(0.24f, 6f, 3.21f));
                shell.Box(plaster, new Vector3(4.46f, 3f, side * 4.11f), new Vector3(0.12f, 6f, 0.14f));
            }
            Solid(shell, home, blue, "Beam above entrance", new Vector3(4.3f, 3.19f, 0f), new Vector3(0.24f, 1.68f, 2.32f));
            Solid(shell, home, plaster, "Window header", new Vector3(4.30f, 5.78f, 0f), new Vector3(0.24f, 0.44f, 2.32f));
            AddCollider(home, "Window glazing", new Vector3(4.31f, 4.82f, 0f), new Vector3(0.035f, 1.65f, 2.31f));
            for (int divider = -2; divider <= 2; divider++)
                shell.Box(walnut, new Vector3(4.34f, 4.82f, divider * 0.55f), new Vector3(0.11f, 1.65f, 0.06f));
            shell.Box(walnut, new Vector3(4.34f, 4.68f, 0f), new Vector3(0.11f, 0.075f, 2.29f));
            shell.Box(plaster, new Vector3(4.44f, 3.97f, 0f), new Vector3(0.38f, 0.10f, 2.52f));
            shell.Box(walnut, new Vector3(4.43f, 3.43f, 0f), new Vector3(0.12f, 0.20f, 8.76f));
            Solid(shell, home, oak, "Ground floor", new Vector3(0f, 0.025f, 0f), new Vector3(8.55f, 0.05f, 8.46f));
            Solid(shell, home, plaster, "Front step", new Vector3(4.78f, 0.044f, 0f), new Vector3(1.13f, 0.088f, 2.32f));
            shell.Add(clay, _cube, new Vector3(2.22f, 6.61f, 0f), Quaternion.Euler(0f, 0f, -16f),
                new Vector3(4.78f, 0.24f, 9.22f));
            shell.Add(clay, _cube, new Vector3(-2.22f, 6.61f, 0f), Quaternion.Euler(0f, 0f, 16f),
                new Vector3(4.78f, 0.24f, 9.22f));
            Mesh gable = new Mesh { name = "Duplex gable",
                vertices = new[] { new Vector3(-4.30f, 5.94f, 0f), new Vector3(4.30f, 5.94f, 0f), new Vector3(0f, 7.20f, 0f) },
                triangles = new[] { 0, 1, 2 } };
            gable.RecalculateNormals();
            _ownedAssets.Add(gable);
            shell.Add(blue, gable, Vector3.forward * 4.27f, Quaternion.identity, Vector3.one);
            shell.Add(blue, gable, Vector3.back * 4.27f, Quaternion.Euler(0f, 180f, 0f), Vector3.one);
            shell.Emit(home, "Blue duplex shell");
            AddLoftAndStair(home, 4.22f, 4.15f, -1.73f, 3.56f, 3.06f, oak, walnut, plaster);

            MeshParts room = new MeshParts(this);
            Rug(room, clay, linen, new Vector3(-0.9f, 0.055f, -1.45f));
            BuildBench(room, home, walnut, linen, new Vector3(-3.44f, 0.05f, -1.48f), "conservatory_tea_seat");
            Solid(room, home, oak, "Low tea table", new Vector3(-1.92f, 0.25f, -1.43f), new Vector3(0.69f, 0.39f, 1.11f));
            for (int cup = -1; cup <= 1; cup++)
                room.Add(clay, _cylinder, new Vector3(-1.92f, 0.49f, -1.42f + cup * 0.27f), Quaternion.identity,
                    new Vector3(0.083f, 0.047f, 0.083f));
            Solid(room, home, walnut, "Herb drying shelves", new Vector3(-3.76f, 0.92f, 1.58f), new Vector3(0.61f, 1.74f, 1.89f));
            for (int i = 0; i < 5; i++)
                room.Box(linen, new Vector3(-3.43f, 0.39f + i * 0.28f, 1.58f), new Vector3(0.045f, 0.08f, 1.60f));

            BuildBench(room, home, oak, green, new Vector3(-1.35f, LoftFloor, -3.22f), "conservatory_reading_seat");
            Rug(room, green, linen, new Vector3(0f, LoftFloor + 0.01f, -1.6f));
            BuildBookcase(room, home, walnut, linen, clay, green, new Vector3(-3.84f, LoftFloor, -2.83f));
            Solid(room, home, oak, "Gardening workbench", new Vector3(2.95f, LoftFloor + 0.43f, -2.78f), new Vector3(0.87f, 0.86f, 1.77f));
            for (int plant = 0; plant < 6; plant++)
            {
                float z = -3.44f + plant * 0.50f;
                float x = plant % 2 == 0 ? 3.63f : 3.82f;
                Plant(room, green, clay, new Vector3(x, LoftFloor, z), 0.87f + 0.16f * (plant % 3), plant);
            }
            for (int plant = 0; plant < 4; plant++)
                Plant(room, green, clay, new Vector3(2.98f, LoftFloor + 0.86f, -3.32f + plant * 0.34f), 0.62f, plant + 1);
            Vector3 can = new Vector3(2.86f, LoftFloor + 1.02f, -2.02f);
            room.Add(green, _cylinder, can, Quaternion.identity, new Vector3(0.27f, 0.14f, 0.27f));
            room.Ring(green, 0.12f, 0.095f, 0.014f, 14, Quaternion.Euler(0f, 90f, 0f), can + new Vector3(0f, 0.05f, 0.15f));
            room.Rod(green, _cylinder, can + Vector3.back * 0.073f, can + new Vector3(0f, 0.17f, -0.24f), 0.023f);
            room.Emit(home, "Tea room and plant loft furniture");
            AddRoomZone(home, LoftFloor * 0.5f, LoftFloor, 8.2f, 8.1f, new Vector3(-0.7f, 2.65f, -0.8f), new Color(1f, 0.78f, 0.54f));
            AddRoomZone(home, 4.50f, 2.23f, 8.2f, 8.1f, new Vector3(0.6f, 5.13f, -1.2f), new Color(0.88f, 0.95f, 1f));
        }

        private void AddLoftAndStair(Transform home, float halfX, float halfZ, float topX, float bottomX, float stairZ,
            Material oak, Material walnut, Material plaster)
        {
            const float stairWidth = 1.42f;
            const float edge = 0.04f;
            float nearZ = stairZ - stairWidth * 0.5f - edge;
            float farZ = stairZ + stairWidth * 0.5f + edge;
            MeshParts deck = new MeshParts(this);
            AddDeckSection(deck, home, oak, -halfX, topX, -halfZ, halfZ);
            AddDeckSection(deck, home, oak, bottomX, halfX, -halfZ, halfZ);
            AddDeckSection(deck, home, oak, topX, bottomX, -halfZ, nearZ);
            AddDeckSection(deck, home, oak, topX, bottomX, farZ, halfZ);
            Solid(deck, home, plaster, "Upper ceiling", new Vector3(0f, 5.61f, 0f), new Vector3(halfX * 2f, 0.12f, halfZ * 2f));

            const int steps = 17;
            const float firstFloor = 0.05f;
            float tread = (bottomX - topX) / steps;
            float rise = (LoftFloor - firstFloor) / steps;
            for (int step = 0; step < steps; step++)
            {
                float x = bottomX - (step + 0.5f) * tread;
                float height = rise * (step + 1);
                Solid(deck, home, oak, "Stair tread", new Vector3(x, firstFloor + height * 0.5f, stairZ),
                    new Vector3(tread + 0.002f, height, stairWidth));
                deck.Box(walnut, new Vector3(x + tread * 0.45f, firstFloor + height - 0.028f, stairZ),
                    new Vector3(0.036f, 0.056f, stairWidth + 0.031f));
            }

            foreach (float railZ in new[] { nearZ, farZ })
            {
                deck.Rod(walnut, _cylinder, new Vector3(bottomX, firstFloor + 0.90f, railZ),
                    new Vector3(topX, LoftFloor + 0.90f, railZ), 0.049f);
                deck.Box(walnut, new Vector3((topX + bottomX) * 0.5f, LoftFloor + 0.98f, railZ),
                    new Vector3(bottomX - topX, 0.086f, 0.10f));
                AddCollider(home, "Loft guard", new Vector3((topX + bottomX) * 0.5f, LoftFloor + 0.52f, railZ),
                    new Vector3(bottomX - topX, 1.04f, 0.094f));
                for (int post = 0; post <= 14; post++)
                {
                    float t = post / 14f;
                    float x = Mathf.Lerp(bottomX, topX, t);
                    deck.Box(walnut, new Vector3(x, Mathf.Lerp(firstFloor, LoftFloor, t) + 0.46f, railZ), new Vector3(0.052f, 0.92f, 0.052f));
                    deck.Box(walnut, new Vector3(x, LoftFloor + 0.47f, railZ), new Vector3(0.048f, 0.94f, 0.048f));
                }
            }
            Solid(deck, home, walnut, "Guard above first step", new Vector3(bottomX, LoftFloor + 0.48f, stairZ),
                new Vector3(0.085f, 0.97f, stairWidth + 0.12f));
            deck.Emit(home, "Walkable upper floor and stairs");
        }

        private static void AddDeckSection(MeshParts mesh, Transform home, Material oak,
            float minX, float maxX, float minZ, float maxZ)
        {
            Solid(mesh, home, oak, "Loft slab", new Vector3((minX + maxX) * 0.5f, LoftFloor - 0.083f, (minZ + maxZ) * 0.5f),
                new Vector3(maxX - minX, 0.166f, maxZ - minZ));
        }

        private void BuildMusicRoom(Transform home, float stairZ, Material oak, Material walnut, Material plaster,
            Material clay, Material green, Material linen)
        {
            MeshParts room = new MeshParts(this);
            float direction = -Mathf.Sign(stairZ);
            Rug(room, clay, linen, new Vector3(0.05f, LoftFloor + 0.01f, direction * 1.77f));
            BuildBench(room, home, walnut, green, new Vector3(-3.70f, LoftFloor, direction * 2.9f), "selman_62_loft_seat");
            BuildBookcase(room, home, walnut, linen, clay, green, new Vector3(-3.94f, LoftFloor, -direction * 2.53f));
            Solid(room, home, oak, "Sketch table", new Vector3(-1.40f, LoftFloor + 0.40f, direction * 3.96f), new Vector3(1.23f, 0.79f, 0.72f));
            room.Add(linen, _cube, new Vector3(-1.55f, LoftFloor + 0.806f, direction * 3.9f),
                Quaternion.Euler(0f, 14f, 0f), new Vector3(0.44f, 0.018f, 0.29f));
            room.Rod(clay, _cylinder, new Vector3(-1.64f, LoftFloor + 0.83f, direction * 3.82f),
                new Vector3(-1.42f, LoftFloor + 0.83f, direction * 4.02f), 0.012f);
            Plant(room, green, clay, new Vector3(-4.24f, LoftFloor, direction * 4.44f), 1.43f, 2);

            Solid(room, home, walnut, "Record cabinet", new Vector3(-3.95f, LoftFloor + 0.43f, 0.7f), new Vector3(0.64f, 0.86f, 1.76f));
            room.Box(clay, new Vector3(-3.90f, LoftFloor + 0.91f, 0.71f), new Vector3(0.48f, 0.10f, 0.58f));
            room.Add(walnut, _cylinder, new Vector3(-3.89f, LoftFloor + 0.976f, 0.71f), Quaternion.identity, new Vector3(0.37f, 0.009f, 0.37f));
            room.Add(linen, _cylinder, new Vector3(-3.89f, LoftFloor + 0.988f, 0.71f), Quaternion.identity, new Vector3(0.11f, 0.006f, 0.11f));
            for (int album = 0; album < 13; album++)
                room.Box(album % 3 == 0 ? clay : album % 3 == 1 ? green : linen,
                    new Vector3(-3.61f, LoftFloor + 0.39f, 0.16f + album * 0.082f), new Vector3(0.052f, 0.40f, 0.055f));

            Solid(room, home, walnut, "Upright piano", new Vector3(2.5f, LoftFloor + 0.54f, direction * 4.54f), new Vector3(1.74f, 1.08f, 0.61f));
            for (int key = 0; key < 20; key++)
            {
                float x = 1.75f + key * 0.078f;
                room.Box(plaster, new Vector3(x, LoftFloor + 0.71f, direction * 4.14f), new Vector3(0.073f, 0.042f, 0.24f));
                if (key % 7 != 2 && key % 7 != 6)
                    room.Box(walnut, new Vector3(x + 0.036f, LoftFloor + 0.754f, direction * 4.19f), new Vector3(0.043f, 0.035f, 0.15f));
            }
            room.Box(oak, new Vector3(2.5f, LoftFloor + 0.46f, direction * 3.55f), new Vector3(0.9f, 0.09f, 0.38f));
            foreach (float side in new[] { -1f, 1f })
                room.Box(walnut, new Vector3(2.5f + side * 0.36f, LoftFloor + 0.22f, direction * 3.55f), new Vector3(0.085f, 0.44f, 0.31f));

            for (int frame = 0; frame < 3; frame++)
            {
                float z = direction * (2.02f - frame * 1.30f);
                room.Box(walnut, new Vector3(4.82f, LoftFloor + 1.55f, z), new Vector3(0.081f, 0.93f, 1.11f));
                room.Box(linen, new Vector3(4.768f, LoftFloor + 1.55f, z), new Vector3(0.031f, 0.80f, 0.98f));
                room.Add(frame == 1 ? clay : green, _cube, new Vector3(4.738f, LoftFloor + 1.55f, z), Quaternion.Euler(24f * (frame - 1), 0f, 0f),
                    new Vector3(0.027f, 0.51f, 0.35f));
            }
            room.Emit(home, "Piano, records and upstairs gallery");
            AddRoomZone(home, 4.50f, 2.23f, 9.55f, 10.25f, new Vector3(0.45f, 5.13f, direction * 1.4f), new Color(1f, 0.83f, 0.61f));
        }

        private static void Solid(MeshParts mesh, Transform parent, Material material, string name, Vector3 offset, Vector3 size)
        {
            mesh.Box(material, offset, size);
            AddCollider(parent, name, offset, size);
        }

        private void Rug(MeshParts mesh, Material center, Material pattern, Vector3 pos)
        {
            mesh.Box(center, pos, new Vector3(2.75f, 0.016f, 1.86f));
            foreach (float side in new[] { -1f, 1f })
                mesh.Box(pattern, pos + new Vector3(0f, 0.009f, side * 0.79f), new Vector3(2.55f, 0.006f, 0.081f));
            for (int mark = -2; mark <= 2; mark++)
            {
                mesh.Add(pattern, _cube, pos + new Vector3(mark * 0.48f, 0.009f, 0f),
                    Quaternion.Euler(0f, 45f, 0f), new Vector3(0.26f, 0.008f, 0.26f));
                mesh.Add(center, _cube, pos + new Vector3(mark * 0.48f, 0.014f, 0f),
                    Quaternion.Euler(0f, 45f, 0f), new Vector3(0.15f, 0.006f, 0.15f));
            }
        }

        private void Plant(MeshParts mesh, Material leaf, Material pot, Vector3 pos, float scale, int variation)
        {
            mesh.Add(pot, _cylinder, pos + Vector3.up * (0.092f * scale), Quaternion.identity,
                new Vector3(0.25f, 0.092f, 0.25f) * scale);
            mesh.Add(pot, _cylinder, pos + Vector3.up * (0.183f * scale), Quaternion.identity,
                new Vector3(0.30f, 0.024f, 0.30f) * scale);
            for (int stem = 0; stem < 5; stem++)
            {
                float angle = (variation * 0.43f + stem * 1.27f);
                float spread = variation % 2 == 0 ? 0.13f : 0.23f;
                Vector3 branch = new Vector3(Mathf.Cos(angle) * spread, 0.27f + stem * 0.042f, Mathf.Sin(angle) * spread);
                Vector3 tip = pos + branch * scale;
                mesh.Rod(leaf, _cylinder, pos + Vector3.up * (0.18f * scale), tip, 0.008f * scale);
                mesh.Add(leaf, _sphere, tip, Quaternion.FromToRotation(Vector3.up, new Vector3(branch.x * 1.6f, 0.21f, branch.z * 1.6f)),
                    new Vector3(variation % 2 == 0 ? 0.065f : 0.13f, 0.22f + stem * 0.018f, 0.035f) * scale);
            }
        }

        private static void BuildBookcase(MeshParts mesh, Transform home, Material wood, Material linen,
            Material clay, Material green, Vector3 pos)
        {
            Solid(mesh, home, wood, "Bookcase back", pos + new Vector3(-0.17f, 0.91f, 0f), new Vector3(0.13f, 1.82f, 1.18f));
            foreach (float edge in new[] { -1f, 1f })
                mesh.Box(wood, pos + new Vector3(0f, 0.91f, edge * 0.58f), new Vector3(0.47f, 1.82f, 0.067f));
            for (int shelf = 0; shelf < 5; shelf++)
            {
                float y = 0.12f + shelf * 0.40f;
                mesh.Box(wood, pos + new Vector3(0f, y, 0f), new Vector3(0.46f, 0.075f, 1.19f));
                if (shelf == 4) continue;
                for (int book = 0; book < 8; book++)
                {
                    float width = 0.062f + (book + shelf) % 3 * 0.017f;
                    float height = 0.22f + (book * 3 + shelf) % 5 * 0.017f;
                    mesh.Box(book % 3 == 0 ? clay : book % 3 == 1 ? linen : green,
                        pos + new Vector3(0.034f, y + 0.038f + height * 0.5f, -0.48f + book * 0.113f),
                        new Vector3(0.26f, height, width));
                }
            }
            AddCollider(home, "Bookcase collision", pos + Vector3.up * 0.91f, new Vector3(0.47f, 1.82f, 1.2f));
        }

        private static void BuildBench(MeshParts mesh, Transform home, Material wood, Material cushion, Vector3 basePos, string id)
        {
            Solid(mesh, home, wood, "Window bench base", basePos + new Vector3(0f, 0.20f, 0f), new Vector3(0.67f, 0.40f, 1.43f));
            mesh.Box(cushion, basePos + new Vector3(0f, 0.43f, 0f), new Vector3(0.72f, 0.12f, 1.51f));
            mesh.Box(cushion, basePos + new Vector3(-0.29f, 0.67f, 0f), new Vector3(0.12f, 0.44f, 1.44f));
            Transform seat = Child(home, id, basePos + new Vector3(0.09f, 0.49f, 0f));
            seat.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Transform prompt = Child(home, id + "_prompt", basePos + new Vector3(0.51f, 0.16f, 0f));
            InteractableTrigger trigger = prompt.gameObject.AddComponent<InteractableTrigger>();
            trigger.Configure(InteractableKind.Seat, id, "ui.btn.sit", 1.6f);
            prompt.gameObject.AddComponent<SeatInteractable>().Configure(seat, trigger);
        }

        private static void AddRoomZone(Transform home, float midY, float height, float length, float width, Vector3 lampPos, Color color)
        {
            Transform zone = Child(home, "Indoor camera and lighting", new Vector3(0f, midY, 0f));
            zone.gameObject.AddComponent<BoxCollider>().size = new Vector3(length, height, width);
            Light light = Child(home, "Room light", lampPos).gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7.5f;
            light.intensity = 2.8f;
            light.color = color;
            light.shadows = LightShadows.None;
            zone.gameObject.AddComponent<InteriorZone>().Configure(new[] { light }, 2.25f);
        }
    }
}
