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
    [HideInInspector] public PlayerInteract interactModule; // 플레이어의 상호작용
    [HideInInspector] public PlayerHit hitModule; // 플레이어 피격 / 스턴 제어

    void Awake()
    {
        moveModule = GetComponent<PlayerMove>();
        animModule = GetComponent<PlayerAnim>();
        attackModule = GetComponent<PlayerAttack>();
        interactModule = GetComponent<PlayerInteract>();
        hitModule = GetComponent<PlayerHit>();
    }

    void Start()
    {
        // 테스트용 기본 스탯 할당 (추후 게임매니저 등이 주입하도록 변경 가능)
        if (myData == null) myData = new CharacterData();
        if (myData.stats == null) myData.stats = new StatData();
        
        // 유니티 인스펙터에서 public class가 자동 생성될 때 speed가 0이 되어 움직이지 않는 현상 방지
        if (myData.stats.speed <= 0f) myData.stats.speed = 3f; 
        
        ChangeState(PlayerState.Idle);
    }

    void Update()
    {
        // HFSM (상태 기계) 프레임 업데이트 로직
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
                        // Shift(달리기 키) 입력 여부로 걷기 / 뛰기 구분 처리
                        bool isRunning = moveModule.IsRunningPressed;
                        PlayerState nextMoveState = isRunning ? PlayerState.Move : PlayerState.Walk;
                        
                        if (currentState != nextMoveState)
                            ChangeState(nextMoveState);
                    }
                    else if (!moveModule.IsMoving && currentState != PlayerState.Idle)
                    {
                        ChangeState(PlayerState.Idle);
                    }
                    
                    // 3. 대시(회피) 입력 처리 (예: Space 바)
                    if (Input.GetKeyDown(KeyCode.Space) && currentState != PlayerState.Dash)
                    {
                        moveModule.StartDash();
                        ChangeState(PlayerState.Dash);
                    }
                }
                break;

            case PlayerState.Dash:
                if (moveModule != null)
                {
                    moveModule.HandleDash();
                }
                break;

            // 추가될 패턴들
            case PlayerState.Attack:
            case PlayerState.Hit:
            case PlayerState.Dead:
                break;
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
