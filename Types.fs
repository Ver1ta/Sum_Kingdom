namespace SumKingdom

// 난이도를 정의하는 판별 공용체 추가
type Difficulty =
    | Easy
    | Normal
    | Hard

type Card =
    | Number of int         // 1~13 카드 [cite: 177]
    | Double                // 내 점수 2배 [cite: 178]
    | Eraser                // 상대의 직전 카드 제거 및 무효화 [cite: 179]
    | Trade                 // 서로의 카드 묶음과 점수를 교환 [cite: 180]

type PlayerState = {
    Name : string
    Hand : Card list        
    PlayedCards : Card list 
    CurrentScore : int     
    GamePoints : int       
    LastErasedCard : Card option
}

type GameState = {
    Player : PlayerState
    Computer : PlayerState
    Deck : Card list        
    IsPlayerTurn : bool
    Difficulty : Difficulty // 💡 난이도 상태 추가
}