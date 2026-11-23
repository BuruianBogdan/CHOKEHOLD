using System;
using UnityEngine;

namespace ChokeHold.Gameplay
{
    /// <summary>
    /// Banii jucatorului, ca in CS: 800 la inceputul meciului, maxim 16000; recompensa pe kill dupa arma,
    /// castig de runda 3250, pierdere 1400 + 500 pentru fiecare runda pierduta la rand (pana la 3400),
    /// 300 pentru plantarea bombei (atacatori).
    /// </summary>
    [RequireComponent(typeof(TeamMember))]
    public class PlayerEconomy : MonoBehaviour
    {
        [SerializeField] private int startMoney = 800;
        [SerializeField] private int maxMoney = 16000;
        [SerializeField] private int winReward = 3250;
        [SerializeField] private int lossBase = 1400;
        [SerializeField] private int lossIncrement = 500;
        [SerializeField] private int lossMax = 3400;
        [SerializeField] private int plantReward = 300;

        private TeamMember member;
        private HitscanWeapon weapon;
        private RoundManager rounds;
        private int lossStreak;
        private int lastRound = -1;

        public int Money { get; private set; }

        /// <summary>Suma noua si diferenta (pozitiva la castig, negativa la cumparare).</summary>
        public event Action<int, int> MoneyChanged;

        private void Awake()
        {
            member = GetComponent<TeamMember>();
            Money = startMoney;
        }

        private void Start()
        {
            weapon = GetComponentInChildren<HitscanWeapon>(true);
            if (weapon != null)
            {
                weapon.TargetKilled += OnTargetKilled;
            }
        }

        private void OnDestroy()
        {
            if (weapon != null)
            {
                weapon.TargetKilled -= OnTargetKilled;
            }
            Unsubscribe();
        }

        private void Update()
        {
            // RoundManager poate porni dupa noi: ne legam de el cand apare
            if (rounds == null && RoundManager.Instance != null)
            {
                rounds = RoundManager.Instance;
                rounds.RoundEnded += OnRoundEnded;
                rounds.PhaseChanged += OnPhaseChanged;
                OnPhaseChanged(rounds.Phase);
            }
        }

        private void Unsubscribe()
        {
            if (rounds != null)
            {
                rounds.RoundEnded -= OnRoundEnded;
                rounds.PhaseChanged -= OnPhaseChanged;
                rounds = null;
            }
        }

        public bool CanAfford(int price) => Money >= price;

        public bool Spend(int price)
        {
            if (price > Money)
            {
                return false;
            }

            Add(-price);
            return true;
        }

        private void Add(int amount)
        {
            int before = Money;
            Money = Mathf.Clamp(Money + amount, 0, maxMoney);
            if (Money != before)
            {
                MoneyChanged?.Invoke(Money, Money - before);
            }
        }

        private void OnTargetKilled(Health target, WeaponData data)
        {
            TeamMember victim = target != null ? target.GetComponent<TeamMember>() : null;
            if (victim != null && victim.Team == member.Team)
            {
                return;     // fara bani pe coechipieri
            }

            Add(data != null ? data.killReward : 300);
        }

        private void OnRoundEnded(Team winner, RoundEndReason reason)
        {
            if (winner == member.Team)
            {
                lossStreak = 0;
                Add(winReward);
            }
            else
            {
                Add(Mathf.Min(lossBase + lossIncrement * lossStreak, lossMax));
                lossStreak++;
            }
        }

        private void OnPhaseChanged(RoundPhase phase)
        {
            if (rounds == null)
            {
                return;
            }

            // Meci nou (runda 1 dupa o runda mai mare): banii revin la suma de start
            if (phase == RoundPhase.PreRound && rounds.RoundNumber == 1 && lastRound != 1)
            {
                lossStreak = 0;
                Add(startMoney - Money);
            }

            if (phase == RoundPhase.BombPlanted && member.Team == Team.Attackers)
            {
                Add(plantReward);
            }

            lastRound = rounds.RoundNumber;
        }
    }
}
