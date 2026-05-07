// 보스룸 전용 카메라 페이즈와 보스 소환 시점을 관리하는 스크립트입니다.
// 플레이어가 보스룸에 들어오면 보스를 지정 위치에 소환하고, 보스 체력이 절반 이하가 되면 2페이즈 카메라 스크롤을 시작합니다.
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
    private PlayerHealth playerHealth;

    [Header("Boss Spawn")]
    public BossDrone bossPrefab;
    public Transform bossSpawnPoint;
    public Vector3 bossSpawnPosition = new Vector3(32f, 8f, 0f);
    public bool hideSceneBossUntilRoomEntered = true;

    [Header("Phase 1 Temporary Wall")]
    public bool createPhase1CeilingWall = true;
    public bool useCameraTopForCeilingWall = false;
    public Transform ceilingWallPoint;
    public Vector3 ceilingWallPosition = new Vector3(32f, 9f, 0f);
    public Vector2 ceilingWallSize = new Vector2(34f, 1f);
    public float ceilingWallHeight = 1f;
    public float ceilingWallWidthPadding = 2f;
    public bool createPhase1EntranceWall = true;
    public Transform entranceWallPoint;
    public Vector3 entranceWallPosition = new Vector3(15.5f, 0f, 0f);
    public Vector2 entranceWallSize = new Vector2(1f, 18f);

    [Header("Camera")]
    public float scrollSpeed = 2f;
    public float scrollDelay = 1f;
    public float phase1FollowSpeed = 3f;

    [Header("Phase 2 Boss Follow")]
    public bool keepBossVisibleInPhase2 = true;
    public float phase2BossCameraYOffset = 6f;
    public float phase2BossFollowSpeed = 9f;

    [Header("Phase")]
    public bool enterWhenPlayerInsideRoom = true;
    public bool startPhase2WhenBossHalfHealth = true;

    private BossRoomPhase phase = BossRoomPhase.Waiting;
    private Camera cam;
    private bool phase2Started;
    private float phase2Timer;
    private bool bossSpawned;
    private GameObject phase1CeilingWall;
    private GameObject phase1EntranceWall;

    private float CameraBottomY => cam.transform.position.y - cam.orthographicSize;

    private void Awake()
    {
        ResolveReferences();
        HideSceneBossUntilEnter();
    }

    private void OnEnable()
    {
        ResolveReferences();
        PlayerHealth.PlayerDied += HandlePlayerDied;
        PlayerHealth.PlayerRespawned += HandlePlayerRespawned;

        if (!hideSceneBossUntilRoomEntered || phase != BossRoomPhase.Waiting)
            SubscribeToBoss();
    }

    private void OnDisable()
    {
        PlayerHealth.PlayerDied -= HandlePlayerDied;
        PlayerHealth.PlayerRespawned -= HandlePlayerRespawned;
        UnsubscribeFromBoss();
        RemovePhase1CeilingWall();
        RemovePhase1EntranceWall();
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
        // 보스룸에 처음 입장했을 때 일반 룸 카메라를 끄고 보스룸 전용 제어로 전환합니다.
        if (phase != BossRoomPhase.Waiting)
            return;

        ResolveReferences();

        if (roomCameraController != null)
            roomCameraController.enabled = false;

        phase = BossRoomPhase.Phase1;
        phase2Started = false;
        phase2Timer = 0f;

        SnapCameraToPhase1Position();
        CreatePhase1CeilingWall();
        CreatePhase1EntranceWall();
        SpawnBossIfNeeded();
        SubscribeToBoss();
        CheckBossHalfHealth();
    }

    public void StartPhase2()
    {
        // 보스 체력 이벤트와 인스펙터 테스트 모두 이 메서드를 통해 2페이즈를 시작합니다.
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
        // 인스펙터 참조가 비어 있어도 씬 안의 기본 오브젝트를 찾아 최대한 자동 연결합니다.
        if (bossRoom == null)
            bossRoom = GetComponent<Room>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        if (roomCameraController == null && Camera.main != null)
            roomCameraController = Camera.main.GetComponent<RoomCameraController>();

        if (boss == null && (!hideSceneBossUntilRoomEntered || phase != BossRoomPhase.Waiting))
            boss = FindFirstObjectByType<BossDrone>();

        if (cam == null)
        {
            if (roomCameraController != null)
                cam = roomCameraController.GetComponent<Camera>();

            if (cam == null)
                cam = Camera.main;
        }
    }

    private void HideSceneBossUntilEnter()
    {
        if (!hideSceneBossUntilRoomEntered || boss == null || bossSpawned)
            return;

        // 씬에 미리 배치된 보스는 입장 전에는 보이지 않게 두고, 입장 순간 지정 위치에서 활성화합니다.
        if (boss.gameObject.scene.IsValid())
            boss.gameObject.SetActive(false);
    }

    private void SpawnBossIfNeeded()
    {
        if (bossSpawned)
            return;

        Vector3 spawnPosition = GetBossSpawnPosition();

        if (boss != null)
        {
            boss.gameObject.SetActive(true);
            boss.PrepareForBossRoomSpawn(spawnPosition, cam);
            bossSpawned = true;
            return;
        }

        if (bossPrefab != null)
        {
            boss = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);
            boss.name = bossPrefab.name;
            boss.PrepareForBossRoomSpawn(spawnPosition, cam);
            bossSpawned = true;
            return;
        }

        Debug.LogWarning("BossRoomController has no BossDrone or bossPrefab to spawn.", this);
    }

    private Vector3 GetBossSpawnPosition()
    {
        // Transform을 지정하면 그 위치를 우선 사용하고, 없으면 인스펙터의 월드 좌표를 사용합니다.
        if (bossSpawnPoint != null)
            return bossSpawnPoint.position;

        return bossSpawnPosition;
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
        // 1페이즈는 방의 하단을 기준으로 카메라를 고정합니다.
        Vector3 target = GetPhase1CameraPosition();

        cam.transform.position = Vector3.Lerp(
            cam.transform.position,
            target,
            Time.deltaTime * phase1FollowSpeed);
    }

    private void SnapCameraToPhase1Position()
    {
        // 보스를 켜기 전에 카메라를 먼저 보스룸 위치로 고정해서 보스의 카메라 경계 보정이 엉뚱한 위치를 기준으로 돌지 않게 합니다.
        if (cam == null || bossRoom == null)
            return;

        cam.transform.position = GetPhase1CameraPosition();
    }

    private Vector3 GetPhase1CameraPosition()
    {
        Camera phaseCamera = cam != null ? cam : Camera.main;
        float orthographicSize = phaseCamera != null ? phaseCamera.orthographicSize : 9f;
        float fixedY = bossRoom.transform.position.y - bossRoom.roomSize.y * 0.5f + orthographicSize;
        float cameraZ = phaseCamera != null ? phaseCamera.transform.position.z : -10f;
        return new Vector3(bossRoom.transform.position.x, fixedY, cameraZ);
    }

    private void CheckBossHalfHealth()
    {
        if (phase2Started)
        {
            phase2Timer -= Time.deltaTime;

            if (phase2Timer <= 0f)
            {
                RemovePhase1CeilingWall();
                RemovePhase1EntranceWall();
                phase = BossRoomPhase.Phase2;
            }

            return;
        }

        if (!startPhase2WhenBossHalfHealth || boss == null || !bossSpawned)
            return;

        if (boss.HealthRatio <= 0.5f)
            StartPhase2();
    }

    private void CheckPlayerEnteredRoom()
    {
        if (!enterWhenPlayerInsideRoom || player == null)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (bossRoom.GetBounds().Contains(player.position))
            EnterBossRoom();
    }

    private void MoveCameraUp()
    {
        // 2페이즈는 방의 상단까지만 카메라가 올라가도록 제한합니다.
        float maxY = bossRoom.transform.position.y + bossRoom.roomSize.y * 0.5f - cam.orthographicSize;
        float nextY = Mathf.Min(cam.transform.position.y + scrollSpeed * Time.deltaTime, maxY);

        cam.transform.position = new Vector3(
            bossRoom.transform.position.x,
            nextY,
            cam.transform.position.z);

        FollowBossWithPhase2Camera();
    }

    private void FollowBossWithPhase2Camera()
    {
        if (!keepBossVisibleInPhase2 || boss == null || cam == null)
            return;

        // 2페이즈에서는 카메라 중심보다 살짝 위쪽을 보스의 기준 높이로 삼아 화면에 잘리지 않게 한다.
        boss.FollowBossRoomCamera(cam, phase2BossCameraYOffset, phase2BossFollowSpeed);
    }

    private void CreatePhase1CeilingWall()
    {
        if (!createPhase1CeilingWall || phase1CeilingWall != null || cam == null || bossRoom == null)
            return;

        Vector3 wallPosition = GetPhase1WallPosition();
        Vector2 wallSize = GetPhase1WallSize();

        phase1CeilingWall = new GameObject("Phase 1 Temporary Ceiling Wall");
        phase1CeilingWall.transform.position = wallPosition;
        phase1CeilingWall.transform.SetParent(transform, true);

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            phase1CeilingWall.layer = groundLayer;

        BoxCollider2D wallCollider = phase1CeilingWall.AddComponent<BoxCollider2D>();
        wallCollider.size = wallSize;
        wallCollider.isTrigger = false;
    }

    private void CreatePhase1EntranceWall()
    {
        if (!createPhase1EntranceWall || phase1EntranceWall != null)
            return;

        // 입구벽은 플레이어가 보스룸 밖으로 되돌아가는 길만 막도록 왼쪽 경계 바로 바깥에 배치합니다.
        Vector3 wallPosition = GetPhase1EntranceWallPosition();
        Vector2 wallSize = GetPhase1EntranceWallSize();

        phase1EntranceWall = CreatePhase1SolidWall("Phase 1 Temporary Entrance Wall", wallPosition, wallSize);
    }

    private GameObject CreatePhase1SolidWall(string wallName, Vector3 wallPosition, Vector2 wallSize)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.position = wallPosition;
        wall.transform.SetParent(transform, true);

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            wall.layer = groundLayer;

        BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = wallSize;
        wallCollider.isTrigger = false;

        return wall;
    }

    private Vector3 GetPhase1WallPosition()
    {
        if (ceilingWallPoint != null)
            return ceilingWallPoint.position;

        if (!useCameraTopForCeilingWall || cam == null)
            return ceilingWallPosition;

        float wallHeight = Mathf.Max(0.1f, ceilingWallHeight);
        float cameraTopY = cam.transform.position.y + cam.orthographicSize;
        return new Vector3(bossRoom.transform.position.x, cameraTopY + wallHeight * 0.5f, 0f);
    }

    private Vector2 GetPhase1WallSize()
    {
        if (!useCameraTopForCeilingWall)
            return new Vector2(Mathf.Max(0.1f, ceilingWallSize.x), Mathf.Max(0.1f, ceilingWallSize.y));

        float wallHeight = Mathf.Max(0.1f, ceilingWallHeight);
        float cameraHalfWidth = cam != null ? cam.orthographicSize * cam.aspect : 16f;
        float wallWidth = Mathf.Max(bossRoom.roomSize.x, cameraHalfWidth * 2f) + Mathf.Max(0f, ceilingWallWidthPadding);
        return new Vector2(wallWidth, wallHeight);
    }

    private Vector3 GetPhase1EntranceWallPosition()
    {
        if (entranceWallPoint != null)
            return entranceWallPoint.position;

        return entranceWallPosition;
    }

    private Vector2 GetPhase1EntranceWallSize()
    {
        return new Vector2(Mathf.Max(0.1f, entranceWallSize.x), Mathf.Max(0.1f, entranceWallSize.y));
    }

    private void RemovePhase1CeilingWall()
    {
        if (phase1CeilingWall == null)
            return;

        Destroy(phase1CeilingWall);
        phase1CeilingWall = null;
    }

    private void RemovePhase1EntranceWall()
    {
        if (phase1EntranceWall == null)
            return;

        Destroy(phase1EntranceWall);
        phase1EntranceWall = null;
    }

    private void CheckPlayerBelowCamera()
    {
        if (player == null || player.position.y >= CameraBottomY)
            return;

        if (playerHealth == null)
            playerHealth = player.GetComponent<PlayerHealth>();

        if (playerHealth != null)
            playerHealth.Kill();
    }

    private void HandlePlayerDied(PlayerHealth deadPlayer)
    {
        if (deadPlayer == null || playerHealth != null && deadPlayer != playerHealth)
            return;

        ResetBossRoomAfterPlayerDeath();
    }

    private void HandlePlayerRespawned(PlayerHealth respawnedPlayer)
    {
        if (respawnedPlayer == null || playerHealth != null && respawnedPlayer != playerHealth)
            return;

        if (roomCameraController != null)
        {
            roomCameraController.enabled = true;
            roomCameraController.ResetToPlayerRoom();
        }
    }

    private void ResetBossRoomAfterPlayerDeath()
    {
        if (phase == BossRoomPhase.Waiting && !bossSpawned)
            return;

        // 플레이어가 보스전에서 죽으면 보스룸 상태를 입장 전으로 돌려 다음 도전에 같은 조건으로 시작하게 합니다.
        phase = BossRoomPhase.Waiting;
        phase2Started = false;
        phase2Timer = 0f;
        bossSpawned = false;

        RemovePhase1CeilingWall();
        RemovePhase1EntranceWall();
        UnsubscribeFromBoss();

        if (boss != null)
        {
            boss.ResetForBossRoomRetry();

            if (hideSceneBossUntilRoomEntered)
                boss.gameObject.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetBossSpawnPosition(), 0.35f);

        if (createPhase1CeilingWall && bossRoom != null)
        {
            Gizmos.color = Color.yellow;
            Vector2 wallSize = GetPhase1WallSize();
            Gizmos.DrawWireCube(GetPhase1WallPosition(), new Vector3(wallSize.x, wallSize.y, 0.1f));
        }

        if (createPhase1EntranceWall)
        {
            Gizmos.color = Color.magenta;
            Vector2 wallSize = GetPhase1EntranceWallSize();
            Gizmos.DrawWireCube(GetPhase1EntranceWallPosition(), new Vector3(wallSize.x, wallSize.y, 0.1f));
        }
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
