using System.Collections.Generic;
using Gridroam.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Gridroam.World
{
    /// <summary>Installs the neighborhood vehicles, court, and house additions in the shipped scene.</summary>
    public sealed partial class NeighborhoodSports : MonoBehaviour
    {
        private static readonly Vector3 CourtPosition = new Vector3(-41f, 0.055f, -42.2f);
        private readonly List<Object> _ownedAssets = new List<Object>();
        private Mesh _cube;
        private Mesh _sphere;
        private Mesh _cylinder;

        public static void Install(Scene scene, ProceduralHumanoid playerModel)
        {
            if (scene.name != "GRIDROAM_GarageHarbor_Test" || playerModel == null) return;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Renderer playerRenderer = playerModel.GetComponentInChildren<Renderer>(true);
                shader = playerRenderer != null && playerRenderer.sharedMaterial != null
                    ? playerRenderer.sharedMaterial.shader : null;
            }
            if (shader == null)
            {
                Debug.LogWarning("Neighborhood sports require a scene material shader.");
                return;
            }

            GameObject root = new GameObject("Hamidiye_NeighborhoodSports");
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                root.AddComponent<NeighborhoodSports>().Build(playerModel, shader);
            }
            catch (System.Exception error)
            {
                Debug.LogException(error, root);
                root.SetActive(false);
                Destroy(root);
            }
        }

        private void Build(ProceduralHumanoid playerModel, Shader shader)
        {
            _cube = PrimitiveMesh(PrimitiveType.Cube);
            _cylinder = PrimitiveMesh(PrimitiveType.Cylinder);
            _sphere = PrimitiveMesh(PrimitiveType.Sphere);

            Material paintedSteel = MaterialFor(shader, "Park Painted Steel", new Color(0.24f, 0.36f, 0.32f), 0.42f);
            Material rubber = MaterialFor(shader, "Bike Rubber", new Color(0.08f, 0.085f, 0.08f), 0.22f);
            Material cream = MaterialFor(shader, "Court Line Paint", new Color(0.88f, 0.87f, 0.76f), 0.22f);
            Material metal = MaterialFor(shader, "Galvanized Steel", new Color(0.59f, 0.61f, 0.59f), 0.53f);
            Material asphalt = MaterialFor(shader, "Weathered Park Asphalt", new Color(0.27f, 0.32f, 0.27f), 0.18f);
            Material clay = MaterialFor(shader, "Terra Cotta Court Surface", new Color(0.56f, 0.31f, 0.23f), 0.19f);
            Material green = MaterialFor(shader, "Tennis Ball Felt", new Color(0.68f, 0.78f, 0.22f), 0.18f);
            green.EnableKeyword("_EMISSION");
            green.SetColor("_EmissionColor", new Color(0.14f, 0.17f, 0.04f));
            BuildBicycle(paintedSteel, rubber, cream, metal);
            BuildMotorcycle(shader, rubber, cream, metal);
            BuildCourt(playerModel, paintedSteel, rubber, cream, asphalt, clay, green);
            BuildDuplexHomes(shader, cream, paintedSteel);
        }

        private void BuildBicycle(Material frame, Material rubber, Material cream, Material metal)
        {
            Transform bike = Child(transform, "Selman_CityBicycle", new Vector3(-65.3f, 0.067f, -5.52f));
            bike.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Transform visual = Child(bike, "BicycleVisual", Vector3.zero);
            Vector3 frontAxle = new Vector3(0f, 0.34f, 0.57f);
            Vector3 rearAxle = new Vector3(0f, 0.34f, -0.55f);
            Vector3 bottomBracket = new Vector3(0f, 0.36f, -0.07f);
            Vector3 headTube = new Vector3(0f, 0.87f, 0.43f);
            Vector3 seatTube = new Vector3(0f, 0.82f, -0.24f);

            MeshParts body = new MeshParts(this);
            body.Rod(frame, _cylinder, bottomBracket, seatTube, 0.024f);
            body.Rod(frame, _cylinder, bottomBracket, headTube, 0.033f);
            body.Rod(frame, _cylinder, seatTube, headTube, 0.018f);
            body.Rod(frame, _cylinder, seatTube, rearAxle, 0.016f);
            body.Rod(frame, _cylinder, bottomBracket, rearAxle, 0.018f);
            body.Rod(metal, _cylinder, seatTube, seatTube + Vector3.up * 0.145f, 0.016f);
            body.Box(rubber, new Vector3(0f, 0.965f, -0.235f), new Vector3(0.17f, 0.065f, 0.27f));
            body.Box(frame, new Vector3(0f, 0.73f, -0.57f), new Vector3(0.16f, 0.035f, 0.42f));
            body.Rod(frame, _cylinder, rearAxle, new Vector3(0.07f, 0.72f, -0.70f), 0.013f);
            body.Rod(frame, _cylinder, rearAxle, new Vector3(-0.07f, 0.72f, -0.70f), 0.013f);
            body.Box(cream, new Vector3(0f, 0.715f, -0.788f), new Vector3(0.08f, 0.028f, 0.02f));
            body.Emit(visual, "Frame");

            Transform fork = Child(visual, "Steering", frontAxle);
            MeshParts handlebars = new MeshParts(this);
            foreach (float side in new[] { -1f, 1f })
            {
                handlebars.Rod(frame, _cylinder, new Vector3(side * 0.055f, 0f, 0f),
                    new Vector3(side * 0.033f, 0.51f, -0.12f), 0.018f);
                handlebars.Rod(rubber, _cylinder, new Vector3(side * 0.155f, 0.69f, -0.18f),
                    new Vector3(side * 0.27f, 0.69f, -0.18f), 0.022f);
            }

            handlebars.Rod(metal, _cylinder, new Vector3(0f, 0.51f, -0.12f), new Vector3(0f, 0.69f, -0.14f), 0.019f);
            handlebars.Rod(metal, _cylinder, new Vector3(-0.19f, 0.69f, -0.18f), new Vector3(0.19f, 0.69f, -0.18f), 0.015f);
            handlebars.Box(cream, new Vector3(0f, 0.55f, -0.055f), new Vector3(0.063f, 0.051f, 0.048f));
            handlebars.Emit(fork, "Fork and bars");

            Transform frontWheel = Child(fork, "Front wheel", Vector3.zero);
            Transform rearWheel = Child(visual, "Rear wheel", rearAxle);
            MeshParts wheel = new MeshParts(this);
            wheel.Ring(rubber, 0.315f, 0.315f, 0.025f, 28, Quaternion.Euler(0f, 90f, 0f));
            wheel.Ring(metal, 0.286f, 0.286f, 0.012f, 28, Quaternion.Euler(0f, 90f, 0f));
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector3 spoke = new Vector3(0f, Mathf.Sin(angle), Mathf.Cos(angle)) * 0.286f;
                wheel.Rod(metal, _cylinder, -spoke, spoke, 0.004f);
            }

            var wheelMeshes = wheel.CreateMeshes("Bicycle wheel");
            MeshParts.AddRenderers(frontWheel, wheelMeshes, "Wheel");
            MeshParts.AddRenderers(rearWheel, wheelMeshes, "Wheel");

            Transform crank = Child(visual, "Crank", bottomBracket);
            MeshParts pedals = new MeshParts(this);
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 pedal = new Vector3(side * 0.16f, 0f, side * 0.15f);
                pedals.Rod(metal, _cylinder, new Vector3(side * 0.08f, 0f, 0f), pedal, 0.014f);
                pedals.Box(rubber, pedal, new Vector3(0.12f, 0.027f, 0.074f));
            }
            pedals.Emit(crank, "Pedals");

            BicycleController controller = bike.gameObject.AddComponent<BicycleController>();
            controller.Configure(visual, fork, frontWheel, rearWheel, crank);
            bike.gameObject.AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.Bicycle, "selman_city_bicycle", "ui.btn.ride_bike", 2.6f);
        }

        private void BuildCourt(ProceduralHumanoid playerModel, Material steel, Material rubber, Material cream,
            Material asphalt, Material clay, Material ballMaterial)
        {
            Transform court = Child(transform, "Gursu_MahalleTenisKortu", CourtPosition);
            MeshParts surface = new MeshParts(this);
            surface.Box(asphalt, new Vector3(0f, -0.06f, 0f), new Vector3(15.8f, 0.12f, 30.4f));
            surface.Box(clay, new Vector3(0f, -0.017f, 0f), new Vector3(11.5f, 0.037f, 26.5f));
            surface.Box(asphalt, new Vector3(-5.25f, -0.016f, 17.1f), new Vector3(2.3f, 0.032f, 5.1f));

            const float serviceLength = 6.4f;
            foreach (float side in new[] { -1f, 1f })
            {
                surface.Box(cream, new Vector3(side * TennisCourt.HalfWidth, TennisCourt.LineHeight, 0f),
                    new Vector3(0.055f, 0.004f, TennisCourt.HalfLength * 2f));
                surface.Box(cream, new Vector3(0f, TennisCourt.LineHeight, side * TennisCourt.HalfLength),
                    new Vector3(TennisCourt.HalfWidth * 2f, 0.004f, 0.075f));
                surface.Box(cream, new Vector3(0f, TennisCourt.LineHeight, side * serviceLength),
                    new Vector3(TennisCourt.HalfWidth * 2f, 0.004f, 0.055f));
            }
            surface.Box(cream, new Vector3(0f, TennisCourt.LineHeight, 0f), new Vector3(0.055f, 0.004f, serviceLength * 2f));
            surface.Emit(court, "Paved court and lines");

            MeshParts fences = new MeshParts(this);
            AddFence(fences, court, steel, new Vector3(-7.6f, 0f, -15f), new Vector3(-7.6f, 0f, 15f));
            AddFence(fences, court, steel, new Vector3(7.6f, 0f, -15f), new Vector3(7.6f, 0f, 15f));
            AddFence(fences, court, steel, new Vector3(-7.6f, 0f, -15f), new Vector3(7.6f, 0f, -15f));
            AddFence(fences, court, steel, new Vector3(-7.6f, 0f, 15f), new Vector3(-6.65f, 0f, 15f));
            AddFence(fences, court, steel, new Vector3(-3.85f, 0f, 15f), new Vector3(7.6f, 0f, 15f));
            fences.Emit(court, "Low municipal fence", shadows: false);

            MeshParts net = new MeshParts(this);
            const float halfNet = 4.8f;
            net.Box(cream, new Vector3(0f, 0.91f, 0f), new Vector3(halfNet * 2f, 0.048f, 0.024f));
            foreach (float x in new[] { -halfNet, halfNet })
            {
                net.Box(steel, new Vector3(x, 0.48f, 0f), new Vector3(0.09f, 0.96f, 0.09f));
            }

            for (int i = 1; i <= 47; i++)
            {
                float x = Mathf.Lerp(-halfNet, halfNet, i / 48f);
                net.Box(rubber, new Vector3(x, 0.46f, 0f), new Vector3(0.013f, 0.89f, 0.013f));
            }

            for (int i = 1; i <= 8; i++)
            {
                net.Box(rubber, new Vector3(0f, i * 0.10f, 0f), new Vector3(halfNet * 2f, 0.012f, 0.013f));
            }
            net.Emit(court, "Tennis net", shadows: false);
            AddCollider(court, "Net collision", new Vector3(0f, 0.46f, 0f), new Vector3(halfNet * 2f, 0.92f, 0.10f));

            AddSign(court, new Vector3(-6.75f, 1.6f, 15.1f), "GÜRSU SOKAĞI\nTENİS KORTU", steel, cream.color);
            AddSign(transform, new Vector3(-44.8f, 1.46f, -6.8f), "TENİS KORTU  ↑", steel, cream.color);

            Transform station = Child(court, "Court entrance", new Vector3(-5.25f, 0f, 14.6f));
            station.gameObject.AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.Tennis, "gursu_tennis", "ui.btn.play_tennis", 3.0f);

            MeshParts ballParts = new MeshParts(this);
            ballParts.Add(ballMaterial, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * (TennisCourt.BallRadius * 2f));
            Transform ball = Child(court, "Tennis ball", Vector3.zero);
            ballParts.Emit(ball, "Felt sphere", shadows: false);

            Material shadowMaterial = MaterialFor(clay.shader, "Tennis ball shadow", Color.Lerp(clay.color, Color.black, 0.38f), 0f);
            MeshParts shadowParts = new MeshParts(this);
            shadowParts.Add(shadowMaterial, _sphere, Vector3.zero, Quaternion.identity, new Vector3(0.15f, 0.001f, 0.15f));
            Transform ballShadow = Child(court, "Tennis ball ground shadow", Vector3.zero);
            shadowParts.Emit(ballShadow, "Ground shadow", shadows: false);

            MeshParts markerParts = new MeshParts(this);
            markerParts.Ring(ballMaterial, 0.43f, 0.43f, 0.021f, 24, Quaternion.Euler(90f, 0f, 0f));
            Transform marker = Child(court, "Return timing marker", Vector3.zero);
            markerParts.Emit(marker, "Ready ring", shadows: false);

            // Use the existing jointed player art with a different shirt for the neighborhood opponent.
            ProceduralHumanoid opponent = Instantiate(playerModel, court);
            opponent.name = "Mahalle Tenis Rakibi";
            Transform opponentTorso = opponent.transform.Find("Hips/Spine");
            if (opponentTorso != null)
            {
                Material shirt = MaterialFor(steel.shader, "Opponent Blue Sports Shirt", new Color(0.22f, 0.42f, 0.55f), 0.22f);
                foreach (Renderer renderer in opponentTorso.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] materials = renderer.sharedMaterials;
                    bool changed = false;
                    for (int slot = 0; slot < materials.Length; slot++)
                    {
                        if (materials[slot] == null || !materials[slot].name.Contains("Shirt")) continue;
                        materials[slot] = shirt;
                        changed = true;
                    }
                    if (changed) renderer.sharedMaterials = materials;
                }
            }

            MeshParts racketParts = new MeshParts(this);
            racketParts.Ring(steel, 0.125f, 0.158f, 0.012f, 22, Quaternion.identity, new Vector3(0f, 0.48f, 0f));
            racketParts.Box(rubber, new Vector3(0f, 0.09f, 0f), new Vector3(0.032f, 0.18f, 0.029f));
            racketParts.Rod(steel, _cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(0.07f, 0.35f, 0f), 0.011f);
            racketParts.Rod(steel, _cylinder, new Vector3(0f, 0.16f, 0f), new Vector3(-0.07f, 0.35f, 0f), 0.011f);
            for (int i = -3; i <= 3; i++)
            {
                float offset = i * 0.245f;
                float chord = Mathf.Sqrt(1f - offset * offset) * 2f;
                racketParts.Box(cream, new Vector3(0.125f * offset, 0.48f, 0f), new Vector3(0.004f, 0.158f * chord, 0.004f));
                racketParts.Box(cream, new Vector3(0f, 0.48f + 0.158f * offset, 0f), new Vector3(0.125f * chord, 0.004f, 0.004f));
            }

            var rackets = racketParts.CreateMeshes("Tennis racket");
            Transform playerRacket = Child(court, "Player racket", Vector3.zero);
            MeshParts.AddRenderers(playerRacket, rackets, "Racket", shadows: false);
            Transform opponentRacket = Child(opponent.transform, "Opponent racket", Vector3.zero);
            MeshParts.AddRenderers(opponentRacket, rackets, "Racket", shadows: false);

            AudioSource ballSound = ball.gameObject.AddComponent<AudioSource>();
            ballSound.playOnAwake = false;
            ballSound.spatialBlend = 0.58f;
            ballSound.minDistance = 3f;
            ballSound.maxDistance = 34f;
            AudioClip hit = BuildBallSound("Racket hit", 340f, 61f, 0.30f);
            AudioClip bounce = BuildBallSound("Court bounce", 155f, 86f, 0.12f);
            court.gameObject.AddComponent<TennisCourt>().Configure(
                ball, ballShadow, opponent, opponentRacket, playerRacket, marker, ballSound, hit, bounce);
        }

        private void AddFence(MeshParts parts, Transform court, Material material, Vector3 start, Vector3 end)
        {
            const float height = 2.05f;
            float length = Vector3.Distance(start, end);
            Vector3 mid = (start + end) * 0.5f;
            int panels = Mathf.Max(1, Mathf.CeilToInt(length / 2.8f));
            for (int i = 0; i <= panels; i++)
            {
                Vector3 post = Vector3.Lerp(start, end, i / (float)panels);
                parts.Box(material, post + Vector3.up * (height * 0.5f), new Vector3(0.075f, height, 0.075f));
            }

            foreach (float rail in new[] { 0.12f, 0.72f, 1.34f, 1.96f })
            {
                parts.Rod(material, _cylinder, start + Vector3.up * rail, end + Vector3.up * rail, 0.018f);
            }

            Vector3 size = end - start;
            AddCollider(court, "Fence collision", mid + Vector3.up * (height * 0.5f),
                new Vector3(Mathf.Max(0.09f, Mathf.Abs(size.x)), height, Mathf.Max(0.09f, Mathf.Abs(size.z))));
        }

        private AudioClip BuildBallSound(string name, float frequency, float decay, float grit)
        {
            const int sampleRate = 22050;
            float[] samples = new float[sampleRate / 10];
            for (int i = 0; i < samples.Length; i++)
            {
                float time = i / (float)sampleRate;
                float thump = Mathf.Sin(2f * Mathf.PI * time * (frequency - 160f * time));
                float pop = Mathf.Sin(time * 9896f) * Mathf.Sin(time * 14073f) * grit * Mathf.Exp(-160f * time);
                samples[i] = (thump * 0.52f + pop) * Mathf.Min(1f, i * 0.06f) * Mathf.Exp(-decay * time);
            }

            AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            _ownedAssets.Add(clip);
            return clip;
        }

        private void AddSign(Transform parent, Vector3 position, string label, Material backing, Color color)
        {
            Transform sign = Child(parent, "Court signage", position);
            sign.localRotation = Quaternion.Euler(0f, 180f, 0f);
            MeshParts parts = new MeshParts(this);
            parts.Box(backing, Vector3.zero, new Vector3(1.8f, 0.49f, 0.07f));
            parts.Box(backing, new Vector3(0f, -position.y * 0.5f, 0f), new Vector3(0.085f, position.y, 0.085f));
            parts.Emit(sign, "Sign and post", shadows: false);
            Transform letters = Child(sign, "Lettering", new Vector3(0f, 0f, -0.038f));
            TextMesh text = letters.gameObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            letters.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = label;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = 0.060f;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
        }

        private static void AddCollider(Transform parent, string name, Vector3 center, Vector3 size)
        {
            Child(parent, name, center).gameObject.AddComponent<BoxCollider>().size = size;
        }

        private static Transform Child(Transform parent, string name, Vector3 position)
        {
            Transform child = new GameObject(name).transform;
            child.SetParent(parent, false);
            child.localPosition = position;
            return child;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.SetActive(false);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            Destroy(primitive);
            return mesh;
        }

        private Material MaterialFor(Shader shader, string name, Color color, float smoothness)
        {
            Material material = new Material(shader) { name = name, color = color };
            material.SetFloat("_Smoothness", smoothness);
            _ownedAssets.Add(material);
            return material;
        }

        private void OnDestroy()
        {
            foreach (Object asset in _ownedAssets) Destroy(asset);
        }

        // Each material shares one combined mesh per static structure or animated bicycle part.
        private sealed class MeshParts
        {
            private readonly NeighborhoodSports _owner;
            private readonly Dictionary<Material, List<CombineInstance>> _parts = new Dictionary<Material, List<CombineInstance>>();

            public MeshParts(NeighborhoodSports owner) { _owner = owner; }

            public void Add(Material material, Mesh mesh, Vector3 position, Quaternion rotation, Vector3 scale)
            {
                if (!_parts.TryGetValue(material, out List<CombineInstance> parts))
                {
                    parts = new List<CombineInstance>();
                    _parts.Add(material, parts);
                }
                parts.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(position, rotation, scale) });
            }

            public void Box(Material material, Vector3 position, Vector3 size)
            {
                Add(material, _owner._cube, position, Quaternion.identity, size);
            }

            public void Rod(Material material, Mesh source, Vector3 start, Vector3 end, float radius)
            {
                Vector3 delta = end - start;
                Add(material, source, (start + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, delta),
                    new Vector3(radius * 2f, delta.magnitude * 0.5f, radius * 2f));
            }

            public void Ring(Material material, float radiusX, float radiusY, float tube, int segments,
                Quaternion rotation, Vector3 center = default)
            {
                var vertices = new Vector3[segments * 6];
                var triangles = new int[segments * 6 * 6];
                for (int i = 0; i < segments; i++)
                {
                    float angle = i * Mathf.PI * 2f / segments;
                    float x = Mathf.Cos(angle);
                    float y = Mathf.Sin(angle);
                    for (int j = 0; j < 6; j++)
                    {
                        float cross = j * Mathf.PI / 3f;
                        float bulge = Mathf.Cos(cross) * tube;
                        int current = i * 6 + j;
                        int along = ((i + 1) % segments) * 6 + j;
                        int around = i * 6 + (j + 1) % 6;
                        int diagonal = ((i + 1) % segments) * 6 + (j + 1) % 6;
                        vertices[current] = center + rotation * new Vector3((radiusX + bulge) * x,
                            (radiusY + bulge) * y, Mathf.Sin(cross) * tube);
                        int face = current * 6;
                        triangles[face] = current;
                        triangles[face + 1] = along;
                        triangles[face + 2] = diagonal;
                        triangles[face + 3] = current;
                        triangles[face + 4] = diagonal;
                        triangles[face + 5] = around;
                    }
                }

                Mesh mesh = new Mesh { name = "Ring", vertices = vertices, triangles = triangles };
                mesh.RecalculateNormals();
                _owner._ownedAssets.Add(mesh);
                Add(material, mesh, Vector3.zero, Quaternion.identity, Vector3.one);
            }

            public List<(Mesh mesh, Material material)> CreateMeshes(string name)
            {
                var meshes = new List<(Mesh, Material)>(_parts.Count);
                foreach (var group in _parts)
                {
                    Mesh mesh = new Mesh { name = name + " · " + group.Key.name };
                    mesh.CombineMeshes(group.Value.ToArray(), true, true);
                    _owner._ownedAssets.Add(mesh);
                    meshes.Add((mesh, group.Key));
                }

                return meshes;
            }

            public void Emit(Transform parent, string name, bool shadows = true)
            {
                AddRenderers(parent, CreateMeshes(name), name, shadows);
            }

            public static void AddRenderers(Transform parent, List<(Mesh mesh, Material material)> meshes,
                string name, bool shadows = true)
            {
                foreach (var entry in meshes)
                {
                    Transform child = Child(parent, name, Vector3.zero);
                    child.gameObject.AddComponent<MeshFilter>().sharedMesh = entry.mesh;
                    MeshRenderer renderer = child.gameObject.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = entry.material;
                    renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                }
            }
        }
    }
}
