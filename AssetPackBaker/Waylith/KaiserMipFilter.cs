// Decompiled from RimWorld 1.7's Waylith.dll (Waylith.Rendering.KaiserMipFilter) and modified.
// See NOTICE.md in this folder.

using System;
using System.Numerics;
using System.Threading.Tasks;

namespace AssetPackBaker.Waylith;

internal static class KaiserMipFilter
{
    private sealed class Taps
    {
        public readonly int[] Start;

        public readonly int[] Count;

        public readonly float[] Weights;

        public readonly int[] Index;

        public readonly int MaxCount;

        public Taps(int sourceSize, int destinationSize)
        {
            float num = System.Math.Max(1f, (float)sourceSize / (float)destinationSize);
            float num2 = 2f * num;
            MaxCount = (int)MathF.Ceiling(num2 * 2f) + 1;
            Start = new int[destinationSize];
            Count = new int[destinationSize];
            Weights = new float[destinationSize * MaxCount];
            Index = new int[destinationSize * MaxCount];
            for (int i = 0; i < destinationSize; i++)
            {
                float num3 = ((float)i + 0.5f) * (float)sourceSize / (float)destinationSize - 0.5f;
                int num4 = (int)MathF.Ceiling(num3 - num2);
                int num5 = (int)MathF.Floor(num3 + num2);
                int num6 = System.Math.Min(MaxCount, num5 - num4 + 1);
                float num7 = 0f;
                for (int j = 0; j < num6; j++)
                {
                    float num8 = Kaiser(((float)(num4 + j) - num3) / num);
                    Weights[i * MaxCount + j] = num8;
                    Index[i * MaxCount + j] = System.Math.Clamp(num4 + j, 0, sourceSize - 1);
                    num7 += num8;
                }
                for (int k = 0; k < num6; k++)
                {
                    Weights[i * MaxCount + k] /= num7;
                }
                Start[i] = num4;
                Count[i] = num6;
            }
        }
    }

    private const float FilterWidth = 2f;

    private const float KaiserAlpha = 4f;

    private const int RowsPerChunk = 32;

    internal static byte Unpremultiply(byte color, byte alpha)
    {
        if (alpha != 0)
        {
            return (byte)System.Math.Min(255, (color * 255 + alpha / 2) / alpha);
        }
        return color;
    }

    internal static byte Premultiply(byte color, byte alpha)
    {
        return (byte)((color * alpha + 127) / 255);
    }

