using System;

/// <summary>
/// Событийная шина мета-систем (батл-пасс, дейлики, ачивки). Без неё каждый
/// экран подписывался бы на все статические классы по отдельности, а эти классы
/// — друг на друга. Все подписчики отписываются при уничтожении UI, потому что
/// события статические и живут дольше любой сцены.
/// </summary>
public static class MetaEvents
{
    public static event Action PassChanged;
    public static event Action QuestsChanged;
    public static event Action AchievementsChanged;
    public static event Action<AchievementDefinition> AchievementUnlocked;

    public static void RaisePassChanged()
    {
        Action handler = PassChanged;
        if (handler != null) handler();
    }

    public static void RaiseQuestsChanged()
    {
        Action handler = QuestsChanged;
        if (handler != null) handler();
    }

    public static void RaiseAchievementsChanged()
    {
        Action handler = AchievementsChanged;
        if (handler != null) handler();
    }

    public static void RaiseAchievementUnlocked(AchievementDefinition achievement)
    {
        Action<AchievementDefinition> handler = AchievementUnlocked;
        if (handler != null) handler(achievement);
    }
}