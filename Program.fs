open System
open System.Drawing
open System.Windows.Forms
open System.Collections.Generic
open SumKingdom

let cardToString (card: Card) =
    match card with
    | Number num  -> sprintf "[ 🔢 %2d ]" num
    | Card.Double -> "[ 👑 Double ]"
    | Card.Eraser -> "[ ❌ Eraser ]"
    | Card.Trade  -> "[ 🔄 Trade ]"

let evaluateRoundEnd (state: SumKingdom.GameState) =
    let updatedPlayerPoints, updatedCompPoints =
        if state.Player.CurrentScore > state.Computer.CurrentScore then
            state.Player.GamePoints + 1, state.Computer.GamePoints
        elif state.Player.CurrentScore < state.Computer.CurrentScore then
            state.Player.GamePoints, state.Computer.GamePoints + 1
        else
            state.Player.GamePoints + 1, state.Computer.GamePoints + 1

    let rec drawValidInitialCard currentDeck =
        let nextDeck, drawnList = GameEngine.drawCards currentDeck 1
        if List.isEmpty drawnList then 
            (nextDeck, Number 1, 1)
        else
            let drawnCard = drawnList.Head
            match drawnCard with
            | Number num -> (nextDeck, drawnCard, num)
            | _ -> drawValidInitialCard nextDeck

    let rec drawInitialPair currentDeck =
        let deckAfterP, pNextFirstCard, pNextScore = drawValidInitialCard currentDeck
        let deckAfterC, cNextFirstCard, cNextScore = drawValidInitialCard deckAfterP
        
        if pNextScore = cNextScore then
            drawInitialPair deckAfterC
        else
            (deckAfterC, pNextFirstCard, pNextScore, cNextFirstCard, cNextScore)

    let finalDeck, pCard, pScore, cCard, cScore = drawInitialPair state.Deck
    let nextPlayerStarts = pScore <= cScore

    { state with 
        Player = { state.Player with GamePoints = updatedPlayerPoints; CurrentScore = pScore; PlayedCards = [pCard]; LastErasedCard = None }
        Computer = { state.Computer with GamePoints = updatedCompPoints; CurrentScore = cScore; PlayedCards = [cCard]; LastErasedCard = None }
        Deck = finalDeck
        IsPlayerTurn = nextPlayerStarts 
    }

