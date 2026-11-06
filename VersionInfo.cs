using System;

namespace RiverDesk
{
    internal static class VersionInfo
    {
        internal const string Name = "RiverDesk Toolkit";
        internal const string StableNumber = "1.3.3.0";
        internal const string BetaNumber = "1.3.4.0";
        internal const string BetaLabel = "beta1";
#if LIQUID_GLASS_BETA
        internal const string Number = BetaNumber;
        internal static bool IsLiquidGlass { get { return true; } }
#else
        internal const string Number = StableNumber;
        internal static bool IsLiquidGlass { get { return false; } }
#endif
        internal const string Releases = "https://github.com/ShallowThoughts/riverdesk-toolkit/releases";
        internal static readonly Version Current = new Version(Number);
        internal static string Display
        {
            get { return Current.ToString(3) + (IsLiquidGlass ? "-" + BetaLabel : ""); }
        }
    }
}
