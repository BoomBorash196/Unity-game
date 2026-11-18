#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Smoke-проверка процедурного UI мета-систем: строит все три оверлея
/// (батл-пасс, задания дня, ачивки) в edit-mode на временном канвасе и
/// проверяет, что каркас собрался: строки, заголовки, кнопки заявок.
///
/// Так панель проверяется дважды: как код (компиляция) и как построенный
/// объект (открытие панели = все вызовы UIFactory до первого кадра). Play mode
/// не нужен — оверлеи читают только статическую логику и PlayerPrefs.
///
/// Проверка ничего не заявляет и не меняет сохранение: открытие панели — это
/// чистое чтение. В конце канвас с оверлеями удаляется целиком.
/// </summary>
public static class MetaUISmokeCheck
{
    private static int _passed;
    private static int _failed;
    private static GameObject _canvas;

    public static int Check()
    {
        _passed = 0;
        _failed = 0;

        try
        {
            _canvas = new GameObject("Meta Smoke Canvas");
            RectTransform rt = _canvas.AddComponent<RectTransform>();
            Canvas canvas = _canvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            _canvas.AddComponent<CanvasScaler>();
            _canvas.AddComponent<GraphicRaycaster>();

            PassOverlay();
            QuestsOverlay();
            AchievementsOverlay();
        }
        finally
        {
            if (_canvas != null) Object.DestroyImmediate(_canvas);
            _canvas = null;
        }

        Debug.Log("[T] RESULT MetaUISmokeCheck passed=" + _passed + " failed=" + _failed);
        return _failed;
    }

    // ------------- батл-пасс -------------

