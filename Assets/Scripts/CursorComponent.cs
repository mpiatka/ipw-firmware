using Naki3D.Common.Protocol;
using UnityEngine;
using UnityEngine.Events;
using Vector3 = UnityEngine.Vector3;

public class CursorComponent : MonoBehaviour
{
    [SerializeField]
    private DualCameraComponent _camera;

    public float MinX = float.MaxValue;
    public float MaxX = float.MinValue;
    public float MinY = float.MaxValue;
    public float MaxY = float.MinValue;

    public TMPro.TMP_Text Text;

    public bool DebugOutput = false;
    public float ActivationTime = 2f;
    public UnityEvent<GameObject> OnActivate;

    public bool IsVisible => ScreenPos.y > 0.45f;//transform.localPosition.y > -0.43f;
    public Vector3 ScreenPos;
    
    public GameObject HoveredObject { get; private set; }
    public float HoverTime { get; private set; }

    private SpriteRenderer _sprite;
    private Vector3 _viewportPos;
    private bool _activated;

    private void Start()
    {
        _sprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (IsVisible)
        {
            // Try a 3D ray instead if we didn't hit anything
            if (!Cast2DRay()) Cast3DRay();
        }

        if (HoveredObject == null) _activated = false;
        if (!_activated && HoverTime > ActivationTime)
        {
            _activated = true;
            OnActivate.Invoke(HoveredObject);
        }

        if (DebugOutput)
        {
            var color = string.Empty;
            if (_activated) color = "<color=\"red\">";
            Text.text = $"{color}{_viewportPos}\n{HoveredObject} ({HoverTime}s)";
        }
    }

    private bool Cast2DRay()
    {
        var hit = Physics2D.OverlapPoint(transform.position);

        if (!hit)
        {
            HoveredObject = null;
            HoverTime = 0f;
            return false;
        }

        if (hit.gameObject == HoveredObject) HoverTime += Time.deltaTime;
        else HoverTime = 0f;
                
        HoveredObject = hit.gameObject;
        return true;
    }

    private bool Cast3DRay()
    {
        var ray = new Ray(transform.position, transform.forward);
        Debug.DrawRay(transform.position, transform.forward * 50, Color.red);
        var didHit = Physics.Raycast(ray, out var hit, 500f);

        if (!didHit)
        {
            HoveredObject = null;
            HoverTime = 0f;
            return false;
        }

        if (hit.collider.gameObject == HoveredObject) HoverTime += Time.deltaTime;
        else HoverTime = 0f;
                
        HoveredObject = hit.collider.gameObject;
        return true;
    }

    public void HandMovement(SensorMessage msg)
    {
        if (msg.DataCase != SensorMessage.DataOneofCase.HandMovement) return;

        var hand = msg.HandMovement;
        if (hand.UserId != 1) return;
        if (hand.Hand != HandType.HandRight) return;

        var handPos = new UnityEngine.Vector2(1f - hand.ProjPosition.X, 1f - hand.ProjPosition.Y);
        _viewportPos = new UnityEngine.Vector3(
            (handPos.x - MinX) / (MaxX - MinX),
            (handPos.y - MinY) / (MaxY - MinY),
            2.5f);

        if (handPos.x < MinX) MinX = handPos.x;
        if (handPos.y < MinY) MinY = handPos.y;

        if (handPos.x > MaxX) MaxX = handPos.x;
        if (handPos.y > MaxY) MaxY = handPos.y;

        Vector3 pos;
        ScreenPos = _viewportPos;
        
        if (_viewportPos.x > 0.5f)
        {
            _viewportPos -= new Vector3(0.5f, 0, 0);
            _viewportPos.Scale(new Vector3(2, 1, 1));
            pos = _camera.TopCamera.Camera.ViewportToWorldPoint(_viewportPos);
        }
        else
        {
            _viewportPos.Scale(new Vector3(2, 1, 1));
            pos = _camera.BottomCamera.Camera.ViewportToWorldPoint(_viewportPos);
        }

        transform.localPosition = pos;
        _sprite.enabled = IsVisible;
    }
}
