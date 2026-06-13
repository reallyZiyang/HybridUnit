namespace Game.Play.Systems.Level
{
    public enum LevelFlowState
    {
        None = 0,
        MainMenu = 1,
        LoadingBattle = 2,
        BattleRunning = 3,
        BattleFinished = 4
    }

    public enum BattleOutcome
    {
        None = 0,
        Victory = 1,
        Defeat = 2
    }
}
