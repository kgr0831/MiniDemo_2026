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
    public LayerMask obstacleLayerMask;    // 순수 벽(장애물)만 포함된 레이어 (배회 목적지 검사용)

    // 내부 변수
    [HideInInspector] public Vector2 lastMoveDir = Vector2.down;
    private Rigidbody2D rb;
    private Vector2 spawnPosition;
    private Vector2 patrolTarget;
    private float patrolWaitTimer;
    private bool isWaiting = false;

    // 지그재그 방지용 (Hysteresis)
    private Vector2 lastPrimaryDir = Vector2.zero;
    private Vector2 currentSlideDir = Vector2.zero; // ★ 슬라이딩 방향 유지용
    
    // Trigger 로 감지한 플레이어
    private Transform playerTransform;

    // 길찾기용 변수
    private bool isPathfinding = false;
    private List<Vector2> aStarPath;
    private int currentPathIndex = 0;
    private Vector2 lastPathTarget;          // 마지막으로 경로를 구했을 때의 플레이어 위치
    private float pathRecalcThreshold = 2f;  // 플레이어가 이 이상 움직여야 경로 재계산

    // 시야 떨림(Jitter) 방지용 (Debounce)
    private int losConfirmCount = 0;
    private const int LosConfirmThreshold = 3; // 3회 연속 동일 판정 시 전환
    private bool currentLineOfSightState = true;

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
        WaitForSeconds chaseWait = new WaitForSeconds(0.1f);
        while (true)
        {
            // 플레이어가 Trigger를 통해 감지된 상태(= Chase 상태)일 때만 가동
            if (playerTransform != null)
            {
                PerformHybridChaseDecision();
            }
            yield return chaseWait; // 0.1초 쿨타임 (기존 0.2초에서 단축)
        }
    }

    void PerformHybridChaseDecision()
    {
        Vector2 directionToPlayer = (playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector2.Distance(transform.position, playerTransform.position);

        // 1. Raycast 발사 (눈 역할) - 선이 얇아서 기둥 옆을 긁고 가는 문제를 해결하기 위해 CircleCast 적용
        // 슬라임의 반경보다 약간 작은 원통을 발사하여, 너무 민감하게 벽에 스쳤다고 오판하지 않도록 함.
        float losThickness = 0.2f; 
        RaycastHit2D[] hits = Physics2D.CircleCastAll(transform.position, losThickness, directionToPlayer, distanceToPlayer, targetLayerMask);

        // ★ 핵심 수정: 거리순 정렬하여 벽 뒤의 플레이어를 오판하지 않도록 함
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool rawLineOfSight = true; // 아무것도 안맞으면 벽 없음 = 시야 확보

        foreach (RaycastHit2D hit in hits)
        {
            // 자신이나 무시할 트리거는 검사에서 뺌
            if (hit.collider.isTrigger || hit.collider.gameObject == gameObject) continue;

            // 가장 처음 부딪힌 "진짜 물체"가 플레이어인지 벽인지 판별
            if (hit.collider.GetComponent<PlayerController>() != null || hit.collider.CompareTag("Player"))
            {
                // [상황 A: 시야 확보]
                rawLineOfSight = true;
                break;
            }
            else
            {
                // [상황 B: 시야 차단(벽)]
                rawLineOfSight = false;
                break;
            }
        }

        // ★ 핵심 수정: 시야 떨림(Jitter) 방지를 위한 Debounce 로직
        if (rawLineOfSight == currentLineOfSightState)
        {
            losConfirmCount = 0;
        }
        else
        {
            losConfirmCount++;
            if (losConfirmCount >= LosConfirmThreshold)
            {
                currentLineOfSightState = rawLineOfSight;
                losConfirmCount = 0;
            }
        }

        // 2. 하이브리드 분기 (상태 업데이트)
        if (currentLineOfSightState)
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
                Debug.Log($"[MonMove] 대기 시간 끝. 새로운 배회 목적지 설정: {patrolTarget}");
            }
            rb.linearVelocity = Vector2.zero;
            return;
        }

        float distance = Vector2.Distance(transform.position, patrolTarget);

        // 직교 이동은 정확히 점에 도달하기 힘드므로 거리를 넉넉하게 체크
        if (distance < 0.5f)
        {
            isWaiting = true;
            patrolWaitTimer = Random.Range(patrolWaitMin, patrolWaitMax);
            rb.linearVelocity = Vector2.zero;
            Debug.Log($"[MonMove] 배회 목적지 도달! 대기 시작 ({patrolWaitTimer:F1}초). 현재 위치: {transform.position}");
        }
        else
        {
            // 단순 직교 이동 (대각선 방지)
            Vector2 moveDir = GetStrictOrthogonalDirection(patrolTarget);
            rb.linearVelocity = moveDir * moveSpeed;
            if (moveDir != Vector2.zero) lastMoveDir = moveDir;
            
            // 너무 로그가 많으면 곤란하므로 생략하지만, 이동 중임을 알 수 있음
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
                
                // 타일에 대략 도달하면 다음 타일로 (여유를 0.15f로 줄여 좁은 통로 충돌 방지)
                if (Vector2.Distance(transform.position, targetTile) < 0.15f)
                {
                    currentPathIndex++;
                    if (currentPathIndex >= aStarPath.Count) 
                    {
                        rb.linearVelocity = Vector2.zero;
                        return;
                    }
                    targetTile = aStarPath[currentPathIndex];
                }

                // ★ A* 경로를 따라갈 때는 Hysteresis 없이 부드러운 직교 스냅
                // 기존 단순 스냅이 너무 거칠어 떨림(Jitter)을 유발하므로 보완
                Vector2 moveDir = GetSmoothOrthogonalSnap(targetTile);
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
    /// A* 경로 추적용: 목표 타일까지의 부드러운 직교 축 스냅 함수.
    /// 너무 날카롭게 축을 전환하면 Jitter(떨림)가 발생하므로 부드럽게 전환.
    /// </summary>
    private Vector2 GetSmoothOrthogonalSnap(Vector2 targetPos)
    {
        Vector2 dir = targetPos - (Vector2)transform.position;
        if (dir.sqrMagnitude < 0.01f) return Vector2.zero;

        // 단순히 더 먼 축으로 이동하되, 이미 축 정렬이 많이 되었다면 그대로 밀고 감
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y) * 1.5f)
            return new Vector2(Mathf.Sign(dir.x), 0f);
        else if (Mathf.Abs(dir.y) > Mathf.Abs(dir.x) * 1.5f)
            return new Vector2(0f, Mathf.Sign(dir.y));
        else 
        {
            // 대각선 근처에 있을 경우, 기존 이동하던 축(lastPrimaryDir)을 우선시하여 Jitter 방지
            bool preferX = Mathf.Abs(lastMoveDir.x) > 0.5f;
            if (preferX) return new Vector2(Mathf.Sign(dir.x), 0f);
            else return new Vector2(0f, Mathf.Sign(dir.y));
        }
    }

    // ──────────────── 4방향 직교 이동 로직 (지그재그 방지 포함) ────────────────

    /// <summary>
    /// 목적지까지 무조건 상하좌우 1개 축으로만 움직이게 합니다. (대각선 방지)
    /// Hysteresis를 적용해 한 축으로 움직이기 시작하면 해당 축이 끝날 때까지 1자를 유지합니다.
    /// 장애물에 막히면 슬라이딩(다른 축으로 우회)을 시도합니다.
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
        if (Mathf.Abs(lastPrimaryDir.x) > 0.5f && absX > 0.1f) preferX = true;
        if (Mathf.Abs(lastPrimaryDir.y) > 0.5f && absY > 0.1f) preferX = false;

        Vector2 primaryDir = preferX ? new Vector2(Mathf.Sign(dir.x), 0f) : new Vector2(0f, Mathf.Sign(dir.y));
        Vector2 secondaryDir = preferX ? new Vector2(0f, Mathf.Sign(dir.y)) : new Vector2(Mathf.Sign(dir.x), 0f);

        // 만약 주 방향이나 보조 방향의 목표 거리가 너무 짧으면(0.1f 미만) 억지로 이동하지 않도록 0으로 덮어씀
        if (preferX && absY < 0.1f) secondaryDir = Vector2.zero;
        if (!preferX && absX < 0.1f) secondaryDir = Vector2.zero;

        // 장애물 슬라이딩: 주 이동 방향으로 벽이 막혀있다면 보조 방향으로 꺾음
        LayerMask maskToCheck = obstacleLayerMask != 0 ? obstacleLayerMask : (AStarManager.Instance != null ? AStarManager.Instance.obstacleMask : targetLayerMask);
        
        // 슬라임 반경 고려하여 충돌 체크 (0.4f)
        bool isPrimaryBlocked = Physics2D.CircleCast(transform.position, 0.4f, primaryDir, 0.3f, maskToCheck);
        
        Vector2 finalDir;
        if (isPrimaryBlocked)
        {
            // 이미 슬라이딩 중이라면, 방해받지 않는 이상 계속 같은 방향으로 슬라이딩 (지터 방지)
            if (currentSlideDir != Vector2.zero && !Physics2D.CircleCast(transform.position, 0.4f, currentSlideDir, 0.3f, maskToCheck))
            {
                finalDir = currentSlideDir;
            }
            else if (secondaryDir != Vector2.zero && !Physics2D.CircleCast(transform.position, 0.4f, secondaryDir, 0.3f, maskToCheck))
            {
                // 보조 방향이 막혀있지 않다면 우선적으로 우회
                currentSlideDir = secondaryDir;
                finalDir = secondaryDir;
            }
            else
            {
                // ★ 핵심: 플레이어와 완벽하게 1자로 서 있을 때 벽에 막힌 상황.
                // secondaryDir이 0이거나 막혔으므로, 강제로 직각(Perpendicular) 방향 중 뚫린 곳을 찾아서 슬라이딩 시도
                Vector2 slide1 = new Vector2(-primaryDir.y, primaryDir.x); // 시계 방향 90도
                Vector2 slide2 = new Vector2(primaryDir.y, -primaryDir.x); // 반시계 방향 90도

                bool isSlide1Blocked = Physics2D.CircleCast(transform.position, 0.4f, slide1, 0.3f, maskToCheck);
                bool isSlide2Blocked = Physics2D.CircleCast(transform.position, 0.4f, slide2, 0.3f, maskToCheck);

                if (!isSlide1Blocked) currentSlideDir = slide1;
                else if (!isSlide2Blocked) currentSlideDir = slide2;
                else currentSlideDir = Vector2.zero; // 양옆도 막혔으면 정지 (A*가 알아서 경로 재계산할 것임)
                
                finalDir = currentSlideDir;
            }
        }
        else
        {
            currentSlideDir = Vector2.zero;
            finalDir = primaryDir;
        }

        // 실제 이동인 finalDir은 저장하되, Hysteresis 기준은 주방향(primaryDir)으로 유지해야
        // 슬라이딩 도중에도 목표를 향한 원래의 목적(X or Y 접근)을 잃지 않음
        if (finalDir != Vector2.zero) lastPrimaryDir = primaryDir;
        return finalDir;
    }

    public void StopMoving()
    {
        if (rb != null)
            rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 넉백되거나 강제로 이동을 멈춰야 할 때, 기존 진행 중이던 A* 경로를 무효화합니다.
    /// </summary>
    public void InvalidatePath()
    {
        aStarPath = null;
        isPathfinding = false;
        currentPathIndex = 0;
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
        // 최장 10번 재시도하여 벽 안쪽이 아닌 유효한 이동 지점을 찾음
        LayerMask maskToCheck = obstacleLayerMask != 0 ? obstacleLayerMask : (AStarManager.Instance != null ? AStarManager.Instance.obstacleMask : targetLayerMask);

        for (int i = 0; i < 10; i++)
        {
            Vector2 randomTarget = spawnPosition + Random.insideUnitCircle * patrolRadius;
            
            // 타겟 지점에 장애물이 있는지 확인 (반경 0.4f)
            if (!Physics2D.OverlapCircle(randomTarget, 0.4f, maskToCheck))
            {
                patrolTarget = randomTarget;
                return;
            }
        }
        
        // 10번 실패하면 제자리 유지 (벽에 끼어 진동하는 현상 방지)
        patrolTarget = transform.position;
        Debug.LogWarning($"[MonMove] 배회 목적지를 10번이나 찾지 못해 제자리로 설정합니다. 검사한 LayerMask: {maskToCheck.value}");
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
