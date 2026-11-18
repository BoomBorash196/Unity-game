using System.Collections.Generic;
using UnityEngine;

public static class Palette
{
    private static readonly Color StartColor = new Color(0.25f, 0.85f, 0.45f, 1f);
    private static readonly Color LowColor = new Color(0.35f, 0.62f, 0.95f, 1f);
    private static readonly Color HighColor = new Color(0.95f, 0.38f, 0.72f, 1f);
    private static readonly Color CoinColor = new Color(1f, 0.76f, 0.2f, 1f);

    private static Material _playerMaterial;
    private static Material _checkpointMaterial;
    private static Material _checkpointActiveMaterial;
    private static Material _checkpointLockedMaterial;
    private static Material _coinMaterial;
    private static Material[] _platformCache;
    private static readonly Dictionary<int, Material> _solidCache = new Dictionary<int, Material>();
    private static readonly Dictionary<int, Material> _texturedCache = new Dictionary<int, Material>();

    public static Material Player()
    {
        if (_playerMaterial == null)
            _playerMaterial = Make(new Color(0.2f, 0.85f, 1f, 1f));
        return _playerMaterial;
    }

    public static Material Checkpoint()
    {
        if (_checkpointMaterial == null)
            _checkpointMaterial = Make(new Color(1f, 0.82f, 0.25f, 1f));
        return _checkpointMaterial;
    }

    public static Material CheckpointActive()
    {
        if (_checkpointActiveMaterial == null)
            _checkpointActiveMaterial = Make(new Color(0.35f, 1f, 0.45f, 1f));
        return _checkpointActiveMaterial;
    }

    public static Material CheckpointLocked()
    {
        if (_checkpointLockedMaterial == null)
            _checkpointLockedMaterial = Make(new Color(0.5f, 0.52f, 0.56f, 1f));
        return _checkpointLockedMaterial;
    }

    public static Material Coin()
    {
        if (_coinMaterial == null)
            _coinMaterial = Make(CoinColor);
        return _coinMaterial;
    }

    public static Color CoinTint()
    {
        return CoinColor;
    }

    /// <summary>Плоский однотонный материал с кэшем — для скинов, монет и любых деталей.</summary>
    public static Material Solid(Color color)
    {
        int key = ColorKey(color);
        Material cached;
        if (_solidCache.TryGetValue(key, out cached) && cached != null)
            return cached;

        Material mat = Make(color);
        _solidCache[key] = mat;
        return mat;
    }

    public static Material Platform(float progress01)
    {
        progress01 = Mathf.Clamp01(progress01);
        int tier = Mathf.FloorToInt(progress01 * 16f);
        if (tier < 0) tier = 0;
        if (tier > 15) tier = 15;

        if (_platformCache == null)
            _platformCache = new Material[16];

        if (_platformCache[tier] == null)
        {
            float t = tier / 15f;
            Color color = Color.Lerp(LowColor, HighColor, t);
            if (tier == 0)
                color = Color.Lerp(StartColor, LowColor, 0.35f);
            _platformCache[tier] = Make(color);
        }
        return _platformCache[tier];
    }

    private static int ColorKey(Color color)
    {
        Color32 c = color;
        return (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
    }

    /// <summary>
    /// Материал с текстурой для импортированных скинов: diffuse + normal, кэш по паре текстур.
    /// Если текстуры нет — берётся однотонный материал цвета текстуры не существует.
    /// </summary>
    public static Material Textured(Texture2D diffuse, Texture2D normal)
    {
        int key = (diffuse != null ? diffuse.GetInstanceID() : 0) * 397 ^ (normal != null ? normal.GetInstanceID() : 0);
        Material cached;
        if (_texturedCache.TryGetValue(key, out cached) && cached != null)
            return cached;

        Material mat;
        if (diffuse == null)
        {
            mat = Make(Color.white);
        }
        else
        {
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
            mat = new Material(shader);
            mat.mainTexture = diffuse;
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetFloat("_BumpScale", 1f);
            }
        }

        _texturedCache[key] = mat;
        return mat;
    }

    private static Material Make(Color color)
    {
        Shader shader = Shader.Find("Standard") ?? Shader.Find("Diffuse");
        Material mat = new Material(shader);
        mat.color = color;
        return mat;
    }
}
