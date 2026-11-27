using System.Collections.Generic;
using ChokeHold.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace ChokeHold.UI
{
    /// <summary>
    /// Meniul de cumparare (B), ca in CS: doar in zona de spawn a echipei si in timpul de cumparare
    /// (toata pregatirea + primele secunde ale rundei). Coloane: pistoale, pusti, lunetiste ale echipei,
    /// cu pret, daune si munitie; click pe arma o cumpara si o pune in mana. Banii se vad mereu in colt.
    /// Isi construieste singur Canvas-ul la runtime.
    /// </summary>
    [RequireComponent(typeof(PlayerLoadout))]
    [RequireComponent(typeof(PlayerEconomy))]
    public class BuyMenu : MonoBehaviour
    {
        [SerializeField] private float buyTime = 20f;
        [SerializeField] private float buyZoneRadius = 16f;

        private static readonly Color Panel = new Color(0.05f, 0.06f, 0.07f, 0.92f);
        private static readonly Color ItemColor = new Color(0.16f, 0.18f, 0.2f, 0.95f);
        private static readonly Color ItemHover = new Color(0.26f, 0.3f, 0.33f, 1f);
        private static readonly Color Owned = new Color(0.35f, 0.8f, 0.45f);
        private static readonly Color TooExpensive = new Color(0.95f, 0.35f, 0.3f);
        private static readonly Color MoneyColor = new Color(0.45f, 0.95f, 0.55f);

        private PlayerLoadout loadout;
        private Gameplay.Grenades.GrenadeInventory grenades;
        private PlayerEconomy economy;
        private TeamMember member;

        private GameObject panel;
        private TMP_Text moneyText;
        private TMP_Text moneyDeltaText;
        private TMP_Text hintText;
        private TMP_Text titleText;
        private RectTransform[] columns;
        private readonly List<Item> items = new List<Item>();
        private Team builtFor = (Team)(-1);
        private float deltaUntil;
        private bool open;

        private sealed class Item
        {
            public WeaponData Data;
            public Gameplay.Grenades.GrenadeType? Grenade;
            public Image Background;
            public TMP_Text Price;
            public TMP_Text State;
        }

        public bool IsOpen => open;

        private void Start()
        {
            loadout = GetComponent<PlayerLoadout>();
            economy = GetComponent<PlayerEconomy>();
            member = GetComponent<TeamMember>();
            EnsureEventSystem();
            Build();
            economy.MoneyChanged += OnMoneyChanged;
            loadout.Changed += Refresh;
            RefreshMoney();
        }

        private void OnDestroy()
        {
            if (economy != null) economy.MoneyChanged -= OnMoneyChanged;
            if (loadout != null) loadout.Changed -= Refresh;
            if (open) PlayerInputGate.Request(false);
        }

        // ------------------------------------------------------------------
        // Reguli
        // ------------------------------------------------------------------

        public bool CanBuyNow
        {
            get
            {
                RoundManager rounds = RoundManager.Instance;
                if (member == null || !member.IsAlive || rounds == null)
                {
                    return false;
                }

                bool time = rounds.Phase == RoundPhase.PreRound || (rounds.Phase == RoundPhase.Live && rounds.LiveElapsed < buyTime);
                return time && InBuyZone();
            }
        }

        private float BuyTimeLeft
        {
            get
            {
                RoundManager rounds = RoundManager.Instance;
                if (rounds == null) return 0f;
                return rounds.Phase == RoundPhase.PreRound ? rounds.PhaseTimeRemaining + buyTime : buyTime - rounds.LiveElapsed;
            }
        }

        private bool InBuyZone()
        {
            foreach (SpawnPoint spawn in FindObjectsByType<SpawnPoint>())
            {
                if (spawn.Team == member.Team && (spawn.transform.position - transform.position).sqrMagnitude < buyZoneRadius * buyZoneRadius)
                {
                    return true;
                }
            }
            return false;
        }

        private void Update()
        {
            if (member == null)
            {
                return;
            }

            bool canBuy = CanBuyNow;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.bKey.wasPressedThisFrame && (open || canBuy))
                {
                    SetOpen(!open);
                }
                else if (open && Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    SetOpen(false);
                }
            }

            if (open && !canBuy)
            {
                SetOpen(false);
            }

            hintText.text = !open && canBuy ? "<color=#FFD75A>[B]</color> Cumpara arme" : string.Empty;

            if (open)
            {
                UpdateTitle();
            }

            if (moneyDeltaText.text.Length > 0 && Time.time > deltaUntil)
            {
                moneyDeltaText.text = string.Empty;
            }
        }

        private void SetOpen(bool value)
        {
            if (open == value)
            {
                return;
            }

            open = value;
            panel.SetActive(open);
            PlayerInputGate.Request(open);
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;

            if (open)
            {
                if (builtFor != member.Team)
                {
                    BuildItems();
                }
                Refresh();
                UpdateTitle();
            }
        }

        private void UpdateTitle()
        {
            string team = member.Team == Team.Attackers ? "ATACATORI" : "APARATORI";
            titleText.text = $"CUMPARARE  <size=60%><color=#BBBBBB>{team}  ·  {Mathf.CeilToInt(Mathf.Max(0f, BuyTimeLeft))} s</color></size>";
        }

        private void PurchaseGrenade(Gameplay.Grenades.GrenadeType type)
        {
            if (!open || grenades == null || !CanBuyNow || !grenades.CanTake(type))
            {
                return;
            }

            if (economy.Spend(Gameplay.Grenades.GrenadeSpec.Of(type).Price))
            {
                grenades.Give(type);
            }
        }

        private void Purchase(WeaponData data)
        {
            if (!open || data == null || loadout.Owns(data) || !CanBuyNow)
            {
                return;
            }

            if (economy.Spend(data.price))
            {
                loadout.Give(data);
            }
        }

        // ------------------------------------------------------------------
        // Afisare
        // ------------------------------------------------------------------

        private void OnMoneyChanged(int money, int delta)
        {
            RefreshMoney();
            if (delta > 0)
            {
                moneyDeltaText.text = $"+${delta}";
                deltaUntil = Time.time + 2.5f;
            }
            Refresh();
        }

        private void RefreshMoney()
        {
            moneyText.text = $"${economy.Money}";
        }

        private void Refresh()
        {
            foreach (Item item in items)
            {
                if (item.Grenade.HasValue)
                {
                    Gameplay.Grenades.GrenadeSpec spec = Gameplay.Grenades.GrenadeSpec.Of(item.Grenade.Value);
                    int have = grenades != null ? grenades.Count(spec.Type) : 0;
                    bool full = grenades == null || !grenades.CanTake(spec.Type);
                    bool canPay = economy.CanAfford(spec.Price);
                    item.Price.text = $"${spec.Price}";
                    item.Price.color = full ? Owned : canPay ? Color.white : TooExpensive;
                    item.State.text = have > 0 && full ? $"AI {have}" : full ? "MAXIM 4" : canPay ? (have > 0 ? $"AI {have}" : string.Empty) : "BANI INSUFICIENTI";
                    item.State.color = full || have > 0 ? Owned : TooExpensive;
                    item.Background.color = have > 0 ? new Color(0.12f, 0.22f, 0.15f, 0.95f) : ItemColor;
                    continue;
                }

                bool owned = loadout.Owns(item.Data);
                bool affordable = economy.CanAfford(item.Data.price);
                item.Price.text = $"${item.Data.price}";
                item.Price.color = owned ? Owned : affordable ? Color.white : TooExpensive;
                item.State.text = owned ? "IN INVENTAR" : affordable ? string.Empty : "BANI INSUFICIENTI";
                item.State.color = owned ? Owned : TooExpensive;
                item.Background.color = owned ? new Color(0.12f, 0.22f, 0.15f, 0.95f) : ItemColor;
            }
        }

        // ------------------------------------------------------------------
        // Constructie UI
        // ------------------------------------------------------------------

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void Build()
        {
            GameObject canvasObject = new GameObject("BuyMenu Canvas");
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();
            RectTransform root = canvasObject.GetComponent<RectTransform>();

            // Banii: mereu vizibili, stanga-sus
            moneyText = Text(root, "Money", new Vector2(0f, 1f), new Vector2(150f, -50f), new Vector2(260f, 50f), 40f, TextAlignmentOptions.Left);
            moneyText.color = MoneyColor;
            moneyDeltaText = Text(root, "MoneyDelta", new Vector2(0f, 1f), new Vector2(150f, -95f), new Vector2(260f, 40f), 28f, TextAlignmentOptions.Left);
            moneyDeltaText.color = MoneyColor;

            hintText = Text(root, "BuyHint", new Vector2(0.5f, 0f), new Vector2(0f, 320f), new Vector2(800f, 40f), 28f, TextAlignmentOptions.Center);

            // Panoul
            panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(1680f, 700f);
            Image background = panel.AddComponent<Image>();
            background.color = Panel;

            titleText = Text(panelRect, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -45f), new Vector2(1200f, 60f), 44f, TextAlignmentOptions.Center);
            TMP_Text footer = Text(panelRect, "Footer", new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(1200f, 40f), 22f, TextAlignmentOptions.Center);
            footer.color = new Color(0.75f, 0.75f, 0.75f);
            footer.text = "Click ca sa cumperi  ·  [1] principala  [2] pistol  [3] cutit  [4] grenada, [G] arunca  ·  [B] / [Esc] inchide";

            string[] headers = { "PISTOALE", "PUSTI", "LUNETISTE", "GRENADE" };
            columns = new RectTransform[headers.Length];
            for (int c = 0; c < headers.Length; c++)
            {
                float x = (c - 1.5f) * 410f;
                TMP_Text header = Text(panelRect, headers[c], new Vector2(0.5f, 1f), new Vector2(x, -115f), new Vector2(390f, 40f), 28f, TextAlignmentOptions.Center);
                header.text = headers[c];
                header.color = new Color(1f, 0.84f, 0.35f);

                GameObject column = new GameObject($"Column_{headers[c]}", typeof(RectTransform));
                column.transform.SetParent(panelRect, false);
                RectTransform rect = column.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(x, -145f);
                rect.sizeDelta = new Vector2(390f, 480f);
                VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10f;
                layout.childControlHeight = false;
                layout.childControlWidth = true;
                layout.childForceExpandHeight = false;
                columns[c] = rect;
            }

            panel.SetActive(false);
        }

        private void BuildItems()
        {
            foreach (Item item in items)
            {
                Destroy(item.Background.gameObject);
            }
            items.Clear();

            WeaponCatalog catalog = loadout.Catalog;
            if (catalog == null)
            {
                return;
            }

            WeaponCategory[] categories = { WeaponCategory.Pistol, WeaponCategory.Rifle, WeaponCategory.Sniper };
            for (int c = 0; c < categories.Length; c++)
            {
                foreach (WeaponData data in catalog.Buyable(member.Team, categories[c]))
                {
                    items.Add(CreateItem(columns[c], data));
                }
            }

            grenades ??= GetComponent<Gameplay.Grenades.GrenadeInventory>();
            if (grenades != null)
            {
                grenades.Changed -= Refresh;
                grenades.Changed += Refresh;
                foreach (Gameplay.Grenades.GrenadeSpec spec in Gameplay.Grenades.GrenadeSpec.All)
                {
                    items.Add(CreateGrenadeItem(columns[3], spec));
                }
            }

            builtFor = member.Team;
        }

        private Item CreateItem(RectTransform column, WeaponData data)
        {
            GameObject go = new GameObject(data.name, typeof(RectTransform));
            go.transform.SetParent(column, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(390f, 96f);
            Image image = go.AddComponent<Image>();
            image.color = ItemColor;
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.targetGraphic = image;
            button.onClick.AddListener(() => Purchase(data));

            RectTransform rect = go.GetComponent<RectTransform>();
            TMP_Text name = Text(rect, "Name", new Vector2(0f, 1f), new Vector2(150f, -26f), new Vector2(270f, 36f), 26f, TextAlignmentOptions.Left);
            name.text = data.displayName;
            TMP_Text price = Text(rect, "Price", new Vector2(1f, 1f), new Vector2(-70f, -26f), new Vector2(120f, 36f), 26f, TextAlignmentOptions.Right);
            TMP_Text stats = Text(rect, "Stats", new Vector2(0f, 0f), new Vector2(145f, 24f), new Vector2(260f, 30f), 18f, TextAlignmentOptions.Left);
            stats.color = new Color(0.72f, 0.75f, 0.78f);
            stats.enableAutoSizing = true;      // textul lung (lunetiste) se micsoreaza, nu intra peste stare
            stats.fontSizeMin = 12f;
            stats.fontSizeMax = 18f;
            stats.text = Describe(data);
            TMP_Text state = Text(rect, "State", new Vector2(1f, 0f), new Vector2(-62f, 24f), new Vector2(110f, 30f), 14f, TextAlignmentOptions.Right);
            return new Item { Data = data, Background = image, Price = price, State = state };
        }

        private Item CreateGrenadeItem(RectTransform column, Gameplay.Grenades.GrenadeSpec spec)
        {
            GameObject go = new GameObject(spec.Prefab, typeof(RectTransform));
            go.transform.SetParent(column, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(390f, 82f);
            Image image = go.AddComponent<Image>();
            image.color = ItemColor;
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.35f, 1.35f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            button.colors = colors;
            button.targetGraphic = image;
            Gameplay.Grenades.GrenadeType type = spec.Type;
            button.onClick.AddListener(() => PurchaseGrenade(type));

            RectTransform rect = go.GetComponent<RectTransform>();
            TMP_Text name = Text(rect, "Name", new Vector2(0f, 1f), new Vector2(150f, -24f), new Vector2(270f, 34f), 24f, TextAlignmentOptions.Left);
            name.text = spec.Name;
            TMP_Text price = Text(rect, "Price", new Vector2(1f, 1f), new Vector2(-70f, -24f), new Vector2(120f, 34f), 24f, TextAlignmentOptions.Right);
            TMP_Text stats = Text(rect, "Stats", new Vector2(0f, 0f), new Vector2(145f, 20f), new Vector2(260f, 28f), 16f, TextAlignmentOptions.Left);
            stats.color = new Color(0.72f, 0.75f, 0.78f);
            stats.enableAutoSizing = true;
            stats.fontSizeMin = 11f;
            stats.fontSizeMax = 16f;
            stats.text = spec.Description;
            TMP_Text state = Text(rect, "State", new Vector2(1f, 0f), new Vector2(-62f, 20f), new Vector2(110f, 28f), 14f, TextAlignmentOptions.Right);
            return new Item { Grenade = spec.Type, Background = image, Price = price, State = state };
        }

        private static string Describe(WeaponData data)
        {
            string fire = data.automatic ? "auto" : "semi";
            string sight = data.sight == SightType.SniperScope ? " · luneta" : data.sight == SightType.Acog ? " · ACOG" : string.Empty;
            return $"Daune {data.damage:0} · {data.magazineSize}/{data.reserveAmmo} · {fire}{sight}";
        }

        private static TMP_Text Text(RectTransform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
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
    }
}
