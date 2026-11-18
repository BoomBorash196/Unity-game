using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель батл-пасса: шкала XP сезона и список из 40 уровней с двумя дорожками.
/// Заявка награды платит монеты сразу, скин помечается купленным (SetSkinOwned).
/// Открывается из главного меню.
/// </summary>
public class PassUI : MonoBehaviour
{
    private const float RowHeight = 62f;
    private const float RowGap = 6f;

    private class Row
    {
        public GameObject Root;
        public Image Background;
        public Text LevelText;
        public Text FreeLabel;
        public Button FreeClaim;
        public Text FreeClaimLabel;
        public Text PremiumLabel;
        public Button PremiumClaim;
        public Text PremiumClaimLabel;
    }

    private RectTransform _root;
    private System.Action _onClosed;
    private Text _seasonLine;
    private Text _header;
    private Text _status;
    private Text _premiumNote;
    private RectTransform _content;
    private RectTransform _xpFill;
    private Button _buyBtn;
    private readonly List<Row> _rows = new List<Row>();

    public static void Open(Transform parent, System.Action onClosed)
    {
        RectTransform rt = UiKit.Overlay(parent, "Pass Overlay");

        PassUI ui = rt.gameObject.AddComponent<PassUI>();
        ui._root = rt;
        ui._onClosed = onClosed;
        ui.Build();
    }

    private void Build()
    {
        RectTransform panel = UiKit.Modal(_root, "Pass Panel", 900f, 1000f);

        _seasonLine = UiKit.Header(panel, "БАТЛ-ПАСС", "", "star", UiKit.C_Gold,
            UiKit.C_GoldDark, MenuArt.Hex("1B1706"), 860f);

        _status = UiKit.Label(panel, "", 19, UiKit.C_Green, 148f, 114f, 860f, 146f,
            TextAnchor.MiddleLeft, false);

        RectTransform bar = UiKit.Surface(panel, "XP Bar", 40f, 156f, 860f, 170f,
            UiKit.C_TileDark, UiKit.C_TileDark, 7, false);
        _xpFill = UiKit.Surface(bar, "XP Fill", 0f, 0f, 0f, 14f,
            UiKit.C_AccentTop, UiKit.C_AccentBottom, 7, false);

        _header = UiKit.Label(panel, "", 19, UiKit.C_Muted, 40f, 176f, 860f, 204f,
            TextAnchor.MiddleLeft, false);

        ScrollRect scroll = UiKit.Scroll(panel, 40f, 214f, 860f, 888f);
        _content = scroll.content;

        for (int lvl = 1; lvl <= SeasonPass.LevelCount; lvl++)
            CreateRow(lvl);

        Button close = UiKit.Back(panel, "Назад", 40f, 910f, 370f, 972f, Close);

        _premiumNote = UiKit.Label(panel, "Премиум активен", 22, UiKit.C_Gold,
            500f, 910f, 860f, 972f, TextAnchor.MiddleRight, true);

        _buyBtn = UiKit.Pill(panel, "Купить премиум", 21, 580f, 910f, 860f, 972f,
            UiKit.C_GoldTop, UiKit.C_GoldBottom, UiKit.C_GoldDark, () =>
            {
                YandexPayments.BuySeasonPass(ok => { Refresh(); });
            });

        Refresh();
    }

    private void CreateRow(int level)
    {
        RectTransform rowRt = UIFactory.Row(_content, RowHeight, RowGap, _rows.Count, 14f);
        rowRt.gameObject.name = "Pass Row " + level;

        Row row = new Row();
        row.Root = rowRt.gameObject;
        row.Background = UiKit.RowBackground(rowRt, UiKit.C_Card, UiKit.C_CardDark);

        RectTransform levelTile = UiKit.Surface(rowRt, "Level", 10f, 9f, 54f, 53f,
            UiKit.C_TileDark, MenuArt.Hex("0B1830"), 14, false);
        row.LevelText = UiKit.Label(levelTile, level.ToString(), 20, UiKit.C_Text,
            0f, 0f, 44f, 44f, TextAnchor.MiddleCenter, true);

        row.FreeLabel = UiKit.Wrap(UiKit.Label(rowRt, "", 19, UiKit.C_Green, 64f, 4f, 288f, 58f,
            TextAnchor.MiddleLeft, false));

        row.FreeClaim = UiKit.SurfaceButton(rowRt, "Free Claim", 292f, 12f, 372f, 50f,
            UiKit.C_AccentTop, UiKit.C_AccentBottom, null);
        row.FreeClaimLabel = UiKit.Label(row.FreeClaim.transform, "Забрать", 15, UiKit.C_Text,
            0f, 0f, 80f, 38f, TextAnchor.MiddleCenter, true);
        row.FreeClaim.onClick.AddListener(delegate { OnFreeClaim(level); });

        row.PremiumLabel = UiKit.Wrap(UiKit.Label(rowRt, "", 19, UiKit.C_Gold, 384f, 4f, 690f, 58f,
            TextAnchor.MiddleLeft, false));

        row.PremiumClaim = UiKit.SurfaceButton(rowRt, "Premium Claim", 700f, 12f, 782f, 50f,
            UiKit.C_TileDark, UiKit.C_TileDark, null);
        row.PremiumClaimLabel = UiKit.Label(row.PremiumClaim.transform, "Забрать", 15, UiKit.C_Text,
            0f, 0f, 82f, 38f, TextAnchor.MiddleCenter, true);
        row.PremiumClaim.onClick.AddListener(delegate { OnPremiumClaim(level); });

        _rows.Add(row);
    }