    private static void PassOverlay()
    {
        PassUI.Open(_canvas.transform, null);
        Transform overlay = _canvas.transform.Find("Pass Overlay");
        Check("ui.pass opened", overlay != null, "оверлея нет");
        if (overlay == null) return;

        Transform panel = overlay.Find("Pass Panel");
        Check("ui.pass panel", panel != null, "панели нет");

        Transform content = FindChild(panel != null ? panel : overlay, "Scroll View/Viewport/Content");
        Check("ui.pass content", content != null, "скролл не собрался");

        int rows = 0;
        if (content != null)
        {
            for (int i = 0; i < content.childCount; i++)
                if (content.GetChild(i).name.StartsWith("Pass Row ")) rows++;
        }
        Check("ui.pass rows==30", rows == SeasonPass.LevelCount, "строк " + rows);

        Transform row10 = FindChild(panel, "Scroll View/Viewport/Content/Pass Row 10");
        if (row10 == null)
        {
            Check("ui.pass row10", false, "строки 10 нет");
        }
        else
        {
            Button[] buttons = row10.GetComponentsInChildren<Button>(true);
            Check("ui.pass row10 buttons==2", buttons.Length == 2, "кнопок " + buttons.Length);
            if (buttons.Length >= 1)
                Check("ui.pass row10 free matches logic",
                    buttons[0].interactable == SeasonPass.CanClaimFree(10),
                    "interactable=" + buttons[0].interactable);
            if (buttons.Length >= 2)
            {
                string text = buttons[1].GetComponentInChildren<Text>().text;
                bool ok = text == "Забрать" || text == "Премиум";
                Check("ui.pass row10 premium label", ok, "label='" + text + "'");
            }
        }

        bool headerOk = false;
        if (panel != null)
        {
            Text[] texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].text.StartsWith("Сезон ")) headerOk = true;
        }
        Check("ui.pass header", headerOk, "заголовка сезона нет");
    }

    // ------------- задания дня -------------

    private static void QuestsOverlay()
    {
        DailyQuestsUI.Open(_canvas.transform, null);
        Transform overlay = _canvas.transform.Find("Daily Quests Overlay");
        Check("ui.quests opened", overlay != null, "оверлея нет");
        if (overlay == null) return;

        Transform panel = overlay.Find("Quests Panel");
        Check("ui.quests panel", panel != null, "панели нет");

        int rows = 0;
        if (panel != null)
        {
            for (int i = 0; i < panel.childCount; i++)
                if (panel.GetChild(i).name.StartsWith("Quest ")) rows++;
        }
        Check("ui.quests rows==3", rows == DailyQuests.DailyCount, "строк " + rows);

        bool headerOk = false;
        int rewardRows = 0;
        bool claimsOk = panel != null;
        Text[] texts = panel != null ? panel.GetComponentsInChildren<Text>(true) : new Text[0];
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].text.StartsWith("Сегодня · ")) headerOk = true;
            if (texts[i].text.StartsWith("+") && texts[i].text.Contains("монет")) rewardRows++;
        }
        Check("ui.quests header", headerOk, "заголовка дня нет");
        Check("ui.quests rewards==3", rewardRows == DailyQuests.DailyCount, "строк наград " + rewardRows);

        if (claimsOk && panel != null)
        {
            for (int i = 0; i < panel.childCount; i++)
            {
                Transform child = panel.GetChild(i);
                if (!child.name.StartsWith("Quest ")) continue;
                if (child.GetComponentsInChildren<Button>(true).Length != 1) claimsOk = false;
            }
        }
        Check("ui.quests claim per row", claimsOk && panel != null, "кнопки заявок разошлись");
    }

    // ------------- ачивки -------------

    private static void AchievementsOverlay()
    {
        AchievementsUI.Open(_canvas.transform, null);
        Transform overlay = _canvas.transform.Find("Achievements Overlay");
        Check("ui.ach opened", overlay != null, "оверлея нет");
        if (overlay == null) return;

        Transform panel = overlay.Find("Achievements Panel");
        Check("ui.ach panel", panel != null, "панели нет");

        Transform content = FindChild(panel != null ? panel : overlay, "Scroll View/Viewport/Content");
        Check("ui.ach content", content != null, "скролл не собрался");

        int rows = 0;
        if (content != null)
        {
            for (int i = 0; i < content.childCount; i++)
                if (content.GetChild(i).name.StartsWith("Achievement ")) rows++;
        }
        Check("ui.ach rows==12", rows == Achievements.All.Count, "строк " + rows);

        bool headerOk = false;
        if (panel != null)
        {
            Text[] texts = panel.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
                if (texts[i].text.StartsWith("Выполнено ")) headerOk = true;
        }
        Check("ui.ach header", headerOk, "заголовка счётчика нет");

        bool rowsOk = content != null;
        if (rowsOk)
        {
            IList<AchievementDefinition> all = Achievements.All;
            for (int i = 0; i < all.Count && i < content.childCount; i++)
            {
                Transform row = content.GetChild(i);
                if (!row.name.StartsWith("Achievement ")) continue;
                Text[] texts = row.GetComponentsInChildren<Text>(true);
                bool hasReward = false;
                bool hasState = false;
                for (int j = 0; j < texts.Length; j++)
                {
                    if (texts[j].text.StartsWith("+") && texts[j].text.Contains("монет")) hasReward = true;
                    if (texts[j].text == "Выполнено" || texts[j].text == "Не выполнено") hasState = true;
                }
                if (!hasReward || !hasState) rowsOk = false;
            }
        }
        Check("ui.ach rows content", rowsOk, "строки ачивок неполные");
    }

    // ------------- утилиты -------------

    private static Transform FindChild(Transform root, string path)
    {
        if (root == null) return null;
        Transform current = root;
        string[] parts = path.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            Transform next = current.Find(parts[i]);
            if (next == null) return null;
            current = next;
        }
        return current;
    }

    private static void Check(string name, bool ok, string detail)
    {
        if (ok)
        {
            _passed++;
            Debug.Log("[T] PASS " + name);
        }
        else
        {
            _failed++;
            Debug.Log("[T] FAIL " + name + " :: " + detail);
        }
    }
}
#endif