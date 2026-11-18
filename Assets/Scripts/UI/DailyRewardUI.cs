using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель ежедневной награды: недельная полоса из 7 дней и кнопка получения.
/// Открывается из главного меню и всегда показывает весь круг целиком —
/// игроку видно, до чего осталось дойти, а не только что он может забрать сейчас.
/// </summary>
public class DailyRewardUI : MonoBehaviour
{
    private const float CardTop = 158f;
    private const float CardHeight = 242f;
    private const float CardGap = 8f;

    private RectTransform _root;
    private Action _onClaimed;
    private Text _header;
    private Text _result;
    private Button _claim;
    private Text _claimLabel;

    private readonly Image[] _rowBackgrounds = new Image[DailyReward.CycleLength];
    private readonly Text[] _rowNames = new Text[DailyReward.CycleLength];
    private readonly Text[] _rowValues = new Text[DailyReward.CycleLength];
    private readonly Image[] _rowIcons = new Image[DailyReward.CycleLength];
    private readonly Text[] _rowBadges = new Text[DailyReward.CycleLength];

    /// <summary>Открывает панель. onClaimed вызывается после успешного получения.</summary>
    public static void Open(Transform parent, Action onClaimed)
    {
        RectTransform rt = UiKit.Overlay(parent, "DailyReward Overlay");

        DailyRewardUI ui = rt.gameObject.AddComponent<DailyRewardUI>();
        ui._root = rt;
        ui._onClaimed = onClaimed;
        ui.Build();
    }

    private void Build()
    {
        RectTransform panel = UiKit.Modal(_root, "Daily Panel", 980f, 700f);

        _header = UiKit.Header(panel, "ЕЖЕДНЕВНАЯ НАГРАДА", "", "gift", UiKit.C_Gold,
            UiKit.C_GoldDark, MenuArt.Hex("1B1706"), 930f);

        float cardW = (900f - (DailyReward.CycleLength - 1) * CardGap) / DailyReward.CycleLength;
        for (int day = 1; day <= DailyReward.CycleLength; day++)
        {
            float x = 40f + (day - 1) * (cardW + CardGap);
            RectTransform card = UiKit.Surface(panel, "Day " + day, x, CardTop, x + cardW,
                CardTop + CardHeight, UiKit.C_Tile, UiKit.C_Card, 22);
            int slot = day - 1;

            _rowBackgrounds[slot] = card.GetComponent<Image>();

            _rowNames[slot] = UiKit.Label(card, "День " + day, 18, UiKit.C_Text,
                0f, 10f, cardW, 42f, TextAnchor.MiddleCenter, true);

            float iconX = (cardW - 42f) * 0.5f;
            _rowIcons[slot] = UiKit.Icon(card, "gift", UiKit.C_Soft,
                iconX, 50f, iconX + 42f, 92f);

            _rowValues[slot] = UiKit.Wrap(UiKit.Label(card, "", 18, UiKit.C_Gold,
                6f, 96f, cardW - 6f, 182f, TextAnchor.MiddleCenter, true));

            _rowBadges[slot] = UiKit.Label(card, "", 13, UiKit.C_Muted,
                4f, 196f, cardW - 4f, 228f, TextAnchor.MiddleCenter, true);
        }

        _result = UiKit.Label(panel, "", 22, UiKit.C_Green, 40f, 410f, 940f, 452f,
            TextAnchor.MiddleCenter, false);

        _claim = UiKit.SurfaceButton(panel, "Claim", 40f, 462f, 940f, 542f,
            UiKit.C_GoldTop, UiKit.C_GoldBottom, OnClaimClicked);
        _claimLabel = UiKit.Label(_claim.transform, "", 27, UiKit.C_GoldDark,
            0f, 0f, 900f, 80f, TextAnchor.MiddleCenter, true);

        Button close = UiKit.Back(panel, "Назад", 330f, 566f, 650f, 632f, Close);

        Refresh();
    }

    private void Refresh()
    {
        int today = DailyReward.DayOfCycle;
        bool claimed = DailyReward.ClaimedToday;

        _header.text = "Круг " + DailyReward.Cycle + " · день " + today + " из " + DailyReward.CycleLength +
                       (claimed ? " · уже получено" : " · можно забрать");

        for (int day = 1; day <= DailyReward.CycleLength; day++)
        {
            int slot = day - 1;
            bool isSkin = day == DailyReward.SkinDay;
            bool isPast = day < today;
            bool isToday = day == today;

            if (isToday)
            {
                UiKit.ApplyRound(_rowBackgrounds[slot],
                    isSkin ? MenuArt.Hex("4A3814") : UiKit.C_GreenTop,
                    isSkin ? MenuArt.Hex("33270D") : UiKit.C_GreenBottom, 22);
            }
            else
            {
                UiKit.ApplyRound(_rowBackgrounds[slot],
                    isPast ? UiKit.C_CardDark : UiKit.C_Tile,
                    isPast ? MenuArt.Hex("0A1428") : UiKit.C_Card, 22);
            }

            _rowNames[slot].text = "День " + day;
            _rowNames[slot].color = isPast ? UiKit.C_Dim : (isToday ? UiKit.C_Text : UiKit.C_Muted);

            _rowValues[slot].text = DailyReward.RewardLabel(day);
            _rowValues[slot].color = isPast ? UiKit.C_Dim : (isToday ? UiKit.C_Gold : UiKit.C_Soft);

            _rowBadges[slot].text = isToday ? "СЕГОДНЯ" : (isPast ? "ПОЛУЧЕНО" : "");
            _rowBadges[slot].color = isToday ? UiKit.C_Gold : UiKit.C_Dim;

            Color iconColor = isToday ? UiKit.C_Gold : (isPast ? UiKit.C_Dim : UiKit.C_Soft);
            _rowIcons[slot].sprite = MenuIcons.Get(isSkin ? "star" : "gift", iconColor);
        }

        bool canClaim = DailyReward.CanClaim;
        _claim.interactable = canClaim;
        _claimLabel.text = DailyReward.ClaimLabel();
        _claimLabel.color = canClaim ? UiKit.C_GoldDark : UiKit.C_Dim;
        UiKit.ApplyRound(_claim.image,
            canClaim ? UiKit.C_GoldTop : UiKit.C_TileDark,
            canClaim ? UiKit.C_GoldBottom : UiKit.C_TileDark, 40);
    }

    private void OnClaimClicked()
    {
        string message;
        if (!DailyReward.Claim(out message))
        {
            if (_result != null) _result.text = message;
            return;
        }

        if (_result != null) _result.text = message;
        // После получения набор дней сдвигается, поэтому строки пересобираются:
        // размеры и цвета считаются от DayOfCycle, он как раз изменился.
        Refresh();

        if (_onClaimed != null) _onClaimed();
    }

    private void Close()
    {
        if (_root != null) Destroy(_root.gameObject);
    }
}
