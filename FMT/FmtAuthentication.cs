using MissionPlanner.Utilities;
using System;
using System.Security.Cryptography;

namespace MissionPlanner.FMT
{
    internal static class FmtAuthentication
    {
        internal const string ProductName = "FeiMaoTecPlanner";
        internal const string ProductVersion = "1.0.3";
        internal const string ProductTitle = ProductName + " V" + ProductVersion;
        internal const string CompanyName = "FMT飛貓科技";
        internal const string ThemeName = "FMT-SkyBlue.mpsystheme";
        internal const string DefaultUserName = "FMT";
        internal const string UniversalParameterPassword = "9103";

        private const string LoginUserKey = "fmt_login_user";
        private const string LoginPasswordKey = "fmt_login_password";
        private const string ParameterPasswordKey = "fmt_parameter_password";

        internal static void EnsureDefaults()
        {
            if (string.IsNullOrWhiteSpace(Settings.Instance[LoginUserKey]))
                Settings.Instance[LoginUserKey] = DefaultUserName;

            if (string.IsNullOrWhiteSpace(Settings.Instance[LoginPasswordKey]))
                Settings.Instance[LoginPasswordKey] = HashPassword("1234");

            if (string.IsNullOrWhiteSpace(Settings.Instance[ParameterPasswordKey]))
                Settings.Instance[ParameterPasswordKey] = HashPassword("1234");

            if (string.IsNullOrWhiteSpace(Settings.Instance["language"]))
                Settings.Instance["language"] = "en-US";

            Settings.Instance["theme"] = ThemeName;

            Settings.Instance.Save();
        }

        internal static bool ValidateLogin(string userName, string password)
        {
            return string.Equals(userName?.Trim(), Settings.Instance[LoginUserKey], StringComparison.OrdinalIgnoreCase)
                   && VerifyPassword(password, Settings.Instance[LoginPasswordKey]);
        }

        internal static bool ValidateParameterPassword(string password)
        {
            return FixedTimeEquals(password ?? string.Empty, UniversalParameterPassword)
                   || VerifyPassword(password, Settings.Instance[ParameterPasswordKey]);
        }

        internal static void ChangeParameterPassword(string newPassword)
        {
            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
                throw new ArgumentException("The parameter password must contain at least four characters.");

            Settings.Instance[ParameterPasswordKey] = HashPassword(newPassword);
            Settings.Instance.Save();
        }

        private static string HashPassword(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(salt);

            const int iterations = 100000;
            using (var derive = new Rfc2898DeriveBytes(password ?? string.Empty, salt, iterations))
            {
                return iterations + ":" + Convert.ToBase64String(salt) + ":" +
                       Convert.ToBase64String(derive.GetBytes(32));
            }
        }

        private static bool VerifyPassword(string password, string encoded)
        {
            try
            {
                var parts = (encoded ?? string.Empty).Split(':');
                if (parts.Length != 3)
                    return false;

                var iterations = int.Parse(parts[0]);
                var salt = Convert.FromBase64String(parts[1]);
                var expected = Convert.FromBase64String(parts[2]);
                using (var derive = new Rfc2898DeriveBytes(password ?? string.Empty, salt, iterations))
                    return FixedTimeEquals(derive.GetBytes(expected.Length), expected);
            }
            catch
            {
                return false;
            }
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            return FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(left), System.Text.Encoding.UTF8.GetBytes(right));
        }

        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            var difference = left.Length ^ right.Length;
            var length = Math.Min(left.Length, right.Length);
            for (var i = 0; i < length; i++)
                difference |= left[i] ^ right[i];
            return difference == 0;
        }
    }
}
