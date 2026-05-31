namespace SumKingdom

// 1. 카드 종류를 표현하는 판별 공용체
type Card =
    | Number of int         // 1~13 카드 (1번 카드는 평소엔 숫자 1 역할)
    | Double                // 내 점수 2배
    | Halve                 // 상대 점수 절반[cite: 1]
    | Eraser                // 상대의 직전 카드 제거 및 무효화[cite: 1]
    | Trade                 // 서로의 카드 묶음과 점수를 교환[cite: 1]

// 2. 플레이어의 상태를 관리하는 레코드 타입
type PlayerState = {
    Name : string
    Hand : Card list        
    PlayedCards : Card list 
    CurrentScore : int     
    GamePoints : int       
    // 💡 [핵심 보완] 상대의 Eraser에 의해 마지막으로 지워진 카드를 기억함 (없으면 None)[cite: 1]
    LastErasedCard : Card option 
}

// 3. 전체 게임판의 상태
type GameState = {
    Player : PlayerState
    Computer : PlayerState
    Deck : Card list        
    IsPlayerTurn : bool
}