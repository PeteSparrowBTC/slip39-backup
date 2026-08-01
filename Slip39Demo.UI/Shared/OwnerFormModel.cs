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
