// Decompiled from RimWorld 1.7's Waylith.dll (Waylith.Rendering.Bc1BlockEncoder) and modified;
// derived from Basis Universal and bc7enc_rdo. See NOTICE.md in this folder.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace AssetPackBaker.Waylith;

internal static class Bc1BlockEncoder
{
    private struct Thresholds
    {
        public Vector<int> T0;

        public Vector<int> T1;

        public Vector<int> T2;

        public Vector<int> Ar2;

        public Vector<int> Ag2;

        public Vector<int> Ab2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Thresholds(
            Vector<int> r0,
            Vector<int> g0,
            Vector<int> b0,
            Vector<int> r1,
            Vector<int> g1,
            Vector<int> b1,
            Vector<int> r2,
            Vector<int> g2,
            Vector<int> b2,
            Vector<int> r3,
            Vector<int> g3,
            Vector<int> b3
        )
        {
            Vector<int> vector = r1 - r0;
            Vector<int> vector2 = g1 - g0;
            Vector<int> vector3 = b1 - b0;
            Vector<int> vector4 = r0 * vector + g0 * vector2 + b0 * vector3;
            Vector<int> vector5 = r1 * vector + g1 * vector2 + b1 * vector3;
            Vector<int> vector6 = r2 * vector + g2 * vector2 + b2 * vector3;
            Vector<int> vector7 = r3 * vector + g3 * vector2 + b3 * vector3;
            T0 = vector4 + vector6;
            T1 = vector6 + vector7;
            T2 = vector7 + vector5;
            Ar2 = vector + vector;
            Ag2 = vector2 + vector2;
            Ab2 = vector3 + vector3;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector<int> Select(Vector<int> r, Vector<int> g, Vector<int> b)
        {
            Vector<int> left = r * Ar2 + g * Ag2 + b * Ab2;
            return Vector.ConditionalSelect(
                Vector.LessThanOrEqual(left, T0),
                Vector<int>.Zero,
                Vector.ConditionalSelect(
                    Vector.LessThan(left, T1),
                    new Vector<int>(2),
                    Vector.ConditionalSelect(
                        Vector.LessThan(left, T2),
                        new Vector<int>(3),
                        Vector<int>.One
                    )
                )
            );
        }
    }

    private const int LeastSquaresRounds = 2;

    private const int AverageVarianceThreshold = 16;

    private static readonly unsafe int* Om5;

    private static readonly unsafe int* Om6;

    static unsafe Bc1BlockEncoder()
    {
        Om5 = (int*)NativeMemory.AllocZeroed(256u, 4u);
        Om6 = (int*)NativeMemory.AllocZeroed(256u, 4u);
        for (int i = 5; i <= 6; i++)
        {
            int num = 1 << i;
            int* ptr = ((i == 5) ? Om5 : Om6);
            for (int j = 0; j < 256; j++)
            {
                int num2 = int.MaxValue;
                int num3 = 0;
                int num4 = 0;
                for (int k = 0; k < num; k++)
                {
                    int num5 = ((i == 5) ? ((k << 3) | (k >> 2)) : ((k << 2) | (k >> 4)));
                    for (int l = 0; l < num; l++)
                    {
                        int num6 = ((i == 5) ? ((l << 3) | (l >> 2)) : ((l << 2) | (l >> 4)));
                        int num7 = (2 * num5 + num6) / 3;
                        int num8 = ((num7 > j) ? (num7 - j) : (j - num7));
                        num8 += ((num5 > num6) ? (num5 - num6) : (num6 - num5)) * 3 / 100;
                        if (num8 < num2)
                        {
                            num2 = num8;
                            num3 = k;
                            num4 = l;
                        }
                    }
                }
                ptr[j] = (num3 << 16) | num4;
            }
        }
    }

