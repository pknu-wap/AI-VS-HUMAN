using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpd = 5f;
    [SerializeField] private float _jumpPower = 7f;

    [Header("Ground Check")]
    [SerializeField] private float _rayDistance = 0.6f; // 캐릭터 중심에서 바닥까지 거리
    [SerializeField] private LayerMask _groundLayer;    // 'Ground' 레이어 선택 필수!
    [SerializeField] private int _maxJumpCount = 2;     // 2단 점프 설정

    private Rigidbody2D _rigid;
    private InputController _input;
    
    private float _currentX;
    private int _jumpRemain;
    private bool _isGrounded;

    private void Awake()
    {
        _input = GetComponent<InputController>();
        _rigid = GetComponent<Rigidbody2D>();

        // 이벤트 구독 (오타 주의!)
        _input.OnMoveEvent += HandleMove;
        _input.OnJumpEvent += HandleJump;
    }

    private void Update()
    {
        CheckGrounded();
    }

    private void CheckGrounded()
    {
        // 발밑으로 레이저를 쏴서 땅인지 확인
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, _rayDistance, _groundLayer);
        
        if (hit.collider != null)
        {
            _isGrounded = true;
            _jumpRemain = _maxJumpCount; // 바닥에 닿으면 점프 횟수 초기화
        }
        else
        {
            _isGrounded = false;
        }

        // 에디터에서 빨간 선으로 레이저 확인 가능
        Debug.DrawRay(transform.position, Vector2.down * _rayDistance, Color.red);
    }

    private void HandleMove(float x)
    {
        _currentX = x;
    }

    private void HandleJump()
    {
        // 바닥이거나 남은 점프 횟수가 있을 때만 실행
        if (_isGrounded || _jumpRemain > 0)
        {
            // 연속 점프 시 힘이 중첩되지 않게 속도 초기화
            _rigid.linearVelocity = new Vector2(_rigid.linearVelocity.x, 0);
            _rigid.AddForce(Vector2.up * _jumpPower, ForceMode2D.Impulse);
            
            _jumpRemain--; // 점프할 때마다 횟수 차감
        }
    }

    private void FixedUpdate()
    {
        // 물리 이동 처리
        _rigid.linearVelocity = new Vector2(_currentX * _moveSpd, _rigid.linearVelocity.y);
    }

    private void OnDestroy()
    {
        if (_input != null)
        {
            _input.OnMoveEvent -= HandleMove;
            _input.OnJumpEvent -= HandleJump;
        }
    }
}