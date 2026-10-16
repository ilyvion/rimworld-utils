// Decompiled from RimWorld 1.7's Waylith.dll (Waylith.Rendering.Bc4BlockEncoder) and modified;
// derived from Basis Universal and bc7enc_rdo. See NOTICE.md in this folder.

using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace AssetPackBaker.Waylith;

internal static class Bc4BlockEncoder
{
    private struct Thresholds
    {
        public Vector<int> Bias;

        public Vector<int> T0;

        public Vector<int> T1;

        public Vector<int> T2;

        public Vector<int> T3;

        public Vector<int> T4;

        public Vector<int> T5;

        public Vector<int> T6;

        public Vector<int> Base;

        public Vector<int> Step;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Thresholds(Vector<int> r0, Vector<int> r1)
        {
            Vector<int> vector = r0 - r1;
            Bias = new Vector<int>(4) - r1 * new Vector<int>(14);
            T0 = vector * new Vector<int>(13);
            T1 = vector * new Vector<int>(11);
            T2 = vector * new Vector<int>(9);
            T3 = vector * new Vector<int>(7);
            T4 = vector * new Vector<int>(5);
            T5 = vector * new Vector<int>(3);
            T6 = vector;
            Base = r0 * new Vector<int>(65541);
            Step = vector * new Vector<int>(9363);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector<int> U(Vector<int> value14)
        {
            Vector<int> left = value14 + Bias;
            return -(
                Vector.LessThan(left, T0)
                + Vector.LessThan(left, T1)
                + Vector.LessThan(left, T2)
                + Vector.LessThan(left, T3)
                + Vector.LessThan(left, T4)
                + Vector.LessThan(left, T5)
                + Vector.LessThan(left, T6)
            );
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector<int> Reconstruct(Vector<int> u)
        {
            return Vector.ShiftRightLogical(Base - u * Step, 16);
        }
    }

    private const int LowFieldBits = 153391689;

    private const int HighFieldBits = 37449;

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
        EncodeRows(rgba, width, height, rowPitch, firstBlockRow, blockRowCount, 1, output);
    }

    internal static unsafe void EncodeRows(
        byte* rgba,
        int width,
        int height,
        int rowPitch,
        int firstBlockRow,
        int blockRowCount,
        int channels,
        byte* output
    )
    {
        int count = Vector<int>.Count;
        int num = (width + 3) / 4;
        int num2 = channels * 8;
        uint* ptr = stackalloc uint[16 * count];
        Vector<int>* ptr2 = stackalloc Vector<int>[32];
        int* lanesOut = stackalloc int[4 * count];
        for (int i = 0; i < blockRowCount; i++)
        {
            byte* ptr3 = output + (long)i * (long)num * num2;
            for (int j = 0; j < num; j += count)
            {
                int count2 = System.Math.Min(count, num - j);
                Bc1BlockEncoder.Gather(
                    rgba,
                    width,
                    height,
                    rowPitch,
                    j,
                    firstBlockRow + i,
                    num,
                    count,
                    ptr
                );
                Vector<int> vector = new Vector<int>(255);
                for (int k = 0; k < channels; k++)
                {
                    for (int l = 0; l < 16; l++)
                    {
                        ptr2[l] =
                            Vector.ShiftRightLogical(Vector.Load((int*)(ptr + l * count)), k * 8)
                            & vector;
                    }
                    EncodeChannel(
                        ptr2,
                        ptr3 + (long)j * (long)num2 + k * 8,
                        num2,
                        count2,
                        lanesOut
                    );
                }
            }
        }
    }

