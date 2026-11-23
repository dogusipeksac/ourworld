using Gridroam.Core;
using Gridroam.Player;
using UnityEngine;

namespace Gridroam.World
{
    /// <summary>
    /// Autonomous 3v3 Halı Saha football (soccer) match: 3 Red vs 3 Blue NPCs (1 GK + 2 outfield per team)
    /// who dynamically sprint, dribble, pass, shoot on goal, dive/defend, celebrate goals, and update
    /// the live 3D scoreboard. Audio is strictly 3D spatial and muted beyond 22 m.
    /// </summary>
    public sealed class LiveFootballMatch : MonoBehaviour
    {
        private const float HalfW = 7.6f;
        private const float HalfL = 11.4f;
        private const float GoalHalfW = 2.05f;
        private const float BallRadius = 0.13f;

        private ProceduralHumanoid[] _red;
        private ProceduralHumanoid[] _blue;
        private Transform _ball;
        private Transform _ballShadow;
        private TextMesh _scoreboard;
        private AudioSource _audio;
        private AudioClip _kickClip;

        private Vector3 _ballPos;
        private Vector3 _ballVel;
        private int _carrierTeam = 0; // 0 = red, 1 = blue, -1 = loose/in flight
        private int _carrierIdx = 1;
        private float _actionTimer = 1.2f;
        private float _goalResetTimer;
        private int _redScore;
        private int _blueScore;
        private float _dribbleStep;

        private Vector3[] _redHome;
        private Vector3[] _blueHome;

        public void Configure(
            ProceduralHumanoid[] redTeam,
            ProceduralHumanoid[] blueTeam,
            Transform ball,
            Transform shadow,
            TextMesh scoreboard,
            AudioSource audio,
            AudioClip kickClip)
        {
            _red = redTeam;
            _blue = blueTeam;
            _ball = ball;
            _ballShadow = shadow;
            _scoreboard = scoreboard;
            _audio = audio;
            _kickClip = kickClip;

            _redHome = new[]
            {
                new Vector3(0f, 0f, -HalfL + 0.9f),
                new Vector3(-3.0f, 0f, -3.2f),
                new Vector3(3.0f, 0f, -1.8f)
            };
            _blueHome = new[]
            {
                new Vector3(0f, 0f, HalfL - 0.9f),
                new Vector3(-3.0f, 0f, 1.8f),
                new Vector3(3.0f, 0f, 3.2f)
            };

            ResetKickoff(0);
            UpdateScoreboard();
        }

        private void ResetKickoff(int team)
        {
            _ballPos = new Vector3(0f, BallRadius, 0f);
            _ballVel = Vector3.zero;
            _carrierTeam = team;
            _carrierIdx = 1;
            _actionTimer = 0.9f;

            for (int i = 0; i < 3; i++)
            {
                if (_red != null && i < _red.Length && _red[i] != null)
                {
                    _red[i].transform.localPosition = _redHome[i];
                    _red[i].transform.localRotation = Quaternion.identity;
                }
                if (_blue != null && i < _blue.Length && _blue[i] != null)
                {
                    _blue[i].transform.localPosition = _blueHome[i];
                    _blue[i].transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                }
            }
        }

