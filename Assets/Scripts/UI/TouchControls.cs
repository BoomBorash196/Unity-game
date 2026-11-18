using UnityEngine;
using UnityEngine.UI;

public class TouchControls : MonoBehaviour
{
    private RectTransform _canvasRect;
    private GameObject _canvasGo;
    private JoystickControl _joystick;
    private bool _enabled = true;

    private void Update()
    {
        if (!_enabled || _canvasGo == null) return;
        bool paused = GameManager.Instance != null && GameManager.Instance.State == GameState.Paused;
        bool shouldBeActive = !paused;
        if (_canvasGo.activeSelf != shouldBeActive)
        {
            _canvasGo.SetActive(shouldBeActive);
            if (shouldBeActive && _joystick != null)
            {
                _joystick.ResetDrag();
                if (InputService.Instance != null)
                    InputService.Instance.SetJoystick(Vector2.zero);
            }
        }
    }

    private void Start()
    {
        _enabled = SaveService.TouchPreferred();
        if (!_enabled) return;

        UIFactory.EnsureEventSystem();

        Canvas canvas = UIFactory.CreateCanvas("Touch Controls Canvas");
        _canvasRect = canvas.GetComponent<RectTransform>();
        _canvasGo = canvas.gameObject;

        RectTransform left = UIFactory.Panel(canvas.transform, "Move Area", new Color(0f, 0f, 0f, 0.001f));
        left.anchorMin = new Vector2(0f, 0f);
        left.anchorMax = new Vector2(0.5f, 1f);
        left.offsetMin = Vector2.zero;
        left.offsetMax = Vector2.zero;
        left.GetComponent<Image>().raycastTarget = true;

        RectTransform right = UIFactory.Panel(canvas.transform, "Look Area", new Color(0f, 0f, 0f, 0.001f));
        right.anchorMin = new Vector2(0.5f, 0f);
        right.anchorMax = new Vector2(1f, 1f);
        right.offsetMin = Vector2.zero;
        right.offsetMax = new Vector2(0f, -110f);
        right.GetComponent<Image>().raycastTarget = true;
        right.gameObject.AddComponent<LookDrag>();

        RectTransform basePanel = UIFactory.Panel(canvas.transform, "Joystick Base", new Color(0.35f, 0.42f, 0.5f, 0.4f));
        basePanel.sizeDelta = new Vector2(200f, 200f);
        basePanel.anchorMin = basePanel.anchorMax = new Vector2(0f, 0f);
        basePanel.pivot = new Vector2(0.5f, 0.5f);

        RectTransform knob = UIFactory.Panel(canvas.transform, "Joystick Knob", new Color(0.75f, 0.82f, 0.9f, 0.7f));
        knob.sizeDelta = new Vector2(110f, 110f);
        knob.anchorMin = knob.anchorMax = new Vector2(0f, 0f);
        knob.pivot = new Vector2(0.5f, 0.5f);

        RectTransform jumpPanel = UIFactory.Panel(canvas.transform, "Jump Button", new Color(1f, 0.55f, 0.2f, 0.75f));
        jumpPanel.sizeDelta = new Vector2(170f, 170f);
        jumpPanel.anchorMin = jumpPanel.anchorMax = new Vector2(1f, 0f);
        jumpPanel.pivot = new Vector2(0.5f, 0.5f);
        jumpPanel.anchoredPosition = new Vector2(-170f, 150f);
        jumpPanel.GetComponent<Image>().raycastTarget = true;
        jumpPanel.gameObject.AddComponent<JumpTouchButton>();

        Text jumpLabel = UIFactory.Text(jumpPanel, "ПРЫЖОК", 26, Color.white);
        UIFactory.Stretch(jumpLabel.rectTransform);

        JoystickControl joystick = left.gameObject.AddComponent<JoystickControl>();
        joystick.Setup(_canvasRect, basePanel, knob);
        _joystick = joystick;
    }
}