type GameForm() as this =
    inherit Form()

    let mutable selectedDifficulty = SumKingdom.Difficulty.Normal

    let initNewGame (diff: SumKingdom.Difficulty) () = 
        let rec getValidInitialState () =
            let initialState = GameEngine.initializeGame diff 9 9
            if initialState.Player.CurrentScore = initialState.Computer.CurrentScore then
                getValidInitialState()
            else
                initialState
        getValidInitialState()

    let mutable currentState : SumKingdom.GameState = GameEngine.initializeGame SumKingdom.Difficulty.Normal 9 9
    let mutable centerStatusMessage = "Welcome!\nYour Turn."
    let mutable isRoundEndWaiting = false
    let mutable pendingNextState : SumKingdom.GameState option = None

    let mutable lastCardPlayedString = "None"
    let mutable isGameFinalClosingMode = false 

    let playerWonPiles = new List<Card list>()
    let computerWonPiles = new List<Card list>()

    let titlePanel = new Panel()
    let gamePanel = new Panel()
    let rulePanel = new Panel()
    let gameOverPanel = new Panel()

    let btnStart = new Button()
    let btnRules = new Button()
    let btnNextRound = new Button()
    let aiTimer = new Timer()
    
    let lblStatsResult = new Label()
    let lblStatsScore = new Label()
    let btnStatsRestart = new Button()

    let grpDifficulty = new GroupBox()
    let rdoEasy = new RadioButton()
    let rdoNormal = new RadioButton()
    let rdoHard = new RadioButton()

    let ruleTextKorean = 
        "=========================================================================\r\n" +
        "  ⚔️ 숨 킹덤 (Sum Kingdom) : 2인 전술 대결 공식 가이드 매뉴얼 ⚔️\r\n" +
        "=========================================================================\r\n\r\n" +
        "【1】 게임 준비 (Setup)\r\n" +
        "  ● 플레이어와 컴퓨터(AI)는 9장의 카드를 무작위로 지급받아 손패(Hand)로 쥡니다.\r\n" +
        "  ● 라운드 시작 시, 양 진영의 필드 에어리어에는 공통 덱에서 추출한 무작위 숫자 카드 1장이\r\n" +
        "    '초기 바닥 카드'로 배치되며, 이 카드의 숫자가 해당 라운드의 시작 점수가 됩니다.\r\n\r\n" +
        "【2】 턴 진행 및 행동 규칙 (Action Rule)\r\n" +
        "  ● 카드를 내려놓기 위해서는 다음의 절대적인 조건을 만족해야 합니다.\r\n" +
        "    자신의 필드 총점이 상대방의 총점보다 '엄격하게 낮을 때만' 손패에서 카드를 낼 수 있습니다.\r\n" +
        "  ● 점수가 서로 같거나 내가 더 높다면 카드를 제출할 수 없습니다.\r\n" +
        "  ● 패를 내어 자신의 총점이 상대방을 추월(초과)하는 순간 즉시 나의 턴이 종료되며,\r\n" +
        "    행동 권한이 자동으로 상대방에게 넘어갑니다.\r\n\r\n" +
        "【3】 스택 연출 및 카드 효과 (Stack & Card Effects)\r\n" +
        "  ● 필드 스택 연출: 필드에는 오직 자신이 가장 최근에 제출한 '맨 위의 카드 1장'만 노출됩니다.\r\n" +
        "  ● 일반 숫자 카드 (2 ~ 13) : 스택에 더해지며 총점에 해당 수치만큼 즉시 누적됩니다.\r\n" +
        "  ● [ 👑 Double ] : 시전 즉시 자신의 현재 필드 총점을 2배로 증폭시킵니다.\r\n" +
        "  ● [ ❌ Eraser ] : 상대방 필드 스택의 가장 위에 놓인 카드 1장을 파괴하여 증발시키고 직전 카드를 노출합니다.\r\n" +
        "  ● [ 🔄 Trade  ] : 사용하는 즉시 자신과 상대방이 쥐고 있는 손패 전체를 통째로 교환합니다.\r\n" +
        "  ● [ 🔢 숫자 1 ] : 평소에는 1점짜리 카드입니다. 하지만 상대가 Eraser를 사용하여 내 카드를 파괴한\r\n" +
        "                    '바로 다음 차례'에 연계하여 제출하면 [💖 부활(Revive)] 콤보가 발동하여 복구시킵니다.\r\n\r\n" +
        "【4】 라운드 결산 및 승패 (Round Settlement)\r\n" +
        "  ● 추월 실패: 내 차례에 카드를 제출했음에도 점수가 상대방보다 낮다면,\r\n" +
        "               추월에 실패한 것으로 간주되어 해당 라운드는 즉시 패배로 종료됩니다.\r\n" +
        "  ● 승리 및 무승부: 라운드 결산 시 점수가 더 높은 진영이 매치 포인트 1점을 획득합니다.\r\n" +
        "                    만약 완벽하게 동점으로 종료되면 양 진영 모두 매치 포인트 1점씩을 얻습니다.\r\n\r\n" +
        "【5】 대결 종료 조건 (Game End)\r\n" +
        "  ● 자신의 차례(상대보다 점수가 낮은 상황)가 도래했으나, 손패가 0장이라서\r\n" +
        "    더 이상 카드를 낼 수 없는 진영이 발생하는 즉시 전체 게임 매치가 전격 종료됩니다.\r\n" +
        "  ● 최종 결산 화면에서 누적 매치 포인트가 더 많은 플레이어가 위대한 승리자가 됩니다."

    let ruleTextEnglish = 
        "=========================================================================\r\n" +
        "  ⚔️ SUM KINGDOM : OFFICIAL TACTICAL DUEL MANUAL ⚔️\r\n" +
        "=========================================================================\r\n\r\n" +
        "【1】 GAME SETUP\r\n" +
        "  ● Both duelists are dealt an equal starting hand of 9 random cards.\r\n" +
        "  ● At the start of each round, 1 random number card from the deck is automatically\r\n" +
        "    placed on each player's Field Stack as the 'Base Card', establishing the initial score.\r\n\r\n" +
        "【2】 TURN SEQUENCE & INITIATIVE (Action Rule)\r\n" +
        "  ● To play a card, you must satisfy one absolute condition:\r\n" +
        "    You can ONLY play a card from your hand when your current Field Score is STRICTLY LESS THAN\r\n" +
        "    your opponent's total score.\r\n" +
        "  ● If the scores are exactly equal or if your score is higher, you are prohibited from making a move.\r\n" +
        "  ● The moment your played card pushes your total score above the opponent's, your initiative ends,\r\n" +
        "    and control shifts automatically to the opponent.\r\n\r\n" +
        "【3】 STACK MECHANICS & CARD EFFECTS\r\n" +
        "  ● Field Stack View: Only the single topmost card played most recently is visible on the field stack.\r\n" +
        "  ● Normal Numbers (2 - 13) : Placed on the stack, adding their exact value to your total score.\r\n" +
        "  ● [ 👑 Double ] : Instantly doubles your current Field Score and sits on top of the field stack.\r\n" +
        "  ● [ ❌ Eraser ] : Targets and vaporizes the topmost visible card on the opponent's field and reveals the card below.\r\n" +
        "  ● [ 🔄 Trade  ] : Instantly swaps your entire hand with your opponent's hand to disrupt their long-term strategy.\r\n" +
        "  ● [ 🔢 Number 1 ] : Acts as a standard 1-point card. However, if played 'EXACTLY on the immediate turn'\r\n" +
        "                      following an opponent's Eraser, it triggers a [💖 Revive] combo, restoring your destroyed card.\r\n\r\n" +
        "【4】 ROUND RESOLUTION (Settlement)\r\n" +
        "  ● Overtake Failure: If you play a card on your turn but your total score is still lower than or equal\r\n" +
        "                      to the opponent's, you fail to overtake, and you instantly LOSE the round.\r\n" +
        "  ● Scoring: The duelist holding the strictly higher score at the resolution wins the round (+1 Match Point).\r\n" +
        "             In case of a perfect tie, a DRAW is declared, rewarding BOTH duelists with +1 Match Point.\r\n\r\n" +
        "【5】 FINAL MATCH END CONDITIONS\r\n" +
        "  ● The game officially terminates the exact moment a player's turn arrives (when their score is lower),\r\n" +
        "    but they have exactly 0 cards left in hand and cannot make a legal move.\r\n" +
        "  ● The duelist who has amassed the highest total of Match Points wins the Duel!"

    // 👑 [수정] 카드 카운트가 비정상적으로 누적되는 중복 버그 해결
    let collectFieldCardsToWinner (stateBeforeReset: SumKingdom.GameState) =
        let filterPlayedCards cards = 
            cards |> List.filter (fun c -> 
                match c with 
                | Number _ | Card.Double -> true 
                | _ -> false
            )
        
        // 초기 라운드 시작 시 기본으로 깔리는 첫 바닥 카드(각 1장씩 총 2장)는 
        // 유저가 직접 '승리하여 따낸 세트' 계산에서 제외시켜 매치 포인트와의 인과관계를 일치시킵니다.
        if stateBeforeReset.Player.CurrentScore > stateBeforeReset.Computer.CurrentScore then
            if stateBeforeReset.Player.PlayedCards.Length > 1 || stateBeforeReset.Computer.PlayedCards.Length > 1 then
                let earned = (filterPlayedCards stateBeforeReset.Player.PlayedCards) @ (filterPlayedCards stateBeforeReset.Computer.PlayedCards)
                playerWonPiles.Add(earned)
        elif stateBeforeReset.Player.CurrentScore < stateBeforeReset.Computer.CurrentScore then
            if stateBeforeReset.Player.PlayedCards.Length > 1 || stateBeforeReset.Computer.PlayedCards.Length > 1 then
                let earned = (filterPlayedCards stateBeforeReset.Player.PlayedCards) @ (filterPlayedCards stateBeforeReset.Computer.PlayedCards)
                computerWonPiles.Add(earned)
        else
            // 무승부(DRAW) 시 각자 자신이 실제로 낸 카드들만 한 세트씩 가져가도록 철저히 검증
            if stateBeforeReset.Player.PlayedCards.Length > 1 then
                playerWonPiles.Add(filterPlayedCards stateBeforeReset.Player.PlayedCards)
            if stateBeforeReset.Computer.PlayedCards.Length > 1 then
                computerWonPiles.Add(filterPlayedCards stateBeforeReset.Computer.PlayedCards)

    let drawCardGraphics (g: Graphics) (card: Card) (x: int) (y: int) (isClickable: bool) =
        let cardRect = Rectangle(x, y, 85, 115)
        let brush = 
            match card with
            | Number _    -> Brushes.White
            | Card.Double -> Brushes.Gold
            | Card.Eraser -> Brushes.LightCoral
            | Card.Trade  -> Brushes.LightCyan

        g.FillRectangle(brush, cardRect)
        g.DrawRectangle(Pens.Black, cardRect)

        let font = new Font("Segoe UI", 13.0f, FontStyle.Bold)
        let text = 
            match card with
            | Number num  -> sprintf "%d" num
            | Card.Double -> "Double"
            | Card.Eraser -> "Eraser"
            | Card.Trade  -> "Trade"
            
        if isClickable then
            use customPen = new Pen(Color.Yellow, 2.5f)
            g.DrawRectangle(customPen, cardRect)

        match card with
        | Number _ -> g.DrawString(text, font, Brushes.Black, float32 (x + 24), float32 (y + 42))
        | Card.Double -> g.DrawString(text, font, Brushes.Black, float32 (x + 5), float32 (y + 42))
        | _ -> g.DrawString(text, font, Brushes.Black, float32 (x + 7), float32 (y + 42))

    let updateToGameEndButtonUI () =
        isGameFinalClosingMode <- true
        btnNextRound.Text <- "GAME END 🏁"
        btnNextRound.BackColor <- Color.Crimson
        btnNextRound.ForeColor <- Color.White
        btnNextRound.Visible <- true

    let forceTriggerGameOver (finalState: SumKingdom.GameState) =
        let winnerTitle, titleColor = 
            if finalState.Player.GamePoints > finalState.Computer.GamePoints then "🏆 VICTORY 🏆", Color.Gold
            elif finalState.Player.GamePoints < finalState.Computer.GamePoints then "💀 DEFEAT 💀", Color.Crimson
            else "🤝 TIE MATCH 🤝", Color.LightGray

        let statsSummary = 
            sprintf "Your Final Match Points :  %d\n" finalState.Player.GamePoints +
            sprintf "AI Final Match Points   :  %d\n\n" finalState.Computer.GamePoints +
            sprintf "Your Won Card Piles     :  %d sets\n" playerWonPiles.Count +
            sprintf "AI Final Won Card Piles :  %d sets" computerWonPiles.Count

        lblStatsResult.Text <- winnerTitle
        lblStatsResult.ForeColor <- titleColor
        lblStatsScore.Text <- statsSummary

        aiTimer.Stop()
        gamePanel.Visible <- false
        gameOverPanel.Visible <- true

    let checkTurnAndHandStatus (state: SumKingdom.GameState) =
        if state.IsPlayerTurn then
            if List.isEmpty state.Player.Hand then
                centerStatusMessage <- "👤 You No Cards! LOSE."
                collectFieldCardsToWinner state
                isRoundEndWaiting <- true
                pendingNextState <- Some (evaluateRoundEnd state)
                updateToGameEndButtonUI()
                gamePanel.Invalidate()
            else
                centerStatusMessage <- "👤 Your Turn!\nGo."
                gamePanel.Invalidate()
        else
            if List.isEmpty state.Computer.Hand then
                centerStatusMessage <- "🤖 AI No Cards! LOSE."
                let invertedWinState = { state with Player = { state.Player with CurrentScore = 999 }; Computer = { state.Computer with CurrentScore = 0 } }
                collectFieldCardsToWinner invertedWinState
                isRoundEndWaiting <- true
                pendingNextState <- Some (evaluateRoundEnd state)
                updateToGameEndButtonUI()
                gamePanel.Invalidate()
            else
                centerStatusMessage <- "🤖 AI Calculating..."
                gamePanel.Invalidate()
                aiTimer.Interval <- 1200
                aiTimer.Start()

    let rec runComputerTurnWithTimer () =
        if not isRoundEndWaiting && not currentState.IsPlayerTurn then
            checkTurnAndHandStatus currentState

    let resetEntireGameToPlay () =
        currentState <- initNewGame selectedDifficulty ()
        playerWonPiles.Clear()
        computerWonPiles.Clear()
        isRoundEndWaiting <- false
        isGameFinalClosingMode <- false
        pendingNextState <- None
        lastCardPlayedString <- "None"
        
        btnNextRound.Text <- "NEXT ROUND ➡️"
        btnNextRound.BackColor <- Color.Gold
        btnNextRound.ForeColor <- Color.Black
        btnNextRound.Visible <- false
        
        gameOverPanel.Visible <- false
        gamePanel.Visible <- true
        checkTurnAndHandStatus currentState

    let onAiTimerTick _ =
        aiTimer.Stop()
        
        if not isRoundEndWaiting && not currentState.IsPlayerTurn && not (List.isEmpty currentState.Computer.Hand) then
            let isPlayerLastEraser = (lastCardPlayedString = "❌ Eraser")
            let aiChosenCardOpt = GameEngine.selectAiCard currentState.Difficulty currentState.Computer.Hand currentState.Computer.CurrentScore currentState.Player.CurrentScore isPlayerLastEraser

            match aiChosenCardOpt with
            | None -> ()
            | Some cardToPlay ->
                let mutable found = false
                let newHand = currentState.Computer.Hand |> List.filter (fun c -> 
                    if not found && c = cardToPlay then found <- true; false else true
                )
                
                lastCardPlayedString <- 
                    match cardToPlay with
                    | Number n -> sprintf "🔢 Num %d" n
                    | Card.Double -> "👑 Double"
                    | Card.Eraser -> "❌ Eraser"
                    | Card.Trade  -> "🔄 Trade"
                
                let actualPlayedCard = cardToPlay
                let newCompState, newPlayerState = GameEngine.applyCardEffect actualPlayedCard { currentState.Computer with Hand = newHand } currentState.Player
                let nextState = { currentState with Computer = newCompState; Player = newPlayerState }
                
                currentState <- nextState
                gamePanel.Invalidate()

                if nextState.Computer.CurrentScore = nextState.Player.CurrentScore then
                    centerStatusMessage <- "🤝 DRAW! Round Reset!"
                    collectFieldCardsToWinner nextState
                    isRoundEndWaiting <- true
                    pendingNextState <- Some (evaluateRoundEnd nextState)
                    btnNextRound.Visible <- true
                elif nextState.Computer.CurrentScore < nextState.Player.CurrentScore then
                    centerStatusMessage <- "🤖 AI Too Low! WIN!"
                    collectFieldCardsToWinner nextState
                    isRoundEndWaiting <- true
                    pendingNextState <- Some (evaluateRoundEnd nextState)
                    btnNextRound.Visible <- true
                else
                    currentState <- { nextState with IsPlayerTurn = true }
                    checkTurnAndHandStatus currentState

                gamePanel.Invalidate()

    do
        this.Text <- "⚔️ Sum Kingdom : Strategic Duel ⚔️"
        this.ClientSize <- Size(950, 650)
        this.FormBorderStyle <- FormBorderStyle.FixedSingle
        this.MaximizeBox <- false

        aiTimer.Tick.Add(onAiTimerTick)

        titlePanel.Dock <- DockStyle.Fill
        titlePanel.BackColor <- Color.FromArgb(24, 24, 32)

        let lblTitle = new Label()
        lblTitle.Text <- "SUM KINGDOM\n👑"
        lblTitle.Font <- new Font("Georgia", 36.0f, FontStyle.Bold)
        lblTitle.ForeColor <- Color.Gold
        lblTitle.TextAlign <- ContentAlignment.MiddleCenter
        lblTitle.Size <- Size(600, 140)
        lblTitle.Location <- Point(175, 60)
        titlePanel.Controls.Add(lblTitle)

        grpDifficulty.Text <- "SELECT DIFFICULTY"
        grpDifficulty.Size <- Size(400, 65)
        grpDifficulty.Location <- Point(275, 230)
        grpDifficulty.ForeColor <- Color.Gold
        grpDifficulty.Font <- new Font("Segoe UI", 9.0f, FontStyle.Bold)

        rdoEasy.Text <- "Easy"
        rdoEasy.Location <- Point(20, 25)
        rdoEasy.ForeColor <- Color.White
        rdoEasy.Size <- Size(100, 25)

        rdoNormal.Text <- "Normal"
        rdoNormal.Location <- Point(140, 25)
        rdoNormal.ForeColor <- Color.White
        rdoNormal.Size <- Size(100, 25)
        rdoNormal.Checked <- true

        rdoHard.Text <- "Hard"
        rdoHard.Location <- Point(260, 25)
        rdoHard.ForeColor <- Color.White
        rdoHard.Size <- Size(100, 25)

        grpDifficulty.Controls.AddRange([| rdoEasy :> Control; rdoNormal :> Control; rdoHard :> Control |])
        titlePanel.Controls.Add(grpDifficulty)

        btnStart.Text <- "START GAME"
        btnStart.Size <- Size(220, 45)
        btnStart.Location <- Point(365, 320)
        btnStart.Font <- new Font("Segoe UI", 11.0f, FontStyle.Bold)
        btnStart.BackColor <- Color.Gold
        btnStart.ForeColor <- Color.Black
        btnStart.FlatStyle <- FlatStyle.Flat
        btnStart.Click.Add(fun _ -> 
            if rdoEasy.Checked then selectedDifficulty <- SumKingdom.Difficulty.Easy
            elif rdoNormal.Checked then selectedDifficulty <- SumKingdom.Difficulty.Normal
            else selectedDifficulty <- SumKingdom.Difficulty.Hard
            
            currentState <- initNewGame selectedDifficulty ()
            titlePanel.Visible <- false
            gamePanel.Visible <- true
            checkTurnAndHandStatus currentState
        )
        titlePanel.Controls.Add(btnStart)

        btnRules.Text <- "HOW TO PLAY"
        btnRules.Size <- Size(220, 45)
        btnRules.Location <- Point(365, 385)
        btnRules.Font <- new Font("Segoe UI", 11.0f, FontStyle.Bold)
        btnRules.BackColor <- Color.DarkSlateGray
        btnRules.ForeColor <- Color.White
        btnRules.FlatStyle <- FlatStyle.Flat
        btnRules.Click.Add(fun _ -> 
            titlePanel.Visible <- false
            rulePanel.Visible <- true
        )
        titlePanel.Controls.Add(btnRules)

        let btnExit = new Button()
        btnExit.Text <- "EXIT GAME"
        btnExit.Size <- Size(220, 45)
        btnExit.Location <- Point(365, 450)
        btnExit.Font <- new Font("Segoe UI", 11.0f, FontStyle.Bold)
        btnExit.BackColor <- Color.FromArgb(70, 30, 30)
        btnExit.ForeColor <- Color.White
        btnExit.FlatStyle <- FlatStyle.Flat
        btnExit.Click.Add(fun _ -> this.Close())
        titlePanel.Controls.Add(btnExit)

        gamePanel.Dock <- DockStyle.Fill
        gamePanel.BackColor <- Color.FromArgb(32, 36, 44)
        gamePanel.Visible <- false
        gamePanel.DoubleBuffered <- true

        btnNextRound.Text <- "NEXT ROUND ➡️"
        btnNextRound.Size <- Size(190, 45)
        btnNextRound.Location <- Point(380, 310)
        btnNextRound.Font <- new Font("Segoe UI", 11.0f, FontStyle.Bold)
        btnNextRound.BackColor <- Color.Gold
        btnNextRound.ForeColor <- Color.Black
        btnNextRound.FlatStyle <- FlatStyle.Flat
        btnNextRound.Visible <- false
        btnNextRound.Click.Add(fun _ -> 
            if isGameFinalClosingMode then
                forceTriggerGameOver currentState
            else
                match pendingNextState with
                | Some nextState ->
                    currentState <- nextState
                    pendingNextState <- None
                    isRoundEndWaiting <- false
                    btnNextRound.Visible <- false
                    lastCardPlayedString <- "None"
                    checkTurnAndHandStatus currentState
                | None -> ()
        )
        gamePanel.Controls.Add(btnNextRound)

        gamePanel.Paint.Add(fun e -> 
            let g = e.Graphics
            let fontLabel = new Font("Segoe UI", 11.0f, FontStyle.Bold)
            let fontScore = new Font("Impact", 24.0f)
            let fontMatch = new Font("Segoe UI", 14.0f, FontStyle.Bold)

            g.DrawString(sprintf "MATCH POINTS:  %d  vs  %d" currentState.Player.GamePoints currentState.Computer.GamePoints, fontMatch, Brushes.Gold, 320.0f, 15.0f)

            g.DrawString("🤖 COMPUTER HAND (" + string currentState.Computer.Hand.Length + " cards)", fontLabel, Brushes.LightGray, 40.0f, 65.0f)
            let compHandCount = currentState.Computer.Hand.Length
            for i in 0 .. compHandCount - 1 do
                let cx = 40 + (i * 32)
                let cardRect = Rectangle(cx, 90, 55, 80)
                g.FillRectangle(Brushes.Firebrick, cardRect)
                g.DrawRectangle(Pens.Black, cardRect)
                g.DrawString("?", fontLabel, Brushes.White, float32 (cx + 20), 115.0f)

            g.DrawLine(Pens.DimGray, 40, 195, 910, 195)

            g.DrawString("🤖 AI FIELD SCORE", fontLabel, Brushes.Tomato, 80.0f, 215.0f)
            g.DrawString(string currentState.Computer.CurrentScore, fontScore, Brushes.Tomato, 130.0f, 240.0f)
            
            let compVisibleCards = 
                currentState.Computer.PlayedCards
                |> List.filter (fun card ->
                    match card with
                    | Number 1 -> currentState.Computer.LastErasedCard.IsNone
                    | Number _ -> true
                    | Card.Double -> true
                    | _ -> false
                )
            
            if not (List.isEmpty compVisibleCards) then
                let topCard = compVisibleCards.Head
                drawCardGraphics g topCard 350 215 false

            g.DrawString("👤 PLAYER FIELD SCORE", fontLabel, Brushes.LightGreen, 80.0f, 375.0f)
            g.DrawString(string currentState.Player.CurrentScore, fontScore, Brushes.LightGreen, 130.0f, 400.0f)
            
            let playerVisibleCards = 
                currentState.Player.PlayedCards
                |> List.filter (fun card ->
                    match card with
                    | Number 1 -> currentState.Player.LastErasedCard.IsNone
                    | Number _ -> true
                    | Card.Double -> true
                    | _ -> false
                )
            
            if not (List.isEmpty playerVisibleCards) then
                let topCard = playerVisibleCards.Head
                drawCardGraphics g topCard 350 365 false

            g.DrawLine(Pens.DimGray, 40, 500, 910, 500)

            g.DrawString("👤 YOUR HAND CARDS", fontLabel, Brushes.LightBlue, 40.0f, 510.0f)
            let mutable hIdx = 0
            let canIPlay = currentState.IsPlayerTurn && not isRoundEndWaiting
            for card in currentState.Player.Hand do
                let hx = 40 + (hIdx * 94)
                drawCardGraphics g card hx 530 (canIPlay && (currentState.Player.CurrentScore < currentState.Computer.CurrentScore))
                hIdx <- hIdx + 1

            let boxRect = Rectangle(780, 45, 145, 55)
            if not btnNextRound.Visible then
                g.FillRectangle(Brushes.Black, boxRect)
                g.DrawRectangle(Pens.Gold, boxRect)
                let fontMsg = new Font("Segoe UI", 9.0f, FontStyle.Bold)
                g.DrawString(centerStatusMessage, fontMsg, Brushes.White, 788.0f, 53.0f)

            let diffText = sprintf "MODE: %A" currentState.Difficulty
            g.DrawString(diffText, fontLabel, Brushes.LightGray, 820.0f, 15.0f)

            g.DrawString("LAST AI CARD", fontLabel, Brushes.LightGray, 800.0f, 215.0f)
            g.DrawString(lastCardPlayedString, fontLabel, Brushes.Gold, 800.0f, 245.0f)

            g.DrawString(sprintf "🏆 PLAYER PILES :  %d  sets" playerWonPiles.Count, fontLabel, Brushes.Gold, 740.0f, 370.0f)
            g.DrawString(sprintf "💀 AI PILES     :  %d  sets" computerWonPiles.Count, fontLabel, Brushes.Tomato, 740.0f, 410.0f)
        )

        gamePanel.MouseClick.Add(fun e -> 
            if currentState.IsPlayerTurn && not isRoundEndWaiting && (currentState.Player.CurrentScore < currentState.Computer.CurrentScore) then
                let myHandCount = currentState.Player.Hand.Length
                let mutable clickedCardIndex = -1
                for i in 0 .. myHandCount - 1 do
                    let hx = 40 + (i * 94)
                    if e.X >= hx && e.X <= hx + 85 && e.Y >= 530 && e.Y <= 645 then
                        clickedCardIndex <- i
                
                if clickedCardIndex <> -1 then
                    let cardToPlay = currentState.Player.Hand.[clickedCardIndex]
                    let newHand = currentState.Player.Hand |> List.mapi (fun idx c -> (idx, c)) |> List.filter (fun (idx, _) -> idx <> clickedCardIndex) |> List.map snd
                    
                    let newPlayerState, newCompState = GameEngine.applyCardEffect cardToPlay { currentState.Player with Hand = newHand } currentState.Computer
                    let nextState = { currentState with Player = newPlayerState; Computer = newCompState }
                    
                    currentState <- nextState
                    gamePanel.Invalidate()

                    if nextState.Player.CurrentScore = nextState.Computer.CurrentScore then
                        centerStatusMessage <- "🤝 DRAW! Round Reset!"
                        collectFieldCardsToWinner nextState
                        isRoundEndWaiting <- true
                        pendingNextState <- Some (evaluateRoundEnd nextState)
                        btnNextRound.Visible <- true
                    elif nextState.Player.CurrentScore < nextState.Computer.CurrentScore then
                        centerStatusMessage <- "👤 You Too Low! LOSE."
                        collectFieldCardsToWinner nextState
                        isRoundEndWaiting <- true
                        pendingNextState <- Some (evaluateRoundEnd nextState)
                        btnNextRound.Visible <- true
                    else
                        currentState <- { nextState with IsPlayerTurn = false }
                        checkTurnAndHandStatus currentState
                    gamePanel.Invalidate()
        )

        rulePanel.Dock <- DockStyle.Fill
        rulePanel.BackColor <- Color.FromArgb(24, 28, 36)
        rulePanel.Visible <- false

        let txtRules = new TextBox()
        txtRules.Multiline <- true
        txtRules.ReadOnly <- true
        txtRules.ScrollBars <- ScrollBars.Vertical
        txtRules.Size <- Size(850, 420)
        txtRules.Location <- Point(45, 90)
        txtRules.Font <- new Font("Consolas", 13.0f, FontStyle.Regular)
        txtRules.BackColor <- Color.FromArgb(16, 18, 22)
        txtRules.ForeColor <- Color.LightYellow
        txtRules.Text <- ruleTextKorean
        rulePanel.Controls.Add(txtRules)

        let btnKo = new Button()
        btnKo.Text <- "KOREAN 🇰🇷"
        btnKo.Size <- Size(120, 38)
        btnKo.Location <- Point(45, 30)
        btnKo.Font <- new Font("Segoe UI", 10.0f, FontStyle.Bold)
        btnKo.BackColor <- Color.SteelBlue
        btnKo.ForeColor <- Color.White
        btnKo.FlatStyle <- FlatStyle.Flat
        btnKo.Click.Add(fun _ -> txtRules.Text <- ruleTextKorean)
        rulePanel.Controls.Add(btnKo)

        let btnEn = new Button()
        btnEn.Text <- "ENGLISH 🇺🇸"
        btnEn.Size <- Size(120, 38)
        btnEn.Location <- Point(180, 30)
        btnEn.Font <- new Font("Segoe UI", 10.0f, FontStyle.Bold)
        btnEn.BackColor <- Color.SlateGray
        btnEn.ForeColor <- Color.White
        btnEn.FlatStyle <- FlatStyle.Flat
        btnEn.Click.Add(fun _ -> txtRules.Text <- ruleTextEnglish)
        rulePanel.Controls.Add(btnEn)

        let btnCloseRules = new Button()
        btnCloseRules.Text <- "RETURN TO MAIN MENU"
        btnCloseRules.Size <- Size(260, 45)
        btnCloseRules.Location <- Point(345, 540)
        btnCloseRules.Font <- new Font("Segoe UI", 10.0f, FontStyle.Bold)
        btnCloseRules.BackColor <- Color.Gold
        btnCloseRules.ForeColor <- Color.Black
        btnCloseRules.FlatStyle <- FlatStyle.Flat
        btnCloseRules.Click.Add(fun _ -> 
            rulePanel.Visible <- false
            titlePanel.Visible <- true
        )
        rulePanel.Controls.Add(btnCloseRules)

        gameOverPanel.Dock <- DockStyle.Fill
        gameOverPanel.BackColor <- Color.FromArgb(20, 20, 26)
        gameOverPanel.Visible <- false

        lblStatsResult.Font <- new Font("Impact", 44.0f, FontStyle.Bold)
        lblStatsResult.Size <- Size(700, 90)
        lblStatsResult.Location <- Point(125, 80)
        lblStatsResult.TextAlign <- ContentAlignment.MiddleCenter
        gameOverPanel.Controls.Add(lblStatsResult)

        lblStatsScore.Font <- new Font("Segoe UI", 13.0f)
        lblStatsScore.ForeColor <- Color.White
        lblStatsScore.Size <- Size(600, 180)
        lblStatsScore.Location <- Point(175, 210)
        lblStatsScore.TextAlign <- ContentAlignment.MiddleCenter
        gameOverPanel.Controls.Add(lblStatsScore)

        btnStatsRestart.Text <- "PLAY AGAIN 🔄"
        btnStatsRestart.Size <- Size(240, 50)
        btnStatsRestart.Location <- Point(355, 410)
        btnStatsRestart.Font <- new Font("Segoe UI", 12.0f, FontStyle.Bold)
        btnStatsRestart.BackColor <- Color.Gold
        btnStatsRestart.ForeColor <- Color.Black
        btnStatsRestart.FlatStyle <- FlatStyle.Flat
        btnStatsRestart.Click.Add(fun _ -> resetEntireGameToPlay())
        gameOverPanel.Controls.Add(btnStatsRestart)

        let btnStatsMain = new Button()
        btnStatsMain.Text <- "RETURN TO MAIN"
        btnStatsMain.Size <- Size(240, 50)
        btnStatsMain.Location <- Point(355, 480)
        btnStatsMain.Font <- new Font("Segoe UI", 12.0f, FontStyle.Bold)
        btnStatsMain.BackColor <- Color.DarkSlateGray
        btnStatsMain.ForeColor <- Color.White
        btnStatsMain.FlatStyle <- FlatStyle.Flat
        btnStatsMain.Click.Add(fun _ -> 
            gameOverPanel.Visible <- false
            titlePanel.Visible <- true
        )
        gameOverPanel.Controls.Add(btnStatsMain)

        this.Controls.Add(titlePanel)
        this.Controls.Add(gamePanel)
        this.Controls.Add(rulePanel)
        this.Controls.Add(gameOverPanel)

    member this.TriggerComputerTurn() = runComputerTurnWithTimer()

[<EntryPoint>]
let main argv =
    Application.EnableVisualStyles()
    Application.SetCompatibleTextRenderingDefault(false)
    Application.Run(new GameForm())
    0