        private void Update()
        {
            if (_red == null || _blue == null || _ball == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            if (_goalResetTimer > 0f)
            {
                _goalResetTimer -= dt;
                IdleTeam(_red, dt);
                IdleTeam(_blue, dt);
                if (_goalResetTimer <= 0f)
                {
                    ResetKickoff((_redScore + _blueScore) % 2);
                }
                UpdateBallVisual(dt);
                return;
            }

            if (_carrierTeam >= 0)
            {
                ProceduralHumanoid carrier = (_carrierTeam == 0 ? _red : _blue)[_carrierIdx];
                float attackDir = _carrierTeam == 0 ? 1f : -1f;
                Vector3 goalTarget = new Vector3(Mathf.Sin(Time.time * 1.1f) * 1.4f, 0f, attackDir * (HalfL - 0.5f));
                Vector3 toGoal = goalTarget - carrier.transform.localPosition;
                toGoal.y = 0f;
                Vector3 runDir = toGoal.sqrMagnitude > 0.01f ? toGoal.normalized : new Vector3(0f, 0f, attackDir);
                // Gentle lateral weave while dribbling
                runDir = (runDir + new Vector3(Mathf.Sin(Time.time * 2.2f + _carrierIdx) * 0.45f, 0f, 0f)).normalized;

                MovePlayer(carrier, carrier.transform.localPosition + runDir * 2.8f, 3.4f, dt);
                _dribbleStep += dt * 7f;
                _ballPos = carrier.transform.localPosition + carrier.transform.localRotation * Vector3.forward * (0.52f + Mathf.Abs(Mathf.Sin(_dribbleStep)) * 0.22f);
                _ballPos.y = BallRadius;

                _actionTimer -= dt;
                float distToGoalZ = Mathf.Abs(attackDir * HalfL - carrier.transform.localPosition.z);

                if (_actionTimer <= 0f || distToGoalZ < 5.6f)
                {
                    bool shoot = distToGoalZ < 7.8f || Random.value < 0.42f;
                    carrier.PlayCombatMove(2); // Kick swing
                    PlayKickSound(shoot ? 0.55f : 0.36f);

                    if (shoot)
                    {
                        float aimX = Random.Range(-GoalHalfW + 0.35f, GoalHalfW - 0.35f);
                        Vector3 shotTarget = new Vector3(aimX, 0.9f, attackDir * (HalfL + 0.6f));
                        Vector3 delta = shotTarget - _ballPos;
                        float flightTime = Mathf.Clamp(delta.magnitude / 12.5f, 0.38f, 0.85f);
                        _ballVel = new Vector3(delta.x / flightTime, Random.Range(2.4f, 4.4f), delta.z / flightTime);
                    }
                    else
                    {
                        // Pass to other outfield teammate
                        int mateIdx = _carrierIdx == 1 ? 2 : 1;
                        ProceduralHumanoid mate = (_carrierTeam == 0 ? _red : _blue)[mateIdx];
                        Vector3 lead = mate.transform.localPosition + new Vector3(0f, 0f, attackDir * 2.2f);
                        lead.x = Mathf.Clamp(lead.x, -HalfW + 1.2f, HalfW - 1.2f);
                        lead.z = Mathf.Clamp(lead.z, -HalfL + 1.8f, HalfL - 1.8f);
                        Vector3 delta = lead - _ballPos;
                        float passTime = Mathf.Clamp(delta.magnitude / 9.2f, 0.35f, 0.85f);
                        _ballVel = new Vector3(delta.x / passTime, 1.8f, delta.z / passTime);
                    }

                    _carrierTeam = -1;
                    _actionTimer = 0.25f;
                }
            }
            else
            {
                // Ball in flight / rolling
                _ballVel.y -= 11.8f * dt;
                _ballPos += _ballVel * dt;
                if (_ballPos.y < BallRadius)
                {
                    _ballPos.y = BallRadius;
                    if (Mathf.Abs(_ballVel.y) > 1.4f)
                    {
                        _ballVel.y = -_ballVel.y * 0.48f;
                        PlayKickSound(0.22f);
                    }
                    else
                    {
                        _ballVel.y = 0f;
                    }
                    _ballVel.x *= Mathf.Exp(-1.8f * dt);
                    _ballVel.z *= Mathf.Exp(-1.8f * dt);
                }

                // Check goals at Z = ±HalfL
                if (Mathf.Abs(_ballPos.z) >= HalfL + 0.12f && Mathf.Abs(_ballPos.x) <= GoalHalfW)
                {
                    bool redScored = _ballPos.z > 0f;
                    if (redScored) _redScore++; else _blueScore++;
                    UpdateScoreboard();
                    _goalResetTimer = 2.4f;
                    _ballVel *= 0.2f;

                    ProceduralHumanoid[] winners = redScored ? _red : _blue;
                    for (int i = 0; i < winners.Length; i++) winners[i].PlayWave();
                    if (IsPlayerNear(26f))
                    {
                        GameFeelAudio.SpawnBurst(
                            transform.TransformPoint(_ballPos + Vector3.up * 0.8f),
                            redScored ? new Color(0.95f, 0.25f, 0.20f) : new Color(0.22f, 0.56f, 0.98f),
                            count: 14, spread: 0.7f);
                    }
                    UpdateBallVisual(dt);
                    return;
                }

                // Bounce off sideline/endline boards
                if (Mathf.Abs(_ballPos.x) > HalfW - BallRadius)
                {
                    _ballPos.x = Mathf.Sign(_ballPos.x) * (HalfW - BallRadius);
                    _ballVel.x = -_ballVel.x * 0.65f;
                }
                if (Mathf.Abs(_ballPos.z) > HalfL - BallRadius)
                {
                    _ballPos.z = Mathf.Sign(_ballPos.z) * (HalfL - BallRadius);
                    _ballVel.z = -_ballVel.z * 0.65f;
                }

                _actionTimer -= dt;
                if (_actionTimer <= 0f && _ballPos.y < 0.75f)
                {
                    TryClaimBall();
                }
            }

            UpdateAllPlayers(dt);
            UpdateBallVisual(dt);
        }

        private void TryClaimBall()
        {
            float bestDist = 1.15f;
            int bestTeam = -1;
            int bestIdx = -1;

            for (int t = 0; t < 2; t++)
            {
                ProceduralHumanoid[] team = t == 0 ? _red : _blue;
                for (int i = 0; i < team.Length; i++)
                {
                    Vector3 d = team[i].transform.localPosition - _ballPos;
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTeam = t;
                        bestIdx = i;
                    }
                }
            }

            if (bestTeam >= 0)
            {
                if (bestIdx == 0)
                {
                    // Goalkeeper clears ball out immediately to an outfield teammate
                    ProceduralHumanoid gk = (bestTeam == 0 ? _red : _blue)[0];
                    gk.PlayCombatMove(2);
                    PlayKickSound(0.45f);
                    float dir = bestTeam == 0 ? 1f : -1f;
                    _ballVel = new Vector3(Random.Range(-3.2f, 3.2f), 3.6f, dir * 8.5f);
                    _actionTimer = 0.45f;
                }
                else
                {
                    _carrierTeam = bestTeam;
                    _carrierIdx = bestIdx;
                    _actionTimer = Random.Range(1.0f, 1.9f);
                }
            }
        }

