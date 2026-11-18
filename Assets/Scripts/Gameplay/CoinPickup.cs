using System.Collections.Generic;
using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    private const float SpinSpeed = 220f;
    private const float BobSpeed = 1.6f;
    private const float BobAmount = 0.12f;
    private const float PickupRadius = 0.6f;

    // Монеты появляются и исчезают каждые несколько платформ. Держим
    // невидимые экземпляры, чтобы не создавать примитив-цилиндр заново.
    private static readonly Stack<CoinPickup> _pool = new Stack<CoinPickup>();

    private Transform _visual;
    private float _phase;
    private bool _taken;
    private bool _pooled;

    public static CoinPickup Create(Transform parent, Vector3 worldPosition)
    {
        CoinPickup pickup = null;
        while (_pool.Count > 0)
        {
            pickup = _pool.Pop();
            if (pickup != null) break;
            pickup = null;
        }

        if (pickup == null)
        {
            pickup = BuildNew();
        }

        pickup._taken = false;
        pickup._pooled = false;
        pickup._phase = Random.Range(0f, 6.28f);
        pickup.transform.SetParent(parent, true);
        pickup.transform.position = worldPosition;
        pickup.gameObject.SetActive(true);
        return pickup;
    }

    private static CoinPickup BuildNew()
    {
        GameObject go = new GameObject("Coin");

        SphereCollider trigger = go.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = PickupRadius;

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Visual";
        disc.transform.SetParent(go.transform, false);
        disc.transform.localScale = new Vector3(0.5f, 0.05f, 0.5f);

        Collider collider = disc.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        Renderer renderer = disc.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = Palette.Coin();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        CoinPickup pickup = go.AddComponent<CoinPickup>();
        pickup._visual = disc.transform;
        return pickup;
    }

    /// <summary>Возвращает монету в пул. Повторный вызов безопасен: объект
    /// может быть уже возвращён после подбора, а LevelGenerator ещё хранит
    /// ссылку на него до ReleaseBelow.</summary>
    public void ReturnToPool()
    {
        if (_pooled || this == null) return;
        _pooled = true;
        _taken = true;
        gameObject.SetActive(false);
        _pool.Push(this);
    }

    /// <summary>Сбрасывает пул перед загрузкой другой сцены. Сами неактивные
    /// объекты принадлежат выгружаемой сцене и будут уничтожены вместе с ней.</summary>
    public static void ClearPool()
    {
        _pool.Clear();
    }

    private void Update()
    {
        if (_visual == null) return;

        _phase += Time.deltaTime;
        _visual.localRotation = Quaternion.Euler(90f, _phase * SpinSpeed, 0f);
        _visual.localPosition = new Vector3(0f, Mathf.Sin(_phase * BobSpeed) * BobAmount, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_taken || other == null) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        _taken = true;
        SkinService.AddCoins(1);
        MetaSystems.RegisterCoinPicked(1);

        if (LevelManager.Instance != null)
            LevelManager.Instance.NotifyCoins(1);

        ReturnToPool();
    }
}
