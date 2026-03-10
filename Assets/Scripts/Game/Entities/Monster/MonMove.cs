using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 적 움직임 (배회, 추적)
// 하이브리드 추적 AI (Raycast 시야 체크 + A* 보완 + Vector 직선 이동 선호) 적용
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

    [Header("하이브리드 추적 설정")]
    public float attackRange = 1.2f;       // 공격 사거리
    public LayerMask targetLayerMask = ~0; // Player와 Wall(장애물)이 포함된 레이어

    // 내부 변수
    [HideInInspector] public Vector2 lastMoveDir = Vector2.down;
    private Rigidbody2D rb;
    private Vector2 spawnPosition;
    private Vector2 patrolTarget;
    private float patrolWaitTimer;
    private bool isWaiting = false;

    // 지그재그 방지용 (Hysteresis)
    private Vector2 lastPrimaryDir = Vector2.zero;
    
    // Trigger 로 감지한 플레이어
    private Transform playerTransform;

    // 길찾기용 변수
    private bool isPathfinding = false;
    private List<Vector2> aStarPath;
    private int currentPathIndex = 0;
    private Vector2 lastPathTarget;          // 마지막으로 경로를 구했을 때의 플레이어 위치
    private float pathRecalcThreshold = 2f;  // 플레이어가 이 이상 움직여야 경로 재계산

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spawnPosition = transform.position;
    }

    void Start()
    {
        SetNewPatrolTarget();
        StartCoroutine(UpdateChaseLogicRoutine());
    }

    // ──────────────── 하이브리드 추적 코루틴 (0.2초 주기) ────────────────

    IEnumerator UpdateChaseLogicRoutine()
    {
        WaitForSeconds chaseWait = new WaitForSeconds(0.2f);
        while (true)
        {
            // 플레이어가 Trigger를 통해 감지된 상태(= Chase 상태)일 때만 가동
            if (playerTransform != null)
            {
                PerformHybridChaseDecision();
            }
            yield return chaseWait; // 0.2초 쿨타임 (최적화)
        }
    }

    void PerformHybridChaseDecision()
    {
        Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // 1. Raycast 발사 (눈 역할) - 자신 통과를 위해 All 사용
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, directionToPlayer, distanceToPlayer, targetLayerMask);

        bool hasLineOfSight = true; // 아무것도 안맞으면 벽 없음 = 시야 확보

        foreach (RaycastHit2D hit in hits)
        {
            // 자신이나 무시할 트리거는 검사에서 뺌
            if (hit.collider.isTrigger || hit.collider.gameObject == gameObject) continue;

            // 가장 처음 부딪힌 "진짜 물체"가 플레이어인지 벽인지 판별
            if (hit.collider.GetComponent<PlayerController>() != null || hit.collider.CompareTag("Player"))
            {
                // [상황 A: 시야 확보]
                hasLineOfSight = true;
                break;
            }
            else
            {
                // [상황 B: 시야 차단(벽)]
                hasLineOfSight = false;
                break;
            }
        }

        // 2. 하이브리드 분기 (상태 업데이트)
        if (hasLineOfSight)
        {
            // A* 정지 및 Vector 직선 이동 플래그 켜기
            isPathfinding = false;
            aStarPath = null; // 시야 확보되면 기존 경로 폐기
        }
        else
        {
            // A* 길찾기 가동
            isPathfinding = true;

            // ★ 핵심 수정: 이미 유효한 경로가 있고, 플레이어가 별로 안 움직였으면 경로를 재탐색하지 않음!
            bool needNewPath = false;

            if (aStarPath == null || aStarPath.Count == 0)
            {
                needNewPath = true; // 경로 자체가 없음
            }
            else if (currentPathIndex >= aStarPath.Count)
            {
                needNewPath = true; // 현재 경로를 다 따라감
            }
            else if (Vector2.Distance((Vector2)playerTransform.position, lastPathTarget) > pathRecalcThreshold)
            {
                needNewPath = true; // 플레이어가 많이 움직여서 경로가 구식이 됨
            }

            if (needNewPath && AStarManager.Instance != null && AStarManager.Instance.gameObject.activeInHierarchy)
            {
                // 플레이어와 자신 사이의 거리를 계산해 그 주변 타일만 실시간 갱신
                float distance = Vector2.Distance(transform.position, playerTransform.position);
                Vector2 centerPos = ((Vector2)transform.position + (Vector2)playerTransform.position) / 2f;
                
                // 넉넉하게 반경을 잡아 지도 업데이트
                AStarManager.Instance.UpdateGridRegion(centerPos, distance * 0.8f + 2f);

                List<Vector2> newPath = AStarManager.Instance.GetPath(transform.position, playerTransform.position);
                
                if (newPath != null && newPath.Count > 0)
                {
                    aStarPath = newPath;
                    currentPathIndex = 0;
                    lastPathTarget = (Vector2)playerTransform.position;
                    Debug.Log($"[MonMove] A* 경로 탐색 성공! 노드 {aStarPath.Count}개");
                }
                else
                {
                    Debug.LogWarning("[MonMove] A* 경로를 찾을 수 없습니다! 장애물로 완전히 막혀있을 수 있습니다.");
                }
            }
        }
    }

    // ──────────────── 행동 로직 (매 프레임 Update에서 호출) ────────────────

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
            // 단순 직교 이동 (대각선 방지)
            Vector2 moveDir = GetStrictOrthogonalDirection(patrolTarget);
            rb.linearVelocity = moveDir * moveSpeed;
            if (moveDir != Vector2.zero) lastMoveDir = moveDir;
        }
    }

    public void HandleChase()
    {
        if (playerTransform == null) return;

        if (!isPathfinding)
        {
            // 시야 확보됨: 직교 방향 이동 (Hysteresis 적용 - 부드러움 방지)
            Vector2 moveDir = GetStrictOrthogonalDirection(playerTransform.position);
            rb.linearVelocity = moveDir * chaseSpeed;
            if (moveDir != Vector2.zero) lastMoveDir = moveDir;
        }
        else
        {
            // 시야 차단됨: A* 타일 경로 추적
            if (aStarPath != null && currentPathIndex < aStarPath.Count)
            {
                Vector2 targetTile = aStarPath[currentPathIndex];
                
                // 타일에 대략 도달하면 다음 타일로 (여유를 0.3f로 늘려 부드러움 방지)
                if (Vector2.Distance(transform.position, targetTile) < 0.3f)
                {
                    currentPathIndex++;
                    if (currentPathIndex >= aStarPath.Count) 
                    {
                        rb.linearVelocity = Vector2.zero;
                        return;
                    }
                    targetTile = aStarPath[currentPathIndex];
                }

                // ★ A* 경로를 따라갈 때는 Hysteresis 없이 단순 축 스냅!
                // A* 노드가 "위로 꺾어" 라고 하면 즉시 위로 꺾어야 함 (관성이 방해하면 안 됨)
                Vector2 moveDir = GetSimpleOrthogonalSnap(targetTile);
                rb.linearVelocity = moveDir * chaseSpeed;
                if (moveDir != Vector2.zero) lastMoveDir = moveDir;
            }
            else
            {
                // 경로 끝이거나 경로 못찾음 = 대기
                rb.linearVelocity = Vector2.zero;
            }
        }
    }

    /// <summary>
    /// A* 경로 추적용: Hysteresis 없이 단순히 더 먼 축으로 스냅하는 함수.
    /// A* 노드가 꼾으라고 하면 즉시 꼾어야 하므로 관성을 적용하면 안 됩니다.
    /// </summary>
    private Vector2 GetSimpleOrthogonalSnap(Vector2 targetPos)
    {
        Vector2 dir = targetPos - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.01f) return Vector2.zero;

        // 단순히 더 먼 축으로 이동 (Hysteresis 없음)
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
            return new Vector2(Mathf.Sign(dir.x), 0f);
        else
            return new Vector2(0f, Mathf.Sign(dir.y));
    }

    // ──────────────── 4방향 직교 이동 로직 (지그재그 방지 포함) ────────────────

    /// <summary>
    /// 목적지까지 무조건 상하좌우 1개 축으로만 움직이게 합니다. (대각선 방지)
    /// Hysteresis를 적용해 한 축으로 움직이기 시작하면 해당 축이 끝날 때까지 1자를 유지합니다.
    /// </summary>
    private Vector2 GetStrictOrthogonalDirection(Vector2 targetPos)
    {
        Vector2 dir = targetPos - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.01f) return Vector2.zero;

        float absX = Mathf.Abs(dir.x);
        float absY = Mathf.Abs(dir.y);

        // 기본적으로는 더 먼 축을 주 이동 방향으로 설정
        bool preferX = absX > absY;

        // Hysteresis (지그재그 방지): 이전에 이동하던 축을 계속 유지하려는 성질
        // X축으로 이동 중이었고 그쪽으로 갈 길이 조금이라도(0.1f) 남아있으면 계속 X축 유지
        if (Mathf.Abs(lastPrimaryDir.x) > 0.5f && absX > 0.1f) preferX = true;
        // Y축으로 이동 중이었고 갈 길이 남아있으면 계속 Y축 유지
        if (Mathf.Abs(lastPrimaryDir.y) > 0.5f && absY > 0.1f) preferX = false;

        Vector2 finalDir;

        if (preferX)
        {
            finalDir = new Vector2(Mathf.Sign(dir.x), 0f);
        }
        else
        {
            finalDir = new Vector2(0f, Mathf.Sign(dir.y));
        }

        lastPrimaryDir = finalDir;
        return finalDir;
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

    public bool IsPlayerInDetectRange()
    {
        return playerTransform != null;
    }

    public bool IsPlayerOutOfRange()
    {
        return false; // 한 번 감지한 플레이어는 절대 놓치지 않습니다.
    }

    public bool IsPlayerInAttackRange()
    {
        return GetDistanceToPlayer() <= attackRange;
    }

    public Vector2 GetDirectionToPlayer()
    {
        if (playerTransform == null) return lastMoveDir;
        
        // 방향은 4방향으로 스냅해서 애니메이션에 예쁘게 들어가도록 전달
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
        if (playerTransform != null) return; // 이미 감지했으면 무시

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            playerTransform = player.transform;
            Debug.Log($"[MonMove] 하이브리드 AI: 플레이어 최초 감지! 추적 시작.");
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
        
        // A* 경로 시각화 (인스펙터 확인용)
        if (aStarPath != null && isPathfinding)
        {
            Gizmos.color = Color.cyan;
            for (int i = currentPathIndex; i < aStarPath.Count - 1; i++)
            {
                Gizmos.DrawLine(aStarPath[i], aStarPath[i+1]);
            }
        }
    }
}
