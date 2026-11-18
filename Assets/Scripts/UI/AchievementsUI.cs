using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Список ачивок: условия, состояние и награды. Живой список — события
/// AchievementUnlocked приходят и в геймплее, и в меню, всё обновляется само.
/// </summary>
public class AchievementsUI : MonoBehaviour
{
    private const float RowHeight = 88f;
    private const float RowGap = 8f;

    private class Row
    {
        public Image Background;
        public Image Icon;
        public Text Title;
        public Text Description;
        public Text Reward;
        public Text State;
    }

    private RectTransform _root;
    private System.Action _onClosed;
    private Text _header;
    private RectTransform _content;
    private readonly List<Row> _rows = new List<Row>();

    public static void Open(Transform parent, System.Action onClosed)
    {
        RectTransform rt = UiKit.Overlay(parent, "Achievements Overlay");

        AchievementsUI ui = rt.gameObject.AddComponent<AchievementsUI>();
        ui._root = rt;
        ui._onClosed = onClosed;
        ui.Build();
    }

    private void OnEnable()
    {
        MetaEvents.AchievementsChanged += Refresh;
    }

    private void OnDisable()
    {
        MetaEvents.AchievementsChanged -= Refresh;
    }

    private void Build()
    {
        RectTransform panel = UiKit.Modal(_root, "Achievements Panel", 1040f, 920f);

        _header = UiKit.Header(panel, "ДОСТИЖЕНИЯ", "", "trophy", UiKit.C_Gold,
            UiKit.C_GoldDark, MenuArt.Hex("1B1706"), 1000f);

        ScrollRect scroll = UiKit.Scroll(panel, 40f, 150f, 1000f, 810f);
        _content = scroll.content;

        IList<AchievementDefinition> all = Achievements.All;
        for (int i = 0; i < all.Count; i++)
            CreateRow();

        Button close = UiKit.Back(panel, "Назад", 40f, 836f, 340f, 900f, Close);

        Text hint = UiKit.Wrap(UiKit.Label(panel,
            "Монеты за достижения начисляются сразу при выполнении.",
            19, UiKit.C_Dim, 380f, 836f, 1000f, 900f, TextAnchor.MiddleRight, false));

        Refresh();
    }

    private void CreateRow()
    {
        RectTransform rowRt = UIFactory.Row(_content, RowHeight, RowGap, _rows.Count, 16f);
        rowRt.gameObject.name = "Achievement " + _rows.Count;

        Row row = new Row();
        row.Background = UiKit.RowBackground(rowRt, UiKit.C_RowTop, UiKit.C_RowBottom);

        RectTransform tile = UiKit.Surface(rowRt, "Icon Tile", 14f, 22f, 58f, 66f,
            UiKit.C_TileDark, MenuArt.Hex("0B1830"), 13, false);
        row.Icon = UiKit.Icon(tile, "trophy", UiKit.C_Dim, 8f, 8f, 36f, 36f);

        row.Title = UiKit.Label(rowRt, "", 24, UiKit.C_Text, 74f, 8f, 540f, 48f,
            TextAnchor.MiddleLeft, true);

        row.Description = UiKit.Wrap(UiKit.Label(rowRt, "", 18, UiKit.C_Dim, 74f, 50f, 640f, 80f,
            TextAnchor.MiddleLeft, false));

        row.Reward = UiKit.Label(rowRt, "", 20, UiKit.C_Gold, 640f, 28f, 790f, 62f,
            TextAnchor.MiddleRight, true);

        row.State = UiKit.Label(rowRt, "", 20, UiKit.C_Dim, 794f, 28f, 912f, 62f,
            TextAnchor.MiddleRight, false);

        _rows.Add(row);
    }

    private void Refresh()
    {
        if (_content == null || _rows.Count == 0) return;

        IList<AchievementDefinition> all = Achievements.All;
        _content.sizeDelta = new Vector2(0f, 10f + all.Count * (RowHeight + RowGap));
        if (_header != null)
        {
            _header.text = "Выполнено " + Achievements.UnlockedCount() + " из " + all.Count;
        }

        for (int i = 0; i < _rows.Count && i < all.Count; i++)
        {
            Row row = _rows[i];
            AchievementDefinition def = all[i];
            bool done = Achievements.IsUnlocked(def.Id);

            UiKit.ApplyRound(row.Background,
                done ? UiKit.C_GreenTop : UiKit.C_RowTop,
                done ? UiKit.C_GreenBottom : UiKit.C_RowBottom, 18, false);

            row.Icon.sprite = MenuIcons.Get(done ? "check" : "trophy",
                done ? UiKit.C_Green : UiKit.C_Dim);

            row.Title.text = def.Title;
            row.Title.color = done ? UiKit.C_Text : UiKit.C_Muted;
            row.Description.text = def.Description;
            row.Description.color = done ? MenuArt.Hex("9FD8BB") : UiKit.C_Dim;
            row.Reward.text = "+" + def.Reward + " монет";
            row.State.text = done ? "Выполнено" : "Не выполнено";
            row.State.color = done ? UiKit.C_Green : UiKit.C_Dim;
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
