// BRAINLESS Protocol: Full Test Harness (Reusable Edition)
// UNINTELLIGENCE License
// Director: Julian Cassin | ZOSCII Foundation
// Use existing ROM or define if missing (recommended: in production use a good entropy 64kb ROM)

var rom = rom || "ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890: ,.ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890: ,.ABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890: ,.";

// --- CORE ZOSCII ---

encode = (r, m) => [...m].map(c => {
 let indices = [...r].map((b, i) => b === c ? i : []).flat();
 return indices.sort(() => Math.random() - 0.5)[0];
});

decode = (r, a) => a.map(addr => r[addr]).join('');

// --- BRAINLESS ENCRYPTION (The Ouroboros Chain) ---

encrypt = (r, m) => {
 let a = encode(r, m);

 // Reverse XOR Chain
 for (let i = a.length - 2; i >= 0; i--) {
 a[i] = a[i] ^ a[i + 1];

 }

 // The Ouroboros Step: Tail XOR'd against Head
 a[a.length - 1] = a[a.length - 1] ^ a[0];

 return a;
};

decrypt = (r, a) => {
 let workingArray = [...a];

 // Reverse the Ouroboros Step
 workingArray[workingArray.length - 1] = workingArray[workingArray.length - 1] ^ workingArray[0];

 // Forward XOR Chain Restoration
 for (let i = 0; i < workingArray.length - 1; i++) {
 workingArray[i] = workingArray[i] ^ workingArray[i + 1];
 }

 return decode(r, workingArray);
};

// --- API CHECK ---

// If api isn't defined by the shell, we provide a fallback

if (typeof api === 'undefined') {
 var api = { print: (text) => console.log(text) };
}

// --- EXECUTION ---

message = "THE DOORS HAVE BEEN REMOVED: OUROBOROS ACTIVE.";
api.print("====================================================");
api.print("BRAINLESS PROTOCOL: REUSABLE TEST HARNESS");
api.print("====================================================");

// Test 1: Original ZOSCII

api.print("\n--- TEST 1: ORIGINAL ZOSCII MAPPING ---");
zEncoded = encode(rom, message);
api.print("Addresses: " + zEncoded.join(', '));
api.print("Decoded: " + decode(rom, zEncoded));

// Test 2: Ouroboros Encryption

api.print("\n--- TEST 2: BRAINLESS OUROBOROS ENCRYPTION ---");
zEncrypted = encrypt(rom, message);
api.print("Ciphertext (Hex): " + zEncrypted.map(x => x.toString(16).padStart(2, '0')).join(' '));
api.print("Decrypted: " + decrypt(rom, zEncrypted));
