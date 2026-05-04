using System.Collections;
using UnityEngine;

public class BossRoomController : MonoBehaviour
{
    [Header("참조")]
    public Transform player;
    public RoomCameraController roomCameraController;
    public Room bossRoom;

    [Header("카메라 스크롤 설정")]
    public float scrollSpeed = 2f;       // 카메라 올라가는 속도
    public float scrollDelay = 1f;       // 페이즈 2 시작 전 대기 시간

    // 페이즈
    // 0: 대기 (아직 보스방 미진입)
    // 1: 카메라 방 하단 고정
    // 2: 카메라 위로 스크롤 + 사망 판정
    private int phase = 0;

    private Camera cam;
    private float cameraBottomY => cam.transform.position.y - cam.orthographicSize;

    private void Awake()
    {
        cam = Camera.main;
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            EnterBossRoom();
        }
    }

    private void LateUpdate()
    {
        if (phase == 1)
        {
            float fixedY = bossRoom.transform.position.y
                        - bossRoom.roomSize.y / 2f
                        + cam.orthographicSize;

            Vector3 target = new Vector3(
                bossRoom.transform.position.x,
                fixedY,
                cam.transform.position.z
            );

            // 즉시 이동 → 부드럽게 이동
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, Time.deltaTime * 3f);
        }

        if (phase == 2)
        {
            // 카메라 위로 스크롤
            cam.transform.position += Vector3.up * scrollSpeed * Time.deltaTime;

            // 방 상단 넘지 않도록 클램프
            float maxY = bossRoom.transform.position.y
                         + bossRoom.roomSize.y / 2f
                         - cam.orthographicSize;

            cam.transform.position = new Vector3(
                cam.transform.position.x,
                Mathf.Min(cam.transform.position.y, maxY),
                cam.transform.position.z
            );

            // 플레이어가 카메라 아래로 벗어나면 사망
            if (player.position.y < cameraBottomY)
            {
                OnPlayerDeath();
            }
        }
    }

    // 보스방 진입 시 호출
    public void EnterBossRoom()
    {
        phase = 1;
        roomCameraController.enabled = false; // 기존 카메라 컨트롤러 비활성화
    }

    // 보스 HP 절반 깎였을 때 호출 (나중에 보스 스크립트에서 연결)
    public void StartPhase2()
    {
        StartCoroutine(Phase2Coroutine());
    }

    private IEnumerator Phase2Coroutine()
    {
        yield return new WaitForSeconds(scrollDelay); // 잠깐 대기
        phase = 2;
    }

    private void OnPlayerDeath()
    {
        // 지금은 일단 로그만
        Debug.Log("플레이어 사망!");

        // 나중에 여기에 사망 처리 추가
        // ex) SceneManager.LoadScene(...), player 비활성화 등
    }

    // 테스트용 — 인스펙터에서 버튼처럼 쓸 수 있어요
    [ContextMenu("보스방 진입 테스트")]
    public void TestEnter() => EnterBossRoom();

    [ContextMenu("페이즈 2 시작 테스트")]
    public void TestPhase2() => StartPhase2();
}