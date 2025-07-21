using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ExileCore;
using ExileCore.PoEMemory;
using ExileCore.PoEMemory.Components;
using ExileCore.PoEMemory.Elements;
using ExileCore.PoEMemory.Elements.InventoryElements;
using ExileCore.PoEMemory.MemoryObjects;
using ExileCore.Shared;
using ExileCore.Shared.Enums;
using ExileCore.Shared.Helpers;
using SharpDX;
using Vector2 = System.Numerics.Vector2;

namespace BeastSorter;

public class BeastSorter : BaseSettingsPlugin<BeastSorterSettings>
{
    private SyncTask<bool> _currentOperation;
    private IngameState InGameState => GameController.IngameState;
    private SharpDX.Vector2 WindowOffset => GameController.Window.GetWindowRectangleTimeCache.TopLeft;

    public override bool Initialise()
    {
        Input.RegisterKey(Settings.ActivateKey);
        Input.RegisterKey(Settings.CancelKey);
        Input.RegisterKey(Settings.OpenInventoryKey);
        Input.RegisterKey(Settings.OpenBestiaryKey);
        Input.RegisterKey(Settings.UseAllBeastsKey);
        Input.RegisterKey(Settings.ReleaseFilteredBeastsKey);
        return true;
    }

    public override void Render()
    {
        if (!Settings.Enable.Value) return;

        // Execute the current operation if it exists
        if (_currentOperation != null)
        {
            TaskUtils.RunOrRestart(ref _currentOperation, () => null);
        }

        // Check if activation key is pressed
        if (Input.GetKeyState(Settings.ActivateKey.Value))
        {
            LogMessageFiltered("Activation key pressed - starting operation", 1);
            if (_currentOperation == null || _currentOperation.GetAwaiter().IsCompleted)
            {
                LogMessageFiltered("Creating new operation", 1);
                _currentOperation = SortCurrencyItems();
                LogMessageFiltered("Operation created directly", 1);
            }
            else
            {
                LogMessageFiltered("Operation already in progress", 1);
            }
        }

        // Check if cancel key is pressed
        if (Input.GetKeyState(Settings.CancelKey.Value))
        {
            LogMessageFiltered("Cancel key pressed - stopping operation", 1);
            _currentOperation = null;
        }

        // Check if use all beasts key is pressed
        if (Input.GetKeyState(Settings.UseAllBeastsKey.Value))
        {
            LogMessageFiltered("Use All Beasts key pressed - starting operation", 1);
            if (_currentOperation == null || _currentOperation.GetAwaiter().IsCompleted)
            {
                LogMessageFiltered("Creating new use all beasts operation", 1);
                _currentOperation = UseAllBeastsInInventory();
                LogMessageFiltered("Use all beasts operation created", 1);
            }
            else
            {
                LogMessageFiltered("Operation already in progress", 1);
            }
        }

        // Check if release filtered beasts key is pressed
        if (Input.GetKeyState(Settings.ReleaseFilteredBeastsKey.Value))
        {
            LogMessageFiltered("Release Filtered Beasts key pressed - starting operation", 1);
            if (_currentOperation == null || _currentOperation.GetAwaiter().IsCompleted)
            {
                LogMessageFiltered("Creating new release filtered beasts operation", 1);
                _currentOperation = ReleaseFilteredBeasts();
                LogMessageFiltered("Release filtered beasts operation created", 1);
            }
            else
            {
                LogMessageFiltered("Operation already in progress", 1);
            }
        }

        // Draw UI elements if needed
        if (Settings.ShowDebugInfo.Value)
        {
            DrawDebugInfo();
        }
    }

