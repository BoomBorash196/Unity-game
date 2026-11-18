using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    public int Index;
    public float Height;
    public bool IsActive { get; private set; }

    private Renderer _renderer;
    private GameObject _probe;

    public Vector3 RespawnPosition
    {
        get { return transform.position - Vector3.up * 0.4f; }
    }

    public void Init(int index, float height)
    {
        Index = index;
        Height = height;
    }

    private void Awake()
    {
        _renderer = GetComponentInParent<Renderer>();
        _probe = gameObject;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsActive) return;
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        LevelManager.Instance.ReachedCheckpoint(this);
    }

    public void Activate()
    {
        IsActive = true;
        if (_renderer != null)
            _renderer.material = Palette.CheckpointActive();
    }

    public void SetCollectable(bool collectable)
    {
        if (_probe != null)
            _probe.SetActive(collectable);
        if (_renderer != null)
        {
            _renderer.material = IsActive
                ? Palette.CheckpointActive()
                : (collectable ? Palette.Checkpoint() : Palette.CheckpointLocked());
        }
    }
}