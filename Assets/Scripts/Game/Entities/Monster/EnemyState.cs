// 적 상태 제어
public enum EnemyState 
{ 
    Idle,    // 정지
    Move,    // 이동
    Dash,    // 회피 사용 중
    Trigger, // 감지
    Attack,  // 공격 중
    Hit,     // 피격 당함
    Stun,    // 기절
    Dead,    // 죽음
    Loading  // 게임 로딩 중
}
