using UnityEngine;

// 플레이어 공격 메커니즘 제어 및 최종 연산 데미지 반환
public class PlayerAttack : MonoBehaviour 
{ 
    public float attackRange = 0.8f; // 공격 거리 (플레이어 중심에서 떨어진 거리)
    public float attackRadius = 0.6f; // 공격 판정 원의 반지름
    public int baseDamage = 10;
    
    private PlayerController controller;
    public int comboStep = 0; // 현재 공격 콤보 단계 (0, 1, 2)
    public float comboWindowDuration = 1.0f; // 1타 이후 2타를 입력할 수 있는 대기 시간 (1초)
    private float comboWindowTimer = 0f;
    private bool isWaitingForCombo = false; // 현재 2타 입력을 기다리는 중인지 여부
    
    private float attackTimer = 0f;
    public float attackDuration = 0.4f; // 기본값 (애니메이션 길이에 맞춰 자동 동기화됨)
    private bool isAnimationSyncing = false; // 애니메이션 타임값 동기화가 필요한 타격 플래그
    
    public float attackStaminaCost = 10f; // 공격 1타당 기력 소모량

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
            
            // 다시 공격 상태로 강제 진입 (만약 그 사이 Idle/Walk 였다 하더라도)
            controller.ChangeState(PlayerState.Attack);
            ExecuteAttackStep();
        }
    }

    private void ExecuteAttackStep()
    {
        attackTimer = 0f;
        comboWindowTimer = 0f;
        isAnimationSyncing = true; // 트랜지션 직후 애니메이터의 클립 길이를 가져오기 위해 플래그 설정
        
        // 스태미나 소모 (스탯 데이터가 존재할 경우)
        if (controller.myData != null && controller.myData.stats != null)
        {
            controller.myData.stats.stamina -= attackStaminaCost;
            if (controller.myData.stats.stamina < 0) controller.myData.stats.stamina = 0;
        }

        // 데미지 판정 발생
        PerformAttackHitbox();
        
        // 애니메이션 트리거 (1타 혹은 2타)
        if (controller.animModule != null)
        {
            controller.animModule.PlayAttackAnim(comboStep);
        }
    }

    public void HandleAttack()
    {
        attackTimer += Time.deltaTime;
        
        // 애니메이션이 실제로 재생되기 시작하는 프레임(트랜지션이 끝나는 지점 등)일 때 클립의 길이를 가져와 동기화
        if (isAnimationSyncing && controller.animModule != null)
        {
            Animator anim = controller.animModule.anim;
            if (anim != null)
            {
                // 다음 상태로 트랜지션 중이라면 다음 상태의 길이를 우선적으로 가져옴
                AnimatorStateInfo stateInfo = anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0) : anim.GetCurrentAnimatorStateInfo(0);
                
                // 트랜지션이 시작되었거나 이미 해당 상태에 진입했을 때만 동기화
                if (stateInfo.IsTag("Attack") || stateInfo.IsName("Attack1") || stateInfo.IsName("Attack2") || comboStep > 0)
                {
                    float length = stateInfo.length;
                    
                    // 클립 길이가 유효한 경우에만 적용 (방어 코드)
                    if (length > 0.05f) 
                    {
                        // 스탯의 오리지널 공격 속도를 참조하여 모션 길이(Duration)도 동일한 비율로 단축
                        float speedStat = 1.0f;
                        if (controller.myData != null && controller.myData.stats != null)
                        {
                            speedStat = controller.myData.stats.attackSpeed;
                            if (speedStat <= 0f) speedStat = 1.0f;
                        }

                        // 애니메이션 길이를 배속으로 나눈 값이 실제 캐릭터의 통제 불능(선딜/후딜) 시간!
                        attackDuration = length / speedStat;
                        isAnimationSyncing = false; // 동기화 완료
                        
                        Debug.Log($"Synchronized Attack{comboStep} Duration: {attackDuration} sec (Base {length} / Speed {speedStat})");
                    }
                }
            }
        }
        
        // 캐릭터가 움직이지 못하는 순수 '공격 모션 기간'이 끝났을 때
        // (attackDuration이 애니메이션 길이에 맞춰 유동적으로 변함)
        if (attackTimer >= attackDuration)
        {
            if (comboStep == 1)
            {
                // 1타가 끝났으면, 즉시 콤보 대기 모드(isWaitingForCombo)로 돌입하고 캐릭터의 이동 제한을 풀어줌
                isWaitingForCombo = true;
                comboWindowTimer = 0f; // 1초 카운트 시작
                controller.ChangeState(PlayerState.Idle);
            }
            else
            {
                // 2타까지 완전히 끝났을 경우엔 콤보 초기화
                ResetCombo();
                controller.ChangeState(PlayerState.Idle);
            }
        }
    }

    // 콤보 유예 시간을 체크하는 용도 (플레이어가 다시 걷거나 대기 중일 때도 측정되어야 함)
    // Update 컴포넌트 사이클을 활용
    void Update()
    {
        if (isWaitingForCombo)
        {
            comboWindowTimer += Time.deltaTime;
            // 1초가 넘어가면 콤보 기회 박탈
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
    }

    private void PerformAttackHitbox()
    {
        Vector2 attackPos = (Vector2)transform.position + (controller.moveModule.lastMoveDir * attackRange);
        
        // 해당 구역 내의 모든 콜라이더 검출
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(attackPos, attackRadius);
        
        foreach (Collider2D hit in hitColliders)
        {
            // 본인(플레이어)은 제외
            if (hit.gameObject == gameObject) continue;

            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                // TODO: 스탯, 장비 기반 데미지 계산으로 확장될 곳
                damageable.TakeDamage(baseDamage);
                Debug.Log($"Attacked {hit.name} for {baseDamage} damage!");
            }
        }
    }

    // 디버그용 기즈모 (에디터 씬 뷰에서 공격 범위 시각화)
    private void OnDrawGizmosSelected()
    {
        PlayerMove move = GetComponent<PlayerMove>();
        Vector2 dir = move != null ? move.lastMoveDir : Vector2.down;
        Vector2 attackPos = (Vector2)transform.position + (dir * attackRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPos, attackRadius);
    }
}
