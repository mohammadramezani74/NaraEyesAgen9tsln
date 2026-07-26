using NaraEyesAgent.Core.models.Module;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NaraEyesAgent.infrastructure.Denomination
{
    public static class GetDenomination
    {
        public static List<DenominationViewModel> GetDenominationInfo()
        {
            string filePath = @"C:\Program Files\Nextware\INI\CDM\DenominationInfo.ini";
            // لیستی برای ذخیره آبجکت‌های CST_ID
            List<DenominationViewModel> denominationInfos = new List<DenominationViewModel>();
            // چک می‌کنیم که فایل وجود داشته باشه
            if (File.Exists(filePath))
            {
            
                string currentCST_ID = string.Empty;
                int currentType = 0;
                string currentCurrencyID = string.Empty;
                int currentValues = 0;

                // خواندن فایل به خط به خط
                foreach (var line in File.ReadAllLines(filePath))
                {
                    var trimmedLine = line.Trim();

                    // پیدا کردن بخش‌های مختلف فایل
                    if (trimmedLine.StartsWith("[CST_ID"))
                    {
                        // CST_ID را استخراج می‌کنیم
                        currentCST_ID = trimmedLine;
                    }
                    else if (trimmedLine.StartsWith("Type"))
                    {
                        // نوع (Type) را استخراج می‌کنیم
                        currentType = int.Parse(trimmedLine.Split('=')[1].Trim());
                    }
                    else if (trimmedLine.StartsWith("CurrencyID"))
                    {
                        // CurrencyID را استخراج می‌کنیم
                        currentCurrencyID = trimmedLine.Split('=')[1].Trim();
                    }
                    else if (trimmedLine.StartsWith("Values"))
                    {
                        // مقدار (Values) را استخراج می‌کنیم
                        currentValues = int.Parse(trimmedLine.Split('=')[1].Trim());

                        // اطلاعات را ذخیره می‌کنیم
                        denominationInfos.Add(new DenominationViewModel
                        {
                            CST_ID = currentCST_ID,
                            Type = currentType,
                            CurrencyID = currentCurrencyID,
                            Values = currentValues
                        });
                    }
                }
            }
            return denominationInfos;
        }
    }
}
