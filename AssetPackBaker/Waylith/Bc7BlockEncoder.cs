// Decompiled from RimWorld 1.7's Waylith.dll (Waylith.Rendering.Bc7BlockEncoder) and modified;
// derived from Basis Universal and bc7enc_rdo. See NOTICE.md in this folder.

using System;
using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace AssetPackBaker.Waylith;

internal static class Bc7BlockEncoder
{
    internal interface IErrorBudget
    {
        bool Exceeded(int partial);
    }

    private ref struct Channels
    {
        public Vector128<byte> R;

        public Vector128<byte> G;

        public Vector128<byte> B;

        public Vector128<byte> A;

        public unsafe Channels(byte* pixels)
        {
            Vector128<byte> v = Vector128.Load(pixels);
            Vector128<byte> v2 = Vector128.Load(pixels + 16);
            Vector128<byte> v3 = Vector128.Load(pixels + 32);
            Vector128<byte> v4 = Vector128.Load(pixels + 48);
            (Vector128<byte>, Vector128<byte>) odd;
            (Vector128<byte>, Vector128<byte>) tuple = Deinterleave(v, v2, v3, v4, out odd);
            R = Deinterleave2(tuple.Item1, tuple.Item2, out B);
            G = Deinterleave2(odd.Item1, odd.Item2, out A);
        }

        private static (Vector128<byte> Low, Vector128<byte> High) Deinterleave(
            Vector128<byte> v0,
            Vector128<byte> v1,
            Vector128<byte> v2,
            Vector128<byte> v3,
            out (Vector128<byte> Low, Vector128<byte> High) odd
        )
        {
            odd = (Low: Narrow(Odd(v0), Odd(v1)), High: Narrow(Odd(v2), Odd(v3)));
            return (
                Low: Narrow(v0.AsUInt16(), v1.AsUInt16()),
                High: Narrow(v2.AsUInt16(), v3.AsUInt16())
            );
        }

        private static Vector128<byte> Deinterleave2(
            Vector128<byte> low,
            Vector128<byte> high,
            out Vector128<byte> odd
        )
        {
            odd = Narrow(Odd(low), Odd(high));
            return Narrow(low.AsUInt16(), high.AsUInt16());
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<ushort> Odd(Vector128<byte> v)
        {
            return Vector128.ShiftRightLogical(v.AsUInt16(), 8);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Narrow(Vector128<ushort> low, Vector128<ushort> high)
        {
            return Vector128.Narrow(low, high);
        }
    }

    [InlineArray(4)]
    private struct Vec4
    {
        private float element;
    }

    private static readonly Vector128<byte> Mode6FieldWindows;

    private static readonly Vector128<ushort> Mode6FieldLifts;

    private static readonly Vector128<byte> Mode6PBitWindows;

    private static readonly Vector128<ushort> Mode6PBitLifts;

    private static readonly Vector128<byte> Mode5FieldWindows;

    private static readonly Vector128<ushort> Mode5FieldLifts;

    private static readonly Vector128<ushort> Mode5FieldMasks;

    private static readonly Vector128<ushort> Mode5ColorLanes;

    private static readonly Vector128<byte> SwapHalves;

    private static readonly Vector128<byte> BroadcastLowHalf;

    private static readonly Vector128<byte> NibbleBytes;

    private static readonly Vector128<byte> HighNibbleLanes;

    private static readonly Vector128<byte> Weights4Table;

    private static readonly Vector128<byte> QuarterBytes;

    private static readonly Vector128<byte> HighNibbleQuads;

    private static readonly Vector128<byte> OddByteLanes;

    private static readonly Vector128<byte> Weights2ByLowPair;

    private static readonly Vector128<byte> Weights2ByHighPair;

    private static readonly ushort[] Weights3Pairs;

    private static readonly Vector128<byte> SpreadOpaque;

    private static readonly Vector256<byte> FirstOfRows01;

    private static readonly Vector256<byte> SecondOfRows01;

    private static readonly Vector256<byte> FirstOfRows23;

    private static readonly Vector256<byte> SecondOfRows23;

    private static readonly ulong[] Bc4Palettes;

    private static readonly uint[] Bc4SelectorSpread;

    private static readonly Vector128<byte> EndpointSpread;

    private static readonly Vector128<ushort> FieldMasks;

    private static readonly Vector128<ushort> ChannelScales;

    private static readonly Vector128<ushort> AlphaOnes;

    private static readonly Vector128<uint> SwapEndpoints;

    private static readonly Vector128<ushort> FirstEntryLanes;

    private static readonly Vector128<ushort> PunchThroughBlack;

    private static readonly byte[] SelectorShuffles;

    private static readonly Vector128<byte> Rotate8;

    private static readonly Vector128<byte> Rotate4;

    private static readonly Vector128<byte> SpreadBits;

    private static readonly Vector128<byte> BitWithinByte;

    private static readonly Vector128<byte> Broadcast0;

    private static readonly Vector128<byte> Broadcast4;

    private static readonly Vector128<byte> Broadcast8;

    private static readonly Vector128<byte> Broadcast12;

    private static readonly Vector256<byte> NibblePopCount;

    private const Bc7PackFlags UnsupportedFlags =
        Bc7PackFlags.PartiallyAnalyticalRgb
        | Bc7PackFlags.PartiallyAnalyticalRgba
        | Bc7PackFlags.NonAnalyticalRgb
        | Bc7PackFlags.NonAnalyticalRgba
        | Bc7PackFlags.AstcCompatible
        | Bc7PackFlags.DisableRgbDualPlane;

    private const int TrivialBlockThreshRgb = 320;

    private const int TrivialBlockThreshRgba = 32;

    private const int DpBlockVarThresh = 32;

    private const float StrongCorrThresh = 0.85f;

    private const int MinBlockMaxVar23Subsets = 1600;

    private const float HighOrthoEnergyThresh = 16f;

    private const float OrthoRatio23SubsetRatioThresh = 0.004f;

    private const int DpBlockVarThreshRgba = 16;

    private const float AlphaDecorrThreshold = 0.995f;

    private const float StrongDecorrThreshRgba = 0.85f;

    private const int MinBlockMaxVar3Subsets = 8000;

    private const int MinBlockMaxVar23SubsetsRgba = 1600;

    private const float HighOrthoEnergyThreshRgba = 16f;

    private const float OrthoRatio23SubsetRatioThreshRgba = 0.004f;

    private static readonly Vector128<int> SwapPairs;

    private static readonly Vector128<int> SwapWithin;

    private const int MaxPatterns2ToCheck = 64;

    private const int MaxPatterns3ToCheck = 64;

    private const float UniquePBitDiscount = 0.85f;

    private const float SharedPBitDiscount = 0.95f;

    private const float SmallFloatValue = 1.25E-05f;

    private static readonly unsafe float* LsTab2Bit;

    private static readonly unsafe float* LsTab3Bit;

    private static readonly unsafe float* LsTab4Bit;

    private static readonly unsafe uint* Part2Bitmasks;

    private static readonly unsafe uint* Part3Bitmasks;

    private static readonly unsafe byte* Mode5OptimalLow;

    private static readonly unsafe byte* Mode5OptimalHigh;

    private const float ProjectionEpsilon = 1.25E-06f;

    private static readonly Vector128<byte> PickR;

    private static readonly Vector128<byte> PickG;

    private static readonly Vector128<byte> PickB;

    private static readonly Vector128<byte> PickA;

    private static readonly Vector128<byte> PackLowBytes;

    private static ReadOnlySpan<byte> Mode5Broadcasts =>
        new byte[64]
        {
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            0,
            1,
            2,
            3,
            4,
            5,
            6,
            7,
            6,
            7,
            2,
            3,
            4,
            5,
            0,
            1,
            6,
            7,
            2,
            3,
            4,
            5,
            0,
            1,
            0,
            1,
            6,
            7,
            4,
            5,
            2,
            3,
            0,
            1,
            6,
            7,
            4,
            5,
            2,
            3,
            0,
            1,
            2,
            3,
            6,
            7,
            4,
            5,
            0,
            1,
            2,
            3,
            6,
            7,
            4,
            5,
        };

    private static ReadOnlySpan<byte> AlphaLaneMasks =>
        new byte[64]
        {
            255,
            255,
            0,
            0,
            0,
            0,
            0,
            0,
            255,
            255,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            255,
            255,
            0,
            0,
            0,
            0,
            0,
            0,
            255,
            255,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            255,
            255,
            0,
            0,
            0,
            0,
            0,
            0,
            255,
            255,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            255,
            255,
            0,
            0,
            0,
            0,
            0,
            0,
            255,
            255,
        };

    private static ReadOnlySpan<byte> Mode5SolidTail =>
        new byte[8] { 172, 170, 170, 170, 0, 0, 0, 0 };

    private static ReadOnlySpan<byte> Partition2 =>
        new byte[1024]
        {
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            1,
            0,
            1,
            1,
            0,
            1,
            0,
            0,
            1,
            0,
            1,
            1,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            0,
            0,
            1,
            0,
            1,
            1,
            0,
            1,
            0,
            0,
            1,
            0,
            1,
            0,
            1,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            0,
            1,
            0,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            1,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            1,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
        };

    private static ReadOnlySpan<byte> Partition3 =>
        new byte[1024]
        {
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            2,
            2,
            1,
            2,
            2,
            2,
            2,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            2,
            1,
            0,
            0,
            0,
            0,
            2,
            0,
            0,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            1,
            1,
            0,
            2,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            2,
            2,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            0,
            1,
            1,
            2,
            0,
            1,
            1,
            2,
            0,
            1,
            1,
            2,
            0,
            1,
            1,
            2,
            0,
            1,
            2,
            2,
            0,
            1,
            2,
            2,
            0,
            1,
            2,
            2,
            0,
            1,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            2,
            1,
            1,
            2,
            2,
            1,
            2,
            2,
            2,
            0,
            0,
            1,
            1,
            2,
            0,
            0,
            1,
            2,
            2,
            0,
            0,
            2,
            2,
            2,
            0,
            0,
            0,
            0,
            1,
            0,
            0,
            1,
            1,
            0,
            1,
            1,
            2,
            1,
            1,
            2,
            2,
            0,
            1,
            1,
            1,
            0,
            0,
            1,
            1,
            2,
            0,
            0,
            1,
            2,
            2,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            2,
            2,
            1,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            2,
            2,
            2,
            0,
            2,
            2,
            2,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            1,
            2,
            2,
            2,
            1,
            2,
            2,
            2,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            1,
            2,
            2,
            0,
            1,
            2,
            2,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            0,
            2,
            2,
            1,
            0,
            2,
            2,
            1,
            0,
            0,
            1,
            2,
            2,
            0,
            1,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            1,
            1,
            2,
            2,
            2,
            2,
            2,
            2,
            0,
            1,
            1,
            0,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            1,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            1,
            0,
            0,
            2,
            2,
            1,
            1,
            0,
            2,
            1,
            1,
            0,
            2,
            0,
            0,
            2,
            2,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            2,
            0,
            0,
            2,
            2,
            2,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            1,
            2,
            2,
            0,
            1,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            0,
            0,
            0,
            2,
            0,
            0,
            0,
            2,
            2,
            1,
            1,
            2,
            2,
            2,
            1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            1,
            1,
            2,
            2,
            1,
            2,
            2,
            2,
            0,
            2,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            2,
            0,
            0,
            2,
            2,
            0,
            2,
            2,
            2,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            0,
            0,
            0,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            0,
            0,
            0,
            0,
            0,
            1,
            2,
            0,
            1,
            2,
            0,
            1,
            2,
            0,
            1,
            2,
            0,
            1,
            2,
            0,
            0,
            1,
            2,
            0,
            2,
            0,
            1,
            2,
            1,
            2,
            0,
            1,
            0,
            1,
            2,
            0,
            0,
            0,
            1,
            1,
            2,
            2,
            0,
            0,
            1,
            1,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            1,
            1,
            2,
            2,
            2,
            2,
            0,
            0,
            0,
            0,
            1,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            1,
            2,
            1,
            2,
            1,
            2,
            1,
            0,
            0,
            2,
            2,
            1,
            1,
            2,
            2,
            0,
            0,
            2,
            2,
            1,
            1,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            0,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            2,
            2,
            0,
            1,
            2,
            2,
            1,
            0,
            2,
            2,
            0,
            1,
            2,
            2,
            1,
            0,
            1,
            0,
            1,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            0,
            1,
            0,
            1,
            0,
            0,
            0,
            0,
            2,
            1,
            2,
            1,
            2,
            1,
            2,
            1,
            2,
            1,
            2,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            0,
            1,
            2,
            2,
            2,
            2,
            0,
            2,
            2,
            2,
            0,
            1,
            1,
            1,
            0,
            2,
            2,
            2,
            0,
            1,
            1,
            1,
            0,
            0,
            0,
            2,
            1,
            1,
            1,
            2,
            0,
            0,
            0,
            2,
            1,
            1,
            1,
            2,
            0,
            0,
            0,
            0,
            2,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            0,
            2,
            2,
            2,
            0,
            1,
            1,
            1,
            0,
            1,
            1,
            1,
            0,
            2,
            2,
            2,
            0,
            0,
            0,
            2,
            1,
            1,
            1,
            2,
            1,
            1,
            1,
            2,
            0,
            0,
            0,
            2,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            2,
            2,
            2,
            2,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            1,
            1,
            0,
            0,
            1,
            1,
            0,
            0,
            2,
            2,
            0,
            0,
            2,
            2,
            1,
            1,
            2,
            2,
            1,
            1,
            2,
            2,
            0,
            0,
            2,
            2,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            2,
            1,
            1,
            2,
            0,
            0,
            0,
            2,
            0,
            0,
            0,
            1,
            0,
            0,
            0,
            2,
            0,
            0,
            0,
            1,
            0,
            2,
            2,
            2,
            1,
            2,
            2,
            2,
            0,
            2,
            2,
            2,
            1,
            2,
            2,
            2,
            0,
            1,
            0,
            1,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            2,
            0,
            1,
            1,
            1,
            2,
            0,
            1,
            1,
            2,
            2,
            0,
            1,
            2,
            2,
            2,
            0,
        };

    private static ReadOnlySpan<byte> AnchorIndexSecondSubset =>
        new byte[64]
        {
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            2,
            8,
            2,
            2,
            8,
            8,
            15,
            2,
            8,
            2,
            2,
            8,
            8,
            2,
            2,
            15,
            15,
            6,
            8,
            2,
            8,
            15,
            15,
            2,
            8,
            2,
            2,
            2,
            15,
            15,
            6,
            6,
            2,
            6,
            8,
            15,
            15,
            2,
            2,
            15,
            15,
            15,
            15,
            15,
            2,
            2,
            15,
        };

    private static ReadOnlySpan<byte> AnchorIndexThirdSubset1 =>
        new byte[64]
        {
            3,
            3,
            15,
            15,
            8,
            3,
            15,
            15,
            8,
            8,
            6,
            6,
            6,
            5,
            3,
            3,
            3,
            3,
            8,
            15,
            3,
            3,
            6,
            10,
            5,
            8,
            8,
            6,
            8,
            5,
            15,
            15,
            8,
            15,
            3,
            5,
            6,
            10,
            8,
            15,
            15,
            3,
            15,
            5,
            15,
            15,
            15,
            15,
            3,
            15,
            5,
            5,
            5,
            8,
            5,
            10,
            5,
            10,
            8,
            13,
            15,
            12,
            3,
            3,
        };

    private static ReadOnlySpan<byte> AnchorIndexThirdSubset2 =>
        new byte[64]
        {
            15,
            8,
            8,
            3,
            15,
            15,
            3,
            8,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            8,
            15,
            8,
            15,
            3,
            15,
            8,
            15,
            8,
            3,
            15,
            6,
            10,
            15,
            15,
            10,
            8,
            15,
            3,
            15,
            10,
            10,
            8,
            9,
            10,
            6,
            15,
            8,
            15,
            3,
            6,
            6,
            8,
            15,
            3,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            15,
            3,
            15,
            15,
            8,
        };

    private static ReadOnlySpan<byte> Weights2 => new byte[4] { 0, 21, 43, 64 };

    private static ReadOnlySpan<byte> Weights3 => new byte[8] { 0, 9, 18, 27, 37, 46, 55, 64 };

    private static ReadOnlySpan<byte> Weights4 =>
        new byte[16] { 0, 4, 9, 13, 17, 21, 26, 30, 34, 38, 43, 47, 51, 55, 60, 64 };

    private static bool Vectorized => Vector128.IsHardwareAccelerated;

    public static void DecodeBc7Block(ReadOnlySpan<byte> block, Span<byte> rgba)
    {
        rgba = rgba.Slice(0, 64);
        ulong num = BinaryPrimitives.ReadUInt64LittleEndian(block);
        ulong high = BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(8));
        switch (BitOperations.TrailingZeroCount((uint)((int)num & 0xFF)))
        {
            case 0:
                DecodeMode0(num, high, rgba);
                break;
            case 1:
                DecodeMode1(num, high, rgba);
                break;
            case 2:
                DecodeMode2(num, high, rgba);
                break;
            case 3:
                DecodeMode3(num, high, rgba);
                break;
            case 4:
                DecodeMode4(num, high, rgba);
                break;
            case 5:
                DecodeMode5(num, high, rgba);
                break;
            case 6:
                DecodeMode6(num, high, rgba);
                break;
            case 7:
                DecodeMode7(num, high, rgba);
                break;
            default:
                rgba.Clear();
                break;
        }
    }

    internal static unsafe bool TrySquaredErrorByRows<TBudget>(
        Vector128<byte> block,
        short* source,
        TBudget budget,
        out int error
    )
        where TBudget : struct, IErrorBudget
    {
        switch (BitOperations.TrailingZeroCount((uint)block.ToScalar()))
        {
            case 5:
                error = Mode5ErrorByRows(block, source, budget);
                return true;
            case 6:
                error = Mode6ErrorByRows(block, source, budget);
                return true;
            default:
                error = 0;
                return false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int Mode5ErrorByRows<TBudget>(
        Vector128<byte> block,
        short* source,
        TBudget budget
    )
        where TBudget : struct, IErrorBudget
    {
        ulong element = block.AsUInt64().GetElement(1);
        int num = (block.ToScalar() >> 6) & 3;
        Vector128<ushort> vector =
            ShiftedFields(block, Mode5FieldWindows, Mode5FieldLifts) & Mode5FieldMasks;
        Vector128<ushort> endpoints = Vector128.ConditionalSelect(
            Mode5ColorLanes,
            (vector << 1) | (vector >>> 6),
            vector
        );
        Vector128<byte> broadcast = Vector128.LoadUnsafe(
            in MemoryMarshal.GetReference(Mode5Broadcasts),
            (nuint)num * (nuint)16u
        );
        (Vector128<short> Origin, Vector128<short> Slope) tuple = BroadcastEndpoints(
            endpoints,
            broadcast
        );
        Vector128<short> item = tuple.Origin;
        Vector128<short> item2 = tuple.Slope;
        Vector128<byte> vector2 = Weights2Vector(
            (uint)InsertAnchor((element >> 2) & 0x7FFFFFFF, 0, 2)
        );
        Vector128<byte> vector3 = Weights2Vector((uint)InsertAnchor(element >> 33, 0, 2));
        Vector128<short> vector4 = Vector128
            .LoadUnsafe(
                in MemoryMarshal.GetReference(AlphaLaneMasks),
                (nuint)AlphaLane(num) * (nuint)16u
            )
            .AsInt16();
        if (Avx2.IsSupported)
        {
            Vector256<short> origins = Vector256.Create(item, item);
            Vector256<short> slopes = Vector256.Create(item2, item2);
            Vector256<byte> value = Vector256.Create(vector2, vector2);
            Vector256<byte> value2 = Vector256.Create(vector3, vector3);
            Vector256<short> condition = Vector256.Create(vector4, vector4);
            int num2 = Vector256.Sum(
                PairSquares(
                    source,
                    origins,
                    slopes,
                    Vector256.ConditionalSelect(
                        condition,
                        Avx2.Shuffle(value2, FirstOfRows01).AsInt16(),
                        Avx2.Shuffle(value, FirstOfRows01).AsInt16()
                    ),
                    Vector256.ConditionalSelect(
                        condition,
                        Avx2.Shuffle(value2, SecondOfRows01).AsInt16(),
                        Avx2.Shuffle(value, SecondOfRows01).AsInt16()
                    )
                )
            );
            if (budget.Exceeded(num2))
            {
                return -1;
            }
            return num2
                + Vector256.Sum(
                    PairSquares(
                        source + 32,
                        origins,
                        slopes,
                        Vector256.ConditionalSelect(
                            condition,
                            Avx2.Shuffle(value2, FirstOfRows23).AsInt16(),
                            Avx2.Shuffle(value, FirstOfRows23).AsInt16()
                        ),
                        Vector256.ConditionalSelect(
                            condition,
                            Avx2.Shuffle(value2, SecondOfRows23).AsInt16(),
                            Avx2.Shuffle(value, SecondOfRows23).AsInt16()
                        )
                    )
                );
        }
        Vector128<int> zero = Vector128<int>.Zero;
        for (int i = 0; i < 4; i++)
        {
            Vector128<byte> vector5 = SpreadOpaque + Vector128.Create((byte)(i * 4));
            Vector128<byte> indices = vector5 + Vector128.Create((byte)2);
            Vector128<short> vector6 = Vector128.ConditionalSelect(
                vector4,
                Vector128.ShuffleNative(vector3, vector5).AsInt16(),
                Vector128.ShuffleNative(vector2, vector5).AsInt16()
            );
            Vector128<short> vector7 = Vector128.ConditionalSelect(
                vector4,
                Vector128.ShuffleNative(vector3, indices).AsInt16(),
                Vector128.ShuffleNative(vector2, indices).AsInt16()
            );
            zero += RowSquares(source, i, item + vector6 * item2, item + vector7 * item2);
            if ((i & 1) != 0 && budget.Exceeded(Vector128.Sum(zero)))
            {
                return -1;
            }
        }
        return Vector128.Sum(zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe int Mode6ErrorByRows<TBudget>(
        Vector128<byte> block,
        short* source,
        TBudget budget
    )
        where TBudget : struct, IErrorBudget
    {
        ulong element = block.AsUInt64().GetElement(1);
        Vector128<ushort> vector =
            ShiftedFields(block, Mode6FieldWindows, Mode6FieldLifts)
            & Vector128.Create((ushort)127);
        Vector128<ushort> vector2 =
            ShiftedFields(block, Mode6PBitWindows, Mode6PBitLifts) & Vector128.Create((ushort)1);
        (Vector128<short> Origin, Vector128<short> Slope) tuple = BroadcastEndpoints(
            (vector << 1) | vector2,
            BroadcastLowHalf
        );
        Vector128<short> item = tuple.Origin;
        Vector128<short> item2 = tuple.Slope;
        Vector128<byte> vector3 = Weights4Vector(InsertAnchor(element >> 1, 0, 4));
        if (Avx2.IsSupported)
        {
            Vector256<short> origins = Vector256.Create(item, item);
            Vector256<short> slopes = Vector256.Create(item2, item2);
            Vector256<byte> value = Vector256.Create(vector3, vector3);
            int num = Vector256.Sum(
                PairSquares(
                    source,
                    origins,
                    slopes,
                    Avx2.Shuffle(value, FirstOfRows01).AsInt16(),
                    Avx2.Shuffle(value, SecondOfRows01).AsInt16()
                )
            );
            if (budget.Exceeded(num))
            {
                return -1;
            }
            return num
                + Vector256.Sum(
                    PairSquares(
                        source + 32,
                        origins,
                        slopes,
                        Avx2.Shuffle(value, FirstOfRows23).AsInt16(),
                        Avx2.Shuffle(value, SecondOfRows23).AsInt16()
                    )
                );
        }
        Vector128<int> zero = Vector128<int>.Zero;
        for (int i = 0; i < 4; i++)
        {
            Vector128<byte> vector4 = SpreadOpaque + Vector128.Create((byte)(i * 4));
            Vector128<short> vector5 = Vector128.ShuffleNative(vector3, vector4).AsInt16();
            Vector128<short> vector6 = Vector128
                .ShuffleNative(vector3, vector4 + Vector128.Create((byte)2))
                .AsInt16();
            zero += RowSquares(source, i, item + vector5 * item2, item + vector6 * item2);
            if ((i & 1) != 0 && budget.Exceeded(Vector128.Sum(zero)))
            {
                return -1;
            }
        }
        return Vector128.Sum(zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> ShiftedFields(
        Vector128<byte> block,
        Vector128<byte> windows,
        Vector128<ushort> lifts
    )
    {
        return Vector128.ShuffleNative(block, windows).AsUInt16() * lifts >>> 8;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (Vector128<short> Origin, Vector128<short> Slope) BroadcastEndpoints(
        Vector128<ushort> endpoints,
        Vector128<byte> broadcast
    )
    {
        Vector128<short> vector = endpoints.AsInt16();
        Vector128<short> vector2 = (vector << 6) + Vector128.Create((short)32);
        return new ValueTuple<Vector128<short>, Vector128<short>>(
            item2: Vector128
                .ShuffleNative(
                    (
                        Vector128.ShuffleNative(vector.AsByte(), SwapHalves).AsInt16() - vector
                    ).AsByte(),
                    broadcast
                )
                .AsInt16(),
            item1: Vector128.ShuffleNative(vector2.AsByte(), broadcast).AsInt16()
        );
    }

    internal static unsafe void WidenRows(byte* source, short* wide)
    {
        for (int i = 0; i < 2; i++)
        {
            Vector128<ulong> vector = Vector128.Load(source + i * 32).AsUInt64();
            Vector128<ulong> vector2 = Vector128.Load(source + i * 32 + 16).AsUInt64();
            var (vector3, vector4) = Vector128.Widen(
                Vector128.Create(vector.ToScalar(), vector2.ToScalar()).AsByte()
            );
            var (vector5, vector6) = Vector128.Widen(
                Vector128.Create(vector.GetElement(1), vector2.GetElement(1)).AsByte()
            );
            vector3.AsInt16().Store(wide + i * 32);
            vector4.AsInt16().Store(wide + i * 32 + 8);
            vector5.AsInt16().Store(wide + i * 32 + 16);
            vector6.AsInt16().Store(wide + i * 32 + 24);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector256<int> PairSquares(
        short* source,
        Vector256<short> origins,
        Vector256<short> slopes,
        Vector256<short> left,
        Vector256<short> right
    )
    {
        Vector256<short> vector =
            Vector256.ShiftRightArithmetic(origins + left * slopes, 6) - Vector256.Load(source);
        Vector256<short> vector2 =
            Vector256.ShiftRightArithmetic(origins + right * slopes, 6)
            - Vector256.Load(source + 16);
        return Avx2.MultiplyAddAdjacent(vector, vector)
            + Avx2.MultiplyAddAdjacent(vector2, vector2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector128<int> RowSquares(
        short* source,
        int row,
        Vector128<short> leftPair,
        Vector128<short> rightPair
    )
    {
        short* ptr = source + (row >> 1) * 32 + (row & 1) * 8;
        Vector128<short> vector = Vector128.ShiftRightArithmetic(leftPair, 6) - Vector128.Load(ptr);
        Vector128<short> vector2 =
            Vector128.ShiftRightArithmetic(rightPair, 6) - Vector128.Load(ptr + 16);
        if (Sse2.IsSupported)
        {
            return Sse2.MultiplyAddAdjacent(vector, vector)
                + Sse2.MultiplyAddAdjacent(vector2, vector2);
        }
        if (AdvSimd.IsSupported)
        {
            return AdvSimd.MultiplyWideningUpperAndAdd(
                AdvSimd.MultiplyWideningLowerAndAdd(
                    AdvSimd.MultiplyWideningUpperAndAdd(
                        AdvSimd.MultiplyWideningLower(vector.GetLower(), vector.GetLower()),
                        vector,
                        vector
                    ),
                    vector2.GetLower(),
                    vector2.GetLower()
                ),
                vector2,
                vector2
            );
        }
        var (vector3, vector4) = Vector128.Widen(vector);
        var (vector5, vector6) = Vector128.Widen(vector2);
        return vector3 * vector3 + vector4 * vector4 + vector5 * vector5 + vector6 * vector6;
    }

    [SkipLocalsInit]
    private static void DecodeMode0(ulong low, ulong high, Span<byte> rgba)
    {
        int num = (int)(low >> 1) & 0xF;
        ulong num2 = Field(low, high, 5, 24);
        ulong num3 = Field(low, high, 29, 24);
        ulong num4 = Field(low, high, 53, 24);
        int num5 = (int)Field(low, high, 77, 6);
        Span<ulong> span = stackalloc ulong[3];
        Span<ulong> span2 = stackalloc ulong[3];
        for (int i = 0; i < 3; i++)
        {
            int num6 = i * 2;
            int num7 = num6 + 1;
            int num8 = (num5 >> num6) & 1;
            int num9 = (num5 >> num7) & 1;
            ref ulong reference = ref span[i];
            ref ulong reference2 = ref span2[i];
            (ulong, ulong) tuple = Endpoints(
                Unquantize5((((int)(num2 >> 4 * num6) & 0xF) << 1) | num8),
                Unquantize5((((int)(num3 >> 4 * num6) & 0xF) << 1) | num8),
                Unquantize5((((int)(num4 >> 4 * num6) & 0xF) << 1) | num8),
                255,
                Unquantize5((((int)(num2 >> 4 * num7) & 0xF) << 1) | num9),
                Unquantize5((((int)(num3 >> 4 * num7) & 0xF) << 1) | num9),
                Unquantize5((((int)(num4 >> 4 * num7) & 0xF) << 1) | num9),
                255
            );
            reference = tuple.Item1;
            reference2 = tuple.Item2;
        }
        ulong window = WidenThreeSubsets(
            high >> 19,
            3,
            AnchorIndexThirdSubset1[num],
            AnchorIndexThirdSubset2[num]
        );
        EmitPartitioned(rgba, span, span2, Partition3.Slice(num * 16, 16), Weights3Vector(window));
    }

    [SkipLocalsInit]
    private static void DecodeMode1(ulong low, ulong high, Span<byte> rgba)
    {
        int num = (int)(low >> 2) & 0x3F;
        ulong num2 = Field(low, high, 8, 24);
        ulong num3 = Field(low, high, 32, 24);
        ulong num4 = Field(low, high, 56, 24);
        int num5 = (int)Field(low, high, 80, 2);
        Span<ulong> span = stackalloc ulong[2];
        Span<ulong> span2 = stackalloc ulong[2];
        for (int i = 0; i < 2; i++)
        {
            int num6 = i * 2;
            int num7 = num6 + 1;
            int num8 = (num5 >> i) & 1;
            ref ulong reference = ref span[i];
            ref ulong reference2 = ref span2[i];
            (ulong, ulong) tuple = Endpoints(
                Unquantize7((((int)(num2 >> 6 * num6) & 0x3F) << 1) | num8),
                Unquantize7((((int)(num3 >> 6 * num6) & 0x3F) << 1) | num8),
                Unquantize7((((int)(num4 >> 6 * num6) & 0x3F) << 1) | num8),
                255,
                Unquantize7((((int)(num2 >> 6 * num7) & 0x3F) << 1) | num8),
                Unquantize7((((int)(num3 >> 6 * num7) & 0x3F) << 1) | num8),
                Unquantize7((((int)(num4 >> 6 * num7) & 0x3F) << 1) | num8),
                255
            );
            reference = tuple.Item1;
            reference2 = tuple.Item2;
        }
        ulong window = WidenTwoSubsets(high >> 18, 3, AnchorIndexSecondSubset[num]);
        EmitPartitioned(rgba, span, span2, Partition2.Slice(num * 16, 16), Weights3Vector(window));
    }

    [SkipLocalsInit]
    private static void DecodeMode2(ulong low, ulong high, Span<byte> rgba)
    {
        int num = (int)(low >> 3) & 0x3F;
        ulong num2 = Field(low, high, 9, 30);
        ulong num3 = Field(low, high, 39, 30);
        ulong num4 = Field(low, high, 69, 30);
        Span<ulong> span = stackalloc ulong[3];
        Span<ulong> span2 = stackalloc ulong[3];
        for (int i = 0; i < 3; i++)
        {
            int num5 = i * 2;
            int num6 = num5 + 1;
            ref ulong reference = ref span[i];
            ref ulong reference2 = ref span2[i];
            (ulong, ulong) tuple = Endpoints(
                Unquantize5((int)(num2 >> 5 * num5) & 0x1F),
                Unquantize5((int)(num3 >> 5 * num5) & 0x1F),
                Unquantize5((int)(num4 >> 5 * num5) & 0x1F),
                255,
                Unquantize5((int)(num2 >> 5 * num6) & 0x1F),
                Unquantize5((int)(num3 >> 5 * num6) & 0x1F),
                Unquantize5((int)(num4 >> 5 * num6) & 0x1F),
                255
            );
            reference = tuple.Item1;
            reference2 = tuple.Item2;
        }
        ulong num7 = WidenThreeSubsets(
            high >> 35,
            2,
            AnchorIndexThirdSubset1[num],
            AnchorIndexThirdSubset2[num]
        );
        EmitPartitioned(
            rgba,
            span,
            span2,
            Partition3.Slice(num * 16, 16),
            Weights2Vector((uint)num7)
        );
    }

    [SkipLocalsInit]
    private static void DecodeMode3(ulong low, ulong high, Span<byte> rgba)
    {
        int num = (int)(low >> 4) & 0x3F;
        ulong num2 = Field(low, high, 10, 28);
        ulong num3 = Field(low, high, 38, 28);
        ulong num4 = Field(low, high, 66, 28);
        int num5 = (int)Field(low, high, 94, 4);
        Span<ulong> span = stackalloc ulong[2];
        Span<ulong> span2 = stackalloc ulong[2];
        for (int i = 0; i < 2; i++)
        {
            int num6 = i * 2;
            int num7 = num6 + 1;
            int num8 = (num5 >> num6) & 1;
            int num9 = (num5 >> num7) & 1;
            ref ulong reference = ref span[i];
            ref ulong reference2 = ref span2[i];
            (ulong, ulong) tuple = Endpoints(
                (((int)(num2 >> 7 * num6) & 0x7F) << 1) | num8,
                (((int)(num3 >> 7 * num6) & 0x7F) << 1) | num8,
                (((int)(num4 >> 7 * num6) & 0x7F) << 1) | num8,
                255,
                (((int)(num2 >> 7 * num7) & 0x7F) << 1) | num9,
                (((int)(num3 >> 7 * num7) & 0x7F) << 1) | num9,
                (((int)(num4 >> 7 * num7) & 0x7F) << 1) | num9,
                255
            );
            reference = tuple.Item1;
            reference2 = tuple.Item2;
        }
        ulong num10 = WidenTwoSubsets(high >> 34, 2, AnchorIndexSecondSubset[num]);
        EmitPartitioned(
            rgba,
            span,
            span2,
            Partition2.Slice(num * 16, 16),
            Weights2Vector((uint)num10)
        );
    }

    [SkipLocalsInit]
    private static void DecodeMode4(ulong low, ulong high, Span<byte> rgba)
    {
        int rotation = (int)(low >> 5) & 3;
        bool flag = ((low >> 7) & 1) != 0;
        int num = (int)Field(low, high, 8, 10);
        int num2 = (int)Field(low, high, 18, 10);
        int num3 = (int)Field(low, high, 28, 10);
        int num4 = (int)Field(low, high, 38, 12);
        int r = Unquantize5(num & 0x1F);
        int r2 = Unquantize5(num >> 5);
        int g = Unquantize5(num2 & 0x1F);
        int g2 = Unquantize5(num2 >> 5);
        int b = Unquantize5(num3 & 0x1F);
        int b2 = Unquantize5(num3 >> 5);
        int a = Unquantize6(num4 & 0x3F);
        int a2 = Unquantize6(num4 >> 6);
        Rotate(rotation, ref r, ref g, ref b, ref a, ref r2, ref g2, ref b2, ref a2);
        Vector128<byte> vector = Weights2Vector((uint)InsertAnchor(Field(low, high, 50, 31), 0, 2));
        Vector128<byte> vector2 = Weights3Vector(InsertAnchor(high >> 17, 0, 3));
        var (originQuad, slopeQuad) = Endpoints(r, g, b, a, r2, g2, b2, a2);
        EmitDualPlane(
            rgba,
            originQuad,
            slopeQuad,
            flag ? vector2 : vector,
            flag ? vector : vector2,
            AlphaLane(rotation)
        );
    }

    [SkipLocalsInit]
    private static void DecodeMode5(ulong low, ulong high, Span<byte> rgba)
    {
        int rotation = (int)(low >> 6) & 3;
        int r = Unquantize7((int)(low >> 8) & 0x7F);
        int r2 = Unquantize7((int)(low >> 15) & 0x7F);
        int g = Unquantize7((int)(low >> 22) & 0x7F);
        int g2 = Unquantize7((int)(low >> 29) & 0x7F);
        int b = Unquantize7((int)(low >> 36) & 0x7F);
        int b2 = Unquantize7((int)(low >> 43) & 0x7F);
        int a = (int)(low >> 50) & 0xFF;
        int a2 = (int)((low >> 58) | (high << 6)) & 0xFF;
        Rotate(rotation, ref r, ref g, ref b, ref a, ref r2, ref g2, ref b2, ref a2);
        var (originQuad, slopeQuad) = Endpoints(r, g, b, a, r2, g2, b2, a2);
        EmitDualPlane(
            rgba,
            originQuad,
            slopeQuad,
            Weights2Vector((uint)InsertAnchor((high >> 2) & 0x7FFFFFFF, 0, 2)),
            Weights2Vector((uint)InsertAnchor(high >> 33, 0, 2)),
            AlphaLane(rotation)
        );
    }

    [SkipLocalsInit]
    private static void DecodeMode6(ulong low, ulong high, Span<byte> rgba)
    {
        int num = (int)(low >> 63) & 1;
        int num2 = (int)high & 1;
        var (originQuad, slopeQuad) = Endpoints(
            (((int)(low >> 7) & 0x7F) << 1) | num,
            (((int)(low >> 21) & 0x7F) << 1) | num,
            (((int)(low >> 35) & 0x7F) << 1) | num,
            (((int)(low >> 49) & 0x7F) << 1) | num,
            (((int)(low >> 14) & 0x7F) << 1) | num2,
            (((int)(low >> 28) & 0x7F) << 1) | num2,
            (((int)(low >> 42) & 0x7F) << 1) | num2,
            (((int)(low >> 56) & 0x7F) << 1) | num2
        );
        EmitSingleSubset(
            rgba,
            originQuad,
            slopeQuad,
            Weights4Vector(InsertAnchor(high >> 1, 0, 4))
        );
    }

    [SkipLocalsInit]
    private static void DecodeMode7(ulong low, ulong high, Span<byte> rgba)
    {
        int num = (int)(low >> 8) & 0x3F;
        ulong num2 = Field(low, high, 14, 20);
        ulong num3 = Field(low, high, 34, 20);
        ulong num4 = Field(low, high, 54, 20);
        ulong num5 = Field(low, high, 74, 20);
        int num6 = (int)Field(low, high, 94, 4);
        Span<ulong> span = stackalloc ulong[2];
        Span<ulong> span2 = stackalloc ulong[2];
        for (int i = 0; i < 2; i++)
        {
            int num7 = i * 2;
            int num8 = num7 + 1;
            int num9 = (num6 >> num7) & 1;
            int num10 = (num6 >> num8) & 1;
            ref ulong reference = ref span[i];
            ref ulong reference2 = ref span2[i];
            (ulong, ulong) tuple = Endpoints(
                Unquantize6((((int)(num2 >> 5 * num7) & 0x1F) << 1) | num9),
                Unquantize6((((int)(num3 >> 5 * num7) & 0x1F) << 1) | num9),
                Unquantize6((((int)(num4 >> 5 * num7) & 0x1F) << 1) | num9),
                Unquantize6((((int)(num5 >> 5 * num7) & 0x1F) << 1) | num9),
                Unquantize6((((int)(num2 >> 5 * num8) & 0x1F) << 1) | num10),
                Unquantize6((((int)(num3 >> 5 * num8) & 0x1F) << 1) | num10),
                Unquantize6((((int)(num4 >> 5 * num8) & 0x1F) << 1) | num10),
                Unquantize6((((int)(num5 >> 5 * num8) & 0x1F) << 1) | num10)
            );
            reference = tuple.Item1;
            reference2 = tuple.Item2;
        }
        ulong num11 = WidenTwoSubsets(high >> 34, 2, AnchorIndexSecondSubset[num]);
        EmitPartitioned(
            rgba,
            span,
            span2,
            Partition2.Slice(num * 16, 16),
            Weights2Vector((uint)num11)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Field(ulong low, ulong high, int position, int width)
    {
        return (
                (position >= 64)
                    ? (high >> position - 64)
                    : (
                        (position + width <= 64)
                            ? (low >> position)
                            : ((low >> position) | (high << 64 - position))
                    )
            ) & (ulong)((1L << width) - 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong InsertAnchor(ulong window, int texel, int bits)
    {
        ulong num = (ulong)((1L << texel * bits + bits - 1) - 1);
        return (window & num) | ((window & ~num) << 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong WidenTwoSubsets(ulong window, int bits, int anchor)
    {
        return InsertAnchor(InsertAnchor(window, 0, bits), anchor, bits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong WidenThreeSubsets(ulong window, int bits, int anchor1, int anchor2)
    {
        return InsertAnchor(
            InsertAnchor(InsertAnchor(window, 0, bits), int.Min(anchor1, anchor2), bits),
            int.Max(anchor1, anchor2),
            bits
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Rotate(
        int rotation,
        ref int r0,
        ref int g0,
        ref int b0,
        ref int a0,
        ref int r1,
        ref int g1,
        ref int b1,
        ref int a1
    )
    {
        switch (rotation)
        {
            case 1:
            {
                int num = r0;
                int num2 = a0;
                int num3 = r1;
                int num4 = a1;
                a0 = num;
                r0 = num2;
                a1 = num3;
                r1 = num4;
                break;
            }
            case 2:
            {
                int num4 = g0;
                int num3 = a0;
                int num2 = g1;
                int num = a1;
                a0 = num4;
                g0 = num3;
                a1 = num2;
                g1 = num;
                break;
            }
            case 3:
            {
                int num = b0;
                int num2 = a0;
                int num3 = b1;
                int num4 = a1;
                a0 = num;
                b0 = num2;
                a1 = num3;
                b1 = num4;
                break;
            }
        }
    }

    private static int AlphaLane(int rotation)
    {
        if (rotation != 0)
        {
            return rotation - 1;
        }
        return 3;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Weights2Vector(uint window)
    {
        Vector128<byte> vector = Vector128.ShuffleNative(
            Vector128.CreateScalar(window).AsByte(),
            QuarterBytes
        );
        Vector128<byte> indices =
            Vector128.ConditionalSelect(
                HighNibbleQuads,
                Vector128.ShiftRightLogical(vector.AsUInt16(), 4).AsByte(),
                vector
            ) & Vector128.Create((byte)15);
        return Vector128.ConditionalSelect(
            OddByteLanes,
            Vector128.ShuffleNative(Weights2ByHighPair, indices),
            Vector128.ShuffleNative(Weights2ByLowPair, indices)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Weights3Vector(ulong window)
    {
        ref ushort arrayDataReference = ref MemoryMarshal.GetArrayDataReference(Weights3Pairs);
        ulong e =
            Unsafe.Add(ref arrayDataReference, (int)window & 0x3F)
            | ((ulong)Unsafe.Add(ref arrayDataReference, (int)(window >> 6) & 0x3F) << 16)
            | ((ulong)Unsafe.Add(ref arrayDataReference, (int)(window >> 12) & 0x3F) << 32)
            | ((ulong)Unsafe.Add(ref arrayDataReference, (int)(window >> 18) & 0x3F) << 48);
        ulong e2 =
            Unsafe.Add(ref arrayDataReference, (int)(window >> 24) & 0x3F)
            | ((ulong)Unsafe.Add(ref arrayDataReference, (int)(window >> 30) & 0x3F) << 16)
            | ((ulong)Unsafe.Add(ref arrayDataReference, (int)(window >> 36) & 0x3F) << 32)
            | ((ulong)Unsafe.Add(ref arrayDataReference, (int)(window >> 42) & 0x3F) << 48);
        return Vector128.Create(e, e2).AsByte();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Weights4Vector(ulong window)
    {
        Vector128<byte> vector = Vector128.Shuffle(
            Vector128.CreateScalar(window).AsByte(),
            NibbleBytes
        );
        Vector128<byte> indices =
            Vector128.ConditionalSelect(
                HighNibbleLanes,
                Vector128.ShiftRightLogical(vector.AsUInt16(), 4).AsByte(),
                vector
            ) & Vector128.Create((byte)15);
        return Vector128.ShuffleNative(Weights4Table, indices);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitSingleSubset(
        Span<byte> rgba,
        ulong originQuad,
        ulong slopeQuad,
        Vector128<byte> weights
    )
    {
        Vector128<short> vector = Vector128.Create(originQuad).AsInt16();
        Vector128<short> vector2 = Vector128.Create(slopeQuad).AsInt16();
        ref byte reference = ref MemoryMarshal.GetReference(rgba);
        for (int i = 0; i < 16; i += 4)
        {
            Vector128<byte> vector3 = Vector128.Create((byte)i);
            Vector128<short> first = Vector128
                .ShuffleNative(weights, SpreadOpaque + vector3)
                .AsInt16();
            Vector128<short> second = Vector128
                .ShuffleNative(weights, SpreadOpaque + vector3 + Vector128.Create((byte)2))
                .AsInt16();
            Store(ref reference, i, vector, vector2, first, vector, vector2, second);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitDualPlane(
        Span<byte> rgba,
        ulong originQuad,
        ulong slopeQuad,
        Vector128<byte> colorWeights,
        Vector128<byte> alphaWeights,
        int alphaLane
    )
    {
        Vector128<short> vector = Vector128.Create(originQuad).AsInt16();
        Vector128<short> vector2 = Vector128.Create(slopeQuad).AsInt16();
        Vector128<short> condition = Vector128
            .LoadUnsafe(
                in MemoryMarshal.GetReference(AlphaLaneMasks),
                (nuint)alphaLane * (nuint)16u
            )
            .AsInt16();
        ref byte reference = ref MemoryMarshal.GetReference(rgba);
        for (int i = 0; i < 16; i += 4)
        {
            Vector128<byte> vector3 = Vector128.Create((byte)i);
            Vector128<byte> vector4 = SpreadOpaque + vector3;
            Vector128<byte> indices = vector4 + Vector128.Create((byte)2);
            Vector128<short> first = Vector128.ConditionalSelect(
                condition,
                Vector128.ShuffleNative(alphaWeights, vector4).AsInt16(),
                Vector128.ShuffleNative(colorWeights, vector4).AsInt16()
            );
            Vector128<short> second = Vector128.ConditionalSelect(
                condition,
                Vector128.ShuffleNative(alphaWeights, indices).AsInt16(),
                Vector128.ShuffleNative(colorWeights, indices).AsInt16()
            );
            Store(ref reference, i, vector, vector2, first, vector, vector2, second);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EmitPartitioned(
        Span<byte> rgba,
        ReadOnlySpan<ulong> origins,
        ReadOnlySpan<ulong> slopes,
        ReadOnlySpan<byte> map,
        Vector128<byte> weights
    )
    {
        ref byte reference = ref MemoryMarshal.GetReference(map);
        ref ulong reference2 = ref MemoryMarshal.GetReference(origins);
        ref ulong reference3 = ref MemoryMarshal.GetReference(slopes);
        ref byte reference4 = ref MemoryMarshal.GetReference(rgba);
        for (int i = 0; i < 16; i += 4)
        {
            Vector128<byte> vector = Vector128.Create((byte)i);
            Vector128<short> first = Vector128
                .ShuffleNative(weights, SpreadOpaque + vector)
                .AsInt16();
            Vector128<short> second = Vector128
                .ShuffleNative(weights, SpreadOpaque + vector + Vector128.Create((byte)2))
                .AsInt16();
            int elementOffset = Unsafe.Add(ref reference, i);
            int elementOffset2 = Unsafe.Add(ref reference, i + 1);
            int elementOffset3 = Unsafe.Add(ref reference, i + 2);
            int elementOffset4 = Unsafe.Add(ref reference, i + 3);
            Store(
                ref reference4,
                i,
                Vector128
                    .Create(
                        Unsafe.Add(ref reference2, elementOffset),
                        Unsafe.Add(ref reference2, elementOffset2)
                    )
                    .AsInt16(),
                Vector128
                    .Create(
                        Unsafe.Add(ref reference3, elementOffset),
                        Unsafe.Add(ref reference3, elementOffset2)
                    )
                    .AsInt16(),
                first,
                Vector128
                    .Create(
                        Unsafe.Add(ref reference2, elementOffset3),
                        Unsafe.Add(ref reference2, elementOffset4)
                    )
                    .AsInt16(),
                Vector128
                    .Create(
                        Unsafe.Add(ref reference3, elementOffset3),
                        Unsafe.Add(ref reference3, elementOffset4)
                    )
                    .AsInt16(),
                second
            );
        }
    }

    private static ushort[] BuildWeights3Pairs()
    {
        ushort[] array = new ushort[64];
        for (int i = 0; i < 64; i++)
        {
            array[i] = (ushort)(Weights3[i & 7] | (Weights3[i >> 3] << 8));
        }
        return array;
    }

    private static int Unquantize5(int value)
    {
        return (value << 3) | (value >> 2);
    }

    private static int Unquantize6(int value)
    {
        return (value << 2) | (value >> 4);
    }

    private static int Unquantize7(int value)
    {
        return (value << 1) | (value >> 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (ulong Origin, ulong Slope) Endpoints(
        int r0,
        int g0,
        int b0,
        int a0,
        int r1,
        int g1,
        int b1,
        int a1
    )
    {
        ulong item = Pack(r0 * 64 + 32, g0 * 64 + 32, b0 * 64 + 32, a0 * 64 + 32);
        ulong item2 = Pack(r1 - r0, g1 - g0, b1 - b0, a1 - a0);
        return (Origin: item, Slope: item2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Pack(int r, int g, int b, int a)
    {
        return (ushort)r
            | ((ulong)(ushort)g << 16)
            | ((ulong)(ushort)b << 32)
            | ((ulong)(ushort)a << 48);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Store(
        ref byte destination,
        int texel,
        Vector128<short> origin,
        Vector128<short> slope,
        Vector128<short> first,
        Vector128<short> secondOrigin,
        Vector128<short> secondSlope,
        Vector128<short> second
    )
    {
        Vector128<short> vector = Vector128.ShiftRightArithmetic(origin + first * slope, 6);
        Vector128
            .Narrow(
                upper: Vector128
                    .ShiftRightArithmetic(secondOrigin + second * secondSlope, 6)
                    .AsUInt16(),
                lower: vector.AsUInt16()
            )
            .StoreUnsafe(ref destination, (nuint)texel * (nuint)4u);
    }

    [SkipLocalsInit]
    public static void DecodeBc1Block(ReadOnlySpan<byte> block, Span<byte> rgba, int pitch = 16)
    {
        rgba = rgba.Slice(0, pitch * 3 + 16);
        Texels(
            BinaryPrimitives.ReadUInt64LittleEndian(block),
            ref MemoryMarshal.GetReference(rgba),
            pitch,
            ref MemoryMarshal.GetArrayDataReference(SelectorShuffles)
        );
    }

    public static void DecodeBc4Block(ReadOnlySpan<byte> block, Span<byte> rgba)
    {
        rgba = rgba.Slice(0, 64);
        (Vector128<ushort> Lower, Vector128<ushort> Upper) tuple = Vector128.Widen(
            DecodeBc4Values(BinaryPrimitives.ReadUInt64LittleEndian(block))
        );
        Vector128<ushort> item = tuple.Lower;
        Vector128<ushort> item2 = tuple.Upper;
        Vector128<uint> vector = Vector128.Create(4278190080u);
        var (vector2, vector3) = Vector128.Widen(item);
        var (vector4, vector5) = Vector128.Widen(item2);
        (vector2 | vector).AsByte().CopyTo(rgba);
        (vector3 | vector).AsByte().CopyTo(rgba.Slice(16));
        (vector4 | vector).AsByte().CopyTo(rgba.Slice(32));
        (vector5 | vector).AsByte().CopyTo(rgba.Slice(48));
    }

    public static void DecodeBc5Block(ReadOnlySpan<byte> block, Span<byte> rgba)
    {
        rgba = rgba.Slice(0, 64);
        Vector128<byte> source = DecodeBc4Values(BinaryPrimitives.ReadUInt64LittleEndian(block));
        Vector128<byte> source2 = DecodeBc4Values(
            BinaryPrimitives.ReadUInt64LittleEndian(block.Slice(8))
        );
        (Vector128<ushort> Lower, Vector128<ushort> Upper) tuple = Vector128.Widen(source);
        Vector128<ushort> item = tuple.Lower;
        Vector128<ushort> item2 = tuple.Upper;
        (Vector128<ushort> Lower, Vector128<ushort> Upper) tuple2 = Vector128.Widen(source2);
        Vector128<ushort> item3 = tuple2.Lower;
        Vector128<ushort> item4 = tuple2.Upper;
        Vector128<uint> vector = Vector128.Create(4278190080u);
        var (vector2, vector3) = Vector128.Widen(item | (item3 << 8));
        var (vector4, vector5) = Vector128.Widen(item2 | (item4 << 8));
        (vector2 | vector).AsByte().CopyTo(rgba);
        (vector3 | vector).AsByte().CopyTo(rgba.Slice(16));
        (vector4 | vector).AsByte().CopyTo(rgba.Slice(32));
        (vector5 | vector).AsByte().CopyTo(rgba.Slice(48));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> DecodeBc4Values(ulong bits)
    {
        Vector128<byte> vector = Vector128
            .CreateScalar(
                Unsafe.Add(
                    ref MemoryMarshal.GetArrayDataReference(Bc4Palettes),
                    (int)(bits & 0xFFFF)
                )
            )
            .AsByte();
        ref uint arrayDataReference = ref MemoryMarshal.GetArrayDataReference(Bc4SelectorSpread);
        ulong num = bits >> 16;
        Vector128<byte> indices = Vector128
            .Create(
                Unsafe.Add(ref arrayDataReference, (int)(num & 0xFFF)),
                Unsafe.Add(ref arrayDataReference, (int)((num >> 12) & 0xFFF)),
                Unsafe.Add(ref arrayDataReference, (int)((num >> 24) & 0xFFF)),
                Unsafe.Add(ref arrayDataReference, (int)(num >> 36))
            )
            .AsByte();
        return Vector128.ShuffleNative(vector, indices);
    }

    private static ulong[] BuildBc4Palettes()
    {
        ulong[] array = new ulong[65536];
        Span<byte> span = stackalloc byte[8];
        for (int i = 0; i < 65536; i++)
        {
            uint num = (uint)(i & 0xFF);
            uint num2 = (uint)i >> 8;
            span[0] = (byte)num;
            span[1] = (byte)num2;
            if (num > num2)
            {
                for (uint num3 = 1u; num3 <= 6; num3++)
                {
                    span[(int)(num3 + 1)] = (byte)((num * (7 - num3) + num2 * num3) / 7);
                }
            }
            else
            {
                for (uint num4 = 1u; num4 <= 4; num4++)
                {
                    span[(int)(num4 + 1)] = (byte)((num * (5 - num4) + num2 * num4) / 5);
                }
                span[6] = 0;
                span[7] = byte.MaxValue;
            }
            array[i] = BinaryPrimitives.ReadUInt64LittleEndian(span);
        }
        return array;
    }

    private static uint[] BuildBc4SelectorSpread()
    {
        uint[] array = new uint[4096];
        for (int i = 0; i < 4096; i++)
        {
            array[i] = (uint)(
                (i & 7) | (((i >> 3) & 7) << 8) | (((i >> 6) & 7) << 16) | (((i >> 9) & 7) << 24)
            );
        }
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (
        Vector128<byte> Row0,
        Vector128<byte> Row1,
        Vector128<byte> Row2,
        Vector128<byte> Row3
    ) DecodeBc1Rows(ulong bits)
    {
        return Bc1Rows(Bc1Palette((uint)bits), (uint)(bits >> 32));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector128<byte> Bc1Palette(uint endpoints)
    {
        return Palette((int)(endpoints & 0xFFFF), (int)(endpoints >> 16));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (
        Vector128<byte> Row0,
        Vector128<byte> Row1,
        Vector128<byte> Row2,
        Vector128<byte> Row3
    ) Bc1Rows(Vector128<byte> palette, uint selectors)
    {
        ref byte arrayDataReference = ref MemoryMarshal.GetArrayDataReference(SelectorShuffles);
        return (
            Row0: Vector128.ShuffleNative(
                palette,
                Vector128.LoadUnsafe(in arrayDataReference, (nuint)(selectors & 0xFF) * (nuint)16u)
            ),
            Row1: Vector128.ShuffleNative(
                palette,
                Vector128.LoadUnsafe(
                    in arrayDataReference,
                    (nuint)((selectors >> 8) & 0xFF) * (nuint)16u
                )
            ),
            Row2: Vector128.ShuffleNative(
                palette,
                Vector128.LoadUnsafe(
                    in arrayDataReference,
                    (nuint)((selectors >> 16) & 0xFF) * (nuint)16u
                )
            ),
            Row3: Vector128.ShuffleNative(
                palette,
                Vector128.LoadUnsafe(in arrayDataReference, (nuint)(selectors >> 24) * (nuint)16u)
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Texels(ulong bits, ref byte destination, int pitch, ref byte masks)
    {
        Vector128<byte> vector = Palette((int)(bits & 0xFFFF), (int)((bits >> 16) & 0xFFFF));
        uint num = (uint)(bits >> 32);
        if (!Vector128.IsHardwareAccelerated)
        {
            for (int i = 0; i < 16; i++)
            {
                Unsafe.WriteUnaligned(
                    ref Unsafe.Add(ref destination, i / 4 * pitch + i % 4 * 4),
                    vector.AsUInt32()[(int)((num >> i * 2) & 3)]
                );
            }
        }
        else
        {
            for (int j = 0; j < 4; j++)
            {
                Vector128
                    .ShuffleNative(
                        vector,
                        Vector128.LoadUnsafe(in masks, (nuint)((num >> j * 8) & 0xFF) * (nuint)16u)
                    )
                    .StoreUnsafe(ref destination, (nuint)(j * pitch));
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> Palette(int c0, int c1)
    {
        Vector128<ushort> vector =
            MultiplyHigh(
                Vector128
                    .ShuffleNative(Vector128.Create(c0 | (c1 << 16)).AsByte(), EndpointSpread)
                    .AsUInt16() & FieldMasks,
                ChannelScales
            ) | AlphaOnes;
        Vector128<ushort> vector2 = Vector128.Shuffle(vector.AsUInt32(), SwapEndpoints).AsUInt16();
        Vector128<ushort> upper = (
            (c0 <= c1)
                ? (
                    (Vector128.ShiftRightLogical(vector + vector2, 1) & FirstEntryLanes)
                    | PunchThroughBlack
                )
                : MultiplyHigh(vector + vector + vector2, Vector128.Create((ushort)21846))
        );
        return Vector128.Narrow(vector, upper);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<ushort> MultiplyHigh(Vector128<ushort> left, Vector128<ushort> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.MultiplyHigh(left, right);
        }
        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.UnzipOdd(
                AdvSimd.MultiplyWideningLower(left.GetLower(), right.GetLower()).AsUInt16(),
                AdvSimd.MultiplyWideningUpper(left, right).AsUInt16()
            );
        }
        return Vector128.Narrow(
            Vector128.ShiftRightLogical(
                Vector128.WidenLower(left) * Vector128.WidenLower(right),
                16
            ),
            Vector128.ShiftRightLogical(
                Vector128.WidenUpper(left) * Vector128.WidenUpper(right),
                16
            )
        );
    }

    private static byte[] BuildSelectorShuffles()
    {
        byte[] array = new byte[4096];
        for (int i = 0; i < 256; i++)
        {
            for (int j = 0; j < 16; j++)
            {
                array[i * 16 + j] = (byte)(((i >> j / 4 * 2) & 3) * 4 + j % 4);
            }
        }
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Sum(Vector128<byte> channel)
    {
        if (Sse2.IsSupported)
        {
            Vector128<int> vector = Sse2.SumAbsoluteDifferences(channel, Vector128<byte>.Zero)
                .AsInt32();
            return vector.GetElement(0) + vector.GetElement(2);
        }
        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd.Arm64.AddAcrossWidening(channel).ToScalar();
        }
        return Vector128.Sum(Vector128.WidenLower(channel))
            + Vector128.Sum(Vector128.WidenUpper(channel));
    }

    private static unsafe void ChannelBounds(byte* pixels, int* mins, int* maxes)
    {
        Vector128<byte> left = Vector128.Load(pixels);
        Vector128<byte> right = Vector128.Load(pixels + 16);
        Vector128<byte> left2 = Vector128.Load(pixels + 32);
        Vector128<byte> right2 = Vector128.Load(pixels + 48);
        Vector128<byte> vector = Vector128.Min(
            Vector128.Min(left, right),
            Vector128.Min(left2, right2)
        );
        Vector128<byte> vector2 = Vector128.Max(
            Vector128.Max(left, right),
            Vector128.Max(left2, right2)
        );
        vector = Vector128.Min(vector, Vector128.Shuffle(vector, Rotate8));
        vector = Vector128.Min(vector, Vector128.Shuffle(vector, Rotate4));
        vector2 = Vector128.Max(vector2, Vector128.Shuffle(vector2, Rotate8));
        vector2 = Vector128.Max(vector2, Vector128.Shuffle(vector2, Rotate4));
        for (int i = 0; i < 4; i++)
        {
            mins[i] = vector.GetElement(i);
            maxes[i] = vector2.GetElement(i);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Centre(
        Vector128<byte> channel,
        int mean,
        out Vector128<short> low,
        out Vector128<short> high
    )
    {
        Vector128<short> vector = Vector128.Create((short)mean);
        low = Vector128.WidenLower(channel).AsInt16() - vector;
        high = Vector128.WidenUpper(channel).AsInt16() - vector;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WidenMask(
        Vector128<byte> mask,
        out Vector128<short> low,
        out Vector128<short> high
    )
    {
        low = Vector128.WidenLower(mask.AsSByte()).AsInt16();
        high = Vector128.WidenUpper(mask.AsSByte()).AsInt16();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Dot(
        Vector128<short> xLow,
        Vector128<short> xHigh,
        Vector128<short> yLow,
        Vector128<short> yHigh
    )
    {
        if (Sse2.IsSupported)
        {
            return Vector128.Sum(
                Sse2.MultiplyAddAdjacent(xLow, yLow) + Sse2.MultiplyAddAdjacent(xHigh, yHigh)
            );
        }
        if (AdvSimd.Arm64.IsSupported)
        {
            return AdvSimd
                .Arm64.AddAcross(
                    AdvSimd.MultiplyWideningUpperAndAdd(
                        AdvSimd.MultiplyWideningLowerAndAdd(
                            AdvSimd.MultiplyWideningUpperAndAdd(
                                AdvSimd.MultiplyWideningLower(xLow.GetLower(), yLow.GetLower()),
                                xLow,
                                yLow
                            ),
                            xHigh.GetLower(),
                            yHigh.GetLower()
                        ),
                        xHigh,
                        yHigh
                    )
                )
                .ToScalar();
        }
        return Vector128.Sum(
            Vector128.WidenLower(xLow) * Vector128.WidenLower(yLow)
                + Vector128.WidenUpper(xLow) * Vector128.WidenUpper(yLow)
                + Vector128.WidenLower(xHigh) * Vector128.WidenLower(yHigh)
                + Vector128.WidenUpper(xHigh) * Vector128.WidenUpper(yHigh)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector128<int> LoadTexel(byte* texel)
    {
        return Vector128
            .WidenLower(Vector128.WidenLower(Vector128.CreateScalar(*(uint*)texel).AsByte()))
            .AsInt32();
    }

    private static unsafe void ChannelStats(byte* pixels, int* sums, int* mins, int* maxes)
    {
        if (!Vectorized)
        {
            for (int i = 0; i < 4; i++)
            {
                sums[i] = 0;
                mins[i] = 255;
                maxes[i] = 0;
            }
            for (int j = 0; j < 16; j++)
            {
                for (int k = 0; k < 4; k++)
                {
                    int num = pixels[j * 4 + k];
                    sums[k] += num;
                    mins[k] = Min(mins[k], num);
                    maxes[k] = Max(maxes[k], num);
                }
            }
        }
        else
        {
            Channels channels = new Channels(pixels);
            *sums = Sum(channels.R);
            sums[1] = Sum(channels.G);
            sums[2] = Sum(channels.B);
            sums[3] = Sum(channels.A);
            ChannelBounds(pixels, mins, maxes);
        }
    }

    private static unsafe void Covariance3(byte* pixels, int meanR, int meanG, int meanB, int* icov)
    {
        if (!Vectorized)
        {
            for (int i = 0; i < 6; i++)
            {
                icov[i] = 0;
            }
            for (int j = 0; j < 16; j++)
            {
                int num = pixels[j * 4] - meanR;
                int num2 = pixels[j * 4 + 1] - meanG;
                int num3 = pixels[j * 4 + 2] - meanB;
                *icov += num * num;
                icov[1] += num * num2;
                icov[2] += num * num3;
                icov[3] += num2 * num2;
                icov[4] += num2 * num3;
                icov[5] += num3 * num3;
            }
        }
        else
        {
            Channels channels = new Channels(pixels);
            Centre(channels.R, meanR, out var low, out var high);
            Centre(channels.G, meanG, out var low2, out var high2);
            Centre(channels.B, meanB, out var low3, out var high3);
            *icov = Dot(low, high, low, high);
            icov[1] = Dot(low, high, low2, high2);
            icov[2] = Dot(low, high, low3, high3);
            icov[3] = Dot(low2, high2, low2, high2);
            icov[4] = Dot(low2, high2, low3, high3);
            icov[5] = Dot(low3, high3, low3, high3);
        }
    }

    private static unsafe void Covariance4(
        byte* pixels,
        int meanR,
        int meanG,
        int meanB,
        int meanA,
        int* icov
    )
    {
        if (!Vectorized)
        {
            for (int i = 0; i < 10; i++)
            {
                icov[i] = 0;
            }
            for (int j = 0; j < 16; j++)
            {
                int num = pixels[j * 4] - meanR;
                int num2 = pixels[j * 4 + 1] - meanG;
                int num3 = pixels[j * 4 + 2] - meanB;
                int num4 = pixels[j * 4 + 3] - meanA;
                *icov += num * num;
                icov[1] += num * num2;
                icov[2] += num * num3;
                icov[3] += num * num4;
                icov[4] += num2 * num2;
                icov[5] += num2 * num3;
                icov[6] += num2 * num4;
                icov[7] += num3 * num3;
                icov[8] += num3 * num4;
                icov[9] += num4 * num4;
            }
        }
        else
        {
            Channels channels = new Channels(pixels);
            Centre(channels.R, meanR, out var low, out var high);
            Centre(channels.G, meanG, out var low2, out var high2);
            Centre(channels.B, meanB, out var low3, out var high3);
            Centre(channels.A, meanA, out var low4, out var high4);
            *icov = Dot(low, high, low, high);
            icov[1] = Dot(low, high, low2, high2);
            icov[2] = Dot(low, high, low3, high3);
            icov[3] = Dot(low, high, low4, high4);
            icov[4] = Dot(low2, high2, low2, high2);
            icov[5] = Dot(low2, high2, low3, high3);
            icov[6] = Dot(low2, high2, low4, high4);
            icov[7] = Dot(low3, high3, low3, high3);
            icov[8] = Dot(low3, high3, low4, high4);
            icov[9] = Dot(low4, high4, low4, high4);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> MembersOf(uint subsetBitmask)
    {
        return Vector128.Equals(
            Vector128.Shuffle(Vector128.CreateScalar(subsetBitmask).AsByte(), SpreadBits)
                & BitWithinByte,
            BitWithinByte
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector128<byte> MembersOf(byte* pattern, int subset)
    {
        return Vector128.Equals(Vector128.Load(pattern), Vector128.Create((byte)subset));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void MaskedSums(
        in Channels channels,
        Vector128<byte> members,
        int* totalR,
        int* totalG,
        int* totalB
    )
    {
        *totalR = Sum(channels.R & members);
        *totalG = Sum(channels.G & members);
        *totalB = Sum(channels.B & members);
    }

    private static unsafe void MaskedCovariance3(
        in Channels channels,
        Vector128<byte> members,
        int meanR,
        int meanG,
        int meanB,
        int* icov
    )
    {
        WidenMask(members, out var low, out var high);
        Centre(channels.R, meanR, out var low2, out var high2);
        Centre(channels.G, meanG, out var low3, out var high3);
        Centre(channels.B, meanB, out var low4, out var high4);
        low2 &= low;
        high2 &= high;
        low3 &= low;
        high3 &= high;
        low4 &= low;
        high4 &= high;
        *icov = Dot(low2, high2, low2, high2);
        icov[1] = Dot(low2, high2, low3, high3);
        icov[2] = Dot(low2, high2, low4, high4);
        icov[3] = Dot(low3, high3, low3, high3);
        icov[4] = Dot(low3, high3, low4, high4);
        icov[5] = Dot(low4, high4, low4, high4);
    }

    private static unsafe void MaskedCovariance4(
        in Channels channels,
        Vector128<byte> members,
        int meanR,
        int meanG,
        int meanB,
        int meanA,
        int* icov
    )
    {
        WidenMask(members, out var low, out var high);
        Centre(channels.R, meanR, out var low2, out var high2);
        Centre(channels.G, meanG, out var low3, out var high3);
        Centre(channels.B, meanB, out var low4, out var high4);
        Centre(channels.A, meanA, out var low5, out var high5);
        low2 &= low;
        high2 &= high;
        low3 &= low;
        high3 &= high;
        low4 &= low;
        high4 &= high;
        low5 &= low;
        high5 &= high;
        *icov = Dot(low2, high2, low2, high2);
        icov[1] = Dot(low2, high2, low3, high3);
        icov[2] = Dot(low2, high2, low4, high4);
        icov[3] = Dot(low2, high2, low5, high5);
        icov[4] = Dot(low3, high3, low3, high3);
        icov[5] = Dot(low3, high3, low4, high4);
        icov[6] = Dot(low3, high3, low5, high5);
        icov[7] = Dot(low4, high4, low4, high4);
        icov[8] = Dot(low4, high4, low5, high5);
        icov[9] = Dot(low5, high5, low5, high5);
    }

    private static unsafe void MaskedExtremes(
        byte* pixels,
        Vector128<byte> members,
        int saxisR,
        int saxisG,
        int saxisB,
        int saxisA,
        bool withAlpha,
        out int lowC,
        out int highC
    )
    {
        Vector128<int> vector = Vector128.Create(saxisR);
        Vector128<int> vector2 = Vector128.Create(saxisG);
        Vector128<int> vector3 = Vector128.Create(saxisB);
        Vector128<int> vector4 = Vector128.Create(saxisA);
        Vector128<int> vector5 = Vector128.Create(int.MaxValue);
        Vector128<int> vector6 = Vector128.Create(int.MinValue);
        Vector128<int> vector7 = Vector128.Create(0, 1, 2, 3);
        Vector128<int> vector8 = Vector128.Create(4);
        Vector128<int>* ptr = stackalloc Vector128<int>[4];
        *ptr = Vector128.Shuffle(members, Broadcast0).AsInt32();
        ptr[1] = Vector128.Shuffle(members, Broadcast4).AsInt32();
        ptr[2] = Vector128.Shuffle(members, Broadcast8).AsInt32();
        ptr[3] = Vector128.Shuffle(members, Broadcast12).AsInt32();
        for (int i = 0; i < 16; i += 4)
        {
            Vector128<byte> vector9 = Vector128.Load(pixels + i * 4);
            Vector128<int> left =
                Vector128.Shuffle(vector9, PickR).AsInt32() * vector
                + Vector128.Shuffle(vector9, PickG).AsInt32() * vector2
                + Vector128.Shuffle(vector9, PickB).AsInt32() * vector3
                + vector7;
            if (withAlpha)
            {
                left += Vector128.Shuffle(vector9, PickA).AsInt32() * vector4;
            }
            Vector128<int> condition = ptr[i / 4];
            vector5 = Vector128.Min(
                vector5,
                Vector128.ConditionalSelect(condition, left, Vector128.Create(int.MaxValue))
            );
            vector6 = Vector128.Max(
                vector6,
                Vector128.ConditionalSelect(condition, left, Vector128.Create(int.MinValue))
            );
            vector7 += vector8;
        }
        lowC = HorizontalMin(vector5) & 0xF;
        highC = HorizontalMax(vector6) & 0xF;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<int> PopCount16x8(Vector256<int> values)
    {
        Vector256<int> vector = Vector256.Create(252645135);
        Vector256<int> vector2 = (
            Avx2.Shuffle(NibblePopCount, (values & vector).AsByte())
            + Avx2.Shuffle(
                NibblePopCount,
                (Vector256.ShiftRightLogical(values, 4) & vector).AsByte()
            )
        ).AsInt32();
        return (vector2 + Vector256.ShiftRightLogical(vector2, 8)) & Vector256.Create(255);
    }

    public static void ValidateFlags(Bc7PackFlags flags)
    {
        if (
            (
                flags
                & (
                    Bc7PackFlags.PartiallyAnalyticalRgb
                    | Bc7PackFlags.PartiallyAnalyticalRgba
                    | Bc7PackFlags.NonAnalyticalRgb
                    | Bc7PackFlags.NonAnalyticalRgba
                    | Bc7PackFlags.AstcCompatible
                    | Bc7PackFlags.DisableRgbDualPlane
                )
            ) != Bc7PackFlags.None
        )
        {
            throw new ArgumentException(
                $"bc7f flags 0x{(uint)(flags & (Bc7PackFlags.PartiallyAnalyticalRgb | Bc7PackFlags.PartiallyAnalyticalRgba | Bc7PackFlags.NonAnalyticalRgb | Bc7PackFlags.NonAnalyticalRgba | Bc7PackFlags.AstcCompatible | Bc7PackFlags.DisableRgbDualPlane)):x} select paths this port leaves out: the partially- and "
                    + "non-analytical modes, ASTC-compatible partitions, and disabling the RGB dual plane",
                "flags"
            );
        }
    }

    public static unsafe void EncodeRows(
        byte* rgba,
        int width,
        int height,
        int rowPitch,
        int firstBlockRow,
        int blockRowCount,
        Bc7PackFlags flags,
        byte* output
    )
    {
        ValidateFlags(flags);
        int num = (width + 3) / 4;
        byte* pixels = stackalloc byte[64];
        for (int i = 0; i < blockRowCount; i++)
        {
            byte* ptr = output + (long)i * (long)num * 16;
            for (int j = 0; j < num; j++)
            {
                GatherBlock(rgba, width, height, rowPitch, j, firstBlockRow + i, pixels);
                PackAuto(ptr + (long)j * 16L, pixels, flags);
            }
        }
    }

    private static unsafe void GatherBlock(
        byte* rgba,
        int width,
        int height,
        int rowPitch,
        int bx,
        int by,
        byte* pixels
    )
    {
        int num = bx * 4;
        int num2 = by * 4;
        if (num + 4 <= width && num2 + 4 <= height)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector128
                    .Load(rgba + (long)(num2 + i) * (long)rowPitch + num * 4)
                    .Store(pixels + i * 16);
            }
            return;
        }
        for (int j = 0; j < 4; j++)
        {
            byte* ptr = rgba + (long)Min(num2 + j, height - 1) * (long)rowPitch;
            for (int k = 0; k < 4; k++)
            {
                *(int*)(pixels + (j * 4 + k) * 4) = *(int*)(ptr + Min(num + k, width - 1) * 4);
            }
        }
    }

    private static unsafe void PackAuto(byte* block, byte* pixels, Bc7PackFlags flags)
    {
        if (
            Vector128.EqualsAll(
                Vector128
                    .Min(
                        Vector128.Min(Vector128.Load(pixels), Vector128.Load(pixels + 16)),
                        Vector128.Min(Vector128.Load(pixels + 32), Vector128.Load(pixels + 48))
                    )
                    .AsUInt32() | Vector128.Create(16777215u),
                Vector128<uint>.AllBitsSet
            )
        )
        {
            PackRgbAnalytical(block, pixels, flags);
        }
        else
        {
            PackRgbaAnalytical(block, pixels, flags);
        }
    }

    private static unsafe bool IsSolid(byte* pixels)
    {
        Vector128<byte> right = Vector128.Create(*(uint*)pixels).AsByte();
        if (
            Vector128.EqualsAll(Vector128.Load(pixels), right)
            && Vector128.EqualsAll(Vector128.Load(pixels + 16), right)
            && Vector128.EqualsAll(Vector128.Load(pixels + 32), right)
        )
        {
            return Vector128.EqualsAll(Vector128.Load(pixels + 48), right);
        }
        return false;
    }

    private static unsafe int PickDualPlaneChannelRgb(int* icov)
    {
        bool flag = *icov > 16;
        bool flag2 = icov[3] > 16;
        bool flag3 = icov[5] > 16;
        int num = (flag ? 1 : 0) + (flag2 ? 1 : 0) + (flag3 ? 1 : 0);
        if (num < 2)
        {
            return -1;
        }
        float num2 = *icov;
        float num3 = icov[3];
        float num4 = icov[5];
        float num5 = ((flag && flag2) ? MathF.Abs((float)icov[1] / MathF.Sqrt(num2 * num3)) : 1f);
        float num6 = ((flag && flag3) ? MathF.Abs((float)icov[2] / MathF.Sqrt(num2 * num4)) : 1f);
        float num7 = ((flag2 && flag3) ? MathF.Abs((float)icov[4] / MathF.Sqrt(num3 * num4)) : 1f);
        if (Min(num5, num6, num7) >= 0.85f)
        {
            return -1;
        }
        if (num == 2)
        {
            return (!flag) ? 1 : 0;
        }
        if (num5 < num7 && num6 < num7)
        {
            return 0;
        }
        if (num5 < num6 && num7 < num6)
        {
            return 1;
        }
        return 2;
    }

    private static unsafe void PackRgbAnalytical(byte* block, byte* pixels, Bc7PackFlags flags)
    {
        if (IsSolid(pixels))
        {
            PackMode5Solid(block, pixels);
            return;
        }
        int* ptr = stackalloc int[4];
        int* ptr2 = stackalloc int[4];
        int* ptr3 = stackalloc int[4];
        ChannelStats(pixels, ptr, ptr2, ptr3);
        int num = *ptr;
        int num2 = ptr[1];
        int num3 = ptr[2];
        int num4 = num + 8 >> 4;
        int num5 = num2 + 8 >> 4;
        int num6 = num3 + 8 >> 4;
        int* ptr4 = stackalloc int[6];
        Covariance3(pixels, num4, num5, num6, ptr4);
        int num7 = Max(*ptr4, ptr4[3], ptr4[5]);
        if (num7 == 0)
        {
            PackMode5Solid(block, pixels);
            return;
        }
        int num8 = -1;
        if ((flags & Bc7PackFlags.UseDualPlaneRgb) != Bc7PackFlags.None && num7 >= 32)
        {
            num8 = PickDualPlaneChannelRgb(ptr4);
        }
        if ((flags & Bc7PackFlags.UseTrivialMode6) != Bc7PackFlags.None && num8 == -1 && num7 < 320)
        {
            PackTrivialMode6Rgb(block, pixels, flags);
            return;
        }
        float* ptr5 = stackalloc float[6];
        for (int i = 0; i < 6; i++)
        {
            ptr5[i] = ptr4[i];
        }
        float num9 = 1f / (float)num7;
        float num10 = num9 * *ptr5;
        float num11 = num9 * ptr5[3];
        float num12 = num9 * ptr5[5];
        float num13 = *ptr5 * num10 + ptr5[1] * num11 + ptr5[2] * num12;
        float num14 = ptr5[1] * num10 + ptr5[3] * num11 + ptr5[4] * num12;
        float num15 = ptr5[2] * num10 + ptr5[4] * num11 + ptr5[5] * num12;
        int* spans = stackalloc int[4] { *ptr3 - *ptr2, ptr3[1] - ptr2[1], ptr3[2] - ptr2[2], 0 };
        bool num16 = (flags & Bc7PackFlags.Use2SubsetsRgb) != Bc7PackFlags.None || num8 >= 0;
        float num17 = 0f;
        float num18 = (num16 ? EstimateSlamToLineSse3D(ptr5, num13, num14, num15, &num17) : 0f);
        float sseEstimateToBeat = (
            num16 ? (num18 + AnalyticalQuantEstimateSse(128, 16, 3, spans, 1f, 16)) : 0f
        );
        if (
            (flags & Bc7PackFlags.Use2SubsetsRgb) != Bc7PackFlags.None
            && num7 >= 1600
            && num17 > 0.004f
            && num18 >= 16f
        )
        {
            if ((flags & Bc7PackFlags.Use3SubsetsRgb) != Bc7PackFlags.None && num7 >= 8000)
            {
                float sseEstimateToBeat2 = 1E+09f;
                if (
                    PackMode0Or2Rgb(
                        block,
                        pixels,
                        num13,
                        num14,
                        num15,
                        num4,
                        num5,
                        num6,
                        sseEstimateToBeat,
                        &sseEstimateToBeat2
                    )
                )
                {
                    byte* ptr6 = stackalloc byte[16];
                    float num19 = 1E+09f;
                    if (
                        PackMode1Or3Rgb(
                            ptr6,
                            pixels,
                            num13,
                            num14,
                            num15,
                            num4,
                            num5,
                            num6,
                            sseEstimateToBeat2,
                            flags,
                            &num19
                        )
                    )
                    {
                        Buffer.MemoryCopy(ptr6, block, 16L, 16L);
                    }
                    return;
                }
            }
            if (
                PackMode1Or3Rgb(
                    block,
                    pixels,
                    num13,
                    num14,
                    num15,
                    num4,
                    num5,
                    num6,
                    sseEstimateToBeat,
                    flags,
                    null
                )
            )
            {
                return;
            }
        }
        if (num8 >= 0 && PackMode4Or5(block, pixels, num8, sseEstimateToBeat, null))
        {
            return;
        }
        (int, int, int) tuple = ScaledAxis(num13, num14, num15);
        FindExtremes(pixels, tuple.Item1, tuple.Item2, tuple.Item3, out var lowC, out var highC);
        byte* ptr7 = stackalloc byte[4];
        byte* ptr8 = stackalloc byte[4];
        uint* ptr9 = stackalloc uint[2];
        float* ptr10 = stackalloc float[4];
        float* ptr11 = stackalloc float[4];
        int num20;
        int num21;
        int lr;
        int lg;
        int lb;
        int hr;
        int hg;
        int hb;
        if ((flags & Bc7PackFlags.PBitOptMode6) != Bc7PackFlags.None)
        {
            *ptr10 = (float)(int)pixels[lowC * 4] * 0.003921569f;
            ptr10[1] = (float)(int)pixels[lowC * 4 + 1] * 0.003921569f;
            ptr10[2] = (float)(int)pixels[lowC * 4 + 2] * 0.003921569f;
            ptr10[3] = 0f;
            *ptr11 = (float)(int)pixels[highC * 4] * 0.003921569f;
            ptr11[1] = (float)(int)pixels[highC * 4 + 1] * 0.003921569f;
            ptr11[2] = (float)(int)pixels[highC * 4 + 2] * 0.003921569f;
            ptr11[3] = 0f;
            DetermineUniquePBits(3, 7, ptr10, ptr11, ptr7, ptr8, ptr9);
            num20 = (int)(*ptr9);
            num21 = (int)ptr9[1];
            lr = *ptr7;
            lg = ptr7[1];
            lb = ptr7[2];
            hr = *ptr8;
            hg = ptr8[1];
            hb = ptr8[2];
        }
        else
        {
            num20 = 1;
            num21 = 1;
            lr = To7(pixels[lowC * 4], num20);
            lg = To7(pixels[lowC * 4 + 1], num20);
            lb = To7(pixels[lowC * 4 + 2], num20);
            hr = To7(pixels[highC * 4], num21);
            hg = To7(pixels[highC * 4 + 1], num21);
            hb = To7(pixels[highC * 4 + 2], num21);
        }
        byte* weights = stackalloc byte[16];
        EvalWeightsMode6Rgb(pixels, weights, lr, lg, lb, hr, hg, hb, num20, num21);
        Vec4 xl = default(Vec4);
        Vec4 xh = default(Vec4);
        if (
            ComputeLeastSquaresEndpoints(
                16,
                weights,
                LsTab4Bit,
                ref xl,
                ref xh,
                pixels,
                num,
                num2,
                num3,
                0f,
                3
            )
        )
        {
            if ((flags & Bc7PackFlags.PBitOptMode6) != Bc7PackFlags.None)
            {
                *ptr10 = xl[0] * 0.003921569f;
                ptr10[1] = xl[1] * 0.003921569f;
                ptr10[2] = xl[2] * 0.003921569f;
                ptr10[3] = 0f;
                *ptr11 = xh[0] * 0.003921569f;
                ptr11[1] = xh[1] * 0.003921569f;
                ptr11[2] = xh[2] * 0.003921569f;
                ptr11[3] = 0f;
                DetermineUniquePBits(3, 7, ptr10, ptr11, ptr7, ptr8, ptr9);
                num20 = (int)(*ptr9);
                num21 = (int)ptr9[1];
                lr = *ptr7;
                lg = ptr7[1];
                lb = ptr7[2];
                hr = *ptr8;
                hg = ptr8[1];
                hb = ptr8[2];
            }
            else
            {
                num20 = 1;
                num21 = 1;
                lr = To7(xl[0], num20);
                lg = To7(xl[1], num20);
                lb = To7(xl[2], num20);
                hr = To7(xh[0], num21);
                hg = To7(xh[1], num21);
                hb = To7(xh[2], num21);
            }
            EvalWeightsMode6Rgb(pixels, weights, lr, lg, lb, hr, hg, hb, num20, num21);
        }
        EncodeMode6RgbaBlock(
            block,
            (uint)lr,
            (uint)lg,
            (uint)lb,
            127u,
            (uint)num20,
            (uint)hr,
            (uint)hg,
            (uint)hb,
            127u,
            (uint)num21,
            weights
        );
    }

    private static unsafe void PackTrivialMode6Rgb(byte* block, byte* pixels, Bc7PackFlags flags)
    {
        FindExtremes(pixels, 32, 64, 16, out var lowC, out var highC);
        int num;
        int num2;
        int lr;
        int lg;
        int lb;
        int hr;
        int hg;
        int hb;
        if ((flags & Bc7PackFlags.PBitOptMode6) != Bc7PackFlags.None)
        {
            float* xl = stackalloc float[4]
            {
                (float)(int)pixels[lowC * 4] * 0.003921569f,
                (float)(int)pixels[lowC * 4 + 1] * 0.003921569f,
                (float)(int)pixels[lowC * 4 + 2] * 0.003921569f,
                0f,
            };
            float* xh = stackalloc float[4]
            {
                (float)(int)pixels[highC * 4] * 0.003921569f,
                (float)(int)pixels[highC * 4 + 1] * 0.003921569f,
                (float)(int)pixels[highC * 4 + 2] * 0.003921569f,
                0f,
            };
            byte* ptr = stackalloc byte[4];
            byte* ptr2 = stackalloc byte[4];
            uint* ptr3 = stackalloc uint[2];
            DetermineUniquePBits(3, 7, xl, xh, ptr, ptr2, ptr3);
            num = (int)(*ptr3);
            num2 = (int)ptr3[1];
            lr = *ptr;
            lg = ptr[1];
            lb = ptr[2];
            hr = *ptr2;
            hg = ptr2[1];
            hb = ptr2[2];
        }
        else
        {
            num = 1;
            num2 = 1;
            lr = To7(pixels[lowC * 4], num);
            lg = To7(pixels[lowC * 4 + 1], num);
            lb = To7(pixels[lowC * 4 + 2], num);
            hr = To7(pixels[highC * 4], num2);
            hg = To7(pixels[highC * 4 + 1], num2);
            hb = To7(pixels[highC * 4 + 2], num2);
        }
        byte* weights = stackalloc byte[16];
        EvalWeightsMode6Rgb(pixels, weights, lr, lg, lb, hr, hg, hb, num, num2);
        EncodeMode6RgbaBlock(
            block,
            (uint)lr,
            (uint)lg,
            (uint)lb,
            127u,
            (uint)num,
            (uint)hr,
            (uint)hg,
            (uint)hb,
            127u,
            (uint)num2,
            weights
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe float AnalyticalQuantEstimateSse(
        int endpointLevels,
        int weightLevels,
        int channels,
        int* spans,
        float endpointWeightScale,
        int pixels
    )
    {
        float num = 1f / (float)(endpointLevels - 1);
        float num2 = 1f / (float)(weightLevels - 1);
        float num3 = weightLevels;
        float num4 = (2f * num3 - 1f) / (3f * (num3 - 1f));
        float num5 = (
            (endpointLevels == 256)
                ? 0f
                : (num * num * (1f / 12f * num4 * 65025f) * (float)channels * endpointWeightScale)
        );
        float num6 = num2 * num2 * (1f / 12f);
        for (int i = 0; i < channels; i++)
        {
            num5 += num6 * (float)(spans[i] * spans[i]);
        }
        return num5 * (float)pixels;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float AnalyticalQuantEstimateSse(
        int endpointLevels,
        int weightLevels,
        int span,
        float endpointWeightScale,
        int pixels
    )
    {
        float num = 1f / (float)(endpointLevels - 1);
        float num2 = 1f / (float)(weightLevels - 1);
        float num3 = weightLevels;
        float num4 = (2f * num3 - 1f) / (3f * (num3 - 1f));
        return (
                (
                    (endpointLevels == 256)
                        ? 0f
                        : (num * num * (1f / 12f * num4 * 65025f) * endpointWeightScale)
                )
                + num2 * num2 * (1f / 12f) * (float)(span * span)
            ) * (float)pixels;
    }

    private static unsafe float EstimateSlamToLineSse3D(
        float* cov,
        float xr,
        float yr,
        float zr,
        float* orthoRatio
    )
    {
        float num = *cov + cov[3] + cov[5];
        float num2 = MathF.Sqrt(xr * xr + yr * yr + zr * zr);
        if (num2 < 1.25E-05f)
        {
            xr = (yr = (zr = 0.57735026f));
        }
        else
        {
            num2 = 1f / num2;
            xr *= num2;
            yr *= num2;
            zr *= num2;
        }
        float num3 = *cov * xr + cov[1] * yr + cov[2] * zr;
        float num4 = cov[1] * xr + cov[3] * yr + cov[4] * zr;
        float num5 = cov[2] * xr + cov[4] * yr + cov[5] * zr;
        float num6 = num3 * xr + num4 * yr + num5 * zr;
        float num7 = Max(0f, num - num6);
        if (orthoRatio != null)
        {
            *orthoRatio = ((num > 1.25E-05f) ? (num7 / num) : 0f);
        }
        return num7;
    }

    private static unsafe float EstimateSlamToLineSse4D(
        float* cov,
        float xr,
        float yr,
        float zr,
        float wr,
        float* orthoRatio
    )
    {
        float num = *cov + cov[4] + cov[7] + cov[9];
        float num2 = MathF.Sqrt(xr * xr + yr * yr + zr * zr + wr * wr);
        if (num2 < 1.25E-05f)
        {
            xr = (yr = (zr = (wr = 0.5f)));
        }
        else
        {
            num2 = 1f / num2;
            xr *= num2;
            yr *= num2;
            zr *= num2;
            wr *= num2;
        }
        float num3 = *cov * xr + cov[1] * yr + cov[2] * zr + cov[3] * wr;
        float num4 = cov[1] * xr + cov[4] * yr + cov[5] * zr + cov[6] * wr;
        float num5 = cov[2] * xr + cov[5] * yr + cov[7] * zr + cov[8] * wr;
        float num6 = cov[3] * xr + cov[6] * yr + cov[8] * zr + cov[9] * wr;
        float num7 = num3 * xr + num4 * yr + num5 * zr + num6 * wr;
        float num8 = Max(0f, num - num7);
        if (orthoRatio != null)
        {
            *orthoRatio = ((num > 1.25E-05f) ? (num8 / num) : 0f);
        }
        return num8;
    }

    private static unsafe void DeterminePBits(
        int totalComps,
        int compBits,
        float* xl,
        float* xh,
        byte* bestMinColor,
        byte* bestMaxColor,
        uint* bestPBits,
        bool shared
    )
    {
        int num = compBits + 1;
        int num2 = (1 << num) - 1;
        float value = num2;
        float num3 = 1E+09f;
        float num4 = 1E+09f;
        float num5 = 1E+09f;
        Vector128<float> vector = Vector128.Load(xl);
        Vector128<float> vector2 = Vector128.Load(xh);
        Vector128<float> vector3 = Vector128.Create(value);
        Vector128<float> vector4 = Vector128.Create(0.5f);
        Vector128<int> vector5 = Vector128.Create(2);
        for (int i = 0; i < 2; i++)
        {
            Vector128<float> vector6 = Vector128.Create((float)i);
            Vector128<int> vector7 = Vector128.Create(i);
            Vector128<int> right = Vector128.Create(num2 - 1 + i);
            Vector128<int> vector8 = Vector128.Min(
                Vector128.Max(
                    Vector128.ConvertToInt32Native((vector * vector3 - vector6) * vector4 + vector4)
                        * vector5
                        + vector7,
                    vector7
                ),
                right
            );
            Vector128<int> vector9 = Vector128.Min(
                Vector128.Max(
                    Vector128.ConvertToInt32Native(
                        (vector2 * vector3 - vector6) * vector4 + vector4
                    ) * vector5
                        + vector7,
                    vector7
                ),
                right
            );
            Vector128<float> vector10 = Vector128.ConvertToSingle(Expand(vector8, num));
            Vector128<float> vector11 = Vector128.ConvertToSingle(Expand(vector9, num));
            if (shared)
            {
                Vector128<float> vector12 = vector10 * Vector128.Create(0.003921569f) - vector;
                Vector128<float> vector13 = vector11 * Vector128.Create(0.003921569f) - vector2;
                float num6 = SumLanes(vector12 * vector12 + vector13 * vector13, totalComps);
                if (!(num6 >= num5))
                {
                    num5 = num6;
                    *bestPBits = (uint)i;
                    bestPBits[1] = (uint)i;
                    Halve(vector8, bestMinColor);
                    Halve(vector9, bestMaxColor);
                }
                continue;
            }
            Vector128<float> vector14 = vector10 - vector * Vector128.Create(255f);
            Vector128<float> vector15 = vector11 - vector2 * Vector128.Create(255f);
            float num7 = SumLanes(vector14 * vector14, totalComps);
            float num8 = SumLanes(vector15 * vector15, totalComps);
            if (num7 < num3)
            {
                num3 = num7;
                *bestPBits = (uint)i;
                Halve(vector8, bestMinColor);
            }
            if (num8 < num4)
            {
                num4 = num8;
                bestPBits[1] = (uint)i;
                Halve(vector9, bestMaxColor);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<int> Expand(Vector128<int> value, int totalBits)
    {
        Vector128<int> vector = Vector128.ShiftLeft(value, 8 - totalBits) & Vector128.Create(255);
        return vector | Vector128.ShiftRightLogical(vector, totalBits);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SumLanes(Vector128<float> v, int count)
    {
        float num = v.GetElement(0) + v.GetElement(1) + v.GetElement(2);
        if (count != 4)
        {
            return num;
        }
        return num + v.GetElement(3);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void Halve(Vector128<int> color, byte* destination)
    {
        Vector128<int> vector = Vector128.ShiftRightArithmetic(color, 1);
        for (int i = 0; i < 4; i++)
        {
            destination[i] = (byte)vector.GetElement(i);
        }
    }

    private static unsafe void DetermineUniquePBits(
        int totalComps,
        int compBits,
        float* xl,
        float* xh,
        byte* bestMinColor,
        byte* bestMaxColor,
        uint* bestPBits
    )
    {
        DeterminePBits(
            totalComps,
            compBits,
            xl,
            xh,
            bestMinColor,
            bestMaxColor,
            bestPBits,
            shared: false
        );
    }

    private static unsafe void DetermineSharedPBits(
        int totalComps,
        int compBits,
        float* xl,
        float* xh,
        byte* bestMinColor,
        byte* bestMaxColor,
        uint* bestPBits
    )
    {
        DeterminePBits(
            totalComps,
            compBits,
            xl,
            xh,
            bestMinColor,
            bestMaxColor,
            bestPBits,
            shared: true
        );
    }

    private static int Distance3(int lr, int lg, int lb, int hr, int hg, int hb)
    {
        return Square(hr - lr) + Square(hg - lg) + Square(hb - lb);
    }

    private static unsafe bool DetermineThreeSubsets(
        byte* result,
        byte* pixels,
        float blockXr,
        float blockXg,
        float blockXb,
        int blockMeanR,
        int blockMeanG,
        int blockMeanB
    )
    {
        int* ptr = stackalloc int[16];
        int* ptr2 = stackalloc int[6];
        int* ptr3 = stackalloc int[2];
        uint num = SplitAlongAxis(
            pixels,
            blockXr,
            blockXg,
            blockXb,
            0f,
            blockMeanR,
            blockMeanG,
            blockMeanB,
            0,
            withAlpha: false
        );
        for (int i = 0; i < 16; i++)
        {
            int num2 = (ptr[i] = (int)((num >> i) & 1));
            ptr2[num2 * 3] += pixels[i * 4];
            ptr2[num2 * 3 + 1] += pixels[i * 4 + 1];
            ptr2[num2 * 3 + 2] += pixels[i * 4 + 2];
            ptr3[num2]++;
        }
        for (int j = 0; j < 2; j++)
        {
            int num3 = ptr3[j];
            if (num3 == 0)
            {
                return false;
            }
            ptr2[j * 3] = (ptr2[j * 3] + (num3 >> 1)) / num3;
            ptr2[j * 3 + 1] = (ptr2[j * 3 + 1] + (num3 >> 1)) / num3;
            ptr2[j * 3 + 2] = (ptr2[j * 3 + 2] + (num3 >> 1)) / num3;
        }
        int* ptr4 = stackalloc int[2];
        for (int k = 0; k < 16; k++)
        {
            int num4 = ptr[k];
            ptr4[num4] += Distance3(
                pixels[k * 4],
                pixels[k * 4 + 1],
                pixels[k * 4 + 2],
                ptr2[num4 * 3],
                ptr2[num4 * 3 + 1],
                ptr2[num4 * 3 + 2]
            );
        }
        int num5 = ((ptr4[1] > *ptr4) ? 1 : 0);
        if (ptr3[num5] < 2)
        {
            return false;
        }
        int num6 = int.MaxValue;
        int num7 = 0;
        for (int l = 0; l < 16; l++)
        {
            if (ptr[l] == num5)
            {
                int b = (pixels[l * 4] + pixels[l * 4 + 1] + pixels[l * 4 + 2] << 4) + l;
                num6 = Min(num6, b);
                num7 = Max(num7, b);
            }
        }
        int num8 = num6 & 0xF;
        int num9 = num7 & 0xF;
        if (num8 == num9)
        {
            return false;
        }
        int lr = pixels[num8 * 4];
        int lg = pixels[num8 * 4 + 1];
        int lb = pixels[num8 * 4 + 2];
        int lr2 = pixels[num9 * 4];
        int lg2 = pixels[num9 * 4 + 1];
        int lb2 = pixels[num9 * 4 + 2];
        new Span<byte>(result, 16).Fill(2);
        for (int m = 0; m < 16; m++)
        {
            if (ptr[m] == num5)
            {
                int num10 = Distance3(
                    lr,
                    lg,
                    lb,
                    pixels[m * 4],
                    pixels[m * 4 + 1],
                    pixels[m * 4 + 2]
                );
                int num11 = Distance3(
                    lr2,
                    lg2,
                    lb2,
                    pixels[m * 4],
                    pixels[m * 4 + 1],
                    pixels[m * 4 + 2]
                );
                result[m] = ((num11 > num10) ? ((byte)1) : ((byte)0));
            }
        }
        return true;
    }

    private static int PopCount16(uint x)
    {
        return PopCount32(x & 0xFFFF);
    }

    private static unsafe int PickThreeSubsetPatternIndex(
        byte* desiredSubsets,
        out int bestPatternIndexFirst16
    )
    {
        bestPatternIndexFirst16 = 0;
        uint* ptr = stackalloc uint[3];
        for (int i = 0; i < 16; i++)
        {
            ptr[(int)desiredSubsets[i]] |= (uint)(1 << i);
        }
        int num = PopCount16(*ptr);
        int num2 = PopCount16(ptr[1]);
        int num3 = 16 - num - num2;
        int num4 = -1;
        int num5 = 0;
        int* ptr2 = stackalloc int[64];
        if (Avx2.IsSupported)
        {
            ScorePatterns3(ptr, num, num2, num3, ptr2);
        }
        for (int j = 0; j < 64; j++)
        {
            int num6;
            if (Avx2.IsSupported)
            {
                num6 = ptr2[j];
            }
            else
            {
                uint num7 = Part3Bitmasks[j] & 0xFFFF;
                uint num8 = Part3Bitmasks[j] >> 16;
                int num9 = PopCount16(*ptr & num7);
                int num10 = PopCount16(*ptr & num8);
                int num11 = num - num9 - num10;
                int num12 = PopCount16(ptr[1] & num7);
                int num13 = PopCount16(ptr[1] & num8);
                int num14 = num2 - num12 - num13;
                int num15 = PopCount16(ptr[2] & num7);
                int num16 = PopCount16(ptr[2] & num8);
                int num17 = num3 - num15 - num16;
                num6 = num9 + num13 + num17;
                num6 = Max(num6, num9 + num14 + num16);
                num6 = Max(num6, num10 + num12 + num17);
                num6 = Max(num6, num10 + num14 + num15);
                num6 = Max(num6, num11 + num12 + num16);
                num6 = Max(num6, num11 + num13 + num15);
            }
            if (num6 > num4)
            {
                num4 = num6;
                num5 = j;
                if (num6 == 16)
                {
                    if (j <= 15)
                    {
                        bestPatternIndexFirst16 = num5;
                    }
                    break;
                }
            }
            if (j == 15)
            {
                bestPatternIndexFirst16 = num5;
            }
        }
        return num5;
    }

    private static unsafe void ScorePatterns3(uint* m, int n0, int n1, int n2, int* scores)
    {
        Vector256<int> vector = Vector256.Create((int)(*m));
        Vector256<int> vector2 = Vector256.Create((int)m[1]);
        Vector256<int> vector3 = Vector256.Create((int)m[2]);
        Vector256<int> vector4 = Vector256.Create(n0);
        Vector256<int> vector5 = Vector256.Create(n1);
        Vector256<int> vector6 = Vector256.Create(n2);
        Vector256<int> vector7 = Vector256.Create(65535);
        for (int i = 0; i < 64; i += 8)
        {
            Vector256<int> vector8 = Vector256.Load((int*)(Part3Bitmasks + i));
            Vector256<int> vector9 = vector8 & vector7;
            Vector256<int> vector10 = Vector256.ShiftRightLogical(vector8, 16);
            Vector256<int> vector11 = PopCount16x8(vector & vector9);
            Vector256<int> vector12 = PopCount16x8(vector & vector10);
            Vector256<int> vector13 = vector4 - vector11 - vector12;
            Vector256<int> vector14 = PopCount16x8(vector2 & vector9);
            Vector256<int> vector15 = PopCount16x8(vector2 & vector10);
            Vector256<int> vector16 = vector5 - vector14 - vector15;
            Vector256<int> vector17 = PopCount16x8(vector3 & vector9);
            Vector256<int> vector18 = PopCount16x8(vector3 & vector10);
            Vector256<int> vector19 = vector6 - vector17 - vector18;
            Vector256
                .Max(
                    Vector256.Max(
                        Vector256.Max(
                            Vector256.Max(
                                Vector256.Max(
                                    vector11 + vector15 + vector19,
                                    vector11 + vector16 + vector18
                                ),
                                vector12 + vector14 + vector19
                            ),
                            vector12 + vector16 + vector17
                        ),
                        vector13 + vector14 + vector18
                    ),
                    vector13 + vector15 + vector17
                )
                .Store(scores + i);
        }
    }

    private static unsafe bool PackMode0Or2Rgb(
        byte* block,
        byte* pixels,
        float blockXr,
        float blockXg,
        float blockXb,
        int blockMeanR,
        int blockMeanG,
        int blockMeanB,
        float sseEstimateToBeat,
        float* finalSseEstimate
    )
    {
        byte* ptr = stackalloc byte[16];
        if (
            !DetermineThreeSubsets(
                ptr,
                pixels,
                blockXr,
                blockXg,
                blockXb,
                blockMeanR,
                blockMeanG,
                blockMeanB
            )
        )
        {
            *finalSseEstimate = 1E+09f;
            return false;
        }
        int* ptr2 = stackalloc int[2];
        ptr2[1] = PickThreeSubsetPatternIndex(ptr, out var bestPatternIndexFirst);
        *ptr2 = bestPatternIndexFirst;
        float* ptr3 = stackalloc float[2];
        float* ptr4 = stackalloc float[2];
        int* ptr5 = stackalloc int[6];
        int* ptr6 = stackalloc int[6];
        int* ptr7 = stackalloc int[6];
        int* ptr8 = stackalloc int[6];
        int* ptr9 = stackalloc int[6];
        int* ptr10 = stackalloc int[6];
        int* ptr11 = stackalloc int[4];
        Vector128<byte>* ptr12 = stackalloc Vector128<byte>[3];
        int* ptr13 = stackalloc int[3];
        int* ptr14 = stackalloc int[3];
        int* ptr15 = stackalloc int[3];
        int* ptr16 = stackalloc int[18];
        int* ptr17 = stackalloc int[3];
        int* ptr18 = stackalloc int[3];
        int* ptr19 = stackalloc int[3];
        float* ptr20 = stackalloc float[6];
        Channels channels = new Channels(pixels);
        fixed (byte* ptr21 = Partition3)
        {
            for (int i = 0; i < 2; i++)
            {
                if (i != 0 && *ptr2 == ptr2[1])
                {
                    for (int j = 0; j < 3; j++)
                    {
                        ptr5[3 + j] = ptr5[j];
                        ptr6[3 + j] = ptr6[j];
                        ptr7[3 + j] = ptr7[j];
                        ptr8[3 + j] = ptr8[j];
                        ptr9[3 + j] = ptr9[j];
                        ptr10[3 + j] = ptr10[j];
                    }
                    ptr4[1] = *ptr4;
                }
                else
                {
                    byte* pattern = ptr21 + ptr2[i] * 16;
                    int* ptr22 = ptr8 + i * 3;
                    int* ptr23 = ptr9 + i * 3;
                    int* ptr24 = ptr10 + i * 3;
                    int* ptr25 = ptr5 + i * 3;
                    for (int k = 0; k < 3; k++)
                    {
                        ptr12[k] = MembersOf(pattern, k);
                        ptr25[k] = PopCount32(ptr12[k].ExtractMostSignificantBits());
                        MaskedSums(in channels, ptr12[k], ptr22 + k, ptr23 + k, ptr24 + k);
                    }
                    for (int l = 0; l < 3; l++)
                    {
                        int num = ptr25[l];
                        int num2 = num >> 1;
                        ptr13[l] = (ptr22[l] + num2) / num;
                        ptr14[l] = (ptr23[l] + num2) / num;
                        ptr15[l] = (ptr24[l] + num2) / num;
                    }
                    for (int m = 0; m < 3; m++)
                    {
                        MaskedCovariance3(
                            in channels,
                            ptr12[m],
                            ptr13[m],
                            ptr14[m],
                            ptr15[m],
                            ptr16 + m * 6
                        );
                    }
                    float num3 = 0f;
                    for (int n = 0; n < 3; n++)
                    {
                        int* ptr26 = ptr16 + n * 6;
                        int num4 = Max(*ptr26, ptr26[3], ptr26[5]);
                        for (int num5 = 0; num5 < 6; num5++)
                        {
                            ptr20[num5] = ptr26[num5];
                        }
                        float num6 = 1f / ((float)num4 + 1.25E-05f);
                        float num7 = num6 * *ptr20;
                        float num8 = num6 * ptr20[3];
                        float num9 = num6 * ptr20[5];
                        float num10 = *ptr20 * num7 + ptr20[1] * num8 + ptr20[2] * num9;
                        float num11 = ptr20[1] * num7 + ptr20[3] * num8 + ptr20[4] * num9;
                        float num12 = ptr20[2] * num7 + ptr20[4] * num8 + ptr20[5] * num9;
                        num3 += EstimateSlamToLineSse3D(ptr20, num10, num11, num12, null);
                        (int, int, int) tuple = ScaledAxis(num10, num11, num12);
                        ptr17[n] = tuple.Item1;
                        ptr18[n] = tuple.Item2;
                        ptr19[n] = tuple.Item3;
                    }
                    ptr4[i] = num3;
                    for (int num13 = 0; num13 < 3; num13++)
                    {
                        MaskedExtremes(
                            pixels,
                            ptr12[num13],
                            ptr17[num13],
                            ptr18[num13],
                            ptr19[num13],
                            0,
                            withAlpha: false,
                            out var lowC,
                            out var highC
                        );
                        ptr6[i * 3 + num13] = lowC;
                        ptr7[i * 3 + num13] = highC;
                    }
                }
                for (int num14 = 0; num14 < 3; num14++)
                {
                    int num15 = ptr6[i * 3 + num14];
                    int num16 = ptr7[i * 3 + num14];
                    for (int num17 = 0; num17 < 3; num17++)
                    {
                        ptr11[num17] = pixels[num16 * 4 + num17] - pixels[num15 * 4 + num17];
                    }
                    ptr3[i] += (
                        (i == 0)
                            ? AnalyticalQuantEstimateSse(
                                16,
                                8,
                                3,
                                ptr11,
                                0.85f,
                                ptr5[i * 3 + num14]
                            )
                            : AnalyticalQuantEstimateSse(32, 4, 3, ptr11, 1f, ptr5[i * 3 + num14])
                    );
                }
            }
            float num18 = *ptr3 + *ptr4;
            float num19 = ptr3[1] + ptr4[1];
            uint* ptr27 = stackalloc uint[3];
            uint* ptr28 = stackalloc uint[3];
            uint* ptr29 = stackalloc uint[3];
            uint* ptr30 = stackalloc uint[3];
            uint* ptr31 = stackalloc uint[3];
            uint* ptr32 = stackalloc uint[3];
            byte* weights = stackalloc byte[16];
            float* z = stackalloc float[3];
            float* z2 = stackalloc float[3];
            float* z3 = stackalloc float[3];
            float* q00r = stackalloc float[3];
            float* q00g = stackalloc float[3];
            float* q00b = stackalloc float[3];
            if (num18 < num19)
            {
                *finalSseEstimate = num18;
                if (num18 >= sseEstimateToBeat)
                {
                    return false;
                }
                int num20 = *ptr2;
                byte* pattern2 = ptr21 + num20 * 16;
                int* ptr33 = ptr6;
                int* ptr34 = ptr7;
                int* totalR = ptr8;
                int* totalG = ptr9;
                int* totalB = ptr10;
                float* ptr35 = stackalloc float[12];
                float* ptr36 = stackalloc float[12];
                for (int num21 = 0; num21 < 3; num21++)
                {
                    int num22 = ptr33[num21];
                    int num23 = ptr34[num21];
                    ptr35[num21 * 4] = (float)(int)pixels[num22 * 4] * 0.003921569f;
                    ptr35[num21 * 4 + 1] = (float)(int)pixels[num22 * 4 + 1] * 0.003921569f;
                    ptr35[num21 * 4 + 2] = (float)(int)pixels[num22 * 4 + 2] * 0.003921569f;
                    ptr35[num21 * 4 + 3] = 0f;
                    ptr36[num21 * 4] = (float)(int)pixels[num23 * 4] * 0.003921569f;
                    ptr36[num21 * 4 + 1] = (float)(int)pixels[num23 * 4 + 1] * 0.003921569f;
                    ptr36[num21 * 4 + 2] = (float)(int)pixels[num23 * 4 + 2] * 0.003921569f;
                    ptr36[num21 * 4 + 3] = 0f;
                }
                uint* ptr37 = stackalloc uint[6];
                byte* ptr38 = stackalloc byte[4];
                byte* ptr39 = stackalloc byte[4];
                for (int num24 = 0; num24 < 3; num24++)
                {
                    DetermineUniquePBits(
                        3,
                        4,
                        ptr35 + num24 * 4,
                        ptr36 + num24 * 4,
                        ptr38,
                        ptr39,
                        ptr37 + (num24 << 1)
                    );
                    ptr27[num24] = *ptr38;
                    ptr28[num24] = ptr38[1];
                    ptr29[num24] = ptr38[2];
                    ptr30[num24] = *ptr39;
                    ptr31[num24] = ptr39[1];
                    ptr32[num24] = ptr39[2];
                }
                EvalWeightsMode0Rgb(
                    pixels,
                    weights,
                    ptr27,
                    ptr28,
                    ptr29,
                    ptr30,
                    ptr31,
                    ptr32,
                    ptr37,
                    num20
                );
                AccumulateSubsetMoments3(
                    pixels,
                    weights,
                    pattern2,
                    LsTab3Bit,
                    z,
                    z2,
                    z3,
                    q00r,
                    q00g,
                    q00b
                );
                for (int num25 = 0; num25 < 3; num25++)
                {
                    if (
                        SolveSubset(
                            num25,
                            totalR,
                            totalG,
                            totalB,
                            z,
                            z2,
                            z3,
                            q00r,
                            q00g,
                            q00b,
                            out var slr,
                            out var slg,
                            out var slb,
                            out var shr,
                            out var shg,
                            out var shb
                        )
                    )
                    {
                        ptr35[num25 * 4] = Clamp(0.003921569f * slr, 0f, 1f);
                        ptr36[num25 * 4] = Clamp(0.003921569f * shr, 0f, 1f);
                        ptr35[num25 * 4 + 1] = Clamp(0.003921569f * slg, 0f, 1f);
                        ptr36[num25 * 4 + 1] = Clamp(0.003921569f * shg, 0f, 1f);
                        ptr35[num25 * 4 + 2] = Clamp(0.003921569f * slb, 0f, 1f);
                        ptr36[num25 * 4 + 2] = Clamp(0.003921569f * shb, 0f, 1f);
                    }
                }
                for (int num26 = 0; num26 < 3; num26++)
                {
                    DetermineUniquePBits(
                        3,
                        4,
                        ptr35 + num26 * 4,
                        ptr36 + num26 * 4,
                        ptr38,
                        ptr39,
                        ptr37 + (num26 << 1)
                    );
                    ptr27[num26] = *ptr38;
                    ptr28[num26] = ptr38[1];
                    ptr29[num26] = ptr38[2];
                    ptr30[num26] = *ptr39;
                    ptr31[num26] = ptr39[1];
                    ptr32[num26] = ptr39[2];
                }
                EvalWeightsMode0Rgb(
                    pixels,
                    weights,
                    ptr27,
                    ptr28,
                    ptr29,
                    ptr30,
                    ptr31,
                    ptr32,
                    ptr37,
                    num20
                );
                EncodeMode0RgbBlock(
                    block,
                    num20,
                    ptr27,
                    ptr28,
                    ptr29,
                    ptr30,
                    ptr31,
                    ptr32,
                    ptr37,
                    weights
                );
            }
            else
            {
                *finalSseEstimate = num19;
                if (num19 >= sseEstimateToBeat)
                {
                    return false;
                }
                int num27 = ptr2[1];
                byte* pattern3 = ptr21 + num27 * 16;
                int* ptr40 = ptr6 + 3;
                int* ptr41 = ptr7 + 3;
                int* totalR2 = ptr8 + 3;
                int* totalG2 = ptr9 + 3;
                int* totalB2 = ptr10 + 3;
                for (int num28 = 0; num28 < 3; num28++)
                {
                    int num29 = ptr40[num28];
                    int num30 = ptr41[num28];
                    ptr27[num28] = (uint)To5(pixels[num29 * 4]);
                    ptr28[num28] = (uint)To5(pixels[num29 * 4 + 1]);
                    ptr29[num28] = (uint)To5(pixels[num29 * 4 + 2]);
                    ptr30[num28] = (uint)To5(pixels[num30 * 4]);
                    ptr31[num28] = (uint)To5(pixels[num30 * 4 + 1]);
                    ptr32[num28] = (uint)To5(pixels[num30 * 4 + 2]);
                }
                EvalWeightsMode2Rgb(
                    pixels,
                    weights,
                    ptr27,
                    ptr28,
                    ptr29,
                    ptr30,
                    ptr31,
                    ptr32,
                    num27
                );
                AccumulateSubsetMoments3(
                    pixels,
                    weights,
                    pattern3,
                    LsTab2Bit,
                    z,
                    z2,
                    z3,
                    q00r,
                    q00g,
                    q00b
                );
                for (int num31 = 0; num31 < 3; num31++)
                {
                    if (
                        SolveSubset(
                            num31,
                            totalR2,
                            totalG2,
                            totalB2,
                            z,
                            z2,
                            z3,
                            q00r,
                            q00g,
                            q00b,
                            out var slr2,
                            out var slg2,
                            out var slb2,
                            out var shr2,
                            out var shg2,
                            out var shb2
                        )
                    )
                    {
                        ptr30[num31] = (uint)To5Clamp(shr2);
                        ptr27[num31] = (uint)To5Clamp(slr2);
                        ptr31[num31] = (uint)To5Clamp(shg2);
                        ptr28[num31] = (uint)To5Clamp(slg2);
                        ptr32[num31] = (uint)To5Clamp(shb2);
                        ptr29[num31] = (uint)To5Clamp(slb2);
                    }
                }
                EvalWeightsMode2Rgb(
                    pixels,
                    weights,
                    ptr27,
                    ptr28,
                    ptr29,
                    ptr30,
                    ptr31,
                    ptr32,
                    num27
                );
                EncodeMode2RgbBlock(
                    block,
                    num27,
                    ptr27,
                    ptr28,
                    ptr29,
                    ptr30,
                    ptr31,
                    ptr32,
                    weights
                );
            }
        }
        return true;
    }

    private static unsafe void AccumulateSubsetMoments3(
        byte* pixels,
        byte* weights,
        byte* pattern,
        float* lsTab,
        float* z00,
        float* z10,
        float* z11,
        float* q00r,
        float* q00g,
        float* q00b
    )
    {
        Vector128<float>* ptr = stackalloc Vector128<float>[3];
        Vector128<float>* ptr2 = stackalloc Vector128<float>[3];
        for (int i = 0; i < 3; i++)
        {
            ptr[i] = Vector128<float>.Zero;
            ptr2[i] = Vector128<float>.Zero;
        }
        for (int j = 0; j < 16; j++)
        {
            byte b = pattern[j];
            Vector128<float> vector = Vector128.Load(lsTab + weights[j] * 4);
            ptr[(int)b] += vector;
            ptr2[(int)b] +=
                Vector128.Create(vector.GetElement(3))
                * Vector128.ConvertToSingle(LoadTexel(pixels + j * 4));
        }
        for (int k = 0; k < 3; k++)
        {
            z00[k] = ptr[k].GetElement(0);
            z10[k] = ptr[k].GetElement(1);
            z11[k] = ptr[k].GetElement(2);
            q00r[k] = ptr2[k].GetElement(0);
            q00g[k] = ptr2[k].GetElement(1);
            q00b[k] = ptr2[k].GetElement(2);
        }
    }

    private static unsafe bool PackMode1Or3Rgb(
        byte* block,
        byte* pixels,
        float blockXr,
        float blockXg,
        float blockXb,
        int blockMeanR,
        int blockMeanG,
        int blockMeanB,
        float sseEstimateToBeat,
        Bc7PackFlags flags,
        float* finalSseEstimate = null
    )
    {
        int num = BestPattern2(
            SplitAlongAxis(
                pixels,
                blockXr,
                blockXg,
                blockXb,
                0f,
                blockMeanR,
                blockMeanG,
                blockMeanB,
                0,
                withAlpha: false
            )
        );
        uint num2 = Part2Bitmasks[num];
        Channels channels = new Channels(pixels);
        Vector128<byte>* ptr = stackalloc Vector128<byte>[2];
        ptr[1] = MembersOf(num2);
        *ptr = ~ptr[1];
        int* ptr2 = stackalloc int[2];
        int* ptr3 = stackalloc int[2];
        int* ptr4 = stackalloc int[2];
        int* ptr5 = stackalloc int[2];
        ptr5[1] = PopCount32(num2 & 0xFFFF);
        *ptr5 = 16 - ptr5[1];
        MaskedSums(in channels, *ptr, ptr2, ptr3, ptr4);
        MaskedSums(in channels, ptr[1], ptr2 + 1, ptr3 + 1, ptr4 + 1);
        int* ptr6 = stackalloc int[2];
        int* ptr7 = stackalloc int[2];
        int* ptr8 = stackalloc int[2];
        for (int i = 0; i < 2; i++)
        {
            int num3 = ptr5[i];
            int num4 = num3 >> 1;
            ptr6[i] = (ptr2[i] + num4) / num3;
            ptr7[i] = (ptr3[i] + num4) / num3;
            ptr8[i] = (ptr4[i] + num4) / num3;
        }
        int* ptr9 = stackalloc int[12];
        for (int j = 0; j < 2; j++)
        {
            MaskedCovariance3(in channels, ptr[j], ptr6[j], ptr7[j], ptr8[j], ptr9 + j * 6);
        }
        int* ptr10 = stackalloc int[2];
        int* ptr11 = stackalloc int[2];
        int* ptr12 = stackalloc int[2];
        float* ptr13 = stackalloc float[6];
        float num5 = 0f;
        for (int k = 0; k < 2; k++)
        {
            int* ptr14 = ptr9 + k * 6;
            int num6 = Max(*ptr14, ptr14[3], ptr14[5]);
            for (int l = 0; l < 6; l++)
            {
                ptr13[l] = ptr14[l];
            }
            float num7 = 1f / ((float)num6 + 1.25E-05f);
            float num8 = num7 * *ptr13;
            float num9 = num7 * ptr13[3];
            float num10 = num7 * ptr13[5];
            float num11 = *ptr13 * num8 + ptr13[1] * num9 + ptr13[2] * num10;
            float num12 = ptr13[1] * num8 + ptr13[3] * num9 + ptr13[4] * num10;
            float num13 = ptr13[2] * num8 + ptr13[4] * num9 + ptr13[5] * num10;
            num5 += EstimateSlamToLineSse3D(ptr13, num11, num12, num13, null);
            (int, int, int) tuple = ScaledAxis(num11, num12, num13);
            ptr10[k] = tuple.Item1;
            ptr11[k] = tuple.Item2;
            ptr12[k] = tuple.Item3;
        }
        int* ptr15 = stackalloc int[2];
        int* ptr16 = stackalloc int[2];
        for (int m = 0; m < 2; m++)
        {
            MaskedExtremes(
                pixels,
                ptr[m],
                ptr10[m],
                ptr11[m],
                ptr12[m],
                0,
                withAlpha: false,
                out ptr15[m],
                out ptr16[m]
            );
        }
        int* ptr17 = stackalloc int[4];
        ptr17[3] = 0;
        float num14 = 0f;
        float num15 = 0f;
        for (int n = 0; n < 2; n++)
        {
            int num16 = ptr15[n];
            int num17 = ptr16[n];
            for (int num18 = 0; num18 < 3; num18++)
            {
                ptr17[num18] = pixels[num17 * 4 + num18] - pixels[num16 * 4 + num18];
            }
            num14 += AnalyticalQuantEstimateSse(
                64,
                8,
                3,
                ptr17,
                ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None) ? 0.85f : 1f,
                ptr5[n]
            );
            num15 += AnalyticalQuantEstimateSse(
                128,
                4,
                3,
                ptr17,
                ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None) ? 0.95f : 1f,
                ptr5[n]
            );
        }
        float num19 = num5 + num14;
        float num20 = num5 + num15;
        uint* ptr18 = stackalloc uint[2];
        uint* ptr19 = stackalloc uint[2];
        uint* ptr20 = stackalloc uint[2];
        uint* ptr21 = stackalloc uint[2];
        uint* ptr22 = stackalloc uint[2];
        uint* ptr23 = stackalloc uint[2];
        byte* weights = stackalloc byte[16];
        byte* ptr24 = stackalloc byte[4];
        byte* ptr25 = stackalloc byte[4];
        uint* ptr26 = stackalloc uint[2];
        float* ptr27 = stackalloc float[4];
        float* ptr28 = stackalloc float[4];
        float* z = stackalloc float[2];
        float* z2 = stackalloc float[2];
        float* z3 = stackalloc float[2];
        float* q00r = stackalloc float[2];
        float* q00g = stackalloc float[2];
        float* q00b = stackalloc float[2];
        if (num19 < num20)
        {
            if (finalSseEstimate != null)
            {
                *finalSseEstimate = num19;
            }
            if (num19 >= sseEstimateToBeat)
            {
                return false;
            }
            uint* ptr29 = stackalloc uint[2] { 0u, 0u };
            for (int num21 = 0; num21 < 2; num21++)
            {
                int num22 = ptr15[num21];
                int num23 = ptr16[num21];
                if ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None)
                {
                    *ptr27 = (float)(int)pixels[num22 * 4] * 0.003921569f;
                    ptr27[1] = (float)(int)pixels[num22 * 4 + 1] * 0.003921569f;
                    ptr27[2] = (float)(int)pixels[num22 * 4 + 2] * 0.003921569f;
                    ptr27[3] = 0f;
                    *ptr28 = (float)(int)pixels[num23 * 4] * 0.003921569f;
                    ptr28[1] = (float)(int)pixels[num23 * 4 + 1] * 0.003921569f;
                    ptr28[2] = (float)(int)pixels[num23 * 4 + 2] * 0.003921569f;
                    ptr28[3] = 0f;
                    DetermineSharedPBits(3, 6, ptr27, ptr28, ptr24, ptr25, ptr26);
                    ptr29[num21] = *ptr26;
                    ptr18[num21] = *ptr24;
                    ptr19[num21] = ptr24[1];
                    ptr20[num21] = ptr24[2];
                    ptr21[num21] = *ptr25;
                    ptr22[num21] = ptr25[1];
                    ptr23[num21] = ptr25[2];
                }
                else
                {
                    int a = pixels[num22 * 4] + pixels[num22 * 4 + 1] + pixels[num22 * 4 + 2];
                    int b = pixels[num23 * 4] + pixels[num23 * 4 + 1] + pixels[num23 * 4 + 2];
                    if (Max(a, b) >= 387)
                    {
                        ptr29[num21] = 1u;
                    }
                    ptr18[num21] = (uint)To6(pixels[num22 * 4], (int)ptr29[num21]);
                    ptr19[num21] = (uint)To6(pixels[num22 * 4 + 1], (int)ptr29[num21]);
                    ptr20[num21] = (uint)To6(pixels[num22 * 4 + 2], (int)ptr29[num21]);
                    ptr21[num21] = (uint)To6(pixels[num23 * 4], (int)ptr29[num21]);
                    ptr22[num21] = (uint)To6(pixels[num23 * 4 + 1], (int)ptr29[num21]);
                    ptr23[num21] = (uint)To6(pixels[num23 * 4 + 2], (int)ptr29[num21]);
                }
            }
            EvalWeightsMode1Rgb(
                pixels,
                weights,
                ptr18,
                ptr19,
                ptr20,
                ptr21,
                ptr22,
                ptr23,
                ptr29,
                num2
            );
            AccumulateSubsetMoments(
                pixels,
                weights,
                num2,
                LsTab3Bit,
                z,
                z2,
                z3,
                q00r,
                q00g,
                q00b,
                null
            );
            for (int num24 = 0; num24 < 2; num24++)
            {
                if (
                    SolveSubset(
                        num24,
                        ptr2,
                        ptr3,
                        ptr4,
                        z,
                        z2,
                        z3,
                        q00r,
                        q00g,
                        q00b,
                        out var slr,
                        out var slg,
                        out var slb,
                        out var shr,
                        out var shg,
                        out var shb
                    )
                )
                {
                    if ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None)
                    {
                        *ptr27 = Clamp(slr * 0.003921569f, 0f, 1f);
                        ptr27[1] = Clamp(slg * 0.003921569f, 0f, 1f);
                        ptr27[2] = Clamp(slb * 0.003921569f, 0f, 1f);
                        ptr27[3] = 0f;
                        *ptr28 = Clamp(shr * 0.003921569f, 0f, 1f);
                        ptr28[1] = Clamp(shg * 0.003921569f, 0f, 1f);
                        ptr28[2] = Clamp(shb * 0.003921569f, 0f, 1f);
                        ptr28[3] = 0f;
                        DetermineSharedPBits(3, 6, ptr27, ptr28, ptr24, ptr25, ptr26);
                        ptr29[num24] = *ptr26;
                        ptr18[num24] = *ptr24;
                        ptr19[num24] = ptr24[1];
                        ptr20[num24] = ptr24[2];
                        ptr21[num24] = *ptr25;
                        ptr22[num24] = ptr25[1];
                        ptr23[num24] = ptr25[2];
                    }
                    else
                    {
                        float a2 = slr + slg + slb;
                        float b2 = shr + shg + shb;
                        ptr29[num24] = ((Max(a2, b2) >= 387f) ? 1u : 0u);
                        ptr18[num24] = (uint)To6Clamp(slr, (int)ptr29[num24]);
                        ptr21[num24] = (uint)To6Clamp(shr, (int)ptr29[num24]);
                        ptr19[num24] = (uint)To6Clamp(slg, (int)ptr29[num24]);
                        ptr22[num24] = (uint)To6Clamp(shg, (int)ptr29[num24]);
                        ptr20[num24] = (uint)To6Clamp(slb, (int)ptr29[num24]);
                        ptr23[num24] = (uint)To6Clamp(shb, (int)ptr29[num24]);
                    }
                }
            }
            EvalWeightsMode1Rgb(
                pixels,
                weights,
                ptr18,
                ptr19,
                ptr20,
                ptr21,
                ptr22,
                ptr23,
                ptr29,
                num2
            );
            EncodeMode1RgbBlock(
                block,
                num,
                ptr18,
                ptr19,
                ptr20,
                ptr21,
                ptr22,
                ptr23,
                *ptr29,
                ptr29[1],
                weights
            );
        }
        else
        {
            if (finalSseEstimate != null)
            {
                *finalSseEstimate = num20;
            }
            if (num20 >= sseEstimateToBeat)
            {
                return false;
            }
            uint* ptr30 = stackalloc uint[4];
            for (int num25 = 0; num25 < 2; num25++)
            {
                int num26 = ptr15[num25];
                int num27 = ptr16[num25];
                if ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None)
                {
                    *ptr27 = (float)(int)pixels[num26 * 4] * 0.003921569f;
                    ptr27[1] = (float)(int)pixels[num26 * 4 + 1] * 0.003921569f;
                    ptr27[2] = (float)(int)pixels[num26 * 4 + 2] * 0.003921569f;
                    ptr27[3] = 0f;
                    *ptr28 = (float)(int)pixels[num27 * 4] * 0.003921569f;
                    ptr28[1] = (float)(int)pixels[num27 * 4 + 1] * 0.003921569f;
                    ptr28[2] = (float)(int)pixels[num27 * 4 + 2] * 0.003921569f;
                    ptr28[3] = 0f;
                    DetermineUniquePBits(3, 7, ptr27, ptr28, ptr24, ptr25, ptr26);
                    ptr30[num25 * 2] = *ptr26;
                    ptr30[num25 * 2 + 1] = ptr26[1];
                    ptr18[num25] = *ptr24;
                    ptr19[num25] = ptr24[1];
                    ptr20[num25] = ptr24[2];
                    ptr21[num25] = *ptr25;
                    ptr22[num25] = ptr25[1];
                    ptr23[num25] = ptr25[2];
                }
                else
                {
                    int pbit = (int)(
                        ptr30[num25 * 2] = (
                            (
                                pixels[num26 * 4] + pixels[num26 * 4 + 1] + pixels[num26 * 4 + 2]
                                >= 129
                            )
                                ? 1u
                                : 0u
                        )
                    );
                    ptr18[num25] = (uint)To7(pixels[num26 * 4], pbit);
                    ptr19[num25] = (uint)To7(pixels[num26 * 4 + 1], pbit);
                    ptr20[num25] = (uint)To7(pixels[num26 * 4 + 2], pbit);
                    int pbit2 = (int)(
                        ptr30[num25 * 2 + 1] = (
                            (
                                pixels[num27 * 4] + pixels[num27 * 4 + 1] + pixels[num27 * 4 + 2]
                                >= 129
                            )
                                ? 1u
                                : 0u
                        )
                    );
                    ptr21[num25] = (uint)To7(pixels[num27 * 4], pbit2);
                    ptr22[num25] = (uint)To7(pixels[num27 * 4 + 1], pbit2);
                    ptr23[num25] = (uint)To7(pixels[num27 * 4 + 2], pbit2);
                }
            }
            EvalWeightsMode3Rgb(
                pixels,
                weights,
                ptr18,
                ptr19,
                ptr20,
                ptr21,
                ptr22,
                ptr23,
                ptr30,
                num2
            );
            AccumulateSubsetMoments(
                pixels,
                weights,
                num2,
                LsTab2Bit,
                z,
                z2,
                z3,
                q00r,
                q00g,
                q00b,
                null
            );
            for (int num28 = 0; num28 < 2; num28++)
            {
                if (
                    SolveSubset(
                        num28,
                        ptr2,
                        ptr3,
                        ptr4,
                        z,
                        z2,
                        z3,
                        q00r,
                        q00g,
                        q00b,
                        out var slr2,
                        out var slg2,
                        out var slb2,
                        out var shr2,
                        out var shg2,
                        out var shb2
                    )
                )
                {
                    if ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None)
                    {
                        *ptr27 = Clamp(slr2 * 0.003921569f, 0f, 1f);
                        ptr27[1] = Clamp(slg2 * 0.003921569f, 0f, 1f);
                        ptr27[2] = Clamp(slb2 * 0.003921569f, 0f, 1f);
                        ptr27[3] = 0f;
                        *ptr28 = Clamp(shr2 * 0.003921569f, 0f, 1f);
                        ptr28[1] = Clamp(shg2 * 0.003921569f, 0f, 1f);
                        ptr28[2] = Clamp(shb2 * 0.003921569f, 0f, 1f);
                        ptr28[3] = 0f;
                        DetermineUniquePBits(3, 7, ptr27, ptr28, ptr24, ptr25, ptr26);
                        ptr30[num28 * 2] = *ptr26;
                        ptr30[num28 * 2 + 1] = ptr26[1];
                        ptr18[num28] = *ptr24;
                        ptr19[num28] = ptr24[1];
                        ptr20[num28] = ptr24[2];
                        ptr21[num28] = *ptr25;
                        ptr22[num28] = ptr25[1];
                        ptr23[num28] = ptr25[2];
                    }
                    else
                    {
                        int pbit3 = (int)(
                            ptr30[num28 * 2] = ((slr2 + slg2 + slb2 >= 387f) ? 1u : 0u)
                        );
                        ptr18[num28] = (uint)To7Clamp(slr2, pbit3);
                        ptr19[num28] = (uint)To7Clamp(slg2, pbit3);
                        ptr20[num28] = (uint)To7Clamp(slb2, pbit3);
                        int pbit4 = (int)(
                            ptr30[num28 * 2 + 1] = ((shr2 + shg2 + shb2 >= 387f) ? 1u : 0u)
                        );
                        ptr21[num28] = (uint)To7Clamp(shr2, pbit4);
                        ptr22[num28] = (uint)To7Clamp(shg2, pbit4);
                        ptr23[num28] = (uint)To7Clamp(shb2, pbit4);
                    }
                }
            }
            EvalWeightsMode3Rgb(
                pixels,
                weights,
                ptr18,
                ptr19,
                ptr20,
                ptr21,
                ptr22,
                ptr23,
                ptr30,
                num2
            );
            EncodeMode3RgbBlock(
                block,
                num,
                ptr18,
                ptr19,
                ptr20,
                ptr21,
                ptr22,
                ptr23,
                ptr30,
                weights
            );
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe uint BetterPattern2(uint bestDiff, byte pattern, uint desiredPatBits)
    {
        int num = PopCount32(Part2Bitmasks[(int)pattern] ^ desiredPatBits);
        uint num2 = (uint)((Min(num, 16 - num) << 8) | pattern);
        if (num2 >= bestDiff)
        {
            return bestDiff;
        }
        return num2;
    }

    private static unsafe int BestPattern2(uint desiredPatBits)
    {
        if (!Avx2.IsSupported)
        {
            uint num = uint.MaxValue;
            for (int i = 0; i < 64; i++)
            {
                num = BetterPattern2(num, (byte)i, desiredPatBits);
            }
            return (int)(num & 0xFF);
        }
        Vector256<int> vector = Vector256.Create((int)desiredPatBits);
        Vector256<int> vector2 = Vector256.Create(16);
        Vector256<int> vector3 = Vector256.Create(0, 1, 2, 3, 4, 5, 6, 7);
        Vector256<int> vector4 = Vector256.Create(8);
        Vector256<int> vector5 = Vector256.Create(int.MaxValue);
        for (int j = 0; j < 64; j += 8)
        {
            Vector256<int> vector6 = PopCount16x8(
                Vector256.Load((int*)(Part2Bitmasks + j)) ^ vector
            );
            Vector256<int> vector7 = Vector256.Min(vector6, vector2 - vector6);
            vector5 = Vector256.Min(vector5, Vector256.ShiftLeft(vector7, 8) | vector3);
            vector3 += vector4;
        }
        return HorizontalMin(Vector128.Min(vector5.GetLower(), vector5.GetUpper())) & 0xFF;
    }

    private static unsafe void AccumulateSubsetMoments(
        byte* pixels,
        byte* weights,
        uint patBits,
        float* lsTab,
        float* z00,
        float* z10,
        float* z11,
        float* q00r,
        float* q00g,
        float* q00b,
        float* q00a = null
    )
    {
        Vector128<float>* ptr = stackalloc Vector128<float>[2];
        Vector128<float>* ptr2 = stackalloc Vector128<float>[2];
        *ptr = Vector128<float>.Zero;
        ptr[1] = Vector128<float>.Zero;
        *ptr2 = Vector128<float>.Zero;
        ptr2[1] = Vector128<float>.Zero;
        for (int i = 0; i < 16; i++)
        {
            int num = (int)((patBits >> i) & 1);
            Vector128<float> vector = Vector128.Load(lsTab + weights[i] * 4);
            ptr[num] += vector;
            ptr2[num] +=
                Vector128.Create(vector.GetElement(3))
                * Vector128.ConvertToSingle(LoadTexel(pixels + i * 4));
        }
        for (int j = 0; j < 2; j++)
        {
            z00[j] = ptr[j].GetElement(0);
            z10[j] = ptr[j].GetElement(1);
            z11[j] = ptr[j].GetElement(2);
            q00r[j] = ptr2[j].GetElement(0);
            q00g[j] = ptr2[j].GetElement(1);
            q00b[j] = ptr2[j].GetElement(2);
            if (q00a != null)
            {
                q00a[j] = ptr2[j].GetElement(3);
            }
        }
    }

    private static unsafe uint SplitAlongAxis(
        byte* pixels,
        float xr,
        float xg,
        float xb,
        float xa,
        int meanR,
        int meanG,
        int meanB,
        int meanA,
        bool withAlpha
    )
    {
        Vector128<float> vector = Vector128.Create(xr);
        Vector128<float> vector2 = Vector128.Create(xg);
        Vector128<float> vector3 = Vector128.Create(xb);
        Vector128<float> vector4 = Vector128.Create(xa);
        Vector128<int> vector5 = Vector128.Create(meanR);
        Vector128<int> vector6 = Vector128.Create(meanG);
        Vector128<int> vector7 = Vector128.Create(meanB);
        Vector128<int> vector8 = Vector128.Create(meanA);
        uint num = 0u;
        for (int i = 0; i < 16; i += 4)
        {
            Vector128<byte> vector9 = Vector128.Load(pixels + i * 4);
            Vector128<float> left =
                Vector128.ConvertToSingle(Vector128.Shuffle(vector9, PickR).AsInt32() - vector5)
                    * vector
                + Vector128.ConvertToSingle(Vector128.Shuffle(vector9, PickG).AsInt32() - vector6)
                    * vector2
                + Vector128.ConvertToSingle(Vector128.Shuffle(vector9, PickB).AsInt32() - vector7)
                    * vector3;
            if (withAlpha)
            {
                left +=
                    Vector128.ConvertToSingle(Vector128.Shuffle(vector9, PickA).AsInt32() - vector8)
                    * vector4;
            }
            num |=
                Vector128.GreaterThan(left, Vector128<float>.Zero).ExtractMostSignificantBits()
                << i;
        }
        return num;
    }

    private static unsafe bool SolveSubset(
        int s,
        int* totalR,
        int* totalG,
        int* totalB,
        float* z00,
        float* z10,
        float* z11,
        float* q00r,
        float* q00g,
        float* q00b,
        out float slr,
        out float slg,
        out float slb,
        out float shr,
        out float shg,
        out float shb
    )
    {
        slr = (slg = (slb = (shr = (shg = (shb = 0f)))));
        float num = (float)totalR[s] - q00r[s];
        float num2 = (float)totalG[s] - q00g[s];
        float num3 = (float)totalB[s] - q00b[s];
        if (!Invert2x2(z00[s], z10[s], z11[s], out var iz, out var iz2, out var iz3, out var iz4))
        {
            return false;
        }
        shr = iz * q00r[s] + iz2 * num;
        slr = iz3 * q00r[s] + iz4 * num;
        shg = iz * q00g[s] + iz2 * num2;
        slg = iz3 * q00g[s] + iz4 * num2;
        shb = iz * q00b[s] + iz2 * num3;
        slb = iz3 * q00b[s] + iz4 * num3;
        return true;
    }

    private static unsafe bool PackMode4Or5(
        byte* block,
        byte* originalPixels,
        int dpChannel,
        float sseEstimateToBeat,
        float* finalSseEstimate = null
    )
    {
        byte* ptr = stackalloc byte[64];
        byte* ptr2 = originalPixels;
        if (dpChannel != 3)
        {
            Buffer.MemoryCopy(originalPixels, ptr, 64L, 64L);
            ptr2 = ptr;
            for (int i = 0; i < 16; i++)
            {
                ref byte reference = ref ptr[i * 4 + dpChannel];
                byte* num = ptr + (i * 4 + 3);
                byte b = ptr[i * 4 + 3];
                byte b2 = ptr[i * 4 + dpChannel];
                reference = b;
                *num = b2;
            }
        }
        int* ptr3 = stackalloc int[4];
        int* ptr4 = stackalloc int[4];
        int* ptr5 = stackalloc int[4];
        ChannelStats(ptr2, ptr3, ptr4, ptr5);
        int num2 = *ptr3;
        int num3 = ptr3[1];
        int num4 = ptr3[2];
        int num5 = ptr3[3];
        int num6 = ptr4[3];
        int num7 = ptr5[3];
        int meanR = num2 + 8 >> 4;
        int meanG = num3 + 8 >> 4;
        int meanB = num4 + 8 >> 4;
        int* ptr6 = stackalloc int[6];
        Covariance3(ptr2, meanR, meanG, meanB, ptr6);
        float* ptr7 = stackalloc float[6];
        for (int j = 0; j < 6; j++)
        {
            ptr7[j] = ptr6[j];
        }
        int num8 = Max(*ptr6, ptr6[3], ptr6[5]);
        float num9 = ((num8 != 0) ? (1f / (float)num8) : 0f);
        float num10 = num9 * *ptr7;
        float num11 = num9 * ptr7[3];
        float num12 = num9 * ptr7[5];
        float num13 = *ptr7 * num10 + ptr7[1] * num11 + ptr7[2] * num12;
        float num14 = ptr7[1] * num10 + ptr7[3] * num11 + ptr7[4] * num12;
        float num15 = ptr7[2] * num10 + ptr7[4] * num11 + ptr7[5] * num12;
        float num16 = EstimateSlamToLineSse3D(ptr7, num13, num14, num15, null);
        (int, int, int) tuple = ScaledAxis(num13, num14, num15);
        FindExtremes(ptr2, tuple.Item1, tuple.Item2, tuple.Item3, out var lowC, out var highC);
        int* spans = stackalloc int[4]
        {
            ptr2[highC * 4] - ptr2[lowC * 4],
            ptr2[highC * 4 + 1] - ptr2[lowC * 4 + 1],
            ptr2[highC * 4 + 2] - ptr2[lowC * 4 + 2],
            0,
        };
        int span = num7 - num6;
        float num17 = AnalyticalQuantEstimateSse(32, 8, 3, spans, 1f, 16);
        float num18 = AnalyticalQuantEstimateSse(64, 4, span, 1f, 16);
        float num19 = AnalyticalQuantEstimateSse(32, 4, 3, spans, 1f, 16);
        float num20 = AnalyticalQuantEstimateSse(64, 8, span, 1f, 16);
        float num21 = num16 + num17 + num18;
        float num22 = num16 + num19 + num20;
        float num23 = AnalyticalQuantEstimateSse(128, 4, 3, spans, 1f, 16);
        float num24 = AnalyticalQuantEstimateSse(256, 4, span, 1f, 16);
        float num25 = num16 + num23 + num24;
        byte* ptr8 = stackalloc byte[16];
        byte* ptr9 = stackalloc byte[16];
        Vec4 xl = default(Vec4);
        Vec4 xh = default(Vec4);
        uint rotIndex = (uint)((dpChannel + 1) & 3);
        if (num25 < Min(num21, num22))
        {
            if (finalSseEstimate != null)
            {
                *finalSseEstimate = num25;
            }
            if (num25 >= sseEstimateToBeat)
            {
                return false;
            }
            int lr = To7(ptr2[lowC * 4]);
            int lg = To7(ptr2[lowC * 4 + 1]);
            int lb = To7(ptr2[lowC * 4 + 2]);
            int la = num6;
            int hr = To7(ptr2[highC * 4]);
            int hg = To7(ptr2[highC * 4 + 1]);
            int hb = To7(ptr2[highC * 4 + 2]);
            int ha = num7;
            EvalWeightsMode5TwoBitRgb(ptr2, ptr8, lr, lg, lb, hr, hg, hb);
            if (
                ComputeLeastSquaresEndpoints(
                    16,
                    ptr8,
                    LsTab2Bit,
                    ref xl,
                    ref xh,
                    ptr2,
                    num2,
                    num3,
                    num4,
                    0f,
                    3
                )
            )
            {
                lr = Round(xl[0] * 0.49803922f);
                lg = Round(xl[1] * 0.49803922f);
                lb = Round(xl[2] * 0.49803922f);
                hr = Round(xh[0] * 0.49803922f);
                hg = Round(xh[1] * 0.49803922f);
                hb = Round(xh[2] * 0.49803922f);
                EvalWeightsMode5TwoBitRgb(ptr2, ptr8, lr, lg, lb, hr, hg, hb);
            }
            EvalWeightsMode5TwoBitAlpha(ptr2, ptr9, la, ha);
            if (
                ComputeLeastSquaresEndpoints1D(
                    16,
                    ptr9,
                    LsTab2Bit,
                    out var xl2,
                    out var xh2,
                    ptr2,
                    3,
                    num5
                )
            )
            {
                la = Round(xl2);
                ha = Round(xh2);
                EvalWeightsMode5TwoBitAlpha(ptr2, ptr9, la, ha);
            }
            EncodeMode5RgbaBlock(
                block,
                (uint)lr,
                (uint)lg,
                (uint)lb,
                (uint)la,
                (uint)hr,
                (uint)hg,
                (uint)hb,
                (uint)ha,
                ptr8,
                ptr9,
                rotIndex
            );
        }
        else if (num21 < num22)
        {
            if (finalSseEstimate != null)
            {
                *finalSseEstimate = num21;
            }
            if (num21 >= sseEstimateToBeat)
            {
                return false;
            }
            int lr2 = To5(ptr2[lowC * 4]);
            int lg2 = To5(ptr2[lowC * 4 + 1]);
            int lb2 = To5(ptr2[lowC * 4 + 2]);
            int la2 = To6(num6);
            int hr2 = To5(ptr2[highC * 4]);
            int hg2 = To5(ptr2[highC * 4 + 1]);
            int hb2 = To5(ptr2[highC * 4 + 2]);
            int ha2 = To6(num7);
            EvalWeightsMode4ThreeBitRgb(ptr2, ptr8, lr2, lg2, lb2, hr2, hg2, hb2);
            if (
                ComputeLeastSquaresEndpoints(
                    16,
                    ptr8,
                    LsTab3Bit,
                    ref xl,
                    ref xh,
                    ptr2,
                    num2,
                    num3,
                    num4,
                    0f,
                    3
                )
            )
            {
                lr2 = Round(xl[0] * 0.12156863f);
                lg2 = Round(xl[1] * 0.12156863f);
                lb2 = Round(xl[2] * 0.12156863f);
                hr2 = Round(xh[0] * 0.12156863f);
                hg2 = Round(xh[1] * 0.12156863f);
                hb2 = Round(xh[2] * 0.12156863f);
                EvalWeightsMode4ThreeBitRgb(ptr2, ptr8, lr2, lg2, lb2, hr2, hg2, hb2);
            }
            EvalWeightsMode4TwoBitAlpha(ptr2, ptr9, la2, ha2);
            if (
                ComputeLeastSquaresEndpoints1D(
                    16,
                    ptr9,
                    LsTab2Bit,
                    out var xl3,
                    out var xh3,
                    ptr2,
                    3,
                    num5
                )
            )
            {
                la2 = Round(xl3 * (21f / 85f));
                ha2 = Round(xh3 * (21f / 85f));
                EvalWeightsMode4TwoBitAlpha(ptr2, ptr9, la2, ha2);
            }
            EncodeMode4RgbaBlock(
                block,
                (uint)lr2,
                (uint)lg2,
                (uint)lb2,
                (uint)la2,
                (uint)hr2,
                (uint)hg2,
                (uint)hb2,
                (uint)ha2,
                ptr8,
                ptr9,
                rotIndex,
                1u
            );
        }
        else
        {
            if (finalSseEstimate != null)
            {
                *finalSseEstimate = num22;
            }
            if (num22 >= sseEstimateToBeat)
            {
                return false;
            }
            int lr3 = To5(ptr2[lowC * 4]);
            int lg3 = To5(ptr2[lowC * 4 + 1]);
            int lb3 = To5(ptr2[lowC * 4 + 2]);
            int la3 = To6(num6);
            int hr3 = To5(ptr2[highC * 4]);
            int hg3 = To5(ptr2[highC * 4 + 1]);
            int hb3 = To5(ptr2[highC * 4 + 2]);
            int ha3 = To6(num7);
            EvalWeightsMode4TwoBitRgb(ptr2, ptr8, lr3, lg3, lb3, hr3, hg3, hb3);
            if (
                ComputeLeastSquaresEndpoints(
                    16,
                    ptr8,
                    LsTab2Bit,
                    ref xl,
                    ref xh,
                    ptr2,
                    num2,
                    num3,
                    num4,
                    0f,
                    3
                )
            )
            {
                lr3 = Round(xl[0] * 0.12156863f);
                lg3 = Round(xl[1] * 0.12156863f);
                lb3 = Round(xl[2] * 0.12156863f);
                hr3 = Round(xh[0] * 0.12156863f);
                hg3 = Round(xh[1] * 0.12156863f);
                hb3 = Round(xh[2] * 0.12156863f);
                EvalWeightsMode4TwoBitRgb(ptr2, ptr8, lr3, lg3, lb3, hr3, hg3, hb3);
            }
            EvalWeightsMode4ThreeBitAlpha(ptr2, ptr9, la3, ha3);
            if (
                ComputeLeastSquaresEndpoints1D(
                    16,
                    ptr9,
                    LsTab3Bit,
                    out var xl4,
                    out var xh4,
                    ptr2,
                    3,
                    num5
                )
            )
            {
                la3 = Round(xl4 * (21f / 85f));
                ha3 = Round(xh4 * (21f / 85f));
                EvalWeightsMode4ThreeBitAlpha(ptr2, ptr9, la3, ha3);
            }
            EncodeMode4RgbaBlock(
                block,
                (uint)lr3,
                (uint)lg3,
                (uint)lb3,
                (uint)la3,
                (uint)hr3,
                (uint)hg3,
                (uint)hb3,
                (uint)ha3,
                ptr8,
                ptr9,
                rotIndex,
                0u
            );
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int R, int G, int B) ScaledAxis(float altXr, float altXg, float altXb)
    {
        int num = 306;
        int num2 = 601;
        int num3 = 117;
        float num4 = Max(MathF.Abs(altXr), MathF.Abs(altXg), MathF.Abs(altXb));
        if (MathF.Abs(num4) >= 1.25E-05f)
        {
            float num5 = 2048f / num4;
            num = Truncate(altXr * num5);
            num2 = Truncate(altXg * num5);
            num3 = Truncate(altXb * num5);
        }
        return (R: num << 4, G: num2 << 4, B: num3 << 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static (int R, int G, int B, int A) ScaledAxis(
        float altXr,
        float altXg,
        float altXb,
        float altXa
    )
    {
        int num = 256;
        int num2 = 256;
        int num3 = 256;
        int num4 = 256;
        float num5 = Max(
            Max(MathF.Abs(altXr), MathF.Abs(altXg), MathF.Abs(altXb)),
            MathF.Abs(altXa)
        );
        if (MathF.Abs(num5) >= 1.25E-05f)
        {
            float num6 = 2048f / num5;
            num = Truncate(altXr * num6);
            num2 = Truncate(altXg * num6);
            num3 = Truncate(altXb * num6);
            num4 = Truncate(altXa * num6);
        }
        return (R: num << 4, G: num2 << 4, B: num3 << 4, A: num4 << 4);
    }

    private static unsafe void FindExtremes(
        byte* pixels,
        int saxisR,
        int saxisG,
        int saxisB,
        out int lowC,
        out int highC
    )
    {
        FindExtremes(pixels, saxisR, saxisG, saxisB, 0, withAlpha: false, out lowC, out highC);
    }

    private static unsafe void FindExtremes(
        byte* pixels,
        int saxisR,
        int saxisG,
        int saxisB,
        int saxisA,
        bool withAlpha,
        out int lowC,
        out int highC
    )
    {
        if (Vectorized)
        {
            Vector128<int> vector = Vector128.Create(saxisR);
            Vector128<int> vector2 = Vector128.Create(saxisG);
            Vector128<int> vector3 = Vector128.Create(saxisB);
            Vector128<int> vector4 = Vector128.Create(saxisA);
            Vector128<int> vector5 = Vector128.Create(int.MaxValue);
            Vector128<int> vector6 = Vector128.Create(int.MinValue);
            Vector128<int> vector7 = Vector128.Create(0, 1, 2, 3);
            Vector128<int> vector8 = Vector128.Create(4);
            for (int i = 0; i < 16; i += 4)
            {
                Vector128<byte> vector9 = Vector128.Load(pixels + i * 4);
                Vector128<int> right =
                    Vector128.Shuffle(vector9, PickR).AsInt32() * vector
                    + Vector128.Shuffle(vector9, PickG).AsInt32() * vector2
                    + Vector128.Shuffle(vector9, PickB).AsInt32() * vector3
                    + vector7;
                if (withAlpha)
                {
                    right += Vector128.Shuffle(vector9, PickA).AsInt32() * vector4;
                }
                vector5 = Vector128.Min(vector5, right);
                vector6 = Vector128.Max(vector6, right);
                vector7 += vector8;
            }
            lowC = HorizontalMin(vector5) & 0xF;
            highC = HorizontalMax(vector6) & 0xF;
            return;
        }
        int num = int.MaxValue;
        int num2 = int.MinValue;
        for (int j = 0; j < 16; j++)
        {
            int num3 =
                pixels[j * 4] * saxisR
                + pixels[j * 4 + 1] * saxisG
                + pixels[j * 4 + 2] * saxisB
                + j;
            if (withAlpha)
            {
                num3 += pixels[j * 4 + 3] * saxisA;
            }
            num = Min(num, num3);
            num2 = Max(num2, num3);
        }
        lowC = num & 0xF;
        highC = num2 & 0xF;
    }

    private static int HorizontalMin(Vector128<int> v)
    {
        v = Vector128.Min(v, Vector128.Shuffle(v, SwapPairs));
        v = Vector128.Min(v, Vector128.Shuffle(v, SwapWithin));
        return v.ToScalar();
    }

    private static int HorizontalMax(Vector128<int> v)
    {
        v = Vector128.Max(v, Vector128.Shuffle(v, SwapPairs));
        v = Vector128.Max(v, Vector128.Shuffle(v, SwapWithin));
        return v.ToScalar();
    }

    private static unsafe bool PackMode7Rgba(
        byte* block,
        byte* pixels,
        float blockXr,
        float blockXg,
        float blockXb,
        float blockXa,
        int blockMeanR,
        int blockMeanG,
        int blockMeanB,
        int blockMeanA,
        float sseEstimateToBeat,
        Bc7PackFlags flags,
        float* finalSseEstimate
    )
    {
        int num = BestPattern2(
            SplitAlongAxis(
                pixels,
                blockXr,
                blockXg,
                blockXb,
                blockXa,
                blockMeanR,
                blockMeanG,
                blockMeanB,
                blockMeanA,
                withAlpha: true
            )
        );
        uint num2 = Part2Bitmasks[num];
        Channels channels = new Channels(pixels);
        Vector128<byte>* ptr = stackalloc Vector128<byte>[2];
        ptr[1] = MembersOf(num2);
        *ptr = ~ptr[1];
        int* ptr2 = stackalloc int[2];
        int* ptr3 = stackalloc int[2];
        int* ptr4 = stackalloc int[2];
        int* ptr5 = stackalloc int[2];
        int* ptr6 = stackalloc int[2];
        ptr6[1] = PopCount32(num2 & 0xFFFF);
        *ptr6 = 16 - ptr6[1];
        for (int i = 0; i < 2; i++)
        {
            MaskedSums(in channels, ptr[i], ptr2 + i, ptr3 + i, ptr4 + i);
            ptr5[i] = Sum(channels.A & ptr[i]);
        }
        int* ptr7 = stackalloc int[2];
        int* ptr8 = stackalloc int[2];
        int* ptr9 = stackalloc int[2];
        int* ptr10 = stackalloc int[2];
        for (int j = 0; j < 2; j++)
        {
            int num3 = ptr6[j];
            int num4 = num3 >> 1;
            ptr7[j] = (ptr2[j] + num4) / num3;
            ptr8[j] = (ptr3[j] + num4) / num3;
            ptr9[j] = (ptr4[j] + num4) / num3;
            ptr10[j] = (ptr5[j] + num4) / num3;
        }
        int* ptr11 = stackalloc int[20];
        for (int k = 0; k < 2; k++)
        {
            MaskedCovariance4(
                in channels,
                ptr[k],
                ptr7[k],
                ptr8[k],
                ptr9[k],
                ptr10[k],
                ptr11 + k * 10
            );
        }
        int* ptr12 = stackalloc int[2];
        int* ptr13 = stackalloc int[2];
        int* ptr14 = stackalloc int[2];
        int* ptr15 = stackalloc int[2];
        float* ptr16 = stackalloc float[10];
        float num5 = 0f;
        for (int l = 0; l < 2; l++)
        {
            int* ptr17 = ptr11 + l * 10;
            int num6 = Max(Max(*ptr17, ptr17[4], ptr17[7]), ptr17[9]);
            for (int m = 0; m < 10; m++)
            {
                ptr16[m] = ptr17[m];
            }
            float num7 = ((num6 != 0) ? (1f / (float)num6) : 0f);
            float num8 = num7 * *ptr16;
            float num9 = num7 * ptr16[4];
            float num10 = num7 * ptr16[7];
            float num11 = num7 * ptr16[9];
            float num12 = *ptr16 * num8 + ptr16[1] * num9 + ptr16[2] * num10 + ptr16[3] * num11;
            float num13 = ptr16[1] * num8 + ptr16[4] * num9 + ptr16[5] * num10 + ptr16[6] * num11;
            float num14 = ptr16[2] * num8 + ptr16[5] * num9 + ptr16[7] * num10 + ptr16[8] * num11;
            float num15 = ptr16[3] * num8 + ptr16[6] * num9 + ptr16[8] * num10 + ptr16[9] * num11;
            float num16 = *ptr16 * num12 + ptr16[1] * num13 + ptr16[2] * num14 + ptr16[3] * num15;
            float num17 = ptr16[1] * num12 + ptr16[4] * num13 + ptr16[5] * num14 + ptr16[6] * num15;
            float num18 = ptr16[2] * num12 + ptr16[5] * num13 + ptr16[7] * num14 + ptr16[8] * num15;
            float num19 = ptr16[3] * num12 + ptr16[6] * num13 + ptr16[8] * num14 + ptr16[9] * num15;
            num5 += EstimateSlamToLineSse4D(ptr16, num16, num17, num18, num19, null);
            (int, int, int, int) tuple = ScaledAxis(num16, num17, num18, num19);
            ptr12[l] = tuple.Item1;
            ptr13[l] = tuple.Item2;
            ptr14[l] = tuple.Item3;
            ptr15[l] = tuple.Item4;
        }
        int* ptr18 = stackalloc int[2];
        int* ptr19 = stackalloc int[2];
        for (int n = 0; n < 2; n++)
        {
            MaskedExtremes(
                pixels,
                ptr[n],
                ptr12[n],
                ptr13[n],
                ptr14[n],
                ptr15[n],
                withAlpha: true,
                out ptr18[n],
                out ptr19[n]
            );
        }
        float num20 = 0f;
        int* ptr20 = stackalloc int[4];
        for (int num21 = 0; num21 < 2; num21++)
        {
            int num22 = ptr18[num21];
            int num23 = ptr19[num21];
            for (int num24 = 0; num24 < 4; num24++)
            {
                ptr20[num24] = pixels[num23 * 4 + num24] - pixels[num22 * 4 + num24];
            }
            num20 += AnalyticalQuantEstimateSse(
                32,
                4,
                4,
                ptr20,
                ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None) ? 0.85f : 1f,
                ptr6[num21]
            );
        }
        if ((*finalSseEstimate = num5 + num20) >= sseEstimateToBeat)
        {
            return false;
        }
        uint* ptr21 = stackalloc uint[2];
        uint* ptr22 = stackalloc uint[2];
        uint* ptr23 = stackalloc uint[2];
        uint* ptr24 = stackalloc uint[2];
        uint* ptr25 = stackalloc uint[2];
        uint* ptr26 = stackalloc uint[2];
        uint* ptr27 = stackalloc uint[2];
        uint* ptr28 = stackalloc uint[2];
        uint* ptr29 = stackalloc uint[4];
        byte* ptr30 = stackalloc byte[4];
        byte* ptr31 = stackalloc byte[4];
        float* ptr32 = stackalloc float[4];
        float* ptr33 = stackalloc float[4];
        for (int num25 = 0; num25 < 2; num25++)
        {
            int num26 = ptr18[num25];
            int num27 = ptr19[num25];
            if ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None)
            {
                for (int num28 = 0; num28 < 4; num28++)
                {
                    ptr32[num28] = (float)(int)pixels[num26 * 4 + num28] * 0.003921569f;
                    ptr33[num28] = (float)(int)pixels[num27 * 4 + num28] * 0.003921569f;
                }
                DetermineUniquePBits(4, 5, ptr32, ptr33, ptr30, ptr31, ptr29 + num25 * 2);
                ptr21[num25] = *ptr30;
                ptr22[num25] = ptr30[1];
                ptr23[num25] = ptr30[2];
                ptr24[num25] = ptr30[3];
                ptr25[num25] = *ptr31;
                ptr26[num25] = ptr31[1];
                ptr27[num25] = ptr31[2];
                ptr28[num25] = ptr31[3];
            }
            else
            {
                int num29 = ((pixels[num26 * 4 + 3] >= 129) ? 1 : 0);
                int num30 = ((pixels[num27 * 4 + 3] >= 129) ? 1 : 0);
                ptr29[num25 * 2] = (uint)num29;
                ptr29[num25 * 2 + 1] = (uint)num30;
                ptr21[num25] = (uint)To5(pixels[num26 * 4], num29);
                ptr22[num25] = (uint)To5(pixels[num26 * 4 + 1], num29);
                ptr23[num25] = (uint)To5(pixels[num26 * 4 + 2], num29);
                ptr24[num25] = (uint)To5(pixels[num26 * 4 + 3], num29);
                ptr25[num25] = (uint)To5(pixels[num27 * 4], num30);
                ptr26[num25] = (uint)To5(pixels[num27 * 4 + 1], num30);
                ptr27[num25] = (uint)To5(pixels[num27 * 4 + 2], num30);
                ptr28[num25] = (uint)To5(pixels[num27 * 4 + 3], num30);
            }
        }
        byte* weights = stackalloc byte[16];
        EvalWeightsMode7Rgba(
            pixels,
            weights,
            ptr21,
            ptr22,
            ptr23,
            ptr24,
            ptr25,
            ptr26,
            ptr27,
            ptr28,
            ptr29,
            num2
        );
        float* ptr34 = stackalloc float[2];
        float* ptr35 = stackalloc float[2];
        float* ptr36 = stackalloc float[2];
        float* ptr37 = stackalloc float[2];
        float* ptr38 = stackalloc float[2];
        float* ptr39 = stackalloc float[2];
        float* ptr40 = stackalloc float[2];
        AccumulateSubsetMoments(
            pixels,
            weights,
            num2,
            LsTab2Bit,
            ptr34,
            ptr35,
            ptr36,
            ptr37,
            ptr38,
            ptr39,
            ptr40
        );
        for (int num31 = 0; num31 < 2; num31++)
        {
            float num32 = (float)ptr2[num31] - ptr37[num31];
            float num33 = (float)ptr3[num31] - ptr38[num31];
            float num34 = (float)ptr4[num31] - ptr39[num31];
            float num35 = (float)ptr5[num31] - ptr40[num31];
            if (
                Invert2x2(
                    ptr34[num31],
                    ptr35[num31],
                    ptr36[num31],
                    out var iz,
                    out var iz2,
                    out var iz3,
                    out var iz4
                )
            )
            {
                float num36 = iz3 * ptr37[num31] + iz4 * num32;
                float num37 = iz * ptr37[num31] + iz2 * num32;
                float num38 = iz3 * ptr38[num31] + iz4 * num33;
                float num39 = iz * ptr38[num31] + iz2 * num33;
                float num40 = iz3 * ptr39[num31] + iz4 * num34;
                float num41 = iz * ptr39[num31] + iz2 * num34;
                float num42 = iz3 * ptr40[num31] + iz4 * num35;
                float num43 = iz * ptr40[num31] + iz2 * num35;
                if ((flags & Bc7PackFlags.PBitOpt) != Bc7PackFlags.None)
                {
                    *ptr32 = Clamp(num36 * 0.003921569f, 0f, 1f);
                    ptr32[1] = Clamp(num38 * 0.003921569f, 0f, 1f);
                    ptr32[2] = Clamp(num40 * 0.003921569f, 0f, 1f);
                    ptr32[3] = Clamp(num42 * 0.003921569f, 0f, 1f);
                    *ptr33 = Clamp(num37 * 0.003921569f, 0f, 1f);
                    ptr33[1] = Clamp(num39 * 0.003921569f, 0f, 1f);
                    ptr33[2] = Clamp(num41 * 0.003921569f, 0f, 1f);
                    ptr33[3] = Clamp(num43 * 0.003921569f, 0f, 1f);
                    DetermineUniquePBits(4, 5, ptr32, ptr33, ptr30, ptr31, ptr29 + num31 * 2);
                    ptr21[num31] = *ptr30;
                    ptr22[num31] = ptr30[1];
                    ptr23[num31] = ptr30[2];
                    ptr24[num31] = ptr30[3];
                    ptr25[num31] = *ptr31;
                    ptr26[num31] = ptr31[1];
                    ptr27[num31] = ptr31[2];
                    ptr28[num31] = ptr31[3];
                }
                else
                {
                    int num44 = ((num42 >= 129f) ? 1 : 0);
                    int num45 = ((num43 >= 129f) ? 1 : 0);
                    ptr29[num31 * 2] = (uint)num44;
                    ptr29[num31 * 2 + 1] = (uint)num45;
                    ptr21[num31] = (uint)To5Clamp(num36, num44);
                    ptr22[num31] = (uint)To5Clamp(num38, num44);
                    ptr23[num31] = (uint)To5Clamp(num40, num44);
                    ptr24[num31] = (uint)To5Clamp(num42, num44);
                    ptr25[num31] = (uint)To5Clamp(num37, num45);
                    ptr26[num31] = (uint)To5Clamp(num39, num45);
                    ptr27[num31] = (uint)To5Clamp(num41, num45);
                    ptr28[num31] = (uint)To5Clamp(num43, num45);
                }
            }
        }
        EvalWeightsMode7Rgba(
            pixels,
            weights,
            ptr21,
            ptr22,
            ptr23,
            ptr24,
            ptr25,
            ptr26,
            ptr27,
            ptr28,
            ptr29,
            num2
        );
        EncodeMode7RgbaBlock(
            block,
            num,
            ptr21,
            ptr22,
            ptr23,
            ptr24,
            ptr25,
            ptr26,
            ptr27,
            ptr28,
            ptr29,
            weights
        );
        return true;
    }

    private static void Swap(ref uint a, ref uint b)
    {
        uint num = b;
        uint num2 = a;
        a = num;
        b = num2;
    }

    private static unsafe void Write(byte* block, ulong value, int offset, int count)
    {
        if (count == 8)
        {
            Unsafe.WriteUnaligned(block + offset, value);
            return;
        }
        if (count >= 4)
        {
            Unsafe.WriteUnaligned(block + offset, (uint)value);
            value >>= 32;
            offset += 4;
            count -= 4;
        }
        for (int i = 0; i < count; i++)
        {
            block[offset + i] = (byte)(value >> i * 8);
        }
    }

    private static unsafe void EncodeMode0RgbBlock(
        byte* block,
        int partId,
        uint* lr,
        uint* lg,
        uint* lb,
        uint* hr,
        uint* hg,
        uint* hb,
        uint* p,
        byte* weights
    )
    {
        ReadOnlySpan<byte> readOnlySpan = Partition3.Slice(partId * 16, 16);
        byte b = AnchorIndexThirdSubset1[partId];
        byte b2 = AnchorIndexThirdSubset2[partId];
        byte* intPtr = stackalloc byte[12];
        // IL initblk instruction
        Unsafe.InitBlock(intPtr, 0, 12);
        uint* ptr = (uint*)intPtr;
        if ((*weights & 4) != 0)
        {
            Swap(ref *lr, ref *hr);
            Swap(ref *lg, ref *hg);
            Swap(ref *lb, ref *hb);
            Swap(ref *p, ref p[1]);
            *ptr = 7u;
        }
        if ((weights[(int)b] & 4) != 0)
        {
            Swap(ref lr[1], ref hr[1]);
            Swap(ref lg[1], ref hg[1]);
            Swap(ref lb[1], ref hb[1]);
            Swap(ref p[2], ref p[3]);
            ptr[1] = 7u;
        }
        if ((weights[(int)b2] & 4) != 0)
        {
            Swap(ref lr[2], ref hr[2]);
            Swap(ref lg[2], ref hg[2]);
            Swap(ref lb[2], ref hb[2]);
            Swap(ref p[4], ref p[5]);
            ptr[2] = 7u;
        }
        ulong value =
            (ulong)(1 | ((long)partId << 1))
            | ((ulong)(*lr) << 5)
            | ((ulong)(*hr) << 9)
            | ((ulong)lr[1] << 13)
            | ((ulong)hr[1] << 17)
            | ((ulong)lr[2] << 21)
            | ((ulong)hr[2] << 25)
            | ((ulong)(*lg) << 29)
            | ((ulong)(*hg) << 33)
            | ((ulong)lg[1] << 37)
            | ((ulong)hg[1] << 41)
            | ((ulong)lg[2] << 45)
            | ((ulong)hg[2] << 49)
            | ((ulong)(*lb) << 53)
            | ((ulong)(*hb) << 57)
            | ((ulong)lb[1] << 61);
        Write(block, value, 0, 8);
        ulong num =
            (lb[1] >> 3)
            | ((ulong)hb[1] << 1)
            | ((ulong)lb[2] << 5)
            | ((ulong)hb[2] << 9)
            | ((ulong)(*p) << 13)
            | ((ulong)p[1] << 14)
            | ((ulong)p[2] << 15)
            | ((ulong)p[3] << 16)
            | ((ulong)p[4] << 17)
            | ((ulong)p[5] << 18);
        int num2 = 19;
        for (int i = 0; i < 16; i++)
        {
            ulong num3 = weights[i] ^ ptr[(int)readOnlySpan[i]];
            num |= num3 << num2;
            num2 += 3 - ((i == 0 || i == b || i == b2) ? 1 : 0);
        }
        Write(block, num, 8, 8);
    }

    private static unsafe void EncodeMode1RgbBlock(
        byte* block,
        int partId,
        uint* lr,
        uint* lg,
        uint* lb,
        uint* hr,
        uint* hg,
        uint* hb,
        uint p0,
        uint p1,
        byte* weights
    )
    {
        ReadOnlySpan<byte> readOnlySpan = Partition2.Slice(partId * 16, 16);
        byte b = AnchorIndexSecondSubset[partId];
        uint* ptr = stackalloc uint[2] { 0u, 0u };
        if ((*weights & 4) != 0)
        {
            Swap(ref *lr, ref *hr);
            Swap(ref *lg, ref *hg);
            Swap(ref *lb, ref *hb);
            *ptr = 7u;
        }
        if ((weights[(int)b] & 4) != 0)
        {
            Swap(ref lr[1], ref hr[1]);
            Swap(ref lg[1], ref hg[1]);
            Swap(ref lb[1], ref hb[1]);
            ptr[1] = 7u;
        }
        *block = (byte)(2 | (partId << 2));
        ulong value =
            *lr
            | ((ulong)(*hr) << 6)
            | ((ulong)lr[1] << 12)
            | ((ulong)hr[1] << 18)
            | ((ulong)(*lg) << 24)
            | ((ulong)(*hg) << 30)
            | ((ulong)lg[1] << 36)
            | ((ulong)hg[1] << 42)
            | ((ulong)(*lb) << 48)
            | ((ulong)(*hb) << 54)
            | ((ulong)lb[1] << 60);
        Write(block, value, 1, 8);
        block[9] = (byte)((lb[1] >> 4) | (hb[1] << 2));
        ulong num = p0 | ((ulong)p1 << 1);
        int num2 = 2;
        for (int i = 0; i < 16; i++)
        {
            ulong num3 = weights[i] ^ ptr[(int)readOnlySpan[i]];
            num |= num3 << num2;
            num2 += 3 - ((i == 0 || i == b) ? 1 : 0);
        }
        Write(block, num, 10, 6);
    }

    private static unsafe void EncodeMode2RgbBlock(
        byte* block,
        int partId,
        uint* lr,
        uint* lg,
        uint* lb,
        uint* hr,
        uint* hg,
        uint* hb,
        byte* weights
    )
    {
        ReadOnlySpan<byte> readOnlySpan = Partition3.Slice(partId * 16, 16);
        byte* intPtr = stackalloc byte[12];
        // IL initblk instruction
        Unsafe.InitBlock(intPtr, 0, 12);
        uint* ptr = (uint*)intPtr;
        if ((*weights & 2) != 0)
        {
            Swap(ref *lr, ref *hr);
            Swap(ref *lg, ref *hg);
            Swap(ref *lb, ref *hb);
            *ptr = 3u;
        }
        byte b = AnchorIndexThirdSubset1[partId];
        if ((weights[(int)b] & 2) != 0)
        {
            Swap(ref lr[1], ref hr[1]);
            Swap(ref lg[1], ref hg[1]);
            Swap(ref lb[1], ref hb[1]);
            ptr[1] = 3u;
        }
        byte b2 = AnchorIndexThirdSubset2[partId];
        if ((weights[(int)b2] & 2) != 0)
        {
            Swap(ref lr[2], ref hr[2]);
            Swap(ref lg[2], ref hg[2]);
            Swap(ref lb[2], ref hb[2]);
            ptr[2] = 3u;
        }
        ulong value =
            (ulong)(4 | ((long)partId << 3))
            | ((ulong)(*lr) << 9)
            | ((ulong)(*hr) << 14)
            | ((ulong)lr[1] << 19)
            | ((ulong)hr[1] << 24)
            | ((ulong)lr[2] << 29)
            | ((ulong)hr[2] << 34)
            | ((ulong)(*lg) << 39)
            | ((ulong)(*hg) << 44)
            | ((ulong)lg[1] << 49)
            | ((ulong)hg[1] << 54)
            | ((ulong)lg[2] << 59);
        Write(block, value, 0, 8);
        ulong num =
            hg[2]
            | ((ulong)(*lb) << 5)
            | ((ulong)(*hb) << 10)
            | ((ulong)lb[1] << 15)
            | ((ulong)hb[1] << 20)
            | ((ulong)lb[2] << 25)
            | ((ulong)hb[2] << 30);
        Write(block, num, 8, 4);
        num >>= 32;
        int num2 = 3;
        for (int i = 0; i < 16; i++)
        {
            ulong num3 = weights[i] ^ ptr[(int)readOnlySpan[i]];
            num |= num3 << num2;
            num2 += 2 - ((i == 0 || i == b || i == b2) ? 1 : 0);
        }
        Write(block, num, 12, 4);
    }

    private static unsafe void EncodeMode3RgbBlock(
        byte* block,
        int partId,
        uint* lr,
        uint* lg,
        uint* lb,
        uint* hr,
        uint* hg,
        uint* hb,
        uint* p,
        byte* weights
    )
    {
        ReadOnlySpan<byte> readOnlySpan = Partition2.Slice(partId * 16, 16);
        byte b = AnchorIndexSecondSubset[partId];
        uint* ptr = stackalloc uint[2] { 0u, 0u };
        if ((*weights & 2) != 0)
        {
            Swap(ref *lr, ref *hr);
            Swap(ref *lg, ref *hg);
            Swap(ref *lb, ref *hb);
            Swap(ref *p, ref p[1]);
            *ptr = 3u;
        }
        if ((weights[(int)b] & 2) != 0)
        {
            Swap(ref lr[1], ref hr[1]);
            Swap(ref lg[1], ref hg[1]);
            Swap(ref lb[1], ref hb[1]);
            Swap(ref p[2], ref p[3]);
            ptr[1] = 3u;
        }
        ulong value =
            (ulong)(8 | ((long)partId << 4))
            | ((ulong)(*lr) << 10)
            | ((ulong)(*hr) << 17)
            | ((ulong)lr[1] << 24)
            | ((ulong)hr[1] << 31)
            | ((ulong)(*lg) << 38)
            | ((ulong)(*hg) << 45)
            | ((ulong)lg[1] << 52)
            | ((ulong)hg[1] << 59);
        Write(block, value, 0, 8);
        ulong num =
            (hg[1] >> 5)
            | ((ulong)(*lb) << 2)
            | ((ulong)(*hb) << 9)
            | ((ulong)lb[1] << 16)
            | ((ulong)hb[1] << 23)
            | ((ulong)(*p) << 30)
            | ((ulong)p[1] << 31)
            | ((ulong)p[2] << 32)
            | ((ulong)p[3] << 33);
        int num2 = 34;
        for (int i = 0; i < 16; i++)
        {
            ulong num3 = weights[i] ^ ptr[(int)readOnlySpan[i]];
            num |= num3 << num2;
            num2 += 2 - ((i == 0 || i == b) ? 1 : 0);
        }
        Write(block, num, 8, 8);
    }

    private static unsafe void EncodeMode4RgbaBlock(
        byte* block,
        uint lr,
        uint lg,
        uint lb,
        uint la,
        uint hr,
        uint hg,
        uint hb,
        uint ha,
        byte* weights0,
        byte* weights1,
        uint rotIndex,
        uint indexFlag
    )
    {
        byte* ptr = ((indexFlag != 0) ? weights1 : weights0);
        byte* ptr2 = ((indexFlag != 0) ? weights0 : weights1);
        uint num = 0u;
        uint num2 = 0u;
        if ((*ptr2 & 4) != 0)
        {
            num = 7u;
            if (indexFlag != 0)
            {
                Swap(ref lr, ref hr);
                Swap(ref lg, ref hg);
                Swap(ref lb, ref hb);
            }
            else
            {
                Swap(ref la, ref ha);
            }
        }
        if ((*ptr & 2) != 0)
        {
            num2 = 3u;
            if (indexFlag != 0)
            {
                Swap(ref la, ref ha);
            }
            else
            {
                Swap(ref lr, ref hr);
                Swap(ref lg, ref hg);
                Swap(ref lb, ref hb);
            }
        }
        *block = (byte)(0x10 | (rotIndex << 5) | (indexFlag << 7));
        ulong num3 =
            lr
            | ((ulong)hr << 5)
            | ((ulong)lg << 10)
            | ((ulong)hg << 15)
            | ((ulong)lb << 20)
            | ((ulong)hb << 25)
            | ((ulong)la << 30)
            | ((ulong)ha << 36);
        Write(block, num3, 1, 5);
        num3 >>= 40;
        int num4 = 2;
        for (int i = 0; i < 16; i++)
        {
            num3 |= (ulong)(ptr[i] ^ num2) << num4;
            num4 += 2 - ((i == 0) ? 1 : 0);
        }
        Write(block, num3, 6, 4);
        num3 >>= 32;
        num4 = 1;
        for (int j = 0; j < 16; j++)
        {
            num3 |= (ulong)(ptr2[j] ^ num) << num4;
            num4 += 3 - ((j == 0) ? 1 : 0);
        }
        Write(block, num3, 10, 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe ulong PackNibbles(byte* weights, uint inverted)
    {
        Vector128<ushort> vector = (
            Vector128.Load(weights) ^ Vector128.Create((byte)inverted)
        ).AsUInt16();
        return Vector128
            .Narrow(
                (vector | Vector128.ShiftRightLogical(vector, 4)) & Vector128.Create((ushort)255),
                Vector128<ushort>.Zero
            )
            .AsUInt64()
            .ToScalar();
    }

    private static unsafe void PackMode5Solid(byte* block, byte* color)
    {
        *block = 32;
        long num = Mode5OptimalLow[(int)(*color)];
        ulong num2 = Mode5OptimalHigh[(int)(*color)];
        ulong num3 = Mode5OptimalLow[(int)color[1]];
        ulong num4 = Mode5OptimalHigh[(int)color[1]];
        ulong num5 = Mode5OptimalLow[(int)color[2]];
        ulong num6 = Mode5OptimalHigh[(int)color[2]];
        ulong num7 = color[3];
        ulong num8 =
            (ulong)num
            | (num2 << 7)
            | (num3 << 14)
            | (num4 << 21)
            | (num5 << 28)
            | (num6 << 35)
            | (num7 << 42)
            | (num7 << 50);
        Write(block, num8, 1, 7);
        num8 >>= 56;
        Mode5SolidTail.CopyTo(new Span<byte>(block + 8, 8));
        byte* num9 = block + 8;
        *num9 |= (byte)num8;
    }

    private static unsafe void EncodeMode5RgbaBlock(
        byte* block,
        uint lr,
        uint lg,
        uint lb,
        uint la,
        uint hr,
        uint hg,
        uint hb,
        uint ha,
        byte* colorWeights,
        byte* alphaWeights,
        uint rotIndex
    )
    {
        uint num = 0u;
        uint num2 = 0u;
        if ((*colorWeights & 2) != 0)
        {
            Swap(ref lr, ref hr);
            Swap(ref lg, ref hg);
            Swap(ref lb, ref hb);
            num = 3u;
        }
        if ((*alphaWeights & 2) != 0)
        {
            Swap(ref la, ref ha);
            num2 = 3u;
        }
        ulong value =
            0x20
            | ((ulong)rotIndex << 6)
            | ((ulong)lr << 8)
            | ((ulong)hr << 15)
            | ((ulong)lg << 22)
            | ((ulong)hg << 29)
            | ((ulong)lb << 36)
            | ((ulong)hb << 43)
            | ((ulong)la << 50)
            | ((ulong)ha << 58);
        Write(block, value, 0, 8);
        ulong num3 = (ha >> 6) & 3;
        int num4 = 2;
        for (int i = 0; i < 16; i++)
        {
            num3 |= (ulong)(colorWeights[i] ^ num) << num4;
            num4 += 2 - ((i == 0) ? 1 : 0);
        }
        for (int j = 0; j < 16; j++)
        {
            num3 |= (ulong)(alphaWeights[j] ^ num2) << num4;
            num4 += 2 - ((j == 0) ? 1 : 0);
        }
        Write(block, num3, 8, 8);
    }

    private static unsafe void EncodeMode6RgbaBlock(
        byte* block,
        uint lr,
        uint lg,
        uint lb,
        uint la,
        uint p0,
        uint hr,
        uint hg,
        uint hb,
        uint ha,
        uint p1,
        byte* weights
    )
    {
        uint inverted = 0u;
        if ((*weights & 8) != 0)
        {
            Swap(ref lr, ref hr);
            Swap(ref lg, ref hg);
            Swap(ref lb, ref hb);
            Swap(ref la, ref ha);
            Swap(ref p0, ref p1);
            inverted = 15u;
        }
        ulong value =
            0x40
            | ((ulong)lr << 7)
            | ((ulong)hr << 14)
            | ((ulong)lg << 21)
            | ((ulong)hg << 28)
            | ((ulong)lb << 35)
            | ((ulong)hb << 42)
            | ((ulong)la << 49)
            | ((ulong)ha << 56)
            | ((ulong)p0 << 63);
        Unsafe.WriteUnaligned(block, value);
        ulong num = PackNibbles(weights, inverted);
        Unsafe.WriteUnaligned(block + 8, p1 | ((num & 0xF) << 1) | (num & 0xFFFFFFFFFFFFFFF0uL));
    }

    private static unsafe void EncodeMode7RgbaBlock(
        byte* block,
        int partId,
        uint* lr,
        uint* lg,
        uint* lb,
        uint* la,
        uint* hr,
        uint* hg,
        uint* hb,
        uint* ha,
        uint* p,
        byte* weights
    )
    {
        ReadOnlySpan<byte> readOnlySpan = Partition2.Slice(partId * 16, 16);
        byte b = AnchorIndexSecondSubset[partId];
        uint* ptr = stackalloc uint[2] { 0u, 0u };
        if ((*weights & 2) != 0)
        {
            Swap(ref *lr, ref *hr);
            Swap(ref *lg, ref *hg);
            Swap(ref *lb, ref *hb);
            Swap(ref *la, ref *ha);
            Swap(ref *p, ref p[1]);
            *ptr = 3u;
        }
        if ((weights[(int)b] & 2) != 0)
        {
            Swap(ref lr[1], ref hr[1]);
            Swap(ref lg[1], ref hg[1]);
            Swap(ref lb[1], ref hb[1]);
            Swap(ref la[1], ref ha[1]);
            Swap(ref p[2], ref p[3]);
            ptr[1] = 3u;
        }
        ulong value =
            (ulong)(0x80 | ((long)partId << 8))
            | ((ulong)(*lr) << 14)
            | ((ulong)(*hr) << 19)
            | ((ulong)lr[1] << 24)
            | ((ulong)hr[1] << 29)
            | ((ulong)(*lg) << 34)
            | ((ulong)(*hg) << 39)
            | ((ulong)lg[1] << 44)
            | ((ulong)hg[1] << 49)
            | ((ulong)(*lb) << 54)
            | ((ulong)(*hb) << 59);
        Write(block, value, 0, 8);
        ulong num =
            lb[1]
            | ((ulong)hb[1] << 5)
            | ((ulong)(*la) << 10)
            | ((ulong)(*ha) << 15)
            | ((ulong)la[1] << 20)
            | ((ulong)ha[1] << 25)
            | ((ulong)(*p) << 30)
            | ((ulong)p[1] << 31)
            | ((ulong)p[2] << 32)
            | ((ulong)p[3] << 33);
        int num2 = 34;
        for (int i = 0; i < 16; i++)
        {
            ulong num3 = weights[i] ^ ptr[(int)readOnlySpan[i]];
            num |= num3 << num2;
            num2 += 2 - ((i == 0 || i == b) ? 1 : 0);
        }
        Write(block, num, 8, 8);
    }

    private static unsafe void PackRgbaAnalytical(byte* block, byte* pixels, Bc7PackFlags flags)
    {
        if (IsSolid(pixels))
        {
            PackMode5Solid(block, pixels);
            return;
        }
        int* ptr = stackalloc int[4];
        int* ptr2 = stackalloc int[4];
        int* ptr3 = stackalloc int[4];
        ChannelStats(pixels, ptr, ptr2, ptr3);
        int num = *ptr;
        int num2 = ptr[1];
        int num3 = ptr[2];
        int num4 = ptr[3];
        int num5 = num + 8 >> 4;
        int num6 = num2 + 8 >> 4;
        int num7 = num3 + 8 >> 4;
        int num8 = num4 + 8 >> 4;
        int* ptr4 = stackalloc int[10];
        Covariance4(pixels, num5, num6, num7, num8, ptr4);
        int num9 = Max(Max(*ptr4, ptr4[4], ptr4[7]), ptr4[9]);
        int num10 = -1;
        if ((flags & Bc7PackFlags.UseDualPlaneRgba) != Bc7PackFlags.None && num9 >= 16)
        {
            num10 = PickDualPlaneChannelRgba(ptr4);
        }
        if ((flags & Bc7PackFlags.UseTrivialMode6) != Bc7PackFlags.None && num10 == -1 && num9 < 32)
        {
            PackTrivialMode6Rgba(block, pixels, flags);
            return;
        }
        float* ptr5 = stackalloc float[10];
        for (int i = 0; i < 10; i++)
        {
            ptr5[i] = ptr4[i];
        }
        float num11 = ((num9 != 0) ? (1f / (float)num9) : 0f);
        float num12 = num11 * *ptr5;
        float num13 = num11 * ptr5[4];
        float num14 = num11 * ptr5[7];
        float num15 = num11 * ptr5[9];
        float num16 = 0f;
        float num17 = 0f;
        float num18 = 0f;
        float num19 = 0f;
        for (int j = 0; j < 4; j++)
        {
            num16 = *ptr5 * num12 + ptr5[1] * num13 + ptr5[2] * num14 + ptr5[3] * num15;
            num17 = ptr5[1] * num12 + ptr5[4] * num13 + ptr5[5] * num14 + ptr5[6] * num15;
            num18 = ptr5[2] * num12 + ptr5[5] * num13 + ptr5[7] * num14 + ptr5[8] * num15;
            num19 = ptr5[3] * num12 + ptr5[6] * num13 + ptr5[8] * num14 + ptr5[9] * num15;
            float num20 = MathF.Sqrt(num16 * num16 + num17 * num17 + num18 * num18 + num19 * num19);
            if (num20 > 1.25E-05f)
            {
                num20 = 1f / num20;
                num16 *= num20;
                num17 *= num20;
                num18 *= num20;
                num19 *= num20;
            }
            else
            {
                num16 = (num17 = (num18 = (num19 = 0.25f)));
            }
            num12 = num16;
            num13 = num17;
            num14 = num18;
            num15 = num19;
        }
        int* spans = stackalloc int[4]
        {
            *ptr3 - *ptr2,
            ptr3[1] - ptr2[1],
            ptr3[2] - ptr2[2],
            ptr3[3] - ptr2[3],
        };
        float num22 = default(float);
        float num21 = EstimateSlamToLineSse4D(ptr5, num16, num17, num18, num19, &num22);
        float num23 = num21 + AnalyticalQuantEstimateSse(128, 16, 4, spans, 1f, 16);
        float num24 = 1E+09f;
        float num25 = 1E+09f;
        byte* ptr6 = stackalloc byte[16];
        if (num10 >= 0)
        {
            PackMode4Or5(ptr6, pixels, num10, num23, &num24);
        }
        byte* ptr7 = stackalloc byte[16];
        if (
            (flags & Bc7PackFlags.Use2SubsetsRgba) != Bc7PackFlags.None
            && num9 >= 1600
            && num22 > 0.004f
            && num21 >= 16f
        )
        {
            PackMode7Rgba(
                ptr7,
                pixels,
                num16,
                num17,
                num18,
                num19,
                num5,
                num6,
                num7,
                num8,
                Min(num23, num24),
                flags,
                &num25
            );
        }
        if (num24 < num25 && num24 < num23)
        {
            Buffer.MemoryCopy(ptr6, block, 16L, 16L);
            return;
        }
        if (num25 < num24 && num25 < num23)
        {
            Buffer.MemoryCopy(ptr7, block, 16L, 16L);
            return;
        }
        (int, int, int, int) tuple = ScaledAxis(num16, num17, num18, num19);
        FindExtremes(
            pixels,
            tuple.Item1,
            tuple.Item2,
            tuple.Item3,
            tuple.Item4,
            withAlpha: true,
            out var lowC,
            out var highC
        );
        byte* ptr8 = stackalloc byte[4];
        byte* ptr9 = stackalloc byte[4];
        uint* ptr10 = stackalloc uint[2];
        float* ptr11 = stackalloc float[4];
        float* ptr12 = stackalloc float[4];
        int num26;
        int num27;
        int lr;
        int lg;
        int lb;
        int la;
        int hr;
        int hg;
        int hb;
        int ha;
        if ((flags & Bc7PackFlags.PBitOptMode6) != Bc7PackFlags.None)
        {
            for (int k = 0; k < 4; k++)
            {
                ptr11[k] = (float)(int)pixels[lowC * 4 + k] * 0.003921569f;
                ptr12[k] = (float)(int)pixels[highC * 4 + k] * 0.003921569f;
            }
            DetermineUniquePBits(4, 7, ptr11, ptr12, ptr8, ptr9, ptr10);
            num26 = (int)(*ptr10);
            num27 = (int)ptr10[1];
            lr = *ptr8;
            lg = ptr8[1];
            lb = ptr8[2];
            la = ptr8[3];
            hr = *ptr9;
            hg = ptr9[1];
            hb = ptr9[2];
            ha = ptr9[3];
        }
        else
        {
            num26 = ((pixels[lowC * 4 + 3] > 128) ? 1 : 0);
            num27 = ((pixels[highC * 4 + 3] > 128) ? 1 : 0);
            lr = To7(pixels[lowC * 4], num26);
            lg = To7(pixels[lowC * 4 + 1], num26);
            lb = To7(pixels[lowC * 4 + 2], num26);
            la = To7(pixels[lowC * 4 + 3], num26);
            hr = To7(pixels[highC * 4], num27);
            hg = To7(pixels[highC * 4 + 1], num27);
            hb = To7(pixels[highC * 4 + 2], num27);
            ha = To7(pixels[highC * 4 + 3], num27);
        }
        byte* weights = stackalloc byte[16];
        EvalWeightsMode6Rgba(pixels, weights, lr, lg, lb, la, num26, hr, hg, hb, ha, num27);
        Vec4 xl = default(Vec4);
        Vec4 xh = default(Vec4);
        if (
            ComputeLeastSquaresEndpoints(
                16,
                weights,
                LsTab4Bit,
                ref xl,
                ref xh,
                pixels,
                num,
                num2,
                num3,
                num4,
                4
            )
        )
        {
            if ((flags & Bc7PackFlags.PBitOptMode6) != Bc7PackFlags.None)
            {
                for (int l = 0; l < 4; l++)
                {
                    ptr11[l] = xl[l] * 0.003921569f;
                    ptr12[l] = xh[l] * 0.003921569f;
                }
                DetermineUniquePBits(4, 7, ptr11, ptr12, ptr8, ptr9, ptr10);
                num26 = (int)(*ptr10);
                num27 = (int)ptr10[1];
                lr = *ptr8;
                lg = ptr8[1];
                lb = ptr8[2];
                la = ptr8[3];
                hr = *ptr9;
                hg = ptr9[1];
                hb = ptr9[2];
                ha = ptr9[3];
            }
            else
            {
                num26 = ((xl[3] >= 129f) ? 1 : 0);
                lr = To7(xl[0], num26);
                lg = To7(xl[1], num26);
                lb = To7(xl[2], num26);
                la = To7(xl[3], num26);
                num27 = ((xh[3] >= 129f) ? 1 : 0);
                hr = To7(xh[0], num27);
                hg = To7(xh[1], num27);
                hb = To7(xh[2], num27);
                ha = To7(xh[3], num27);
            }
            EvalWeightsMode6Rgba(pixels, weights, lr, lg, lb, la, num26, hr, hg, hb, ha, num27);
        }
        EncodeMode6RgbaBlock(
            block,
            (uint)lr,
            (uint)lg,
            (uint)lb,
            (uint)la,
            (uint)num26,
            (uint)hr,
            (uint)hg,
            (uint)hb,
            (uint)ha,
            (uint)num27,
            weights
        );
    }

    private static unsafe int PickDualPlaneChannelRgba(int* icov)
    {
        float num = *icov;
        float num2 = icov[4];
        float num3 = icov[7];
        float num4 = icov[9];
        if (icov[9] > 0)
        {
            float a = ((*icov != 0) ? MathF.Abs((float)icov[3] / MathF.Sqrt(num * num4)) : 1f);
            float b = ((icov[4] != 0) ? MathF.Abs((float)icov[6] / MathF.Sqrt(num2 * num4)) : 1f);
            float c = ((icov[7] != 0) ? MathF.Abs((float)icov[8] / MathF.Sqrt(num3 * num4)) : 1f);
            if (Min(a, b, c) < 0.995f)
            {
                return 3;
            }
        }
        bool flag = *icov > 16;
        bool flag2 = icov[4] > 16;
        bool flag3 = icov[7] > 16;
        int num5 = (flag ? 1 : 0) + (flag2 ? 1 : 0) + (flag3 ? 1 : 0);
        if (num5 < 2)
        {
            return -1;
        }
        float num6 = ((flag && flag2) ? MathF.Abs((float)icov[1] / MathF.Sqrt(num * num2)) : 1f);
        float num7 = ((flag && flag3) ? MathF.Abs((float)icov[2] / MathF.Sqrt(num * num3)) : 1f);
        float num8 = ((flag2 && flag3) ? MathF.Abs((float)icov[5] / MathF.Sqrt(num2 * num3)) : 1f);
        if (Min(num6, num7, num8) >= 0.85f)
        {
            return -1;
        }
        if (num5 == 2)
        {
            return (!flag) ? 1 : 0;
        }
        if (num6 < num8 && num7 < num8)
        {
            return 0;
        }
        if (num6 < num7 && num8 < num7)
        {
            return 1;
        }
        return 2;
    }

    private static unsafe void PackTrivialMode6Rgba(byte* block, byte* pixels, Bc7PackFlags flags)
    {
        FindExtremes(pixels, 32, 64, 16, 64, withAlpha: true, out var lowC, out var highC);
        int num;
        int num2;
        int lr;
        int lg;
        int lb;
        int la;
        int hr;
        int hg;
        int hb;
        int ha;
        if ((flags & Bc7PackFlags.PBitOptMode6) != Bc7PackFlags.None)
        {
            float* xl = stackalloc float[4]
            {
                (float)(int)pixels[lowC * 4] * 0.003921569f,
                (float)(int)pixels[lowC * 4 + 1] * 0.003921569f,
                (float)(int)pixels[lowC * 4 + 2] * 0.003921569f,
                (float)(int)pixels[lowC * 4 + 3] * 0.003921569f,
            };
            float* xh = stackalloc float[4]
            {
                (float)(int)pixels[highC * 4] * 0.003921569f,
                (float)(int)pixels[highC * 4 + 1] * 0.003921569f,
                (float)(int)pixels[highC * 4 + 2] * 0.003921569f,
                (float)(int)pixels[highC * 4 + 3] * 0.003921569f,
            };
            byte* ptr = stackalloc byte[4];
            byte* ptr2 = stackalloc byte[4];
            uint* ptr3 = stackalloc uint[2];
            DetermineUniquePBits(4, 7, xl, xh, ptr, ptr2, ptr3);
            num = (int)(*ptr3);
            num2 = (int)ptr3[1];
            lr = *ptr;
            lg = ptr[1];
            lb = ptr[2];
            la = ptr[3];
            hr = *ptr2;
            hg = ptr2[1];
            hb = ptr2[2];
            ha = ptr2[3];
        }
        else
        {
            num = ((pixels[lowC * 4 + 3] > 128) ? 1 : 0);
            num2 = ((pixels[highC * 4 + 3] > 128) ? 1 : 0);
            lr = To7(pixels[lowC * 4], num);
            lg = To7(pixels[lowC * 4 + 1], num);
            lb = To7(pixels[lowC * 4 + 2], num);
            la = To7(pixels[lowC * 4 + 3], num);
            hr = To7(pixels[highC * 4], num2);
            hg = To7(pixels[highC * 4 + 1], num2);
            hb = To7(pixels[highC * 4 + 2], num2);
            ha = To7(pixels[highC * 4 + 3], num2);
        }
        byte* weights = stackalloc byte[16];
        EvalWeightsMode6Rgba(pixels, weights, lr, lg, lb, la, num, hr, hg, hb, ha, num2);
        EncodeMode6RgbaBlock(
            block,
            (uint)lr,
            (uint)lg,
            (uint)lb,
            (uint)la,
            (uint)num,
            (uint)hr,
            (uint)hg,
            (uint)hb,
            (uint)ha,
            (uint)num2,
            weights
        );
    }

    static unsafe Bc7BlockEncoder()
    {
        Mode6FieldWindows = Vector128.Create(
            (byte)0,
            (byte)1,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7,
            (byte)1,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7,
            (byte)8
        );
        Mode6FieldLifts = Vector128.Create(
            (ushort)2,
            (ushort)8,
            (ushort)32,
            (ushort)128,
            (ushort)4,
            (ushort)16,
            (ushort)64,
            (ushort)256
        );
        Mode6PBitWindows = Vector128.Create(
            7,
            128,
            7,
            128,
            7,
            128,
            7,
            128,
            8,
            128,
            8,
            128,
            8,
            128,
            8,
            128
        );
        Mode6PBitLifts = Vector128.Create(
            (ushort)2,
            (ushort)2,
            (ushort)2,
            (ushort)2,
            (ushort)256,
            (ushort)256,
            (ushort)256,
            (ushort)256
        );
        Mode5FieldWindows = Vector128.Create(
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7,
            (byte)1,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7,
            (byte)8
        );
        Mode5FieldLifts = Vector128.Create(
            (ushort)256,
            (ushort)4,
            (ushort)16,
            (ushort)64,
            (ushort)2,
            (ushort)8,
            (ushort)32,
            (ushort)64
        );
        Mode5FieldMasks = Vector128.Create(
            (ushort)127,
            (ushort)127,
            (ushort)127,
            (ushort)255,
            (ushort)127,
            (ushort)127,
            (ushort)127,
            (ushort)255
        );
        Mode5ColorLanes = Vector128.Create(
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            0,
            ushort.MaxValue,
            ushort.MaxValue,
            ushort.MaxValue,
            0
        );
        SwapHalves = Vector128.Create(
            (byte)8,
            (byte)9,
            (byte)10,
            (byte)11,
            (byte)12,
            (byte)13,
            (byte)14,
            (byte)15,
            (byte)0,
            (byte)1,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7
        );
        BroadcastLowHalf = Vector128.Create(
            (byte)0,
            (byte)1,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7,
            (byte)0,
            (byte)1,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7
        );
        NibbleBytes = Vector128.Create(
            (byte)0,
            (byte)0,
            (byte)1,
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)3,
            (byte)4,
            (byte)4,
            (byte)5,
            (byte)5,
            (byte)6,
            (byte)6,
            (byte)7,
            (byte)7
        );
        HighNibbleLanes = Vector128
            .Create(
                0,
                byte.MaxValue,
                0,
                byte.MaxValue,
                0,
                byte.MaxValue,
                0,
                byte.MaxValue,
                0,
                byte.MaxValue,
                0,
                byte.MaxValue,
                0,
                byte.MaxValue,
                0,
                byte.MaxValue
            )
            .AsByte();
        Weights4Table = Vector128.Create(
            (byte)0,
            (byte)4,
            (byte)9,
            (byte)13,
            (byte)17,
            (byte)21,
            (byte)26,
            (byte)30,
            (byte)34,
            (byte)38,
            (byte)43,
            (byte)47,
            (byte)51,
            (byte)55,
            (byte)60,
            (byte)64
        );
        QuarterBytes = Vector128.Create(
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)3,
            (byte)3,
            (byte)3
        );
        HighNibbleQuads = Vector128
            .Create(
                0,
                0,
                byte.MaxValue,
                byte.MaxValue,
                0,
                0,
                byte.MaxValue,
                byte.MaxValue,
                0,
                0,
                byte.MaxValue,
                byte.MaxValue,
                0,
                0,
                byte.MaxValue,
                byte.MaxValue
            )
            .AsByte();
        OddByteLanes = HighNibbleLanes;
        Weights2ByLowPair = Vector128.Create(
            (byte)0,
            (byte)21,
            (byte)43,
            (byte)64,
            (byte)0,
            (byte)21,
            (byte)43,
            (byte)64,
            (byte)0,
            (byte)21,
            (byte)43,
            (byte)64,
            (byte)0,
            (byte)21,
            (byte)43,
            (byte)64
        );
        Weights2ByHighPair = Vector128.Create(
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)21,
            (byte)21,
            (byte)21,
            (byte)21,
            (byte)43,
            (byte)43,
            (byte)43,
            (byte)43,
            (byte)64,
            (byte)64,
            (byte)64,
            (byte)64
        );
        Weights3Pairs = BuildWeights3Pairs();
        SpreadOpaque = Vector128.Create(
            0,
            128,
            0,
            128,
            0,
            128,
            0,
            128,
            1,
            128,
            1,
            128,
            1,
            128,
            1,
            128
        );
        FirstOfRows01 = Vector256.Create(SpreadOpaque, SpreadOpaque + Vector128.Create((byte)4));
        SecondOfRows01 = FirstOfRows01 + Vector256.Create((byte)2);
        FirstOfRows23 = FirstOfRows01 + Vector256.Create((byte)8);
        SecondOfRows23 = FirstOfRows01 + Vector256.Create((byte)10);
        Bc4Palettes = BuildBc4Palettes();
        Bc4SelectorSpread = BuildBc4SelectorSpread();
        EndpointSpread = Vector128.Create(0, 1, 0, 1, 1, 0, 128, 128, 2, 3, 2, 3, 3, 2, 128, 128);
        FieldMasks = Vector128.Create(63488, 2016, 7936, 0, 63488, 2016, 7936, 0);
        ChannelScales = Vector128.Create(
            (ushort)264,
            (ushort)8320,
            (ushort)2112,
            (ushort)0,
            (ushort)264,
            (ushort)8320,
            (ushort)2112,
            (ushort)0
        );
        AlphaOnes = Vector128.Create(
            (ushort)0,
            (ushort)0,
            (ushort)0,
            (ushort)255,
            (ushort)0,
            (ushort)0,
            (ushort)0,
            (ushort)255
        );
        SwapEndpoints = Vector128.Create(2u, 3u, 0u, 1u);
        FirstEntryLanes = Vector128
            .Create(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, ushort.MaxValue, 0, 0, 0, 0)
            .AsUInt16();
        PunchThroughBlack = Vector128.Create(
            (ushort)0,
            (ushort)0,
            (ushort)0,
            (ushort)0,
            (ushort)0,
            (ushort)0,
            (ushort)0,
            (ushort)255
        );
        SelectorShuffles = BuildSelectorShuffles();
        Rotate8 = Vector128.Create(
            (byte)8,
            (byte)9,
            (byte)10,
            (byte)11,
            (byte)12,
            (byte)13,
            (byte)14,
            (byte)15,
            (byte)0,
            (byte)1,
            (byte)2,
            (byte)3,
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7
        );
        Rotate4 = Vector128.Create(
            (byte)4,
            (byte)5,
            (byte)6,
            (byte)7,
            (byte)8,
            (byte)9,
            (byte)10,
            (byte)11,
            (byte)12,
            (byte)13,
            (byte)14,
            (byte)15,
            (byte)0,
            (byte)1,
            (byte)2,
            (byte)3
        );
        SpreadBits = Vector128.Create(
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)1
        );
        BitWithinByte = Vector128.Create(1, 2, 4, 8, 16, 32, 64, 128, 1, 2, 4, 8, 16, 32, 64, 128);
        Broadcast0 = Vector128.Create(
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)0,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)3,
            (byte)3,
            (byte)3
        );
        Broadcast4 = Vector128.Create(
            (byte)4,
            (byte)4,
            (byte)4,
            (byte)4,
            (byte)5,
            (byte)5,
            (byte)5,
            (byte)5,
            (byte)6,
            (byte)6,
            (byte)6,
            (byte)6,
            (byte)7,
            (byte)7,
            (byte)7,
            (byte)7
        );
        Broadcast8 = Vector128.Create(
            (byte)8,
            (byte)8,
            (byte)8,
            (byte)8,
            (byte)9,
            (byte)9,
            (byte)9,
            (byte)9,
            (byte)10,
            (byte)10,
            (byte)10,
            (byte)10,
            (byte)11,
            (byte)11,
            (byte)11,
            (byte)11
        );
        Broadcast12 = Vector128.Create(
            (byte)12,
            (byte)12,
            (byte)12,
            (byte)12,
            (byte)13,
            (byte)13,
            (byte)13,
            (byte)13,
            (byte)14,
            (byte)14,
            (byte)14,
            (byte)14,
            (byte)15,
            (byte)15,
            (byte)15,
            (byte)15
        );
        NibblePopCount = Vector256.Create(
            (byte)0,
            (byte)1,
            (byte)1,
            (byte)2,
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)2,
            (byte)3,
            (byte)3,
            (byte)4,
            (byte)0,
            (byte)1,
            (byte)1,
            (byte)2,
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)1,
            (byte)2,
            (byte)2,
            (byte)3,
            (byte)2,
            (byte)3,
            (byte)3,
            (byte)4
        );
        SwapPairs = Vector128.Create(2, 3, 0, 1);
        SwapWithin = Vector128.Create(1, 0, 3, 2);
        LsTab2Bit = (float*)NativeMemory.AllocZeroed(16u, 4u);
        LsTab3Bit = (float*)NativeMemory.AllocZeroed(32u, 4u);
        LsTab4Bit = (float*)NativeMemory.AllocZeroed(64u, 4u);
        Part2Bitmasks = (uint*)NativeMemory.AllocZeroed(64u, 4u);
        Part3Bitmasks = (uint*)NativeMemory.AllocZeroed(64u, 4u);
        Mode5OptimalLow = (byte*)NativeMemory.AllocZeroed(256u, 1u);
        Mode5OptimalHigh = (byte*)NativeMemory.AllocZeroed(256u, 1u);
        PickR = Vector128.Create(
            0,
            128,
            128,
            128,
            4,
            128,
            128,
            128,
            8,
            128,
            128,
            128,
            12,
            128,
            128,
            128
        );
        PickG = Vector128.Create(
            1,
            128,
            128,
            128,
            5,
            128,
            128,
            128,
            9,
            128,
            128,
            128,
            13,
            128,
            128,
            128
        );
        PickB = Vector128.Create(
            2,
            128,
            128,
            128,
            6,
            128,
            128,
            128,
            10,
            128,
            128,
            128,
            14,
            128,
            128,
            128
        );
        PickA = Vector128.Create(
            3,
            128,
            128,
            128,
            7,
            128,
            128,
            128,
            11,
            128,
            128,
            128,
            15,
            128,
            128,
            128
        );
        PackLowBytes = Vector128.Create(
            0,
            4,
            8,
            12,
            128,
            128,
            128,
            128,
            128,
            128,
            128,
            128,
            128,
            128,
            128,
            128
        );
        BuildLsTable(LsTab2Bit, Weights2);
        BuildLsTable(LsTab3Bit, Weights3);
        BuildLsTable(LsTab4Bit, Weights4);
        for (int i = 0; i < 64; i++)
        {
            uint num = 0u;
            for (int j = 0; j < 16; j++)
            {
                num |= (uint)(Partition2[i * 16 + j] << j);
            }
            Part2Bitmasks[i] = num;
            uint num2 = 0u;
            for (int k = 0; k < 16; k++)
            {
                switch (Partition3[i * 16 + k])
                {
                    case 0:
                        num2 |= (uint)(1 << k);
                        break;
                    case 1:
                        num2 |= (uint)(65536 << k);
                        break;
                }
            }
            Part3Bitmasks[i] = num2;
        }
        for (int l = 0; l < 256; l++)
        {
            int num3 = int.MaxValue;
            for (int m = 0; m < 128; m++)
            {
                int num4 = (m << 1) | (m >> 6);
                for (int n = 0; n < 128; n++)
                {
                    int num5 = (n << 1) | (n >> 6);
                    int num6 = num4 * (64 - Weights2[1]) + num5 * Weights2[1] + 32 >> 6;
                    int num7 = (num6 - l) * (num6 - l);
                    if (num7 < num3)
                    {
                        num3 = num7;
                        Mode5OptimalLow[l] = (byte)m;
                        Mode5OptimalHigh[l] = (byte)n;
                    }
                }
            }
        }
    }

    private static unsafe void BuildLsTable(float* table, ReadOnlySpan<byte> weights)
    {
        for (int i = 0; i < weights.Length; i++)
        {
            float num = (float)(int)weights[i] * (1f / 64f);
            table[i * 4] = num * num;
            table[i * 4 + 1] = (1f - num) * num;
            table[i * 4 + 2] = (1f - num) * (1f - num);
            table[i * 4 + 3] = num;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Min(int a, int b)
    {
        if (a >= b)
        {
            return b;
        }
        return a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Min(int a, int b, int c)
    {
        return Min(Min(a, b), c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Max(int a, int b)
    {
        if (a <= b)
        {
            return b;
        }
        return a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Max(int a, int b, int c)
    {
        return Max(Max(a, b), c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Min(float a, float b)
    {
        if (!(a < b))
        {
            return b;
        }
        return a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Min(float a, float b, float c)
    {
        return Min(Min(a, b), c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Max(float a, float b)
    {
        if (!(a > b))
        {
            return b;
        }
        return a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Max(float a, float b, float c)
    {
        return Max(Max(a, b), c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Clamp(int value, int low, int high)
    {
        if (value >= low)
        {
            if (value <= high)
            {
                return value;
            }
            return high;
        }
        return low;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Clamp(float value, float low, float high)
    {
        if (!(value < low))
        {
            if (!(value > high))
            {
                return value;
            }
            return high;
        }
        return low;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int PopCount32(uint x)
    {
        return BitOperations.PopCount(x);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Truncate(float x)
    {
        if (!Sse.IsSupported)
        {
            return (int)x;
        }
        return Sse.ConvertToInt32WithTruncation(Vector128.CreateScalarUnsafe(x));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RoundPositive(float x)
    {
        return Truncate(x + 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Round(float x)
    {
        return Truncate((x >= 0f) ? (x + 0.5f) : (x - 0.5f));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int From7(int v)
    {
        return (v << 1) | (v >> 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int From7(int v, int p)
    {
        return (v << 1) | p;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To7(int c8, int pbit)
    {
        return Min(127, c8 + (pbit ^ 1) >>> 1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To7(int c8)
    {
        return (c8 * 127 + 127) / 255;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To7(float c, int pbit)
    {
        return To7(RoundPositive(c), pbit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To7Clamp(float c, int pbit)
    {
        return To7(Clamp(Round(c), 0, 255), pbit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To5(int c8)
    {
        return (c8 * 31 + 127) / 255;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To5Clamp(float c)
    {
        return Clamp(Round(c * 0.12156863f), 0, 31);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To6(int c8)
    {
        return (c8 * 63 + 127) / 255;
    }

    private static int To6(int c8, int pbit)
    {
        int num = (c8 * 127 + 127) / 255;
        if ((num & 1) != pbit)
        {
            num = (
                (c8 * 127 < 255 * num)
                    ? ((num > 0) ? (num - 1) : (num + 1))
                    : ((num < 127) ? (num + 1) : (num - 1))
            );
        }
        return num >> 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To6Clamp(float c, int pbit)
    {
        return To6(Clamp(Round(c), 0, 255), pbit);
    }

    private static int To5(int c8, int pbit)
    {
        int num = (c8 * 63 + 127) / 255;
        if ((num & 1) != pbit)
        {
            num = (
                (c8 * 63 < 255 * num)
                    ? ((num > 0) ? (num - 1) : (num + 1))
                    : ((num < 63) ? (num + 1) : (num - 1))
            );
        }
        return num >> 1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int To5Clamp(float c, int pbit)
    {
        return To5(Clamp(Round(c), 0, 255), pbit);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int From6(int v, int p)
    {
        v = (v << 1) | p;
        return (v << 1) | (v >> 6);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int From4(int v, int p)
    {
        v = (v << 1) | p;
        return (v << 3) | (v >> 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int From5(int v)
    {
        return (v << 3) | (v >> 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int From5(int v, int p)
    {
        v = (v << 1) | p;
        return (v << 2) | (v >> 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int From6(int v)
    {
        return (v << 2) | (v >> 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Invert2x2(
        float z00,
        float z10,
        float z11,
        out float iz00,
        out float iz01,
        out float iz10,
        out float iz11
    )
    {
        float num = z00 * z11 - z10 * z10;
        if (MathF.Abs(num) < 1E-08f)
        {
            iz00 = (iz01 = (iz10 = (iz11 = 0f)));
            return false;
        }
        num = 1f / num;
        iz00 = z11 * num;
        iz01 = (0f - z10) * num;
        iz10 = (0f - z10) * num;
        iz11 = z00 * num;
        return true;
    }

    private static unsafe bool ComputeLeastSquaresEndpoints1D(
        int n,
        byte* weights,
        float* selectorWeights,
        out float xl,
        out float xh,
        byte* colors,
        int component,
        float tr
    )
    {
        float num = 0f;
        float num2 = 0f;
        float num3 = 0f;
        float num4 = 0f;
        for (int i = 0; i < n; i++)
        {
            float* ptr = selectorWeights + weights[i] * 4;
            num += *ptr;
            num2 += ptr[1];
            num3 += ptr[2];
            num4 += ptr[3] * (float)(int)colors[i * 4 + component];
        }
        float num5 = tr - num4;
        if (!Invert2x2(num, num2, num3, out var iz, out var iz2, out var iz3, out var iz4))
        {
            xl = 0f;
            xh = 0f;
            return false;
        }
        xh = Clamp(iz * num4 + iz2 * num5, 0f, 255f);
        xl = Clamp(iz3 * num4 + iz4 * num5, 0f, 255f);
        return true;
    }

    private static unsafe bool ComputeLeastSquaresEndpoints(
        int n,
        byte* weights,
        float* selectorWeights,
        ref Vec4 xl,
        ref Vec4 xh,
        byte* colors,
        float tr,
        float tg,
        float tb,
        float ta,
        int components
    )
    {
        float num = 0f;
        float num2 = 0f;
        float num3 = 0f;
        float num4 = 0f;
        float num5 = 0f;
        float num6 = 0f;
        float num7 = 0f;
        if (Vectorized)
        {
            Vector128<float> zero = Vector128<float>.Zero;
            Vector128<float> zero2 = Vector128<float>.Zero;
            for (int i = 0; i < n; i++)
            {
                Vector128<float> vector = Vector128.Load(selectorWeights + weights[i] * 4);
                zero += vector;
                zero2 +=
                    Vector128.Create(vector.GetElement(3))
                    * Vector128.ConvertToSingle(LoadTexel(colors + i * 4));
            }
            num = zero.GetElement(0);
            num2 = zero.GetElement(1);
            num3 = zero.GetElement(2);
            num4 = zero2.GetElement(0);
            num5 = zero2.GetElement(1);
            num6 = zero2.GetElement(2);
            num7 = zero2.GetElement(3);
        }
        else
        {
            for (int j = 0; j < n; j++)
            {
                float* ptr = selectorWeights + weights[j] * 4;
                num += *ptr;
                num2 += ptr[1];
                num3 += ptr[2];
                float num8 = ptr[3];
                num4 += num8 * (float)(int)colors[j * 4];
                num5 += num8 * (float)(int)colors[j * 4 + 1];
                num6 += num8 * (float)(int)colors[j * 4 + 2];
                if (components == 4)
                {
                    num7 += num8 * (float)(int)colors[j * 4 + 3];
                }
            }
        }
        float num9 = tr - num4;
        float num10 = tg - num5;
        float num11 = tb - num6;
        float num12 = ta - num7;
        if (!Invert2x2(num, num2, num3, out var iz, out var iz2, out var iz3, out var iz4))
        {
            return false;
        }
        xh[0] = Clamp(iz * num4 + iz2 * num9, 0f, 255f);
        xl[0] = Clamp(iz3 * num4 + iz4 * num9, 0f, 255f);
        xh[1] = Clamp(iz * num5 + iz2 * num10, 0f, 255f);
        xl[1] = Clamp(iz3 * num5 + iz4 * num10, 0f, 255f);
        xh[2] = Clamp(iz * num6 + iz2 * num11, 0f, 255f);
        xl[2] = Clamp(iz3 * num6 + iz4 * num11, 0f, 255f);
        if (components == 4)
        {
            xh[3] = Clamp(iz * num7 + iz2 * num12, 0f, 255f);
            xl[3] = Clamp(iz3 * num7 + iz4 * num12, 0f, 255f);
        }
        else
        {
            xh[3] = 0f;
            xl[3] = 0f;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void StoreSelectors(
        Vector128<int> projection,
        Vector128<float> scale,
        Vector128<int> mask,
        byte* weights
    )
    {
        Vector128<int> vector = Vector128.Min(
            Vector128.Max(
                Vector128.ConvertToInt32Native(
                    Vector128.ConvertToSingle(projection) * scale + Vector128.Create(0.5f)
                ),
                Vector128<int>.Zero
            ),
            mask
        );
        *(uint*)weights = Vector128.Shuffle(vector.AsByte(), PackLowBytes).AsUInt32().ToScalar();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Square(int v)
    {
        return v * v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte ClampSelector(int selector, int mask)
    {
        return (byte)(((uint)selector > (uint)mask) ? ((~selector >> 31) & mask) : selector);
    }

    private static unsafe void EvalWeightsRgb(
        byte* pixels,
        byte* weights,
        int lr,
        int lg,
        int lb,
        int hr,
        int hg,
        int hb,
        int mask
    )
    {
        int num = hr - lr;
        int num2 = hg - lg;
        int num3 = hb - lb;
        float num4 = (float)mask / ((float)(Square(num) + Square(num2) + Square(num3)) + 1.25E-06f);
        int num5 = lr * num + lg * num2 + lb * num3;
        if (Vectorized)
        {
            Vector128<int> vector = Vector128.Create(num);
            Vector128<int> vector2 = Vector128.Create(num2);
            Vector128<int> vector3 = Vector128.Create(num3);
            Vector128<int> vector4 = Vector128.Create(num5);
            Vector128<float> scale = Vector128.Create(num4);
            Vector128<int> mask2 = Vector128.Create(mask);
            for (int i = 0; i < 16; i += 4)
            {
                Vector128<byte> vector5 = Vector128.Load(pixels + i * 4);
                StoreSelectors(
                    Vector128.Shuffle(vector5, PickR).AsInt32() * vector
                        + Vector128.Shuffle(vector5, PickG).AsInt32() * vector2
                        + Vector128.Shuffle(vector5, PickB).AsInt32() * vector3
                        - vector4,
                    scale,
                    mask2,
                    weights + i
                );
            }
        }
        else
        {
            for (int j = 0; j < 16; j++)
            {
                int selector = Truncate(
                    (float)(
                        pixels[j * 4] * num
                        + pixels[j * 4 + 1] * num2
                        + pixels[j * 4 + 2] * num3
                        - num5
                    ) * num4
                        + 0.5f
                );
                weights[j] = ClampSelector(selector, mask);
            }
        }
    }

    private static unsafe void EvalWeightsRgba(
        byte* pixels,
        byte* weights,
        int lr,
        int lg,
        int lb,
        int la,
        int hr,
        int hg,
        int hb,
        int ha,
        int mask
    )
    {
        int num = hr - lr;
        int num2 = hg - lg;
        int num3 = hb - lb;
        int num4 = ha - la;
        float num5 =
            (float)mask
            / ((float)(Square(num) + Square(num2) + Square(num3) + Square(num4)) + 1.25E-06f);
        int num6 = lr * num + lg * num2 + lb * num3 + la * num4;
        if (Vectorized)
        {
            Vector128<int> vector = Vector128.Create(num);
            Vector128<int> vector2 = Vector128.Create(num2);
            Vector128<int> vector3 = Vector128.Create(num3);
            Vector128<int> vector4 = Vector128.Create(num4);
            Vector128<int> vector5 = Vector128.Create(num6);
            Vector128<float> scale = Vector128.Create(num5);
            Vector128<int> mask2 = Vector128.Create(mask);
            for (int i = 0; i < 16; i += 4)
            {
                Vector128<byte> vector6 = Vector128.Load(pixels + i * 4);
                StoreSelectors(
                    Vector128.Shuffle(vector6, PickR).AsInt32() * vector
                        + Vector128.Shuffle(vector6, PickG).AsInt32() * vector2
                        + Vector128.Shuffle(vector6, PickB).AsInt32() * vector3
                        + Vector128.Shuffle(vector6, PickA).AsInt32() * vector4
                        - vector5,
                    scale,
                    mask2,
                    weights + i
                );
            }
        }
        else
        {
            for (int j = 0; j < 16; j++)
            {
                int selector = Truncate(
                    (float)(
                        pixels[j * 4] * num
                        + pixels[j * 4 + 1] * num2
                        + pixels[j * 4 + 2] * num3
                        + pixels[j * 4 + 3] * num4
                        - num6
                    ) * num5
                        + 0.5f
                );
                weights[j] = ClampSelector(selector, mask);
            }
        }
    }

    private static unsafe void EvalWeightsAlpha(
        byte* pixels,
        byte* weights,
        int la,
        int ha,
        int mask
    )
    {
        int num = ha - la;
        float num2 = (float)mask / ((float)num + 1.25E-06f);
        if (Vectorized)
        {
            Vector128<int> vector = Vector128.Create(la);
            Vector128<float> scale = Vector128.Create(num2);
            Vector128<int> mask2 = Vector128.Create(mask);
            for (int i = 0; i < 16; i += 4)
            {
                StoreSelectors(
                    Vector128.Shuffle(Vector128.Load(pixels + i * 4), PickA).AsInt32() - vector,
                    scale,
                    mask2,
                    weights + i
                );
            }
        }
        else
        {
            for (int j = 0; j < 16; j++)
            {
                int selector = Truncate((float)(pixels[j * 4 + 3] - la) * num2 + 0.5f);
                weights[j] = ClampSelector(selector, mask);
            }
        }
    }

    private static unsafe void EvalWeightsSubsetsRgb(
        byte* pixels,
        byte* weights,
        byte* subsetIndex,
        int* lr,
        int* lg,
        int* lb,
        int* hr,
        int* hg,
        int* hb,
        int subsets,
        int mask
    )
    {
        int* ptr = stackalloc int[3];
        int* ptr2 = stackalloc int[3];
        int* ptr3 = stackalloc int[3];
        float* ptr4 = stackalloc float[3];
        int* ptr5 = stackalloc int[3];
        for (int i = 0; i < subsets; i++)
        {
            ptr[i] = hr[i] - lr[i];
            ptr2[i] = hg[i] - lg[i];
            ptr3[i] = hb[i] - lb[i];
            ptr4[i] =
                (float)mask
                / ((float)(Square(ptr[i]) + Square(ptr2[i]) + Square(ptr3[i])) + 1.25E-06f);
            ptr5[i] = lr[i] * ptr[i] + lg[i] * ptr2[i] + lb[i] * ptr3[i];
        }
        for (int j = 0; j < 16; j++)
        {
            byte b = subsetIndex[j];
            int selector = Truncate(
                (float)(
                    pixels[j * 4] * ptr[(int)b]
                    + pixels[j * 4 + 1] * ptr2[(int)b]
                    + pixels[j * 4 + 2] * ptr3[(int)b]
                    - ptr5[(int)b]
                ) * ptr4[(int)b]
                    + 0.5f
            );
            weights[j] = ClampSelector(selector, mask);
        }
    }

    private static unsafe void EvalWeightsSubsetsRgba(
        byte* pixels,
        byte* weights,
        byte* subsetIndex,
        int* lr,
        int* lg,
        int* lb,
        int* la,
        int* hr,
        int* hg,
        int* hb,
        int* ha,
        int mask
    )
    {
        int* ptr = stackalloc int[2];
        int* ptr2 = stackalloc int[2];
        int* ptr3 = stackalloc int[2];
        int* ptr4 = stackalloc int[2];
        float* ptr5 = stackalloc float[2];
        int* ptr6 = stackalloc int[2];
        for (int i = 0; i < 2; i++)
        {
            ptr[i] = hr[i] - lr[i];
            ptr2[i] = hg[i] - lg[i];
            ptr3[i] = hb[i] - lb[i];
            ptr4[i] = ha[i] - la[i];
            ptr5[i] =
                (float)mask
                / (
                    (float)(Square(ptr[i]) + Square(ptr2[i]) + Square(ptr3[i]) + Square(ptr4[i]))
                    + 1.25E-06f
                );
            ptr6[i] = lr[i] * ptr[i] + lg[i] * ptr2[i] + lb[i] * ptr3[i] + la[i] * ptr4[i];
        }
        for (int j = 0; j < 16; j++)
        {
            byte b = subsetIndex[j];
            int selector = Truncate(
                (float)(
                    pixels[j * 4] * ptr[(int)b]
                    + pixels[j * 4 + 1] * ptr2[(int)b]
                    + pixels[j * 4 + 2] * ptr3[(int)b]
                    + pixels[j * 4 + 3] * ptr4[(int)b]
                    - ptr6[(int)b]
                ) * ptr5[(int)b]
                    + 0.5f
            );
            weights[j] = ClampSelector(selector, mask);
        }
    }

    private static unsafe void ExpandSubsetBitmask(uint subsetBitmask, byte* subsetIndex)
    {
        for (int i = 0; i < 16; i++)
        {
            subsetIndex[i] = (byte)((subsetBitmask >> i) & 1);
        }
    }

    private static unsafe void EvalWeightsMode6Rgb(
        byte* pixels,
        byte* weights,
        int lr,
        int lg,
        int lb,
        int hr,
        int hg,
        int hb,
        int p0,
        int p1
    )
    {
        EvalWeightsRgb(
            pixels,
            weights,
            From7(lr, p0),
            From7(lg, p0),
            From7(lb, p0),
            From7(hr, p1),
            From7(hg, p1),
            From7(hb, p1),
            15
        );
    }

    private static unsafe void EvalWeightsMode6Rgba(
        byte* pixels,
        byte* weights,
        int lr,
        int lg,
        int lb,
        int la,
        int p0,
        int hr,
        int hg,
        int hb,
        int ha,
        int p1
    )
    {
        EvalWeightsRgba(
            pixels,
            weights,
            From7(lr, p0),
            From7(lg, p0),
            From7(lb, p0),
            From7(la, p0),
            From7(hr, p1),
            From7(hg, p1),
            From7(hb, p1),
            From7(ha, p1),
            15
        );
    }

    private static unsafe void EvalWeightsMode1Rgb(
        byte* pixels,
        byte* weights,
        uint* blr,
        uint* blg,
        uint* blb,
        uint* bhr,
        uint* bhg,
        uint* bhb,
        uint* pbits,
        uint subsetBitmask
    )
    {
        int* ptr = stackalloc int[2];
        int* ptr2 = stackalloc int[2];
        int* ptr3 = stackalloc int[2];
        int* ptr4 = stackalloc int[2];
        int* ptr5 = stackalloc int[2];
        int* ptr6 = stackalloc int[2];
        for (int i = 0; i < 2; i++)
        {
            ptr[i] = From6((int)blr[i], (int)pbits[i]);
            ptr2[i] = From6((int)blg[i], (int)pbits[i]);
            ptr3[i] = From6((int)blb[i], (int)pbits[i]);
            ptr4[i] = From6((int)bhr[i], (int)pbits[i]);
            ptr5[i] = From6((int)bhg[i], (int)pbits[i]);
            ptr6[i] = From6((int)bhb[i], (int)pbits[i]);
        }
        byte* subsetIndex = stackalloc byte[16];
        ExpandSubsetBitmask(subsetBitmask, subsetIndex);
        EvalWeightsSubsetsRgb(
            pixels,
            weights,
            subsetIndex,
            ptr,
            ptr2,
            ptr3,
            ptr4,
            ptr5,
            ptr6,
            2,
            7
        );
    }

    private static unsafe void EvalWeightsMode3Rgb(
        byte* pixels,
        byte* weights,
        uint* blr,
        uint* blg,
        uint* blb,
        uint* bhr,
        uint* bhg,
        uint* bhb,
        uint* pbits,
        uint subsetBitmask
    )
    {
        int* ptr = stackalloc int[2];
        int* ptr2 = stackalloc int[2];
        int* ptr3 = stackalloc int[2];
        int* ptr4 = stackalloc int[2];
        int* ptr5 = stackalloc int[2];
        int* ptr6 = stackalloc int[2];
        for (int i = 0; i < 2; i++)
        {
            ptr[i] = From7((int)blr[i], (int)pbits[i * 2]);
            ptr2[i] = From7((int)blg[i], (int)pbits[i * 2]);
            ptr3[i] = From7((int)blb[i], (int)pbits[i * 2]);
            ptr4[i] = From7((int)bhr[i], (int)pbits[i * 2 + 1]);
            ptr5[i] = From7((int)bhg[i], (int)pbits[i * 2 + 1]);
            ptr6[i] = From7((int)bhb[i], (int)pbits[i * 2 + 1]);
        }
        byte* subsetIndex = stackalloc byte[16];
        ExpandSubsetBitmask(subsetBitmask, subsetIndex);
        EvalWeightsSubsetsRgb(
            pixels,
            weights,
            subsetIndex,
            ptr,
            ptr2,
            ptr3,
            ptr4,
            ptr5,
            ptr6,
            2,
            3
        );
    }

    private static unsafe void EvalWeightsMode7Rgba(
        byte* pixels,
        byte* weights,
        uint* blr,
        uint* blg,
        uint* blb,
        uint* bla,
        uint* bhr,
        uint* bhg,
        uint* bhb,
        uint* bha,
        uint* pbits,
        uint subsetBitmask
    )
    {
        int* ptr = stackalloc int[2];
        int* ptr2 = stackalloc int[2];
        int* ptr3 = stackalloc int[2];
        int* ptr4 = stackalloc int[2];
        int* ptr5 = stackalloc int[2];
        int* ptr6 = stackalloc int[2];
        int* ptr7 = stackalloc int[2];
        int* ptr8 = stackalloc int[2];
        for (int i = 0; i < 2; i++)
        {
            int p = (int)pbits[i * 2];
            int p2 = (int)pbits[i * 2 + 1];
            ptr[i] = From5((int)blr[i], p);
            ptr2[i] = From5((int)blg[i], p);
            ptr3[i] = From5((int)blb[i], p);
            ptr4[i] = From5((int)bla[i], p);
            ptr5[i] = From5((int)bhr[i], p2);
            ptr6[i] = From5((int)bhg[i], p2);
            ptr7[i] = From5((int)bhb[i], p2);
            ptr8[i] = From5((int)bha[i], p2);
        }
        byte* subsetIndex = stackalloc byte[16];
        ExpandSubsetBitmask(subsetBitmask, subsetIndex);
        EvalWeightsSubsetsRgba(
            pixels,
            weights,
            subsetIndex,
            ptr,
            ptr2,
            ptr3,
            ptr4,
            ptr5,
            ptr6,
            ptr7,
            ptr8,
            3
        );
    }

    private static unsafe void EvalWeightsMode0Rgb(
        byte* pixels,
        byte* weights,
        uint* blr,
        uint* blg,
        uint* blb,
        uint* bhr,
        uint* bhg,
        uint* bhb,
        uint* pbits,
        int patIndex
    )
    {
        int* ptr = stackalloc int[3];
        int* ptr2 = stackalloc int[3];
        int* ptr3 = stackalloc int[3];
        int* ptr4 = stackalloc int[3];
        int* ptr5 = stackalloc int[3];
        int* ptr6 = stackalloc int[3];
        for (int i = 0; i < 3; i++)
        {
            ptr[i] = From4((int)blr[i], (int)pbits[i * 2]);
            ptr2[i] = From4((int)blg[i], (int)pbits[i * 2]);
            ptr3[i] = From4((int)blb[i], (int)pbits[i * 2]);
            ptr4[i] = From4((int)bhr[i], (int)pbits[i * 2 + 1]);
            ptr5[i] = From4((int)bhg[i], (int)pbits[i * 2 + 1]);
            ptr6[i] = From4((int)bhb[i], (int)pbits[i * 2 + 1]);
        }
        fixed (byte* ptr7 = Partition3)
        {
            EvalWeightsSubsetsRgb(
                pixels,
                weights,
                ptr7 + patIndex * 16,
                ptr,
                ptr2,
                ptr3,
                ptr4,
                ptr5,
                ptr6,
                3,
                7
            );
        }
    }

    private static unsafe void EvalWeightsMode2Rgb(
        byte* pixels,
        byte* weights,
        uint* blr,
        uint* blg,
        uint* blb,
        uint* bhr,
        uint* bhg,
        uint* bhb,
        int patIndex
    )
    {
        int* ptr = stackalloc int[3];
        int* ptr2 = stackalloc int[3];
        int* ptr3 = stackalloc int[3];
        int* ptr4 = stackalloc int[3];
        int* ptr5 = stackalloc int[3];
        int* ptr6 = stackalloc int[3];
        for (int i = 0; i < 3; i++)
        {
            ptr[i] = From5((int)blr[i]);
            ptr2[i] = From5((int)blg[i]);
            ptr3[i] = From5((int)blb[i]);
            ptr4[i] = From5((int)bhr[i]);
            ptr5[i] = From5((int)bhg[i]);
            ptr6[i] = From5((int)bhb[i]);
        }
        fixed (byte* ptr7 = Partition3)
        {
            EvalWeightsSubsetsRgb(
                pixels,
                weights,
                ptr7 + patIndex * 16,
                ptr,
                ptr2,
                ptr3,
                ptr4,
                ptr5,
                ptr6,
                3,
                3
            );
        }
    }

    private static unsafe void EvalWeightsMode4ThreeBitRgb(
        byte* pixels,
        byte* weights,
        int lr,
        int lg,
        int lb,
        int hr,
        int hg,
        int hb
    )
    {
        EvalWeightsRgb(
            pixels,
            weights,
            From5(lr),
            From5(lg),
            From5(lb),
            From5(hr),
            From5(hg),
            From5(hb),
            7
        );
    }

    private static unsafe void EvalWeightsMode4TwoBitRgb(
        byte* pixels,
        byte* weights,
        int lr,
        int lg,
        int lb,
        int hr,
        int hg,
        int hb
    )
    {
        EvalWeightsRgb(
            pixels,
            weights,
            From5(lr),
            From5(lg),
            From5(lb),
            From5(hr),
            From5(hg),
            From5(hb),
            3
        );
    }

    private static unsafe void EvalWeightsMode4TwoBitAlpha(
        byte* pixels,
        byte* weights,
        int la,
        int ha
    )
    {
        EvalWeightsAlpha(pixels, weights, From6(la), From6(ha), 3);
    }

    private static unsafe void EvalWeightsMode4ThreeBitAlpha(
        byte* pixels,
        byte* weights,
        int la,
        int ha
    )
    {
        EvalWeightsAlpha(pixels, weights, From6(la), From6(ha), 7);
    }

    private static unsafe void EvalWeightsMode5TwoBitRgb(
        byte* pixels,
        byte* weights,
        int lr,
        int lg,
        int lb,
        int hr,
        int hg,
        int hb
    )
    {
        EvalWeightsRgb(
            pixels,
            weights,
            From7(lr),
            From7(lg),
            From7(lb),
            From7(hr),
            From7(hg),
            From7(hb),
            3
        );
    }

    private static unsafe void EvalWeightsMode5TwoBitAlpha(
        byte* pixels,
        byte* weights,
        int la,
        int ha
    )
    {
        EvalWeightsAlpha(pixels, weights, la, ha, 3);
    }
}
