using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Таблица рекордов высоты из Яндекс Игр.</summary>
public class LeaderboardUI : MonoBehaviour
{
    private const float RowHeight = 58f;
    private const float RowGap = 6f;

    private RectTransform _root;
    private System.Action _onClosed;
    private Text _status;
    private RectTransform _content;
    private readonly List<Row> _rows = new List<Row>();

    private class Row
    {
        public GameObject Root;
        public Image Background;
        public Image RankTile;
        public Text Rank;
        public Text Name;
        public Text Score;
    }

    public static void Open(Transform parent, System.Action onClosed)
    {
        RectTransform rt = UiKit.Overlay(parent, "Leaderboard Overlay");

        LeaderboardUI ui = rt.gameObject.AddComponent<LeaderboardUI>();
        ui._root = rt;
        ui._onClosed = onClosed;
        ui.Build();
        YandexLeaderboard.Fetch();
    }

    private void OnEnable()
    {
        YandexLeaderboard.Loaded += OnLoaded;
        YandexLeaderboard.Failed += OnFailed;
    }

    private void OnDisable()
    {
        YandexLeaderboard.Loaded -= OnLoaded;
        YandexLeaderboard.Failed -= OnFailed;
    }

    private void Build()
    {
        RectTransform panel = UiKit.Modal(_root, "Leaderboard Panel", 900f, 900f);

        _status = UiKit.Header(panel, "РЕЙТИНГ", "Загрузка...", "trophy", UiKit.C_Gold,
            UiKit.C_GoldDark, MenuArt.Hex("1B1706"), 860f);

        ScrollRect scroll = UiKit.Scroll(panel, 40f, 150f, 860f, 790f);
        _content = scroll.content;

        for (int i = 0; i < 16; i++) CreateRow(i);

        Button close = UiKit.Back(panel, "Назад", 300f, 814f, 600f, 878f, Close);
    }

    private void CreateRow(int index)
    {
        RectTransform rowRt = UIFactory.Row(_content, RowHeight, RowGap, _rows.Count, 12f);
        rowRt.gameObject.name = "LB Row " + index;

        Row row = new Row();
        row.Root = rowRt.gameObject;
        row.Background = UiKit.RowBackground(rowRt, UiKit.C_Card, UiKit.C_CardDark);

        RectTransform tile = UiKit.Surface(rowRt, "Rank Tile", 10f, 9f, 54f, 49f,
            UiKit.C_TileDark, MenuArt.Hex("0B1830"), 13, false);
        row.RankTile = tile.GetComponent<Image>();

        row.Rank = UiKit.Label(tile, "", 20, UiKit.C_Text, 0f, 0f, 44f, 40f,
            TextAnchor.MiddleCenter, true);

        row.Name = UiKit.Label(rowRt, "", 21, UiKit.C_Text, 66f, 10f, 560f, 48f,
            TextAnchor.MiddleLeft, false);

        row.Score = UiKit.Label(rowRt, "", 21, UiKit.C_Gold, 560f, 10f, 784f, 48f,
            TextAnchor.MiddleRight, true);

        row.Root.SetActive(false);
        _rows.Add(row);
    }

    private void OnLoaded(List<YandexLeaderboard.Entry> list)
    {
        _status.text = list.Count > 0 ? "ТОП " + Mathf.Min(10, list.Count) + " по высоте забега" : "Пока пусто";

        for (int i = 0; i < _rows.Count; i++)
            _rows[i].Root.SetActive(false);

        for (int i = 0; i < list.Count && i < _rows.Count; i++)
        {
            var e = list[i];
            Row row = _rows[i];
            row.Root.SetActive(true);
            row.Rank.text = e.Rank.ToString();
            row.Name.text = e.Name;
            row.Score.text = e.Score + " м";

            Color top;
            Color bottom;
            Color rankColor;
            if (e.Rank == 1)
            {
                top = MenuArt.Hex("3A2C10"); bottom = MenuArt.Hex("2A2008");
                rankColor = UiKit.C_Gold;
            }
            else if (e.Rank == 2)
            {
                top = MenuArt.Hex("2C3450"); bottom = MenuArt.Hex("202840");
                rankColor = MenuArt.Hex("C7D3E8");
            }
            else if (e.Rank == 3)
            {
                top = MenuArt.Hex("3A2A20"); bottom = MenuArt.Hex("2A1E16");
                rankColor = MenuArt.Hex("D79A66");
            }
            else
            {
                top = UiKit.C_Card; bottom = UiKit.C_CardDark;
                rankColor = UiKit.C_Muted;
            }

            UiKit.ApplyRound(row.Background, top, bottom, 18, false);
            UiKit.ApplyRound(row.RankTile,
                Color.Lerp(rankColor, Color.black, 0.70f),
                Color.Lerp(rankColor, Color.black, 0.82f), 13, false);
            row.Rank.color = rankColor;
            row.Name.color = e.Rank <= 3 ? UiKit.C_Text : UiKit.C_Muted;
        }

        _content.sizeDelta = new Vector2(0f, 16f + list.Count * (RowHeight + RowGap));
    }

    private void OnFailed()
    {
        _status.text = "Не удалось загрузить рейтинг";
    }

    private void Close()
    {
        System.Action handler = _onClosed;
        _onClosed = null;
        if (_root != null) Destroy(_root.gameObject);
        handler?.Invoke();
    }
}
