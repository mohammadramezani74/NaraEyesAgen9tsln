using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using static System.Net.Mime.MediaTypeNames;

namespace NaraEyesAgent.Infrastructure.TakeScreenShot
{
    internal static class NativeScreenCapture
    {
        [DllImport("Dll3.dll", CallingConvention = CallingConvention.StdCall)]
        private static extern bool CaptureAllScreensBmp(
            out IntPtr data,
            out int size,
            out int width,
            out int height);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr ptr);

        /// <summary>
        /// اسکرین‌شات به PNG در حافظه. روی .NET 3.5 سازگار.
        /// </summary>
        public static byte[] CaptureAsPng(out int width, out int height)
        {
            width = 0; height = 0;
            IntPtr ptr = IntPtr.Zero;
            int size = 0;

            try
            {
                if (!CaptureAllScreensBmp(out ptr, out size, out width, out height) || ptr == IntPtr.Zero || size <= 0)
                    throw new InvalidOperationException("CaptureAllScreensBmp failed.");

                // کپی از بافر Native
                byte[] bmpBytes = new byte[size];
                Marshal.Copy(ptr, bmpBytes, 0, size);

                // تبدیل BMP → PNG (بدون named args)
                using (MemoryStream msIn = new MemoryStream(bmpBytes))
                using (System.Drawing.Image img = System.Drawing.Image.FromStream(msIn, false, true)) // useEmbeddedColorManagement=false, validateImageData=true
                using (MemoryStream msOut = new MemoryStream())
                {
                    img.Save(msOut, ImageFormat.Png);
                    return msOut.ToArray();
                }
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    CoTaskMemFree(ptr); // اگر Dll3 با CoTaskMemAlloc تخصیص داده
            }
        }

        /// <summary>
        /// اسکرین‌شات به JPEG کم‌حجم‌تر. quality: 0..100
        /// </summary>
        public static byte[] CaptureAsJpeg(int quality, out int width, out int height)
        {
            if (quality < 0) quality = 0; else if (quality > 100) quality = 100;

            width = 0; height = 0;
            IntPtr ptr = IntPtr.Zero;
            int size = 0;

            try
            {
                if (!CaptureAllScreensBmp(out ptr, out size, out width, out height) || ptr == IntPtr.Zero || size <= 0)
                    throw new InvalidOperationException("CaptureAllScreensBmp failed.");

                byte[] bmpBytes = new byte[size];
                Marshal.Copy(ptr, bmpBytes, 0, size);

                using (MemoryStream msIn = new MemoryStream(bmpBytes))
                using (System.Drawing.Image img = System.Drawing.Image.FromStream(msIn, false, true))
                using (MemoryStream msOut = new MemoryStream())
                {
                    ImageCodecInfo encoder = GetImageEncoder(ImageFormat.Jpeg);
                    using (EncoderParameters parms = new EncoderParameters(1))
                    {
                        parms.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, (long)quality);
                        img.Save(msOut, encoder, parms);
                    }
                    return msOut.ToArray();
                }
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                    CoTaskMemFree(ptr);
            }
        }

        private static ImageCodecInfo GetImageEncoder(ImageFormat fmt)
        {
            ImageCodecInfo[] arr = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < arr.Length; i++)
                if (arr[i].FormatID == fmt.Guid) return arr[i];
            throw new InvalidOperationException("Encoder not found: " + fmt.ToString());
        }
    }
}
