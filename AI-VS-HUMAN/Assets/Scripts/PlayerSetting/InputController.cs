using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputController : MonoBehaviour
{
    public event Action<float> OnMoveEvent;
    public event Action OnJumpEvent;
    public event Action OnDashEvent;
    public event Action OnAttackEvent;
    public event Action<Vector2> OnFireEvent;
    public event Action<Vector2> OnFireStartEvent; // ✅ 추가
    public event Action<Vector2> OnFireEndEvent;   // ✅ 추가

    private void OnMove(InputValue value)
    {
        OnMoveEvent?.Invoke(value.Get<Vector2>().x);
    }

    private void OnJump(InputValue value)
    {
        if (value.isPressed) OnJumpEvent?.Invoke();
    }

    private void OnDash(InputValue value)
    {
        if (value.isPressed) OnDashEvent?.Invoke();
    }

    private void OnAttack(InputValue value)
    {
        if (value.isPressed) OnAttackEvent?.Invoke();
    }

    private void OnFire(InputValue value)
    {
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(
            Mouse.current.position.ReadValue());

        if (value.isPressed)
        {
            OnFireEvent?.Invoke(mouseWorldPos);
            OnFireStartEvent?.Invoke(mouseWorldPos); // ✅ 누르는 순간
        }
        else
        {
            OnFireEndEvent?.Invoke(mouseWorldPos);   // ✅ 떼는 순간
        }
    }
}