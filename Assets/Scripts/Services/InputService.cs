using UnityEngine;

public class InputService : MonoBehaviour
{
    public static InputService Instance { get; private set; }

    private const float DeadZone = 0.15f;

    private Vector2 _joystick;
    private float _touchLookX;
    private float _touchLookY;
    private float _mouseLookX;
    private float _mouseLookY;
    private bool _jumpRequested;

    public bool TouchActive { get; private set; }

    public static void EnsureExists()
    {
        if (Instance == null)
        {
            GameObject go = new GameObject("[Services] InputService");
            go.AddComponent<InputService>();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space))
            RequestJump();

        if (Input.touchCount > 0)
            TouchActive = true;
    }

    private void LateUpdate()
    {
        _mouseLookX = Input.GetAxis("Mouse X") * GameSettings.MouseLookSensitivity;
        _mouseLookY = Input.GetAxis("Mouse Y") * GameSettings.MouseLookSensitivity;
    }

    public void SetJoystick(Vector2 value)
    {
        _joystick = value;
        TouchActive = _joystick.sqrMagnitude > DeadZone * DeadZone;
    }

    public void AddLookDelta(float dx, float dy)
    {
        _touchLookX += dx * GameSettings.TouchLookSensitivity;
        _touchLookY += dy * GameSettings.TouchLookSensitivity;
    }

    public void RequestJump()
    {
        _jumpRequested = true;
    }

    public Vector2 Move()
    {
        bool useTouch = _joystick.sqrMagnitude > DeadZone * DeadZone;
        if (useTouch)
            return _joystick.magnitude > 1f ? _joystick.normalized : _joystick;

        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    public Vector2 LookDelta()
    {
        Vector2 total = new Vector2(_mouseLookX + _touchLookX, _mouseLookY + _touchLookY);
        _touchLookX = 0f;
        _touchLookY = 0f;
        return total;
    }

    public bool ConsumeJump()
    {
        bool jump = _jumpRequested;
        _jumpRequested = false;
        return jump;
    }
}