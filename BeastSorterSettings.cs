using ExileCore.Shared.Attributes;
using ExileCore.Shared.Interfaces;
using ExileCore.Shared.Nodes;
using System.Windows.Forms;
using SharpDX;

namespace BeastSorter;

public class BeastSorterSettings : ISettings
{
    [Menu("Plugin Description", "BeastSorter automatically uses Bestiary Orbs on captured beasts to create Imprinted Bestiary Orbs. It right-clicks orbs, clicks on beasts, and places the resulting items in free inventory space.")]
    public EmptyNode Description { get; set; } = new EmptyNode();

    [Menu("How to Use", "1. Buy Bestiary Orbs\n2. Open the Bestiary panel and go to Captured Beasts tab\n3. Open your inventory panel\n4. Press the Activation Key to start the process\n5. The plugin will automatically process all orbs until none remain or no free space is available")]
    public EmptyNode UsageInstructions { get; set; } = new EmptyNode();

    [Menu("Activation Key", "Press this key to start the currency sorting operation")]
    public HotkeyNode ActivateKey { get; set; } = new HotkeyNode(Keys.F7);

    [Menu("Cancel Key", "Press this key to cancel the current operation")]
    public HotkeyNode CancelKey { get; set; } = new HotkeyNode(Keys.F8);

    [Menu("Open Inventory Key", "Key to open inventory panel if not already open")]
    public HotkeyNode OpenInventoryKey { get; set; } = new HotkeyNode(Keys.I);

    [Menu("Open Bestiary Key", "Key to open bestiary panel if not already open")]
    public HotkeyNode OpenBestiaryKey { get; set; } = new HotkeyNode(Keys.N);

    [Menu("Show Debug Info", "Display debug information on screen")]
    public ToggleNode ShowDebugInfo { get; set; } = new ToggleNode(true);

    [Menu("Click Delay (ms)", "Delay between mouse clicks")]
    public RangeNode<int> ClickDelay { get; set; } = new RangeNode<int>(100, 50, 500);

    [Menu("Wait Timeout (ms)", "Maximum time to wait for operations")]
    public RangeNode<int> WaitTimeout { get; set; } = new RangeNode<int>(2000, 1000, 5000);

    [Menu("Target Settings")]
    public TargetSettings TargetSettings { get; set; } = new TargetSettings();

    [Menu("Timing Settings")]
    public TimingSettings TimingSettings { get; set; } = new TimingSettings();

    [Menu("Currency Filter")]
    public CurrencyFilterSettings CurrencyFilter { get; set; } = new CurrencyFilterSettings();

    public ToggleNode Enable { get; set; } = new ToggleNode(true);
}

public class TargetSettings
{
    [Menu("Use Free Inventory Space", "Automatically find and use free inventory space for the resulting item")]
    public ToggleNode UseFreeInventorySpace { get; set; } = new ToggleNode(true);
}

public class TimingSettings
{
    [Menu("Click Delay (ms)", "Delay between mouse clicks")]
    public RangeNode<int> ClickDelay { get; set; } = new RangeNode<int>(100, 50, 500);

    [Menu("Wait Timeout (ms)", "Maximum time to wait for operations")]
    public RangeNode<int> WaitTimeout { get; set; } = new RangeNode<int>(2000, 1000, 5000);

    [Menu("Frame Delay", "Number of frames to wait between operations")]
    public RangeNode<int> FrameDelay { get; set; } = new RangeNode<int>(1, 1, 10);
}

public class CurrencyFilterSettings
{
    [Menu("Bestiary Orb Detection", "Detect Bestiary Orbs in inventory")]
    public ToggleNode DetectBestiaryOrbs { get; set; } = new ToggleNode(true);

    [Menu("Debug Item Names", "Show detected item names in log for debugging")]
    public ToggleNode DebugItemNames { get; set; } = new ToggleNode(false);
} 