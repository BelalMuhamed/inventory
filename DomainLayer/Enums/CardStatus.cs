using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.Enums
{
    public enum CardStatus
    {
        OnHold = 0,
        Available = 1,
        SuccessPrinted = 2,
        FailedPrinting = 3,
        Expired = 4
    }
}
