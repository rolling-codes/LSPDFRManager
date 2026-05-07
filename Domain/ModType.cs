namespace LSPDFRManager.Domain;

/// <summary>Broad category of a GTA V / LSPDFR mod.</summary>
public enum ModType
{
    /// <summary>Type could not be determined.</summary>
    Unknown,
    /// <summary>Add-on vehicle DLC pack (dlcpacks/).</summary>
    VehicleDlc,
    /// <summary>Vehicle texture/model replacement.</summary>
    VehicleReplace,
    /// <summary>LSPDFR plugin DLL (plugins/lspdfr/).</summary>
    LspdfrPlugin,
    /// <summary>ASI mod or trainer.</summary>
    AsiMod,
    /// <summary>ScriptHookV or SHVDN script (.cs/.vb/.lua).</summary>
    Script,
    /// <summary>EUP clothing / ped pack.</summary>
    Eup,
    /// <summary>Map add-on or MLO interior.</summary>
    Map,
    /// <summary>Audio / siren sound pack.</summary>
    Sound,
    /// <summary>Does not match any known pattern.</summary>
    Misc,
}