    private void OnFreeClaim(int level)
    {
        string message;
        SeasonPass.ClaimFree(level, out message);
        if (_status != null) _status.text = message;
        Refresh();
    }

    private void OnPremiumClaim(int level)
    {
        string message;
        SeasonPass.ClaimPremium(level, out message);
        if (_status != null) _status.text = message;
        Refresh();
    }

    private void Refresh()
    {
        if (_content == null) return;

        _content.sizeDelta = new Vector2(0f, 12f + SeasonPass.LevelCount * (RowHeight + RowGap));

        bool premium = SeasonPass.IsPremium || YandexPayments.HasSeasonPass;
        int level = SeasonPass.Level;

        if (_seasonLine != null)
        {
            _seasonLine.text = "Сезон " + SeasonPass.SeasonId + " · уровень " + level + "/" +
                               SeasonPass.LevelCount + " · до конца " + SeasonPass.DaysLeft + " дн.";
        }

        if (_header != null)
        {
            _header.text = "XP " + SeasonPass.XpIntoLevel + " / " + SeasonPass.XpToNext +
                           " · награда на каждом из " + SeasonPass.LevelCount + " уровней";
        }

        if (_xpFill != null)
        {
            float ratio = SeasonPass.XpToNext > 0
                ? Mathf.Clamp01(SeasonPass.XpIntoLevel / (float)SeasonPass.XpToNext)
                : 1f;
            _xpFill.sizeDelta = new Vector2(820f * ratio, 14f);
        }

        if (_buyBtn != null)
            _buyBtn.gameObject.SetActive(!premium);
        if (_premiumNote != null)
            _premiumNote.gameObject.SetActive(premium);

        for (int i = 0; i < _rows.Count && i < SeasonPass.LevelCount; i++)
        {
            Row row = _rows[i];
            int lvl = i + 1;
            bool reached = lvl <= level;

            UiKit.ApplyRound(row.Background,
                reached ? UiKit.C_BlueTop : UiKit.C_Card,
                reached ? UiKit.C_BlueBottom : UiKit.C_CardDark, 18, false);

            row.FreeLabel.text = SeasonPass.FreeRewardLabel(lvl);
            row.FreeLabel.color = reached ? UiKit.C_Green : UiKit.C_Dim;

            bool hasFree = SeasonPass.HasFreeReward(lvl);
            row.FreeClaim.gameObject.SetActive(hasFree);
            if (hasFree)
            {
                bool can = SeasonPass.CanClaimFree(lvl);
                row.FreeClaim.interactable = can;
                row.FreeClaimLabel.color = can ? UiKit.C_Text : UiKit.C_Dim;
                UiKit.ApplyRound(row.FreeClaim.image,
                    can ? UiKit.C_AccentTop : UiKit.C_TileDark,
                    can ? UiKit.C_AccentBottom : UiKit.C_TileDark, 19);
            }

            row.PremiumLabel.text = SeasonPass.PremiumRewardLabel(lvl);
            row.PremiumLabel.color = reached ? UiKit.C_Gold : UiKit.C_Dim;

            bool hasPremium = SeasonPass.HasPremiumReward(lvl);
            row.PremiumClaim.gameObject.SetActive(hasPremium);
            if (hasPremium)
            {
                bool can = SeasonPass.CanClaimPremium(lvl);
                if (!premium)
                {
                    row.PremiumClaimLabel.text = "Премиум";
                    row.PremiumClaimLabel.color = UiKit.C_Dim;
                    row.PremiumClaim.interactable = false;
                    UiKit.ApplyRound(row.PremiumClaim.image, UiKit.C_TileDark, UiKit.C_TileDark, 19);
                }
                else if (can)
                {
                    row.PremiumClaimLabel.text = "Забрать";
                    row.PremiumClaimLabel.color = UiKit.C_GoldDark;
                    row.PremiumClaim.interactable = true;
                    UiKit.ApplyRound(row.PremiumClaim.image, UiKit.C_GoldTop, UiKit.C_GoldBottom, 19);
                }
                else
                {
                    row.PremiumClaimLabel.text = "Забрать";
                    row.PremiumClaimLabel.color = UiKit.C_Dim;
                    row.PremiumClaim.interactable = false;
                    UiKit.ApplyRound(row.PremiumClaim.image, UiKit.C_TileDark, UiKit.C_TileDark, 19);
                }
            }
        }
    }

    private void Close()
    {
        System.Action handler = _onClosed;
        _onClosed = null;
        if (_root != null) Destroy(_root.gameObject);
        if (handler != null) handler();
    }
}
