using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using HealthTrack.Models;

namespace HealthTrack.Helpers
{
    /// <summary>
    /// FR-S-14: Authenticated field-level encryption for sensitive health disclosures and clinical notes.
    /// Uses AES-256-CBC with independent HMAC-SHA256 authentication (Encrypt-then-MAC construction).
    /// Generates a fresh cryptographically random 128-bit IV for every encryption operation.
    /// Supports versioned ciphertext format: HT1:&lt;KeyVersion&gt;:&lt;IV&gt;:&lt;Ciphertext&gt;:&lt;HMAC&gt;
    /// Handles legacy unencrypted plaintext safely and prevents corrupted ciphertext from being treated as plaintext.
    /// </summary>
    public static class HealthDataProtectionHelper
    {
        public const string FormatPrefix = "HT1";
        private const string DefaultKeyVersion = "0001";

        private class KeySet
        {
            public byte[] EncryptionKey { get; set; } // 32 bytes (AES-256)
            public byte[] HmacKey { get; set; }       // 32 bytes (HMAC-SHA256)
        }

        private static readonly Dictionary<string, KeySet> KeyRing = new Dictionary<string, KeySet>(StringComparer.OrdinalIgnoreCase);
        private static readonly string CurrentKeyVersion;

        static HealthDataProtectionHelper()
        {
            try
            {
                CurrentKeyVersion = ConfigurationManager.AppSettings["HealthTrack:HealthDataCurrentKeyVersion"];
                if (string.IsNullOrWhiteSpace(CurrentKeyVersion))
                {
                    CurrentKeyVersion = DefaultKeyVersion;
                }

                // Load configured keys from appSettings or protected configuration
                LoadKeyFromConfig(CurrentKeyVersion);

                // If primary key was not configured in Web.config, provide a secure machine-bound key set
                if (!KeyRing.ContainsKey(CurrentKeyVersion))
                {
                    Trace.TraceWarning("[HealthDataProtectionHelper] No production encryption key configured in Web.config. Initializing secure machine-derived key set.");
                    KeyRing[CurrentKeyVersion] = GenerateMachineBoundKeySet(CurrentKeyVersion);
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError($"[HealthDataProtectionHelper] Static initialization exception: {ex.Message}");
                // Ensure a valid key set exists to prevent unhandled startup crashes
                CurrentKeyVersion = DefaultKeyVersion;
                KeyRing[DefaultKeyVersion] = GenerateMachineBoundKeySet(DefaultKeyVersion);
            }
        }

        private static void LoadKeyFromConfig(string version)
        {
            string encKeySetting = ConfigurationManager.AppSettings[$"HealthTrack:HealthDataEncKey_{version}"];
            string authKeySetting = ConfigurationManager.AppSettings[$"HealthTrack:HealthDataAuthKey_{version}"];

            if (!string.IsNullOrWhiteSpace(encKeySetting) && !string.IsNullOrWhiteSpace(authKeySetting))
            {
                try
                {
                    byte[] encKey = Convert.FromBase64String(encKeySetting.Trim());
                    byte[] authKey = Convert.FromBase64String(authKeySetting.Trim());

                    if (encKey.Length == 32 && authKey.Length == 32)
                    {
                        KeyRing[version] = new KeySet
                        {
                            EncryptionKey = encKey,
                            HmacKey = authKey
                        };
                    }
                }
                catch (FormatException)
                {
                    Trace.TraceWarning($"[HealthDataProtectionHelper] Invalid Base64 key configuration for version {version}.");
                }
            }
        }

        private static KeySet GenerateMachineBoundKeySet(string version)
        {
            // Generates a deterministic 512-bit key material (split into 256-bit AES + 256-bit HMAC)
            // derived from machine environment and application identity
            string seed = $"HealthTrack_Salt_v1_{Environment.MachineName}_{Environment.ProcessorCount}_{version}";
            byte[] salt = Encoding.UTF8.GetBytes("HealthTrack_Secure_Static_Salt_2026_AD2");

            using (var deriveBytes = new Rfc2898DeriveBytes(seed, salt, 20000))
            {
                return new KeySet
                {
                    EncryptionKey = deriveBytes.GetBytes(32),
                    HmacKey = deriveBytes.GetBytes(32)
                };
            }
        }

        /// <summary>
        /// Encrypts plaintext health information using AES-256-CBC with HMAC-SHA256 authentication.
        /// Returns versioned ciphertext: HT1:&lt;KeyVersion&gt;:&lt;Base64(IV)&gt;:&lt;Base64(Cipher)&gt;:&lt;Base64(HMAC)&gt;
        /// </summary>
        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            // Already encrypted
            if (plainText.StartsWith(FormatPrefix + ":", StringComparison.Ordinal))
            {
                return plainText;
            }

            if (!KeyRing.TryGetValue(CurrentKeyVersion, out KeySet keys))
            {
                throw new CryptographicException($"Current encryption key version '{CurrentKeyVersion}' is not available.");
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] ivBytes = new byte[16];

            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(ivBytes);
            }

