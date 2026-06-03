// 蹂댁뒪猷??꾩슜 移대찓???섏씠利덉? 蹂댁뒪 ?뚰솚 ?쒖젏??愿由ы븯???ㅽ겕由쏀듃?낅땲??
// ?뚮젅?댁뼱媛 蹂댁뒪猷몄뿉 ?ㅼ뼱?ㅻ㈃ 蹂댁뒪瑜?吏???꾩튂???뚰솚?섍퀬, 蹂댁뒪 泥대젰???덈컲 ?댄븯媛 ?섎㈃ 2?섏씠利?移대찓???ㅽ겕濡ㅼ쓣 ?쒖옉?⑸땲??
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class Stage1BossRoomController : MonoBehaviour
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
    public GiantDrone boss;
    private PlayerHealth playerHealth;

    [Header("Boss Activation")]
    public bool hideSceneBossUntilRoomEntered = true;
    public bool spawnOnlyOnce = true;
    public bool resetOnPlayerDeath = true;

    [Header("Boss Room Lock")]
    public bool createBossLockWalls = true;
    public float lockWallThickness = 1f;
    public float lockWallPadding = 0f;
    public string lockWallLayerName = "Ground";


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

    [Header("Boss Clear Cleanup")]
    public bool destroyEnemiesInsideRoomOnBossDeath = true;
    public List<GameObject> enemiesToDestroyOnBossDeath = new List<GameObject>();

    private BossRoomPhase phase = BossRoomPhase.Waiting;
    private Camera cam;
    private bool phase2Started;
    private float phase2Timer;
    private bool bossSpawned;
    private bool bossCleared;
    private bool hasBossStartPosition;
    private Vector3 bossStartPosition;
    private readonly List<GameObject> bossLockWalls = new List<GameObject>();

    private float CameraBottomY => cam.transform.position.y - cam.orthographicSize;

    private void Awake()
    {
        ResolveReferences();
        CacheBossStartPosition();
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
        RemoveBossLockWalls();
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
        // 蹂댁뒪猷몄뿉 泥섏쓬 ?낆옣?덉쓣 ???쇰컲 猷?移대찓?쇰? ?꾧퀬 蹂댁뒪猷??꾩슜 ?쒖뼱濡??꾪솚?⑸땲??
        if (bossCleared)
            return;

        if (phase != BossRoomPhase.Waiting)
            return;

        if (bossSpawned && spawnOnlyOnce)
            return;

        ResolveReferences();

        if (roomCameraController != null)
            roomCameraController.enabled = false;

        phase = BossRoomPhase.Phase1;
        phase2Started = false;
        phase2Timer = 0f;

        SnapCameraToPhase1Position();
        if (createBossLockWalls)
            CreateBossLockWalls();
        ActivateBossIfNeeded();
        SubscribeToBoss();
        CheckBossHalfHealth();
    }

    public void StartPhase2()
    {
        // 蹂댁뒪 泥대젰 ?대깽?몄? ?몄뒪?숉꽣 ?뚯뒪??紐⑤몢 ??硫붿꽌?쒕? ?듯빐 2?섏씠利덈? ?쒖옉?⑸땲??
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
        // ?몄뒪?숉꽣 李몄“媛 鍮꾩뼱 ?덉뼱?????덉쓽 湲곕낯 ?ㅻ툕?앺듃瑜?李얠븘 理쒕????먮룞 ?곌껐?⑸땲??
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
            boss = FindFirstObjectByType<GiantDrone>();

        if (cam == null)
        {
            if (roomCameraController != null)
                cam = roomCameraController.GetComponent<Camera>();

            if (cam == null)
                cam = Camera.main;
        }

        CacheBossStartPosition();
    }

    private void CacheBossStartPosition()
    {
        if (hasBossStartPosition || boss == null)
            return;

        bossStartPosition = boss.transform.position;
        hasBossStartPosition = true;
    }

    private void HideSceneBossUntilEnter()
    {
        if (!hideSceneBossUntilRoomEntered || boss == null || bossSpawned)
            return;

        // ?ъ뿉 誘몃━ 諛곗튂??蹂댁뒪???낆옣 ?꾩뿉??蹂댁씠吏 ?딄쾶 ?먭퀬, ?낆옣 ?쒓컙 吏???꾩튂?먯꽌 ?쒖꽦?뷀빀?덈떎.
        if (boss.gameObject.scene.IsValid())
        {
            boss.gameObject.SetActive(false);
        }
    }

    private void ActivateBossIfNeeded()
    {
        if (bossSpawned)
            return;

        if (boss == null)
        {
            Debug.LogWarning("Stage1BossRoomController has no GiantDrone assigned.", this);
            return;
        }

        RestoreBossStartPosition();
        boss.deactivateOnDeathInsteadOfDestroy = true;
        boss.gameObject.SetActive(true);
        boss.PrepareForBossRoomActivation(cam);
        bossSpawned = true;
    }

    private void RestoreBossStartPosition()
    {
        if (!hasBossStartPosition || boss == null)
            return;

        boss.transform.position = bossStartPosition;
        Physics2D.SyncTransforms();
    }

    private void SubscribeToBoss()
    {
        if (boss == null)
            return;

        boss.HalfHealthReached -= OnBossHalfHealthReached;
        boss.HalfHealthReached += OnBossHalfHealthReached;
        boss.Died -= OnBossDied;
        boss.Died += OnBossDied;
    }

    private void UnsubscribeFromBoss()
    {
        if (boss != null)
        {
            boss.HalfHealthReached -= OnBossHalfHealthReached;
            boss.Died -= OnBossDied;
        }
    }

    private void OnBossHalfHealthReached()
    {
        if (phase == BossRoomPhase.Phase1)
            StartPhase2();
    }

    private void OnBossDied()
    {
        UnsubscribeFromBoss();
        bossCleared = true;
        bossSpawned = true;
        phase = BossRoomPhase.Waiting;
        phase2Started = false;
        phase2Timer = 0f;

        RemoveBossLockWalls();
        ClearBossRoomEnemies();

        if (roomCameraController != null)
        {
            roomCameraController.enabled = true;
            roomCameraController.ResetToPlayerRoom();
        }
        if (boss == null || !boss.deactivateOnDeathInsteadOfDestroy)
            boss = null;
    }

    private void MoveCameraToPhase1Position()
    {
        // 1?섏씠利덈뒗 諛⑹쓽 ?섎떒??湲곗??쇰줈 移대찓?쇰? 怨좎젙?⑸땲??
        Vector3 target = GetPhase1CameraPosition();

        cam.transform.position = Vector3.Lerp(
            cam.transform.position,
            target,
            Time.deltaTime * phase1FollowSpeed);
    }

    private void SnapCameraToPhase1Position()
    {
        // 蹂댁뒪瑜?耳쒓린 ?꾩뿉 移대찓?쇰? 癒쇱? 蹂댁뒪猷??꾩튂濡?怨좎젙?댁꽌 蹂댁뒪??移대찓??寃쎄퀎 蹂댁젙???됰슧???꾩튂瑜?湲곗??쇰줈 ?뚯? ?딄쾶 ?⑸땲??
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

        if (bossCleared)
            return;

        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (bossRoom.GetBounds().Contains(player.position))
            EnterBossRoom();
    }

    private void MoveCameraUp()
    {
        // 2?섏씠利덈뒗 諛⑹쓽 ?곷떒源뚯?留?移대찓?쇨? ?щ씪媛?꾨줉 ?쒗븳?⑸땲??
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

        // 2?섏씠利덉뿉?쒕뒗 移대찓??以묒떖蹂대떎 ?댁쭩 ?꾩そ??蹂댁뒪??湲곗? ?믪씠濡??쇱븘 ?붾㈃???섎━吏 ?딄쾶 ?쒕떎.
        boss.FollowBossRoomCamera(cam, phase2BossCameraYOffset, phase2BossFollowSpeed);
    }

    private void CreateBossLockWalls()
    {
        RemoveBossLockWalls();

        if (bossRoom == null)
            return;

        Bounds bounds = bossRoom.GetBounds();
        float thickness = Mathf.Max(0.05f, lockWallThickness);
        float padding = Mathf.Max(0f, lockWallPadding);
        float width = bounds.size.x + padding * 2f + thickness * 2f;
        float height = bounds.size.y + padding * 2f + thickness * 2f;

        CreateBossLockWall("Stage 1 Boss Wall Top",
            new Vector3(bounds.center.x, bounds.max.y + padding + thickness * 0.5f, 0f),
            new Vector2(width, thickness));

        CreateBossLockWall("Stage 1 Boss Wall Bottom",
            new Vector3(bounds.center.x, bounds.min.y - padding - thickness * 0.5f, 0f),
            new Vector2(width, thickness));

        CreateBossLockWall("Stage 1 Boss Wall Left",
            new Vector3(bounds.min.x - padding - thickness * 0.5f, bounds.center.y, 0f),
            new Vector2(thickness, height));

        CreateBossLockWall("Stage 1 Boss Wall Right",
            new Vector3(bounds.max.x + padding + thickness * 0.5f, bounds.center.y, 0f),
            new Vector2(thickness, height));
    }

    private void CreateBossLockWall(string wallName, Vector3 wallPosition, Vector2 wallSize)
    {
        GameObject wall = new GameObject(wallName);
        wall.transform.position = wallPosition;
        wall.transform.SetParent(transform, true);

        int layer = LayerMask.NameToLayer(lockWallLayerName);
        if (layer >= 0)
            wall.layer = layer;

        BoxCollider2D wallCollider = wall.AddComponent<BoxCollider2D>();
        wallCollider.size = wallSize;
        wallCollider.isTrigger = false;

        bossLockWalls.Add(wall);
    }

    private void RemoveBossLockWalls()
    {
        foreach (GameObject wall in bossLockWalls)
        {
            if (wall != null)
                Destroy(wall);
        }

        bossLockWalls.Clear();
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
        if (!resetOnPlayerDeath)
            return;

        if (bossCleared)
            return;

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

        // ?뚮젅?댁뼱媛 蹂댁뒪?꾩뿉??二쎌쑝硫?蹂댁뒪猷??곹깭瑜??낆옣 ?꾩쑝濡??뚮젮 ?ㅼ쓬 ?꾩쟾??媛숈? 議곌굔?쇰줈 ?쒖옉?섍쾶 ?⑸땲??
        phase = BossRoomPhase.Waiting;
        phase2Started = false;
        phase2Timer = 0f;
        bossSpawned = false;
        bossCleared = false;

        RemoveBossLockWalls();
        UnsubscribeFromBoss();

        if (boss != null)
        {
            RestoreBossStartPosition();
            boss.ResetForBossRoomRetry();

            if (hideSceneBossUntilRoomEntered)
                boss.gameObject.SetActive(false);
        }
    }

    public void ResetForDebugTeleport()
    {
        phase = BossRoomPhase.Waiting;
        phase2Started = false;
        phase2Timer = 0f;
        bossSpawned = false;
        bossCleared = false;

        RemoveBossLockWalls();
        UnsubscribeFromBoss();
        ResolveReferences();

        if (boss != null)
        {
            RestoreBossStartPosition();
            boss.ResetForBossRoomRetry();

            if (hideSceneBossUntilRoomEntered)
                boss.gameObject.SetActive(false);
        }
    }

    private void ClearBossRoomEnemies()
    {
        foreach (GameObject enemy in enemiesToDestroyOnBossDeath)
            DestroyIfPresent(enemy);

        if (!destroyEnemiesInsideRoomOnBossDeath || bossRoom == null)
            return;

        DestroyComponentsInsideRoom<EnemyBase>();
        DestroyComponentsInsideRoom<GhostEnemy>();
        DestroyComponentsInsideRoom<ShadowEnemy>();
        DestroyComponentsInsideRoom<HealDrone>();
    }

    private void DestroyComponentsInsideRoom<T>() where T : Component
    {
        Bounds bounds = bossRoom.GetBounds();
        T[] components = FindObjectsByType<T>(FindObjectsSortMode.None);

        foreach (T component in components)
        {
            if (component != null && bounds.Contains(component.transform.position))
                DestroyIfPresent(component.gameObject);
        }
    }

    private void DestroyIfPresent(GameObject obj)
    {
        if (obj == null)
            return;

        if (boss != null && obj == boss.gameObject)
            return;

        Destroy(obj);
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
