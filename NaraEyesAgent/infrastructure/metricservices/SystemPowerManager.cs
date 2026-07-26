using System;
using System.Diagnostics;
using System.Threading;


namespace NaraEyesAgent.Infrastructure.MetricServices
{
    public static class SystemPowerManager
    {

        private static int _powerActionInProgress = 0;

        /// <summary>
        /// تلاش برای ریبوت. اگر قبلاً عملیاتی در جریان باشد، false برمی‌گرداند.
        /// delaySeconds = تاخیر قبل از اجرا، cancel = لغو اختیاری
        /// </summary>
        public static bool TryRestart(int delaySeconds, CancellationToken cancel, out Exception error)
        {
            return TrySchedulePowerAction(PowerAction.Restart, delaySeconds, cancel, out error);
        }

        /// <summary>
        /// تلاش برای شات‌داون. اگر قبلاً عملیاتی در جریان باشد، false برمی‌گرداند.
        /// </summary>
        public static bool TryShutdown(int delaySeconds, CancellationToken cancel, out Exception error)
        {
            return TrySchedulePowerAction(PowerAction.Shutdown, delaySeconds, cancel, out error);
        }

        private static bool TrySchedulePowerAction(PowerAction action, int delaySeconds, CancellationToken cancel, out Exception error)
        {
            error = null;

            // فقط یک عملیات هم‌زمان
            if (Interlocked.Exchange(ref _powerActionInProgress, 1) == 1)
                return false; // عملیاتی در حال اجراست

            try
            {
                // تاخیر با قابلیت لغو
                if (delaySeconds > 0)
                {
                    if (!DelayWithCancel(delaySeconds, cancel))
                    {
                        // لغو شد
                        Interlocked.Exchange(ref _powerActionInProgress, 0);
                        return false;
                    }
                }

                string args = (action == PowerAction.Restart) ? "/r /t 0 /f" : "/s /t 0 /f";

                var psi = new ProcessStartInfo();
                psi.FileName = "shutdown.exe";
                psi.Arguments = args;
                psi.UseShellExecute = false;       // برای سرویس‌ها ضروری
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;

                Process.Start(psi);

                // موفق: سیستم در آستانه ریبوت/خاموشی است؛ نیازی به آزادکردن فلگ نیست
                // چون پروسه‌ی فعلی هم‌اکنون بسته خواهد شد.
                return true;
            }
            catch (Exception ex)
            {
                // خطا: فلگ را آزاد کن تا بعداً دوباره بتوان تلاش کرد
                Interlocked.Exchange(ref _powerActionInProgress, 0);
                error = ex;
                return false;
            }
        }

        /// <summary>
        /// تاخیر به ثانیه با چکِ لغو. true یعنی به سلامت گذشت، false یعنی لغو شد.
        /// </summary>
        private static bool DelayWithCancel(int delaySeconds, CancellationToken cancel)
        {
            if (delaySeconds <= 0) return true;
            if (cancel.IsCancellationRequested)
            {
                // فقط تاخیر ساده
                Thread.Sleep(delaySeconds * 1000);
                return true;
            }

            // تاخیر دانه‌ریز برای پاسخ‌گویی به لغو
            const int sliceMs = 200; // گرانولاریته‌ی چک
            int totalMs = delaySeconds * 1000;
            int waited = 0;

            while (waited < totalMs)
            {
                if (cancel.IsCancellationRequested) return false; // لغو شد
                int chunk = Math.Min(sliceMs, totalMs - waited);
                Thread.Sleep(chunk);
                waited += chunk;
            }
            return true;
        }

        private enum PowerAction { Restart, Shutdown }
    }

}
