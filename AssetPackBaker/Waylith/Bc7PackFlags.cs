// Decompiled from RimWorld 1.7's Waylith.dll (Waylith.Rendering.Bc7PackFlags) and modified;
// derived from Basis Universal and bc7enc_rdo. See NOTICE.md in this folder.

using System;

namespace AssetPackBaker.Waylith;

[Flags]
internal enum Bc7PackFlags : uint
{
    None = 0u,
    Use2SubsetsRgb = 1u,
    Use2SubsetsRgba = 2u,
    Use3SubsetsRgb = 4u,
    UseDualPlaneRgb = 8u,
    UseDualPlaneRgba = 0x10u,
    PBitOpt = 0x20u,
    PBitOptMode6 = 0x40u,
    UseTrivialMode6 = 0x80u,
    PartiallyAnalyticalRgb = 0x100u,
    PartiallyAnalyticalRgba = 0x200u,
    NonAnalyticalRgb = 0x400u,
    NonAnalyticalRgba = 0x800u,
    AstcCompatible = 0x1000u,
    DisableRgbDualPlane = 0x2000u,
    DefaultFastest = 0x80u,
    DefaultFaster = 0xB0u,
    DefaultFast = 0xB3u,
    Default = 0xFFu,
    WaylithDefault = 0xBFu,
}
