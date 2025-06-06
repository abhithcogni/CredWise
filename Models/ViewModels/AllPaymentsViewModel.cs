using System.Collections.Generic;

namespace CredWise_Trail.Models.ViewModels
{
    /// <summary>
    /// This model holds the complete list of payments for the AllPayments view.
    /// </summary>
    public class AllPaymentsViewModel
    {
        public List<RecentPaymentItem> Payments { get; set; }

        public AllPaymentsViewModel()
        {
            Payments = new List<RecentPaymentItem>();
        }
    }
}