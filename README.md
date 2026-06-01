⚔️ SUM KINGDOM : Strategic Duel

A tactical card-dueling board game built with F# and targeting .NET 10.
Players face off against an advanced AI, managing a dynamic field stack and utilizing powerful ability cards to outmaneuver the opponent.
🚀 How to Run

Follow these steps to download, build, and run the game on your local machine.
1. Prerequisites

Ensure you have the .NET 10 SDK installed on your system.

    Download .NET SDK

2. Download and Navigate to the Project
Option A: Via Git Clone (Recommended)

Clone this repository using your terminal and navigate into the project root folder:

```bash
git clone https://github.com/Ver1ta/Sum_Kingdom.git
cd Sum_Kingdom
```
Option B: Via ZIP Download

    Click the green Code button at the top right of this GitHub page.

    Click Download ZIP and extract the files.

    Open your terminal or command prompt and navigate (cd) to the extracted folder:

```bash
cd /path/to/extracted/Sum_Kingdom
```
3. Execution via CLI

Once you are inside the project root directory (where Sum_Kingdom.fsproj is located), run the following command to restore dependencies and launch the game instantly:

```bash
dotnet run
```
🎮 Game Rules & Mechanics
1. Setup

    Both players are dealt an equal starting hand of 9 random cards.

    At the beginning of the round, 1 random number card from the deck is automatically placed on each player's Field Stack as the "Base Card", establishing the initial round score.

2. Turn Sequence & Initiative (Action Rule)

    The Absolute Condition: You can ONLY play a card from your hand when your current Field Score is strictly less than your opponent's total score.

    If your score is equal to or higher than the opponent's, you are prohibited from making a move.

    The moment your played card pushes your total score above the opponent's, your initiative immediately ends, and control shifts automatically to the other player.

3. Field Stack Mechanics

    To optimize UI clarity, only the single topmost card played most recently is visible on each field stack.

    When a card is destroyed by an Eraser, the previous card hidden underneath is instantly revealed, and the player's score is reverted accordingly.

4. Card Registry & Spell Effects

    Normal Numbers (2 - 13): Placed directly on the stack, permanently adding their exact value to your total score.

    [ 👑 Double ]: Instantly doubles your current Field Score and sits on top of the stack.

    [ ❌ Eraser ]: Targets and vaporizes the topmost visible card on the opponent's field stack.

    [ 🔄 Trade ]: Instantly swaps your entire hand with your opponent's hand to disrupt their long-term strategy.

    [ 🔢 Number 1 ]: Acts as a standard 1-point card. However, if played exactly on the immediate turn following an opponent's Eraser, it triggers a [💖 Revive] combo, resurrecting and restoring your destroyed card back to the top of the stack.

5. Final Match End Condition

    The game officially terminates the exact moment a player's turn arrives (when their score is lower), but they have exactly 0 cards left in hand and cannot make a legal move.

    In the final resolution screen, the duelist who has amassed the highest total of Match Points (Game Points) ascends the throne as the Ultimate Champion!

🤖 LLM Attribution & Project Integrity

In compliance with the term project specifications, this section provides full disclosure regarding the utilization of Large Language Models (LLMs) during the development of Sum Kingdom:

    LLM Used: Google Gemini (2026 Presentation Model)

    Scope of Assistance:

        Assisted in generating the foundational layout and event-driven boilerplate code for the Windows Forms GUI structure in F#.

        Collaborated in debugging the coordinate mapping logic for card-clicking mechanics and testing edge-case round settlement behaviors.

        Formatted and structured this professional markdown README.md documentation.

    Human Ownership: All core functional programming logic, game rule state transitions, win-loss evaluations, and mathematical mechanics within GameEngine.fs were independently conceptualized, engineered, and verified by the human author to ensure full adherence to the original academic goals.
