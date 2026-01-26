<Query Kind="Program">
  <NuGetReference>Xecrets.Slip39</NuGetReference>
  <Namespace>Xecrets.Slip39</Namespace>
  <Namespace>System.Security.Cryptography</Namespace>
</Query>

void Main()
{
	// Initialize Shamir's Secret Sharing with strong random number generation
	var sss = new ShamirsSecretSharing(new StrongRandom());

	"╔═══════════════════════════════════════════════════════════════════════╗".Dump();
	"║ Complete Wallet Backup: Seed + BOTH Passphrases via SLIP-39          ║".Dump();
	"╚═══════════════════════════════════════════════════════════════════════╝".Dump();
	"".Dump();

	/**
	 * Real-world use case: Backing up a BIP-39 wallet seed phrase using SLIP-39
	 *
	 * 4 groups of SLIP-39 shares:
	 * - Two for Alice (backup #1 and backup #2)
	 * - One for friends
	 * - One for family members
	 *
	 * Two of these group shares are required to reconstruct the BIP-39 seed phrase.
	 */

	// START: Your wallet credentials
	var bip39SeedPhrase = "wrist eye before pact minute dilemma other entry escape vapor imitate history";
	var bip39Passphrase = "MyWalletPass123";   // Your wallet's BIP-39 passphrase (the "25th word")
	var slip39Passphrase = "SLIP39EncryptKey";  // For encrypting shares (NOT backed up, just remembered!)

	// EFFICIENT STRATEGY: Store BIP-39 as compact BYTES, not text
	// BIP-39 seed: 12 words → only 16 bytes (128 bits)!
	var bip39SeedBytes = bip39SeedPhrase.FromBip39();  // 16 bytes
	var bip39PassBytes = System.Text.Encoding.UTF8.GetBytes(bip39Passphrase);  // ~15 bytes

	// Combine: [16 bytes seed][passphrase bytes]
	// We know first 16 bytes = BIP-39 seed, rest = BIP-39 passphrase
	var masterSecret = bip39SeedBytes.Concat(bip39PassBytes).ToArray();

	// Pad to even byte length if needed (SLIP-39 requirement)
	if (masterSecret.Length % 2 != 0)
	{
		masterSecret = masterSecret.Concat(new byte[] { 0xFF }).ToArray();
	}

	var masterSecretHex = Convert.ToHexString(masterSecret);

	// Display what's being backed up
	new {
		BIP39_SeedPhrase = bip39SeedPhrase,
		BIP39_SeedBytes = $"{bip39SeedBytes.Length} bytes (compact!)",
		BIP39_SeedHex = Convert.ToHexString(bip39SeedBytes),
		BIP39_Passphrase = string.IsNullOrEmpty(bip39Passphrase) ? "(none)" : bip39Passphrase,
		BIP39_PassBytes = $"{bip39PassBytes.Length} bytes",
		SLIP39_Passphrase = slip39Passphrase,
		SLIP39_PassIncluded = "❌ NO - Must remember separately (security!)",
		TotalMasterSecretBytes = masterSecret.Length,
		Efficiency = $"✅ COMPACT: {bip39SeedBytes.Length} bytes (seed) + {bip39PassBytes.Length} bytes (pass) vs ~100+ if stored as text",
		MasterSecretHex = masterSecretHex
	}.Dump("📱 Input: Efficient Wallet Backup (Binary Format)");

	"".Dump();

	// Generate shares with the 4-group configuration
	var shares = sss.GenerateShares(
		extendable: true,
		iterationExponent: 0, // Use 2-4 for production
		groupThreshold: 2,    // Need 2 groups to reconstruct
		groups: [
			// Alice's group shares. 1 is enough to reconstruct a group share,
			// therefore she needs at least two group shares to be reconstructed.
			new Xecrets.Slip39.Group(ShareThreshold: 1, ShareCount: 1), // Alice backup #1
			new Xecrets.Slip39.Group(ShareThreshold: 1, ShareCount: 1), // Alice backup #2

			// 3 of 5 Friends' shares are required to reconstruct this group share
			new Xecrets.Slip39.Group(ShareThreshold: 3, ShareCount: 5),

			// 2 of 6 Family's shares are required to reconstruct this group share
			new Xecrets.Slip39.Group(ShareThreshold: 2, ShareCount: 6),
		],
		passphrase: slip39Passphrase,
		masterSecret: masterSecret
	);

	// Display generated shares summary
	"Your BIP-39 seed phrase has been split into SLIP-39 shares:".Dump();

	new {
		OriginalSeed = "Protected (not stored in shares directly)",
		TotalGroups = shares.Length,
		Group0_Alice1 = new { Shares = shares[0].Length, Threshold = "1-of-1", Purpose = "Alice's safe #1" },
		Group1_Alice2 = new { Shares = shares[1].Length, Threshold = "1-of-1", Purpose = "Alice's safe #2" },
		Group2_Friends = new { Shares = shares[2].Length, Threshold = "3-of-5", Purpose = "Distributed to 5 friends" },
		Group3_Family = new { Shares = shares[3].Length, Threshold = "2-of-6", Purpose = "Distributed to 6 family members" },
		RecoveryRequirement = "Any 2 groups can recover the wallet"
	}.Dump("🔐 SLIP-39 Shares Generated");

	// Display Alice's shares (she can use these alone)
	new {
		Group0_Share = shares[0][0].ToString(),
		Group1_Share = shares[1][0].ToString()
	}.Dump("Alice's Shares");

	// Display some Friends shares (need 3 of these)
	shares[2].Take(3).Select((s, i) => new {
		Index = i,
		Share = s.ToString()
	}).Dump("Friends' Shares (showing 3 of 5)");

	// Display some Family shares (need 2 of these)
	shares[3].Take(2).Select((s, i) => new {
		Index = i,
		Share = s.ToString()
	}).Dump("Family Shares (showing 2 of 6)");

	"".Dump();
	"─────────────────────────────────────────────────────────────────────────".Dump();
	"RECOVERY SCENARIOS".Dump();
	"─────────────────────────────────────────────────────────────────────────".Dump();
	"".Dump();

	// Scenario 1: Alice uses both her backup shares
	"Scenario 1: Alice uses both her backup shares".Dump();
	var aliceRecovery = sss.CombineShares(
		[shares[0][0], shares[1][0]],
		slip39Passphrase
	);

	// Recover the combined bytes and split back into components
	var recoveredBytes = aliceRecovery.Secret.TakeWhile(b => b != 0xFF).ToArray();

	// First 16 bytes = BIP-39 seed, rest = BIP-39 passphrase
	var recoveredBip39SeedBytes = recoveredBytes.Take(16).ToArray();
	var recoveredBip39PassBytes = recoveredBytes.Skip(16).ToArray();

	var recoveredBip39Seed = recoveredBip39SeedBytes.ToBip39();
	var recoveredBip39Pass = System.Text.Encoding.UTF8.GetString(recoveredBip39PassBytes);

	new {
		RecoveredSeedBytes = Convert.ToHexString(recoveredBip39SeedBytes),
		RecoveredPassBytes = Convert.ToHexString(recoveredBip39PassBytes),
		BIP39_SeedPhrase = recoveredBip39Seed,
		BIP39_Passphrase = string.IsNullOrEmpty(recoveredBip39Pass) ? "(none)" : recoveredBip39Pass,
		SeedMatches = recoveredBip39Seed == bip39SeedPhrase,
		PassMatches = recoveredBip39Pass == bip39Passphrase,
		ReadyForWalletImport = "✅ Yes - import seed + BIP-39 passphrase into wallet",
		ImportInstructions = new[] {
			"1. Open your wallet app",
			$"2. Import seed: {recoveredBip39Seed}",
			$"3. Enter BIP-39 passphrase: {recoveredBip39Pass}",
			"4. Wallet restored!"
		}
	}.Dump("Alice's Recovery → Wallet Credentials Recovered!");

	"".Dump();

	// Scenario 2: Friends (3) + Family (2) collaborate
	"Scenario 2: 3 Friends + 2 Family members collaborate".Dump();
	var collaborativeRecovery = sss.CombineShares(
		[
			shares[2][0], shares[2][1], shares[2][2], // 3 friends
			shares[3][0], shares[3][1]                // 2 family
		],
		slip39Passphrase
	);

	// Recover the combined bytes and split back into components
	var collabRecoveredBytes = collaborativeRecovery.Secret.TakeWhile(b => b != 0xFF).ToArray();

	// First 16 bytes = BIP-39 seed, rest = BIP-39 passphrase
	var collabRecoveredSeedBytes = collabRecoveredBytes.Take(16).ToArray();
	var collabRecoveredPassBytes = collabRecoveredBytes.Skip(16).ToArray();

	var collabRecoveredSeed = collabRecoveredSeedBytes.ToBip39();
	var collabRecoveredPass = System.Text.Encoding.UTF8.GetString(collabRecoveredPassBytes);

	new {
		BIP39_SeedPhrase = collabRecoveredSeed,
		BIP39_Passphrase = string.IsNullOrEmpty(collabRecoveredPass) ? "(none)" : collabRecoveredPass,
		SeedMatches = collabRecoveredSeed == bip39SeedPhrase,
		PassMatches = collabRecoveredPass == bip39Passphrase,
		ReadyForWalletImport = "✅ Yes - import seed + BIP-39 passphrase into wallet"
	}.Dump("Collaborative Recovery → Wallet Credentials Recovered!");

	"".Dump();

	// Scenario 3: Insufficient shares (only Friends group)
	"Scenario 3: Insufficient shares - only Friends group (need 2 groups)".Dump();
	var insufficientRecovery = sss.CombineShares(
		[shares[2][0], shares[2][1], shares[2][2]], // Only 1 group
		slip39Passphrase
	);

	new {
		SecretRecovered = insufficientRecovery.Secret.Length > 0,
		GroupsProvided = insufficientRecovery.ShareGroups.Length,
		GroupsRequired = 2,
		Message = "Insufficient groups - need at least 2 groups"
	}.Dump("Insufficient Recovery");

	"".Dump();
	"─────────────────────────────────────────────────────────────────────────".Dump();
	"COMPLETE WORKFLOW SUMMARY".Dump();
	"─────────────────────────────────────────────────────────────────────────".Dump();
	"".Dump();

	new {
		Step1_Input = "BIP-39 seed phrase + BIP-39 passphrase",
		Step2_Convert = "BIP-39 seed → 16 bytes (compact!) + passphrase → UTF-8 bytes",
		Step3_Combine = "Concatenate: [16 bytes seed][passphrase bytes]",
		Step4_Split = "Combined bytes → SLIP-39 shares (encrypted with SLIP-39 passphrase)",
		Step5_Distribute = "Give shares to Alice, Friends, Family",
		Step6_Later_Collect = "Collect required shares (2 groups minimum)",
		Step7_Combine = "SLIP-39 shares + SLIP-39 passphrase → combined bytes",
		Step8_Extract = "First 16 bytes → BIP-39 seed, rest → BIP-39 passphrase",
		Step9_Convert = "16 bytes → BIP-39 words using .ToBip39()",
		Step10_Import = "Import seed + passphrase into wallet → DONE!",
		Efficiency = "🚀 Only ~30-35 bytes total vs ~100+ if stored as text!"
	}.Dump("🔄 Efficient Wallet Backup & Recovery");

	"".Dump();
	"─────────────────────────────────────────────────────────────────────────".Dump();
	"⚠️ CRITICAL: UNDERSTANDING THE TWO PASSPHRASES".Dump();
	"─────────────────────────────────────────────────────────────────────────".Dump();
	"".Dump();

	new {
		Component_1 = "BIP-39 Seed Phrase",
		Purpose_1 = "Your 12-word wallet seed",
		CurrentValue_1 = bip39SeedPhrase,
		StoredAs_1 = "16 bytes (compact binary)",
		BackedUp_1 = "✅ YES - In the SLIP-39 shares",
		WhenNeeded_1 = "Wallet import",

		Component_2 = "BIP-39 Passphrase ('25th word')",
		Purpose_2 = "Used by wallet for key derivation",
		CurrentValue_2 = string.IsNullOrEmpty(bip39Passphrase) ? "(none)" : bip39Passphrase,
		StoredAs_2 = $"{bip39PassBytes.Length} bytes (UTF-8 text)",
		BackedUp_2 = "✅ YES - In the SLIP-39 shares (appended after seed)",
		WhenNeeded_2 = "Wallet import",

		Component_3 = "SLIP-39 Passphrase",
		Purpose_3 = "Encrypts the SLIP-39 shares themselves (NOT part of wallet)",
		CurrentValue_3 = slip39Passphrase,
		StoredAs_3 = "N/A - not stored",
		BackedUp_3 = "❌ NO - You MUST remember this!",
		WhenNeeded_3 = "To decrypt/combine the SLIP-39 shares",

		Format = "[16 bytes seed][passphrase bytes]",
		SecurityNote = "⚠️ SLIP-39 passphrase protects shares - if backed up, anyone with shares has everything!"
	}.Dump("🔑 What's Backed Up vs What You Must Remember");

	"".Dump();

	new {
		InTheShares = "✅ BIP-39 seed (16 bytes) + BIP-39 passphrase (~15 bytes)",
		MustRemember = "❌ SLIP-39 passphrase (NOT backed up for security!)",
		Format = "[16 bytes BIP-39 seed][remaining bytes = BIP-39 passphrase]",
		ToRecover = new[] {
			"1. Collect required SLIP-39 shares (2 groups)",
			"2. Remember your SLIP-39 passphrase",
			"3. Combine shares with SLIP-39 passphrase → get combined bytes",
			"4. First 16 bytes → convert to BIP-39 seed using .ToBip39()",
			"5. Remaining bytes → convert to UTF-8 for BIP-39 passphrase",
			"6. Import both into wallet → DONE!"
		},
		Efficiency = "Only ~32 bytes total vs ~100+ if text",
		WhyNotBackupSlip39Pass = "Security: Stolen shares still need SLIP-39 passphrase to decrypt"
	}.Dump("📋 Recovery Process - What You Need");

	"".Dump();
	"╔═══════════════════════════════════════════════════════════════════════╗".Dump();
	"║ Demo Complete - Complete Wallet Backup with SLIP-39                  ║".Dump();
	"║ ✅ Backed up: BIP-39 seed + BIP-39 passphrase                         ║".Dump();
	"║ ⚠️  Must remember: SLIP-39 passphrase (protects the shares)           ║".Dump();
	"╚═══════════════════════════════════════════════════════════════════════╝".Dump();
}
