using CommunityToolkit.HighPerformance;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleLib.Runtime
{
    public static class CommandArguments
    {
        private static Dictionary<int, object?> _arguments = new Dictionary<int, object?>();

        //TODO: add support for multi-value arguments aka: "-Arg Val1 Val2 -Arg2 Val3" etc
        internal static void Parse(ref string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                object? val = null;

                if (args.Length > i + 1 && !args[i + 1].StartsWith('-'))
                {
                    if (long.TryParse(args[i + 1], CultureInfo.InvariantCulture, out long long_val))
                        val = long_val;
                    else if (bool.TryParse(args[i + 1], out bool bool_val))
                        val = bool_val;
                    else if (float.TryParse(args[i + 1], CultureInfo.InvariantCulture, out float float_val))
                        val = float_val;
                    else
                        val = args[i + 1];
                }

                _arguments[arg.GetDjb2HashCode()] = val;
            }
        }

        public static bool Exists(string key)
        {
            return _arguments.ContainsKey(key.GetDjb2HashCode());
        }

        public static T? GetValue<T>(string key)
            where T : unmanaged
        {
            int hash = key.GetDjb2HashCode();
            return _arguments.TryGetValue(hash, out object? v) ? (T?)v : null;
        }

        public static T GetValueOrDefault<T>(string key, T def = default)
            where T : unmanaged
        {
            int hash = key.GetDjb2HashCode();
            return _arguments.TryGetValue(hash, out object? v) ? (T)(v ?? def) : def;
        }
    }
}
