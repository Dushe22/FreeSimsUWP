namespace FSO.Common.Platform
{
    public interface IGamePaths
    {
        string ContentRoot { get; }
        string GameDataRoot { get; }
        string UserDataRoot { get; }
        string GetContentPath(string relativePath);
        string GetGameDataPath(string relativePath);
        string GetUserDataPath(string relativePath);
    }
}
