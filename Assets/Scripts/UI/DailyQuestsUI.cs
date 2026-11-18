using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель ежедневных заданий: три задания дня с прогрессом и заявкой наград.
/// Обновляется по событию QuestsChanged (монеты собраны, забеги закрыты,
/// высота набрана — всё приходит из игры).
/// </summary>
public class DailyQuestsUI : MonoBehaviour
{
    private const float RowHeight = 140f;
    private const float RowGap = 12f;

    private class Row
    {
        public Image Background;
        public Text Title;
        public Text Progress;
        public Text Reward;
        public Button Claim;
        public Text ClaimLabel;
        public RectTransform ProgressFill;
    }

    private RectTransform _root;
    private System.Action _onClosed;
    private Text _header;
    private Text _status;
    private readonly Row[] _rows = new Row[DailyQuests.DailyCount];

    public static void Open(Transform parent, System.Action onClosed)
    {
        RectTransform rt = UiKit.Overlay(parent, "Daily Quests Overlay");

        DailyQuestsUI ui = rt.gameObject.AddComponent<DailyQuestsUI>();
        ui._root = rt;
        ui._onClosed = onClosed;
        ui.Build();
    }

    private void OnEnable()
    {
        MetaEvents.QuestsChanged += Refresh;
    }

    private void OnDisable()
    {
        MetaEvents.QuestsChanged -= Refresh;
    }

    private void Build()
    {
        RectTransform panel = UiKit.Modal(_root, "Quests Panel", 940f, 820f);

        _header = UiKit.Header(panel, "ЗАДАНИЯ ДНЯ", "", "check", UiKit.C_Green,
            UiKit.C_GreenTop, UiKit.C_GreenBottom, 900f);

        float y = 150f;
        for (int i = 0; i < DailyQuests.DailyCount; i++)
        {
            RectTransform rowRt = UiKit.Surface(panel, "Quest " + i, 40f, y, 900f, y + RowHeight,
                UiKit.C_RowTop, UiKit.C_RowBottom, 20);

            Row row = new Row();
            row.Background = rowRt.GetComponent<Image>();

            row.Title = UiKit.Label(rowRt, "", 26, UiKit.C_Text, 24f, 10f, 600f, 52f,
                TextAnchor.MiddleLeft, true);

            row.Progress = UiKit.Label(rowRt, "", 18, UiKit.C_Muted, 24f, 54f, 600f, 88f,
                TextAnchor.MiddleLeft, false);

            RectTransform bar = UiKit.Surface(rowRt, "Bar", 24f, 100f, 430f, 116f,
                MenuArt.Hex("0B1830"), MenuArt.Hex("0B1830"), 8, false);
            row.ProgressFill = UiKit.Surface(bar, "Fill", 0f, 0f, 0f, 16f,
                UiKit.C_AccentTop, UiKit.C_AccentBottom, 8, false);

            row.Reward = UiKit.Label(rowRt, "", 22, UiKit.C_Gold, 540f, 12f, 836f, 50f,
                TextAnchor.MiddleRight, true);

            row.Claim = UiKit.SurfaceButton(rowRt, "Claim", 660f, 62f, 836f, 122f,
                UiKit.C_GoldTop, UiKit.C_GoldBottom, null);
            row.ClaimLabel = UiKit.Label(row.Claim.transform, "Забрать", 22, UiKit.C_GoldDark,
                0f, 0f, 176f, 60f, TextAnchor.MiddleCenter, true);

            int slot = i;
            row.Claim.onClick.AddListener(delegate { OnClaim(slot); });

            _rows[i] = row;
            y += RowHeight + RowGap;
        }

        _status = UiKit.Label(panel, "", 20, UiKit.C_Green, 40f, 612f, 620f, 652f,
            TextAnchor.MiddleLeft, false);

        Text note = UiKit.Wrap(UiKit.Label(panel,
            "Задания обновляются каждый день — просто играй.",
            19, UiKit.C_Dim, 360f, 700f, 900f, 766f, TextAnchor.MiddleRight, false));

        Button close = UiKit.Back(panel, "Назад", 40f, 700f, 340f, 766f, Close);

        Refresh();
    }

    private void OnClaim(int index)
    {
        string message;
        DailyQuests.Claim(index, out message);
        if (_status != null) _status.text = message;
        Refresh();
    }

    private void Refresh()
    {
        if (_rows[0] == null) return;

        List<DailyQuest> quests = DailyQuests.List();
        if (_header != null)
        {
            _header.text = "Сегодня · " + DailyQuests.DateKey +
                           " · выполнено " + DailyQuests.CompletedUnclaimed() + " из " + quests.Count;
        }

        for (int i = 0; i < _rows.Length; i++)
        {
            Row row = _rows[i];
            if (i >= quests.Count)
            {
                UiKit.ApplyRound(row.Background, UiKit.C_Card, UiKit.C_CardDark, 20, false);
                row.Title.text = "";
                row.Progress.text = "";
                row.Reward.text = "";
                row.Claim.gameObject.SetActive(false);
                row.ProgressFill.sizeDelta = new Vector2(0f, 16f);
                continue;
            }

            DailyQuest quest = quests[i];
            bool complete = quest.IsComplete;

            UiKit.ApplyRound(row.Background,
                complete ? UiKit.C_GreenTop : UiKit.C_RowTop,
                complete ? UiKit.C_GreenBottom : UiKit.C_RowBottom, 20, false);

            row.Title.text = quest.Title;
            row.Progress.text = complete
                ? (quest.Claimed ? "Выполнено · награда получена" : "Выполнено! Нажми «Забрать»")
                : "Прогресс: " + quest.Progress + " / " + quest.Target;
            row.Progress.color = complete ? UiKit.C_Green : UiKit.C_Muted;
            row.Reward.text = "+" + quest.Reward + " монет";

            float ratio = quest.Target > 0 ? Mathf.Clamp01(quest.Progress / (float)quest.Target) : 0f;
            if (complete) ratio = 1f;
            row.ProgressFill.sizeDelta = new Vector2(406f * ratio, 16f);

            bool claimable = complete && !quest.Claimed;
            row.Claim.gameObject.SetActive(true);
            row.Claim.interactable = claimable;

            if (claimable)
            {
                UiKit.ApplyRound(row.Claim.image, UiKit.C_GoldTop, UiKit.C_GoldBottom, 20);
                row.ClaimLabel.color = UiKit.C_GoldDark;
            }
            else
            {
                UiKit.ApplyRound(row.Claim.image, UiKit.C_TileDark, UiKit.C_TileDark, 20);
                row.ClaimLabel.color = UiKit.C_Dim;
            }

            row.ClaimLabel.text = quest.Claimed ? "Получено" : "Забрать";
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