    public static void Downsample(
        byte[] source,
        int sourceWidth,
        int sourceHeight,
        byte[] destination,
        int destinationWidth,
        int destinationHeight,
        bool parallel = true,
        bool premultiplied = false
    )
    {
        Taps taps = new Taps(sourceWidth, destinationWidth);
        Taps rowTaps = new Taps(sourceHeight, destinationHeight);
        int toExclusive = (destinationHeight + 32 - 1) / 32;
        Parallel.For(
            0,
            toExclusive,
            new ParallelOptions { MaxDegreeOfParallelism = ((!parallel) ? 1 : (-1)) },
            delegate(int chunk)
            {
                Vector4[][] array = new Vector4[rowTaps.MaxCount][];
                int[] array2 = new int[rowTaps.MaxCount];
                for (int i = 0; i < array.Length; i++)
                {
                    array[i] = new Vector4[destinationWidth];
                    array2[i] = -1;
                }
                Vector4[] linear = new Vector4[sourceWidth];
                Vector4[] array3 = new Vector4[destinationWidth];
                int num = System.Math.Min(destinationHeight, (chunk + 1) * 32);
                for (int j = chunk * 32; j < num; j++)
                {
                    int num2 = rowTaps.Start[j];
                    int num3 = rowTaps.Count[j];
                    for (int k = 0; k < num3; k++)
                    {
                        int num4 = System.Math.Clamp(num2 + k, 0, sourceHeight - 1);
                        int num5 = num4 % array.Length;
                        if (array2[num5] != num4)
                        {
                            FilterRow(
                                source.AsSpan(num4 * sourceWidth * 4, sourceWidth * 4),
                                linear,
                                taps,
                                array[num5],
                                premultiplied
                            );
                            array2[num5] = num4;
                        }
                    }
                    Array.Clear(array3);
                    for (int l = 0; l < num3; l++)
                    {
                        Vector4[] array4 = array[
                            System.Math.Clamp(num2 + l, 0, sourceHeight - 1) % array.Length
                        ];
                        float num6 = rowTaps.Weights[j * rowTaps.MaxCount + l];
                        for (int m = 0; m < array3.Length; m++)
                        {
                            array3[m] += array4[m] * num6;
                        }
                    }
                    Span<byte> span = destination.AsSpan(
                        j * destinationWidth * 4,
                        destinationWidth * 4
                    );
                    for (int n = 0; n < array3.Length; n++)
                    {
                        Vector4 vector = array3[n];
                        byte b = (byte)(System.Math.Clamp(vector.W, 0f, 1f) * 255f + 0.5f);
                        if (premultiplied)
                        {
                            Vector4 vector2 = ((b == 0) ? Vector4.Zero : (vector / vector.W));
                            span[n * 4] = Premultiply(SrgbBytes.FromLinear(vector2.X), b);
                            span[n * 4 + 1] = Premultiply(SrgbBytes.FromLinear(vector2.Y), b);
                            span[n * 4 + 2] = Premultiply(SrgbBytes.FromLinear(vector2.Z), b);
                        }
                        else
                        {
                            span[n * 4] = SrgbBytes.FromLinear(vector.X);
                            span[n * 4 + 1] = SrgbBytes.FromLinear(vector.Y);
                            span[n * 4 + 2] = SrgbBytes.FromLinear(vector.Z);
                        }
                        span[n * 4 + 3] = b;
                    }
                }
            }
        );
    }

    private static void FilterRow(
        ReadOnlySpan<byte> source,
        Vector4[] linear,
        Taps taps,
        Vector4[] output,
        bool premultiplied
    )
    {
        float[] toLinear = SrgbBytes.ToLinear;
        if (premultiplied)
        {
            for (int i = 0; i < linear.Length; i++)
            {
                byte b = source[i * 4 + 3];
                float num = (float)(int)b * 0.003921569f;
                linear[i] = new Vector4(
                    toLinear[Unpremultiply(source[i * 4], b)] * num,
                    toLinear[Unpremultiply(source[i * 4 + 1], b)] * num,
                    toLinear[Unpremultiply(source[i * 4 + 2], b)] * num,
                    num
                );
            }
        }
        else
        {
            for (int j = 0; j < linear.Length; j++)
            {
                linear[j] = new Vector4(
                    toLinear[source[j * 4]],
                    toLinear[source[j * 4 + 1]],
                    toLinear[source[j * 4 + 2]],
                    (float)(int)source[j * 4 + 3] * 0.003921569f
                );
            }
        }
        for (int k = 0; k < output.Length; k++)
        {
            Vector4 zero = Vector4.Zero;
            Span<float> span = taps.Weights.AsSpan(k * taps.MaxCount, taps.Count[k]);
            Span<int> span2 = taps.Index.AsSpan(k * taps.MaxCount, span.Length);
            for (int l = 0; l < span.Length; l++)
            {
                zero += linear[span2[l]] * span[l];
            }
            output[k] = zero;
        }
    }

    private static float Kaiser(float x)
    {
        float num = x / 2f;
        float num2 = 1f - num * num;
        if (num2 <= 0f)
        {
            return 0f;
        }
        return Sinc((float)System.Math.PI * x) * Bessel0(4f * MathF.Sqrt(num2)) / Bessel0(4f);
    }

    private static float Sinc(float x)
    {
        if (!(MathF.Abs(x) < 0.0001f))
        {
            return MathF.Sin(x) / x;
        }
        return 1f;
    }

    private static float Bessel0(float x)
    {
        float num = 1f;
        float num2 = 1f;
        float num3 = x * 0.5f;
        for (int i = 1; i < 24; i++)
        {
            num2 *= num3 / (float)i * (num3 / (float)i);
            num += num2;
            if (num2 < 1E-07f * num)
            {
                break;
            }
        }
        return num;
    }
}
