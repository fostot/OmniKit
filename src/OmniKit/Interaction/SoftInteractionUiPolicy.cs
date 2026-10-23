namespace OmniKit.Interaction;

/// <summary>Only passive player bags may retain native game focus during an interaction press.</summary>
internal static class SoftInteractionUiPolicy
{
    public static string? PauseReason(bool textInputActive, bool worldInteractionActive,
        bool focusedAddonVisible, string? focusedAddon)
    {
        if (textInputActive)
            return "Interaction paused while game text input is active.";
        if (worldInteractionActive)
            return "Interaction paused during a world interaction; camera detection remains active.";
        // Exact names from the installed AddonInventory / Large / Expansion / Grid
        // attributes. Retainer bags, saddlebags, context menus and unknown UI stay blocked.
        if (focusedAddonVisible && focusedAddon is not ("Inventory" or "InventoryLarge" or "InventoryExpansion"
            or "InventoryGrid" or "InventoryGrid0" or "InventoryGrid1" or "InventoryGrid0E"
            or "InventoryGrid1E" or "InventoryGrid2E" or "InventoryGrid3E"))
            return "Interaction paused while a game menu is focused; camera detection remains active.";
        return null;
    }
}
