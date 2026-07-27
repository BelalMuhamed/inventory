using ApplicationLayer.Contracts;
using DomainLayer.Entities;
using InfrastructureLayer.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfrastructureLayer.Repositories
{
    public class BatchRepo : GenericRepo<Batch, long>, IBatchRepo
    {
        private readonly AppDbContext context;

        public BatchRepo(AppDbContext context) : base(context)
        {
            this.context = context;
        }
    }
}
