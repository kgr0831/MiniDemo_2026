using UnityEngine;

// 플레이어 공격 메커니즘 제어 및 최종 연산 데미지 반환
public class PlayerAttack : MonoBehaviour 
{ 
    public int baseDamage = 10;
    
    private PlayerController controller;
    public int comboStep = 0; // 현재 공격 콤보 단계 (0, 1, 2)
    public float comboWindowDuration = 1.0f; // 1타 이후 2타를 입력할 수 있는 대기 시간 (1초)
    private float comboWindowTimer = 0f;
    private bool isWaitingForCombo = false; // 현재 2타 입력을 기다리는 중인지 여부
    
    private float attackTimer = 0f;
    public float attackDuration = 0.4f; // 기본값 (애니메이션 길이에 맞춰 자동 동기화됨)
    private bool isAnimationSyncing = false;
    
    public float attackStaminaCost = 10f; // 공격 1타당 기력 소모량

    [Header("방향별 히트박스 (1타)")]
    [SerializeField] private AttackHitbox hitboxDown1;
    [SerializeField] private AttackHitbox hitboxUp1;
    [SerializeField] private AttackHitbox hitboxLeft1;
    [SerializeField] private AttackHitbox hitboxRight1;

    [Header("방향별 히트박스 (2타)")]
    [SerializeField] private AttackHitbox hitboxDown2;
    [SerializeField] private AttackHitbox hitboxUp2;
    [SerializeField] private AttackHitbox hitboxLeft2;
    [SerializeField] private AttackHitbox hitboxRight2;

    // 현재 활성화된 히트박스 (끌 때 참조용)
    private AttackHitbox activeHitbox;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
    }

    public void StartAttack()
    {
        if (comboStep == 0)
        {
            // 첫 번째 공격 시작
            comboStep = 1;
            ExecuteAttackStep();
        }
        else if (isWaitingForCombo && comboStep == 1) 
        {
            // 1타 모션이 끝나고 대기 시간(1초) 안에 다시 좌클릭을 한 경우 -> 2타 즉시 발동
            isWaitingForCombo = false;
            comboStep = 2;
            
            // 다시 공격 상태로 강제 진입
            controller.ChangeState(PlayerState.Attack);
            ExecuteAttackStep();
        }
    }

    private void ExecuteAttackStep()
    {
        attackTimer = 0f;
        comboWindowTimer = 0f;
        isAnimationSyncing = true;
        
        // 스태미나 소모
        if (controller.myData != null && controller.myData.stats != null)
        {
            controller.myData.stats.stamina -= attackStaminaCost;
            if (controller.myData.stats.stamina < 0) controller.myData.stats.stamina = 0;
        }

        // ★ 히트박스는 바로 켜지 않음! Animation Event에서 켬!
        // 애니메이션 트리거 (1타 혹은 2타)
        if (controller.animModule != null)
        {
            controller.animModule.PlayAttackAnim(comboStep);
        }
    }

    /// <summary>
    /// Animation Event에서 호출: 공격 모션 중 칼이 휘둘려지는 프레임에서 히트박스 ON
    /// </summary>
    public void OnAttackHitboxEnable()
    {
        // 이전에 켜져있던 히트박스가 있으면 먼저 끔
        if (activeHitbox != null)
        {
            activeHitbox.DisableHitbox();
            activeHitbox = null;
        }

        // 현재 방향과 콤보 단계에 해당하는 히트박스를 찾아서 활성화
        activeHitbox = GetHitboxForCurrentDirection(comboStep);
        if (activeHitbox != null)
        {
            activeHitbox.EnableHitbox();
        }
    }

    /// <summary>
    /// Animation Event에서 호출: 공격 모션의 타격 구간이 끝나면 히트박스 OFF
    /// </summary>
    public void OnAttackHitboxDisable()
    {
        if (activeHitbox != null)
        {
            activeHitbox.DisableHitbox();
            activeHitbox = null;
        }
    }

    /// <summary>
    /// 현재 바라보는 방향과 콤보 단계에 맞는 히트박스를 반환
    /// </summary>
    private AttackHitbox GetHitboxForCurrentDirection(int step)
    {
        Vector2 dir = controller.moveModule != null ? controller.moveModule.lastMoveDir : Vector2.down;

        // 4방향 중 가장 가까운 방향 판별
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            // 좌우
            if (dir.x > 0)
                return step == 1 ? hitboxRight1 : hitboxRight2;
            else
                return step == 1 ? hitboxLeft1 : hitboxLeft2;
        }
        else
        {
            // 상하
            if (dir.y > 0)
                return step == 1 ? hitboxUp1 : hitboxUp2;
            else
                return step == 1 ? hitboxDown1 : hitboxDown2;
        }
    }

    /// <summary>
    /// 데미지 계산 (AttackHitbox에서 호출됨)
    /// 향후 스탯, 장비, 버프 등을 반영하여 확장할 수 있는 지점
    /// </summary>
    public int CalculateDamage()
    {
        // TODO: 스탯, 장비 기반 데미지 계산으로 확장
        return baseDamage;
    }

    /// <summary>
    /// 채집 데미지 계산 (자원 오브젝트용, gatherPower 스탯 기반)
    /// </summary>
    public int CalculateGatherDamage()
    {
        if (controller.myData != null && controller.myData.stats != null)
        {
            int gp = controller.myData.stats.gatherPower;
            return gp > 0 ? gp : 5; // gatherPower가 0이면 기본값 5
        }
        return 5;
    }

    public void HandleAttack()
    {
        attackTimer += Time.deltaTime;
        
        // 애니메이션 클립 길이 동기화
        if (isAnimationSyncing && controller.animModule != null)
        {
            Animator anim = controller.animModule.anim;
            if (anim != null)
            {
                AnimatorStateInfo stateInfo = anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0) : anim.GetCurrentAnimatorStateInfo(0);
                
                if (stateInfo.IsTag("Attack") || stateInfo.IsName("Attack1") || stateInfo.IsName("Attack2") || comboStep > 0)
                {
                    float length = stateInfo.length;
                    
                    if (length > 0.05f) 
                    {
                        float speedStat = 1.0f;
                        if (controller.myData != null && controller.myData.stats != null)
                        {
                            speedStat = controller.myData.stats.attackSpeed;
                            if (speedStat <= 0f) speedStat = 1.0f;
                        }

                        attackDuration = length / speedStat;
                        isAnimationSyncing = false;
                    }
                }
            }
        }
        
        if (attackTimer >= attackDuration)
        {
            // 공격 모션 종료 시 히트박스도 반드시 끔 (안전장치)
            OnAttackHitboxDisable();

            if (comboStep == 1)
            {
                isWaitingForCombo = true;
                comboWindowTimer = 0f;
                controller.ChangeState(PlayerState.Idle);
            }
            else
            {
                ResetCombo();
                controller.ChangeState(PlayerState.Idle);
            }
        }
    }

    void Update()
    {
        if (isWaitingForCombo)
        {
            comboWindowTimer += Time.deltaTime;
            if (comboWindowTimer >= comboWindowDuration)
            {
                ResetCombo();
            }
        }
    }

    public void ResetCombo()
    {
        comboStep = 0;
        isWaitingForCombo = false;
        attackTimer = 0f;
        comboWindowTimer = 0f;
        
        // 콤보 초기화 시 히트박스도 끔
        OnAttackHitboxDisable();
    }
}
