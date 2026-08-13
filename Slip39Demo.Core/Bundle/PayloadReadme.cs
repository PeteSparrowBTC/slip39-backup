namespace Slip39Demo.Core.Bundle;

// Plain-text note that ships inside the payload/ folder of the output bundle.
// Reminds the OWNER that the ciphertext is theirs to safeguard: it is NOT
// distributed to share-holders. Static text, no parameters.
public static class PayloadReadme
{
    public const string Text =
        "IMPORTANT: READ FIRST\n" +
        "================================================================\n" +
        "\n" +
        "These files belong to YOU, the wallet OWNER. They must NOT be\n" +
        "distributed to your share-holders.\n" +
        "\n" +
        "What is in this folder:\n" +
        "  - payload.age.gpg.asc  the secret. One file, plain text, safe to\n" +
        "                         paste into a password manager or to print.\n" +
        "\n" +
        "  It has two locks, one inside the other: age on the inside, OpenPGP\n" +
        "  (AES-256) on the outside. Both open with the same key, and your\n" +
        "  SLIP-39 shares rebuild that key. Two locks means a future weakness\n" +
        "  in one of the two formats still leaves the other standing.\n" +
        "\n" +
        "  Earlier versions also shipped payload.age and payload.age.txt, the\n" +
        "  inner file on its own. Those are gone deliberately: keeping them\n" +
        "  beside this file let anyone who found the folder open the weaker one\n" +
        "  and ignore the outer lock entirely.\n" +
        "\n" +
        "  - VERIFY-THIS-BACKUP.txt   how to prove this blob decrypts using\n" +
        "                         other people's tools, not this one\n" +
        "\n" +
        "DO THIS FIRST:\n" +
        "  Follow VERIFY-THIS-BACKUP.txt before you distribute any share,\n" +
        "  and before you shut the offline machine down. The tool checked\n" +
        "  itself using software it shipped, which cannot catch a tool that\n" +
        "  is wrong the same way twice. Twenty minutes now, while every\n" +
        "  share is still in front of you, beats an expedition later.\n" +
        "\n" +
        "Where to put it:\n" +
        "  - PRIMARY:   a dedicated password manager entry (Bitwarden /\n" +
        "               Vaultwarden) with Emergency Access configured for\n" +
        "               your executor.\n" +
        "  - BACKUP A:  encrypted USB in your home safe (during-life fallback).\n" +
        "  - BACKUP B:  printed paper in your home safe (offline fallback).\n" +
        "\n" +
        "What it does:\n" +
        "  Decrypts to a plain-text wallet payload (seed words, optional\n" +
        "  passphrase, derivation path, label). Useless without\n" +
        "  threshold-many SLIP-39 shares, and the shares are useless\n" +
        "  without this file.\n" +
        "\n" +
        "See the per-share README.txt files for the executor recovery\n" +
        "procedure.\n";
}