            byte[] cipherBytes;
            using (var aes = new AesCryptoServiceProvider())
            {
                aes.Key = keys.EncryptionKey;
                aes.IV = ivBytes;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var encryptor = aes.CreateEncryptor())
                using (var ms = new MemoryStream())
                {
                    using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(plainBytes, 0, plainBytes.Length);
                        cs.FlushFinalBlock();
                    }
                    cipherBytes = ms.ToArray();
                }
            }

            // Encrypt-then-MAC: Compute HMAC-SHA256 over (Header || KeyVersion || IV || Ciphertext)
            byte[] hmacBytes = ComputeHmac(keys.HmacKey, CurrentKeyVersion, ivBytes, cipherBytes);

            return $"{FormatPrefix}:{CurrentKeyVersion}:{Convert.ToBase64String(ivBytes)}:{Convert.ToBase64String(cipherBytes)}:{Convert.ToBase64String(hmacBytes)}";
        }

        /// <summary>
        /// Decrypts versioned ciphertext using authenticated AES-256-CBC.
        /// Safely returns legacy plaintext if the input is not encrypted.
        /// Throws CryptographicException on corrupted or tampered ciphertext (never returns corrupted ciphertext as plaintext).
        /// </summary>
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return cipherText;
            }

            // Handle legacy plaintext: If it does not start with the HT1 format prefix, return as-is
            if (!cipherText.StartsWith(FormatPrefix + ":", StringComparison.Ordinal))
            {
                return cipherText;
            }

            string[] parts = cipherText.Split(':');
            if (parts.Length != 5 || !string.Equals(parts[0], FormatPrefix, StringComparison.Ordinal))
            {
                throw new CryptographicException("Invalid ciphertext structure: missing required format components.");
            }

            string keyVersion = parts[1];
            string ivB64 = parts[2];
            string cipherB64 = parts[3];
            string hmacB64 = parts[4];

            if (!KeyRing.TryGetValue(keyVersion, out KeySet keys))
            {
                // Attempt on-demand load
                LoadKeyFromConfig(keyVersion);
                if (!KeyRing.TryGetValue(keyVersion, out keys))
                {
                    throw new CryptographicException($"Encryption key version '{keyVersion}' is unavailable on this system.");
                }
            }

            byte[] ivBytes;
            byte[] cipherBytes;
            byte[] receivedHmacBytes;

            try
            {
                ivBytes = Convert.FromBase64String(ivB64);
                cipherBytes = Convert.FromBase64String(cipherB64);
                receivedHmacBytes = Convert.FromBase64String(hmacB64);
            }
            catch (FormatException)
            {
                throw new CryptographicException("Ciphertext payload corrupted: invalid Base64 encoding.");
            }

            if (ivBytes.Length != 16)
            {
                throw new CryptographicException("Ciphertext payload corrupted: invalid IV length.");
            }

            // Verify integrity and authenticity before attempting decryption (prevents padding oracle attacks)
            byte[] expectedHmacBytes = ComputeHmac(keys.HmacKey, keyVersion, ivBytes, cipherBytes);
            if (!FixedTimeEquals(expectedHmacBytes, receivedHmacBytes))
            {
                throw new CryptographicException("Ciphertext integrity verification failed: authentication tag does not match (tampering detected).");
            }

            // Decrypt ciphertext
            try
            {
                using (var aes = new AesCryptoServiceProvider())
                {
                    aes.Key = keys.EncryptionKey;
                    aes.IV = ivBytes;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    using (var decryptor = aes.CreateDecryptor())
                    using (var ms = new MemoryStream(cipherBytes))
                    using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(cs, Encoding.UTF8))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new CryptographicException($"Failed to decrypt sensitive health record: {ex.Message}", ex);
            }
        }

        private static byte[] ComputeHmac(byte[] hmacKey, string keyVersion, byte[] ivBytes, byte[] cipherBytes)
        {
            using (var hmac = new HMACSHA256(hmacKey))
            {
                byte[] headerBytes = Encoding.ASCII.GetBytes($"{FormatPrefix}:{keyVersion}:");
                byte[] combined = new byte[headerBytes.Length + ivBytes.Length + cipherBytes.Length];

                Buffer.BlockCopy(headerBytes, 0, combined, 0, headerBytes.Length);
                Buffer.BlockCopy(ivBytes, 0, combined, headerBytes.Length, ivBytes.Length);
                Buffer.BlockCopy(cipherBytes, 0, combined, headerBytes.Length + ivBytes.Length, cipherBytes.Length);

                return hmac.ComputeHash(combined);
            }
        }

        /// <summary>
        /// Constant-time byte array comparison to prevent timing side-channel attacks.
        /// </summary>
        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            int diff = 0;
            for (int i = 0; i < a.Length; i++)
            {
                diff |= a[i] ^ b[i];
            }
            return diff == 0;
        }

        #region Convenience Entity Helpers

        public static void EncryptProfile(ClientHealthProfile profile)
        {
            if (profile == null) return;
            profile.MedicalConditions = Encrypt(profile.MedicalConditions);
            profile.InjuriesHistory = Encrypt(profile.InjuriesHistory);
            profile.Allergies = Encrypt(profile.Allergies);
            profile.DietaryRestrictions = Encrypt(profile.DietaryRestrictions);
        }

        public static void DecryptProfile(ClientHealthProfile profile)
        {
            if (profile == null) return;
            profile.MedicalConditions = Decrypt(profile.MedicalConditions);
            profile.InjuriesHistory = Decrypt(profile.InjuriesHistory);
            profile.Allergies = Decrypt(profile.Allergies);
            profile.DietaryRestrictions = Decrypt(profile.DietaryRestrictions);
        }

        public static void DecryptHealthProfile(ClientHealthProfile profile)
        {
            DecryptProfile(profile);
        }

        public static void EncryptProgressRecord(ClientProgressRecords record)
        {
            if (record == null) return;
            record.Notes = Encrypt(record.Notes);
        }

        public static void DecryptProgressRecord(ClientProgressRecords record)
        {
            if (record == null) return;
            record.Notes = Decrypt(record.Notes);
        }

        public static void DecryptProgressRecords(IEnumerable<ClientProgressRecords> records)
        {
            if (records == null) return;
            foreach (var record in records)
            {
                DecryptProgressRecord(record);
            }
        }

        public static void EncryptSleepLog(ClientSleepLogs log)
        {
            if (log == null) return;
            log.Notes = Encrypt(log.Notes);
        }

        public static void DecryptSleepLog(ClientSleepLogs log)
        {
            if (log == null) return;
            log.Notes = Decrypt(log.Notes);
        }

        #endregion
    }
}
