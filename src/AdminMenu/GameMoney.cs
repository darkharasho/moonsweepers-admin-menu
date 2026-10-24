using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using MoonCleaners;

namespace AdminMenu
{
    /// <summary>
    /// The game's money is a <c>System.Decimal</c>, and the generated <c>Il2CppSystem.Decimal</c> is broken in the
    /// interop assemblies (its four fields are all at offset 0, so it's 8 bytes instead of 16 and any call passing or
    /// returning one corrupts memory). So every game method that takes or returns a Decimal is invoked here by hand
    /// with a correctly sized struct (the game's layout: flags, hi, lo, mid).
    /// </summary>
    internal static unsafe class GameMoney
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct Raw
        {
            public int Flags;
            public int Hi;
            public int Lo;
            public int Mid;

            public static Raw From(decimal value)
            {
                var bits = decimal.GetBits(value); // lo, mid, hi, flags
                return new Raw { Lo = bits[0], Mid = bits[1], Hi = bits[2], Flags = bits[3] };
            }

            public decimal ToDecimal() =>
                new decimal(Lo, Mid, Hi, (Flags & int.MinValue) != 0, (byte)((Flags >> 16) & 0xFF));
        }

        private static readonly string Dec = IL2CPP.RenderTypeName<Il2CppSystem.Decimal>(false);
        private static readonly string DecRef = IL2CPP.RenderTypeName<Il2CppSystem.Decimal>(true);
        private static readonly string Str = IL2CPP.RenderTypeName<string>(false);
        private static readonly string Bool = IL2CPP.RenderTypeName<bool>(false);

        private static IntPtr _wallet, _crewBuildMoney, _toolPrice, _treePrice, _format, _notEarned;

        private static IntPtr Find(IntPtr cls, string name, string ret, params string[] parameters)
        {
            var method = IL2CPP.GetIl2CppMethod(cls, false, name, ret, parameters);
            if (method == IntPtr.Zero)
                throw new InvalidOperationException($"{name} not found, likely a game update");
            return method;
        }

        private static IntPtr WorldClass => Il2CppClassPointerStore<MoonWorld>.NativeClassPtr;

        private static IntPtr Invoke(IntPtr method, IntPtr instance, void** args)
        {
            IntPtr exception = IntPtr.Zero;
            var result = IL2CPP.il2cpp_runtime_invoke(method, instance, args, ref exception);
            Il2CppInterop.Runtime.Il2CppException.RaiseExceptionIfNecessary(exception);
            return result;
        }

        public static decimal Wallet(MoonWorld world)
        {
            if (_wallet == IntPtr.Zero)
                _wallet = Find(WorldClass, "WalletDollars", Dec, Str);
            var args = stackalloc IntPtr[1];
            args[0] = IL2CPP.ManagedStringToIl2Cpp("local");
            var boxed = Invoke(_wallet, world.Pointer, (void**)args);
            return (*(Raw*)IL2CPP.il2cpp_object_unbox(boxed)).ToDecimal();
        }

        public static void Add(MoonWorld world, string player, decimal amount)
        {
            if (_crewBuildMoney == IntPtr.Zero)
                _crewBuildMoney = Find(WorldClass, "CrewBuildMoney", "System.Void", Str, Dec);
            var raw = Raw.From(amount);
            var args = stackalloc IntPtr[2];
            args[0] = IL2CPP.ManagedStringToIl2Cpp(player);
            args[1] = (IntPtr)(&raw);
            Invoke(_crewBuildMoney, world.Pointer, (void**)args);
        }

        public static bool ToolPrice(MoonWorld world, string toolId, string player, out decimal price)
        {
            if (_toolPrice == IntPtr.Zero)
                _toolPrice = Find(WorldClass, "TryGetToolPrice", Bool, Str, Str, DecRef);
            var raw = default(Raw);
            var args = stackalloc IntPtr[3];
            args[0] = IL2CPP.ManagedStringToIl2Cpp(toolId);
            args[1] = IL2CPP.ManagedStringToIl2Cpp(player);
            args[2] = (IntPtr)(&raw);
            var ok = *(bool*)IL2CPP.il2cpp_object_unbox(Invoke(_toolPrice, world.Pointer, (void**)args));
            price = raw.ToDecimal();
            return ok;
        }

        public static bool TreePrice(MoonWorld world, string player, string nodeId, out decimal price)
        {
            if (_treePrice == IntPtr.Zero)
                _treePrice = Find(WorldClass, "TryGetTreePrice", Bool, Str, Str, DecRef);
            var raw = default(Raw);
            var args = stackalloc IntPtr[3];
            args[0] = IL2CPP.ManagedStringToIl2Cpp(player);
            args[1] = IL2CPP.ManagedStringToIl2Cpp(nodeId);
            args[2] = (IntPtr)(&raw);
            var ok = *(bool*)IL2CPP.il2cpp_object_unbox(Invoke(_treePrice, world.Pointer, (void**)args));
            price = raw.ToDecimal();
            return ok;
        }

        /// <summary>The game's own money formatting; falls back to plain text if that can't be called.</summary>
        public static string Format(decimal dollars)
        {
            try
            {
                if (_format == IntPtr.Zero)
                    _format = Find(WorldClass, "FormatMoney", Str, Dec);
                var raw = Raw.From(dollars);
                var args = stackalloc IntPtr[1];
                args[0] = (IntPtr)(&raw);
                var result = Invoke(_format, IntPtr.Zero, (void**)args);
                return result == IntPtr.Zero ? Fallback(dollars) : IL2CPP.Il2CppStringToManaged(result);
            }
            catch (Exception)
            {
                return Fallback(dollars);
            }
        }

        private static string Fallback(decimal dollars) => "$" + dollars.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Keeps mod money out of the host's Steam achievement counters.</summary>
        public static void NotEarned(string player, decimal delta)
        {
            if (_notEarned == IntPtr.Zero)
                _notEarned = Find(IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "MoonCleaners", "MoonAchievements"), "NotEarned", "System.Void", Str, Dec);
            var raw = Raw.From(delta);
            var args = stackalloc IntPtr[2];
            args[0] = IL2CPP.ManagedStringToIl2Cpp(player);
            args[1] = (IntPtr)(&raw);
            Invoke(_notEarned, IntPtr.Zero, (void**)args);
        }
    }
}