    private async SyncTask<bool> SortCurrencyItems()
    {
        LogMessageFiltered("=== SortCurrencyItems STARTED ===", 1);
        LogMessageFiltered("SortCurrencyItems method called!", 1);
        LogMessageFiltered("About to enter try block", 1);
        try
        {
            LogMessageFiltered("=== Starting BeastSorter operation ===", 1);

            // Try to open inventory if not visible
            if (!InGameState.IngameUi.InventoryPanel.IsVisible)
            {
                LogMessageFiltered("Inventory panel not visible, attempting to open...", 1);
                Input.KeyDown(Settings.OpenInventoryKey.Value);
                await TaskUtils.NextFrame();
                Input.KeyUp(Settings.OpenInventoryKey.Value);
                await TaskUtils.NextFrame();
                
                // Wait a bit for the panel to open
                await Task.Delay(Settings.ClickDelay.Value);
                
                if (!InGameState.IngameUi.InventoryPanel.IsVisible)
                {
                    LogMessage("ERROR: Failed to open inventory panel", 2);
                    return false;
                }
                LogMessageFiltered("SUCCESS: Inventory panel opened", 1);
            }
            else
            {
                LogMessageFiltered("Inventory panel already visible", 1);
            }

            // Try to open Bestiary tab if not visible
            var bestiaryTab = InGameState.IngameUi.ChallengesPanel?.TabContainer?.BestiaryTab?.CapturedBeastsTab;
            if (bestiaryTab == null || !bestiaryTab.IsVisible)
            {
                LogMessageFiltered("Bestiary tab not visible, attempting to open...", 1);
                
                // First try to open the challenges panel
                if (InGameState.IngameUi.ChallengesPanel?.IsVisible != true)
                {
                    Input.KeyDown(Settings.OpenBestiaryKey.Value);
                    await TaskUtils.NextFrame();
                    Input.KeyUp(Settings.OpenBestiaryKey.Value);
                    await TaskUtils.NextFrame();
                    await Task.Delay(Settings.ClickDelay.Value);
                }
                
                // Now try to navigate to the Bestiary tab
                var challengesPanel = InGameState.IngameUi.ChallengesPanel;
                if (challengesPanel?.IsVisible == true)
                {
                    // Try to click on the Bestiary tab
                    var bestiaryTabButton = challengesPanel.TabContainer?.BestiaryTab;
                    if (bestiaryTabButton != null && bestiaryTabButton.IsVisible)
                    {
                        await ClickOnElement(bestiaryTabButton);
                        await Task.Delay(Settings.ClickDelay.Value);
                    }
                }
                
                // Check again
                bestiaryTab = InGameState.IngameUi.ChallengesPanel?.TabContainer?.BestiaryTab?.CapturedBeastsTab;
                if (bestiaryTab == null || !bestiaryTab.IsVisible)
                {
                    LogMessage("ERROR: Failed to open Bestiary tab", 2);
                    return false;
                }
                LogMessageFiltered("SUCCESS: Bestiary tab opened", 1);
            }
            else
            {
                LogMessageFiltered("Bestiary tab already visible", 1);
            }

            // Get currency items from inventory
            var currencyItems = GetCurrencyItemsFromInventory();
            LogMessageFiltered($"Found {currencyItems.Count} Bestiary Orbs in inventory", 1);
            
            if (!currencyItems.Any())
            {
                LogMessage("ERROR: No Bestiary Orbs found in inventory", 2);
                return false;
            }

            // Check free inventory space
            var freePosition = FindFreeInventoryPosition();
            LogMessageFiltered($"Free inventory space found: {freePosition.HasValue}", 1);
            if (freePosition.HasValue)
            {
                LogMessageFiltered($"Free space at position: ({freePosition.Value.X}, {freePosition.Value.Y})", 1);
            }
            else
            {
                LogMessage("ERROR: No free inventory space available", 2);
                return false;
            }

            int totalOrbsToProcess = currencyItems.Sum(item => item.Item.GetComponent<Stack>()?.Size ?? 1);
            LogMessageFiltered($"Ready to process {totalOrbsToProcess} Bestiary Orbs", 1);

            // Process currency items until no more orbs or no free space
            while (true)
            {
                if (Input.GetKeyState(Settings.CancelKey.Value))
                {
                    LogMessageFiltered("Operation cancelled by user", 1);
                    return false;
                }

                // Refresh inventory data to get current state
                var currentCurrencyItems = GetCurrencyItemsFromInventory();
                
                if (!currentCurrencyItems.Any())
                {
                    LogMessageFiltered("No more Bestiary Orbs found in inventory", 1);
                    break;
                }

                // Check if there are any captured beasts available
                var targetBeast = FindTargetBeast();
                if (targetBeast == null)
                {
                    LogMessageFiltered("No captured beasts available to use orbs on", 1);
                    break;
                }

                // Get the first available orb
                var currencyItem = currentCurrencyItems.First();
                var stackSize = currencyItem.Item.GetComponent<Stack>()?.Size ?? 1;
                
                LogMessageFiltered($"Processing stack at position ({currencyItem.PosX}, {currencyItem.PosY}) with {stackSize} orbs", 1);
                
                // Check if the item still exists and has orbs left
                if (currencyItem.Item == null || !currencyItem.Item.IsValid)
                {
                    LogMessage("Item no longer valid, skipping", 2);
                    continue;
                }
                
                var currentStackSize = currencyItem.Item.GetComponent<Stack>()?.Size ?? 0;
                if (currentStackSize <= 0)
                {
                    LogMessageFiltered("Stack is empty, moving to next stack", 1);
                    continue;
                }

                // Check if we have free space, but allow processing the last orb even without space
                // because the orb will be consumed and free up space
                var currentFreePosition = FindFreeInventoryPosition();
                var totalOrbsRemaining = currentCurrencyItems.Sum(item => item.Item.GetComponent<Stack>()?.Size ?? 1);
                
                if (!currentFreePosition.HasValue)
                {
                    // If this is the very last orb, we can still process it because it will be consumed
                    if (totalOrbsRemaining <= 1)
                    {
                        LogMessageFiltered("Last orb detected - processing despite no free space (orb will be consumed)", 1);
                    }
                    else
                    {
                        LogMessageFiltered("No free inventory space available and more than 1 orb remaining", 1);
                        break;
                    }
                }
                else
                {
                    LogMessageFiltered($"Free space available at ({currentFreePosition.Value.X}, {currentFreePosition.Value.Y})", 1);
                }

                LogMessageFiltered($"Processing orb 1/{currentStackSize} from stack at ({currencyItem.PosX}, {currencyItem.PosY})", 1);

                // Actually process the currency item and wait for it to complete
                if (!await ProcessCurrencyItem(currencyItem))
                {
                    LogMessage("ERROR: Failed to process currency item", 2);
                    return false;
                }
            }

            LogMessageFiltered("BeastSorter operation completed successfully", 1);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR during BeastSorter operation: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> UseAllBeastsInInventory()
    {
        LogMessageFiltered("=== UseAllBeastsInInventory STARTED ===", 1);
        try
        {
            LogMessageFiltered("=== Starting Use All Beasts operation ===", 1);

            // Try to open inventory if not visible
            if (!InGameState.IngameUi.InventoryPanel.IsVisible)
            {
                LogMessageFiltered("Inventory panel not visible, attempting to open...", 1);
                Input.KeyDown(Settings.OpenInventoryKey.Value);
                await TaskUtils.NextFrame();
                Input.KeyUp(Settings.OpenInventoryKey.Value);
                await TaskUtils.NextFrame();
                
                // Wait a bit for the panel to open
                await Task.Delay(Settings.ClickDelay.Value);
                
                if (!InGameState.IngameUi.InventoryPanel.IsVisible)
                {
                    LogMessage("ERROR: Failed to open inventory panel", 2);
                    return false;
                }
                LogMessageFiltered("SUCCESS: Inventory panel opened", 1);
            }
            else
            {
                LogMessageFiltered("Inventory panel already visible", 1);
            }

            // Get all beasts from inventory
            var beastItems = GetBeastItemsFromInventory();
            LogMessageFiltered($"Found {beastItems.Count} beasts in inventory", 1);
            
            if (!beastItems.Any())
            {
                LogMessage("ERROR: No beasts found in inventory", 2);
                return false;
            }

            // Process each beast
            foreach (var beastItem in beastItems)
            {
                if (Input.GetKeyState(Settings.CancelKey.Value))
                {
                    LogMessageFiltered("Operation cancelled by user", 1);
                    return false;
                }

                LogMessageFiltered($"Processing beast: {GetItemBaseName(beastItem.Item)}", 1);
                
                if (!await ProcessBeastItem(beastItem))
                {
                    LogMessage($"ERROR: Failed to process beast {GetItemBaseName(beastItem.Item)}", 2);
                    continue; // Continue with next beast even if one fails
                }
            }

            LogMessageFiltered("Use All Beasts operation completed successfully", 1);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR during Use All Beasts operation: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> ReleaseFilteredBeasts()
    {
        LogMessageFiltered("=== ReleaseFilteredBeasts STARTED ===", 1);
        try
        {
            LogMessageFiltered("=== Starting Release Filtered Beasts operation ===", 1);

            // Try to open Bestiary tab if not visible
            var bestiaryTab = InGameState.IngameUi.ChallengesPanel?.TabContainer?.BestiaryTab?.CapturedBeastsTab;
            if (bestiaryTab == null || !bestiaryTab.IsVisible)
            {
                LogMessageFiltered("Bestiary tab not visible, attempting to open...", 1);
                
                // First try to open the challenges panel
                if (InGameState.IngameUi.ChallengesPanel?.IsVisible != true)
                {
                    Input.KeyDown(Settings.OpenBestiaryKey.Value);
                    await TaskUtils.NextFrame();
                    Input.KeyUp(Settings.OpenBestiaryKey.Value);
                    await TaskUtils.NextFrame();
                    await Task.Delay(Settings.ClickDelay.Value);
                }
                
                // Now try to navigate to the Bestiary tab
                var challengesPanel = InGameState.IngameUi.ChallengesPanel;
                if (challengesPanel?.IsVisible == true)
                {
                    // Try to click on the Bestiary tab
                    var bestiaryTabButton = challengesPanel.TabContainer?.BestiaryTab;
                    if (bestiaryTabButton != null && bestiaryTabButton.IsVisible)
                    {
                        await ClickOnElement(bestiaryTabButton);
                        await Task.Delay(Settings.ClickDelay.Value);
                    }
                }
                
                // Check again
                bestiaryTab = InGameState.IngameUi.ChallengesPanel?.TabContainer?.BestiaryTab?.CapturedBeastsTab;
                if (bestiaryTab == null || !bestiaryTab.IsVisible)
                {
                    LogMessage("ERROR: Failed to open Bestiary tab", 2);
                    return false;
                }
                LogMessageFiltered("SUCCESS: Bestiary tab opened", 1);
            }
            else
            {
                LogMessageFiltered("Bestiary tab already visible", 1);
            }

            // Hold Ctrl key for the entire process
            LogMessageFiltered("Holding Ctrl key for entire release process", 1);
            Input.KeyDown(Keys.LControlKey);

            try
            {
                // Keep releasing the first beast until none are left
                while (true)
                {
                    if (Input.GetKeyState(Settings.CancelKey.Value))
                    {
                        LogMessageFiltered("Operation cancelled by user", 1);
                        return false;
                    }

                    // Get the first visible beast
                    var firstBeast = GetFirstVisibleBeast();
                    if (firstBeast == null)
                    {
                        LogMessageFiltered("No more beasts to release", 1);
                        break;
                    }

                    LogMessageFiltered($"Releasing first beast: {GetBeastName(firstBeast)}", 1);
                    
                    if (!await ReleaseFirstBeast(firstBeast))
                    {
                        LogMessage($"ERROR: Failed to release first beast {GetBeastName(firstBeast)}", 2);
                        break; // Stop if we can't release the first beast
                    }

                    // Wait a moment for the beast to be released and list to update
                    await Task.Delay(Settings.ClickDelay.Value);
                }
            }
            finally
            {
                // Always release Ctrl key
                Input.KeyUp(Keys.LControlKey);
                LogMessageFiltered("Released Ctrl key", 1);
            }

            LogMessageFiltered("Release Filtered Beasts operation completed successfully", 1);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR during Release Filtered Beasts operation: {ex.Message}", 5);
            return false;
        }
    }

    private List<ServerInventory.InventSlotItem> GetCurrencyItemsFromInventory()
    {
        var inventoryItems = InGameState.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;
        var currencyItems = new List<ServerInventory.InventSlotItem>();

        int totalOrbs = 0;
        foreach (var item in inventoryItems)
        {
            if (IsCurrencyItem(item.Item))
            {
                currencyItems.Add(item);
                totalOrbs += item.Item.GetComponent<Stack>()?.Size ?? 1;
            }
        }

        LogMessageFiltered($"Found {currencyItems.Count} inventory slots with Bestiary Orbs", 1);
        LogMessageFiltered($"Total Bestiary Orbs: {totalOrbs}", 1);

        return currencyItems;
    }

    private List<ServerInventory.InventSlotItem> GetBeastItemsFromInventory()
    {
        var inventoryItems = InGameState.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;
        var beastItems = new List<ServerInventory.InventSlotItem>();

        int totalBeasts = 0;
        foreach (var item in inventoryItems)
        {
            if (IsBeastItem(item.Item))
            {
                beastItems.Add(item);
                totalBeasts += item.Item.GetComponent<Stack>()?.Size ?? 1;
            }
        }

        LogMessageFiltered($"Found {beastItems.Count} inventory slots with beasts", 1);
        LogMessageFiltered($"Total beasts: {totalBeasts}", 1);

        return beastItems;
    }

    private List<CapturedBeast> GetVisibleBeasts()
    {
        var visibleBeasts = new List<CapturedBeast>();
        
        try
        {
            var capturedBeastsTab = InGameState.IngameUi.ChallengesPanel?.TabContainer?.BestiaryTab?.CapturedBeastsTab;
            if (capturedBeastsTab == null || !capturedBeastsTab.IsVisible)
            {
                LogMessage("Captured beasts tab is not visible", 2);
                return visibleBeasts;
            }

            var beasts = capturedBeastsTab.CapturedBeasts;
            if (beasts == null || beasts.Count == 0)
            {
                LogMessage($"No captured beasts found. Count: {beasts?.Count ?? 0}", 2);
                return visibleBeasts;
            }

            LogMessageFiltered($"Found {beasts.Count} visible beasts to release", 1);
            
            // Return all visible beasts (user has already filtered them manually)
            return beasts.ToList();
        }
        catch (Exception ex)
        {
            LogMessage($"Error getting visible beasts: {ex.Message}", 5);
        }

        return visibleBeasts;
    }

    private CapturedBeast GetFirstVisibleBeast()
    {
        try
        {
            var capturedBeastsTab = InGameState.IngameUi.ChallengesPanel?.TabContainer?.BestiaryTab?.CapturedBeastsTab;
            if (capturedBeastsTab == null || !capturedBeastsTab.IsVisible)
            {
                LogMessage("Captured beasts tab is not visible", 2);
                return null;
            }

            var beasts = capturedBeastsTab.CapturedBeasts;
            if (beasts == null || beasts.Count == 0)
            {
                LogMessageFiltered("No captured beasts found", 1);
                return null;
            }

            // Get the first beast (index 0)
            var firstBeast = beasts[0];
            if (firstBeast != null && firstBeast.IsValid)
            {
                LogMessageFiltered("Found first visible beast", 1);
                return firstBeast;
            }
            else
            {
                LogMessage("First beast is null or invalid", 2);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error getting first visible beast: {ex.Message}", 5);
        }

        return null;
    }

    private string GetBeastName(CapturedBeast beast)
    {
        try
        {
            if (beast?.Children == null || beast.Children.Count < 2)
            {
                return "";
            }
            
            // Element 1 is the beast name
            var nameElement = beast.Children.ElementAtOrDefault(1);
            if (nameElement != null)
            {
                return nameElement.Text ?? "";
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error getting beast name: {ex.Message}", 5);
        }
        
        return "";
    }

    private bool IsCurrencyItem(Entity item)
    {
        if (item == null || !item.IsValid) return false;

        // Check if Bestiary Orb detection is enabled
        if (!Settings.CurrencyFilter.DetectBestiaryOrbs.Value)
            return false;

        // Check if item is a Bestiary Orb
        var baseName = GameController.Files.BaseItemTypes.Translate(item.Path)?.BaseName ?? "";
        
        if (Settings.CurrencyFilter.DebugItemNames.Value)
        {
            LogMessageFiltered($"Checking item: {baseName}", 1);
        }
        
        // Only match exact "Bestiary Orb", not "Imprinted Bestiary Orb"
        // This prevents processing captured beasts that are already in inventory
        return baseName.Equals("Bestiary Orb", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsBeastItem(Entity item)
    {
        if (item == null || !item.IsValid) return false;

        // Check if item is a captured beast (Imprinted Bestiary Orb)
        var baseName = GameController.Files.BaseItemTypes.Translate(item.Path)?.BaseName ?? "";
        
        if (Settings.CurrencyFilter.DebugItemNames.Value)
        {
            LogMessageFiltered($"Checking beast item: {baseName}", 1);
        }
        
        // Match "Imprinted Bestiary Orb" which are the captured beasts
        return baseName.Equals("Imprinted Bestiary Orb", StringComparison.OrdinalIgnoreCase);
    }

    private async SyncTask<bool> ProcessCurrencyItem(ServerInventory.InventSlotItem currencyItem)
    {
        try
        {
            LogMessageFiltered($"Processing Bestiary Orb: {GetItemBaseName(currencyItem.Item)}", 1);

            // Check for cancel key before starting
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user before processing orb", 1);
                return false;
            }

            // Step 1: Right-click the Bestiary Orb to pick it up
            LogMessageFiltered("Step 1: Right-clicking Bestiary Orb...", 1);
            if (!await RightClickItem(currencyItem))
            {
                LogMessage("ERROR: Failed to right-click Bestiary Orb", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Right-clicked Bestiary Orb", 1);

            // Check for cancel key
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user after right-click", 1);
                return false;
            }

            // Step 2: Wait for Bestiary Orb to be on cursor
            LogMessageFiltered("Step 2: Waiting for Bestiary Orb on cursor...", 1);
            if (!await WaitForItemOnCursor())
            {
                LogMessage("ERROR: Failed to get Bestiary Orb on cursor", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Bestiary Orb is now on cursor", 1);

            // Check for cancel key
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user after getting item on cursor", 1);
                return false;
            }

            // Step 3: Find target beast in the specified tab
            LogMessageFiltered("Step 3: Finding target beast...", 1);
            var targetBeast = FindTargetBeast();
            if (targetBeast == null)
            {
                LogMessage("ERROR: Target beast not found", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Target beast found", 1);

            // Step 4: Left-click on the target beast
            LogMessageFiltered("Step 4: Clicking on target beast...", 1);
            if (!await ClickOnElement(targetBeast))
            {
                LogMessage("ERROR: Failed to click on target beast", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Clicked on target beast", 1);

            // Step 5: Wait for the new item to appear on cursor
            LogMessageFiltered("Step 5: Waiting for new item on cursor...", 1);
            if (!await WaitForNewItemOnCursor())
            {
                LogMessage("ERROR: Failed to get new item on cursor after clicking beast", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: New item is now on cursor", 1);

            // Step 6: Find free inventory space and place the item
            LogMessageFiltered("Step 6: Finding free inventory space...", 1);
            var freeSpace = FindFreeInventorySpace();
            if (freeSpace == null)
            {
                LogMessage("ERROR: No free inventory space found", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Free inventory space found", 1);

            // Step 7: Click on free space to place the item
            LogMessageFiltered("Step 7: Placing item in free space...", 1);
            if (!await ClickOnElement(freeSpace))
            {
                LogMessage("ERROR: Failed to place item in free space", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Placed item in free space", 1);

            // Step 8: Wait for item to be placed
            LogMessageFiltered("Step 8: Confirming item placement...", 1);
            if (!await WaitForItemPlaced())
            {
                LogMessage("ERROR: Failed to confirm item placement", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Item placement confirmed", 1);

            LogMessageFiltered("SUCCESS: Processed Bestiary Orb", 1);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR processing Bestiary Orb: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> ProcessBeastItem(ServerInventory.InventSlotItem beastItem)
    {
        try
        {
            LogMessageFiltered($"Processing beast: {GetItemBaseName(beastItem.Item)}", 1);

            // Check for cancel key before starting
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user before processing beast", 1);
                return false;
            }

            // Step 1: Right-click the beast to use it (this adds it to bestiary and removes from inventory)
            LogMessageFiltered("Step 1: Right-clicking beast to use it...", 1);
            if (!await RightClickItem(beastItem))
            {
                LogMessage("ERROR: Failed to right-click beast", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Right-clicked beast - it has been used and added to bestiary", 1);

            // Step 2: Wait a moment for the beast to be processed
            LogMessageFiltered("Step 2: Waiting for beast processing...", 1);
            await Task.Delay(Settings.ClickDelay.Value);
            LogMessageFiltered("SUCCESS: Beast processing completed", 1);

            LogMessageFiltered("SUCCESS: Processed beast", 1);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR processing beast: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> ReleaseBeast(CapturedBeast beast)
    {
        try
        {
            LogMessageFiltered($"Releasing beast: {GetBeastName(beast)}", 1);

            // Check for cancel key before starting
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user before releasing beast", 1);
                return false;
            }

            // Get the release button (element 3)
            if (beast?.Children == null || beast.Children.Count < 4)
            {
                LogMessage("ERROR: Beast element doesn't have enough children for release button", 2);
                return false;
            }

            var releaseButton = beast.Children.ElementAtOrDefault(3);
            if (releaseButton == null || !releaseButton.IsValid)
            {
                LogMessage("ERROR: Release button not found or invalid", 2);
                return false;
            }

            // Step 1: Ctrl+Left-click the release button
            LogMessageFiltered("Step 1: Ctrl+Left-clicking release button...", 1);
            if (!await CtrlLeftClickElement(releaseButton))
            {
                LogMessage("ERROR: Failed to Ctrl+Left-click release button", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Ctrl+Left-clicked release button", 1);

            // Step 2: Wait a moment for the beast to be released
            LogMessageFiltered("Step 2: Waiting for beast release...", 1);
            await Task.Delay(Settings.ClickDelay.Value);
            LogMessageFiltered("SUCCESS: Beast release completed", 1);

            LogMessageFiltered("SUCCESS: Released beast", 1);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR releasing beast: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> ReleaseFirstBeast(CapturedBeast beast)
    {
        try
        {
            LogMessageFiltered($"Releasing first beast: {GetBeastName(beast)}", 1);

            // Check for cancel key before starting
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user before releasing beast", 1);
                return false;
            }

            // Get the release button (element 3)
            if (beast?.Children == null || beast.Children.Count < 4)
            {
                LogMessage("ERROR: Beast element doesn't have enough children for release button", 2);
                return false;
            }

            var releaseButton = beast.Children.ElementAtOrDefault(3);
            if (releaseButton == null || !releaseButton.IsValid)
            {
                LogMessage("ERROR: Release button not found or invalid", 2);
                return false;
            }

            // Step 1: Left-click the release button (Ctrl is already held)
            LogMessageFiltered("Step 1: Left-clicking release button (Ctrl already held)...", 1);
            if (!await LeftClickElement(releaseButton))
            {
                LogMessage("ERROR: Failed to Left-click release button", 2);
                return false;
            }
            LogMessageFiltered("SUCCESS: Left-clicked release button", 1);

            // Step 2: Wait a moment for the beast to be released
            LogMessageFiltered("Step 2: Waiting for beast release...", 1);
            await Task.Delay(Settings.ClickDelay.Value);
            LogMessageFiltered("SUCCESS: Beast release completed", 1);

            LogMessageFiltered("SUCCESS: Released first beast", 1);
            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"ERROR releasing first beast: {ex.Message}", 5);
            return false;
        }
    }

    private bool IsMouseInPosition(SharpDX.Vector2 pos)
    {
        var mousePos = new SharpDX.Vector2(InGameState.MousePosX, InGameState.MousePosY);
        return SharpDX.Vector2.Distance(mousePos, pos) < 2; // 2 pixels threshold
    }

    private async SyncTask<bool> RightClickItem(ServerInventory.InventSlotItem item)
    {
        try
        {
            var itemRect = item.GetClientRect();
            var clickPosition = itemRect.Center + WindowOffset;

            // Move mouse to item
            Input.SetCursorPos(clickPosition);
            // Wait until the mouse is actually over the item (or a short timeout)
            await TaskUtils.CheckEveryFrame(() => IsMouseInPosition(clickPosition), new CancellationTokenSource(100).Token);

            // Right-click the item
            Input.RightDown();
            await TaskUtils.NextFrame();
            Input.RightUp();

            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Error right-clicking item: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> WaitForItemOnCursor()
    {
        var maxWaitTime = Settings.WaitTimeout.Value;
        var startTime = Environment.TickCount;

        while (Environment.TickCount - startTime < maxWaitTime)
        {
            // Check for cancel key
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user during wait for item on cursor", 1);
                return false;
            }

            if (InGameState.IngameUi.Cursor.Action == MouseActionType.UseItem)
            {
                return true;
            }
            await TaskUtils.NextFrame();
        }

        return false;
    }

    private Element FindTargetElement()
    {
        // For Bestiary Orb workflow, we always need to find the target beast first
        return FindTargetBeast();
    }

    private Element FindStashTarget()
    {
        // For Bestiary Orb workflow, we don't need stash targets
        return null;
    }

    private Element FindCraftingBenchTarget()
    {
        // For Bestiary Orb workflow, we don't need crafting bench targets
        return null;
    }

    private Element FindFreeInventorySpace()
    {
        // Find a free space in the player's inventory
        var inventoryPanel = InGameState.IngameUi.InventoryPanel;
        if (!inventoryPanel.IsVisible)
            return null;

        var freePosition = FindFreeInventoryPosition();
        if (freePosition.HasValue)
        {
            // Convert inventory position to screen coordinates
            var screenPos = ConvertInventoryPositionToScreen((int)freePosition.Value.X, (int)freePosition.Value.Y);
            return CreateVirtualElement(screenPos);
        }

        return null;
    }

    private Element FindSpecificItem()
    {
        // For Bestiary Orb workflow, we don't need to find specific items
        return null;
    }

    private Vector2? FindFreeInventoryPosition()
    {
        const int inventoryWidth = 12;
        const int inventoryHeight = 5;
        
        var inventoryItems = InGameState.ServerData.PlayerInventories[0].Inventory.InventorySlotItems;
        var occupiedSlots = new bool[inventoryWidth, inventoryHeight];

        LogMessageFiltered($"Checking {inventoryItems.Count} items in inventory for free space", 1);

        // Mark occupied slots
        foreach (var item in inventoryItems)
        {
            var itemName = GetItemBaseName(item.Item);
                            LogMessageFiltered($"Item at ({item.PosX}, {item.PosY}) with size ({item.SizeX}, {item.SizeY}): {itemName}", 1);
            
            for (int x = item.PosX; x < item.PosX + item.SizeX && x < inventoryWidth; x++)
            {
                for (int y = item.PosY; y < item.PosY + item.SizeY && y < inventoryHeight; y++)
                {
                    if (x >= 0 && y >= 0)
                    {
                        occupiedSlots[x, y] = true;
                        LogMessageFiltered($"Marking slot ({x}, {y}) as occupied", 1);
                    }
                }
            }
        }

        // Find first free slot
        for (int y = 0; y < inventoryHeight; y++)
        {
            for (int x = 0; x < inventoryWidth; x++)
            {
                if (!occupiedSlots[x, y])
                {
                    LogMessageFiltered($"Found free slot at ({x}, {y})", 1);
                    return new Vector2(x, y);
                }
            }
        }

        LogMessage("No free slots found in inventory", 2);
        return null;
    }

    private Element FindFreeStashSpace()
    {
        // Similar logic for stash, but would need to be adapted based on stash type
        // For now, return null - this would need customization based on stash layout
        return null;
    }

    private void SwitchToStashTab(string tabName)
    {
        // Implementation to switch to a specific stash tab
        // This would need to be customized based on how stash tabs are accessed
        LogMessageFiltered($"Attempting to switch to stash tab: {tabName}", 1);
    }

    private SharpDX.Vector2 ConvertInventoryPositionToScreen(int posX, int posY)
    {
        // Convert inventory grid position to screen coordinates
        var inventoryPanel = InGameState.IngameUi.InventoryPanel;
        
        // Player inventory is at index 2, not 0 (based on other plugins)
        var playerInventory = inventoryPanel[2]; // Player inventory
        var panelRect = playerInventory.GetClientRect();
        
        LogMessageFiltered($"Inventory panel bounds: X={panelRect.X}, Y={panelRect.Y}, Width={panelRect.Width}, Height={panelRect.Height}", 1);
        
        // Calculate cell size based on actual inventory panel dimensions
        // Standard inventory is 12x5 grid
        const int gridWidth = 12;
        const int gridHeight = 5;
        
        var cellWidth = panelRect.Width / gridWidth;
        var cellHeight = panelRect.Height / gridHeight;
        
        LogMessageFiltered($"Calculated cell size: {cellWidth}x{cellHeight}", 1);
        
        // Calculate position within the inventory panel
        var x = panelRect.X + posX * cellWidth + cellWidth / 2;
        var y = panelRect.Y + posY * cellHeight + cellHeight / 2;
        
        LogMessageFiltered($"Converting inventory position ({posX}, {posY}) to screen position ({x}, {y})", 1);
        
        return new SharpDX.Vector2(x, y);
    }

    private Element CreateVirtualElement(SharpDX.Vector2 position)
    {
        // Create a virtual element for clicking at a specific position
        // This is a simplified approach - you might need a more sophisticated solution
        return new VirtualElement(position);
    }

    private bool IsTargetItem(NormalInventoryItem item)
    {
        // For Bestiary Orb workflow, we don't need to check for specific target items
        return false;
    }

    private bool IsTargetItem(ServerInventory.InventSlotItem item)
    {
        // For Bestiary Orb workflow, we don't need to check for specific target items
        return false;
    }

    private Element FindCraftingBenchElement()
    {
        // Implement logic to find specific crafting bench element
        // This would depend on what you're trying to interact with
        return null;
    }

    private async SyncTask<bool> ClickOnElement(Element element)
    {
        try
        {
            var elementRect = element.GetClientRect();
            var clickPosition = elementRect.Center + WindowOffset;

            // Move mouse to element
            Input.SetCursorPos(clickPosition);
            // Wait until the mouse is actually over the element (or a short timeout)
            await TaskUtils.CheckEveryFrame(() => IsMouseInPosition(clickPosition), new CancellationTokenSource(100).Token);

            // Left-click the element
            Input.LeftDown();
            await TaskUtils.NextFrame();
            Input.LeftUp();

            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Error clicking on element: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> CtrlLeftClickElement(Element element)
    {
        try
        {
            var elementRect = element.GetClientRect();
            var clickPosition = elementRect.Center + WindowOffset;

            // Move mouse to element
            Input.SetCursorPos(clickPosition);
            // Wait until the mouse is actually over the element (or a short timeout)
            await TaskUtils.CheckEveryFrame(() => IsMouseInPosition(clickPosition), new CancellationTokenSource(100).Token);

            // Hold Ctrl key
            Input.KeyDown(Keys.Control);
            await TaskUtils.NextFrame();
            
            // Left-click the element
            Input.LeftDown();
            await TaskUtils.NextFrame();
            Input.LeftUp();
            await TaskUtils.NextFrame();
            
            // Release Ctrl key
            Input.KeyUp(Keys.Control);

            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Error Ctrl+Left-clicking element: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> LeftClickElement(Element element)
    {
        try
        {
            var elementRect = element.GetClientRect();
            var clickPosition = elementRect.Center + WindowOffset;

            // Move mouse to element
            Input.SetCursorPos(clickPosition);
            // Wait until the mouse is actually over the element (or a short timeout)
            await TaskUtils.CheckEveryFrame(() => IsMouseInPosition(clickPosition), new CancellationTokenSource(100).Token);

            // Left-click the element (Ctrl is already held)
            Input.LeftDown();
            await TaskUtils.NextFrame();
            Input.LeftUp();

            return true;
        }
        catch (Exception ex)
        {
            LogMessage($"Error Left-clicking element: {ex.Message}", 5);
            return false;
        }
    }

    private async SyncTask<bool> WaitForItemPlaced()
    {
        var maxWaitTime = Settings.WaitTimeout.Value;
        var startTime = Environment.TickCount;
        var initialCursorAction = InGameState.IngameUi.Cursor.Action;
        
        LogMessageFiltered($"Waiting for item to be placed. Initial cursor action: {initialCursorAction}", 1);

        // Add a small delay after clicking to allow the game to process the action
        await TaskUtils.NextFrame();

        while (Environment.TickCount - startTime < maxWaitTime)
        {
            // Check for cancel key
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user during wait for item placement", 1);
                return false;
            }

            var currentCursorAction = InGameState.IngameUi.Cursor.Action;
            
            // Check if cursor is now free (item was placed)
            if (currentCursorAction == MouseActionType.Free)
            {
                LogMessageFiltered($"SUCCESS: Cursor is now free, item placed", 1);
                return true;
            }
            
            // Also check if the cursor action changed from what it was when we had the item
            if (currentCursorAction != initialCursorAction && 
                (currentCursorAction == MouseActionType.Free || currentCursorAction == MouseActionType.HoldItemForSell))
            {
                LogMessageFiltered($"SUCCESS: Cursor action changed to {currentCursorAction}, item likely placed", 1);
                return true;
            }
            
            // Additional check: verify no item on cursor
            var cursorItem = GetCursorItem();
            if (cursorItem == null)
            {
                LogMessageFiltered($"SUCCESS: No item found on cursor, item placed", 1);
                return true;
            }
            
            // Check if the item on cursor is different (meaning the original item was placed)
            var itemName = GetItemBaseName(cursorItem);
            if (itemName != "Imprinted Bestiary Orb" && itemName != "Bestiary Orb")
            {
                LogMessageFiltered($"SUCCESS: Different item on cursor ({itemName}), original item placed", 1);
                return true;
            }
            
            await TaskUtils.NextFrame();
        }

        LogMessage($"ERROR: Failed to confirm item placement. Final cursor action: {InGameState.IngameUi.Cursor.Action}", 2);
        return false;
    }

    private async SyncTask<bool> WaitForNewItemOnCursor()
    {
        var maxWaitTime = Settings.WaitTimeout.Value;
        var startTime = Environment.TickCount;
        var initialCursorAction = InGameState.IngameUi.Cursor.Action;
        
        LogMessageFiltered($"Waiting for new item on cursor. Initial cursor action: {initialCursorAction}", 1);

        while (Environment.TickCount - startTime < maxWaitTime)
        {
            // Check for cancel key
            if (Input.GetKeyState(Settings.CancelKey.Value))
            {
                LogMessageFiltered("Operation cancelled by user during wait for new item on cursor", 1);
                return false;
            }

            var currentCursorAction = InGameState.IngameUi.Cursor.Action;
            
            // Check if we have a new item on cursor (different from the Bestiary Orb)
            if (currentCursorAction == MouseActionType.UseItem || 
                currentCursorAction == MouseActionType.HoldItem ||
                currentCursorAction == MouseActionType.HoldItemForSell)
            {
                LogMessageFiltered($"Cursor action changed to: {currentCursorAction}", 1);
                
                // Additional check: verify it's not the same Bestiary Orb by checking if cursor action changed
                if (currentCursorAction != initialCursorAction)
                {
                    LogMessageFiltered($"SUCCESS: Cursor action changed from {initialCursorAction} to {currentCursorAction}", 1);
                    return true;
                }
            }
            
            // Also check if we have any item on cursor using the inventory method
            var cursorItem = GetCursorItem();
            if (cursorItem != null)
            {
                var itemName = GetItemBaseName(cursorItem);
                LogMessageFiltered($"Found item on cursor: {itemName}", 1);
                
                // If it's not a Bestiary Orb, we succeeded
                if (!IsCurrencyItem(cursorItem))
                {
                    LogMessageFiltered($"SUCCESS: Found non-currency item on cursor: {itemName}", 1);
                    return true;
                }
            }
            
            await TaskUtils.NextFrame();
        }

        LogMessage($"ERROR: Failed to get new item on cursor after clicking beast. Final cursor action: {InGameState.IngameUi.Cursor.Action}", 2);
        return false;
    }

    private Entity GetCursorItem()
    {
        try
        {
            // Get the item currently on the cursor
            var playerInventories = InGameState.ServerData?.PlayerInventories;
            if (playerInventories == null)
            {
                LogMessage("PlayerInventories is null", 2);
                return null;
            }
            
            // Check if we have the cursor inventory
            if (playerInventories.Count <= (int)InventorySlotE.Cursor1)
            {
                LogMessage($"PlayerInventories count ({playerInventories.Count}) is too small for Cursor1 index ({(int)InventorySlotE.Cursor1})", 2);
                return null;
            }
            
            var cursorInventory = playerInventories[(int)InventorySlotE.Cursor1];
            if (cursorInventory?.Inventory?.Items == null)
            {
                LogMessage("Cursor inventory or items is null", 2);
                return null;
            }
            
            if (cursorInventory.Inventory.Items.Count > 0)
            {
                var item = cursorInventory.Inventory.Items.FirstOrDefault();
                if (item != null && item.IsValid)
                {
                    LogMessageFiltered($"Found valid item on cursor: {GetItemBaseName(item)}", 1);
                    return item;
                }
            }
            
            LogMessageFiltered("No valid items found on cursor", 1);
            return null;
        }
        catch (Exception ex)
        {
            LogMessage($"Error getting cursor item: {ex.Message}", 5);
            return null;
        }
    }

    private Element FindTargetBeast()
    {
        LogMessageFiltered("Looking for target beast in Bestiary tab", 1);
        
        try
        {
            // Use the same approach as the working Beasts plugin
            var ingameUI = InGameState.IngameUi;
            if (ingameUI?.ChallengesPanel?.TabContainer?.BestiaryTab?.CapturedBeastsTab == null)
            {
                LogMessage("Bestiary tab path not found", 2);
                return null;
            }

            var capturedBeastsTab = ingameUI.ChallengesPanel.TabContainer.BestiaryTab.CapturedBeastsTab;
            if (!capturedBeastsTab.IsVisible)
            {
                LogMessage("Captured beasts tab is not visible", 2);
                return null;
            }

            var beasts = capturedBeastsTab.CapturedBeasts;
            if (beasts == null || beasts.Count == 0)
            {
                LogMessage($"No captured beasts found. Count: {beasts?.Count ?? 0}", 2);
                return null;
            }

            LogMessageFiltered($"Found {beasts.Count} captured beasts", 1);
            
            // Get the first beast (index 0)
            var targetBeast = beasts[0];
            if (targetBeast != null && targetBeast.IsValid)
            {
                LogMessageFiltered("Found target beast at index 0", 1);
                return targetBeast;
            }
            else
            {
                LogMessage("Target beast at index 0 is null or invalid", 2);
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error finding target beast: {ex.Message}", 5);
        }

        return null;
    }

    private string GetItemBaseName(Entity item)
    {
        if (item == null) return "";
        return GameController.Files.BaseItemTypes.Translate(item.Path)?.BaseName ?? "";
    }

    private void LogMessageFiltered(string message, int level = 1)
    {
        // Only log if debug is enabled or it's an error/warning (level 2+)
        if (Settings.ShowDebugInfo.Value || level >= 2)
        {
            LogMessage($"[BeastSorter] {message}", level);
        }
    }

    private void DrawDebugInfo()
    {
        if (!Settings.ShowDebugInfo.Value) return;
        
        var operationStatus = _currentOperation?.GetAwaiter().IsCompleted == false ? "Running" : "Idle";
        var debugText = $"BeastSorter Status: {operationStatus}";
        Graphics.DrawText(debugText, new Vector2(10, 10), Color.White);
        
        var hotkeyText = $"Hotkeys: F7=Use Orbs, F8=Cancel, F9=Use All Beasts, F10=Release Filtered";
        Graphics.DrawText(hotkeyText, new Vector2(10, 30), Color.White);
    }
}

// Virtual element class for clicking at specific positions
public class VirtualElement : Element
{
    private readonly SharpDX.Vector2 _position;

    public VirtualElement(SharpDX.Vector2 position)
    {
        _position = position;
    }

    public override SharpDX.RectangleF GetClientRect()
    {
        return new SharpDX.RectangleF(_position.X - 10, _position.Y - 10, 20, 20);
    }
} 