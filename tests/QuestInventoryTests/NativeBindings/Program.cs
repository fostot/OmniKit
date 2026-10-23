Console.WriteLine("Production quest services compiled against installed bindings. No game functions were called.");

namespace OmniKit
{
    // Only the two switches read by the linked production services. No game host,
    // services, configuration file, inventory or player state is initialized.
    internal sealed class Configuration
    {
        public bool QuestItemUseEnabled = false;
        public bool QuestEmoteUseEnabled = false;
    }
}
