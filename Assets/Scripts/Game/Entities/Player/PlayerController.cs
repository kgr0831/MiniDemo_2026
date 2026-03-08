using UnityEngine;

// 플레이어
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    public CharacterData myData; // 플레이어 데이터
    public PlayerState currentState; // 플레이어 상태 제어

    [HideInInspector] public PlayerMove moveModule; // 플레이어 움직임(이동, 회피)
    [HideInInspector] public PlayerAnim animModule; // 플레이어 애니메이션 제어
    [HideInInspector] public PlayerAttack attackModule; // 플레이어 공격 메커니즘 제어
    [HideInInspector] public PlayerTool toolModule; // 도구 메커니즘 제어
    [HideInInspector] public PlayerInteract interactModule; // 플레이어의 상호작용
    [HideInInspector] public PlayerHit hitModule; // 플레이어 피격 / 스턴 제어

    // 스태미나 회복 지연 타이머 (소모 직후 바로 회복되지 않도록)
    private float staminaRegenDelayTimer = 0f;
    private float lastFrameStamina = 100f; // 이전 프레임의 스태미나 기록용

    void Awake()
    {
        moveModule = GetComponent<PlayerMove>();
        animModule = GetComponent<PlayerAnim>();
        attackModule = GetComponent<PlayerAttack>();
        toolModule = GetComponent<PlayerTool>();
        interactModule = GetComponent<PlayerInteract>();
        hitModule = GetComponent<PlayerHit>();
    }

    void Start()
    {
        // 테스트용 기본 스탯 할당 (추후 게임매니저 등이 주입하도록 변경 가능)
        if (myData == null) myData = new CharacterData();
        if (myData.stats == null) myData.stats = new StatData();
        
        // 유니티 인스펙터에서 public class가 자동 생성될 때 변수들이 0이 되는 현상 방지
        if (myData.stats.speed <= 0f) myData.stats.speed = 3f; 
        if (myData.stats.maxStamina <= 0f) myData.stats.maxStamina = 100f;
        myData.stats.stamina = myData.stats.maxStamina; // 게임 시작 시 항상 최대 기력으로 시작
        myData.stats.isExhausted = false; // 탈진 상태 초기화
        if (myData.stats.staminaRecoveryRate <= 0f) myData.stats.staminaRecoveryRate = 10f; // 초당 10 회복
        
        ChangeState(PlayerState.Idle);
    }

    void Update()
    {
        // HFSM 프레임 업데이트 로직
        switch (currentState)
        {
            case PlayerState.Idle:
            case PlayerState.Walk:
            case PlayerState.Move:
                if (moveModule != null)
                {
                    // 1. 입력 감지 및 방향 갱신
                    moveModule.HandleInput();
                    
                    // 2. 이동 상태 전환 (Idle <-> Walk/Move)
                    if (moveModule.IsMoving)
                    {
                        // 달리기 조건: 쉬프트를 눌렀고, 탈진 상태가 아니며, 기력이 존재해야 함
                        bool wantsToRun = moveModule.IsRunningPressed && !myData.stats.isExhausted && myData.stats.stamina > 0f;
                        PlayerState nextMoveState = wantsToRun ? PlayerState.Move : PlayerState.Walk;
                        
                        if (currentState != nextMoveState)
                        {
                            ChangeState(nextMoveState);
                        }
                    }
                    else if (!moveModule.IsMoving && currentState != PlayerState.Idle)
                    {
                        ChangeState(PlayerState.Idle);
                    }
                    
                    // 3. 대시(회피) 입력 처리 (탈진 상태가 아니고 기력이 조금이라도 남아있으면 사용 가능)
                    if (Input.GetKeyDown(KeyCode.Space) && currentState != PlayerState.Dash)
                    {
                        if (!myData.stats.isExhausted && myData.stats.stamina > 0f)
                        {
                            moveModule.StartDash();
                            ChangeState(PlayerState.Dash);
                        }
                    }
                    else if (Input.GetMouseButtonDown(1) && currentState != PlayerState.Tool)
                    {
                        // 우클릭 -> 도구(채집) 사용 (오직 이동/대기중일 때만 가능)
                        if (toolModule != null)
                        {
                            toolModule.StartTool();
                            ChangeState(PlayerState.Tool);
                        }
                    }
                }
                break;

            case PlayerState.Dash:
                if (moveModule != null)
                {
                    moveModule.HandleDash();
                }
                break;

            case PlayerState.Attack:
                if (attackModule != null) 
                {
                    attackModule.HandleAttack();
                }
                break;

            case PlayerState.Tool:
                if (toolModule != null) toolModule.HandleTool();
                break;

            case PlayerState.Hit:
            case PlayerState.Dead:
                break;
        }

        // 콤보 공격을 위해, Idle/Walk 뿐만 아니라 현재 Attack 상태일 때도 좌클릭(Attack) 입력을 감지해야 함.
        if (Input.GetMouseButtonDown(0))
        {
            // 기력이 조금이라도 남아있고, 탈진 상태가 아닌지 검사
            if (myData != null && myData.stats != null && !myData.stats.isExhausted && myData.stats.stamina > 0f)
            {
                if (currentState == PlayerState.Idle || currentState == PlayerState.Walk || currentState == PlayerState.Move)
                {
                    // 평상시 공격 시작
                    if (attackModule != null)
                    {
                        ChangeState(PlayerState.Attack);
                        attackModule.StartAttack();
                    }
                }
                else if (currentState == PlayerState.Attack)
                {
                    // 이미 공격 중일 때 또 클릭하면 (콤보 예약)
                    if (attackModule != null)
                    {
                        attackModule.StartAttack(); // 내부에서 예약 처리
                    }
                }
            }
        }
        
        // 기력(스태미나) 고갈 및 자연 회복 처리
        if (myData != null && myData.stats != null)
        {
            // 달리기 중 초당 스태미나 소모 (PlayerMove.FixedUpdate에서 넘어옴)
            if (currentState == PlayerState.Move && moveModule != null)
            {
                myData.stats.stamina -= moveModule.runStaminaCostPerSec * Time.deltaTime;
                if (myData.stats.stamina <= 0f)
                {
                    myData.stats.stamina = 0f;
                    ChangeState(PlayerState.Walk); // 기력이 다 닳으면 강제로 걷기 전환
                }
            }

            // 이번 프레임에 스태미나가 소모되었는지 체크 (공격, 대시 등 프레임 내 순간 소모 모두 감지)
            if (myData.stats.stamina < lastFrameStamina)
            {
                staminaRegenDelayTimer = 0.5f; // 소모 직후 0.5초 동안은 회복 안 됨
            }
            else if (staminaRegenDelayTimer > 0f)
            {
                staminaRegenDelayTimer -= Time.deltaTime; // 지연 시간 타이머 감소
            }

            // 기력이 0 이하가 되면 탈진 상태 돌입 (한 번 돌입하면 100% 찰 때까지 유지)
            if (myData.stats.stamina <= 0f && !myData.stats.isExhausted)
            {
                myData.stats.stamina = 0f;
                myData.stats.isExhausted = true;
            }

            // 딜레이 타이머가 끝났을 때만 자연 회복 실행
            if (staminaRegenDelayTimer <= 0f)
            {
                if (myData.stats.stamina < myData.stats.maxStamina)
                {
                    // 탈진 상태일 때는 평소보다 1.3배 빠르게 회복
                    float multiplier = myData.stats.isExhausted ? 1.3f : 1.0f;
                    myData.stats.stamina += myData.stats.staminaRecoveryRate * multiplier * Time.deltaTime;
                    
                    // 최대치 도달 시 탈진 상태 해제
                    if (myData.stats.stamina >= myData.stats.maxStamina)
                    {
                        myData.stats.stamina = myData.stats.maxStamina;
                        myData.stats.isExhausted = false; // 완전히 다 차면 탈진 극복!
                    }
                }
            }
            
            // 다음 프레임 비교를 위해 이번 프레임의 최종 스태미나 저장
            lastFrameStamina = myData.stats.stamina;
        }
    }

    void FixedUpdate()
    {
        // 실제 물리적인 이동 처리는 FixedUpdate에서 실행 (리지드바디 충돌 방지)
        if (moveModule != null)
        {
            moveModule.ApplyMovement();
        }
    }

    // 플레이어 상태를 변경하고 애니메이션을 동기화하는 핵심 함수
    public void ChangeState(PlayerState newState)
    {
        if (currentState == newState) return; // 동일 상태전환 방지
        
        // 상태를 빠져나갈 때(Exit) 처리할 로직
        if (currentState == PlayerState.Dash && moveModule != null)
        {
            // 대시 도중 피격/스턴 등으로 강제 상태 변경 시 투명도/무적 원복을 보장
            moveModule.EndDash(); 
        }

        currentState = newState;

        if (animModule != null)
        {
            animModule.PlayStateAnim(newState);
        }
    }
}
