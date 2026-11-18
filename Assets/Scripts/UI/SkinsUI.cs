using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Экран скинов: карточки, условия разблокировки, покупка и надевание.</summary>
public class SkinsUI : MonoBehaviour
{
    private const int Columns = 2;
    private const float CardWidth = 552f;
    private const float CardHeight = 176f;
    private const float CardGap = 16f;
    private const float PadX = 18f;
    private const float PadTop = 14f;

    private static readonly Color LockedTop = UiKit.C_Card;
    private static readonly Color LockedBottom = UiKit.C_CardDark;
    private static readonly Color UnlockedTop = UiKit.C_BlueTop;
    private static readonly Color UnlockedBottom = UiKit.C_BlueBottom;
    private static readonly Color EquippedTop = UiKit.C_GreenTop;
    private static readonly Color EquippedBottom = UiKit.C_GreenBottom;
    private static readonly Color BuyTop = UiKit.C_GoldTop;
    private static readonly Color BuyBottom = UiKit.C_GoldBottom;
    private static readonly Color EquipTop = UiKit.C_AccentTop;
    private static readonly Color EquipBottom = UiKit.C_AccentBottom;

    private class Card
    {
        public SkinDefinition Skin;
        public GameObject Root;
        public Image Background;
        public Image SwatchBody;
        public Text Title;
        public Text Status;
        public Button Action;
        public Text ActionLabel;
    }

    private RectTransform _host;
    private GameObject _overlay;
    private RectTransform _content;
    private Text _summary;
    private Text _coinsChip;
    private Action _onClosed;
    private readonly List<Card> _cards = new List<Card>();

    public static SkinsUI Open(RectTransform host, Action onClosed)
    {
        GameObject go = new GameObject("Skins UI");
        SkinsUI ui = go.AddComponent<SkinsUI>();
        ui._host = host;
        ui._onClosed = onClosed;
        ui.Build();
        return ui;
    }

    private void OnEnable()
    {
        SkinService.Changed += OnServiceChanged;
    }

    private void OnDisable()
    {
        SkinService.Changed -= OnServiceChanged;
    }

    private void OnServiceChanged()
    {
        if (_summary != null)
            Refresh();
    }

    public void Close()
    {
        Action handler = _onClosed;
        _onClosed = null;

        // Панель — ребёнок _host (меню), а не носителя этого компонента,
        // поэтому Destroy(gameObject) её не убирает. Удаляем оба, иначе
        // панель остаётся на экране, а при каждом открытии копится новая.
        if (_overlay != null)
            Destroy(_overlay);
        _overlay = null;

        Destroy(gameObject);

        if (handler != null) handler();
    }

    private void Build()
    {
        _overlay = UiKit.Overlay(_host, "Skins Panel").gameObject;
        RectTransform root = _overlay.GetComponent<RectTransform>();

        RectTransform panel = UiKit.Modal(root, "Skins Panel Body", 1240f, 880f);

        _summary = UiKit.Header(panel, "СКИНЫ", "", "person", UiKit.C_Soft,
            UiKit.C_Tile, UiKit.C_TileDark, 1000f);
        _coinsChip = UiKit.CoinChip(panel, 40f, 36f, 200f, 58f, GameProgress.GetCoins());

        ScrollRect scroll = UiKit.Scroll(panel, 40f, 142f, 1200f, 700f);
        _content = scroll.content;

        Button back = UiKit.Back(panel, "Назад", 900f, 722f, 1200f, 790f, Close);

        Text hint = UiKit.Label(panel,
            "Монеты — за чекпоинты, каждые 100 м рекорда и pickups в уровне.",
            20, UiKit.C_Muted, 40f, 722f, 860f, 790f, TextAnchor.MiddleLeft, false);
        UiKit.Wrap(hint);

        Refresh();
    }

    private void Refresh()
    {
        if (_content == null) return;

        _summary.text = SkinService.SummaryLine();
        if (_coinsChip != null) _coinsChip.text = UiKit.Money(GameProgress.GetCoins());

        List<SkinDefinition> all = SkinCatalog.All;
        int used = 0;
        for (int i = 0; i < all.Count; i++)
        {
            Fill(GetCard(used), all[i]);
            used++;
        }

        for (int i = used; i < _cards.Count; i++)
        {
            if (_cards[i].Root != null)
                _cards[i].Root.SetActive(false);
        }

        int rows = Mathf.CeilToInt(used / (float)Columns);
        _content.sizeDelta = new Vector2(0f, PadTop + rows * (CardHeight + CardGap) + 20f);
    }

    private Card GetCard(int index)
    {
        while (_cards.Count <= index)
            _cards.Add(CreateCard(_cards.Count));

        Card card = _cards[index];
        if (card.Root != null)
            card.Root.SetActive(true);

        int column = index % Columns;
        int row = index / Columns;
        RectTransform rt = card.Root.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(PadX + column * (CardWidth + CardGap), -(PadTop + row * (CardHeight + CardGap)));
        return card;
    }

