using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ManagedLzma
{
    /*
    /// <summary>
    /// This interface is used to allow decoding of password protected 7z archives.
    /// The password is only requested when an attempt is made to decode password protected content.
    /// </summary>
    public interface IPasswordProvider
    {
        /// <summary>
        /// Requests the password to decode password protected content.
        /// This method is only called once per archive reader, the returned password is cached for future reference.
        /// </summary>
        /// <param name="ct">If the decoding is aborted this token will forward the cancellation to abort the password request.</param>
        /// <returns>
        /// The returned task should complete with the cleartext password.
        /// If the returned task throws a cancellation exception the decoding will be aborted.
        /// </returns>
        Task<PasswordStorage> GetPasswordAsync(CancellationToken ct);
    }
    */

    /// <summary>
    /// Stores a password in a way so that memory dumps do not expose the password in plaintext.
    /// Note that the password is still recoverable with some work if you have a full memory dump.
    /// The intention is to protect against accidental exposal of the password, not protecting it from attackers.
    /// </summary>
    /// <remarks>
    /// Note that we store a char array and not a string so we can control its lifetime by zeroing the array contents when we are done.
    /// The intention is to avoid accidently leaving passwords in memory dumps just because the GC didn't yet get around to clean them up.
    /// To avoid issues with the array being modified after completing the task we immediately make a copy and clear the original array.
    /// </remarks>
    public sealed class PasswordStorage : IDisposable
    {
        // This is the multiplier used by the C random number generator.
        private static int kMultiplier = 0x41C64E6D;

        private static void Encode(char[] password, int seed)
        {
            for (int i = 0; i < password.Length; i++)
            {
                var ch = password[i];
                password[i] = (char)(ch ^ seed);
                seed = seed * kMultiplier + ch;
            }
        }

        private static void Decode(char[] password, int seed)
        {
            for (int i = 0; i < password.Length; i++)
            {
                var ch = password[i];
                password[i] = (char)(ch ^ seed);
                seed = seed * kMultiplier + password[i];
            }
        }

        /// <summary>
        /// WARNING: This is just a convenience method. If you care about memory dumps not containing readable copies
        /// of your password you shouldn't put it in an immutable string object. Use the overload taking an array.
        /// </summary>
        public static PasswordStorage Create(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            return new PasswordStorage(password.ToCharArray());
        }

        /// <summary>
        /// Returns a container storing a copy of the given password. The array passed as argument is cleared.
        /// </summary>
        public static PasswordStorage Create(char[] password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            // Make a copy of the array so it can't be modified externally by the provider.
            // Zero out the original array so it doesn't leave a copy of the password in memory dumps.
            var buffer = new char[password.Length];

            for (int i = 0; i < password.Length; i++)
            {
                buffer[i] = password[i];
                password[i] = default(char);
            }

            return new PasswordStorage(buffer);
        }

        private char[] mPassword;

        private PasswordStorage(char[] password)
        {
            // Ensure the password is not plaintext readable in a memory dump. As a small bonus, using our own
            // hash as seed makes the password impossible to decode if you just got the encoded character array.
            Encode(mPassword = password, GetHashCode());
        }

        public void Dispose()
        {
            // Exchange the password and then zero it out so there are no traces left of it.
            // This may interfere with the GetPassword function, but it has a safety check for this case.
            new PasswordAccessor(Interlocked.Exchange(ref mPassword, null)).Dispose();
        }

        public PasswordAccessor GetPassword()
        {
            var result = default(PasswordAccessor);
            try
            {
                var password = Volatile.Read(ref mPassword);
                if (password != null)
                {
                    // Make a copy of the password so asynchronous disposal does not interfere with the caller processing the password.
                    result = new PasswordAccessor((char[])password.Clone());

                    // Must do a second check because if a dispose happened while the array was cloned it may have been partially zeroed out.
                    // However, if at this point the stored password is not null later disposal will not have any effect on the copy.
                    if (Volatile.Read(ref mPassword) != null)
                    {
                        Decode(result, GetHashCode());
                        return new PasswordAccessor(result);
                    }
                }

                throw new ObjectDisposedException(null);
            }
            catch
            {
                // If an exception happens before we can return the accessor to the caller we need to clear potential copies of the password.
                result.Dispose();
                throw;
            }
        }
    }

    [System.Diagnostics.DebuggerDisplay(@"\{Password {new System.String(mPassword)}\}")]
    public struct PasswordAccessor : IDisposable, IEquatable<PasswordAccessor>
    {
        private char[] mPassword;

        public PasswordAccessor(char[] password)
        {
            mPassword = password;
        }

        public void Dispose()
        {
            Utilities.ClearBuffer(ref mPassword);
        }

        public char[] Password
        {
            get { return mPassword; }
        }

        public int Length
        {
            get { return mPassword != null ? mPassword.Length : 0; }
        }

        public char this[int index]
        {
            get
            {
                if (index < 0 || index >= Length)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return mPassword[index];
            }
        }

        /// <remarks>
        /// Intentionally not returning the password as string here because that risks leaking it in a memory dump.
        /// </remarks>
        public override string ToString()
        {
            return "{Password}";
        }

        /// <remarks>Hash function taken from Roslyn.</remarks>
        public override int GetHashCode()
        {
            int hash = unchecked((int)2166136261);

            for (int i = 0; i < mPassword.Length; i++)
                hash = (hash ^ mPassword[i]) * 16777619;

            return hash;
        }

        public override bool Equals(object obj)
        {
            return obj is PasswordAccessor && Equals((PasswordAccessor)obj);
        }

        public bool Equals(PasswordAccessor other)
        {
            var thisPassword = mPassword;
            var otherPassword = other.mPassword;

            if (thisPassword == otherPassword)
                return true;

            if (thisPassword == null || otherPassword == null)
                return false;

            if (thisPassword.Length != otherPassword.Length)
                return false;

            for (int i = 0; i < thisPassword.Length; i++)
                if (thisPassword[i] != otherPassword[i])
                    return false;

            return true;
        }

        public static bool operator ==(PasswordAccessor left, PasswordAccessor right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PasswordAccessor left, PasswordAccessor right)
        {
            return !left.Equals(right);
        }

        public static implicit operator char[] (PasswordAccessor holder)
        {
            return holder.mPassword;
        }
    }
}