        private void UpdateAllPlayers(float dt)
        {
            // Goalkeepers track ball X along their goal line
            UpdateGoalkeeper(_red[0], -HalfL + 0.85f, dt);
            UpdateGoalkeeper(_blue[0], HalfL - 0.85f, dt);

            for (int t = 0; t < 2; t++)
            {
                ProceduralHumanoid[] team = t == 0 ? _red : _blue;
                Vector3[] homes = t == 0 ? _redHome : _blueHome;
                float attackDir = t == 0 ? 1f : -1f;

                for (int i = 1; i < team.Length; i++)
                {
                    if (_carrierTeam == t && _carrierIdx == i) continue;

                    Vector3 target;
                    if (_carrierTeam == -1)
                    {
                        // Nearest outfielder chases loose ball, other supports
                        int other = i == 1 ? 2 : 1;
                        float myDist = (team[i].transform.localPosition - _ballPos).sqrMagnitude;
                        float otherDist = (team[other].transform.localPosition - _ballPos).sqrMagnitude;
                        target = myDist <= otherDist
                            ? new Vector3(_ballPos.x, 0f, _ballPos.z)
                            : new Vector3(homes[i].x, 0f, Mathf.Clamp(_ballPos.z + attackDir * 2.0f, -HalfL + 2f, HalfL - 2f));
                    }
                    else if (_carrierTeam == t)
                    {
                        // Support run ahead on opposite flank
                        float flank = i == 1 ? -3.6f : 3.6f;
                        target = new Vector3(flank, 0f, Mathf.Clamp(_ballPos.z + attackDir * 3.8f, -HalfL + 2.2f, HalfL - 2.2f));
                    }
                    else
                    {
                        // Defend: closest defender presses ball carrier, other covers goal side
                        int other = i == 1 ? 2 : 1;
                        float myDist = (team[i].transform.localPosition - _ballPos).sqrMagnitude;
                        float otherDist = (team[other].transform.localPosition - _ballPos).sqrMagnitude;
                        target = myDist <= otherDist
                            ? new Vector3(_ballPos.x, 0f, _ballPos.z - attackDir * 0.9f)
                            : new Vector3(homes[i].x * 0.8f, 0f, Mathf.Clamp(_ballPos.z - attackDir * 3.5f, -HalfL + 2.5f, HalfL - 2.5f));
                    }

                    target.x = Mathf.Clamp(target.x, -HalfW + 0.9f, HalfW - 0.9f);
                    target.z = Mathf.Clamp(target.z, -HalfL + 1.4f, HalfL - 1.4f);
                    MovePlayer(team[i], target, 3.8f, dt);
                }
            }
        }

        private void UpdateGoalkeeper(ProceduralHumanoid gk, float lineZ, float dt)
        {
            Vector3 target = new Vector3(Mathf.Clamp(_ballPos.x * 0.55f, -GoalHalfW + 0.45f, GoalHalfW - 0.45f), 0f, lineZ);
            MovePlayer(gk, target, 3.2f, dt, facePoint: _ballPos);
        }

        private void MovePlayer(ProceduralHumanoid hum, Vector3 localTarget, float maxSpeed, float dt, Vector3? facePoint = null)
        {
            Vector3 pos = hum.transform.localPosition;
            Vector3 delta = localTarget - pos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float speed = 0f;
            Vector3 worldDir = hum.transform.forward;

            if (dist > 0.14f)
            {
                speed = Mathf.Min(maxSpeed, dist * 4.5f);
                Vector3 dir = delta / dist;
                pos += dir * (speed * dt);
                hum.transform.localPosition = pos;
                worldDir = transform.TransformDirection(dir);
                Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
                hum.transform.localRotation = Quaternion.Slerp(hum.transform.localRotation, rot, 1f - Mathf.Exp(-10f * dt));
            }
            else if (facePoint.HasValue)
            {
                Vector3 look = facePoint.Value - pos;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    hum.transform.localRotation = Quaternion.Slerp(
                        hum.transform.localRotation,
                        Quaternion.LookRotation(look.normalized, Vector3.up),
                        1f - Mathf.Exp(-8f * dt));
                }
            }

            hum.SetLocomotion(speed, true, 0f, worldDir, sampleGround: false);
        }

        private void IdleTeam(ProceduralHumanoid[] team, float dt)
        {
            for (int i = 0; i < team.Length; i++)
            {
                team[i].SetLocomotion(0f, true, 0f, team[i].transform.forward, sampleGround: false);
            }
        }

