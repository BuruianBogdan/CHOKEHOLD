using ChokeHold.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ChokeHold.UI
{
    /// <summary>
    /// HUD de runda: scor, timer, mesaje, viata si bara de plant/defuse.
    /// Isi construieste singur Canvas-ul la runtime, deci nu trebuie legat nimic in Inspector.
    /// </summary>
    public class RoundHUD : MonoBehaviour
    {
        private static readonly Color AttackerColor = new Color(0.45f, 0.65f, 1f);
        private static readonly Color DefenderColor = new Color(1f, 0.45f, 0.4f);
        private static readonly Color BombColor = new Color(1f, 0.25f, 0.2f);

        private RoundManager roundManager;
        private BombController bomb;

        private TMP_Text scoreText;
        private TMP_Text timerText;
        private TMP_Text messageText;
        private TMP_Text hintText;
        private TMP_Text healthText;
        private TMP_Text bombStatusText;
        private TMP_Text prepStatusText;

        private DefenderPrepController prep;

        private bool LocalPlayerDefends =>
            roundManager.LocalPlayer != null &&
            roundManager.LocalPlayer.Team == Team.Defenders;

        private GameObject progressRoot;
        private RectTransform progressFill;

        private void Start()
        {
            roundManager = RoundManager.Instance;

            if (roundManager == null)
            {
                Debug.LogWarning("RoundHUD: nu exista RoundManager in scena.", this);
                enabled = false;
                return;
            }

            bomb = roundManager.Bomb;
            prep = roundManager.Prep;
            BuildUI();
        }

        private void Update()
        {
            UpdateScoreAndTimer();
            UpdateMessage();
            UpdateBombUI();
            UpdateHealth();
        }

        private void UpdateScoreAndTimer()
        {
            scoreText.text =
                $"<color=#{ColorUtility.ToHtmlStringRGB(AttackerColor)}>ATK {roundManager.AttackerScore}</color>" +
                "  :  " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(DefenderColor)}>{roundManager.DefenderScore} DEF</color>";

            int seconds = Mathf.CeilToInt(roundManager.PhaseTimeRemaining);
            string time = $"{seconds / 60}:{seconds % 60:00}";

            switch (roundManager.Phase)
            {
                case RoundPhase.BombPlanted:
                    timerText.text = $"BOMBA {time}";
                    timerText.color = BombColor;
                    break;

                case RoundPhase.Live:
                    timerText.text = time;
                    timerText.color = seconds <= 10 ? BombColor : Color.white;
                    break;

                case RoundPhase.PreRound:
                    timerText.text = LocalPlayerDefends ? $"PREGATIRE {time}" : time;
                    timerText.color = Color.yellow;
                    break;

                default:
                    timerText.text = string.Empty;
                    break;
            }
        }

        private void UpdateMessage()
        {
            switch (roundManager.Phase)
            {
                case RoundPhase.PreRound:
                    // Aparatorul are de lucru in pregatire - nu ii acoperim ecranul cu un mesaj mare.
                    messageText.color = Color.white;
                    messageText.text = LocalPlayerDefends
                        ? string.Empty
                        : $"RUNDA {roundManager.RoundNumber}\n" +
                          "<size=60%>Aparatorii se pregatesc...</size>";
                    break;

                case RoundPhase.RoundEnd:
                    ShowWinner(DescribeReason(roundManager.LastEndReason));
                    break;

                case RoundPhase.MatchEnd:
                    ShowWinner("MECI TERMINAT - apasa [Enter] pentru un meci nou");
                    break;

                default:
                    messageText.text = string.Empty;
                    break;
            }
        }

        private void ShowWinner(string subtitle)
        {
            bool attackersWon = roundManager.LastWinner == Team.Attackers;

            messageText.color = attackersWon ? AttackerColor : DefenderColor;
            messageText.text =
                (attackersWon ? "ATACATORII CASTIGA" : "APARATORII CASTIGA") +
                $"\n<size=60%>{subtitle}</size>";
        }

        private static string DescribeReason(RoundEndReason reason)
        {
            switch (reason)
            {
                case RoundEndReason.AttackersEliminated: return "Atacatorii au fost eliminati";
                case RoundEndReason.DefendersEliminated: return "Aparatorii au fost eliminati";
                case RoundEndReason.TimeExpired: return "Timpul a expirat";
                case RoundEndReason.BombExploded: return "Bomba a explodat";
                case RoundEndReason.BombDefused: return "Bomba a fost dezamorsata";
                default: return string.Empty;
            }
        }

        private void UpdateBombUI()
        {
            if (bomb == null)
            {
                hintText.text = string.Empty;
                bombStatusText.text = string.Empty;
                prepStatusText.text = string.Empty;
                progressRoot.SetActive(false);
                return;
            }

            // Bara si hint-ul sunt comune: plant/defuse sau pregatirea aparatorului (intarire, baricada).
            bool bombAction = bomb.IsPlanting || bomb.IsDefusing;
            bool prepAction = prep != null && prep.IsActing;
            bool usePrep = prep != null && !bombAction &&
                           (prepAction || !string.IsNullOrEmpty(prep.Hint));

            hintText.text = usePrep ? prep.Hint : bomb.Hint;

            bool showProgress = bombAction || prepAction;
            progressRoot.SetActive(showProgress);

            if (showProgress)
            {
                float progress = bombAction ? bomb.ActionProgress : prep.ActionProgress;
                progressFill.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
            }

            bool showPrepStatus =
                prep != null &&
                LocalPlayerDefends &&
                (roundManager.Phase == RoundPhase.PreRound ||
                 roundManager.Phase == RoundPhase.Live ||
                 roundManager.Phase == RoundPhase.BombPlanted);

            prepStatusText.text = showPrepStatus
                ? $"Intariri {prep.ReinforcementsLeft}   Baricade {prep.BarricadesLeft}"
                : string.Empty;

            bool roundActive =
                roundManager.Phase == RoundPhase.Live ||
                roundManager.Phase == RoundPhase.PreRound;

            if (roundActive &&
                bomb.Carrier != null &&
                bomb.Carrier == roundManager.LocalPlayer)
            {
                bombStatusText.text = "[ BOMBA ]";
            }
            else if (roundActive && bomb.IsDropped)
            {
                bombStatusText.text = "Bomba e pe jos";
            }
            else if (bomb.IsPlanted && bomb.PlantedSite != null)
            {
                bombStatusText.text = $"Bomba plantata pe {bomb.PlantedSite.SiteId}";
            }
            else
            {
                bombStatusText.text = string.Empty;
            }
        }

        private void UpdateHealth()
        {
            TeamMember player = roundManager.LocalPlayer;

            if (player == null)
            {
                healthText.text = string.Empty;
                return;
            }

            float health = player.Health.CurrentHealth;

            healthText.text = player.IsAlive
                ? $"HP {Mathf.CeilToInt(health)}"
                : "ELIMINAT";

            healthText.color = health <= 30f ? BombColor : Color.white;
        }

        // ------------------------------------------------------------------
        // Constructie UI
        // ------------------------------------------------------------------
        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("RoundHUD Canvas");
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform root = canvasObject.GetComponent<RectTransform>();

            // Sus-centru: scor si timer
            scoreText = CreateText(root, "Score", new Vector2(0.5f, 1f), new Vector2(0f, -40f),
                new Vector2(600f, 50f), 34f, TextAlignmentOptions.Center);

            timerText = CreateText(root, "Timer", new Vector2(0.5f, 1f), new Vector2(0f, -90f),
                new Vector2(600f, 60f), 46f, TextAlignmentOptions.Center);

            // Centru-sus: mesaje mari (runda, castigator)
            messageText = CreateText(root, "Message", new Vector2(0.5f, 0.5f), new Vector2(0f, 220f),
                new Vector2(1400f, 200f), 64f, TextAlignmentOptions.Center);

            // Jos-centru: hint si bara de progres
            hintText = CreateText(root, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 260f),
                new Vector2(1000f, 50f), 30f, TextAlignmentOptions.Center);

            progressRoot = CreateImage(root, "Progress", new Vector2(0.5f, 0f), new Vector2(0f, 210f),
                new Vector2(500f, 22f), new Color(0f, 0f, 0f, 0.6f));

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(progressRoot.transform, false);
            progressFill = fill.GetComponent<RectTransform>();
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = new Vector2(3f, 3f);
            progressFill.offsetMax = new Vector2(-3f, -3f);
            fill.AddComponent<Image>().color = new Color(1f, 0.8f, 0.15f);

            progressRoot.SetActive(false);

            // Jos-stanga, deasupra textului de munitie existent: viata, apoi starea bombei.
            healthText = CreateText(root, "Health", new Vector2(0f, 0f), new Vector2(200f, 125f),
                new Vector2(320f, 60f), 44f, TextAlignmentOptions.Left);

            bombStatusText = CreateText(root, "BombStatus", new Vector2(0f, 0f), new Vector2(200f, 180f),
                new Vector2(320f, 40f), 26f, TextAlignmentOptions.Left);
            bombStatusText.color = BombColor;

            // Resursele aparatorului (intariri / baricade ramase).
            prepStatusText = CreateText(root, "PrepStatus", new Vector2(0f, 0f), new Vector2(240f, 225f),
                new Vector2(400f, 40f), 26f, TextAlignmentOptions.Left);
            prepStatusText.color = new Color(1f, 0.85f, 0.4f);
        }

        private static TMP_Text CreateText(RectTransform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.fontStyle = FontStyles.Bold;
            text.richText = true;
            text.raycastTarget = false;
            text.text = string.Empty;

            return text;
        }

        private static GameObject CreateImage(RectTransform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return go;
        }
    }
}
