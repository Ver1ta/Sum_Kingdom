namespace SumKingdom

open System

module GameEngine =

    let createInitialDeck () =
        [
            for num in 2 .. 13 do
                yield! [Number num; Number num; Number num]
            
            yield! [Number 1; Number 1; Number 1]
            
            yield! [Card.Double; Card.Double; Card.Double]
            yield! [Card.Eraser; Card.Eraser; Card.Eraser]
            yield! [Card.Trade; Card.Trade; Card.Trade]
        ]

    let shuffleDeck (deck: Card list) =
        let rand = Random()
        deck |> List.sortBy (fun _ -> rand.Next())

    let drawCards (deck: Card list) (count: int) =
        let rec draw acc remaining countLeft =
            if countLeft = 0 || List.isEmpty remaining then
                (remaining, acc)
            else
                draw (remaining.Head :: acc) remaining.Tail (countLeft - 1)
        draw [] deck count

    let rec applyCardEffect (playedCard: Card) (myState: PlayerState) (oppState: PlayerState) =
        match playedCard with
        | Number 1 ->
            match myState.LastErasedCard with
            | Some erasedCard ->
                let stateAfterOne = { myState with CurrentScore = myState.CurrentScore + 1; LastErasedCard = None }
                applyCardEffect erasedCard stateAfterOne oppState
            | None ->
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

        | Eraser ->
            let newPlayed = playedCard :: myState.PlayedCards
            match oppState.PlayedCards with
            | [] -> { myState with PlayedCards = newPlayed }, oppState
            | lastCard :: remainingCards ->
                let updatedOppState = 
                    match lastCard with
                    | Number num -> 
                        let newScore = Math.Max(0, oppState.CurrentScore - num)
                        { oppState with PlayedCards = remainingCards; CurrentScore = newScore }
                    | Card.Double -> 
                        let newScore = oppState.CurrentScore / 2
                        { oppState with PlayedCards = remainingCards; CurrentScore = newScore }
                    | _ -> 
                        { oppState with PlayedCards = remainingCards }
                { myState with PlayedCards = newPlayed }, updatedOppState

        | Trade ->
            let temporaryMyPlayed = playedCard :: myState.PlayedCards
            let newMyState = { myState with PlayedCards = oppState.PlayedCards; CurrentScore = oppState.CurrentScore }
            let newOppState = { oppState with PlayedCards = temporaryMyPlayed; CurrentScore = myState.CurrentScore }
            newMyState, newOppState

    let selectAiCard (difficulty: Difficulty) (aiHand: Card list) (aiScore: int) (playerScore: int) (isPlayerLastEraser: bool) =
        if List.isEmpty aiHand then None
        else
            match difficulty with
            | Easy -> 
                Some (aiHand.Head)

            | Normal ->
                let validNumbers = 
                    aiHand 
                    |> List.filter (fun c -> match c with | Number n -> aiScore + n > playerScore | _ -> false)
                
                if not (List.isEmpty validNumbers) then
                    Some (validNumbers |> List.minBy (fun c -> match c with | Number n -> n | _ -> 0))
                else
                    Some (aiHand.Head)

            | Hard ->
                let hasEraser = aiHand |> List.tryFind (fun c -> c = Card.Eraser)
                if hasEraser.IsSome && isPlayerLastEraser then 
                    hasEraser
                else
                    let hasOne = aiHand |> List.tryFind (fun c -> c = Number 1)
                    if hasOne.IsSome && isPlayerLastEraser then
                        hasOne
                    else
                        let validNumbers = 
                            aiHand 
                            |> List.filter (fun c -> match c with | Number n -> aiScore + n > playerScore | _ -> false)
                        
                        if not (List.isEmpty validNumbers) then
                            Some (validNumbers |> List.minBy (fun c -> match c with | Number n -> aiScore + n - playerScore | _ -> 0))
                        else
                            let hasDouble = aiHand |> List.tryFind (fun c -> c = Card.Double)
                            if hasDouble.IsSome && aiScore * 2 > playerScore then hasDouble
                            else Some (aiHand.Head)

    let initializeGame (difficulty: Difficulty) playerHandCount compHandCount =
        let rawDeck = createInitialDeck ()
        let shuffled = shuffleDeck rawDeck
        
        let deckAfterPlayer, playerHand = drawCards shuffled playerHandCount
        let deckAfterComp, compHand = drawCards deckAfterPlayer compHandCount
        
        let rec drawValidInitialCard currentDeck =
            let nextDeck, drawnList = drawCards currentDeck 1
            let drawnCard = drawnList.Head
            match drawnCard with
            | Number num -> (nextDeck, drawnCard, num)
            | _ -> drawValidInitialCard nextDeck
            
        let deckAfterP, pFirstCard, pInitialScore = drawValidInitialCard deckAfterComp
        let deckAfterC, cFirstCard, cInitialScore = drawValidInitialCard deckAfterP
        
        let playerStarts = pInitialScore <= cInitialScore
        
        {
            Player = { Name = "PLAYER"; Hand = playerHand; PlayedCards = [pFirstCard]; CurrentScore = pInitialScore; GamePoints = 0; LastErasedCard = None }
            Computer = { Name = "COMPUTER"; Hand = compHand; PlayedCards = [cFirstCard]; CurrentScore = cInitialScore; GamePoints = 0; LastErasedCard = None }
            Deck = deckAfterC
            IsPlayerTurn = playerStarts
            Difficulty = difficulty
        }