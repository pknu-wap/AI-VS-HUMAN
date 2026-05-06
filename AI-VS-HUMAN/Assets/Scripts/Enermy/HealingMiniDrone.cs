using UnityEngine;

public class HealingMiniDrone : EnemyBase
{
    private enum State
    {
        FlyingIn,
        Attached
    }

    [Header("Healing")]
    public float healPerSecond = 6f;
    public float attachedLifetime = 8f;

    [Header("Movement")]
    public float flyInSpeed = 7f;
    public float attachedFollowSpeed = 10f;
    public float attachDistance = 0.08f;

    private GiantDroneBoss boss;
    private Vector2 attachOffset;
    private float lifeTimer;
    private State state = State.FlyingIn;
    private LineRenderer healBeam;
    private bool notifiedBoss;

    protected override void Start()
    {
        base.Start();
    }

    private void Update()
    {
        if (isDead)
            return;

        if (boss == null || boss.IsDead())
        {
            Die();
            return;
        }

        if (state == State.FlyingIn)
        {
            FlyToBoss();
            return;
        }

        lifeTimer += Time.deltaTime;

        if (lifeTimer >= attachedLifetime)
        {
            Die();
            return;
        }

        StayAttached();
        boss.Heal(healPerSecond * Time.deltaTime);
        UpdateHealBeam();
    }

    public void Init(GiantDroneBoss owner, Vector2 offsetFromBoss)
    {
        boss = owner;
        attachOffset = offsetFromBoss;
    }

    protected override void Die()
    {
        if (healBeam != null)
            Destroy(healBeam.gameObject);

        NotifyBoss();
        base.Die();
    }

    private void OnDestroy()
    {
        NotifyBoss();

        if (healBeam != null)
            Destroy(healBeam.gameObject);
    }

    private void FlyToBoss()
    {
        Vector2 target = GetAttachPosition();
        transform.position = Vector2.MoveTowards(
            transform.position,
            target,
            flyInSpeed * Time.deltaTime
        );

        if (Vector2.Distance(transform.position, target) <= attachDistance)
            AttachToBoss();
    }

    private void StayAttached()
    {
        transform.position = Vector2.MoveTowards(
            transform.position,
            GetAttachPosition(),
            attachedFollowSpeed * Time.deltaTime
        );
    }

    private void AttachToBoss()
    {
        state = State.Attached;
        lifeTimer = 0f;
        CreateHealBeam();
    }

    private Vector2 GetAttachPosition()
    {
        if (boss == null)
            return transform.position;

        return (Vector2)boss.transform.position + attachOffset;
    }

    private void CreateHealBeam()
    {
        if (healBeam != null)
            return;

        GameObject beamObj = new GameObject("HealBeam");
        healBeam = beamObj.AddComponent<LineRenderer>();
        healBeam.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        healBeam.startColor = new Color(0.2f, 1f, 0.7f, 0.75f);
        healBeam.endColor = new Color(0.2f, 1f, 0.7f, 0.05f);
        healBeam.startWidth = 0.08f;
        healBeam.endWidth = 0.02f;
        healBeam.positionCount = 2;
        healBeam.useWorldSpace = true;
        healBeam.sortingOrder = 50;
    }

    private void UpdateHealBeam()
    {
        if (healBeam == null || boss == null)
            return;

        healBeam.SetPosition(0, transform.position);
        healBeam.SetPosition(1, boss.transform.position);
    }

    private void NotifyBoss()
    {
        if (notifiedBoss || boss == null)
            return;

        notifiedBoss = true;
        boss.NotifyHealingMiniDroneDestroyed();
    }
}
