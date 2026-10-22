namespace OmniKit.Interaction;

/// <summary>Tracks the physical primary key independently of chord and UI eligibility.</summary>
internal sealed class SoftInteractionPress
{
    private bool initialized;
    private int key;
    private int modifiers;
    private bool wasDown;

    public bool Consumed { get; private set; }

    public bool Sample(int configuredKey, int configuredModifiers, bool down)
    {
        if (!initialized || key != configuredKey || modifiers != configuredModifiers)
        {
            initialized = true;
            key = configuredKey;
            modifiers = configuredModifiers;
            wasDown = down;
            Consumed = false;
            return false;
        }

        var pressed = down && !wasDown;
        wasDown = down;
        if (!down) Consumed = false;
        return pressed;
    }

    public void Consume() => Consumed = true;
    public void Pause() => Consumed = false;
}
