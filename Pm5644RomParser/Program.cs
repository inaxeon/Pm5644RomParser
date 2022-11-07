using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Pm5644RomParser
{
    class Pm5644Data
    {
        public byte[] LumaROM1 { get; set; }
        public byte[] LumaROM2 { get; set; }
        public byte[] LumaROM3 { get; set; }
        public byte[] LumaROM4 { get; set; }

        public byte[] RminusYROM1 { get; set; }
        public byte[] RminusYROM2 { get; set; }

        public byte[] BminusYROM1 { get; set; }
        public byte[] BminusYROM2 { get; set; }

        public byte[] VectorROM { get; set; }

        public List<ushort> PatternVectors { get; set; }
    }

    enum PatternType
    {
        Luma,
        RminusY,
        BminusY
    }

    class Program
    {
        public delegate void PixelRenderer(Pm5644Data devData, Bitmap bitmap, int start, int finish, ref int hpixel, ref int vline);

        static void Main(string[] args)
        {
            Directory.SetCurrentDirectory("Resources");
            var currentDir = Environment.CurrentDirectory;

            // This only needs to be run once. Once PM5644_Luma_Original.png, PM5644_BminusY_Original.png
            // and PM5644_RminusY_Original.png have been generated it is no longer required.
            if (args.Count() > 0 && args[0] == "/fromeproms")
            {
                ConvertEpromsToRawBitmaps();
                return;
            }

            ConvertRawBitmapsToProcessedBitmaps();
        }

        /// <summary>
        /// Process the bitmap representations of the EPROM contents for viewing on computer screens
        /// </summary>
        static void ConvertRawBitmapsToProcessedBitmaps()
        {
            var lumaRaw = (Bitmap)Image.FromFile("PM5644_Luma_Original.png");
            var rYraw = (Bitmap)Image.FromFile("PM5644_RminusY_Original.png");
            var bYraw = (Bitmap)Image.FromFile("PM5644_BminusY_Original.png");

            var lastLine = 574;

            var lumaSaturated = GenerateSaturatedLuma(lumaRaw);
            var lumaCropped = lumaSaturated.Clone(new Rectangle(144, 41, 707, lastLine), lumaSaturated.PixelFormat);
            lumaCropped.Save("PM5644_Luma_Inverted_Saturated_Cropped.png", ImageFormat.Png);

            // It seems to be necessary to add a couple of pixels of fudge factor to get the chroma to align
            // with the luma. Not presently sure why this is necessary.
            var lumaXOffset = -2;

            var rySaturated = GenerateSaturatedChroma(rYraw, 65);
            var rYexpanded = new Bitmap(rySaturated, new Size(rySaturated.Width * 2, rySaturated.Height));
            var ryCropped = rYexpanded.Clone(new Rectangle(144 + lumaXOffset, 41, 707, lastLine), rYraw.PixelFormat);
            ryCropped.Save("PM5644_RminusY_Inverted_Saturated_Expanded_Cropped.png", ImageFormat.Png);

            var bySaturated = GenerateSaturatedChroma(bYraw, 46);
            var bYexpanded = new Bitmap(bySaturated, new Size(bySaturated.Width * 2, bySaturated.Height));
            var bYcropped = bYexpanded.Clone(new Rectangle(144 + lumaXOffset, 41, 707, lastLine), bYraw.PixelFormat);
            bYcropped.Save("PM5644_BminusY_Inverted_Saturated_Expanded_Cropped.png", ImageFormat.Png);

            var composite = GenerateComposite(lumaCropped, ryCropped, bYcropped);
            composite.Save("PM5644_Composite.png", ImageFormat.Png);
        }

        /// <summary>
        /// Generate true bit-for-bit bitmap representations of what's in the EPROMs,
        /// for interests sake, and as inputs for the next step.
        /// </summary>
        static void ConvertEpromsToRawBitmaps()
        {
            var deviceData = LoadDeviceData();
            var bitmap = GenerateBitmap(deviceData, PatternType.Luma, false);

            bitmap.Save("PM5644_Luma_Original.png", ImageFormat.Png);

            bitmap = GenerateBitmap(deviceData, PatternType.RminusY, false);

            bitmap.Save("PM5644_RminusY_Original.png", ImageFormat.Png);

            bitmap = GenerateBitmap(deviceData, PatternType.BminusY, false);

            bitmap.Save("PM5644_BminusY_Original.png", ImageFormat.Png);
        }

        static Bitmap GenerateComposite(Bitmap Y, Bitmap RY, Bitmap BY)
        {
            var comp = new Bitmap(Y.Width, Y.Height);

            for (int line = 0; line < Y.Height; line++)
            {
                for (int pixel = 0; pixel < Y.Width; pixel++)
                {
                    comp.SetPixel(pixel, line, RGBFromYCbCr(Y.GetPixel(pixel, line).R, BY.GetPixel(pixel, line).R, RY.GetPixel(pixel, line).R));
                }
            }

            return comp;
        }

        static Bitmap GenerateSaturatedLuma(Bitmap unsaturated)
        {
            var saturated = new Bitmap(unsaturated.Width, unsaturated.Height);

            for (int line = 0; line < unsaturated.Height; line++)
            {
                for (int pixel = 0; pixel < unsaturated.Width; pixel++)
                {
                    saturated.SetPixel(pixel, line, SaturateY(unsaturated.GetPixel(pixel, line).R));
                }
            }

            return saturated;
        }

        static Bitmap GenerateSaturatedChroma(Bitmap unsaturated, int range)
        {
            var saturated = new Bitmap(unsaturated.Width, unsaturated.Height);

            for (int line = 0; line < unsaturated.Height; line++)
            {
                for (int pixel = 0; pixel < unsaturated.Width; pixel++)
                {
                    saturated.SetPixel(pixel, line, SaturateChroma(unsaturated.GetPixel(pixel, line).R, range));
                }
            }

            return saturated;
        }

        static void DrawPixelsY(Pm5644Data devData, Bitmap bitmap, int start, int finish, ref int hpixel, ref int vline)
        {
            for (int i = start; i <= finish; i++)
            {
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.LumaROM1[i]));
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.LumaROM2[i]));
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.LumaROM3[i]));
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.LumaROM4[i]));
            }
        }

        static void DrawPixelsRY(Pm5644Data devData, Bitmap bitmap, int start, int finish, ref int hpixel, ref int vline)
        {
            for (int i = start; i <= finish; i++)
            {
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.RminusYROM1[i]));
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.RminusYROM2[i]));
            }
        }

        static void DrawPixelsBY(Pm5644Data devData, Bitmap bitmap, int start, int finish, ref int hpixel, ref int vline)
        {
            for (int i = start; i <= finish; i++)
            {
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.BminusYROM1[i]));
                bitmap.SetPixel(hpixel++, vline, MonochromeFromByte(devData.BminusYROM2[i]));
            }
        }

        static Color MonochromeFromByte(byte b)
        {
            return Color.FromArgb(b, b, b);
        }

        /// <summary>
        /// Inverts and fully saturates a luma pixel
        /// </summary>
        /// <param name="lum"></param>
        /// <returns></returns>
        static Color SaturateY(int romData)
        {
            //Luma range is found in a range between 41 and 181

            float adjusted = romData - 41; // Now 0-140
            adjusted = 140 - adjusted; // Invert

            adjusted *= 1.82f;

            if ((int)adjusted > 255)
                throw new InvalidDataException(); // Overshoot

            if ((int)adjusted < 0)
                adjusted = 0; // Clip the negative luminance in the black ref area in the centre of the circle;

            return Color.FromArgb((byte)adjusted, (byte)adjusted, (byte)adjusted);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="romData">The actual data from the ROM</param>
        /// <param name="range">The amount (decimal) that chrominance data is observed to deviate from 128 (0 degrees) in ROM</param>
        /// <returns></returns>
        static Color SaturateChroma(int romData, int range)
        {
            float adjusted = romData - 128;

            // The design amplitude of the chroma samples is not known. Therefore this is a manually
            // adjusted figure which was observed to reduce clipping to near-zero in RGBFromYCbCr()
            // which results in a saturation of around 75%. This matches the "Zacabeb" recreation and
            // is assumed to be what we're aiming for.
            float headroom = 32f; 

            adjusted = -adjusted;
            adjusted *= ((128 - (float)headroom) / (float)range);
            adjusted += 128;

            if (Math.Abs((float)(adjusted - 128)) > (128 - headroom))
            {
                throw new InvalidDataException(); // Overshoot
            }

            return Color.FromArgb((byte)adjusted, (byte)adjusted, (byte)adjusted);
        }

        /// <summary>
        /// ITU-R BT.601 YCbCr -> RGB conversion
        /// </summary>
        /// <param name="Y"></param>
        /// <param name="Cb"></param>
        /// <param name="Cr"></param>
        /// <returns></returns>
        static Color RGBFromYCbCr(byte Y, byte Cb, byte Cr)
        {
            float r = Y + 1.402f * (Cr - 128f);
            float g = Y - 1.772f * (0.114f / 0.587f) * (Cb - 128) - 1.402f * (0.299f / 0.587f) * (Cr - 128);
            float b = Y + 1.772f * (Cb - 128);

            // Some clipping is currently necessary for the sake of accurate colours on the colourbars.
            // This only activates on transition pixels.

            if (r < 0)
                r = 0;
            if (g < 0)
                g = 0;
            if (b < 0)
                b = 0;

            if (r > 255)
                r = 255;
            if (g > 255)
                g = 255;
            if (b > 255)
                b = 255;

            return Color.FromArgb((int)r, (int)g, (int)b);
        }

        static Pm5644Data LoadDeviceData()
        {
            var deviceData = new Pm5644Data { PatternVectors = new List<ushort>() };

            deviceData.LumaROM1 = File.ReadAllBytes("4008_102_56191.bin");
            deviceData.LumaROM2 = File.ReadAllBytes("4008_102_56201.bin");
            deviceData.LumaROM3 = File.ReadAllBytes("4008_102_56211.bin");
            deviceData.LumaROM4 = File.ReadAllBytes("4008_102_56221.bin");

            deviceData.RminusYROM1 = File.ReadAllBytes("4008_102_56241.bin");
            deviceData.RminusYROM2 = File.ReadAllBytes("4008_102_56251.bin");

            deviceData.BminusYROM1 = File.ReadAllBytes("4008_102_56261.bin");
            deviceData.BminusYROM2 = File.ReadAllBytes("4008_102_56271.bin");

            // Not required since we have an exact vector table already
            deviceData.VectorROM = File.ReadAllBytes("4008_102_56231.bin");

            // The logic which sequences the retreival of samples from the EPROMs is quite complicated.
            // Rather than try figure it out, instead it was sampled on an Agilent 16702B thus giving 
            // a true recreation of the data within.
            var vectorLines = File.ReadAllLines("pm5644_vectors.txt");

            bool ignore = false;
            foreach (var line in vectorLines)
            {
                if (line == "16505_Data_Header_Begin")
                    ignore = true;

                if (line == "16505_Data_Header_End")
                {
                    ignore = false;
                    continue;
                }

                if (ignore)
                    continue;

                var addr = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                deviceData.PatternVectors.Add(ushort.Parse(addr[0], System.Globalization.NumberStyles.HexNumber));
            }

            return deviceData;
        }

        /// <summary>
        /// Uses the vector table to draw the pattern from the ROM data
        /// </summary>
        /// <param name="data"></param>
        /// <param name="type"></param>
        /// <param name="saturate"></param>
        /// <returns></returns>
        static Bitmap GenerateBitmap(Pm5644Data data, PatternType type, bool saturate)
        {
            int hpixel = 0;
            int vline = 0;
            var numLines = 313;
            var rasterLength = 120;
            var backSpriteLength = 64;
            var frontSpriteLength = 32;
            var lineWidth = backSpriteLength + rasterLength + frontSpriteLength;
            var bitmap = new Bitmap(lineWidth * (type == PatternType.Luma ? 4 : 2), 624);
            PixelRenderer render = null;

            var idx1 = 0;
            var idx2 = idx1 + (lineWidth * numLines);

            switch (type)
            {
                case PatternType.Luma:
                    render = DrawPixelsY;
                    break;
                case PatternType.RminusY:
                    render = DrawPixelsRY;
                    break;
                case PatternType.BminusY:
                    render = DrawPixelsBY;
                    break;
            }

            for (int i = 0; i < (numLines - 1); i++)
            {
                // Draw and de-interlace at the same time

                render(data, bitmap, data.PatternVectors[idx1], data.PatternVectors[idx1 + backSpriteLength - 1], ref hpixel, ref vline);
                idx1 += backSpriteLength;
                render(data, bitmap, data.PatternVectors[idx1], data.PatternVectors[idx1 + rasterLength - 1], ref hpixel, ref vline);
                idx1 += rasterLength;
                render(data, bitmap, data.PatternVectors[idx1], data.PatternVectors[idx1 + frontSpriteLength - 1], ref hpixel, ref vline);
                idx1 += frontSpriteLength;

                vline++;
                hpixel = 0;

                render(data, bitmap, data.PatternVectors[idx2], data.PatternVectors[idx2 + backSpriteLength - 1], ref hpixel, ref vline);
                idx2 += backSpriteLength;
                render(data, bitmap, data.PatternVectors[idx2], data.PatternVectors[idx2 + rasterLength - 1], ref hpixel, ref vline);
                idx2 += rasterLength;
                render(data, bitmap, data.PatternVectors[idx2], data.PatternVectors[idx2 + frontSpriteLength - 1], ref hpixel, ref vline);
                idx2 += frontSpriteLength;

                vline++;
                hpixel = 0;
            }

            return bitmap;
        }
    }
}
