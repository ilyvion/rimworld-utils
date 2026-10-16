// Decompiled from RimWorld 1.7's Waylith.dll (Waylith.Rendering.SrgbBytes) and modified.
// See NOTICE.md in this folder.

using System;

namespace AssetPackBaker.Waylith;

internal static class SrgbBytes
{
    private const int BucketShift = 15;

    public static readonly float[] ToLinear = BuildToLinear();

    private static readonly float[] Thresholds = BuildThresholds();

    private static readonly byte[] Buckets = BuildBuckets();

    public static byte FromLinear(float linear)
    {
        int num = System.Math.Clamp(
            BitConverter.SingleToInt32Bits(linear) >> 15,
            0,
            Buckets.Length - 1
        );
        byte b = Buckets[num];
        if (b >= byte.MaxValue || !(linear >= Thresholds[b + 1]))
        {
            return b;
        }
        return (byte)(b + 1);
    }

    private static byte Encode(float linear)
    {
        float num = System.Math.Clamp(linear, 0f, 1f);
        return (byte)(
            ((num <= 0.0031308f) ? (num * 12.92f) : (1.055f * MathF.Pow(num, 5f / 12f) - 0.055f))
                * 255f
            + 0.5f
        );
    }

    private static float[] BuildToLinear()
    {
        float[] array = new float[256];
        for (int i = 0; i < 256; i++)
        {
            float num = (float)i / 255f;
            array[i] = (
                (num <= 0.04045f) ? (num / 12.92f) : MathF.Pow((num + 0.055f) / 1.055f, 2.4f)
            );
        }
        return array;
    }

    private static float[] BuildThresholds()
    {
        float[] array = new float[256];
        array[0] = float.NegativeInfinity;
        for (int i = 1; i < 256; i++)
        {
            int num = 0;
            int num2 = BitConverter.SingleToInt32Bits(1f);
            while (num < num2)
            {
                int num3 = num + (num2 - num) / 2;
                if (Encode(BitConverter.Int32BitsToSingle(num3)) >= i)
                {
                    num2 = num3;
                }
                else
                {
                    num = num3 + 1;
                }
            }
            array[i] = BitConverter.Int32BitsToSingle(num);
        }
        return array;
    }

    private static byte[] BuildBuckets()
    {
        byte[] array = new byte[(BitConverter.SingleToInt32Bits(1f) >> 15) + 1];
        for (int i = 0; i < array.Length; i++)
        {
            float num = BitConverter.Int32BitsToSingle(i << 15);
            int j;
            for (j = 0; j < 255 && num >= Thresholds[j + 1]; j++) { }
            array[i] = (byte)j;
        }
        return array;
    }
}
