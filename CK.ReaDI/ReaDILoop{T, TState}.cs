namespace CK.Core;

readonly struct ReaDILoop<T, TState>
    where T : class
    where TState : new()
{
    public readonly T Value;
    public readonly TState State;

    public ReaDILoop( T value )
    {
        Value = value;
        State = new TState();
    }

}
