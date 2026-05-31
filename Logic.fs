namespace SumKingdom

open System

module GameEngine =

    // 1. 기획서 규칙에 맞게 전체 51장의 카드 덱을 생성하는 함수 [cite: 42, 45]
    let createInitialDeck () =
        [
            // 숫자 카드: 2부터 13까지 각 3장씩 생성 [cite: 45]
            for num in 2 .. 13 do
                yield! [Number num; Number num; Number num]
            
            // 특수 카드: 각 3장씩 생성 [cite: 46, 47, 49, 51, 53]
            yield! [Revive; Revive; Revive]
            yield! [Double; Double; Double]
            yield! [Halve; Halve; Halve]
            yield! [Eraser; Eraser; Eraser]
            yield! [Trade; Trade; Trade]
        ]

    // 2. 덱을 무작위로 섞는 함수 [cite: 5]
    let shuffleDeck (deck: Card list) =
        let rand = Random()
        deck |> List.sortBy (fun _ -> rand.Next())

    // 3. 덱에서 카드를 뽑는 함수
    let drawCards (deck: Card list) (count: int) =
        let rec draw acc remaining countLeft =
            if countLeft = 0 || List.isEmpty remaining then
                (remaining, acc)
            else
                draw (remaining.Head :: acc) remaining.Tail (countLeft - 1)
        draw [] deck count

    // 4. 규칙 엔진: 카드를 냈을 때 점수 계산 및 효과 적용
    let rec applyCardEffect (playedCard: Card) (myState: PlayerState) (oppState: PlayerState) =
        match playedCard with
        | Number 1 ->
            // 💡 [핵심 규칙] 1번 카드를 냈을 때의 부활 조건 체크![cite: 1]
            match myState.LastErasedCard with
            | Some erasedCard ->
                // [부활 발동!] 지워진 카드가 있다면 1점을 더하고, 그 카드의 효과를 다시 적용함[cite: 1]
                let stateAfterOne = { myState with CurrentScore = myState.CurrentScore + 1; LastErasedCard = None }
                // 재귀 호출(rec)을 이용해 부활한 카드의 효과를 깨끗하게 다시 한 번 입힘
                applyCardEffect erasedCard stateAfterOne oppState
            
            | None ->
                // 평소에는 그냥 일반 숫자 1처럼 작동함[cite: 1]
                let newPlayed = playedCard :: myState.PlayedCards
                let newScore = myState.CurrentScore + 1
                { myState with PlayedCards = newPlayed; CurrentScore = newScore }, oppState

        | Number num ->
            let newPlayed = playedCard :: myState.PlayedCards
            let newScore = myState.CurrentScore + num
            { myState with PlayedCards = newPlayed; CurrentScore = newScore }, oppState

        | Double ->
            let newPlayed = playedCard :: myState.PlayedCards
            let newScore = myState.CurrentScore * 2
            { myState with PlayedCards = newPlayed; CurrentScore = newScore }, oppState

        | Halve ->
            let newPlayed = playedCard :: myState.PlayedCards
            let newOppScore = oppState.CurrentScore / 2
            { myState with PlayedCards = newPlayed }, { oppState with CurrentScore = newOppScore }

        | Eraser ->
            let newPlayed = playedCard :: myState.PlayedCards
            match oppState.PlayedCards with
            | [] -> { myState with PlayedCards = newPlayed }, oppState
            | lastCard :: remainingCards ->
                // 상대방의 최근 카드를 빼앗아와서 상대방의 'LastErasedCard' 공간에 박아버림![cite: 1]
                let scoreDeduction = 
                    match lastCard with
                    | Number num -> num
                    | _ -> 0 // 특수 카드가 지워졌을 때의 점수 복구는 0 처리
                
                let newOppScore = Math.Max(0, oppState.CurrentScore - scoreDeduction)
                
                { myState with PlayedCards = newPlayed }, 
                { oppState with 
                    PlayedCards = remainingCards
                    CurrentScore = newOppScore
                    LastErasedCard = Some lastCard // 💡 상대방에게 "너 이거 지워짐" 하고 기록을 남김[cite: 1]
                }

        | Trade ->
            let temporaryMyPlayed = playedCard :: myState.PlayedCards
            let newMyState = { myState with PlayedCards = oppState.PlayedCards; CurrentScore = oppState.CurrentScore }
            let newOppState = { oppState with PlayedCards = temporaryMyPlayed; CurrentScore = myState.CurrentScore }
            newMyState, newOppState

    // 5. 게임 초기 상태 설정 함수 (LastErasedCard는 처음에 None으로 세팅)
    let initializeGame playerHandCount compHandCount =
        let rawDeck = createInitialDeck ()
        let shuffled = shuffleDeck rawDeck
        
        let deckAfterPlayer, playerHand = drawCards shuffled playerHandCount
        let deckAfterComp, compHand = drawCards deckAfterPlayer compHandCount
        
        {
            Player = { Name = "PLAYER"; Hand = playerHand; PlayedCards = []; CurrentScore = 0; GamePoints = 0; LastErasedCard = None }
            Computer = { Name = "COMPUTER"; Hand = compHand; PlayedCards = []; CurrentScore = 0; GamePoints = 0; LastErasedCard = None }
            Deck = deckAfterComp
            IsPlayerTurn = true
        }