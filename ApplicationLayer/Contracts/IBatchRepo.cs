using DomainLayer.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationLayer.Contracts
{
    public interface IBatchRepo:IGenericRepo<Batch, long>
    {

    }
}
