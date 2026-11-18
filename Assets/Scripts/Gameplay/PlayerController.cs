using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float MoveSpeed = PlayerPhysics.RunSpeed;
    public float Gravity = PlayerPhysics.Gravity;
    public float JumpHeight = PlayerPhysics.JumpHeight;

    [Header("Timing")]
    public float CoyoteTime = 0.12f;
    public float JumpBufferTime = 0.15f;

    [Header("Wall Mechanics")]
    public float WallSlideSpeed = 2.0f;
    public float WallJumpHorizontal = 7.0f;
    public float WallJumpVertical = 7.0f;
    public float WallDetectRadius = 0.7f;
    public float WallJumpCooldown = 0.35f;

    public CharacterController Controller { get; private set; }

    private float _verticalVelocity;
    private float _groundedTimer;
    private float _jumpBufferTimer;
    private float _wallJumpCooldownTimer;
    private Vector3 _wallNormal;
    private Vector3 _pushVelocity;
    private bool _isWallSliding;
    private bool _wasGrounded;
    private Transform _body;
    private Camera _camera;

    public event Action Jumped;
    public event Action<float> Landed;

    public bool IsGrounded { get { return _groundedTimer > 0f; } }
    public bool IsWallSliding { get { return _isWallSliding; } }
    public float CurrentHeight { get { return transform.position.y; } }
    public float VerticalVelocity { get { return _verticalVelocity; } }
    public Vector3 WallNormal { get { return _wallNormal; } }
    public Transform VisualRoot { get { return _body; } }

    /// <summary>Горизонтальная скорость 0..1 — для процедурной анимации.</summary>
    public float PlanarSpeed01 { get; private set; }

    private void Awake()
    {
        Controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        _body = transform.Find("Body");
        if (_body == null && transform.childCount > 0)
            _body = transform.GetChild(0);
    }

    private void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            return;

        float delta = Time.deltaTime;
        _groundedTimer -= delta;
        _jumpBufferTimer -= delta;
        _wallJumpCooldownTimer -= delta;

        if (Controller.isGrounded)
        {
            _groundedTimer = CoyoteTime;
            if (!_wasGrounded)
            {
                float impact = Mathf.Clamp01(-_verticalVelocity / 14f);
                if (impact > 0.12f)
                {
                    Action<float> handler = Landed;
                    if (handler != null) handler(impact);
                }
            }
            _wasGrounded = true;
        }
        else
        {
            _wasGrounded = false;
        }

        if (InputService.Instance != null && InputService.Instance.ConsumeJump())
            _jumpBufferTimer = JumpBufferTime;

        Vector2 input = InputService.Instance != null ? InputService.Instance.Move() : Vector2.zero;
        Vector3 moveDir = CameraRelativeMove(input);

        _isWallSliding = DetectWallSlide(input, moveDir);

        if (_isWallSliding)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity, -WallSlideSpeed);
        }
        else
        {
            _verticalVelocity -= Gravity * delta;
        }

        bool wantJump = _jumpBufferTimer > 0f;

        if (wantJump && (IsGrounded || _groundedTimer > 0f))
        {
            DoJump();
            _jumpBufferTimer = 0f;
        }
        else if (wantJump && _isWallSliding && _wallJumpCooldownTimer <= 0f)
        {
            DoWallJump();
            _jumpBufferTimer = 0f;
            _wallJumpCooldownTimer = WallJumpCooldown;
        }

        _pushVelocity = Vector3.Lerp(_pushVelocity, Vector3.zero, 6f * delta);
        if (_pushVelocity.sqrMagnitude < 0.01f)
            _pushVelocity = Vector3.zero;

        Vector3 velocity = moveDir * MoveSpeed + _pushVelocity;
        velocity.y = _verticalVelocity;

        Move(velocity, delta);

        Vector3 planar = Controller.velocity;
        planar.y = 0f;
        PlanarSpeed01 = MoveSpeed > 0f ? Mathf.Clamp01(planar.magnitude / MoveSpeed) : 0f;

        if (_body != null && input.sqrMagnitude > 0.01f)
        {
            Vector3 dir = new Vector3(moveDir.x, 0f, moveDir.z);
            if (dir.sqrMagnitude > 0.001f)
                _body.rotation = Quaternion.Slerp(_body.rotation, Quaternion.LookRotation(dir, Vector3.up), 12f * delta);
        }
    }

    private void Move(Vector3 velocity, float delta)
    {
        CollisionFlags flags = Controller.Move(velocity * delta);
        if ((flags & CollisionFlags.CollidedBelow) != 0 && _verticalVelocity < 0f)
            _verticalVelocity = 0f;
        if ((flags & CollisionFlags.CollidedAbove) != 0 && _verticalVelocity > 0f)
            _verticalVelocity = 0f;
    }

    private Vector3 CameraRelativeMove(Vector2 input)
    {
        if (input.sqrMagnitude < 0.01f)
            return Vector3.zero;

        if (_camera == null)
            _camera = Camera.main;

        Camera cam = _camera;
        Vector3 forward;
        Vector3 right;
        if (cam != null)
        {
            forward = cam.transform.forward;
            forward.y = 0f;
            forward.Normalize();
            right = cam.transform.right;
            right.y = 0f;
            right.Normalize();
        }
        else
        {
            forward = Vector3.forward;
            right = Vector3.right;
        }

        Vector3 dir = (forward * input.y + right * input.x);
        if (dir.sqrMagnitude > 1f)
            dir.Normalize();
        return dir;
    }

    private bool DetectWallSlide(Vector2 input, Vector3 moveDir)
    {
        _wallNormal = Vector3.zero;
        if (IsGrounded || _verticalVelocity >= 0f || moveDir.sqrMagnitude < 0.001f)
            return false;

        Vector3 origin = transform.position + Vector3.up * 1.0f;
        if (Physics.SphereCast(origin, WallDetectRadius * 0.45f, moveDir, out RaycastHit hit, 0.9f))
        {
            float dot = Vector3.Dot(hit.normal, Vector3.up);
            if (dot < 0.3f)
            {
                _wallNormal = hit.normal;
                return true;
            }
        }
        return false;
    }

    private void DoJump()
    {
        _verticalVelocity = PlayerPhysics.JumpSpeed;
        _groundedTimer = 0f;
        _wasGrounded = false;

        Action handler = Jumped;
        if (handler != null) handler();
    }

    private void DoWallJump()
    {
        _verticalVelocity = WallJumpVertical;
        _pushVelocity = _wallNormal * WallJumpHorizontal;
        _wasGrounded = false;

        Action handler = Jumped;
        if (handler != null) handler();
    }

    public void SetVisualRoot(Transform visualRoot)
    {
        _body = visualRoot;
    }

    public void ResetTo(Vector3 position)
    {
        Controller.enabled = false;
        transform.position = position;
        _verticalVelocity = 0f;
        _groundedTimer = 0f;
        _jumpBufferTimer = 0f;
        _wallJumpCooldownTimer = 0f;
        _wallNormal = Vector3.zero;
        _pushVelocity = Vector3.zero;
        _isWallSliding = false;
        _wasGrounded = false;
        PlanarSpeed01 = 0f;
        Controller.enabled = true;
    }
}