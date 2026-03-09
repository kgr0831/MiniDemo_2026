using UnityEngine;

// 적 움직임 (배회, 추적)
// 4방향 직교 이동 및 Trigger 기반 액적 감지 적용
[RequireComponent(typeof(Rigidbody2D))]
public class MonMove : MonoBehaviour 
{ 
    [Header("이동 설정")]
    public float moveSpeed = 1.5f;         // 일반 이동 속도
    public float chaseSpeed = 2.5f;        // 추적 속도

    [Header("배회(Patrol) 설정")]
    public float patrolRadius = 3f;        // 스폰 지점 기준 배회 반경
    public float patrolWaitMin = 1f;       // 배회 후 대기 최소 시간
    public float patrolWaitMax = 3f;       // 배회 후 대기 최대 시간

    [Header("거리 설정")]
    public float attackRange = 1.2f;       // 공격 사거리 (이건 계산용 유지)

    // 내부 변수
    [HideInInspector] public Vector2 lastMoveDir = Vector2.down;
    private Rigidbody2D rb;
    private Vector2 spawnPosition;
    private Vector2 patrolTarget;
    private float patrolWaitTimer;
    private bool isWaiting = false;
    
    // 회피 로직 변수
    private Vector2 currentAvoidanceDir = Vector2.zero;
    private float avoidanceTimer = 0f;
    
