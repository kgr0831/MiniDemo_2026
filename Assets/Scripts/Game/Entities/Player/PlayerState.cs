// 플레이어 상태 제어
public enum PlayerState 
{ 
    Idle,     // 정지
    Walk,     // 걷기
    Move,     // 뛰기 (Run)
    Dash,     // 회피 사용 중
    Hit,      // 피격 당함
    Attack,   // 공격 중
    Stun,     // 기절
    Tool,     // 도구 사용
    Interact, // 상호작용
    Dead,     // 죽음
    Loading   // 게임 로딩 중
}
