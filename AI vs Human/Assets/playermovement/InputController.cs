using System;
using UnityEngine;
using UnityEngine.InputSystem; // 새 입력 시스템 사용

public class InputController : MonoBehaviour
{
    public event Action<float> OnMoveEvent;
    public event Action OnJumpEvent;

    // Player Input 컴포넌트의 "Move" 액션과 연결됨
    private void OnMove(InputValue value)
    {
        Vector2 inputVec = value.Get<Vector2>();
        OnMoveEvent?.Invoke(inputVec.x);
    }

    // Player Input 컴포넌트의 "Jump" 액션과 연결됨
    private void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            OnJumpEvent?.Invoke();
        }
    }
}