    private Card CreateCard(int index)
    {
        GameObject go = new GameObject("Card " + index);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.SetParent(_content, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(CardWidth, CardHeight);

        Card card = new Card();
        card.Root = go;
        card.Background = go.AddComponent<Image>();
        card.Background.raycastTarget = false;
        UiKit.ApplyRound(card.Background, LockedTop, LockedBottom, 20);

        // Плашка с цветом скина: скруглённый квадрат с внутренним акцентом.
        RectTransform swatch = UiKit.Surface(rt, "Swatch", 20f, 30f, 120f, 146f,
            UiKit.C_CardDark, UiKit.C_CardDark, 22, false);
        card.SwatchBody = swatch.GetComponent<Image>();
        Image accentImage = UiKit.Surface(swatch, "Accent", 26f, 26f, 74f, 90f,
            Color.white, Color.white, 16, false).GetComponent<Image>();
        accentImage.name = "Accent Image";

        card.Title = UiKit.Label(rt, "", 27, UiKit.C_Text, 136f, 22f, 400f, 66f,
            TextAnchor.MiddleLeft, true);

        card.Status = UiKit.Wrap(UiKit.Label(rt, "", 18, UiKit.C_Muted, 136f, 68f, 404f, 148f,
            TextAnchor.UpperLeft, false));

        card.Action = UiKit.SurfaceButton(rt, "Action", 408f, 62f, 532f, 122f,
            EquipTop, EquipBottom, null);
        card.ActionLabel = UiKit.Label(card.Action.transform, "", 21, UiKit.C_Text,
            0f, 0f, 124f, 60f, TextAnchor.MiddleCenter, true);
        card.Action.onClick.AddListener(delegate { OnCardClicked(card); });

        return card;
    }

    private void Fill(Card card, SkinDefinition skin)
    {
        if (card == null || skin == null) return;

        card.Skin = skin;
        card.Title.text = skin.Title;
        UiKit.ApplyRound(card.SwatchBody, Color.Lerp(skin.BodyColor, Color.white, 0.10f),
            Color.Lerp(skin.BodyColor, Color.black, 0.38f), 22, false);

        Image accent = card.SwatchBody.transform.Find("Accent Image")?.GetComponent<Image>();
        if (accent != null)
        {
            Color acc = skin.AccentColor;
            accent.color = acc;
            UiKit.ApplyRound(accent, Color.Lerp(acc, Color.white, 0.18f),
                Color.Lerp(acc, Color.black, 0.25f), 16, false);
        }

        bool unlocked = SkinService.IsUnlocked(skin);
        bool equipped = SkinService.IsEquipped(skin);
        bool purchasable = SkinService.IsPurchasable(skin);

        Color top = equipped ? EquippedTop : (unlocked ? UnlockedTop : LockedTop);
        Color bottom = equipped ? EquippedBottom : (unlocked ? UnlockedBottom : LockedBottom);
        UiKit.ApplyRound(card.Background, top, bottom, 20);

        if (card.ActionLabel != null)
        {
            card.ActionLabel.text = equipped
                ? "НАДЕТ"
                : (unlocked ? "НАДЕТЬ" : (purchasable ? "КУПИТЬ" : "ЗАКРЫТО"));
            card.ActionLabel.color = (!equipped && !unlocked && purchasable)
                ? UiKit.C_GoldDark
                : (equipped ? UiKit.C_Green : UiKit.C_Text);
        }

        if (equipped)
            UiKit.ApplyRound(card.Action.image, MenuArt.Hex("1C3B2C"), MenuArt.Hex("142C21"), 20);
        else if (!unlocked && purchasable)
            UiKit.ApplyRound(card.Action.image, BuyTop, BuyBottom, 20);
        else if (!unlocked)
            UiKit.ApplyRound(card.Action.image, UiKit.C_TileDark, UiKit.C_TileDark, 20);
        else
            UiKit.ApplyRound(card.Action.image, EquipTop, EquipBottom, 20);

        card.Action.interactable = !equipped && (unlocked || purchasable);

        if (equipped)
            card.Status.text = "Активный скин";
        else if (unlocked)
            card.Status.text = skin.RequirementText() + (string.IsNullOrEmpty(skin.Description) ? "" : "\n" + skin.Description);
        else
            card.Status.text = SkinService.LockHint(skin);

        card.Status.color = unlocked ? UiKit.C_Muted : UiKit.C_Dim;
    }

    private void OnCardClicked(Card card)
    {
        if (card == null || card.Skin == null) return;

        SkinDefinition skin = card.Skin;
        if (SkinService.IsUnlocked(skin))
            SkinService.Select(skin.Id);
        else
            SkinService.TryUnlock(skin);

        Refresh();
    }
}
