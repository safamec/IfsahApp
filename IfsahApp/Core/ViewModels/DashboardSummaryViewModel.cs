namespace IfsahApp.Core.ViewModels
{
    public class DashboardSummaryViewModel
    {
        // مثال: "September 2025" أو "سبتمبر 2025"
        public string Month { get; set; } = string.Empty;

        /// <summary>
        /// إجمالي عدد الإفصاحات في هذا الشهر (مجموع كل الحالات).
        /// </summary>
        public int NumberOfDisclosures { get; set; }

        /// <summary>
        /// عدد الإفصاحات في حالة "طلب جديد".
        /// </summary>
        /// public string DateText => Date.ToString("dd/MM/yyyy");
public string DateText => Date.ToString("dd/MM/yyyy");

        public int NewRequests { get; set; }
        public DateTime Date { get; set; }  // التاريخ الحقيقي من الداتابيز

        /// <summary>
        /// عدد الإفصاحات في حالة "قيد المراجعة".
        /// </summary>
        public int UnderReview { get; set; }

        /// <summary>
        /// عدد الإفصاحات في حالة "قيد الفحص".
        /// </summary>
        public int UnderExamination { get; set; }

        /// <summary>
        /// عدد الإفصاحات في حالة "منتهي".
        /// </summary>
        public int Completed { get; set; }

        /// <summary>
        /// عدد الإفصاحات في حالة "مرفوض".
        /// </summary>
        public int Rejected { get; set; }
    }
}
