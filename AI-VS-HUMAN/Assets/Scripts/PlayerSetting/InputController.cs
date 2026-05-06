// Unity Input System 입력을 받아 다른 플레이어 스크립트에 이벤트로 전달하는 입력 허브
// 이동, 점프, 대시, 공격, 마우스 조준 위치를 한 곳에서 처리한다.
using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputController : MonoBehaviour
{
    private Camera mainCamera;

    public event Action<float> OnMoveEvent;
    public event Action OnJumpEvent;
    public event Action OnDashEvent;
    public event Action OnAttackEvent;
    public event Action<Vector2> OnFireEvent;
    public event Action<Vector2> OnFireStartEvent; // 발사 버튼을 누른 순간
    public event Action<Vector2> OnFireEndEvent;   // 발사 버튼을 뗀 순간

    private void Awake()
    {
        mainCamera = Camera.main;
    }

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
        // 마우스 위치를 월드 좌표로 바꿔서 무기 스크립트가 바로 조준에 사용할 수 있게 한다.
        if (Mouse.current == null)
            return;

        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        Vector2 mouseWorldPos = mainCamera.ScreenToWorldPoint(
            Mouse.current.position.ReadValue());

        if (value.isPressed)
        {
            OnFireEvent?.Invoke(mouseWorldPos);
            OnFireStartEvent?.Invoke(mouseWorldPos);
        }
        else
        {
            OnFireEndEvent?.Invoke(mouseWorldPos);
        }
    }
}
