using System;
using System.Security.Cryptography;

namespace HealthTrack.Helpers
{
    /// Secure Password Helper using PBKDF2 (Rfc2898DeriveBytes).
    public static class PasswordHelper
    {
        private const int SaltSize = 16;        // 128-bit salt
        private const int HashSize = 20;        // 160-bit subkey
        private const int Iterations = 10000;   // Industry standard iterations

        /// Creates a secure salted PBKDF2 hash of the plaintext password.
        public static string HashPassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be empty.", nameof(password));
            }

            // 1. Generate a cryptographic random salt
            byte[] salt = new byte[SaltSize];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }

            // 2. Derive the salted hash
            byte[] hash;
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations))
            {
                hash = pbkdf2.GetBytes(HashSize);
            }

            // 3. Combine salt and hash into one array
            byte[] combinedBytes = new byte[SaltSize + HashSize];
            Array.Copy(salt, 0, combinedBytes, 0, SaltSize);
            Array.Copy(hash, 0, combinedBytes, SaltSize, HashSize);

            // 4. Return as Base64 string for safe storage in SQL Server
            return Convert.ToBase64String(combinedBytes);
        }

        /// Verifies whether the provided plaintext password matches the stored salted hash.
        public static bool VerifyPassword(string enteredPassword, string storedHash)
        {
            if (string.IsNullOrEmpty(enteredPassword) || string.IsNullOrEmpty(storedHash))
            {
                return false;
            }

            try
            {
                // 1. Decode the combined salt+hash from Base64
                byte[] combinedBytes = Convert.FromBase64String(storedHash);
                if (combinedBytes.Length != SaltSize + HashSize)
                {
                    return false;
                }

                // 2. Extract the original salt (first 16 bytes)
                byte[] salt = new byte[SaltSize];
                Array.Copy(combinedBytes, 0, salt, 0, SaltSize);

                // 3. Hash the entered password with the exact same salt
                byte[] enteredHash;
                using (var pbkdf2 = new Rfc2898DeriveBytes(enteredPassword, salt, Iterations))
                {
                    enteredHash = pbkdf2.GetBytes(HashSize);
                }

                // 4. Compare the newly generated hash with the stored hash in constant time
                int diff = 0;
                for (int i = 0; i < HashSize; i++)
                {
                    diff |= combinedBytes[SaltSize + i] ^ enteredHash[i];
                }

                return diff == 0; // Match!
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether a stored password string matches the expected PBKDF2 hash format (Base64 of 36 bytes).
        /// </summary>
        public static bool IsHashed(string storedPassword)
        {
            if (string.IsNullOrWhiteSpace(storedPassword) || storedPassword.Length != 48)
            {
                return false;
            }

            try
            {
                byte[] combinedBytes = Convert.FromBase64String(storedPassword);
                return combinedBytes.Length == (SaltSize + HashSize);
            }
            catch
            {
                return false;
            }
        }
    }
}
