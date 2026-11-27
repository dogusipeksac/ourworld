using System.Collections.Generic;
using OurWorld.Player;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OurWorld.World
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
            _atmosphere = gameObject.AddComponent<SportsArenaAtmosphere>();

            Material paintedSteel = MaterialFor(shader, "Park Painted Steel", new Color(0.20f, 0.34f, 0.29f), 0.44f);
            Material rubber = MaterialFor(shader, "Bike Rubber", new Color(0.08f, 0.085f, 0.08f), 0.22f);
            Material cream = MaterialFor(shader, "Court Line Paint", new Color(0.92f, 0.91f, 0.84f), 0.20f);
            Material metal = MaterialFor(shader, "Galvanized Steel", new Color(0.60f, 0.62f, 0.61f), 0.56f);
            Texture2D asphaltTex = BuildAsphaltTexture(new Color(0.27f, 0.30f, 0.28f), 1101);
            Material asphalt = MaterialForTextured(shader, "Weathered Park Asphalt", asphaltTex, Color.white, 0.16f, new Vector2(6f, 10f));
            Texture2D clayTex = BuildClayCourtTexture(new Color(0.62f, 0.31f, 0.21f), 2202);
            Material clay = MaterialForTextured(shader, "Terra Cotta Court Surface", clayTex, Color.white, 0.14f, new Vector2(5f, 10f));
            Material green = MaterialFor(shader, "Tennis Ball Felt", new Color(0.72f, 0.84f, 0.20f), 0.18f);
            green.EnableKeyword("_EMISSION");
            green.SetColor("_EmissionColor", new Color(0.16f, 0.20f, 0.04f));
            BuildBicycles(shader, paintedSteel, rubber, cream, metal);
            BuildMotorcycles(shader, rubber, cream, metal);
            BuildCourt(playerModel, shader, paintedSteel, rubber, cream, metal, asphalt, clay, green);
            BuildCommunitySportsArenas(playerModel, shader, paintedSteel, rubber, cream, metal, asphalt, clay);
            BuildDuplexHomes(shader, cream, paintedSteel);
        }

        private void BuildBicycles(Shader shader, Material defaultFrame, Material rubber, Material cream, Material metal)
        {
            // 7 rideable City Bicycles distributed across the entire expanded map so players discover bikes everywhere
            // instead of clustering everything outside No:48!
            (string objName, string triggerId, Vector3 pos, float yaw, Color color, bool useDefault)[] bikes =
            {
                ("Gursu_CityBicycle",   "selman_city_bicycle",  new Vector3(-28.5f, 0.067f, -23.4f),  90f, new Color(0.24f, 0.36f, 0.32f), true),
                ("Park_CityBicycle",    "park_city_bicycle",    new Vector3(-20.0f, 0.067f,  63.4f),  90f, new Color(0.12f, 0.58f, 0.64f), false),
                ("Fatih_CityBicycle",   "fatih_city_bicycle",   new Vector3( 18.0f, 0.067f, 132.4f), 270f, new Color(0.84f, 0.62f, 0.18f), false),
                ("Havadis_CityBicycle", "havadis_city_bicycle", new Vector3(-32.0f, 0.067f, 180.6f),  90f, new Color(0.74f, 0.22f, 0.20f), false),
                ("Kadife_CityBicycle",  "kadife_city_bicycle",  new Vector3( 24.0f, 0.067f,-180.6f), 270f, new Color(0.20f, 0.36f, 0.66f), false),
                ("Lake_CityBicycle",    "lake_city_bicycle",    new Vector3(-164.0f,0.067f,  -7.2f),   0f, new Color(0.26f, 0.64f, 0.50f), false),
                ("Farm_CityBicycle",    "farm_city_bicycle",    new Vector3( 146.5f,0.067f,   4.8f), 270f, new Color(0.78f, 0.38f, 0.18f), false)
            };

            for (int i = 0; i < bikes.Length; i++)
            {
                var b = bikes[i];
                Material frameMat = b.useDefault
                    ? defaultFrame
                    : MaterialFor(shader, b.objName + " Frame", b.color, 0.45f);
                BuildBicycleAt(b.objName, b.triggerId, b.pos, b.yaw, frameMat, rubber, cream, metal);
            }
        }

        private void BuildBicycleAt(string objName, string triggerId, Vector3 worldPos, float yaw, Material frame, Material rubber, Material cream, Material metal)
        {
            Transform bike = Child(transform, objName, worldPos);
            bike.localRotation = Quaternion.Euler(0f, yaw, 0f);
            if (Physics.Raycast(bike.position + Vector3.up, Vector3.down, out RaycastHit surface, 1.8f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                bike.position = surface.point + Vector3.up * 0.015f;
            Transform visual = Child(bike, "BicycleVisual", Vector3.zero);
            GameObject bikeFbx = OurWorldModelLibrary.InstantiateModel("OW_Bicycle", visual, Vector3.zero, Quaternion.identity);
            if (bikeFbx != null)
            {
                Color paintCol = frame != null && frame.HasProperty("_BaseColor") ? frame.GetColor("_BaseColor") : new Color(0.14f, 0.44f, 0.56f);
                OurWorldModelLibrary.TintMaterialByKeyword(bikeFbx, "BikePaint", paintCol);

                Transform fbxFork = OurWorldModelLibrary.FindPart(bikeFbx.transform, "Fork");
                Transform fbxFrontW = OurWorldModelLibrary.FindPart(bikeFbx.transform, "Wheel_Front");
                Transform fbxRearW = OurWorldModelLibrary.FindPart(bikeFbx.transform, "Wheel_Rear");
                Transform fbxCrank = OurWorldModelLibrary.FindPart(bikeFbx.transform, "Crank");
                // Parent front wheel under steering fork so turning handlebars steers the wheel smoothly!
                if (fbxFork != null && fbxFrontW != null)
                {
                    fbxFrontW.SetParent(fbxFork, true);
                }

                BicycleController fbxCtrl = bike.gameObject.AddComponent<BicycleController>();
                fbxCtrl.Configure(visual, fbxFork, fbxFrontW, fbxRearW, fbxCrank);
                bike.gameObject.AddComponent<InteractableTrigger>()
                    .Configure(InteractableKind.Bicycle, triggerId, "ui.btn.ride_bike", 2.6f);
                return;
            }

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
                .Configure(InteractableKind.Bicycle, triggerId, "ui.btn.ride_bike", 2.6f);
        }

        private void BuildCourt(ProceduralHumanoid playerModel, Shader shader, Material steel, Material rubber, Material cream,
            Material metal, Material asphalt, Material clay, Material ballMaterial)
        {
            Transform court = Child(transform, "Gursu_MahalleTenisKortu", CourtPosition);

            // Roland-Garros inspired dark emerald outer run-off apron + swept red clay court + subtle baseline scuffing
            Texture2D outerTex = BuildAcrylicCourtTexture(new Color(0.14f, 0.34f, 0.23f), 2205);
            Material outerGreen = MaterialForTextured(shader, "Tennis Outer Green Apron", outerTex, Color.white, 0.16f, new Vector2(5f, 9f));
            Material wornClay = MaterialFor(shader, "Tennis Baseline Worn Clay", new Color(0.50f, 0.24f, 0.16f), 0.10f);
            Material whiteLine = MaterialFor(shader, "Tennis Pure White Line", new Color(0.96f, 0.96f, 0.94f), 0.18f);
            Material tarpGreen = MaterialFor(shader, "Tennis Windscreen Tarp", new Color(0.08f, 0.26f, 0.17f), 0.14f);
            Material wireDark = MaterialFor(shader, "Tennis Cage Wire", new Color(0.14f, 0.22f, 0.18f), 0.38f);
            Material woodBench = MaterialFor(shader, "Tennis Bench Timber", new Color(0.54f, 0.35f, 0.19f), 0.26f);
            Material brassMat = MaterialFor(shader, "Tennis Crank Brass", new Color(0.84f, 0.68f, 0.26f), 0.72f);

            const float doublesHalfW = 5.48f;
            const float serviceLength = 6.4f;
            const float lineY = TennisCourt.LineHeight + 0.004f;

            MeshParts surface = new MeshParts(this);
            // Perimeter concrete curb + emerald run-off court + swept terracotta clay playing area
            surface.Box(asphalt, new Vector3(0f, -0.062f, 0f), new Vector3(16.2f, 0.12f, 30.8f));
            surface.Box(outerGreen, new Vector3(0f, -0.022f, 0f), new Vector3(15.2f, 0.036f, 29.8f));
            surface.Box(clay, new Vector3(0f, -0.008f, 0f), new Vector3(11.9f, 0.024f, 26.2f));
            // Subtle darker worn-clay player footwork zones behind both baselines
            foreach (float side in new[] { -1f, 1f })
            {
                surface.Box(wornClay, new Vector3(0f, 0.0042f, side * (TennisCourt.HalfLength - 0.15f)), new Vector3(5.2f, 0.002f, 1.85f));
            }
            surface.Box(asphalt, new Vector3(-5.25f, -0.016f, 17.1f), new Vector3(2.3f, 0.032f, 5.1f));

            // Full ITF markings: Doubles sidelines, Singles sidelines, Baselines, Service boxes & Center service hashes
            foreach (float side in new[] { -1f, 1f })
            {
                // Doubles outer sidelines
                surface.Box(whiteLine, new Vector3(side * doublesHalfW, lineY, 0f),
                    new Vector3(0.060f, 0.004f, TennisCourt.HalfLength * 2f));
                // Singles inner sidelines
                surface.Box(whiteLine, new Vector3(side * TennisCourt.HalfWidth, lineY, 0f),
                    new Vector3(0.055f, 0.004f, TennisCourt.HalfLength * 2f));
                // Baselines (full doubles width!)
                surface.Box(whiteLine, new Vector3(0f, lineY, side * TennisCourt.HalfLength),
                    new Vector3(doublesHalfW * 2f + 0.06f, 0.004f, 0.080f));
                // Center baseline service hash marks
                surface.Box(whiteLine, new Vector3(0f, lineY, side * (TennisCourt.HalfLength - 0.16f)),
                    new Vector3(0.055f, 0.004f, 0.28f));
                // Service lines (between singles lines)
                surface.Box(whiteLine, new Vector3(0f, lineY, side * serviceLength),
                    new Vector3(TennisCourt.HalfWidth * 2f, 0.004f, 0.055f));
            }
            surface.Box(whiteLine, new Vector3(0f, lineY, 0f), new Vector3(0.055f, 0.004f, serviceLength * 2f));
            surface.Emit(court, "Paved court and lines");

            // Tall 2.95m municipal tennis perimeter wire cage + forest-green windscreen tarps (Rüzgarlık Branda!)
            MeshParts fences = new MeshParts(this);
            AddTallCageFence(fences, court, steel, wireDark, new Vector3(-7.6f, 0f, -15f), new Vector3(-7.6f, 0f, 15f), 2.95f);
            AddTallCageFence(fences, court, steel, wireDark, new Vector3(7.6f, 0f, -15f), new Vector3(7.6f, 0f, 15f), 2.95f);
            AddTallCageFence(fences, court, steel, wireDark, new Vector3(-7.6f, 0f, -15f), new Vector3(7.6f, 0f, -15f), 2.95f);
            AddTallCageFence(fences, court, steel, wireDark, new Vector3(-7.6f, 0f, 15f), new Vector3(-6.65f, 0f, 15f), 2.95f);
            AddTallCageFence(fences, court, steel, wireDark, new Vector3(-3.85f, 0f, 15f), new Vector3(7.6f, 0f, 15f), 2.95f);

            // Signature tennis club green windscreens strapped along South backstop & West flank
            AddWindscreenTarp(fences, tarpGreen, new Vector3(-7.5f, 0f, -14.95f), new Vector3(7.5f, 0f, -14.95f), 0.22f, 2.25f, new Vector3(0f, 0f, 0.04f));
            AddWindscreenTarp(fences, tarpGreen, new Vector3(-7.55f, 0f, -14.8f), new Vector3(-7.55f, 0f, -2.5f), 0.22f, 2.05f, new Vector3(0.04f, 0f, 0f));
            AddWindscreenTarp(fences, tarpGreen, new Vector3(-7.55f, 0f, 2.5f), new Vector3(-7.55f, 0f, 14.8f), 0.22f, 2.05f, new Vector3(0.04f, 0f, 0f));
            fences.Emit(court, "Tennis club cage and windscreens", shadows: false);

            // Club banner mounted on South windscreen facing the player
            AddPerimeterAdBoard(court, new Vector3(0f, 1.05f, -14.85f), 180f, 5.2f, 0.72f, steel, whiteLine, "GÜRSU TENİS KULÜBÜ", new Color(0.96f, 0.94f, 0.76f));

            // Pro full-width tennis net with white canvas headband, center anchor strap, round posts & brass crank
            MeshParts net = new MeshParts(this);
            const float halfNet = 5.85f;
            net.Box(whiteLine, new Vector3(0f, 0.93f, 0f), new Vector3(halfNet * 2f, 0.056f, 0.028f));
            // White center strap anchoring net middle to ground
            net.Box(whiteLine, new Vector3(0f, 0.465f, 0f), new Vector3(0.055f, 0.92f, 0.032f));
            foreach (float x in new[] { -halfNet, halfNet })
            {
                net.Rod(steel, _cylinder, new Vector3(x, 0f, 0f), new Vector3(x, 1.07f, 0f), 0.052f);
                net.Add(brassMat, _sphere, new Vector3(x, 1.09f, 0f), Quaternion.identity, Vector3.one * 0.11f);
            }
            // Brass tension crank handle on East post
            net.Rod(brassMat, _cylinder, new Vector3(halfNet + 0.05f, 0.84f, 0f), new Vector3(halfNet + 0.16f, 0.84f, 0f), 0.015f);
            net.Rod(brassMat, _cylinder, new Vector3(halfNet + 0.16f, 0.84f, 0f), new Vector3(halfNet + 0.16f, 0.72f, 0.06f), 0.014f);
            // Singles sticks at ±4.55m
            foreach (float sx in new[] { -4.55f, 4.55f })
            {
                net.Rod(whiteLine, _cylinder, new Vector3(sx, 0f, 0f), new Vector3(sx, 1.05f, 0f), 0.018f);
            }
            for (int i = 1; i <= 55; i++)
            {
                float x = Mathf.Lerp(-halfNet, halfNet, i / 56f);
                net.Box(rubber, new Vector3(x, 0.46f, 0f), new Vector3(0.012f, 0.89f, 0.012f));
            }
            for (int i = 1; i <= 8; i++)
            {
                net.Box(rubber, new Vector3(0f, i * 0.105f, 0f), new Vector3(halfNet * 2f, 0.011f, 0.012f));
            }
            net.Emit(court, "Tennis net", shadows: false);
            AddCollider(court, "Net collision", new Vector3(0f, 0.46f, 0f), new Vector3(halfNet * 2f, 0.94f, 0.10f));

            // Elevated Umpire Chair (Hakem Sandalyesi) + 2 Player Changeover Benches + Ball Hopper on East sideline
            AddElevatedRefChair(court, new Vector3(6.62f, 0f, 0f), -90f, 1.55f, withCanopy: true, shader, steel, woodBench);
            MeshParts sideProps = new MeshParts(this);
            foreach (float bz in new[] { -3.1f, 3.1f })
            {
                sideProps.Box(woodBench, new Vector3(6.75f, 0.44f, bz), new Vector3(0.46f, 0.055f, 1.75f));
                sideProps.Box(woodBench, new Vector3(6.96f, 0.72f, bz), new Vector3(0.055f, 0.34f, 1.75f));
                foreach (float dz in new[] { -0.72f, 0.72f })
                    sideProps.Box(steel, new Vector3(6.75f, 0.22f, bz + dz), new Vector3(0.40f, 0.44f, 0.06f));
            }
            // Tennis ball hopper wire basket filled with yellow balls next to umpire chair
            sideProps.Box(metal, new Vector3(6.62f, 0.48f, -1.35f), new Vector3(0.38f, 0.34f, 0.38f));
            foreach (float lx in new[] { -0.16f, 0.16f })
                foreach (float lz in new[] { -0.16f, 0.16f })
                    sideProps.Rod(metal, _cylinder, new Vector3(6.62f + lx, 0f, -1.35f + lz), new Vector3(6.62f + lx, 0.65f, -1.35f + lz), 0.012f);
            for (int b = 0; b < 6; b++)
            {
                float bx = 6.62f + ((b % 3) - 1) * 0.09f;
                float bz2 = -1.35f + ((b / 3) - 0.5f) * 0.10f;
                sideProps.Add(ballMaterial, _sphere, new Vector3(bx, 0.66f, bz2), Quaternion.identity, Vector3.one * 0.075f);
            }
            sideProps.Emit(court, "Tennis benches and hopper", shadows: false);

            // Two East Corner Floodlight Towers pairing with West Floodlight_TennisCourt mast
            Material floodHousing = MaterialFor(shader, "Tennis Flood Housing", new Color(0.14f, 0.16f, 0.18f), 0.40f);
            Material floodLens = MaterialFor(shader, "Tennis Flood Lens", new Color(0.98f, 0.97f, 0.90f), 0.60f);
            Material nightConeMat = MaterialForTranslucent(Shader.Find("Sprites/Default") ?? shader, "Tennis Night Flood Cone", new Color(0.95f, 0.96f, 1.0f, 0.065f), 0f);
            AddStadiumFloodlightTower(court, new Vector3(8.25f, 0f, -12.6f), Vector3.zero, 6.4f, metal, floodHousing, floodLens, nightConeMat);
            AddStadiumFloodlightTower(court, new Vector3(8.25f, 0f,  12.6f), Vector3.zero, 6.4f, metal, floodHousing, floodLens, nightConeMat);

            // Live 3D Digital Tennis Scoreboard on North fence + Entrance signage
            TextMesh tennisScoreboard = AddDigitalScoreboard(court, new Vector3(1.8f, 2.05f, 15.20f), "GÜRSU TENİS\n0 - 0", shader, new Color(0.18f, 0.78f, 0.42f), new Color(1.0f, 0.95f, 0.70f), width: 2.35f, height: 0.74f);
            AddSign(court, new Vector3(-6.75f, 1.6f, 15.1f), "GÜRSU SOKAĞI\nTENİS KORTU", steel, cream.color);
            AddSign(transform, new Vector3(-44.8f, 1.46f, -6.8f), "TENİS KORTU  ↑", steel, cream.color);

            Transform station = Child(court, "Court entrance", new Vector3(-5.25f, 0f, 14.6f));
            station.gameObject.AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.Tennis, "gursu_tennis", "ui.btn.play_tennis", 4.6f);
            Transform courtPlayZone = Child(court, "Court center trigger", Vector3.zero);
            courtPlayZone.gameObject.AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.Tennis, "gursu_tennis_center", "ui.btn.play_tennis", 7.8f);

            MeshParts ballParts = new MeshParts(this);
            ballParts.Add(ballMaterial, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * (TennisCourt.BallRadius * 2f));
            Transform ball = Child(court, "Tennis ball", Vector3.zero);
            ballParts.Emit(ball, "Felt sphere", shadows: false);

            Material shadowMaterial = MaterialFor(clay.shader, "Tennis ball shadow", Color.Lerp(new Color(0.62f, 0.31f, 0.21f), Color.black, 0.42f), 0f);
            MeshParts shadowParts = new MeshParts(this);
            shadowParts.Add(shadowMaterial, _sphere, Vector3.zero, Quaternion.identity, new Vector3(0.15f, 0.001f, 0.15f));
            Transform ballShadow = Child(court, "Tennis ball ground shadow", Vector3.zero);
            shadowParts.Emit(ballShadow, "Ground shadow", shadows: false);

            MeshParts markerParts = new MeshParts(this);
            markerParts.Ring(ballMaterial, 0.43f, 0.43f, 0.021f, 24, Quaternion.Euler(90f, 0f, 0f));
            Transform marker = Child(court, "Return timing marker", Vector3.zero);
            markerParts.Emit(marker, "Ready ring", shadows: false);

            // East-side outdoor spectator bleacher in the wide 9.5m open promenade between Tennis & Halı Saha
            AddSpectatorBleacher(
                court,
                new Vector3(9.35f, 0f, 0f),
                yawDeg: -90f,
                length: 3.4f,
                covered: false,
                shader: shader,
                steelMat: steel,
                template: playerModel,
                sportKind: "tennis",
                watchBall: ball,
                spectatorNames: new[] { "Selin", "Mert" },
                shirtColors: new[] { new Color(0.86f, 0.84f, 0.78f), new Color(0.18f, 0.50f, 0.36f) });

            // Use the existing jointed player art with distinct shirts for both neighborhood tennis NPCs
            // so two NPCs rally autonomously whenever the player isn't playing!
            ProceduralHumanoid opponent = Instantiate(playerModel, court);
            opponent.name = "Mahalle Tenis Rakibi";
            ApplyNpcShirtColor(opponent, steel.shader, "Opponent Blue Sports Shirt", new Color(0.22f, 0.42f, 0.55f));

            ProceduralHumanoid ambientPlayer = Instantiate(playerModel, court);
            ambientPlayer.name = "Mahalle Tenis Oyuncusu";
            ApplyNpcShirtColor(ambientPlayer, steel.shader, "Ambient Crimson Sports Shirt", new Color(0.68f, 0.24f, 0.20f));

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
            ballSound.spatialBlend = 1.0f;
            ballSound.rolloffMode = AudioRolloffMode.Linear;
            ballSound.minDistance = 4f;
            ballSound.maxDistance = 24f;
            AudioClip hit = BuildBallSound("Racket hit", 340f, 61f, 0.30f);
            AudioClip bounce = BuildBallSound("Court bounce", 155f, 86f, 0.12f);
            court.gameObject.AddComponent<TennisCourt>().Configure(
                ball, ballShadow, opponent, opponentRacket, playerRacket, marker, ballSound, hit, bounce, ambientPlayer, tennisScoreboard);
        }

        private void ApplyNpcShirtColor(ProceduralHumanoid npc, Shader shader, string matName, Color shirtColor)
        {
            if (npc == null) return;
            Transform torso = npc.transform.Find("Hips/Spine");
            if (torso == null) return;
            Material shirt = MaterialFor(shader, matName, shirtColor, 0.22f);
            foreach (Renderer renderer in torso.GetComponentsInChildren<Renderer>(true))
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

            public void Arc(Material material, float radius, float width, float startDeg, float endDeg, int segments, Vector3 center)
            {
                if (segments < 2) segments = 2;
                float rIn = Mathf.Max(0.01f, radius - width * 0.5f);
                float rOut = radius + width * 0.5f;
                Vector3[] verts = new Vector3[(segments + 1) * 2];
                int[] tris = new int[segments * 6];
                for (int i = 0; i <= segments; i++)
                {
                    float deg = Mathf.Lerp(startDeg, endDeg, i / (float)segments);
                    float rad = deg * Mathf.Deg2Rad;
                    float cs = Mathf.Cos(rad);
                    float sn = Mathf.Sin(rad);
                    verts[i * 2] = center + new Vector3(cs * rIn, 0.0025f, sn * rIn);
                    verts[i * 2 + 1] = center + new Vector3(cs * rOut, 0.0025f, sn * rOut);
                    if (i < segments)
                    {
                        int t = i * 6;
                        int v = i * 2;
                        tris[t] = v;
                        tris[t + 1] = v + 2;
                        tris[t + 2] = v + 1;
                        tris[t + 3] = v + 1;
                        tris[t + 4] = v + 2;
                        tris[t + 5] = v + 3;
                    }
                }
                Mesh mesh = new Mesh { name = "CourtArc", vertices = verts, triangles = tris };
                mesh.RecalculateNormals();
                // Ensure upward-facing normal regardless of winding direction
                Vector3[] norms = mesh.normals;
                if (norms.Length > 0 && norms[0].y < 0f)
                {
                    for (int i = 0; i < tris.Length; i += 3)
                    {
                        int tmp = tris[i + 1];
                        tris[i + 1] = tris[i + 2];
                        tris[i + 2] = tmp;
                    }
                    mesh.triangles = tris;
                    mesh.RecalculateNormals();
                }
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
