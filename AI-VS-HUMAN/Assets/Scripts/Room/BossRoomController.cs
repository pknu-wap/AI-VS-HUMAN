// 보스룸 전용 카메라 페이즈를 관리하는 스크립트
// 플레이어가 보스룸에 들어오면 카메라를 고정하고, 보스 체력이 절반 이하가 되면 화면을 위로 스크롤한다.
using UnityEngine;

[DisallowMultipleComponent]
public class BossRoomController : MonoBehaviour
{
    private enum BossRoomPhase
    {
        Waiting,
        Phase1,
        Phase2
    }

    [Header("References")]
    public Transform player;
    public RoomCameraController roomCameraController;
    public Room bossRoom;
    public BossDrone boss;

    [Header("Camera")]
    public float scrollSpeed = 2f;
    public float scrollDelay = 1f;
    public float phase1FollowSpeed = 3f;

    [Header("Phase")]
    public bool enterWhenPlayerInsideRoom = true;
    public bool startPhase2WhenBossHalfHealth = true;

    private BossRoomPhase phase = BossRoomPhase.Waiting;
    private Camera cam;
    private bool phase2Started;
    private float phase2Timer;

    private float CameraBottomY => cam.transform.position.y - cam.orthographicSize;

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();
        SubscribeToBoss();
    }

    private void OnDisable()
    {
        UnsubscribeFromBoss();
    }

    private void Reset()
    {
        bossRoom = GetComponent<Room>();
    }

    private void LateUpdate()
    {
        if (cam == null || bossRoom == null)
        {
            ResolveReferences();
            if (cam == null || bossRoom == null)
                return;
        }

        if (phase == BossRoomPhase.Waiting)
        {
            CheckPlayerEnteredRoom();
        }
        else if (phase == BossRoomPhase.Phase1)
        {
            MoveCameraToPhase1Position();
            CheckBossHalfHealth();
        }
        else if (phase == BossRoomPhase.Phase2)
        {
            MoveCameraUp();
            CheckPlayerBelowCamera();
        }
    }

    public void EnterBossRoom()
    {
        // 보스룸에 처음 입장했을 때 일반 룸 카메라를 끄고 보스룸 전용 제어로 전환한다.
        if (phase != BossRoomPhase.Waiting)
            return;

        ResolveReferences();
        SubscribeToBoss();

        if (roomCameraController != null)
            roomCameraController.enabled = false;

        phase = BossRoomPhase.Phase1;
        phase2Started = false;
        phase2Timer = 0f;

        MoveCameraToPhase1Position();
        CheckBossHalfHealth();
    }

    public void StartPhase2()
    {
        // 보스 체력 이벤트와 인스펙터 테스트 모두 이 메서드를 통해 2페이즈를 시작한다.
        if (phase2Started)
            return;

        phase2Started = true;
        phase2Timer = Mathf.Max(0f, scrollDelay);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
            EnterBossRoom();
    }

    private void ResolveReferences()
    {
        // 인스펙터 참조가 비어 있어도 씬 안의 기본 오브젝트를 찾아 최대한 자동 연결한다.
        if (bossRoom == null)
            bossRoom = GetComponent<Room>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (roomCameraController == null && Camera.main != null)
            roomCameraController = Camera.main.GetComponent<RoomCameraController>();

        if (boss == null)
            boss = FindFirstObjectByType<BossDrone>();

        if (cam == null)
        {
            if (roomCameraController != null)
                cam = roomCameraController.GetComponent<Camera>();

            if (cam == null)
                cam = Camera.main;
        }
    }

    private void SubscribeToBoss()
    {
        if (boss == null)
            return;

        boss.HalfHealthReached -= OnBossHalfHealthReached;
        boss.HalfHealthReached += OnBossHalfHealthReached;
    }

    private void UnsubscribeFromBoss()
    {
        if (boss != null)
            boss.HalfHealthReached -= OnBossHalfHealthReached;
    }

    private void OnBossHalfHealthReached()
    {
        if (phase == BossRoomPhase.Phase1)
            StartPhase2();
    }

    private void MoveCameraToPhase1Position()
    {
        // 1페이즈는 방의 하단을 기준으로 카메라를 고정한다.
        float fixedY = bossRoom.transform.position.y - bossRoom.roomSize.y * 0.5f + cam.orthographicSize;
        Vector3 target = new Vector3(bossRoom.transform.position.x, fixedY, cam.transform.position.z);

        cam.transform.position = Vector3.Lerp(
            cam.transform.position,
            target,
            Time.deltaTime * phase1FollowSpeed);
    }

    private void CheckBossHalfHealth()
    {
        if (phase2Started)
        {
            phase2Timer -= Time.deltaTime;

            if (phase2Timer <= 0f)
                phase = BossRoomPhase.Phase2;

            return;
        }

        if (!startPhase2WhenBossHalfHealth || boss == null)
            return;

        if (boss.HealthRatio <= 0.5f)
            StartPhase2();
    }

    private void CheckPlayerEnteredRoom()
    {
        if (!enterWhenPlayerInsideRoom || player == null)
            return;

        if (bossRoom.GetBounds().Contains(player.position))
            EnterBossRoom();
    }

    private void MoveCameraUp()
    {
        // 2페이즈는 방의 상단까지만 카메라가 올라가도록 제한한다.
        float maxY = bossRoom.transform.position.y + bossRoom.roomSize.y * 0.5f - cam.orthographicSize;
        float nextY = Mathf.Min(cam.transform.position.y + scrollSpeed * Time.deltaTime, maxY);

        cam.transform.position = new Vector3(
            bossRoom.transform.position.x,
            nextY,
            cam.transform.position.z);
    }

    private void CheckPlayerBelowCamera()
    {
        if (player != null && player.position.y < CameraBottomY)
            OnPlayerDeath();
    }

    private void OnPlayerDeath()
    {
        Debug.Log("Player fell below the boss room camera.", this);
    }

    [ContextMenu("Test Enter Boss Room")]
    private void TestEnter()
    {
        EnterBossRoom();
    }

    [ContextMenu("Test Start Phase 2")]
    private void TestPhase2()
    {
        StartPhase2();
    }
}
