using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Окно паузы: продолжение, возрождение у чекпоинта, перезапуск и меню плюс
/// быстрое путешествие по уже открытым точкам.
///
/// Оформлено теми же блоками UiKit, что и меню: затемнение, скруглённая
/// карточка с градиентом, синяя кнопка продолжения, золотые акценты на
/// чекпоинтах. Список точек строится заново при каждом открытии — между
/// паузами игрок успевает подняться выше, и старые строки уже неверны.
/// </summary>
public class PauseOverlay : MonoBehaviour
{
    public bool IsVisible { get; private set; }

    private LevelManager _manager;
    private RectTransform _before;
    private RectTransform _after;
    private Text _subtitle;

    public static PauseOverlay Build(Transform parent, LevelManager manager)
    {
        RectTransform root = UiKit.Overlay(parent, "Pause Overlay");
        PauseOverlay overlay = root.gameObject.AddComponent<PauseOverlay>();
        overlay._manager = manager;

        RectTransform panel = UiKit.Modal(root, "Pause Panel", 1300f, 960f);

        UiKit.Label(panel, "ПАУЗА", 44, UiKit.C_Text, 0f, 24f, 1300f, 92f,
            TextAnchor.MiddleCenter, true);

        overlay._subtitle = UiKit.Label(panel, "", 20, UiKit.C_Muted, 0f, 88f, 1300f, 122f,
            TextAnchor.MiddleCenter, false);

        // ── левая колонка: путешествие по чекпоинтам ────────────────────────
        UiKit.Label(panel, "ПУТЕШЕСТВИЕ", 24, UiKit.C_Text, 44f, 144f, 616f, 180f,
            TextAnchor.MiddleLeft, true);
        UiKit.Label(panel, "Выбери точку, с которой продолжить", 18, UiKit.C_Muted,
            44f, 178f, 616f, 206f, TextAnchor.MiddleLeft, false);

        overlay._before = UIFactory.Panel(panel, "Travel Before", new Color(0f, 0f, 0f, 0f));
        UIFactory.SetRect(overlay._before, 44f, 210f, 616f, 540f);

        overlay._after = UIFactory.Panel(panel, "Travel After", new Color(0f, 0f, 0f, 0f));
        UIFactory.SetRect(overlay._after, 44f, 552f, 616f, 882f);

        // ── правая колонка: действия ────────────────────────────────────────
        UiKit.Label(panel, "ДЕЙСТВИЯ", 24, UiKit.C_Text, 680f, 144f, 1256f, 180f,
            TextAnchor.MiddleLeft, true);
        UiKit.Label(panel, "Прогресс сохраняется автоматически", 18, UiKit.C_Muted,
            680f, 178f, 1256f, 206f, TextAnchor.MiddleLeft, false);

        ActionButton(panel, "Продолжить", "play", 210f,
            UiKit.C_AccentTop, UiKit.C_AccentBottom, UiKit.C_Text, () =>
            {
                if (manager != null) manager.Pause();
            });

        ActionButton(panel, "Возродиться у чекпоинта", "flag", 310f,
            UiKit.C_Tile, UiKit.C_TileDark, UiKit.C_Text, () =>
            {
                if (manager != null)
                {
                    manager.RespawnPlayer();
                    manager.Pause();
                }
            });

        ActionButton(panel, "Перезапустить уровень", "arrow", 410f,
            UiKit.C_Tile, UiKit.C_TileDark, UiKit.C_Text, () =>
            {
                if (manager != null) manager.RestartLevel();
            });

        ActionButton(panel, "В главное меню", "home", 510f,
            MenuArt.Hex("3A1F2A"), MenuArt.Hex("2A1620"), MenuArt.Hex("FFB4C0"), () =>
            {
                if (manager != null) manager.ToMenu();
            });

        Text note = UiKit.Label(panel,
            "Возрождение вернёт к последнему чекпоинту. Монеты, уровень пасса и открытые точки останутся.",
            20, UiKit.C_Muted, 680f, 620f, 1256f, 720f, TextAnchor.UpperLeft, false);
        UiKit.Wrap(note);

        UiKit.Label(panel, "ESC — продолжить игру", 18, UiKit.C_Dim,
            680f, 760f, 1256f, 792f, TextAnchor.MiddleLeft, false);

        return overlay;
    }

