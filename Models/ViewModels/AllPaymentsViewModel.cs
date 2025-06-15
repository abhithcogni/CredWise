using System.Collections.Generic;

namespace CredWise_Trail.Models.ViewModels
{
    public class AllPaymentsViewModel
    {
        public List<RecentPaymentItem> Payments { get; set; }

        //This is to prevent NullReferenceException
        public AllPaymentsViewModel()
        {
            Payments = new List<RecentPaymentItem>();
        }
    }
}