    private static unsafe void EncodeChannel(
        Vector<int>* values,
        byte* output,
        int blockBytes,
        int count,
        int* lanesOut
    )
    {
        Vector<int> vector = *values;
        Vector<int> vector2 = *values;
        Vector<int> vector3 = *values;
        values[16] = *values * new Vector<int>(14);
        for (int i = 1; i < 16; i++)
        {
            vector = Vector.Min(vector, values[i]);
            vector2 = Vector.Max(vector2, values[i]);
            vector3 += values[i];
            values[16 + i] = values[i] * new Vector<int>(14);
        }
        Vector<int> vector4 = Vector.Equals(vector, vector2);
        Vector<int> vector5 = Vector<int>.Zero;
        Vector<int> vector6 = Vector<int>.Zero;
        Vector<int> vector7 = Vector<int>.Zero;
        Vector<int> vector8 = Vector<int>.Zero;
        if (!Vector.EqualsAll(vector4, Vector<int>.Zero))
        {
            Vector<int> vector9 = Vector.GreaterThan(vector2, Vector<int>.Zero);
            Vector<int> vector10 = Vector<int>.One + vector9;
            vector5 = Vector.ConditionalSelect(vector9, vector2, Vector<int>.One);
            vector6 = Vector.ConditionalSelect(
                vector9,
                vector2 - Vector<int>.One,
                Vector<int>.Zero
            );
            vector7 = vector10 * new Vector<int>(153391689);
            vector8 = vector10 * new Vector<int>(37449);
        }
        if (!Vector.EqualsAll(vector4, new Vector<int>(-1)))
        {
            Vector<int> low;
            Vector<int> high;
            Vector<int> sumU;
            Vector<int> sumUSquared;
            Vector<int> sumUValue;
            Vector<int> right = EvaluateWithMoments(
                values,
                vector2,
                vector,
                out low,
                out high,
                out sumU,
                out sumUSquared,
                out sumUValue
            );
            Refit(
                new Vector<int>(112) - sumU,
                new Vector<int>(784) - new Vector<int>(14) * sumU + sumUSquared,
                sumRankValue: new Vector<int>(7) * vector3 - sumUValue,
                sum: vector3,
                r0: out var r,
                r1: out var r2,
                wellConditioned: out var wellConditioned
            );
            Vector<int> low2;
            Vector<int> high2;
            Vector<int> left = Evaluate(values, r, r2, out low2, out high2);
            Vector<int> vector11 = ~vector4;
            Vector<int> condition =
                vector11
                & wellConditioned
                & Vector.GreaterThan(r, r2)
                & Vector.LessThan(left, right);
            vector5 = Vector.ConditionalSelect(
                condition,
                r,
                Vector.ConditionalSelect(vector11, vector2, vector5)
            );
            vector6 = Vector.ConditionalSelect(
                condition,
                r2,
                Vector.ConditionalSelect(vector11, vector, vector6)
            );
            Vector<int> packed = Vector.ConditionalSelect(condition, low2, low);
            Vector<int> packed2 = Vector.ConditionalSelect(condition, high2, high);
            vector7 = Vector.ConditionalSelect(vector11, Selectors(packed, 153391689), vector7);
            vector8 = Vector.ConditionalSelect(vector11, Selectors(packed2, 37449), vector8);
        }
        int count2 = Vector<int>.Count;
        vector5.Store(lanesOut);
        vector6.Store(lanesOut + count2);
        vector7.Store(lanesOut + count2 * 2);
        vector8.Store(lanesOut + count2 * 3);
        for (int j = 0; j < count; j++)
        {
            byte* num = output + j * blockBytes;
            ulong num2 =
                (uint)lanesOut[count2 * 2 + j] | ((ulong)(uint)lanesOut[count2 * 3 + j] << 30);
            *num = (byte)lanesOut[j];
            num[1] = (byte)lanesOut[count2 + j];
            ((short*)num)[1] = (short)(ushort)num2;
            ((int*)num)[1] = (int)(num2 >> 16);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector<int> Selectors(Vector<int> packed, int fieldBits)
    {
        Vector<int> vector = new Vector<int>(fieldBits);
        Vector<int> vector2 = Vector.ShiftRightLogical(packed, 1);
        Vector<int> vector3 = Vector.ShiftRightLogical(packed, 2);
        Vector<int> left = (packed | vector2 | vector3) & vector;
        Vector<int> vector4 = packed & vector2 & vector3 & vector;
        return (Vector.AndNot(packed, vector4 * new Vector<int>(7)) | vector4)
            + Vector.AndNot(left, vector4);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector<int> Evaluate(
        Vector<int>* values,
        Vector<int> r0,
        Vector<int> r1,
        out Vector<int> low,
        out Vector<int> high
    )
    {
        Thresholds thresholds = new Thresholds(r0, r1);
        Vector<int> zero = Vector<int>.Zero;
        low = Vector<int>.Zero;
        high = Vector<int>.Zero;
        for (int i = 0; i < 10; i++)
        {
            Vector<int> vector = thresholds.U(values[16 + i]);
            low |= Vector.ShiftLeft(vector, i * 3);
            Vector<int> vector2 = values[i] - thresholds.Reconstruct(vector);
            zero += vector2 * vector2;
        }
        for (int j = 10; j < 16; j++)
        {
            Vector<int> vector3 = thresholds.U(values[16 + j]);
            high |= Vector.ShiftLeft(vector3, (j - 10) * 3);
            Vector<int> vector4 = values[j] - thresholds.Reconstruct(vector3);
            zero += vector4 * vector4;
        }
        return zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector<int> EvaluateWithMoments(
        Vector<int>* values,
        Vector<int> r0,
        Vector<int> r1,
        out Vector<int> low,
        out Vector<int> high,
        out Vector<int> sumU,
        out Vector<int> sumUSquared,
        out Vector<int> sumUValue
    )
    {
        Thresholds thresholds = new Thresholds(r0, r1);
        Vector<int> error = Vector<int>.Zero;
        low = Vector<int>.Zero;
        high = Vector<int>.Zero;
        sumU = Vector<int>.Zero;
        sumUSquared = Vector<int>.Zero;
        sumUValue = Vector<int>.Zero;
        for (int i = 0; i < 10; i++)
        {
            low |= Vector.ShiftLeft(
                Accumulate(
                    values,
                    i,
                    in thresholds,
                    ref error,
                    ref sumU,
                    ref sumUSquared,
                    ref sumUValue
                ),
                i * 3
            );
        }
        for (int j = 10; j < 16; j++)
        {
            high |= Vector.ShiftLeft(
                Accumulate(
                    values,
                    j,
                    in thresholds,
                    ref error,
                    ref sumU,
                    ref sumUSquared,
                    ref sumUValue
                ),
                (j - 10) * 3
            );
        }
        return error;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe Vector<int> Accumulate(
        Vector<int>* values,
        int i,
        in Thresholds thresholds,
        ref Vector<int> error,
        ref Vector<int> sumU,
        ref Vector<int> sumUSquared,
        ref Vector<int> sumUValue
    )
    {
        Vector<int> vector = values[i];
        Vector<int> vector2 = thresholds.U(values[16 + i]);
        Vector<int> vector3 = vector - thresholds.Reconstruct(vector2);
        error += vector3 * vector3;
        sumU += vector2;
        sumUSquared += vector2 * vector2;
        sumUValue += vector2 * vector;
        return vector2;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Refit(
        Vector<int> sumRank,
        Vector<int> sumRankSquared,
        Vector<int> sum,
        Vector<int> sumRankValue,
        out Vector<int> r0,
        out Vector<int> r1,
        out Vector<int> wellConditioned
    )
    {
        Vector<int> vector = new Vector<int>(16);
        Vector<int> vector2 = vector * sumRankSquared - sumRank * sumRank;
        Vector<int> vector3 = Vector.Equals(vector2, Vector<int>.Zero);
        wellConditioned = ~vector3;
        Vector<float> vector4 = Vector.ConvertToSingle(
            Vector.ConditionalSelect(vector3, Vector<int>.One, vector2)
        );
        Vector<float> vector5 =
            new Vector<float>(7f)
            * Vector.ConvertToSingle(vector * sumRankValue - sumRank * sum)
            / vector4;
        Vector<float> vector6 =
            vector5 * Vector.ConvertToSingle(sumRank) * new Vector<float>(1f / 7f);
        Vector<float> vector7 = (Vector.ConvertToSingle(sum) - vector6) / new Vector<float>(16f);
        Vector<int> right = new Vector<int>(255);
        r1 = Vector.Min(
            Vector.Max(Vector.ConvertToInt32(Vector.Round(vector7)), Vector<int>.Zero),
            right
        );
        r0 = Vector.Min(
            Vector.Max(Vector.ConvertToInt32(Vector.Round(vector7 + vector5)), Vector<int>.Zero),
            right
        );
    }
}
