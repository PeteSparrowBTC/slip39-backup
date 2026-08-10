namespace Slip39Demo.UI.Shared;

// Mutable view-models used by Blazor @bind. NOT domain types — these never
// leave the UI layer; the Owner page projects them into immutable
// PayloadV1_1 / GroupConfig records when the user clicks Generate.
public sealed class OwnerFormModel
{
    public string? Label { get; set; } = "Main wallet";
    public string? TopLevelSeedWords { get; set; }
    public string? Descriptor { get; set; }
    public int GroupThreshold { get; set; } = 1;

    // The optional backup key, transcribed from a tool that derived it from physical
    // dice (see Slip39Demo.Core.Slip39.BackupKeyEntry). Both empty is the default and
    // means Generate creates the key with RandomNumberGenerator, exactly as before.
    // Anything typed here must survive validation or generation fails: there is
    // deliberately no fallback from a supplied key to the generator.
    public string? BackupKeyHex { get; set; }

    public string? BackupKeyCheckCode { get; set; }
    public List<CosignerVm> Cosigners { get; set; } = [new CosignerVm { Id = "main", DerivationPath = "m/84'/0'/0'" }];
    public List<ShareGroupVm> Groups { get; set; } = [new ShareGroupVm { Name = "only", Threshold = 3, Count = 5 }];
}

public sealed class CosignerVm
{
    public string Id { get; set; } = "";
    public string WalletType { get; set; } = "bip39";
    public string? Passphrase { get; set; }
    public string DerivationPath { get; set; } = "m/84'/0'/0'";
    public string? SeedWords { get; set; }
}

public sealed class ShareGroupVm
{
    public string Name { get; set; } = "";
    public int Threshold { get; set; } = 2;
    public int Count { get; set; } = 3;
}
