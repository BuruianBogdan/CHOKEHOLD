using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ChokeHold.Gameplay
{
    public enum RoundPhase
    {
        PreRound,     // freeze time: jucatorii sunt la spawn, nu se pot misca
        Live,         // runda in desfasurare, bomba neplantata
        BombPlanted,  // bomba plantata, ruleaza timerul bombei
        RoundEnd,     // pauza scurta cu rezultatul rundei
        MatchEnd      // o echipa a castigat meciul
    }

    public enum RoundEndReason
    {
        AttackersEliminated,
        DefendersEliminated,
        TimeExpired,
        BombExploded,
        BombDefused
    }

    /// <summary>
    /// Bucla de runda: freeze time -> runda -> (bomba plantata) -> sfarsit de runda -> runda urmatoare.
    ///
    /// Reguli de castig:
    ///  - Atacatorii castiga daca bomba explodeaza sau toti aparatorii sunt eliminati.
    ///  - Aparatorii castiga daca expira timpul fara plant, daca dezamorseaza bomba,
    ///    sau daca elimina toti atacatorii INAINTE de plant.
    /// </summary>
    public class RoundManager : MonoBehaviour
    {
        [Header("Timpi (secunde)")]
        [Tooltip("Faza de pregatire cand jucatorul e aparator: intareste pereti, monteaza baricade. Atacatorii asteapta.")]
        [SerializeField] private float prepTime = 30f;

        [Tooltip("Pauza scurta la inceput de runda cand jucatorul e atacator (nu are ce pregati).")]
        [SerializeField] private float preRoundTime = 5f;
        [SerializeField] private float roundTime = 100f;
        [SerializeField] private float bombTime = 40f;
        [SerializeField] private float roundEndTime = 5f;

        [Header("Meci")]
        [SerializeField] private int roundsToWin = 7;
        [SerializeField] private Team playerTeam = Team.Attackers;

        [Header("References")]
        [SerializeField] private BombController bomb;
        [SerializeField] private DefenderPrepController prep;

        private bool matchIsOver;

        public static RoundManager Instance { get; private set; }

        public RoundPhase Phase { get; private set; }
        public float PhaseTimeRemaining { get; private set; }
        public int RoundNumber { get; private set; }
        public int AttackerScore { get; private set; }
        public int DefenderScore { get; private set; }
        public int RoundsToWin => roundsToWin;
        public float RoundTime => roundTime;

        /// <summary>Secundele trecute din runda propriu-zisa (0 in pregatire); timpul de cumparare se masoara de aici.</summary>
        public float LiveElapsed => Phase == RoundPhase.Live ? roundTime - PhaseTimeRemaining : 0f;
        public Team LastWinner { get; private set; }
        public RoundEndReason LastEndReason { get; private set; }
        public TeamMember LocalPlayer { get; private set; }
        public BombController Bomb => bomb;
        public DefenderPrepController Prep => prep;

        public event Action<RoundPhase> PhaseChanged;
        public event Action<Team, RoundEndReason> RoundEnded;

        private void Awake()
        {
            Instance = this;

            if (bomb == null)
            {
                bomb = GetComponent<BombController>();
            }

            if (prep == null)
            {
                prep = GetComponent<DefenderPrepController>();
            }
        }

        private void OnEnable()
        {
            TeamMember.AnyDied += OnMemberDied;
        }

        private void OnDisable()
        {
            TeamMember.AnyDied -= OnMemberDied;

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Start()
        {
            foreach (TeamMember member in TeamMember.All)
            {
                if (member.IsLocalPlayer)
                {
                    LocalPlayer = member;
                    member.SetTeam(playerTeam);
                    break;
                }
            }

            StartRound();
        }

        private void Update()
        {
            if (Phase == RoundPhase.MatchEnd)
            {
                if (Keyboard.current != null &&
                    Keyboard.current.enterKey.wasPressedThisFrame)
                {
                    RestartMatch();
                }

                return;
            }

            PhaseTimeRemaining -= Time.deltaTime;

            if (PhaseTimeRemaining > 0f)
            {
                return;
            }

            PhaseTimeRemaining = 0f;

            switch (Phase)
            {
                case RoundPhase.PreRound:
                    SetFreeze(false);
                    SetPhase(RoundPhase.Live, roundTime);
                    break;

                case RoundPhase.Live:
                    EndRound(Team.Defenders, RoundEndReason.TimeExpired);
                    break;

                case RoundPhase.BombPlanted:
                    if (bomb != null)
                    {
                        bomb.Explode();
                    }

                    EndRound(Team.Attackers, RoundEndReason.BombExploded);
                    break;

                case RoundPhase.RoundEnd:
                    if (matchIsOver)
                    {
                        SetPhase(RoundPhase.MatchEnd, 0f);
                    }
                    else
                    {
                        StartRound();
                    }

                    break;
            }
        }

        // ------------------------------------------------------------------
        // Apelate de BombController
        // ------------------------------------------------------------------
        public void NotifyBombPlanted()
        {
            if (Phase != RoundPhase.Live)
            {
                return;
            }

            SetPhase(RoundPhase.BombPlanted, bombTime);
        }

        public void NotifyBombDefused()
        {
            if (Phase != RoundPhase.BombPlanted)
            {
                return;
            }

            EndRound(Team.Defenders, RoundEndReason.BombDefused);
        }

        // ------------------------------------------------------------------
        // Runda
        // ------------------------------------------------------------------
        private void StartRound()
        {
            RoundNumber++;

            // Harta revine la starea initiala: pereti sparti refacuti, intariri si baricade scoase.
            Destructible.ResetAll();
            BarricadePoint.ResetAll();

            RespawnEveryone();
            SetFreeze(true);

            if (bomb != null)
            {
                bomb.ResetForRound(ChooseBombCarrier());
            }

            if (prep != null)
            {
                prep.ResetForRound();
            }

            // Pregatirea lunga are rost doar cand un jucator apara. Cu multiplayer va fi mereu prepTime.
            bool playerDefends =
                LocalPlayer != null &&
                LocalPlayer.Team == Team.Defenders;

            SetPhase(RoundPhase.PreRound, playerDefends ? prepTime : preRoundTime);
        }

        private void RestartMatch()
        {
            AttackerScore = 0;
            DefenderScore = 0;
            RoundNumber = 0;
            matchIsOver = false;

            StartRound();
        }

        private void EndRound(Team winner, RoundEndReason reason)
        {
            if (Phase == RoundPhase.RoundEnd || Phase == RoundPhase.MatchEnd)
            {
                return;
            }

            LastWinner = winner;
            LastEndReason = reason;

            if (winner == Team.Attackers)
            {
                AttackerScore++;
            }
            else
            {
                DefenderScore++;
            }

            matchIsOver =
                AttackerScore >= roundsToWin ||
                DefenderScore >= roundsToWin;

            SetPhase(RoundPhase.RoundEnd, roundEndTime);
            RoundEnded?.Invoke(winner, reason);

            Debug.Log(
                $"Runda {RoundNumber}: castiga {winner} ({reason}). " +
                $"Scor {AttackerScore} - {DefenderScore}"
            );
        }

        private void SetPhase(RoundPhase phase, float duration)
        {
            Phase = phase;
            PhaseTimeRemaining = duration;
            PhaseChanged?.Invoke(phase);
        }

        private void OnMemberDied(TeamMember _)
        {
            if (Phase != RoundPhase.Live && Phase != RoundPhase.BombPlanted)
            {
                return;
            }

            CountTeam(Team.Attackers, out int attackersTotal, out int attackersAlive);
            CountTeam(Team.Defenders, out int defendersTotal, out int defendersAlive);

            // O echipa fara niciun membru in scena (ex. testezi singur) nu conteaza ca "eliminata".
            if (defendersTotal > 0 && defendersAlive == 0)
            {
                EndRound(Team.Attackers, RoundEndReason.DefendersEliminated);
                return;
            }

            // Dupa plant, aparatorii trebuie sa si dezamorseze - eliminarea atacatorilor nu ajunge.
            if (Phase == RoundPhase.Live &&
                attackersTotal > 0 && attackersAlive == 0)
            {
                EndRound(Team.Defenders, RoundEndReason.AttackersEliminated);
            }
        }

        private static void CountTeam(Team team, out int total, out int alive)
        {
            total = 0;
            alive = 0;

            foreach (TeamMember member in TeamMember.All)
            {
                if (member.Team != team)
                {
                    continue;
                }

                total++;

                if (member.IsAlive)
                {
                    alive++;
                }
            }
        }

        // ------------------------------------------------------------------
        // Spawn
        // ------------------------------------------------------------------
        private void RespawnEveryone()
        {
            SpawnPoint[] spawnPoints =
                FindObjectsByType<SpawnPoint>();

            Dictionary<Team, int> nextSpawnIndex = new Dictionary<Team, int>
            {
                { Team.Attackers, 0 },
                { Team.Defenders, 0 }
            };

            // Copie: Respawn nu modifica lista, dar e mai sigur sa nu iteram direct pe ea.
            List<TeamMember> members = new List<TeamMember>(TeamMember.All);

            foreach (TeamMember member in members)
            {
                if (!member.RespawnsAtSpawnPoint)
                {
                    member.RespawnAtStart();
                    continue;
                }

                SpawnPoint spawnPoint = FindSpawnPoint(
                    spawnPoints,
                    member.Team,
                    nextSpawnIndex[member.Team]
                );

                nextSpawnIndex[member.Team]++;

                if (spawnPoint != null)
                {
                    member.Respawn(
                        spawnPoint.transform.position,
                        spawnPoint.transform.rotation
                    );
                }
                else
                {
                    Debug.LogWarning(
                        $"Nu exista SpawnPoint pentru echipa {member.Team}. " +
                        $"{member.name} ramane pe loc.",
                        member
                    );

                    member.RespawnAtStart();
                }
            }
        }

        private static SpawnPoint FindSpawnPoint(
            SpawnPoint[] spawnPoints, Team team, int index)
        {
            List<SpawnPoint> teamPoints = new List<SpawnPoint>();

            foreach (SpawnPoint spawnPoint in spawnPoints)
            {
                if (spawnPoint.Team == team)
                {
                    teamPoints.Add(spawnPoint);
                }
            }

            if (teamPoints.Count == 0)
            {
                return null;
            }

            // FindObjectsByType nu garanteaza ordinea - sortam dupa nume ca sa fie stabil.
            teamPoints.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

            return teamPoints[index % teamPoints.Count];
        }

        /// <summary>
        /// In faza de pregatire doar atacatorii stau pe loc; aparatorii se misca si pregatesc site-urile.
        /// </summary>
        private void SetFreeze(bool frozen)
        {
            foreach (TeamMember member in TeamMember.All)
            {
                if (member.FpsController == null)
                {
                    continue;
                }

                bool locked = frozen && member.Team == Team.Attackers;
                member.FpsController.SetInputLock(this, locked);
            }
        }

        private TeamMember ChooseBombCarrier()
        {
            if (LocalPlayer != null && LocalPlayer.Team == Team.Attackers)
            {
                return LocalPlayer;
            }

            foreach (TeamMember member in TeamMember.All)
            {
                if (member.Team == Team.Attackers)
                {
                    return member;
                }
            }

            return null;
        }
    }
}
