using UnityEngine;

public class PlayerRespawn : MonoBehaviour
{
    public PlayerController Player;
    public CameraFollow Camera;
    public Transform CheckpointAnchor;
    public Vector3 CheckpointPosition;

    /// <summary>Поднимается после любого респавна (смерть или ручная точка).
    /// На нём живёт мета-счётчик отрезков забега.</summary>
    public event System.Action Respawned;

    /// <summary>Последний респавн был смертью (падение за KillY), а не ручной
    /// телепортацией. Читается сразу после Respawned.</summary>
    public bool LastRespawnWasDeath { get; private set; }

    private float _killY;
    private bool _dying;

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            return;
        if (_dying) return;
        if (transform.position.y < _killY)
        {
            _dying = true;
            LastRespawnWasDeath = true;
            Respawn();
        }
    }

    public void SetKillY(float killY)
    {
        _killY = killY;
    }

    public void SetCheckpoint(Vector3 worldPosition, bool updateKillY)
    {
        CheckpointPosition = worldPosition;
        if (updateKillY && worldPosition.y - 14f > _killY)
            _killY = worldPosition.y - 14f;
    }

    public Vector3 RespawnPoint()
    {
        return CheckpointPosition;
    }

    public void Respawn()
    {
        LastRespawnWasDeath = _dying;
        _dying = false;

        Player.ResetTo(RespawnPoint());
        if (Camera != null)
            Camera.Snap();

        SkinAnimator animator = GetComponentInChildren<SkinAnimator>();
        if (animator != null)
            animator.ResetPose();

        System.Action handler = Respawned;
        if (handler != null) handler();
    }
}
