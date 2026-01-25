namespace Maelstrom.Unity
{
    /// <summary>
    ///     Enum defining all possible data tags for type-safe network communication
    /// </summary>
    public enum DataTag : ushort
    {
        CurrentMaelstromValue = 0,

        TargetMaelstromValue = 1,

        CurrentDataDate = 2,
        Logs = 3
    }
}