    private static void ActionButton(Transform panel, string label, string iconId, float y0,
        Color top, Color bottom, Color textColor, UnityAction onClick)
    {
        Button b = UiKit.SurfaceButton(panel, "Pause " + label, 680f, y0, 1256f, y0 + 84f,
            top, bottom, onClick, 22);
        UiKit.Icon(b.transform, iconId, textColor, 22f, 22f, 62f, 62f);
        UiKit.Label(b.transform, label, 26, textColor, 78f, 0f, 566f, 84f,
            TextAnchor.MiddleLeft, true);
    }

    private void Refresh()
    {
        _subtitle.text = "Высота: " + Mathf.FloorToInt(_manager != null ? _manager.LevelHeight : 0f) +
                         " м · Рекорд: " + GameProgress.GetBestHeight() + " м";

        DestroyChildren(_before);
        DestroyChildren(_after);
        BuildTravelGroup(_before, _manager, "Ниже текущей высоты", false);
        BuildTravelGroup(_after, _manager, "Выше текущей высоты", true);
    }

    private static void DestroyChildren(RectTransform container)
    {
        if (container == null) return;
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            GameObject child = container.GetChild(i).gameObject;
            // Выключаем сразу: Destroy сработает в конце кадра, и без этого
            // старые строки на один кадр наложились бы на новые.
            child.SetActive(false);
            Destroy(child);
        }
    }

    private static void BuildTravelGroup(RectTransform container, LevelManager manager,
        string label, bool after)
    {
        UiKit.Label(container, label, 18, UiKit.C_Muted, 0f, 0f, 572f, 28f,
            TextAnchor.MiddleLeft, false);

        List<int> checkpoints = Collect(manager, after, 5);

        for (int i = 0; i < 5; i++)
        {
            float top = 34f + i * 60f;

            if (i < checkpoints.Count)
            {
                int cp = checkpoints[i];
                int height = Mathf.FloorToInt(GameProgress.GetCheckpointHeight(cp));
                Button b = UiKit.SurfaceButton(container, "Point " + cp, 0f, top, 572f, top + 52f,
                    UiKit.C_RowTop, UiKit.C_RowBottom, () =>
                    {
                        if (GameManager.Instance != null)
                            GameManager.Instance.StartFromCheckpoint(cp);
                    }, 16);
                UiKit.Icon(b.transform, "flag", UiKit.C_Gold, 14f, 10f, 46f, 42f);
                UiKit.Label(b.transform, "Точка " + cp + " — " + height + " м", 21, UiKit.C_Text,
                    58f, 0f, 560f, 52f, TextAnchor.MiddleLeft, false);
            }
            else if (i == 0)
            {
                string text = after ? "нет доступных чекпоинтов выше" : "нет чекпоинтов ниже";
                UiKit.Label(container, text, 18, UiKit.C_Dim, 6f, top + 10f, 566f, top + 42f,
                    TextAnchor.MiddleLeft, false);
            }
        }
    }

    private static List<int> Collect(LevelManager manager, bool after, int count)
    {
        List<int> result = new List<int>();
        if (manager == null)
            return result;

        int best = GameProgress.GetBestCheckpoint();
        float currentHeight = manager.LevelHeight;
        float margin = 1f;

        if (after)
        {
            for (int cp = 1; cp <= best; cp++)
            {
                if (GameProgress.GetCheckpointHeight(cp) > currentHeight + margin)
                {
                    result.Add(cp);
                    if (result.Count >= count)
                        break;
                }
            }
        }
        else
        {
            for (int cp = best; cp >= 1; cp--)
            {
                if (GameProgress.GetCheckpointHeight(cp) < currentHeight - margin)
                {
                    result.Add(cp);
                    if (result.Count >= count)
                        break;
                }
            }
        }
        return result;
    }

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        Refresh();
        IsVisible = true;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        IsVisible = false;
        gameObject.SetActive(false);
    }
}
