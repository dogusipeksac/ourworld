using OurWorld.Player;
using UnityEngine;

namespace OurWorld.World
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

        // =====================================================================
        // 1. HAMİDİYE HALI SAHA (3v3 TURKISH SYNTHETIC TURF ARENA)
        // =====================================================================
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

            // Procedural mowed emerald Halı Saha synthetic turf with rubber infill granules
            Texture2D turfDarkTex = BuildTurfTexture(new Color(0.11f, 0.39f, 0.16f), new Color(0.17f, 0.49f, 0.21f), 3101);
            Texture2D turfLightTex = BuildTurfTexture(new Color(0.16f, 0.48f, 0.21f), new Color(0.24f, 0.58f, 0.26f), 3102);
            Material turfDark = MaterialForTextured(shader, "Halı Saha Dark Turf", turfDarkTex, Color.white, 0.12f, new Vector2(5f, 1.2f));
            Material turfLight = MaterialForTextured(shader, "Halı Saha Light Turf", turfLightTex, Color.white, 0.12f, new Vector2(5f, 1.2f));
            Material wornTurf = MaterialFor(shader, "Goal Mouth Worn Turf", new Color(0.24f, 0.34f, 0.17f), 0.08f);
            Material whiteLine = MaterialFor(shader, "Sports Pure White", new Color(0.96f, 0.97f, 0.95f), 0.18f);
            Material goalNet = MaterialFor(shader, "Goal Net Mesh", new Color(0.90f, 0.92f, 0.94f), 0.14f);
            Material cagePostMat = MaterialFor(shader, "Halı Saha Cage Post", new Color(0.12f, 0.27f, 0.19f), 0.42f);
            Material cageWireMat = MaterialFor(shader, "Halı Saha Cage Wire", new Color(0.10f, 0.18f, 0.14f), 0.32f);
            Material flagMat = MaterialFor(shader, "Corner Flag Red", new Color(0.92f, 0.20f, 0.14f), 0.22f);
            Material floodHousing = MaterialFor(shader, "Stadium Flood Housing", new Color(0.14f, 0.16f, 0.18f), 0.42f);
            Material floodLens = MaterialFor(shader, "Stadium Flood Lens", new Color(1.0f, 0.97f, 0.88f), 0.65f);
            Material nightConeMat = MaterialForTranslucent(Shader.Find("Sprites/Default") ?? shader, "Football Night Flood Cone", new Color(1.0f, 0.96f, 0.84f, 0.072f), 0f);

            const float halfW = 8.4f;
            const float halfL = 12.2f;
            const float lineY = 0.014f;
            const float lineW = halfW - 0.5f;   // 7.9
            const float lineL = halfL - 0.6f;   // 11.6

            // Striped textured emerald turf + weathered asphalt apron + complete FIFA-style pitch markings
            MeshParts surface = new MeshParts(this);
            surface.Box(asphalt, new Vector3(0f, -0.055f, 0f), new Vector3(19.2f, 0.11f, 27.6f));
            int stripeCount = 10;
            float stripeLen = (halfL * 2f) / stripeCount;
            for (int i = 0; i < stripeCount; i++)
            {
                float z = -halfL + (i + 0.5f) * stripeLen;
                surface.Box(i % 2 == 0 ? turfDark : turfLight, new Vector3(0f, -0.01f, z), new Vector3(halfW * 2f, 0.036f, stripeLen + 0.01f));
            }

            // Touchlines, goal lines, halfway line, center circle + kickoff spot, penalty box, 6-yard box, penalty spot, D-arc & corner arcs!
            foreach (float side in new[] { -1f, 1f })
            {
                // Subtle worn turf patch in goal mouth
                surface.Box(wornTurf, new Vector3(0f, 0.0085f, side * (lineL - 0.65f)), new Vector3(4.2f, 0.002f, 1.35f));

                // Touchlines & goal lines
                surface.Box(whiteLine, new Vector3(side * lineW, lineY, 0f), new Vector3(0.085f, 0.005f, lineL * 2f));
                surface.Box(whiteLine, new Vector3(0f, lineY, side * lineL), new Vector3(lineW * 2f + 0.085f, 0.005f, 0.085f));

                // 18-yard penalty area
                surface.Box(whiteLine, new Vector3(0f, lineY, side * (lineL - 3.3f)), new Vector3(8.2f, 0.005f, 0.075f));
                surface.Box(whiteLine, new Vector3(-4.1f, lineY, side * (lineL - 1.65f)), new Vector3(0.075f, 0.005f, 3.3f));
                surface.Box(whiteLine, new Vector3( 4.1f, lineY, side * (lineL - 1.65f)), new Vector3(0.075f, 0.005f, 3.3f));

                // 6-yard goal area (Altıpas)
                surface.Box(whiteLine, new Vector3(0f, lineY, side * (lineL - 1.45f)), new Vector3(4.9f, 0.005f, 0.065f));
                surface.Box(whiteLine, new Vector3(-2.45f, lineY, side * (lineL - 0.725f)), new Vector3(0.065f, 0.005f, 1.45f));
                surface.Box(whiteLine, new Vector3( 2.45f, lineY, side * (lineL - 0.725f)), new Vector3(0.065f, 0.005f, 1.45f));

                // Penalty spot & clipped D-arc curving cleanly into the pitch!
                float penZ = side * (lineL - 2.35f);
                surface.Add(whiteLine, _cylinder, new Vector3(0f, lineY, penZ), Quaternion.identity, new Vector3(0.24f, 0.004f, 0.24f));
                float dStart = side > 0f ? 215f : 35f;
                float dEnd   = side > 0f ? 325f : 145f;
                surface.Arc(whiteLine, 1.65f, 0.070f, dStart, dEnd, 18, new Vector3(0f, lineY, penZ));
            }
            // Halfway line, center circle & kickoff spot
            surface.Box(whiteLine, new Vector3(0f, lineY, 0f), new Vector3(lineW * 2f, 0.005f, 0.085f));
            surface.Ring(whiteLine, 2.35f, 2.35f, 0.042f, 36, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, lineY, 0f));
            surface.Add(whiteLine, _cylinder, new Vector3(0f, lineY, 0f), Quaternion.identity, new Vector3(0.28f, 0.004f, 0.28f));

            // 4 FIFA corner quarter-arcs
            surface.Arc(whiteLine, 0.72f, 0.060f,   0f,  90f, 10, new Vector3(-lineW, lineY, -lineL));
            surface.Arc(whiteLine, 0.72f, 0.060f,  90f, 180f, 10, new Vector3( lineW, lineY, -lineL));
            surface.Arc(whiteLine, 0.72f, 0.060f, 180f, 270f, 10, new Vector3( lineW, lineY,  lineL));
            surface.Arc(whiteLine, 0.72f, 0.060f, 270f, 360f, 10, new Vector3(-lineW, lineY,  lineL));
            surface.Emit(pitch, "Football turf and lines");

            // 4 Fluttering Corner Flags
            AddCornerFlag(pitch, new Vector3(-lineW - 0.05f, 0f, -lineL - 0.05f), whiteLine, flagMat, 0.0f);
            AddCornerFlag(pitch, new Vector3( lineW + 0.05f, 0f, -lineL - 0.05f), whiteLine, flagMat, 1.4f);
            AddCornerFlag(pitch, new Vector3( lineW + 0.05f, 0f,  lineL + 0.05f), whiteLine, flagMat, 2.8f);
            AddCornerFlag(pitch, new Vector3(-lineW - 0.05f, 0f,  lineL + 0.05f), whiteLine, flagMat, 4.2f);

            // Two pro 3D soccer goals with full frames + complete back, side & top roof nets (each net pulses on goal!)
            MeshParts goalFrames = new MeshParts(this);
            const float gHalfW = 2.15f;
            const float gH = 2.15f;
            const float gDepthTop = 0.85f;
            const float gDepthBot = 1.25f;
            foreach (float side in new[] { -1f, 1f })
            {
                float gz = side * lineL;
                float topBackZ = gz + side * gDepthTop;
                float botBackZ = gz + side * gDepthBot;

                // Front goalposts & crossbar + rear ground base frame
                goalFrames.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, 0f, gz), new Vector3(-gHalfW, gH, gz), 0.058f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3( gHalfW, 0f, gz), new Vector3( gHalfW, gH, gz), 0.058f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, gH, gz), new Vector3( gHalfW, gH, gz), 0.058f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, gH, gz), new Vector3(-gHalfW, gH * 0.92f, topBackZ), 0.036f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3( gHalfW, gH, gz), new Vector3( gHalfW, gH * 0.92f, topBackZ), 0.036f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, gH * 0.92f, topBackZ), new Vector3(gHalfW, gH * 0.92f, topBackZ), 0.032f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, 0.03f, gz), new Vector3(-gHalfW, 0.03f, botBackZ), 0.034f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3( gHalfW, 0.03f, gz), new Vector3( gHalfW, 0.03f, botBackZ), 0.034f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, 0.03f, botBackZ), new Vector3(gHalfW, 0.03f, botBackZ), 0.034f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3(-gHalfW, 0.03f, botBackZ), new Vector3(-gHalfW, gH * 0.92f, topBackZ), 0.032f);
                goalFrames.Rod(whiteLine, _cylinder, new Vector3( gHalfW, 0.03f, botBackZ), new Vector3( gHalfW, gH * 0.92f, topBackZ), 0.032f);

                // Dedicated goal-net child mesh (sloped back + BOTH sides + top roof!) so it can ripple on goals
                Transform netRoot = Child(pitch, side > 0f ? "GoalNet_North" : "GoalNet_South", new Vector3(0f, gH * 0.5f, (gz + botBackZ) * 0.5f));
                Vector3 netOffset = netRoot.localPosition;
                MeshParts netMesh = new MeshParts(this);

                // Sloped rear net strands
                for (int n = -7; n <= 7; n++)
                {
                    float nx = n * (gHalfW / 7.5f);
                    netMesh.Rod(goalNet, _cylinder,
                        new Vector3(nx, 0.04f, botBackZ) - netOffset,
                        new Vector3(nx, gH * 0.92f, topBackZ) - netOffset, 0.008f);
                }
                for (int r = 1; r <= 6; r++)
                {
                    float u = r / 7f;
                    float ry = Mathf.Lerp(0.05f, gH * 0.92f, u);
                    float rz = Mathf.Lerp(botBackZ, topBackZ, u);
                    netMesh.Rod(goalNet, _cylinder,
                        new Vector3(-gHalfW, ry, rz) - netOffset,
                        new Vector3( gHalfW, ry, rz) - netOffset, 0.008f);
                }
                // Top roof net strands
                for (int n = -7; n <= 7; n++)
                {
                    float nx = n * (gHalfW / 7.5f);
                    netMesh.Rod(goalNet, _cylinder,
                        new Vector3(nx, gH, gz) - netOffset,
                        new Vector3(nx, gH * 0.92f, topBackZ) - netOffset, 0.007f);
                }
                for (int tr = 1; tr <= 3; tr++)
                {
                    float u = tr / 4f;
                    float tz = Mathf.Lerp(gz, topBackZ, u);
                    float ty = Mathf.Lerp(gH, gH * 0.92f, u);
                    netMesh.Rod(goalNet, _cylinder,
                        new Vector3(-gHalfW, ty, tz) - netOffset,
                        new Vector3( gHalfW, ty, tz) - netOffset, 0.007f);
                }
                // Left & Right side nets
                foreach (float sx in new[] { -gHalfW, gHalfW })
                {
                    for (int r = 1; r <= 6; r++)
                    {
                        float u = r / 7f;
                        float ry = Mathf.Lerp(0.05f, gH * 0.92f, u);
                        float rzBack = Mathf.Lerp(botBackZ, topBackZ, u);
                        netMesh.Rod(goalNet, _cylinder,
                            new Vector3(sx, ry, gz) - netOffset,
                            new Vector3(sx, ry, rzBack) - netOffset, 0.007f);
                    }
                    for (int c = 1; c <= 4; c++)
                    {
                        float u = c / 5f;
                        float czTop = Mathf.Lerp(gz, topBackZ, u);
                        float czBot = Mathf.Lerp(gz, botBackZ, u);
                        float cyTop = Mathf.Lerp(gH, gH * 0.92f, u);
                        netMesh.Rod(goalNet, _cylinder,
                            new Vector3(sx, 0.04f, czBot) - netOffset,
                            new Vector3(sx, cyTop, czTop) - netOffset, 0.007f);
                    }
                }
                netMesh.Emit(netRoot, "GoalNetStrands", shadows: false);
                _atmosphere?.RegisterNetTarget(netRoot, pitch.TransformPoint(netOffset));

                AddCollider(pitch, "Goal frame", new Vector3(0f, gH * 0.5f, botBackZ), new Vector3(gHalfW * 2f, gH, 0.25f));
            }
            goalFrames.Emit(pitch, "Football goal frames", shadows: true);

            // 4 Detailed Corner Stadium Floodlight Towers (outside the 9.2 x 13.4 cage!)
            foreach (float sx in new[] { -9.40f, 9.40f })
            {
                foreach (float sz in new[] { -13.60f, 13.60f })
                {
                    AddStadiumFloodlightTower(pitch, new Vector3(sx, 0f, sz), Vector3.zero, 6.9f, metal, floodHousing, floodLens, nightConeMat);
                }
            }

            // Authentic 3.55m Turkish Halı Saha Perimeter Wire Cage + Overhead Safety Net Cables + North Arch Entrance
            MeshParts fence = new MeshParts(this);
            AddTallCageFence(fence, pitch, cagePostMat, cageWireMat, new Vector3(-9.2f, 0f, -13.4f), new Vector3(-9.2f, 0f, 13.4f), 3.55f);
            AddTallCageFence(fence, pitch, cagePostMat, cageWireMat, new Vector3( 9.2f, 0f, -13.4f), new Vector3( 9.2f, 0f, 13.4f), 3.55f);
            AddTallCageFence(fence, pitch, cagePostMat, cageWireMat, new Vector3(-9.2f, 0f, -13.4f), new Vector3( 9.2f, 0f, -13.4f), 3.55f);
            AddTallCageFence(fence, pitch, cagePostMat, cageWireMat, new Vector3(-9.2f, 0f,  13.4f), new Vector3(-2.4f, 0f, 13.4f), 3.55f);
            AddTallCageFence(fence, pitch, cagePostMat, cageWireMat, new Vector3( 2.4f, 0f,  13.4f), new Vector3( 9.2f, 0f, 13.4f), 3.55f);

            // North open gate overhead header arch beam (high at Y = 3.55m so players walk right under!)
            fence.Box(cagePostMat, new Vector3(0f, 3.52f, 13.4f), new Vector3(5.0f, 0.14f, 0.14f));

            // Signature Turkish Halı Saha overhead safety net tension cables (thin cables high at Y = 3.72m)
            for (int r = -2; r <= 2; r++)
            {
                float cz = r * 4.45f;
                fence.Rod(cageWireMat, _cylinder, new Vector3(-9.2f, 3.68f, cz), new Vector3(9.2f, 3.68f, cz), 0.011f);
            }
            foreach (float cx in new[] { -4.6f, 0f, 4.6f })
            {
                fence.Rod(cageWireMat, _cylinder, new Vector3(cx, 3.68f, -13.4f), new Vector3(cx, 3.68f, 13.4f), 0.011f);
            }
            fence.Emit(pitch, "Halı saha wire cage", shadows: false);

            // Classic Turkish Halı Saha Perimeter Reklam / Kulüp Panoları along West, East & South boards!
            Material adNavy = MaterialFor(shader, "AdBoard Navy", new Color(0.09f, 0.22f, 0.42f), 0.28f);
            Material adEmerald = MaterialFor(shader, "AdBoard Emerald", new Color(0.09f, 0.36f, 0.24f), 0.28f);
            Material adCrimson = MaterialFor(shader, "AdBoard Crimson", new Color(0.58f, 0.12f, 0.14f), 0.28f);
            Material adGold = MaterialFor(shader, "AdBoard DarkGold", new Color(0.52f, 0.36f, 0.08f), 0.28f);
            AddPerimeterAdBoard(pitch, new Vector3(-9.08f, 0.02f, -6.5f), -90f, 5.2f, 0.88f, adNavy,    whiteLine, "HAMİDİYE SPOR", new Color(1.0f, 0.95f, 0.72f));
            AddPerimeterAdBoard(pitch, new Vector3(-9.08f, 0.02f,  0.0f), -90f, 5.2f, 0.88f, adEmerald, whiteLine, "SULTANBEYLİ",   Color.white);
            AddPerimeterAdBoard(pitch, new Vector3(-9.08f, 0.02f,  6.5f), -90f, 5.2f, 0.88f, adCrimson, whiteLine, "ATLAS AVM",     new Color(1.0f, 0.92f, 0.68f));
            AddPerimeterAdBoard(pitch, new Vector3( 9.08f, 0.02f, -6.5f),  90f, 5.2f, 0.88f, adGold,    whiteLine, "SELMAN OTO",    Color.white);
            AddPerimeterAdBoard(pitch, new Vector3( 9.08f, 0.02f,  0.0f),  90f, 5.2f, 0.88f, adNavy,    whiteLine, "AYDOS SU",      new Color(0.74f, 0.94f, 1.0f));
            AddPerimeterAdBoard(pitch, new Vector3( 9.08f, 0.02f,  6.5f),  90f, 5.2f, 0.88f, adEmerald, whiteLine, "HALI SAHA",     new Color(1.0f, 0.94f, 0.66f));
            AddPerimeterAdBoard(pitch, new Vector3( 0.00f, 0.02f, -13.28f), 180f, 6.4f, 0.88f, adNavy,   whiteLine, "HAMİDİYE ARENA", new Color(1.0f, 0.90f, 0.36f));

            // Glowing LED Digital Scoreboard on North fence
            TextMesh scoreboard = AddDigitalScoreboard(
                pitch,
                new Vector3(-5.6f, 2.25f, 13.62f),
                "HAMİDİYE HALI SAHA\nKIRMIZI 0 - 0 MAVİ",
                shader,
                new Color(0.18f, 0.82f, 0.44f),
                new Color(1.0f, 0.94f, 0.64f),
                width: 2.85f,
                height: 0.88f);

            // Soccer ball + ground shadow
            Material ballMat = MaterialFor(shader, "Soccer Ball", new Color(0.97f, 0.97f, 0.96f), 0.38f);
            Material patchMat = MaterialFor(shader, "Soccer Patch", new Color(0.10f, 0.11f, 0.13f), 0.32f);
            Transform ball = Child(pitch, "Football_MatchBall", new Vector3(0f, 0.13f, 0f));
            MeshParts bp = new MeshParts(this);
            bp.Add(ballMat, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.26f);
            bp.Add(patchMat, _sphere, new Vector3(0.08f, 0.06f, 0f), Quaternion.identity, Vector3.one * 0.11f);
            bp.Add(patchMat, _sphere, new Vector3(-0.08f, -0.05f, 0.05f), Quaternion.identity, Vector3.one * 0.11f);
            bp.Add(patchMat, _sphere, new Vector3(0f, 0.07f, -0.08f), Quaternion.identity, Vector3.one * 0.10f);
            bp.Emit(ball, "Soccer ball", shadows: false);

            Transform shadow = CreateBallShadow(pitch, shader, new Color(0.14f, 0.44f, 0.19f), 0.26f);

            // Covered 2-Tier Wooden Spectator Bleacher (3 cheering fans) + Covered Team Dugout along open East promenade!
            AddSpectatorBleacher(
                pitch,
                new Vector3(11.1f, 0f, -2.5f),
                yawDeg: -90f,
                length: 6.2f,
                covered: true,
                shader: shader,
                steelMat: cagePostMat,
                template: playerModel,
                sportKind: "football",
                watchBall: ball,
                spectatorNames: new[] { "Emre", "Burak", "Cem" },
                shirtColors: new[] { new Color(0.84f, 0.22f, 0.18f), new Color(0.18f, 0.44f, 0.78f), new Color(0.90f, 0.74f, 0.20f) });
            AddTeamDugout(pitch, new Vector3(10.85f, 0f, 4.4f), yawDeg: -90f, shader, cagePostMat);
            AddBallRack(pitch, new Vector3(10.65f, 0f, 7.2f), yawDeg: -90f, metal, ballMat, 0.24f, ballCount: 3);

            // Spawn 3v3 animated NPC football players (Red vs Blue kits!)
            ProceduralHumanoid[] red = SpawnSportTeam(playerModel, pitch, shader, "Futbol_Kirmizi", 3, new Color(0.82f, 0.16f, 0.16f));
            ProceduralHumanoid[] blue = SpawnSportTeam(playerModel, pitch, shader, "Futbol_Mavi", 3, new Color(0.14f, 0.42f, 0.84f));

            AudioSource audio = CreateSpatialBallAudio(ball.gameObject);
            AudioClip kick = BuildBallSound("Soccer kick", 145f, 45f, 0.25f);
            pitch.gameObject.AddComponent<LiveFootballMatch>().Configure(red, blue, ball, shadow, scoreboard, audio, kick);

            // Anytime playable triggers at the north open gate and center circle
            Child(pitch, "FootballEntrance", new Vector3(0f, 0f, 13.0f)).gameObject
                .AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.SportsArena, "hamidiye_football_arena", "ui.btn.play_football", 5.2f);
            Child(pitch, "FootballCenterTrigger", Vector3.zero).gameObject
                .AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.SportsArena, "hamidiye_football_center", "ui.btn.play_football", 8.2f);
        }

        // =====================================================================
        // 2. HAMİDİYE BASKETBOL SAHASI (PRO ACRYLIC STREETBALL COURT)
        // =====================================================================
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

            // Textured non-slip municipal acrylic court + deep navy 3-second key + tempered-glass backboards!
            Texture2D courtAcrylicTex = BuildAcrylicCourtTexture(new Color(0.64f, 0.28f, 0.18f), 4101);
            Texture2D keyNavyTex = BuildAcrylicCourtTexture(new Color(0.13f, 0.27f, 0.48f), 4102);
            Material courtAcrylic = MaterialForTextured(shader, "Basketball Acrylic Red", courtAcrylicTex, Color.white, 0.22f, new Vector2(4f, 7f));
            Material keyPaint = MaterialForTextured(shader, "Basketball Key Navy", keyNavyTex, Color.white, 0.24f, new Vector2(2f, 3f));
            Material centerFill = MaterialFor(shader, "Basketball Center Navy", new Color(0.14f, 0.28f, 0.48f), 0.24f);
            Material whiteLine = MaterialFor(shader, "Basketball Line White", new Color(0.96f, 0.97f, 0.95f), 0.18f);
            Material backboardGlass = MaterialForTranslucent(shader, "Backboard Tempered Glass", new Color(0.86f, 0.94f, 0.99f, 0.46f), 0.85f);
            Material padNavy = MaterialFor(shader, "Hoop Stanchion Pad", new Color(0.10f, 0.20f, 0.38f), 0.28f);
            Material rimOrange = MaterialFor(shader, "Basketball Rim Orange", new Color(0.96f, 0.34f, 0.08f), 0.48f);
            Material ballOrange = MaterialFor(shader, "Basketball Leather", new Color(0.90f, 0.36f, 0.10f), 0.32f);
            Material clockBlack = MaterialFor(shader, "ShotClock Housing", new Color(0.08f, 0.09f, 0.11f), 0.30f);
            Material wireDark = MaterialFor(shader, "Streetball Fence Wire", new Color(0.15f, 0.18f, 0.20f), 0.36f);
            Material floodHousing = MaterialFor(shader, "Basket Flood Housing", new Color(0.14f, 0.16f, 0.18f), 0.42f);
            Material floodLens = MaterialFor(shader, "Basket Flood Lens", new Color(0.96f, 0.98f, 1.0f), 0.65f);
            Material nightConeMat = MaterialForTranslucent(Shader.Find("Sprites/Default") ?? shader, "Basket Night Flood Cone", new Color(0.90f, 0.96f, 1.0f, 0.068f), 0f);

            const float halfW = 6.2f;
            const float halfL = 10.0f;
            const float rimZ = 8.95f;
            const float lineY = 0.015f;
            const float threePtR = 4.75f; // Matches LiveBasketballMatch.PlayerInThreePointZone EXACTLY!

            MeshParts surface = new MeshParts(this);
            surface.Box(asphalt, new Vector3(0f, -0.055f, 0f), new Vector3(14.8f, 0.11f, 22.8f));
            surface.Box(courtAcrylic, new Vector3(0f, -0.01f, 0f), new Vector3(halfW * 2f, 0.036f, halfL * 2f));

            // Center jump circle medallion fill
            surface.Add(centerFill, _cylinder, new Vector3(0f, 0.0095f, 0f), Quaternion.identity, new Vector3(3.45f, 0.004f, 3.45f));

            foreach (float side in new[] { -1f, 1f })
            {
                // Painted 3-second key
                surface.Box(keyPaint, new Vector3(0f, 0.010f, side * (halfL - 2.6f)), new Vector3(3.8f, 0.005f, 5.2f));
                // Court outer boundary lines
                surface.Box(whiteLine, new Vector3(side * halfW, lineY, 0f), new Vector3(0.08f, 0.005f, halfL * 2f));
                surface.Box(whiteLine, new Vector3(0f, lineY, side * halfL), new Vector3(halfW * 2f + 0.08f, 0.005f, 0.08f));
                // Key outer lane lines & free-throw line
                surface.Box(whiteLine, new Vector3(-1.9f, lineY, side * (halfL - 2.6f)), new Vector3(0.065f, 0.005f, 5.2f));
                surface.Box(whiteLine, new Vector3( 1.9f, lineY, side * (halfL - 2.6f)), new Vector3(0.065f, 0.005f, 5.2f));
                surface.Box(whiteLine, new Vector3(0f, lineY, side * (halfL - 5.2f)), new Vector3(3.86f, 0.005f, 0.07f));
                // Rebound hash tick marks along both sides of the free-throw lane
                foreach (float hz in new[] { 1.75f, 2.65f, 3.55f, 4.40f })
                {
                    float mz = side * (halfL - hz);
                    surface.Box(whiteLine, new Vector3(-2.06f, lineY, mz), new Vector3(0.24f, 0.005f, 0.055f));
                    surface.Box(whiteLine, new Vector3( 2.06f, lineY, mz), new Vector3(0.24f, 0.005f, 0.055f));
                }
                // Free-throw semicircle + Restricted area arc under basket
                float arcStart = side > 0f ? 180f : 0f;
                float arcEnd   = side > 0f ? 360f : 180f;
                surface.Arc(whiteLine, 1.55f, 0.065f, arcStart, arcEnd, 22, new Vector3(0f, lineY, side * (halfL - 5.2f)));
                surface.Arc(whiteLine, 1.05f, 0.055f, arcStart, arcEnd, 18, new Vector3(0f, lineY, side * rimZ));

                // TRUE CLIPPED FIBA 3-POINT ARC + STRAIGHT CORNER-3 SIDELINES (NEVER overflows baselines!)
                surface.Arc(whiteLine, threePtR, 0.075f, arcStart, arcEnd, 36, new Vector3(0f, lineY, side * rimZ));
                float cornerLen = halfL - rimZ; // 1.05m straight segment to baseline
                float cornerMidZ = side * (rimZ + cornerLen * 0.5f);
                surface.Box(whiteLine, new Vector3(-threePtR, lineY, cornerMidZ), new Vector3(0.075f, 0.005f, cornerLen));
                surface.Box(whiteLine, new Vector3( threePtR, lineY, cornerMidZ), new Vector3(0.075f, 0.005f, cornerLen));
            }
            surface.Box(whiteLine, new Vector3(0f, lineY, 0f), new Vector3(halfW * 2f, 0.005f, 0.08f));
            surface.Ring(whiteLine, 1.75f, 1.75f, 0.042f, 32, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, lineY, 0f));
            surface.Ring(whiteLine, 0.62f, 0.62f, 0.032f, 20, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, lineY, 0f));
            surface.Emit(court, "Basketball court surface");

            // Two pro tempered-glass basketball hoops with padded stanchions, full 4-sided shooter's square, 24s shot clock & diamond nets!
            MeshParts hoops = new MeshParts(this);
            foreach (float side in new[] { -1f, 1f })
            {
                float poleZ = side * (halfL + 0.58f);
                float boardZ = side * 9.35f;
                float rZ = side * rimZ;
                float faceOff = -side * 0.036f;

                // Concrete base + steel gooseneck stanchion + navy safety foam wrap on lower 1.95m
                hoops.Box(asphalt, new Vector3(0f, 0.16f, poleZ), new Vector3(0.48f, 0.32f, 0.48f));
                hoops.Box(padNavy, new Vector3(0f, 1.10f, poleZ), new Vector3(0.28f, 1.88f, 0.28f));
                hoops.Rod(metal, _cylinder, new Vector3(0f, 1.95f, poleZ), new Vector3(0f, 3.68f, poleZ), 0.095f);
                hoops.Rod(metal, _cylinder, new Vector3(0f, 3.50f, poleZ), new Vector3(0f, 3.45f, boardZ + side * 0.04f), 0.075f);
                hoops.Rod(metal, _cylinder, new Vector3(0f, 2.88f, poleZ), new Vector3(0f, 3.38f, boardZ + side * 0.04f), 0.045f);

                // Translucent tempered-glass backboard + crisp white aluminum perimeter frame
                hoops.Box(backboardGlass, new Vector3(0f, 3.42f, boardZ), new Vector3(1.80f, 1.12f, 0.05f));
                hoops.Box(whiteLine, new Vector3(0f, 3.98f, boardZ), new Vector3(1.86f, 0.055f, 0.068f));
                hoops.Box(whiteLine, new Vector3(0f, 2.86f, boardZ), new Vector3(1.86f, 0.055f, 0.068f));
                hoops.Box(whiteLine, new Vector3(-0.90f, 3.42f, boardZ), new Vector3(0.055f, 1.16f, 0.068f));
                hoops.Box(whiteLine, new Vector3( 0.90f, 3.42f, boardZ), new Vector3(0.055f, 1.16f, 0.068f));

                // Full 4-sided orange shooter's square on glass face
                hoops.Box(rimOrange, new Vector3(0f, 3.12f, boardZ + faceOff), new Vector3(0.60f, 0.042f, 0.014f));
                hoops.Box(rimOrange, new Vector3(0f, 3.56f, boardZ + faceOff), new Vector3(0.60f, 0.042f, 0.014f));
                hoops.Box(rimOrange, new Vector3(-0.28f, 3.34f, boardZ + faceOff), new Vector3(0.042f, 0.46f, 0.014f));
                hoops.Box(rimOrange, new Vector3( 0.28f, 3.34f, boardZ + faceOff), new Vector3(0.042f, 0.46f, 0.014f));

                // Rim mounting flange + orange breakaway rim (3.05 m official height)
                hoops.Box(rimOrange, new Vector3(0f, 3.05f, (boardZ + rZ) * 0.5f), new Vector3(0.14f, 0.08f, Mathf.Abs(boardZ - rZ)));
                hoops.Ring(rimOrange, 0.24f, 0.24f, 0.020f, 22, Quaternion.Euler(90f, 0f, 0f), new Vector3(0f, 3.05f, rZ));

                // Mini glowing 24-second Shot Clock atop backboard
                hoops.Rod(metal, _cylinder, new Vector3(0f, 4.00f, boardZ), new Vector3(0f, 4.24f, boardZ), 0.025f);
                hoops.Box(clockBlack, new Vector3(0f, 4.44f, boardZ), new Vector3(0.56f, 0.40f, 0.14f));
                Transform scText = Child(court, side > 0f ? "ShotClock_North" : "ShotClock_South", new Vector3(0f, 4.44f, boardZ + (side > 0f ? -0.078f : 0.078f)));
                scText.localRotation = side > 0f ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
                TextMesh scTm = scText.gameObject.AddComponent<TextMesh>();
                scTm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                scText.GetComponent<MeshRenderer>().sharedMaterial = scTm.font.material;
                scTm.text = "24";
                scTm.anchor = TextAnchor.MiddleCenter;
                scTm.alignment = TextAlignment.Center;
                scTm.fontSize = 46;
                scTm.characterSize = 0.056f;
                scTm.fontStyle = FontStyle.Bold;
                scTm.color = new Color(1.0f, 0.28f, 0.16f);

                // Dedicated criss-cross diamond nylon net child so swishes trigger a real net ripple!
                Transform netRoot = Child(court, side > 0f ? "HoopNet_North" : "HoopNet_South", new Vector3(0f, 3.03f, rZ));
                MeshParts netP = new MeshParts(this);
                const int loops = 10;
                for (int n = 0; n < loops; n++)
                {
                    float a0 = n * (Mathf.PI * 2f / loops);
                    float aNext = (n + 1) * (Mathf.PI * 2f / loops);
                    float aMid = (a0 + aNext) * 0.5f;
                    Vector3 top0 = new Vector3(Mathf.Cos(a0) * 0.22f, 0f, Mathf.Sin(a0) * 0.22f);
                    Vector3 top1 = new Vector3(Mathf.Cos(aNext) * 0.22f, 0f, Mathf.Sin(aNext) * 0.22f);
                    Vector3 mid  = new Vector3(Mathf.Cos(aMid) * 0.175f, -0.22f, Mathf.Sin(aMid) * 0.175f);
                    Vector3 bot0 = new Vector3(Mathf.Cos(a0) * 0.135f, -0.44f, Mathf.Sin(a0) * 0.135f);
                    Vector3 bot1 = new Vector3(Mathf.Cos(aNext) * 0.135f, -0.44f, Mathf.Sin(aNext) * 0.135f);
                    netP.Rod(whiteLine, _cylinder, top0, mid, 0.007f);
                    netP.Rod(whiteLine, _cylinder, top1, mid, 0.007f);
                    netP.Rod(whiteLine, _cylinder, mid, bot0, 0.007f);
                    netP.Rod(whiteLine, _cylinder, mid, bot1, 0.007f);
                }
                netP.Emit(netRoot, "DiamondHoopNet", shadows: false);
                _atmosphere?.RegisterNetTarget(netRoot, pitchPosWorld(court, new Vector3(0f, 2.85f, rZ)));

                AddCollider(court, "Hoop pole", new Vector3(0f, 1.8f, poleZ), new Vector3(0.36f, 3.6f, 0.36f));
            }
            hoops.Emit(court, "Basketball hoops", shadows: true);

            // 3.1m Streetball Perimeter Backstop Mesh Fence (South full, East full, North wings leaving wide open center entry!)
            MeshParts bFence = new MeshParts(this);
            AddTallCageFence(bFence, court, steel, wireDark, new Vector3(-7.2f, 0f, -11.2f), new Vector3(7.2f, 0f, -11.2f), 3.1f);
            AddTallCageFence(bFence, court, steel, wireDark, new Vector3( 7.2f, 0f, -11.2f), new Vector3(7.2f, 0f,  11.2f), 3.1f);
            AddTallCageFence(bFence, court, steel, wireDark, new Vector3(-7.2f, 0f,  11.2f), new Vector3(-3.3f, 0f, 11.2f), 3.1f);
            AddTallCageFence(bFence, court, steel, wireDark, new Vector3( 3.3f, 0f,  11.2f), new Vector3( 7.2f, 0f, 11.2f), 3.1f);
            bFence.Emit(court, "Streetball backstop fence", shadows: false);

            // Two East Corner Floodlight Towers
            AddStadiumFloodlightTower(court, new Vector3(7.55f, 0f, -10.8f), Vector3.zero, 6.4f, metal, floodHousing, floodLens, nightConeMat);
            AddStadiumFloodlightTower(court, new Vector3(7.55f, 0f,  10.8f), Vector3.zero, 6.4f, metal, floodHousing, floodLens, nightConeMat);

            // Glowing LED Digital Scoreboard on North-West wing
            TextMesh scoreboard = AddDigitalScoreboard(
                court,
                new Vector3(-5.1f, 2.15f, 11.35f),
                "HAMİDİYE BASKETBOL\nSARI 0 - 0 LACİVERT",
                shader,
                new Color(0.96f, 0.48f, 0.12f),
                new Color(1.0f, 0.94f, 0.68f),
                width: 2.75f,
                height: 0.84f);

            Transform ball = Child(court, "Basketball_MatchBall", new Vector3(0f, 0.135f, 0f));
            MeshParts bp = new MeshParts(this);
            bp.Add(ballOrange, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.27f);
            bp.Ring(rubber, 0.136f, 0.136f, 0.007f, 18, Quaternion.identity);
            bp.Ring(rubber, 0.136f, 0.136f, 0.007f, 18, Quaternion.Euler(0f, 90f, 0f));
            bp.Emit(ball, "Basketball", shadows: false);

            Transform shadow = CreateBallShadow(court, shader, new Color(0.64f, 0.28f, 0.18f), 0.26f);

            // West Sideline 2-Tier Streetball Bleacher (2 cheering fans) + Rolling Basketball Rack
            AddSpectatorBleacher(
                court,
                new Vector3(-8.45f, 0f, -1.0f),
                yawDeg: 90f,
                length: 4.2f,
                covered: false,
                shader: shader,
                steelMat: steel,
                template: playerModel,
                sportKind: "basketball",
                watchBall: ball,
                spectatorNames: new[] { "Kaan", "Denizcan" },
                shirtColors: new[] { new Color(0.92f, 0.64f, 0.16f), new Color(0.18f, 0.30f, 0.56f) });
            AddBallRack(court, new Vector3(-7.90f, 0f, 3.4f), yawDeg: 90f, metal, ballOrange, 0.25f, ballCount: 3);

            ProceduralHumanoid[] gold = SpawnSportTeam(playerModel, court, shader, "Basket_Sari", 2, new Color(0.92f, 0.66f, 0.14f));
            ProceduralHumanoid[] navy = SpawnSportTeam(playerModel, court, shader, "Basket_Lacivert", 2, new Color(0.14f, 0.24f, 0.52f));

            AudioSource audio = CreateSpatialBallAudio(ball.gameObject);
            AudioClip bounce = BuildBallSound("Basketball bounce", 130f, 52f, 0.18f);
            court.gameObject.AddComponent<LiveBasketballMatch>().Configure(gold, navy, ball, shadow, scoreboard, audio, bounce);

            // Anytime playable triggers at court approach and center court
            Child(court, "BasketballEntrance", new Vector3(0f, 0f, 10.6f)).gameObject
                .AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.SportsArena, "hamidiye_basketball_arena", "ui.btn.play_basketball", 5.2f);
            Child(court, "BasketballCenterTrigger", Vector3.zero).gameObject
                .AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.SportsArena, "hamidiye_basketball_center", "ui.btn.play_basketball", 7.4f);
        }

        // =====================================================================
        // 3. HAMİDİYE VOLEYBOL SAHASI (TIMBER-CURBED BEACH VOLLEYBALL COURT)
        // =====================================================================
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

            // Procedural rippled golden beach sand + warm timber pit curb + royal-blue boundary tapes!
            Texture2D sandTex = BuildSandCourtTexture(new Color(0.86f, 0.74f, 0.50f), 5101);
            Material sandMat = MaterialForTextured(shader, "Volleyball Warm Sand", sandTex, Color.white, 0.08f, new Vector2(5f, 8f));
            Material timberCurb = MaterialFor(shader, "Volleyball Timber Curb", new Color(0.50f, 0.32f, 0.18f), 0.24f);
            Material tapeBlue = MaterialFor(shader, "Volleyball Boundary Tape Blue", new Color(0.10f, 0.40f, 0.84f), 0.24f);
            Material whiteLine = MaterialFor(shader, "Volleyball Line White", new Color(0.97f, 0.97f, 0.95f), 0.20f);
            Material postPadYellow = MaterialFor(shader, "Volley Post Pad Yellow", new Color(0.96f, 0.76f, 0.12f), 0.30f);
            Material antennaRed = MaterialFor(shader, "Volley Antenna Red", new Color(0.92f, 0.16f, 0.14f), 0.35f);
            Material ballWhite = MaterialFor(shader, "Volleyball White", new Color(0.96f, 0.96f, 0.95f), 0.35f);
            Material ballBlue = MaterialFor(shader, "Volleyball Blue Stripe", new Color(0.16f, 0.48f, 0.86f), 0.35f);
            Material wireDark = MaterialFor(shader, "Volley Catch Wire", new Color(0.15f, 0.20f, 0.18f), 0.34f);
            Material floodHousing = MaterialFor(shader, "Volley Flood Housing", new Color(0.14f, 0.16f, 0.18f), 0.42f);
            Material floodLens = MaterialFor(shader, "Volley Flood Lens", new Color(1.0f, 0.97f, 0.88f), 0.65f);
            Material nightConeMat = MaterialForTranslucent(Shader.Find("Sprites/Default") ?? shader, "Volley Night Flood Cone", new Color(1.0f, 0.96f, 0.86f, 0.068f), 0f);

            const float halfW = 4.5f;
            const float halfL = 8.0f;
            const float lineY = 0.019f;

            MeshParts surface = new MeshParts(this);
            surface.Box(asphalt, new Vector3(0f, -0.055f, 0f), new Vector3(11.8f, 0.11f, 19.0f));
            surface.Box(sandMat, new Vector3(0f, -0.008f, 0f), new Vector3(10.6f, 0.042f, 17.8f));

            // Recessed natural timber sand-pit curb frame around perimeter
            surface.Box(timberCurb, new Vector3(-5.38f, 0.05f, 0f), new Vector3(0.18f, 0.13f, 18.14f));
            surface.Box(timberCurb, new Vector3( 5.38f, 0.05f, 0f), new Vector3(0.18f, 0.13f, 18.14f));
            surface.Box(timberCurb, new Vector3(0f, 0.05f, -8.98f), new Vector3(10.94f, 0.13f, 0.18f));
            surface.Box(timberCurb, new Vector3(0f, 0.05f,  8.98f), new Vector3(10.94f, 0.13f, 0.18f));

            // High-contrast royal-blue beach boundary tapes + white center & 3m attack lines + corner anchor stakes
            foreach (float side in new[] { -1f, 1f })
            {
                surface.Box(tapeBlue, new Vector3(side * halfW, lineY, 0f), new Vector3(0.085f, 0.007f, halfL * 2f));
                surface.Box(tapeBlue, new Vector3(0f, lineY, side * halfL), new Vector3(halfW * 2f + 0.085f, 0.007f, 0.085f));
                // 3-meter attack line
                surface.Box(whiteLine, new Vector3(0f, lineY, side * 3.0f), new Vector3(halfW * 2f, 0.006f, 0.055f));
                // Corner stakes
                foreach (float sx in new[] { -halfW, halfW })
                {
                    surface.Add(whiteLine, _cylinder, new Vector3(sx, 0.045f, side * halfL), Quaternion.identity, new Vector3(0.09f, 0.04f, 0.09f));
                }
            }
            surface.Box(whiteLine, new Vector3(0f, lineY, 0f), new Vector3(halfW * 2f, 0.006f, 0.06f));
            surface.Emit(court, "Volleyball sand court");

            // Pro 2.43 m Olympic-style volleyball net with padded posts, side bands & red/white striped fiberglass antennas!
            MeshParts net = new MeshParts(this);
            const float netHalf = 4.95f;
            foreach (float x in new[] { -netHalf, netHalf })
            {
                net.Rod(metal, _cylinder, new Vector3(x, 0f, 0f), new Vector3(x, 2.62f, 0f), 0.055f);
                // Bright yellow padded safety sleeve wrapping the lower 2.1m of each post
                net.Rod(postPadYellow, _cylinder, new Vector3(x, 0.12f, 0f), new Vector3(x, 2.15f, 0f), 0.082f);
            }
            // Top & bottom crisp white canvas bands + side boundary bands at ±4.5m
            net.Box(whiteLine, new Vector3(0f, 2.43f, 0f), new Vector3(netHalf * 2f, 0.080f, 0.030f));
            net.Box(whiteLine, new Vector3(0f, 1.45f, 0f), new Vector3(netHalf * 2f, 0.052f, 0.024f));
            foreach (float ax in new[] { -halfW, halfW })
            {
                net.Box(whiteLine, new Vector3(ax, 1.94f, 0f), new Vector3(0.055f, 0.98f, 0.030f));
                // Iconic Red-and-White striped fiberglass net antennas rising 0.84m above the top tape!
                for (int seg = 0; seg < 7; seg++)
                {
                    float y0 = 2.44f + seg * 0.12f;
                    float y1 = y0 + 0.12f;
                    net.Rod(seg % 2 == 0 ? antennaRed : whiteLine, _cylinder, new Vector3(ax, y0, 0f), new Vector3(ax, y1, 0f), 0.013f);
                }
            }
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

            // Elevated Steel Referee Stand (Hakem Kulesi) cleanly outside the West timber sand-pit curb!
            AddElevatedRefChair(court, new Vector3(-5.90f, 0f, 0f), yawDeg: 90f, seatHeight: 1.42f, withCanopy: false, shader, metal, tapeBlue);

            // South baseline ball-stop catch fence + 2 SW/SE corner floodlight towers
            MeshParts vFence = new MeshParts(this);
            AddTallCageFence(vFence, court, steel, wireDark, new Vector3(-5.6f, 0f, -9.35f), new Vector3(5.6f, 0f, -9.35f), 2.75f);
            vFence.Emit(court, "Volleyball catch fence", shadows: false);

            AddStadiumFloodlightTower(court, new Vector3(-5.75f, 0f, -9.25f), Vector3.zero, 6.0f, metal, floodHousing, floodLens, nightConeMat);
            AddStadiumFloodlightTower(court, new Vector3( 5.75f, 0f, -9.25f), Vector3.zero, 6.0f, metal, floodHousing, floodLens, nightConeMat);

            // Glowing LED Digital Scoreboard on North-West corner
            TextMesh scoreboard = AddDigitalScoreboard(
                court,
                new Vector3(-3.35f, 2.05f, 9.35f),
                "HAMİDİYE VOLEYBOL\nTURKUAZ 0 - 0 BORDO",
                shader,
                new Color(0.14f, 0.78f, 0.76f),
                new Color(1.0f, 0.95f, 0.72f),
                width: 2.65f,
                height: 0.82f);

            Transform ball = Child(court, "Volleyball_MatchBall", new Vector3(0f, 1.5f, 0f));
            MeshParts bp = new MeshParts(this);
            bp.Add(ballWhite, _sphere, Vector3.zero, Quaternion.identity, Vector3.one * 0.24f);
            bp.Ring(ballBlue, 0.122f, 0.122f, 0.018f, 18, Quaternion.Euler(35f, 20f, 0f));
            bp.Emit(ball, "Volleyball", shadows: false);

            Transform shadow = CreateBallShadow(court, shader, new Color(0.84f, 0.72f, 0.50f), 0.23f);

            // West-North apron Spectator Bench (1 cheering fan) + Striped Beach Umbrella + Rolling Volleyball Cart
            AddSpectatorBleacher(
                court,
                new Vector3(-6.55f, 0f, 4.4f),
                yawDeg: 90f,
                length: 2.4f,
                covered: false,
                shader: shader,
                steelMat: steel,
                template: playerModel,
                sportKind: "volleyball",
                watchBall: ball,
                spectatorNames: new[] { "Ece" },
                shirtColors: new[] { new Color(0.16f, 0.72f, 0.70f) });
            AddBeachUmbrella(court, new Vector3(-6.45f, 0f, 6.75f), shader, metal, new Color(0.14f, 0.64f, 0.68f), Color.white);
            AddBallRack(court, new Vector3(-6.05f, 0f, -3.5f), yawDeg: 90f, metal, ballWhite, 0.23f, ballCount: 3);

            ProceduralHumanoid[] teal = SpawnSportTeam(playerModel, court, shader, "Voleybol_Turkuaz", 2, new Color(0.14f, 0.64f, 0.64f));
            ProceduralHumanoid[] burgundy = SpawnSportTeam(playerModel, court, shader, "Voleybol_Bordo", 2, new Color(0.64f, 0.16f, 0.28f));

            AudioSource audio = CreateSpatialBallAudio(ball.gameObject);
            AudioClip hit = BuildBallSound("Volleyball hit", 285f, 58f, 0.22f);
            court.gameObject.AddComponent<LiveVolleyballMatch>().Configure(teal, burgundy, ball, shadow, scoreboard, audio, hit);

            // Anytime playable triggers at court approach and sand center
            Child(court, "VolleyballEntrance", new Vector3(0f, 0f, 8.6f)).gameObject
                .AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.SportsArena, "hamidiye_volleyball_arena", "ui.btn.play_volleyball", 5.0f);
            Child(court, "VolleyballCenterTrigger", Vector3.zero).gameObject
                .AddComponent<InteractableTrigger>()
                .Configure(InteractableKind.SportsArena, "hamidiye_volleyball_center", "ui.btn.play_volleyball", 6.6f);
        }

        private static Vector3 pitchPosWorld(Transform t, Vector3 localPos)
        {
            return t != null ? t.TransformPoint(localPos) : localPos;
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
            Material shadowMat = MaterialFor(shader, "Sport Ball Shadow", Color.Lerp(groundColor, Color.black, 0.42f), 0f);
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
            // Face South (-Z) toward the court & stadium camera so the live scoreboard reads cleanly left-to-right!
            sign.localRotation = Quaternion.identity;
            MeshParts parts = new MeshParts(this);
            parts.Box(backing, Vector3.zero, new Vector3(2.35f, 0.62f, 0.08f));
            parts.Box(backing, new Vector3(0f, -position.y * 0.5f, 0f), new Vector3(0.095f, position.y, 0.095f));
            parts.Emit(sign, "Scoreboard panel", shadows: false);

            Transform letters = Child(sign, "ScoreboardText", new Vector3(0f, 0f, -0.048f));
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
