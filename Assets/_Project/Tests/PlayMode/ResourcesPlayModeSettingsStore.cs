using NUnit.Framework;
using TowerOfBabel.Settings;

/// <summary>
/// Automated tests never read or write the player's saved settings: every test in this assembly runs on an in-memory
/// store holding the defaults (High quality, default keys).
/// </summary>
[SetUpFixture]
public sealed class ResourcesPlayModeSettingsStore
{
    private IGameSettingsStore previous;

    [OneTimeSetUp]
    public void UseMemoryStore()
    {
        previous = GameSettings.Store;
        GameSettings.Store = new MemorySettingsStore();
    }

    [OneTimeTearDown]
    public void RestoreStore() => GameSettings.Store = previous;
}
