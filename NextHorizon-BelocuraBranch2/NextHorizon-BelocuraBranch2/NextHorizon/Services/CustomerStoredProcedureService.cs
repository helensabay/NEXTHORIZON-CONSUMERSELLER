using Microsoft.EntityFrameworkCore;
using NextHorizon.Data;
using NextHorizon.Models;

namespace NextHorizon.Services;

public sealed class CustomerStoredProcedureService : ICustomerStoredProcedureService
{
    private readonly AppDbContext _dbContext;

    public CustomerStoredProcedureService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Customer>> GetLatestCustomersAsync(int top = 10, CancellationToken cancellationToken = default)
    {
        if (top <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(top), "Top must be greater than zero.");
        }

        return await _dbContext.Customers
            .FromSqlInterpolated($"EXEC dbo.sp_GetLatestCustomers @Top={top}")
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}
