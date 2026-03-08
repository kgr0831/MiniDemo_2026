using UnityEngine;

// 플레이어 움직임(이동, 회피)
[RequireComponent(typeof(PlayerController))]
public class PlayerMove : MonoBehaviour 
{ 
    private Rigidbody2D rb;
    private PlayerController controller;
    private SpriteRenderer spriteRenderer;

    private Vector2 moveInput;
    public Vector2 lastMoveDir { get; private set; } = Vector2.down; // 기본 바라보는 방향(아래)
    
    // 입력 벡터의 크기가 0.01보다 크면 움직이는 것으로 간주
    public bool IsMoving => moveInput.magnitude > 0.01f;
    public bool IsRunningPressed { get; private set; } // 쉬프트(Shift) 입력 여부
    
    [Header("Movement Settings")]
    public float walkSpeedMultiplier = 0.5f; // 걷기 모드일 때 깎이는 속도 비율

    [Header("Dash Settings")]
    public float dashDistance = 5f; // 대시 총 이동 거리
    public float dashDuration = 0.4f; // 실제 대시가 지속되는 시간
    public AnimationCurve dashSpeedCurve = AnimationCurve.EaseInOut(0, 1, 1, 0); // 대시 속도 곡선 (처음엔 빠르고 끝에 느려짐)
    
    [Header("Dash Visuals")]
    [Range(0f, 1f)] public float dashAlpha = 0.5f; // 대시 중 투명도

    private float dashTimer;
    private Vector2 dashDir;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        controller = GetComponent<PlayerController>();
        spriteRenderer = GetComponent<SpriteRenderer>(); // SpriteRenderer 캐싱

        // 자식 객체에 SpriteRenderer가 있을 경우 대비
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 탑다운 2D 물리 최적화 설정
        if (rb != null)
        {
            rb.gravityScale = 0f; // 중력 제거
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // 얇은 벽뚫기 방지
            rb.freezeRotation = true; // Z축 회전 방지
        }
    }

    // 입력 처리 (Controller의 Update에서 호출됨)
    public void HandleInput()
    {
        // 기존 Input Manager 기준 (WASD or 화살표)
        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        
        // 4방향 고정(Snap) 로직: 대각선 압력을 무시하고 상하좌우 중 하나로만 축을 고정
        if (x != 0 && y != 0)
        {
            // 좌우 입력을 상하 입력보다 우선하거나, 반대면 반대로. 
            // 탑다운 4방향 게임 특성상 좌우를 움직일 땐 좌우를 우선하는 것이 자연스러움.
            if (Mathf.Abs(x) >= Mathf.Abs(y)) y = 0;
            else x = 0;
        }

        IsRunningPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        // 정규화
        moveInput = new Vector2(x, y).normalized;

        if (IsMoving)
        {
            lastMoveDir = moveInput; // 이동 중일 때 마지막 방향 갱신
        }

        // 애니메이터에 변수 전달
        if (controller.animModule != null)
        {
            controller.animModule.UpdateMoveAnimation(moveInput, lastMoveDir, IsMoving);
        }
    }

    // 대시 시작 (Controller에서 Space바 누르면 호출됨)
    public void StartDash()
    {
        dashTimer = 0f; // 진행 시간 0부터 시작
        // 제자리에서 눌렀을 땐 마지막 바라본 방향으로, 이동 중일 땐 진행 방향으로 대시
        dashDir = IsMoving ? moveInput : lastMoveDir; 

        if (controller.animModule != null)
        {
            // 대시 시 방향 전환을 바로 잡아주기 위해 한번 갱신
            controller.animModule.UpdateMoveAnimation(dashDir, dashDir, true);
        }

        // 무적 상태 돌입 및 반투명 처리
        if (controller.hitModule != null) controller.hitModule.IsInvincible = true;
        SetSpriteAlpha(dashAlpha);
    }

    // 대시 진행 (Controller의 Update에서 호출됨)
    public void HandleDash()
    {
        dashTimer += Time.deltaTime;
        
        // 대시 체공 시간(Duration)이 모두 끝났을 때 종료
        if (dashTimer >= dashDuration)
        {
            // 대시 종료 시점
            EndDash();
            // 대시가 끝나면 자연스럽게 다음 상태 판단을 위해 Idle로 초기화
            controller.ChangeState(PlayerState.Idle);
        }
    }

    // 대시 강제 종료 및 상태 원복 (피격 스턴 등 예외 상황 방어용)
    public void EndDash()
    {
        if (controller.hitModule != null) controller.hitModule.IsInvincible = false;
        SetSpriteAlpha(1f); // 투명도 원복
    }

    private void SetSpriteAlpha(float alpha)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }
    }

    // 실제 물리 속도 제어 (Controller의 FixedUpdate에서 호출됨)
    public void ApplyMovement()
    {
        if (rb == null) return;

        if (controller.currentState == PlayerState.Dash)
        {
            // 애니메이션 진행률 (0.0 ~ 1.0)
            float normalizedTime = Mathf.Clamp01(dashTimer / dashDuration);
            
            // AnimationCurve를 이용해 현재 프레임의 속도 배율 가져오기
            float curveMultiplier = dashSpeedCurve.Evaluate(normalizedTime);
            
            // 지정된 총 이동 거리(dashDistance)를 달성하기 위한 기본 평균 속도 계산
            float averageSpeed = dashDistance / dashDuration;

            // 곡률이 반영된 실제 프레임 속도 적용
            rb.linearVelocity = dashDir * (averageSpeed * curveMultiplier);
        }
        else if (controller.currentState == PlayerState.Move || controller.currentState == PlayerState.Walk || controller.currentState == PlayerState.Idle)
        {
            // 데이터 객체에서 스피드를 가져와서 적용 (기본 스피드 = 달리기 속도를 기준으로 삼음)
            float currentSpeed = 3f;
            if (controller.myData != null && controller.myData.stats != null)
            {
                currentSpeed = controller.myData.stats.speed;
            }

            // 걷기 상태면 속도를 절반(설정값)으로 감소
            if (controller.currentState == PlayerState.Walk)
            {
                currentSpeed *= walkSpeedMultiplier;
            }

            rb.linearVelocity = moveInput * currentSpeed;
        }
        else
        {
            // 다른 상태(공격, 피격)일 때는 일단 멈춤 
            rb.linearVelocity = Vector2.zero;
        }
    }
}
