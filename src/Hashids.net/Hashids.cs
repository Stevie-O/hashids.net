using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HashidsNet
{
    /// <summary>
    /// Generate YouTube-like hashes from one or many numbers. Use hashids when you do not want to expose your database ids to the user.
    /// </summary>
    public class Hashids : IHashids
    {
        public const string DEFAULT_ALPHABET = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890";
        public const string DEFAULT_SEPS = "cfhistuCFHISTU";

        private const int MIN_ALPHABET_LENGTH = 16;
        private const double SEP_DIV = 3.5;
        private const double GUARD_DIV = 12.0;

        private string alphabet;
        private string salt;
        private char[] seps;
        private char[] guards;
        private int minHashLength;

        //  Creates the Regex in the first usage, speed up first use of non hex methods
#if CORE
        private static Lazy<Regex> hexValidator = new Lazy<Regex>(() => new Regex("^[0-9a-fA-F]+$"));
        private static Lazy<Regex> hexSplitter = new Lazy<Regex>(() => new Regex(@"[\w\W]{1,12}"));
#else
        private static Lazy<Regex> hexValidator = new Lazy<Regex>(() => new Regex("^[0-9a-fA-F]+$", RegexOptions.Compiled));
        private static Lazy<Regex> hexSplitter = new Lazy<Regex>(() => new Regex(@"[\w\W]{1,12}", RegexOptions.Compiled));
#endif

        /// <summary>
        /// Instantiates a new Hashids with the default setup.
        /// </summary>
        public Hashids() : this(string.Empty, 0, DEFAULT_ALPHABET, DEFAULT_SEPS)
        { }

        /// <summary>
        /// Instantiates a new Hashids en/de-coder.
        /// </summary>
        /// <param name="salt"></param>
        /// <param name="minHashLength"></param>
        /// <param name="alphabet"></param>
        public Hashids(string salt = "", int minHashLength = 0, string alphabet = DEFAULT_ALPHABET, string seps = DEFAULT_SEPS)
        {
            if (string.IsNullOrWhiteSpace(alphabet))
                throw new ArgumentNullException("alphabet");

            this.salt = salt;
            this.alphabet = new string(alphabet.ToCharArray().Distinct().ToArray());
            this.minHashLength = minHashLength;

            if (this.alphabet.Length < 16)
                throw new ArgumentException("alphabet must contain atleast 4 unique characters.", "alphabet");

            this.SetupSeps(seps);
            this.SetupGuards();
        }

        /// <summary>
        /// Encodes the provided numbers into a hashed string
        /// </summary>
        /// <param name="numbers">the numbers to encode</param>
        /// <returns>the hashed string</returns>
        public virtual string Encode(params int[] numbers)
        {
            ulong[] ulongs = new ulong[numbers.Length];
            for (int i = 0; i < numbers.Length; i++)
            {
                if (numbers[i] < 0) return string.Empty;
                ulongs[i] = (ulong)numbers[i];
            }
            return this.GenerateHashFrom(ulongs);
        }

        /// <summary>
        /// Encodes the provided numbers into a hashed string
        /// </summary>
        /// <param name="numbers">the numbers to encode</param>
        /// <returns>the hashed string</returns>
        public virtual string Encode(IEnumerable<int> numbers)
        {
            return this.Encode(numbers.ToArray());
        }

        /// <summary>
        /// Decodes the provided hash into
        /// </summary>
        /// <param name="hash">the hash</param>
        /// <exception cref="T:System.OverflowException">if the decoded number overflows integer</exception>
        /// <returns>the numbers</returns>
        public virtual int[] Decode(string hash)
        {
            return this.GetNumbersFrom(hash).Select(n => (int)n).ToArray();
        }

        /// <summary>
        /// Encodes the provided hex string to a hashids hash.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public virtual string EncodeHex(string hex)
        {
            if (!hexValidator.Value.IsMatch(hex))
                return string.Empty;

            var numbers = new List<long>();
            var matches = hexSplitter.Value.Matches(hex);

            foreach (Match match in matches)
            {
                var number = Convert.ToInt64(string.Concat("1", match.Value), 16);
                numbers.Add(number);
            }

            return this.EncodeLong(numbers.ToArray());
        }

        /// <summary>
        /// Decodes the provided hash into a hex-string
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        public virtual string DecodeHex(string hash)
        {
            var ret = new StringBuilder();
            var numbers = this.DecodeLong(hash);

            foreach (var number in numbers)
                ret.Append(string.Format("{0:X}", number).Substring(1));

            return ret.ToString();
        }

        /// <summary>
        /// Gets/sets whether or not to exploit array variance to improve performance of <see cref="DecodeLong"/>.
        /// </summary>
        /// <remarks>
        /// If this is set to true, an unnecessary memory allocation and copy is avoided, but
        /// the array returned by <see cref="DecodeLong"/> behaves oddly when accessed via reflection,
        /// such as what happens when tests are performed.
        /// </remarks>
        public static bool ExploitArrayVariance { get; set; }

        /// <summary>
        /// Decodes the provided hashed string into an array of longs 
        /// </summary>
        /// <param name="hash">the hashed string</param>
        /// <returns>the numbers</returns>
        public long[] DecodeLong(string hash)
        {
            var ulongs = GetNumbersFrom(hash);
            if (ExploitArrayVariance)
            {
                // C# doesn't allow ulong[]->long[] variance, but CLR does
                return (long[])(object)ulongs;
            }
            // go the slow way
#if CORE
            return ulongs.Select(x => (long)x).ToArray();
#else
            return Array.ConvertAll<ulong, long>(ulongs, x => (long)x);
#endif
        }

        public ulong[] DecodeUnsignedLong(string hash)
        {
            return this.GetNumbersFrom(hash);
        }

        /// <summary>
        /// Encodes a sequence of unsigned 64-bit integers
        /// </summary>
        /// <param name="numbers"></param>
        /// <returns></returns>
        public string EncodeUnsignedLong(params ulong[] numbers)
        {
            return this.GenerateHashFrom(numbers);
        }

        /// <summary>
        /// Encodes the provided longs to a hashed string
        /// </summary>
        /// <param name="numbers">the numbers</param>
        /// <returns>the hashed string</returns>
        public string EncodeLong(params long[] numbers)
        {
            if (numbers.Any(n => n < 0)) return string.Empty;
            // C# does not allow array variance for value types, but CLR does; an intermediate cast to object
            // lets us exploit this.
            var ulongs = (ulong[])(object)numbers;
            return this.GenerateHashFrom(ulongs);
        }

        /// <summary>
        /// Encodes the provided longs to a hashed string
        /// </summary>
        /// <param name="numbers">the numbers</param>
        /// <returns>the hashed string</returns>
        public string EncodeLong(IEnumerable<long> numbers)
        {
            return this.EncodeLong(numbers.ToArray());
        }

        /// <summary>
        /// Encodes the provided numbers into a string.
        /// </summary>
        /// <param name="number">the numbers</param>
        /// <returns>the hash</returns>
        [Obsolete("Use 'Encode' instead. The method was renamed to better explain what it actually does.")]
        public virtual string Encrypt(params int[] numbers)
        {
            return Encode(numbers);
        }

        /// <summary>
        /// Encrypts the provided hex string to a hashids hash.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        [Obsolete("Use 'EncodeHex' instead. The method was renamed to better explain what it actually does.")]
        public virtual string EncryptHex(string hex)
        {
            return EncodeHex(hex);
        }

        /// <summary>
        /// Decodes the provided numbers into a array of numbers
        /// </summary>
        /// <param name="hash">hash</param>
        /// <returns>array of numbers.</returns>
        [Obsolete("Use 'Decode' instead. Method was renamed to better explain what it actually does.")]
        public virtual int[] Decrypt(string hash)
        {
            return Decode(hash);
        }

        /// <summary>
        /// Decodes the provided hash to a hex-string
        /// </summary>
        /// <param name="hash"></param>
        /// <returns></returns>
        [Obsolete("Use 'DecodeHex' instead. The method was renamed to better explain what it actually does.")]
        public virtual string DecryptHex(string hash)
        {
            return DecodeHex(hash);
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetupSeps(string seps)
        {
            // seps should contain only characters present in alphabet; 
            seps = new string(seps.ToCharArray().Intersect(alphabet.ToCharArray()).ToArray());

            // alphabet should not contain seps.
            alphabet = new string(alphabet.ToCharArray().Except(seps.ToCharArray()).ToArray());

            seps = ConsistentShuffle(seps, salt);

            if (seps.Length == 0 || (alphabet.Length / seps.Length) > SEP_DIV)
            {
                var sepsLength = (int)Math.Ceiling(alphabet.Length / SEP_DIV);
                if (sepsLength == 1)
                    sepsLength = 2;

                if (sepsLength > seps.Length)
                {
                    var diff = sepsLength - seps.Length;
                    seps += alphabet.Substring(0, diff);
                    alphabet = alphabet.Substring(diff);
                }

                else seps = seps.Substring(0, sepsLength);
            }

            this.seps = seps.ToCharArray();
            alphabet = ConsistentShuffle(alphabet, salt);
        }

        static T[] Slice<T>(T[] array, int offset, int count)
        {
            T[] slice = new T[count];
            Array.Copy(array, offset, slice, 0, count);
            return slice;
        }

        /// <summary>
        /// 
        /// </summary>
        private void SetupGuards()
        {
            var guardCount = (int)Math.Ceiling(alphabet.Length / GUARD_DIV);

            if (alphabet.Length < 3)
            {
                guards = Slice(seps, 0, guardCount);
                seps = Slice(seps, guardCount, seps.Length - guardCount);
            }
            else
            {
                guards = alphabet.ToCharArray(0, guardCount);
                alphabet = alphabet.Substring(guardCount);
            }
        }

        /// <summary>
        /// Internal function that does the work of creating the hash
        /// </summary>
        /// <param name="numbers"></param>
        /// <returns></returns>
        private string GenerateHashFrom(ulong[] numbers)
        {
            if (numbers == null || numbers.Length == 0)
                return string.Empty;

            var ret = new StringBuilder();
            var alphabet = this.alphabet;

            long numbersHashInt = 0;
            for (var i = 0; i < numbers.Length; i++)
                numbersHashInt += (int)(numbers[i] % (ulong)(i + 100));

            var lottery = alphabet[(int)(numbersHashInt % alphabet.Length)];
            ret.Append(lottery.ToString());

            for (var i = 0; i < numbers.Length; i++)
            {
                var number = numbers[i];
                var buffer = lottery + this.salt + alphabet;

                alphabet = ConsistentShuffle(alphabet, buffer);
                var last = this.Hash(number, alphabet);

                ret.Append(last);

                if (i + 1 < numbers.Length)
                {
                    number %= (uint)((int)last[0] + i);
                    var sepsIndex = ((int)number % this.seps.Length);

                    ret.Append(this.seps[sepsIndex]);
                }
            }

            if (ret.Length < this.minHashLength)
            {
                var guardIndex = ((int)(numbersHashInt + (int)ret[0]) % this.guards.Length);
                var guard = this.guards[guardIndex];

                ret.Insert(0, guard);

                if (ret.Length < this.minHashLength)
                {
                    guardIndex = ((int)(numbersHashInt + (int)ret[2]) % this.guards.Length);
                    guard = this.guards[guardIndex];

                    ret.Append(guard);
                }
            }

            var halfLength = (int)(alphabet.Length / 2);
            while (ret.Length < this.minHashLength)
            {
                alphabet = ConsistentShuffle(alphabet, alphabet);
                ret.Insert(0, alphabet.Substring(halfLength));
                ret.Append(alphabet.Substring(0, halfLength));

                var excess = ret.Length - this.minHashLength;
                if (excess > 0)
                {
                    ret.Remove(0, excess / 2);
                    ret.Remove(this.minHashLength, ret.Length - this.minHashLength);
                }
            }

            return ret.ToString();
        }

        private string Hash(ulong input, string alphabet)
        {
            var hash = new StringBuilder();

            do
            {
                hash.Insert(0, alphabet[(int)(input % (uint)alphabet.Length)]);
                input = (input / (uint)alphabet.Length);
            } while (input > 0);

            return hash.ToString();
        }

        private ulong Unhash(string input, string alphabet)
        {
            ulong number = 0;

            for (var i = 0; i < input.Length; i++)
            {
                var pos = (uint)alphabet.IndexOf(input[i]);
                number = number * (uint)alphabet.Length + pos;
            }

            return number;
        }

        string removeGuards(string s)
        {
            int g0 = s.IndexOfAny(guards);
            if (g0 < 0) return s;
            g0++;
            int g1 = s.IndexOfAny(guards, g0);
            if (g1 < 0) g1 = s.Length;
            return s.Substring(g0, g1 - g0);
        }

        private ulong[] GetNumbersFrom(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return InvalidCodeResult;

            var alphabet = this.alphabet;
            int i = 0;

            var hashBreakdown = removeGuards(hash);
            if (hashBreakdown.Length == 0)
                return InvalidCodeResult;

            var lottery = hashBreakdown[0];
            hashBreakdown = hashBreakdown.Substring(1);

            var hashArray = hashBreakdown.Split(seps, StringSplitOptions.RemoveEmptyEntries);
            var result = new ulong[hashArray.Length];

            for (var j = 0; j < hashArray.Length; j++)
            {
                var subHash = hashArray[j];
                var buffer = lottery + this.salt + alphabet;

                alphabet = ConsistentShuffle(alphabet, buffer.Substring(0, alphabet.Length));
                result[j] = Unhash(subHash, alphabet);
            }

            if (EncodeUnsignedLong(result) == hash)
                return result;
            return InvalidCodeResult;
        }

        static readonly ulong[] InvalidCodeResult = new ulong[0];

        /// <summary>
        /// 
        /// </summary>
        /// <param name="alphabet"></param>
        /// <param name="salt"></param>
        /// <returns></returns>
        private string ConsistentShuffle(string alphabet, string salt)
        {
            if (string.IsNullOrWhiteSpace(salt))
                return alphabet;

            int n;
            var letters = alphabet.ToCharArray();
            for (int i = letters.Length - 1, v = 0, p = 0; i > 0; i--, v++)
            {
                v %= salt.Length;
                p += (n = salt[v]);
                var j = (n + v + p) % i;
                // swap characters at positions i and j
                var temp = letters[j];
                letters[j] = letters[i];
                letters[i] = temp;
            }

            return new string(letters);
        }

    }
}
