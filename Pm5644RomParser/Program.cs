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

            var lumaSaturated = GenerateSaturatedLuma(lumaRaw);
            var lumaCropped = lumaSaturated.Clone(new Rectangle(144, 41, 707, 575), lumaSaturated.PixelFormat);
            lumaCropped.Save("PM5644_Luma_Inverted_Saturated_Cropped.png", ImageFormat.Png);

            // It seems to be necessary to add a couple of pixels of fudge factor to get the chroma to align
            // with the luma. Not presently sure why this is necessary.
            var lumaXOffset = -2;

            var rYexpanded = new Bitmap(rYraw, new Size(rYraw.Width * 2, rYraw.Height));
            var ryCropped = rYexpanded.Clone(new Rectangle(144 + lumaXOffset, 41, 707, 575), rYraw.PixelFormat);
            ryCropped.Save("PM5644_RminusY_Expanded.png", ImageFormat.Png);

            var bYexpanded = new Bitmap(bYraw, new Size(bYraw.Width * 2, bYraw.Height));
            var bYcropped = bYexpanded.Clone(new Rectangle(144 + lumaXOffset, 41, 707, 575), bYraw.PixelFormat);
            bYcropped.Save("PM5644_BminusY_Expanded.png", ImageFormat.Png);

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
                    comp.SetPixel(pixel, line, RGBFromPm5644YCbCr(Y.GetPixel(pixel, line).R, BY.GetPixel(pixel, line).R, RY.GetPixel(pixel, line).R));
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
        static Color SaturateY(int lum)
        {
            int luma = 256 - lum;

            luma -= 75;

            int max = 140;

            if (luma > max)
                luma = (byte)max;

            if (luma < 0)
                luma = 0;

            int color = (256 * luma) / max;

            if (color > 255)
                color = 255;

            return Color.FromArgb(color, color, color);
        }

        /// <summary>
        /// A very rough, approximate attempt to decode the colours in the PM5644's EPROMs.
        /// It gets a result that* looks* OK but ideally an actual expert in this area needs
        /// to scrutinise this and fix it because frankly I have no idea what I'm doing.
        /// </summary>
        /// <param name="Y"></param>
        /// <param name="Cb"></param>
        /// <param name="Cr"></param>
        /// <returns></returns>
        static Color RGBFromPm5644YCbCr(byte Y, byte Cb, byte Cr)
        {
            // ROM values are 0-255 theroetically 128 appears to be zero, so start from that
            float CbFactored = Cb - 128;
            float CrFactored = Cr - 128;

            // Invert polairty
            CbFactored = -CbFactored;
            CrFactored = -CrFactored;

            // fudge factor to accomodate for the present lack of pre-processing on the chroma bitmaps
            CbFactored *= 1.45f;
            CrFactored *= 1.45f;

            var yFactor = 1.0f;

            // Standard YCbCr -> RGB conversion
            float r = yFactor * Y + 0f * CbFactored + 1.402f * CrFactored;
            float g = yFactor * Y - 0.344136f * CbFactored - 0.714136f * CrFactored;
            float b = yFactor * Y + 1.772f * CbFactored + 0 * CrFactored;

            // Clip values that fall slightly out of range. This is bad but has to be done until
            // this conversion is properly understood
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