        private void UpdateBallVisual(float dt)
        {
            _ball.localPosition = _ballPos;
            _ball.Rotate(new Vector3(_ballVel.z, 0f, -_ballVel.x) * (dt * 120f), Space.Self);
            if (_ballShadow != null)
            {
                _ballShadow.localPosition = new Vector3(_ballPos.x, 0.024f, _ballPos.z);
                float s = Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(_ballPos.y / 3.5f));
                _ballShadow.localScale = Vector3.one * s;
            }
        }

        private void UpdateScoreboard()
        {
            if (_scoreboard != null)
            {
                _scoreboard.text = $"HAMİDİYE HALI SAHA\nKIRMIZI {_redScore} - {_blueScore} MAVİ";
            }
        }

        private void PlayKickSound(float vol)
        {
            if (_audio == null || _kickClip == null || !IsPlayerNear(22f)) return;
            _audio.pitch = Random.Range(0.92f, 1.08f);
            _audio.PlayOneShot(_kickClip, vol);
        }

        private bool IsPlayerNear(float range)
        {
            if (PlayerModeController.Instance == null) return false;
            return (PlayerModeController.Instance.ActiveWorldPosition - transform.position).sqrMagnitude <= range * range;
        }
    }

    /// <summary>
    /// Autonomous 2v2 streetball basketball match: ball-handler dribbles with synced right-hand bounce IK,
    /// passes, rises for a 2-hand overhead jump shot toward the 3.05 m orange rim, scores baskets or
    /// battles for rebounds, and updates the live 3D scoreboard.
    /// </summary>
    public sealed class LiveBasketballMatch : MonoBehaviour
    {
        private const float HalfW = 5.6f;
        private const float HalfL = 9.2f;
        private const float RimZ = 8.95f;
        private const float RimY = 3.05f;
        private const float BallRadius = 0.135f;

        private ProceduralHumanoid[] _gold;
        private ProceduralHumanoid[] _navy;
        private Transform _ball;
        private Transform _ballShadow;
        private TextMesh _scoreboard;
        private AudioSource _audio;
        private AudioClip _bounceClip;

        private int _offenseTeam; // 0 = gold (attacks +Z), 1 = navy (attacks -Z)
        private int _handlerIdx;
        private int _phase; // 0 = dribble/attack, 1 = pass flight, 2 = shot flight, 3 = celebrate/inbound
        private float _phaseTimer = 2.0f;
        private float _flightDuration = 0.8f;
        private float _flightElapsed;
        private Vector3 _flightStart;
        private Vector3 _flightEnd;
        private float _flightArcHeight = 2.0f;
        private bool _shotWillScore;
        private float _dribbleCycle;
        private int _lastBounceTick;
        private Vector3 _ballPos;
        private int _goldScore;
        private int _navyScore;

        public void Configure(
            ProceduralHumanoid[] goldTeam,
            ProceduralHumanoid[] navyTeam,
            Transform ball,
            Transform shadow,
            TextMesh scoreboard,
            AudioSource audio,
            AudioClip bounceClip)
        {
            _gold = goldTeam;
            _navy = navyTeam;
            _ball = ball;
            _ballShadow = shadow;
            _scoreboard = scoreboard;
            _audio = audio;
            _bounceClip = bounceClip;

            _gold[0].transform.localPosition = new Vector3(-2.2f, 0f, -2.5f);
            _gold[1].transform.localPosition = new Vector3(2.4f, 0f, 1.5f);
            _navy[0].transform.localPosition = new Vector3(-1.8f, 0f, 2.2f);
            _navy[1].transform.localPosition = new Vector3(2.1f, 0f, 4.5f);

            _offenseTeam = 0;
            _handlerIdx = 0;
            _phase = 0;
            _phaseTimer = 2.1f;
            UpdateScoreboard();
        }

        private void Update()
        {
            if (_gold == null || _navy == null || _ball == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            ProceduralHumanoid[] off = _offenseTeam == 0 ? _gold : _navy;
            ProceduralHumanoid[] def = _offenseTeam == 0 ? _navy : _gold;
            float dirZ = _offenseTeam == 0 ? 1f : -1f;
            Vector3 hoopPos = new Vector3(0f, RimY, dirZ * RimZ);
            ProceduralHumanoid handler = off[_handlerIdx];
            ProceduralHumanoid mate = off[1 - _handlerIdx];

            ClearHandPoses(_gold);
            ClearHandPoses(_navy);

            if (_phase == 0)
            {
                // Ball-handler dribbles toward shooting arc
                Vector3 driveSpot = new Vector3(
                    (_handlerIdx == 0 ? -2.2f : 2.2f) + Mathf.Sin(Time.time * 1.4f) * 1.1f,
                    0f,
                    dirZ * 4.4f);
                MoveCourtPlayer(handler, driveSpot, 3.0f, dt, hoopPos);

                // Teammate cuts to opposite wing
                Vector3 wingSpot = new Vector3(_handlerIdx == 0 ? 3.2f : -3.2f, 0f, dirZ * 5.8f);
                MoveCourtPlayer(mate, wingSpot, 3.2f, dt, handler.transform.localPosition);

                // Defenders guard between player & hoop
                MoveCourtPlayer(def[0], Vector3.Lerp(handler.transform.localPosition, new Vector3(0f, 0f, dirZ * RimZ), 0.34f), 3.1f, dt, handler.transform.localPosition);
                MoveCourtPlayer(def[1], Vector3.Lerp(mate.transform.localPosition, new Vector3(0f, 0f, dirZ * RimZ), 0.38f), 3.1f, dt, mate.transform.localPosition);

                // Synced rhythmic dribble bounce + right-hand IK push!
                _dribbleCycle += dt * 4.2f;
                float bounceSin = Mathf.Abs(Mathf.Sin(_dribbleCycle * Mathf.PI));
                int tick = Mathf.FloorToInt(_dribbleCycle);
                if (tick != _lastBounceTick)
                {
                    _lastBounceTick = tick;
                    PlayBounce(0.28f);
                }

                Vector3 dribbleBase = handler.transform.localPosition + handler.transform.localRotation * new Vector3(0.32f, 0f, 0.36f);
                _ballPos = new Vector3(dribbleBase.x, BallRadius + bounceSin * 0.88f, dribbleBase.z);
                Vector3 handWorld = transform.TransformPoint(new Vector3(dribbleBase.x, 0.92f + bounceSin * 0.16f, dribbleBase.z));
                handler.SetDualHandAction(true, Vector3.zero, handWorld, 0f, 0.92f, 6f, 8f, 0f);

                _phaseTimer -= dt;
                if (_phaseTimer <= 0f)
                {
                    if (Random.value < 0.42f)
                    {
                        // Chest pass to teammate
                        _phase = 1;
                        _flightStart = _ballPos;
                        _flightEnd = mate.transform.localPosition + Vector3.up * 1.15f;
                        _flightDuration = 0.48f;
                        _flightElapsed = 0f;
                        _flightArcHeight = 0.45f;
                        PlayBounce(0.32f);
                    }
                    else
                    {
                        // Jump shot toward orange rim!
                        _phase = 2;
                        _shotWillScore = Random.value < 0.64f;
                        _flightStart = handler.transform.localPosition + Vector3.up * 1.95f;
                        _flightEnd = _shotWillScore
                            ? hoopPos
                            : hoopPos + new Vector3(Random.Range(-0.45f, 0.45f), 0.1f, -dirZ * 0.35f);
                        _flightDuration = 0.95f;
                        _flightElapsed = 0f;
                        _flightArcHeight = 1.65f;
                        PlayBounce(0.35f);
                    }
                }
            }
            else if (_phase == 1)
            {
                // Pass in flight
                _flightElapsed += dt;
                float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
                _ballPos = ParabolicLerp(_flightStart, _flightEnd, u, _flightArcHeight);
                IdleAll(dt);
                if (u >= 1f)
                {
                    _handlerIdx = 1 - _handlerIdx;
                    _phase = 0;
                    _phaseTimer = Random.Range(1.2f, 2.0f);
                }
            }
            else if (_phase == 2)
            {
                // Jump shot arc! Shooter holds high follow-through arms
                _flightElapsed += dt;
                float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
                _ballPos = ParabolicLerp(_flightStart, _flightEnd, u, _flightArcHeight);

                if (u < 0.45f)
                {
                    Vector3 upL = handler.transform.position + handler.transform.forward * 0.25f - handler.transform.right * 0.12f + Vector3.up * 1.95f;
                    Vector3 upR = handler.transform.position + handler.transform.forward * 0.25f + handler.transform.right * 0.12f + Vector3.up * 1.95f;
                    handler.SetDualHandAction(true, upL, upR, 0.95f, 0.95f, -5f, -18f, 0f);
                }
                IdleAll(dt);

                if (u >= 1f)
                {
                    if (_shotWillScore)
                    {
                        if (_offenseTeam == 0) _goldScore += 2; else _navyScore += 2;
                        UpdateScoreboard();
                        handler.PlayWave();
                        PlayBounce(0.45f);
                        if (IsPlayerNear(24f))
                        {
                            GameFeelAudio.SpawnBurst(
                                transform.TransformPoint(hoopPos),
                                _offenseTeam == 0 ? new Color(1.0f, 0.78f, 0.18f) : new Color(0.28f, 0.56f, 0.96f),
                                count: 10, spread: 0.45f);
                        }
                    }
                    else
                    {
                        PlayBounce(0.50f);
                    }

                    // Drop ball from rim to rebounding team & switch possession
                    _phase = 3;
                    _phaseTimer = 1.05f;
                    _flightStart = _ballPos;
                    _offenseTeam = 1 - _offenseTeam;
                    _handlerIdx = Random.Range(0, 2);
                    ProceduralHumanoid rebounder = (_offenseTeam == 0 ? _gold : _navy)[_handlerIdx];
                    _flightEnd = rebounder.transform.localPosition + Vector3.up * 1.1f;
                    _flightElapsed = 0f;
                    _flightDuration = 0.75f;
                }
            }
            else
            {
                // Rebound / transition
                _flightElapsed += dt;
                float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
                _ballPos = ParabolicLerp(_flightStart, _flightEnd, u, 0.65f);
                IdleAll(dt);
                _phaseTimer -= dt;
                if (_phaseTimer <= 0f)
                {
                    _phase = 0;
                    _phaseTimer = Random.Range(1.8f, 2.8f);
                }
            }

            _ball.localPosition = _ballPos;
            _ball.Rotate(Vector3.right * (dt * 240f), Space.Self);
            if (_ballShadow != null)
            {
                _ballShadow.localPosition = new Vector3(_ballPos.x, 0.024f, _ballPos.z);
                float s = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(_ballPos.y / 4.2f));
                _ballShadow.localScale = Vector3.one * s;
            }
        }

        private void ClearHandPoses(ProceduralHumanoid[] team)
        {
            for (int i = 0; i < team.Length; i++)
            {
                team[i].SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
            }
        }

        private void IdleAll(float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                _gold[i].SetLocomotion(0f, true, 0f, _gold[i].transform.forward, sampleGround: false);
                _navy[i].SetLocomotion(0f, true, 0f, _navy[i].transform.forward, sampleGround: false);
            }
        }

        private void MoveCourtPlayer(ProceduralHumanoid hum, Vector3 localTarget, float maxSpeed, float dt, Vector3 faceTarget)
        {
            localTarget.x = Mathf.Clamp(localTarget.x, -HalfW + 0.8f, HalfW - 0.8f);
            localTarget.z = Mathf.Clamp(localTarget.z, -HalfL + 0.9f, HalfL - 0.9f);
            Vector3 pos = hum.transform.localPosition;
            Vector3 delta = localTarget - pos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float spd = 0f;
            Vector3 wDir = hum.transform.forward;

            if (dist > 0.14f)
            {
                spd = Mathf.Min(maxSpeed, dist * 4.2f);
                Vector3 dir = delta / dist;
                pos += dir * (spd * dt);
                hum.transform.localPosition = pos;
                wDir = transform.TransformDirection(dir);
            }

            Vector3 look = faceTarget - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                hum.transform.localRotation = Quaternion.Slerp(
                    hum.transform.localRotation,
                    Quaternion.LookRotation(look.normalized, Vector3.up),
                    1f - Mathf.Exp(-10f * dt));
            }
            hum.SetLocomotion(spd, true, 0f, wDir, sampleGround: false);
        }

        private static Vector3 ParabolicLerp(Vector3 a, Vector3 b, float u, float extraArc)
        {
            Vector3 p = Vector3.Lerp(a, b, u);
            p.y += 4f * extraArc * u * (1f - u);
            return p;
        }

        private void UpdateScoreboard()
        {
            if (_scoreboard != null)
            {
                _scoreboard.text = $"HAMİDİYE BASKETBOL\nSARI {_goldScore} - {_navyScore} LACİVERT";
            }
        }

        private void PlayBounce(float vol)
        {
            if (_audio == null || _bounceClip == null || !IsPlayerNear(22f)) return;
            _audio.pitch = Random.Range(0.92f, 1.08f);
            _audio.PlayOneShot(_bounceClip, vol);
        }

        private bool IsPlayerNear(float range)
        {
            if (PlayerModeController.Instance == null) return false;
            return (PlayerModeController.Instance.ActiveWorldPosition - transform.position).sqrMagnitude <= range * range;
        }
    }

    /// <summary>
    /// Autonomous 2v2 Volleyball rally: baseline serve -> back-row dual-arm bump -> set -> overhead spike
    /// over the 2.43 m net with live scoreboard and celebrations.
    /// </summary>
    public sealed class LiveVolleyballMatch : MonoBehaviour
    {
        private const float HalfW = 4.2f;
        private const float HalfL = 7.6f;

        private ProceduralHumanoid[] _teal;
        private ProceduralHumanoid[] _burgundy;
        private Transform _ball;
        private Transform _ballShadow;
        private TextMesh _scoreboard;
        private AudioSource _audio;
        private AudioClip _hitClip;

        private int _targetTeam = 1;
        private int _targetIdx = 1;
        private int _touchStep; // 0 = receive/bump, 1 = set to front, 2 = spike over net
        private int _rallyCount;
        private float _flightElapsed;
        private float _flightDuration = 1.15f;
        private Vector3 _flightStart;
        private Vector3 _flightEnd;
        private float _flightArc = 2.6f;
        private float _pauseTimer;
        private float _hitAnimTimer;
        private ProceduralHumanoid _lastHitter;
        private bool _lastHitWasSpike;
        private int _tealScore;
        private int _burgundyScore;

        public void Configure(
            ProceduralHumanoid[] tealTeam,
            ProceduralHumanoid[] burgundyTeam,
            Transform ball,
            Transform shadow,
            TextMesh scoreboard,
            AudioSource audio,
            AudioClip hitClip)
        {
            _teal = tealTeam;
            _burgundy = burgundyTeam;
            _ball = ball;
            _ballShadow = shadow;
            _scoreboard = scoreboard;
            _audio = audio;
            _hitClip = hitClip;

            StartServe(0);
            UpdateScoreboard();
        }

        private void StartServe(int servingTeam)
        {
            _teal[0].transform.localPosition = new Vector3(-1.6f, 0f, -2.4f);
            _teal[1].transform.localPosition = new Vector3(1.6f, 0f, -5.6f);
            _burgundy[0].transform.localPosition = new Vector3(1.6f, 0f, 2.4f);
            _burgundy[1].transform.localPosition = new Vector3(-1.6f, 0f, 5.6f);

            ProceduralHumanoid server = (servingTeam == 0 ? _teal : _burgundy)[1];
            _targetTeam = 1 - servingTeam;
            _targetIdx = 1;
            _touchStep = 0;
            _rallyCount = 0;

            float recvZ = _targetTeam == 0 ? Random.Range(-6.2f, -4.2f) : Random.Range(4.2f, 6.2f);
            _flightStart = server.transform.localPosition + Vector3.up * 1.85f;
            _flightEnd = new Vector3(Random.Range(-2.6f, 2.6f), 1.05f, recvZ);
            _flightDuration = 1.25f;
            _flightElapsed = 0f;
            _flightArc = 2.55f;
            _lastHitter = server;
            _lastHitWasSpike = true;
            _hitAnimTimer = 0.40f;
            PlayHit(0.45f);
        }

        private void Update()
        {
            if (_teal == null || _burgundy == null || _ball == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            ClearPoses(_teal);
            ClearPoses(_burgundy);

            if (_pauseTimer > 0f)
            {
                _pauseTimer -= dt;
                IdleTeam(_teal);
                IdleTeam(_burgundy);
                if (_pauseTimer <= 0f)
                {
                    StartServe((_tealScore + _burgundyScore) % 2);
                }
                return;
            }

            _flightElapsed += dt;
            float u = Mathf.Clamp01(_flightElapsed / _flightDuration);
            Vector3 ballPos = Vector3.Lerp(_flightStart, _flightEnd, u);
            ballPos.y += 4f * _flightArc * u * (1f - u);

            // Move receiving player smoothly to exact landing spot while other 3 hold ready formation
            ProceduralHumanoid[] recvTeam = _targetTeam == 0 ? _teal : _burgundy;
            ProceduralHumanoid[] otherTeam = _targetTeam == 0 ? _burgundy : _teal;

            Vector3 catchPos = new Vector3(_flightEnd.x, 0f, _flightEnd.z);
            MoveVolleyPlayer(recvTeam[_targetIdx], catchPos, 4.4f, dt, _targetTeam == 0 ? 1f : -1f);
            MoveVolleyPlayer(recvTeam[1 - _targetIdx], GetReadyPos(_targetTeam, 1 - _targetIdx), 2.8f, dt, _targetTeam == 0 ? 1f : -1f);
            MoveVolleyPlayer(otherTeam[0], GetReadyPos(1 - _targetTeam, 0), 2.8f, dt, _targetTeam == 0 ? -1f : 1f);
            MoveVolleyPlayer(otherTeam[1], GetReadyPos(1 - _targetTeam, 1), 2.8f, dt, _targetTeam == 0 ? -1f : 1f);

            // Apply expressive bump or overhead spike pose on active hitter
            if (_hitAnimTimer > 0f && _lastHitter != null)
            {
                _hitAnimTimer -= dt;
                Transform ht = _lastHitter.transform;
                if (_lastHitWasSpike)
                {
                    Vector3 upR = ht.position + ht.forward * 0.25f + ht.right * 0.14f + Vector3.up * 2.02f;
                    Vector3 upL = ht.position + ht.forward * 0.20f - ht.right * 0.22f + Vector3.up * 1.45f;
                    _lastHitter.SetDualHandAction(true, upL, upR, 0.85f, 0.96f, -6f, -15f, 0f);
                }
                else
                {
                    Vector3 bumpL = ht.position + ht.forward * 0.44f - ht.right * 0.05f + Vector3.up * 1.08f;
                    Vector3 bumpR = ht.position + ht.forward * 0.44f + ht.right * 0.05f + Vector3.up * 1.08f;
                    _lastHitter.SetDualHandAction(true, bumpL, bumpR, 0.95f, 0.95f, 16f, 10f, 0.06f);
                }
            }

            if (u >= 1f)
            {
                ProceduralHumanoid hitter = recvTeam[_targetIdx];
                _lastHitter = hitter;
                _hitAnimTimer = 0.42f;
                PlayHit(_touchStep == 2 ? 0.52f : 0.34f);

                if (_touchStep == 0)
                {
                    // Bump/dig to front-row setter/spiker
                    _lastHitWasSpike = false;
                    _touchStep = 1;
                    _targetIdx = 0;
                    float frontZ = _targetTeam == 0 ? -1.8f : 1.8f;
                    _flightStart = ballPos;
                    _flightEnd = new Vector3(Random.Range(-2.2f, 2.2f), 2.05f, frontZ);
                    _flightDuration = 0.85f;
                    _flightElapsed = 0f;
                    _flightArc = 1.65f;
                }
                else
                {
                    // Spike over the 2.43 m net to opponent side!
                    _lastHitWasSpike = true;
                    _rallyCount++;
                    bool pointOver = _rallyCount >= 4 && Random.value < 0.42f;

                    if (pointOver)
                    {
                        if (_targetTeam == 0) _tealScore++; else _burgundyScore++;
                        UpdateScoreboard();
                        for (int i = 0; i < recvTeam.Length; i++) recvTeam[i].PlayWave();
                        if (IsPlayerNear(24f))
                        {
                            GameFeelAudio.SpawnBurst(
                                transform.TransformPoint(ballPos + Vector3.up * 0.4f),
                                _targetTeam == 0 ? new Color(0.18f, 0.82f, 0.78f) : new Color(0.78f, 0.22f, 0.34f),
                                count: 10, spread: 0.45f);
                        }
                        _pauseTimer = 1.9f;
                    }
                    else
                    {
                        _targetTeam = 1 - _targetTeam;
                        _targetIdx = 1;
                        _touchStep = 0;
                        float oppZ = _targetTeam == 0 ? Random.Range(-6.2f, -3.5f) : Random.Range(3.5f, 6.2f);
                        _flightStart = ballPos;
                        _flightEnd = new Vector3(Random.Range(-2.8f, 2.8f), 1.05f, oppZ);
                        _flightDuration = 1.10f;
                        _flightElapsed = 0f;
                        _flightArc = 2.05f;
                    }
                }
            }

            _ball.localPosition = ballPos;
            _ball.Rotate(Vector3.one * (dt * 180f), Space.Self);
            if (_ballShadow != null)
            {
                _ballShadow.localPosition = new Vector3(ballPos.x, 0.024f, ballPos.z);
                float s = Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(ballPos.y / 4.5f));
                _ballShadow.localScale = Vector3.one * s;
            }
        }

        private static Vector3 GetReadyPos(int team, int idx)
        {
            float zSign = team == 0 ? -1f : 1f;
            return idx == 0
                ? new Vector3(team == 0 ? -1.4f : 1.4f, 0f, zSign * 2.2f)
                : new Vector3(team == 0 ? 1.5f : -1.5f, 0f, zSign * 5.2f);
        }

        private void MoveVolleyPlayer(ProceduralHumanoid hum, Vector3 localTarget, float maxSpeed, float dt, float netDirZ)
        {
            localTarget.x = Mathf.Clamp(localTarget.x, -HalfW + 0.5f, HalfW - 0.5f);
            localTarget.z = Mathf.Clamp(localTarget.z, -HalfL + 0.6f, HalfL - 0.6f);
            Vector3 pos = hum.transform.localPosition;
            Vector3 delta = localTarget - pos;
            delta.y = 0f;
            float dist = delta.magnitude;
            float spd = 0f;
            Vector3 wDir = hum.transform.forward;

            if (dist > 0.14f)
            {
                spd = Mathf.Min(maxSpeed, dist * 4.6f);
                Vector3 dir = delta / dist;
                pos += dir * (spd * dt);
                hum.transform.localPosition = pos;
                wDir = transform.TransformDirection(dir);
            }

            Quaternion faceNet = Quaternion.LookRotation(new Vector3(0f, 0f, netDirZ), Vector3.up);
            hum.transform.localRotation = Quaternion.Slerp(hum.transform.localRotation, faceNet, 1f - Mathf.Exp(-9f * dt));
            hum.SetLocomotion(spd, true, 0f, wDir, sampleGround: false);
        }

        private void ClearPoses(ProceduralHumanoid[] team)
        {
            for (int i = 0; i < team.Length; i++)
            {
                team[i].SetDualHandAction(false, Vector3.zero, Vector3.zero, 0f, 0f, 0f, 0f, 0f);
            }
        }

        private void IdleTeam(ProceduralHumanoid[] team)
        {
            for (int i = 0; i < team.Length; i++)
            {
                team[i].SetLocomotion(0f, true, 0f, team[i].transform.forward, sampleGround: false);
            }
        }

        private void UpdateScoreboard()
        {
            if (_scoreboard != null)
            {
                _scoreboard.text = $"HAMİDİYE VOLEYBOL\nTURKUAZ {_tealScore} - {_burgundyScore} BORDO";
            }
        }

        private void PlayHit(float vol)
        {
            if (_audio == null || _hitClip == null || !IsPlayerNear(22f)) return;
            _audio.pitch = Random.Range(0.94f, 1.08f);
            _audio.PlayOneShot(_hitClip, vol);
        }

        private bool IsPlayerNear(float range)
        {
            if (PlayerModeController.Instance == null) return false;
            return (PlayerModeController.Instance.ActiveWorldPosition - transform.position).sqrMagnitude <= range * range;
        }
    }
}