    public static unsafe void EncodeRows(
        byte* rgba,
        int width,
        int height,
        int rowPitch,
        int firstBlockRow,
        int blockRowCount,
        byte* output
    )
    {
        int count = Vector<int>.Count;
        int num = (width + 3) / 4;
        uint* texels = stackalloc uint[16 * count];
        for (int i = 0; i < blockRowCount; i++)
        {
            byte* ptr = output + (long)i * (long)num * 8;
            for (int j = 0; j < num; j += count)
            {
                int count2 = System.Math.Min(count, num - j);
                Gather(rgba, width, height, rowPitch, j, firstBlockRow + i, num, count, texels);
                EncodeGroup(texels, ptr + (long)j * 8L, count2);
            }
        }
    }

    internal static unsafe void Gather(
        byte* rgba,
        int width,
        int height,
        int rowPitch,
        int firstBlock,
        int blockRow,
        int blocksWide,
        int lanes,
        uint* texels
    )
    {
        int num = blockRow * 4;
        bool flag = num + 4 <= height && (firstBlock + lanes) * 4 <= width;
        for (int i = 0; i < lanes; i++)
        {
            int num2 = System.Math.Min(firstBlock + i, blocksWide - 1) * 4;
            if (flag)
            {
                for (int j = 0; j < 4; j++)
                {
                    uint* ptr = (uint*)(rgba + (long)(num + j) * (long)rowPitch + num2 * 4);
                    uint* num3 = texels + j * 4 * lanes + i;
                    *num3 = *ptr;
                    num3[lanes] = ptr[1];
                    num3[lanes * 2] = ptr[2];
                    num3[lanes * 3] = ptr[3];
                }
                continue;
            }
            for (int k = 0; k < 4; k++)
            {
                byte* ptr2 = rgba + (long)System.Math.Min(num + k, height - 1) * (long)rowPitch;
                for (int l = 0; l < 4; l++)
                {
                    texels[(k * 4 + l) * lanes + i] = *(uint*)(
                        ptr2 + System.Math.Min(num2 + l, width - 1) * 4
                    );
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> Div3(Vector<int> x)
    {
        return Vector.ShiftRightLogical(x * new Vector<int>(43691), 17);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> Expand5(Vector<int> c)
    {
        return Vector.ShiftLeft(c, 3) | Vector.ShiftRightLogical(c, 2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> Expand6(Vector<int> c)
    {
        return Vector.ShiftLeft(c, 2) | Vector.ShiftRightLogical(c, 4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> Round565(Vector<float> value, float scale)
    {
        return Vector.ConvertToInt32(
            Vector.Round(
                Vector.Min(Vector.Max(value, Vector<float>.Zero), new Vector<float>(255f))
                    * new Vector<float>(scale)
            )
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> To565(Vector<float> r, Vector<float> g, Vector<float> b)
    {
        return Vector.ShiftLeft(Round565(r, 0.12156863f), 11)
            | Vector.ShiftLeft(Round565(g, 21f / 85f), 5)
            | Round565(b, 0.12156863f);
    }

    private static unsafe Vector<int> Gather(int* table, Vector<int> index)
    {
        Span<int> span = stackalloc int[Vector<int>.Count];
        for (int i = 0; i < span.Length; i++)
        {
            span[i] = table[index[i]];
        }
        return new Vector<int>((ReadOnlySpan<int>)span);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void PaletteColors(
        Vector<int> c0,
        Vector<int> c1,
        out Vector<int> r0,
        out Vector<int> g0,
        out Vector<int> b0,
        out Vector<int> r1,
        out Vector<int> g1,
        out Vector<int> b1,
        out Vector<int> r2,
        out Vector<int> g2,
        out Vector<int> b2,
        out Vector<int> r3,
        out Vector<int> g3,
        out Vector<int> b3
    )
    {
        Vector<int> vector = new Vector<int>(31);
        Vector<int> vector2 = new Vector<int>(63);
        r0 = Expand5(Vector.ShiftRightLogical(c0, 11) & vector);
        g0 = Expand6(Vector.ShiftRightLogical(c0, 5) & vector2);
        b0 = Expand5(c0 & vector);
        r1 = Expand5(Vector.ShiftRightLogical(c1, 11) & vector);
        g1 = Expand6(Vector.ShiftRightLogical(c1, 5) & vector2);
        b1 = Expand5(c1 & vector);
        Vector<int> vector3 = new Vector<int>(2);
        r2 = Div3(vector3 * r0 + r1);
        g2 = Div3(vector3 * g0 + g1);
        b2 = Div3(vector3 * b0 + b1);
        r3 = Div3(r0 + vector3 * r1);
        g3 = Div3(g0 + vector3 * g1);
        b3 = Div3(b0 + vector3 * b1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Thresholds MakeThresholds(Vector<int> c0, Vector<int> c1)
    {
        PaletteColors(
            c0,
            c1,
            out var r,
            out var g,
            out var b,
            out var r2,
            out var g2,
            out var b2,
            out var r3,
            out var g3,
            out var b3,
            out var r4,
            out var g4,
            out var b4
        );
        return new Thresholds(r, g, b, r2, g2, b2, r3, g3, b3, r4, g4, b4);
    }

    private static unsafe void EncodeGroup(uint* texels, byte* output, int count)
    {
        int count2 = Vector<int>.Count;
        Span<Vector<int>> span = stackalloc Vector<int>[48];
        Span<Vector<int>> r = span.Slice(0, 16);
        Span<Vector<int>> g = span.Slice(16, 16);
        Span<Vector<int>> b = span.Slice(32, 16);
        Vector<int> vector = new Vector<int>(255);
        for (int i = 0; i < 16; i++)
        {
            Vector<int> vector2 = Vector.Load((int*)(texels + i * count2));
            r[i] = vector2 & vector;
            g[i] = Vector.ShiftRightLogical(vector2, 8) & vector;
            b[i] = Vector.ShiftRightLogical(vector2, 16) & vector;
        }
        Vector<int> vector3 = new Vector<int>(255);
        Vector<int> vector4 = new Vector<int>(255);
        Vector<int> vector5 = new Vector<int>(255);
        Vector<int> vector6 = Vector<int>.Zero;
        Vector<int> vector7 = Vector<int>.Zero;
        Vector<int> vector8 = Vector<int>.Zero;
        Vector<int> zero = Vector<int>.Zero;
        Vector<int> zero2 = Vector<int>.Zero;
        Vector<int> zero3 = Vector<int>.Zero;
        Vector<int> zero4 = Vector<int>.Zero;
        Vector<int> zero5 = Vector<int>.Zero;
        Vector<int> zero6 = Vector<int>.Zero;
        Vector<int> zero7 = Vector<int>.Zero;
        Vector<int> zero8 = Vector<int>.Zero;
        Vector<int> zero9 = Vector<int>.Zero;
        for (int j = 0; j < 16; j++)
        {
            vector3 = Vector.Min(vector3, r[j]);
            vector4 = Vector.Min(vector4, g[j]);
            vector5 = Vector.Min(vector5, b[j]);
            vector6 = Vector.Max(vector6, r[j]);
            vector7 = Vector.Max(vector7, g[j]);
            vector8 = Vector.Max(vector8, b[j]);
            zero += r[j];
            zero2 += g[j];
            zero3 += b[j];
            zero4 += r[j] * r[j];
            zero5 += g[j] * g[j];
            zero6 += b[j] * b[j];
            zero7 += r[j] * g[j];
            zero8 += r[j] * b[j];
            zero9 += g[j] * b[j];
        }
        Vector<int> vector9 =
            Vector.Equals(vector3, vector6)
            & Vector.Equals(vector4, vector7)
            & Vector.Equals(vector5, vector8);
        Vector<int> color = Vector<int>.Zero;
        Vector<int> color2 = Vector<int>.Zero;
        Vector<int> selectors = Vector<int>.Zero;
        if (!Vector.EqualsAll(vector9, Vector<int>.Zero))
        {
            EncodeSolid(zero, zero2, zero3, ref color, ref color2, ref selectors, vector9);
        }
        if (!Vector.EqualsAll(vector9, new Vector<int>(-1)))
        {
            EncodeDetailed(
                r,
                g,
                b,
                vector3,
                vector4,
                vector5,
                vector6,
                vector7,
                vector8,
                zero,
                zero2,
                zero3,
                zero4,
                zero5,
                zero6,
                zero7,
                zero8,
                zero9,
                out var color3,
                out var color4,
                out var selectors2
            );
            Vector<int> condition = ~vector9;
            color = Vector.ConditionalSelect(condition, color3, color);
            color2 = Vector.ConditionalSelect(condition, color4, color2);
            selectors = Vector.ConditionalSelect(condition, selectors2, selectors);
        }
        for (int k = 0; k < count; k++)
        {
            byte* num = output + k * 8;
            *(ushort*)num = (ushort)color[k];
            ((short*)num)[1] = (short)(ushort)color2[k];
            ((int*)num)[1] = selectors[k];
        }
    }

    private static void EncodeSolid(
        Vector<int> sumR,
        Vector<int> sumG,
        Vector<int> sumB,
        ref Vector<int> color0,
        ref Vector<int> color1,
        ref Vector<int> selectors,
        Vector<int> solid
    )
    {
        SolidEndpoints(sumR, sumG, sumB, out var high, out var low);
        Vector<int> vector = new Vector<int>(170);
        Vector<int> vector2 = Vector.Equals(low, high);
        if (!Vector.EqualsAll(vector2, Vector<int>.Zero))
        {
            Vector<int> condition = Vector.GreaterThan(low, Vector<int>.Zero);
            Vector<int> left = Vector.ConditionalSelect(
                condition,
                low - Vector<int>.One,
                Vector<int>.Zero
            );
            Vector<int> left2 = Vector.ConditionalSelect(condition, high, Vector<int>.One);
            Vector<int> left3 = Vector.ConditionalSelect(
                condition,
                Vector<int>.Zero,
                new Vector<int>(85)
            );
            low = Vector.ConditionalSelect(vector2, left, low);
            high = Vector.ConditionalSelect(vector2, left2, high);
            vector = Vector.ConditionalSelect(vector2, left3, vector);
        }
        Vector<int> condition2 = Vector.LessThan(high, low);
        Vector<int> left4 = Vector.ConditionalSelect(condition2, low, high);
        Vector<int> left5 = Vector.ConditionalSelect(condition2, high, low);
        vector = Vector.ConditionalSelect(condition2, vector ^ new Vector<int>(85), vector);
        color0 = Vector.ConditionalSelect(solid, left4, color0);
        color1 = Vector.ConditionalSelect(solid, left5, color1);
        selectors = Vector.ConditionalSelect(
            solid,
            vector
                | Vector.ShiftLeft(vector, 8)
                | Vector.ShiftLeft(vector, 16)
                | Vector.ShiftLeft(vector, 24),
            selectors
        );
    }

    private static unsafe void SolidEndpoints(
        Vector<int> sumR,
        Vector<int> sumG,
        Vector<int> sumB,
        out Vector<int> high,
        out Vector<int> low
    )
    {
        Vector<int> vector = new Vector<int>(8);
        Vector<int> vector2 = Gather(Om5, Vector.ShiftRightLogical(sumR + vector, 4));
        Vector<int> vector3 = Gather(Om6, Vector.ShiftRightLogical(sumG + vector, 4));
        Vector<int> vector4 = Gather(Om5, Vector.ShiftRightLogical(sumB + vector, 4));
        Vector<int> vector5 = new Vector<int>(65535);
        high =
            Vector.ShiftLeft(Vector.ShiftRightLogical(vector2, 16), 11)
            | Vector.ShiftLeft(Vector.ShiftRightLogical(vector3, 16), 5)
            | Vector.ShiftRightLogical(vector4, 16);
        low =
            Vector.ShiftLeft(vector2 & vector5, 11)
            | Vector.ShiftLeft(vector3 & vector5, 5)
            | (vector4 & vector5);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Separate(ref Vector<int> c0, ref Vector<int> c1)
    {
        Vector<int> vector = Vector.Max(c0, c1);
        Vector<int> vector2 = Vector.Min(c0, c1);
        Vector<int> vector3 = Vector.Equals(vector, vector2);
        Vector<int> vector4 = Vector.GreaterThan(vector2, Vector<int>.Zero);
        c0 = Vector.ConditionalSelect(vector3 & ~vector4, vector + Vector<int>.One, vector);
        c1 = Vector.ConditionalSelect(vector3 & vector4, vector2 - Vector<int>.One, vector2);
    }

    private static void EncodeDetailed(
        Span<Vector<int>> r,
        Span<Vector<int>> g,
        Span<Vector<int>> b,
        Vector<int> minR,
        Vector<int> minG,
        Vector<int> minB,
        Vector<int> maxR,
        Vector<int> maxG,
        Vector<int> maxB,
        Vector<int> sumR,
        Vector<int> sumG,
        Vector<int> sumB,
        Vector<int> sumRR,
        Vector<int> sumGG,
        Vector<int> sumBB,
        Vector<int> sumRG,
        Vector<int> sumRB,
        Vector<int> sumGB,
        out Vector<int> color0,
        out Vector<int> color1,
        out Vector<int> selectors
    )
    {
        Vector<int> vector = new Vector<int>(16);
        Vector<float> vector2 = Vector.ConvertToSingle(vector * sumRR - sumR * sumR);
        Vector<float> vector3 = Vector.ConvertToSingle(vector * sumRG - sumR * sumG);
        Vector<float> vector4 = Vector.ConvertToSingle(vector * sumRB - sumR * sumB);
        Vector<float> vector5 = Vector.ConvertToSingle(vector * sumGG - sumG * sumG);
        Vector<float> vector6 = Vector.ConvertToSingle(vector * sumGB - sumG * sumB);
        Vector<float> vector7 = Vector.ConvertToSingle(vector * sumBB - sumB * sumB);
        Vector<float> vector8 = Vector.ConvertToSingle(maxR - minR);
        Vector<float> vector9 = Vector.ConvertToSingle(maxG - minG);
        Vector<float> vector10 = Vector.ConvertToSingle(maxB - minB);
        for (int i = 0; i < 4; i++)
        {
            Vector<float> vector11 = vector8 * vector2 + vector9 * vector3 + vector10 * vector4;
            Vector<float> vector12 = vector8 * vector3 + vector9 * vector5 + vector10 * vector6;
            Vector<float> vector13 = vector8 * vector4 + vector9 * vector6 + vector10 * vector7;
            vector8 = vector11;
            vector9 = vector12;
            vector10 = vector13;
        }
        Vector<float> vector14 =
            new Vector<float>(1024f)
            / Vector.Max(
                Vector.Max(Vector.Abs(vector8), Vector.Abs(vector9)),
                Vector.Max(Vector.Abs(vector10), new Vector<float>(0.001f))
            );
        vector8 *= vector14;
        vector9 *= vector14;
        vector10 *= vector14;
        Vector<float> vector15 =
            Vector<float>.One
            / (
                vector8 * vector8
                + vector9 * vector9
                + vector10 * vector10
                + new Vector<float>(1.25E-05f)
            );
        Vector<float> vector16 = new Vector<float>(0.0625f);
        Vector<float> vector17 = Vector.ConvertToSingle(sumR) * vector16;
        Vector<float> vector18 = Vector.ConvertToSingle(sumG) * vector16;
        Vector<float> vector19 = Vector.ConvertToSingle(sumB) * vector16;
        Vector<float> vector20 = new Vector<float>(1E+30f);
        Vector<float> vector21 = new Vector<float>(-1E+30f);
        for (int j = 0; j < 16; j++)
        {
            Vector<float> right =
                (Vector.ConvertToSingle(r[j]) - vector17) * vector8
                + (Vector.ConvertToSingle(g[j]) - vector18) * vector9
                + (Vector.ConvertToSingle(b[j]) - vector19) * vector10;
            vector20 = Vector.Min(vector20, right);
            vector21 = Vector.Max(vector21, right);
        }
        Vector<float> vector22 = vector20 * vector15;
        Vector<float> vector23 = vector21 * vector15;
        color0 = To565(
            vector17 + vector22 * vector8,
            vector18 + vector22 * vector9,
            vector19 + vector22 * vector10
        );
        color1 = To565(
            vector17 + vector23 * vector8,
            vector18 + vector23 * vector9,
            vector19 + vector23 * vector10
        );
        Separate(ref color0, ref color1);
        for (int k = 0; k < 2; k++)
        {
            LeastSquaresRound(r, g, b, ref color0, ref color1);
        }
        Separate(ref color0, ref color1);
        Vector<int> vector24 = Vector.LessThan(
            maxR - minR + (maxG - minG) + (maxB - minB),
            new Vector<int>(16)
        );
        if (!Vector.EqualsAll(vector24, Vector<int>.Zero))
        {
            SolidEndpoints(sumR, sumG, sumB, out var high, out var low);
            Separate(ref high, ref low);
            LeastSquaresRound(r, g, b, ref high, ref low);
            Separate(ref high, ref low);
            Vector<int> condition =
                vector24
                & Vector.LessThan(Error(r, g, b, high, low), Error(r, g, b, color0, color1));
            color0 = Vector.ConditionalSelect(condition, high, color0);
            color1 = Vector.ConditionalSelect(condition, low, color1);
        }
        Thresholds thresholds = MakeThresholds(color0, color1);
        selectors = Vector<int>.Zero;
        for (int l = 0; l < 16; l++)
        {
            selectors |= Vector.ShiftLeft(thresholds.Select(r[l], g[l], b[l]), l * 2);
        }
    }

    private static void LeastSquaresRound(
        Span<Vector<int>> r,
        Span<Vector<int>> g,
        Span<Vector<int>> b,
        ref Vector<int> c0,
        ref Vector<int> c1
    )
    {
        Thresholds thresholds = MakeThresholds(c0, c1);
        Vector<float> zero = Vector<float>.Zero;
        Vector<float> zero2 = Vector<float>.Zero;
        Vector<float> zero3 = Vector<float>.Zero;
        Vector<float> zero4 = Vector<float>.Zero;
        Vector<float> zero5 = Vector<float>.Zero;
        Vector<float> zero6 = Vector<float>.Zero;
        Vector<float> zero7 = Vector<float>.Zero;
        Vector<float> zero8 = Vector<float>.Zero;
        Vector<float> zero9 = Vector<float>.Zero;
        Vector<int> one = Vector<int>.One;
        Vector<int> right = new Vector<int>(2);
        Vector<float> left = new Vector<float>(1f / 3f);
        Vector<float> right2 = new Vector<float>(2f / 3f);
        for (int i = 0; i < 16; i++)
        {
            Vector<int> left2 = thresholds.Select(r[i], g[i], b[i]);
            Vector<float> vector = Vector.ConditionalSelect(
                Vector.Equals(left2, Vector<int>.Zero),
                Vector<float>.Zero,
                Vector.ConditionalSelect(
                    Vector.Equals(left2, one),
                    Vector<float>.One,
                    Vector.ConditionalSelect(Vector.Equals(left2, right), left, right2)
                )
            );
            Vector<float> vector2 = Vector<float>.One - vector;
            zero += vector2 * vector2;
            zero2 += vector2 * vector;
            zero3 += vector * vector;
            Vector<float> vector3 = Vector.ConvertToSingle(r[i]);
            Vector<float> vector4 = Vector.ConvertToSingle(g[i]);
            Vector<float> vector5 = Vector.ConvertToSingle(b[i]);
            zero4 += vector2 * vector3;
            zero5 += vector2 * vector4;
            zero6 += vector2 * vector5;
            zero7 += vector * vector3;
            zero8 += vector * vector4;
            zero9 += vector * vector5;
        }
        Vector<float> left3 = zero * zero3 - zero2 * zero2;
        Vector<float> vector6 = Vector<float>.One / Vector.Max(left3, new Vector<float>(1E-06f));
        Vector<int> left4 = To565(
            (zero4 * zero3 - zero7 * zero2) * vector6,
            (zero5 * zero3 - zero8 * zero2) * vector6,
            (zero6 * zero3 - zero9 * zero2) * vector6
        );
        Vector<int> right3 = To565(
            (zero7 * zero - zero4 * zero2) * vector6,
            (zero8 * zero - zero5 * zero2) * vector6,
            (zero9 * zero - zero6 * zero2) * vector6
        );
        Vector<int> left5 = Vector.Max(left4, right3);
        Vector<int> left6 = Vector.Min(left4, right3);
        Vector<int> condition = Vector.AsVectorInt32(
            Vector.GreaterThan(left3, new Vector<float>(1E-06f))
        );
        c0 = Vector.ConditionalSelect(condition, left5, c0);
        c1 = Vector.ConditionalSelect(condition, left6, c1);
    }

    private static Vector<int> Error(
        Span<Vector<int>> r,
        Span<Vector<int>> g,
        Span<Vector<int>> b,
        Vector<int> c0,
        Vector<int> c1
    )
    {
        PaletteColors(
            c0,
            c1,
            out var r2,
            out var g2,
            out var b2,
            out var r3,
            out var g3,
            out var b3,
            out var r4,
            out var g4,
            out var b4,
            out var r5,
            out var g5,
            out var b5
        );
        Thresholds thresholds = new Thresholds(r2, g2, b2, r3, g3, b3, r4, g4, b4, r5, g5, b5);
        Vector<int> zero = Vector<int>.Zero;
        Vector<int> one = Vector<int>.One;
        Vector<int> right = new Vector<int>(2);
        for (int i = 0; i < 16; i++)
        {
            Vector<int> left = thresholds.Select(r[i], g[i], b[i]);
            Vector<int> condition = Vector.Equals(left, Vector<int>.Zero);
            Vector<int> condition2 = Vector.Equals(left, one);
            Vector<int> condition3 = Vector.Equals(left, right);
            Vector<int> vector = Vector.ConditionalSelect(
                condition,
                r2,
                Vector.ConditionalSelect(
                    condition2,
                    r3,
                    Vector.ConditionalSelect(condition3, r4, r5)
                )
            );
            Vector<int> vector2 = Vector.ConditionalSelect(
                condition,
                g2,
                Vector.ConditionalSelect(
                    condition2,
                    g3,
                    Vector.ConditionalSelect(condition3, g4, g5)
                )
            );
            Vector<int> vector3 = Vector.ConditionalSelect(
                condition,
                b2,
                Vector.ConditionalSelect(
                    condition2,
                    b3,
                    Vector.ConditionalSelect(condition3, b4, b5)
                )
            );
            Vector<int> vector4 = r[i] - vector;
            Vector<int> vector5 = g[i] - vector2;
            Vector<int> vector6 = b[i] - vector3;
            zero += vector4 * vector4 + vector5 * vector5 + vector6 * vector6;
        }
        return zero;
    }
}