    // Trigger 로 감지한 플레이어
    private Transform playerTransform;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;
    }

    void Start()
    {
        // FindObjectOfType 제거! 오직 Trigger로만 플레이어를 찾습니다.
        SetNewPatrolTarget();
    }

    // ──────────────── 4방향 직교 이동 로직 (Pathfinding 우회) ────────────────

    /// <summary>
    /// 목적지까지의 4방향(상하좌우) 중 가장 최적의 이동 방향 하나를 반환합니다.
    /// 장애물(나무, 바위)이 있으면 우회합니다.
    /// </summary>
    private Vector2 GetOrthogonalDirection(Vector2 targetPos)
    {
        Vector2 dir = targetPos - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.01f) return Vector2.zero;

        Vector2 primaryDir, secondaryDir;

        // X축과 Y축 차이 중 더 큰 쪽을 주 이동 방향(Primary)으로 설정
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            primaryDir = new Vector2(Mathf.Sign(dir.x), 0f);
            secondaryDir = new Vector2(0f, Mathf.Sign(dir.y));
            // y차이가 0이면 임의의 수직 직교 방향 설정
            if (secondaryDir == Vector2.zero) secondaryDir = new Vector2(0f, 1f); 
        }
        else
        {
            primaryDir = new Vector2(0f, Mathf.Sign(dir.y));
            secondaryDir = new Vector2(Mathf.Sign(dir.x), 0f);
            if (secondaryDir == Vector2.zero) secondaryDir = new Vector2(1f, 0f);
        }

        // 1. 회피 모드: 회피 타이머가 켜져 있으면, 현재 회피 방향을 먼저 유지해본다 (기둥에 비비기)
        if (avoidanceTimer > 0f)
        {
            avoidanceTimer -= Time.deltaTime;
            if (!IsDirectionBlocked(currentAvoidanceDir))
            {
                return currentAvoidanceDir;
            }
            else
            {
                avoidanceTimer = 0f; // 회피 방향마저 막히면 취소
            }
        }

        // 2. 주 방향 체크
        if (!IsDirectionBlocked(primaryDir))
        {
            return primaryDir;
        }

        // 3. 주 방향이 막혔다면 보조 방향 체크
        if (!IsDirectionBlocked(secondaryDir))
        {
            // 보조 방향으로 꺾고, 0.5초간 그 방향을 유지하여 벽을 쭉 타고 넘어가도록 함
            currentAvoidanceDir = secondaryDir;
            avoidanceTimer = 0.5f; 
            return secondaryDir;
        }

        // 4. 보조 방향도 막혔다면 보조 방향의 반대쪽 체크
        Vector2 altSecondary = -secondaryDir;
        if (!IsDirectionBlocked(altSecondary))
        {
            currentAvoidanceDir = altSecondary;
            avoidanceTimer = 0.5f;
            return altSecondary;
        }

        // 5. 다 막혔으면 주 방향의 반대쪽 (후퇴)
        Vector2 altPrimary = -primaryDir;
        if (!IsDirectionBlocked(altPrimary))
        {
            currentAvoidanceDir = altPrimary;
            avoidanceTimer = 0.5f;
            return altPrimary;
        }

        return Vector2.zero; // 사방이 막힘
    }

    /// <summary>
    /// 해당 방향에 장애물(나무, 바위 등)이 있는지 Raycast로 확인합니다.
    /// </summary>
    private bool IsDirectionBlocked(Vector2 dir)
    {
        // 중심에서 해당 방향으로 0.8 유닛 길이의 레이캐스트를 쏨
        RaycastHit2D hit = Physics2D.Raycast(transform.position, dir, 0.8f);
        
        if (hit.collider != null && !hit.collider.isTrigger && hit.collider.gameObject != gameObject)
        {
            // 플레이어는 장애물로 치지 않음 (때리러 가야 하니까 통과)
            if (hit.collider.GetComponent<PlayerController>() == null)
            {
                return true; // 돌이나 나무에 막힘!
            }
        }
        return false;
    }

    // ──────────────── 행동 로직 ────────────────

    public void HandlePatrol()
    {
        if (isWaiting)
        {
            patrolWaitTimer -= Time.deltaTime;
            if (patrolWaitTimer <= 0f)
            {
                isWaiting = false;
                SetNewPatrolTarget();
            }
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, patrolTarget);

        if (distance < 0.2f)
        {
            isWaiting = true;
            patrolWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax);
            rb.linearVelocity = Vector2.zero;
        }
        else
        {
            // 대각선 대신 4방향 경로 탐색 이동 적용
            Vector2 moveDir = GetOrthogonalDirection(patrolTarget);
            if (moveDir != Vector2.zero)
            {
                rb.linearVelocity = moveDir * moveSpeed;
                lastMoveDir = moveDir;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    public void HandleChase()
    {
        if (playerTransform == null) return;

        // 대각선 대신 4방향 경로 탐색 이동 적용
        Vector2 moveDir = GetOrthogonalDirection(playerTransform.position);
        if (moveDir != Vector2.zero)
        {
            rb.linearVelocity = moveDir * chaseSpeed;
            lastMoveDir = moveDir;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    public void StopMoving()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    // ──────────────── 거리 및 감지 판별 ────────────────

    public float GetDistanceToPlayer()
    {
        if (playerTransform == null) return float.MaxValue;
        return Vector2.Distance(transform.position, playerTransform.position);
    }

    /// <summary>
    /// 플레이어가 Trigger 범위 안에 들어왔으므로 감지됨
    /// </summary>
    public bool IsPlayerInDetectRange()
    {
        return playerTransform != null;
    }

    /// <summary>
    /// 플레이어가 Trigger 범위를 벗어남
    /// </summary>
    public bool IsPlayerOutOfRange()
    {
        return playerTransform == null;
    }

    public bool IsPlayerInAttackRange()
    {
        return GetDistanceToPlayer() <= attackRange;
    }

    public Vector2 GetDirectionToPlayer()
    {
        if (playerTransform == null) return lastMoveDir;
        
        // 공격 방향을 잡을 때도 4방향을 바라보게 처리
        Vector2 dir = playerTransform.position - transform.position;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(dir.y));
    }

    private void SetNewPatrolTarget()
    {
        patrolTarget = spawnPosition + Random.insideUnitCircle * patrolRadius;
    }

    // ──────────────── Trigger 감지 로직 ────────────────

    // 몬스터에 부착된 CircleCollider2D (IsTrigger=true) 안으로 무언가 들어올 때
    void OnTriggerEnter2D(Collider2D other)
    {
        // 이름이나 태그 대신 컴포넌트로 플레이어 유무 확인
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
            Debug.Log($"[MonMove] 플레이어 감지! 추적을 시작합니다.");
        }
    }

    // 플레이어가 시야 밖으로 나갈 때
    void OnTriggerExit2D(Collider2D other)
    {
        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null && player.transform == playerTransform)
        {
            playerTransform = null; // 타겟 소실 -> 배회 모드로 복귀
            Debug.Log($"[MonMove] 플레이어를 잃어버렸습니다.");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
