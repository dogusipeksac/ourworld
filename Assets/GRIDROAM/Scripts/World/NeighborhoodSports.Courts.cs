using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    public sealed partial class NeighborhoodSports
    {
        private static readonly Vector3 FootballPitchPosition   = new Vector3(-14.0f, 0.055f, -44.0f);
        private static readonly Vector3 BasketballCourtPosition = new Vector3( 41.0f, 0.055f, -44.0f);
        private static readonly Vector3 VolleyballCourtPosition = new Vector3( 59.0f, 0.055f, -44.0f);

        private void BuildCommunitySportsArenas(
            ProceduralHumanoid playerModel,
            Shader shader,
            Material steel,
            Material rubber,
            Material cream,
            Material metal,
            Material asphalt,
            Material clay)
        {
            BuildFootballPitch(playerModel, shader, steel, rubber, cream, metal, asphalt);
            BuildBasketballCourt(playerModel, shader, steel, rubber, cream, metal, asphalt, clay);
            BuildVolleyballCourt(playerModel, shader, steel, rubber, cream, metal, asphalt);
            BuildPlayerHomeSign(shader, steel, cream);

            // Directional street signs along Selman Sokağı pointing south to the Hamidiye Sports Complex
            AddSign(transform, new Vector3(-13.8f, 1.50f, -6.6f), "HALI SAHA  ↑\nFUTBOL", steel, cream.color);
            AddSign(transform, new Vector3( 44.5f, 1.50f, -6.6f), "BASKETBOL ↑\nVOLEYBOL", steel, cream.color);
        }

        private void BuildPlayerHomeSign(Shader shader, Material steel, Material cream)
        {
            Material homeBadgeMat = MaterialFor(shader, "Player Home Sign Backing", new Color(0.11f, 0.42f, 0.34f), 0.35f);
            TextMesh tm = AddLiveScoreboard(transform, new Vector3(-62.8f, 1.72f, 5.4f), "BENİM EVİM\nSELMAN SK. NO:48", homeBadgeMat, new Color(1.0f, 0.94f, 0.66f));
            if (tm != null && tm.transform.parent != null)
            {
                // Face South (-Z) toward Selman Sokağı so it reads cleanly from the street!
                tm.transform.parent.localRotation = Quaternion.identity;
            }
        }

        private void BuildFootballPitch(
            ProceduralHumanoid playerModel,
            Shader shader,
            Material steel,
            Material rubber,
            Material cream,
            Material metal,
            Material asphalt)
        {
            Transform pitch = Child(transform, "Hamidiye_HaliSaha_Futbol", FootballPitchPosition);

            Material turfDark = MaterialFor(shader, "Halı Saha Dark Turf", new Color(0.14f, 0.44f, 0.19f), 0.14f);
            Material turfLight = MaterialFor(shader, "Halı Saha Light Turf", new Color(0.18f, 0.52f, 0.23f), 0.14f);
            Material whiteLine = MaterialFor(shader, "Sports Pure White", new Color(0.95f, 0.96f, 0.94f), 0.20f);
            Material goalNet = MaterialFor(shader, "Goal Net Mesh", new Color(0.84f, 0.86f, 0.88f), 0.15f);
            Material floodGlow = MaterialFor(shader, "Floodlight Glow", new Color(1.0f, 0.96f, 0.82f), 0.3f);
            floodGlow.EnableKeyword("_EMISSION");
            floodGlow.SetColor("_EmissionColor", new Color(0.75f, 0.70f, 0.52f));

            const float halfW = 8.4f;
            const float halfL = 12.2f;
            const float lineY = 0.014f;

            // Striped emerald turf + perimeter asphalt apron + crisp FIFA-style pitch lines
            MeshParts surface = new MeshParts(this);
            surface.Box(asphalt, new Vector3(0f, -0.055f, 0f), new Vector3(19.2f, 0.11f, 27.6f));
            int stripeCount = 10;
            float stripeLen = (halfL * 2f) / stripeCount;
            for (int i = 0; i < stripeCount; i++)
            {
                float z = -halfL + (i + 0.5f) * stripeLen;
                surface.Box(i % 2 == 0 ? turfDark : turfLight, new Vector3(0f, -0.01f, z), new Vector3(halfW * 2f, 0.036f, stripeLen + 0.01f));
            }

            // Touchlines, goal lines, halfway line, center circle, penalty areas
            foreach (float side in new[] { -1f, 1f })
            {
                surface.Box(whiteLine, new Vector3(side * (halfW - 0.5f), lineY, 0f), new Vector3(0.08f, 0.005f, (halfL - 0.6f) * 2f));
                surface.Box(whiteLine, new Vector3(0f, lineY, side * (halfL - 0.6f)), new Vector3((halfW - 0.5f) * 2f, 0.005f, 0.08f));
                // Penalty box
                surface.Box(whiteLine, new Vector3(0f, lineY, side * (halfL - 3.9f)), new Vector3(8.2f, 0.005f, 0.075f));
                surface.Box(whiteLine, new Vector3(-4.1f, lineY, side * (halfL - 2.25f)), new Vector3(0.075f, 0.005f, 3.3f));
                surface.Box(whiteLine, new Vector3( 4.1f, lineY, side * (halfL - 2.25f)), new Vector3(0.075f, 0.005f, 3.3f));
            }
            surface.Box(whiteLine, new Vector3(0f, lineY, 0f), new Vector3((halfW - 0.5f) * 2f, 0.005f, 0.08f));
            surface.Ring(whiteLine, 2.35f, 2.35f, 0.042f, 32, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, lineY, 0f));
            surface.Emit(pitch, "Football turf and lines");

            // Two realistic white 3D soccer goals with back net rods
            MeshParts goals = new MeshParts(this);
            const float gHalfW = 2.15f;
            const float gH = 2.15f;
            const float gDepth = 1.15f;
            foreach (float side in new[] { -1f, 1f })
            {
                float gz = side * (halfL - 0.6f);
                float backZ = gz + side * gDepth;
                goals.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, 0f, gz), new Vector3(-gHalfW, gH, gz), 0.055f);
                goals.Rod(whiteLine, _cylinder, new Vector3( gHalfW, 0f, gz), new Vector3( gHalfW, gH, gz), 0.055f);
                goals.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, gH, gz), new Vector3( gHalfW, gH, gz), 0.055f);
                // Back support frame
                goals.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, gH, gz), new Vector3(-gHalfW, gH * 0.85f, backZ), 0.035f);
                goals.Rod(whiteLine, _cylinder, new Vector3( gHalfW, gH, gz), new Vector3( gHalfW, gH * 0.85f, backZ), 0.035f);
                goals.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, 0f, backZ), new Vector3(-gHalfW, gH * 0.85f, backZ), 0.035f);
                goals.Rod(whiteLine, _cylinder, new Vector3( gHalfW, 0f, backZ), new Vector3( gHalfW, gH * 0.85f, backZ), 0.035f);
                goals.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, gH * 0.85f, backZ), new Vector3(gHalfW, gH * 0.85f, backZ), 0.032f);
                // Fine vertical net strands on back
                for (int n = -5; n <= 5; n++)
                {
                    float nx = n * (gHalfW / 5.5f);
                    goals.Box(goalNet, new Vector3(nx, gH * 0.43f, backZ), new Vector3(0.015f, gH * 0.85f, 0.015f));
                }
                for (int r = 1; r <= 4; r++)
                {
                    goals.Box(goalNet, new Vector3(0f, r * 0.42f, backZ), new Vector3(gHalfW * 2f, 0.015f, 0.015f));
                }
                AddCollider(pitch, "Goal frame", new Vector3(0f, gH * 0.5f, backZ), new Vector3(gHalfW * 2f, gH, 0.25f));
            }
            // Four corner floodlight poles
            foreach (float sx in new[] { -halfW - 0.6f, halfW + 0.6f })
            {
                foreach (float sz in new[] { -halfL - 0.8f, halfL + 0.8f })
                {
                    goals.Rod(metal, _cylinder, new Vector3(sx, 0f, sz), new Vector3(sx, 6.2f, sz), 0.09f);
                    goals.Box(floodGlow, new Vector3(sx * 0.96f, 6.1f, sz * 0.96f), new Vector3(0.65f, 0.28f, 0.35f));
                }
            }
            goals.Emit(pitch, "Football goals and floodlights", shadows: false);

            // Low perimeter fence with wide open entrance gate on north side (+Z)
            MeshParts fence = new MeshParts(this);
            AddFence(fence, pitch, steel, new Vector3(-9.2f, 0f, -13.4f), new Vector3(-9.2f, 0f, 13.4f));
            AddFence(fence, pitch, steel, new Vector3( 9.2f, 0f, -13.4f), new Vector3( 9.2f, 0f, 13.4f));
            AddFence(fence, pitch, steel, new Vector3(-9.2f, 0f, -13.4f), new Vector3( 9.2f, 0f, -13.4f));
            AddFence(fence, pitch, steel, new Vector3(-9.2f, 0f,  13.4f), new Vector3(-2.4f, 0f, 13.4f));
            AddFence(fence, pitch, steel, new Vector3( 2.4f, 0f,  13.4f), new Vector3( 9.2f, 0f, 13.4f));
            fence.Emit(pitch, "Halı saha fence", shadows: false);

            TextMesh scoreboard = AddLiveScoreboard(pitch, new Vector3(-5.2f, 2.0f, 13.55f), "HAMİDİYE HALI SAHA\nKIRMIZI 0 - 0 MAVİ", steel, cream.color);

            // Soccer ball + ground shadow
            Material ballMat = MaterialFor(shader, "Soccer Ball", new Color(0.96f, 0.96f, 0.96f), 0.35f);
            Material patchMat = MaterialFor(shader, "Soccer Patch", new Color(0.12f, 0.13f, 0.15f), 0.30f);
            Transform ball = Child(pitch, "Football_MatchBall", new Vector3(0f, 0.13f, 0f));
            MeshParts bp = new MeshParts(this);
            bp.Add(ballMat, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.26f);
            bp.Add(patchMat, _sphere, new Vector3(0.08f, 0.06f, 0f), Quaternion.identity, Vector3.one * 0.11f);
            bp.Add(patchMat, _sphere, new Vector3(-0.08f, -0.05f, 0.05f), Quaternion.identity, Vector3.one * 0.11f);
            bp.Emit(ball, "Soccer ball", shadows: false);

            Transform shadow = CreateBallShadow(pitch, shader, turfDark.color, 0.26f);

            // Spawn 3v3 animated NPC football players (Red vs Blue kits!)
            ProceduralHumanoid[] red = SpawnSportTeam(playerModel, pitch, shader, "Futbol_Kirmizi", 3, new Color(0.82f, 0.16f, 0.16f));
            ProceduralHumanoid[] blue = SpawnSportTeam(playerModel, pitch, shader, "Futbol_Mavi", 3, new Color(0.14f, 0.42f, 0.84f));

            AudioSource audio = CreateSpatialBallAudio(ball.gameObject);
            AudioClip kick = BuildBallSound("Soccer kick", 145f, 45f, 0.25f);
            pitch.gameObject.AddComponent<LiveFootballMatch>().Configure(red, blue, ball, shadow, scoreboard, audio, kick);
        }

        private void BuildBasketballCourt(
            ProceduralHumanoid playerModel,
            Shader shader,
            Material steel,
            Material rubber,
            Material cream,
            Material metal,
            Material asphalt,
            Material clay)
        {
            Transform court = Child(transform, "Hamidiye_BasketbolSahasi", BasketballCourtPosition);

            Material keyPaint = MaterialFor(shader, "Basketball Key Navy", new Color(0.15f, 0.28f, 0.44f), 0.22f);
            Material whiteLine = MaterialFor(shader, "Basketball Line White", new Color(0.95f, 0.96f, 0.94f), 0.20f);
            Material backboardMat = MaterialFor(shader, "Backboard Acrylic", new Color(0.92f, 0.95f, 0.98f), 0.72f);
            Material rimOrange = MaterialFor(shader, "Basketball Rim Orange", new Color(0.94f, 0.34f, 0.08f), 0.45f);
            Material ballOrange = MaterialFor(shader, "Basketball Leather", new Color(0.88f, 0.36f, 0.10f), 0.30f);

            const float halfW = 6.2f;
            const float halfL = 10.0f;
            const float lineY = 0.015f;

            MeshParts surface = new MeshParts(this);
            surface.Box(asphalt, new Vector3(0f, -0.055f, 0f), new Vector3(14.8f, 0.11f, 22.8f));
            surface.Box(clay, new Vector3(0f, -0.01f, 0f), new Vector3(halfW * 2f, 0.036f, halfL * 2f));

            foreach (float side in new[] { -1f, 1f })
            {
                // Painted 3-second key
                surface.Box(keyPaint, new Vector3(0f, 0.010f, side * (halfL - 2.6f)), new Vector3(3.8f, 0.005f, 5.2f));
                // Boundary & key lines
                surface.Box(whiteLine, new Vector3(side * halfW, lineY, 0f), new Vector3(0.075f, 0.005f, halfL * 2f));
                surface.Box(whiteLine, new Vector3(0f, lineY, side * halfL), new Vector3(halfW * 2f, 0.005f, 0.075f));
                surface.Box(whiteLine, new Vector3(0f, lineY, side * (halfL - 5.2f)), new Vector3(3.8f, 0.005f, 0.07f));
                surface.Ring(whiteLine, 1.55f, 1.55f, 0.036f, 24, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, lineY, side * (halfL - 5.2f)));
                // 3-point arc ring
                surface.Ring(whiteLine, 4.65f, 4.65f, 0.038f, 32, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, lineY, side * (halfL - 1.2f)));
            }
            surface.Box(whiteLine, new Vector3(0f, lineY, 0f), new Vector3(halfW * 2f, 0.005f, 0.075f));
            surface.Ring(whiteLine, 1.75f, 1.75f, 0.040f, 28, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, lineY, 0f));
            surface.Emit(court, "Basketball court surface");

            // Two realistic 3D basketball hoops at Z = ±9.35 m (Rim at y = 3.05 m!)
            MeshParts hoops = new MeshParts(this);
            foreach (float side in new[] { -1f, 1f })
            {
                float poleZ = side * (halfL + 0.55f);
                float boardZ = side * 9.35f;
                float rimZ = side * 8.95f;
                hoops.Rod(metal, _cylinder, new Vector3(0f, 0f, poleZ), new Vector3(0f, 3.65f, poleZ), 0.10f);
                hoops.Rod(metal, _cylinder, new Vector3(0f, 3.45f, poleZ), new Vector3(0f, 3.45f, boardZ), 0.07f);
                hoops.Box(backboardMat, new Vector3(0f, 3.40f, boardZ), new Vector3(1.80f, 1.12f, 0.06f));
                // Red shooter's square border on backboard
                hoops.Box(rimOrange, new Vector3(0f, 3.16f, boardZ - side * 0.036f), new Vector3(0.62f, 0.045f, 0.015f));
                hoops.Box(rimOrange, new Vector3(0f, 3.58f, boardZ - side * 0.036f), new Vector3(0.62f, 0.045f, 0.015f));
                // Orange steel rim (3.05 m official height)
                hoops.Ring(rimOrange, 0.24f, 0.24f, 0.020f, 20, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, 3.05f, rimZ));
                // White net hanging below rim
                for (int n = 0; n < 8; n++)
                {
                    float a = n * Mathf.PI * 0.25f;
                    Vector3 top = new Vector3(Mathf.Cos(a) * 0.22f, 3.03f, rimZ + Mathf.Sin(a) * 0.22f);
                    Vector3 bot = new Vector3(Mathf.Cos(a) * 0.14f, 2.60f, rimZ + Mathf.Sin(a) * 0.14f);
                    hoops.Rod(whiteLine, _cylinder, top, bot, 0.008f);
                }
                AddCollider(court, "Hoop pole", new Vector3(0f, 1.8f, poleZ), new Vector3(0.32f, 3.6f, 0.32f));
            }
            hoops.Emit(court, "Basketball hoops", shadows: false);

            TextMesh scoreboard = AddLiveScoreboard(court, new Vector3(-4.2f, 1.95f, 11.1f), "HAMİDİYE BASKETBOL\nSARI 0 - 0 LACİVERT", steel, cream.color);

            Transform ball = Child(court, "Basketball_MatchBall", new Vector3(0f, 0.135f, 0f));
            MeshParts bp = new MeshParts(this);
            bp.Add(ballOrange, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.27f);
            bp.Ring(rubber, 0.136f, 0.136f, 0.007f, 18, Quaternion.identity);
            bp.Ring(rubber, 0.136f, 0.136f, 0.007f, 18, Quaternion.Euler(0f, 90f, 0f));
            bp.Emit(ball, "Basketball", shadows: false);

            Transform shadow = CreateBallShadow(court, shader, clay.color, 0.26f);

            ProceduralHumanoid[] gold = SpawnSportTeam(playerModel, court, shader, "Basket_Sari", 2, new Color(0.92f, 0.66f, 0.14f));
            ProceduralHumanoid[] navy = SpawnSportTeam(playerModel, court, shader, "Basket_Lacivert", 2, new Color(0.14f, 0.24f, 0.52f));

            AudioSource audio = CreateSpatialBallAudio(ball.gameObject);
            AudioClip bounce = BuildBallSound("Basketball bounce", 130f, 52f, 0.18f);
            court.gameObject.AddComponent<LiveBasketballMatch>().Configure(gold, navy, ball, shadow, scoreboard, audio, bounce);
        }

        private void BuildVolleyballCourt(
            ProceduralHumanoid playerModel,
            Shader shader,
            Material steel,
            Material rubber,
            Material cream,
            Material metal,
            Material asphalt)
        {
            Transform court = Child(transform, "Hamidiye_VoleybolSahasi", VolleyballCourtPosition);

            Material sandMat = MaterialFor(shader, "Volleyball Warm Sand", new Color(0.84f, 0.72f, 0.50f), 0.12f);
            Material whiteLine = MaterialFor(shader, "Volleyball Line White", new Color(0.96f, 0.96f, 0.94f), 0.22f);
            Material ballWhite = MaterialFor(shader, "Volleyball White", new Color(0.96f, 0.96f, 0.95f), 0.35f);
            Material ballBlue = MaterialFor(shader, "Volleyball Blue Stripe", new Color(0.16f, 0.48f, 0.86f), 0.35f);

            const float halfW = 4.5f;
            const float halfL = 8.0f;
            const float lineY = 0.018f;

            MeshParts surface = new MeshParts(this);
            surface.Box(asphalt, new Vector3(0f, -0.055f, 0f), new Vector3(11.6f, 0.11f, 18.8f));
            surface.Box(sandMat, new Vector3(0f, -0.008f, 0f), new Vector3(10.6f, 0.04f, 17.8f));

            foreach (float side in new[] { -1f, 1f })
            {
                surface.Box(whiteLine, new Vector3(side * halfW, lineY, 0f), new Vector3(0.07f, 0.006f, halfL * 2f));
                surface.Box(whiteLine, new Vector3(0f, lineY, side * halfL), new Vector3(halfW * 2f, 0.006f, 0.07f));
                // 3-meter attack line
                surface.Box(whiteLine, new Vector3(0f, lineY, side * 3.0f), new Vector3(halfW * 2f, 0.006f, 0.055f));
            }
            surface.Box(whiteLine, new Vector3(0f, lineY, 0f), new Vector3(halfW * 2f, 0.006f, 0.06f));
            surface.Emit(court, "Volleyball sand court");

            // Authentic 2.43 m tall volleyball net across center
            MeshParts net = new MeshParts(this);
            const float netHalf = 4.9f;
            foreach (float x in new[] { -netHalf, netHalf })
            {
                net.Rod(metal, _cylinder, new Vector3(x, 0f, 0f), new Vector3(x, 2.58f, 0f), 0.055f);
            }
            net.Box(whiteLine, new Vector3(0f, 2.43f, 0f), new Vector3(netHalf * 2f, 0.075f, 0.028f));
            net.Box(whiteLine, new Vector3(0f, 1.45f, 0f), new Vector3(netHalf * 2f, 0.045f, 0.022f));
            for (int i = -18; i <= 18; i++)
            {
                float nx = i * (netHalf / 18.5f);
                net.Box(rubber, new Vector3(nx, 1.94f, 0f), new Vector3(0.012f, 0.96f, 0.012f));
            }
            for (int r = 1; r <= 7; r++)
            {
                net.Box(rubber, new Vector3(0f, 1.45f + r * 0.125f, 0f), new Vector3(netHalf * 2f, 0.012f, 0.012f));
            }
            net.Emit(court, "Volleyball net", shadows: false);
            AddCollider(court, "Volleyball net collider", new Vector3(0f, 1.9f, 0f), new Vector3(netHalf * 2f, 1.1f, 0.12f));

            TextMesh scoreboard = AddLiveScoreboard(court, new Vector3(-3.2f, 1.95f, 9.2f), "HAMİDİYE VOLEYBOL\nTURKUAZ 0 - 0 BORDO", steel, cream.color);

            Transform ball = Child(court, "Volleyball_MatchBall", new Vector3(0f, 1.5f, 0f));
            MeshParts bp = new MeshParts(this);
            bp.Add(ballWhite, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.24f);
            bp.Ring(ballBlue, 0.122f, 0.122f, 0.018f, 18, Quaternion.Euler(35f, 20f, 0f));
            bp.Emit(ball, "Volleyball", shadows: false);

            Transform shadow = CreateBallShadow(court, shader, sandMat.color, 0.23f);

            ProceduralHumanoid[] teal = SpawnSportTeam(playerModel, court, shader, "Voleybol_Turkuaz", 2, new Color(0.14f, 0.64f, 0.64f));
            ProceduralHumanoid[] burgundy = SpawnSportTeam(playerModel, court, shader, "Voleybol_Bordo", 2, new Color(0.64f, 0.16f, 0.28f));

            AudioSource audio = CreateSpatialBallAudio(ball.gameObject);
            AudioClip hit = BuildBallSound("Volleyball hit", 285f, 58f, 0.22f);
            court.gameObject.AddComponent<LiveVolleyballMatch>().Configure(teal, burgundy, ball, shadow, scoreboard, audio, hit);
        }

        private ProceduralHumanoid[] SpawnSportTeam(
            ProceduralHumanoid template,
            Transform parent,
            Shader shader,
            string prefix,
            int count,
            Color jerseyColor)
        {
            ProceduralHumanoid[] team = new ProceduralHumanoid[count];
            for (int i = 0; i < count; i++)
            {
                ProceduralHumanoid npc = Instantiate(template, parent);
                npc.name = $"{prefix}_{i + 1}";
                ApplyNpcShirtColor(npc, shader, $"{prefix}_Jersey_{i}", jerseyColor);
                team[i] = npc;
            }
            return team;
        }

        private Transform CreateBallShadow(Transform parent, Shader shader, Color groundColor, float diameter)
        {
            Material shadowMat = MaterialFor(shader, "Sport Ball Shadow", Color.Lerp(groundColor, Color.black, 0.40f), 0f);
            Transform shadow = Child(parent, "MatchBallShadow", Vector3.zero);
            MeshParts sp = new MeshParts(this);
            sp.Add(shadowMat, _sphere, Vector3.zero, Quaternion.identity, new Vector3(diameter, 0.002f, diameter));
            sp.Emit(shadow, "Ball shadow", shadows: false);
            return shadow;
        }

        private static AudioSource CreateSpatialBallAudio(GameObject host)
        {
            AudioSource src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 1.0f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 4f;
            src.maxDistance = 22f;
            return src;
        }

        private TextMesh AddLiveScoreboard(Transform parent, Vector3 position, string initialText, Material backing, Color color)
        {
            Transform sign = Child(parent, "LiveScoreboard", position);
            sign.localRotation = Quaternion.Euler(0f, 180f, 0f);
            MeshParts parts = new MeshParts(this);
            parts.Box(backing, Vector3.zero, new Vector3(2.35f, 0.62f, 0.08f));
            parts.Box(backing, new Vector3(0f, -position.y * 0.5f, 0f), new Vector3(0.095f, position.y, 0.095f));
            parts.Emit(sign, "Scoreboard panel", shadows: false);

            Transform letters = Child(sign, "ScoreboardText", new Vector3(0f, 0f, -0.045f));
            TextMesh text = letters.gameObject.AddComponent<TextMesh>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            letters.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.text = initialText;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 46;
            text.characterSize = 0.056f;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            return text;
        }
    }
}
